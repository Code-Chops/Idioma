using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Reports a call that asks for the text of an enum member by its value, where an earlier member has that value too
/// (<c>LOC600</c>). <c>OrderStatus.None.GetText()</c> next to <c>Default = 0</c> gives the text of <c>Default</c>.
/// </summary>
/// <remarks>
/// <para>
/// <c>GetText()</c> and <c>GetTextByValue</c> find a text by the value, and a value can't tell two members apart. So
/// each value gives the text of the first member with that value. A call that names a later member in the code gets
/// another member's text, and the code reads as if it gets its own.
/// </para>
/// <para>
/// A value the code only has at run time, such as <c>order.Status.GetText()</c>, isn't reported, because no member is
/// named there. The value of a member that has no text of its own isn't reported either. That's a member without
/// <c>[Localized]</c>, such as <c>Default = Active</c>, which shows the text of the marked member.
/// </para>
/// <para>
/// For an enum with <c>[Localized]</c> members, only an enum of this assembly is checked, because the attribute isn't
/// kept in a compiled assembly.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SharedEnumValueAnalyzer : DiagnosticAnalyzer
{
    public const string SharedValueDiagnosticId = "LOC600";

    /// <summary>
    /// The key of the property of a diagnostic that holds the member's own text as code at the call, such as
    /// <c>OrderStatusLocalization.None</c>. Only a member of a set of the enum has one, which the code fix writes.
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
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(Start);
    }

    private static void Start(CompilationStartAnalysisContext context)
    {
        // Null in a project that doesn't reference the package, which has no texts.
        if (context.Compilation.GetTypeByMetadataName(PackageNames.LocalizableText) is null)
            return;

        context.RegisterOperationAction(Analyze, OperationKind.Invocation);
    }

    private static void Analyze(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;

        if (method.Name is not (LocalizationSetsAnalysis.LookupName or LocalizationSetsAnalysis.ExtensionName)
            || method.ReturnType.SpecialType is not SpecialType.System_String)
        {
            return;
        }

        // The value: what GetText() is called on, or what GetTextByValue takes. An extension method takes it as its
        // first argument.
        var value = invocation.Instance ?? (invocation.Arguments is [var argument] ? argument.Value : null);

        while (value is IConversionOperation { IsImplicit: true } conversion)
            value = conversion.Operand;

        if (value is not IFieldReferenceOperation { Field: { HasConstantValue: true, ContainingType: { TypeKind: TypeKind.Enum } @enum } member })
            return;

        // An extension member's method lies in a block inside the static class that holds it.
        for (var holder = method.ContainingType; holder is not null; holder = holder.ContainingType)
        {
            if (KeptTextAnalyzer.IsGeneratedClass(holder))
            {
                AnalyzeSetLookup(context, invocation, value, holder, @enum, member);
                return;
            }

            // The class the source generator writes for an enum with [Localized] members.
            if (String.Equals(method.Name, LocalizedMembers.ExtensionName, StringComparison.Ordinal)
                && holder.Name.EndsWith(LocalizedMembers.ExtensionHolderSuffix, StringComparison.Ordinal))
            {
                AnalyzeLocalizedLookup(context, invocation, value, @enum, member);
                return;
            }
        }
    }

    /// <summary>
    /// Reports a call of the generated class of a set of the enum that names a member whose value an earlier member
    /// has. Every member has a text in the set, so the fix asks the generated class for the member's own:
    /// <c>OrderStatusLocalization.None</c>.
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
        var ownText = $"{className}.{LocalizationSetsPart.EscapeIdentifier(member.Name)}";

        Report(context, invocation, value, @enum, first, member, ownText, ImmutableDictionary<string, string?>.Empty.Add(OwnTextProperty, ownText));
    }

    /// <summary>
    /// Reports a call of the generated <c>GetText()</c> of an enum with <c>[Localized]</c> members that names a marked
    /// member whose value an earlier marked member has. <c>LocalizableText.GetTextByMember</c> finds the member's own
    /// text.
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
        var ownText = $"LocalizableText.GetTextByMember<{enumName}>(nameof({enumName}.{LocalizationSetsPart.EscapeIdentifier(member.Name)}))";

        Report(context, invocation, value, @enum, first, member, ownText, ImmutableDictionary<string, string?>.Empty);

        bool IsMarked(IFieldSymbol field) =>
            field.GetAttributes().Any(attribute => LocalizedMembers.IsGeneratedAttribute(attribute.AttributeClass, assembly));
    }

    /// <summary>
    /// The first member of the enum with the value of <paramref name="member"/> that <paramref name="hasText"/> holds
    /// for, when that's another member. It's <see langword="null"/> when <paramref name="member"/> is the first itself.
    /// </summary>
    // In the order the members are declared, which is the order the source generator reads them in.
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
    /// Reports the call on the member it names, with the whole call as the place for the fix to replace.
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
