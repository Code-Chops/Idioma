using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Reports a call that asks for the text of an enum member by its value, where an earlier member has that value too
/// (<c>LOC600</c>). <c>OrderStatus.None.GetText()</c> next to <c>Default = 0</c> gives the text of <c>Default</c>.
/// </summary>
/// <remarks>
/// A text is found by value, so each value gives the first member's text. A value known only at run time isn't
/// reported, and neither is an unmarked member. For an enum with <c>[Localized]</c> members, only an enum of this
/// assembly is checked, because the attribute isn't kept in a compiled assembly.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SharedEnumValueAnalyzer : DiagnosticAnalyzer
{
    public const string SharedValueDiagnosticId = "LOC600";

    /// <summary>
    /// The diagnostic property with code for the member's own text, such as <c>OrderStatusLocalization.None</c>. Only
    /// a member of an enum with a set has one, which the code fix writes.
    /// </summary>
    public const string OwnTextProperty = "OwnText";

    private static readonly DiagnosticDescriptor SharedValueRule = new(
        id: SharedValueDiagnosticId,
        title: "Text of an enum member asked for by a value another member has",
        messageFormat: "{0} gives the text of '{1}' for '{2}', because the two have the same value. Use '{3}' for the text of '{4}'.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "GetText() and GetTextByValue find a text by the value, and a value can't tell two members apart. So a value gives the text of the first member with that value. A call that names a later member gets the text of that first member. A value the code only has at run time isn't reported, because no member is named there.",
        helpLinkUri: PackageNames.HelpLink(SharedValueDiagnosticId));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [SharedValueRule];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();

        // Razor's output counts as generated code, and a call written in markup must be read there.
        // AnalyzerSupport.IsHandWritten leaves out the rest.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.RegisterCompilationStartAction(Start);
    }

    private static void Start(CompilationStartAnalysisContext context)
    {
        if (context.Compilation.GetTypeByMetadataName(PackageNames.LocalizedText) is null)
            return;

        context.RegisterOperationAction(Analyze, OperationKind.Invocation);
    }

    private static void Analyze(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;

        if (method.Name is not (LocalizationSetsAnalysis.LookupName or LocalizationSetsAnalysis.ExtensionName)
            || method.ReturnType.SpecialType is not SpecialType.System_String
            || !AnalyzerSupport.IsHandWritten(context.IsGeneratedCode, invocation.Syntax.SyntaxTree))
        {
            return;
        }

        // An extension method takes the value as its first argument.
        var value = invocation.Instance ?? (invocation.Arguments is [var argument] ? argument.Value : null);

        while (value is IConversionOperation { IsImplicit: true } conversion)
            value = conversion.Operand;

        if (value is not IFieldReferenceOperation { Field: { HasConstantValue: true, ContainingType: { TypeKind: TypeKind.Enum } @enum } member })
            return;

        // An extension member's method lies in a block inside the static class that holds it.
        if (AnalyzerSupport.GetContainingTypes(method).FirstOrDefault(KeptTextAnalyzer.IsGeneratedClass) is { } generatedClass)
            AnalyzeSetLookup(context, invocation, value, generatedClass, @enum, member);
        else if (AnalyzerSupport.IsGetTextOfLocalizedMembers(method))
            AnalyzeLocalizedLookup(context, invocation, value, @enum, member);
    }

    /// <summary>
    /// Reports a lookup through the generated class of a set of the enum. Every member has a text there, so the fix
    /// reads it directly.
    /// </summary>
    private static void AnalyzeSetLookup(OperationAnalysisContext context, IInvocationOperation invocation, IOperation value,
        INamedTypeSymbol generatedClass, INamedTypeSymbol @enum, IFieldSymbol member)
    {
        if (FindFirstOfValue(@enum, member, static _ => true) is not { } first
            || !generatedClass.GetMembers(member.Name).Any(static text => text is IPropertySymbol { IsStatic: true }))
        {
            return;
        }

        var className = generatedClass.ToMinimalDisplayString(invocation.SemanticModel!, invocation.Syntax.SpanStart);
        var ownText = $"{className}.{CodeEscaping.EscapeIdentifier(member.Name)}";

        Report(context, invocation, value, @enum, first, member, ownText, ImmutableDictionary<string, string?>.Empty.Add(OwnTextProperty, ownText));
    }

    /// <summary>
    /// Reports a generated <c>GetText()</c> of an enum with <c>[Localized]</c> members. The advice uses
    /// <c>LocalizedText.GetTextByMember</c>.
    /// </summary>
    private static void AnalyzeLocalizedLookup(OperationAnalysisContext context, IInvocationOperation invocation, IOperation value,
        INamedTypeSymbol @enum, IFieldSymbol member)
    {
        var assembly = context.Compilation.Assembly;

        if (!SymbolEqualityComparer.Default.Equals(@enum.ContainingAssembly, assembly)
            || !IsMarked(member)
            || FindFirstOfValue(@enum, member, IsMarked) is not { } first)
        {
            return;
        }

        var enumName = @enum.ToMinimalDisplayString(invocation.SemanticModel!, invocation.Syntax.SpanStart);
        var ownText = $"LocalizedText.GetTextByMember<{enumName}>(nameof({enumName}.{CodeEscaping.EscapeIdentifier(member.Name)}))";

        Report(context, invocation, value, @enum, first, member, ownText, ImmutableDictionary<string, string?>.Empty);

        bool IsMarked(IFieldSymbol field) =>
            field.GetAttributes().Any(attribute => LocalizedMembers.IsGeneratedAttribute(attribute.AttributeClass, assembly));
    }

    /// <summary>
    /// The earlier member with a text and the same value, or <see langword="null"/> when <paramref name="member"/> is
    /// the first.
    /// </summary>
    // In declaration order, as the source generator reads them.
    private static IFieldSymbol? FindFirstOfValue(INamedTypeSymbol @enum, IFieldSymbol member, Func<IFieldSymbol, bool> hasText)
    {
        foreach (var candidate in @enum.GetMembers())
        {
            if (candidate is not IFieldSymbol { HasConstantValue: true } field || !hasText(field))
                continue;

            if (SymbolEqualityComparer.Default.Equals(field, member))
                return null;

            if (Equals(field.ConstantValue, member.ConstantValue))
                return field;
        }

        return null;
    }

    /// <summary>
    /// Reports on the member, with the whole call as the additional location the fix replaces.
    /// </summary>
    private static void Report(OperationAnalysisContext context, IInvocationOperation invocation, IOperation value, INamedTypeSymbol @enum,
        IFieldSymbol first, IFieldSymbol member, string ownText, ImmutableDictionary<string, string?> properties)
    {
        var call = invocation.TargetMethod.Name is LocalizationSetsAnalysis.LookupName
            ? LocalizationSetsAnalysis.LookupName
            : $"{LocalizationSetsAnalysis.ExtensionName}()";

        context.ReportDiagnostic(Diagnostic.Create(
            SharedValueRule,
            value.Syntax.GetLocation(),
            additionalLocations: [invocation.Syntax.GetLocation()],
            properties: properties,
            call, $"{@enum.Name}.{first.Name}", $"{@enum.Name}.{member.Name}", ownText, member.Name));
    }
}
