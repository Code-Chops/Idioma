using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Closes what the generated factory can't in an assembly that declares <c>[assembly: SupportedLanguages(...)]</c>:
/// <c>LOC500</c> for wording written in one language, and <c>LOC501</c> for a language argument without its name, of
/// <c>LocalizableText.Inline</c> or of <c>[Localized]</c>. In an assembly without the attribute, it reports a call of
/// <c>LocalizableText.Inline</c> (<c>LOC100</c>), which the compiler alone reports as a method that doesn't exist. The
/// generator reports a member marked <c>[Localized]</c> there (<see cref="LocalizableTextFactoryGenerator"/>), because
/// the compiler stops at an attribute that doesn't exist before any analyzer runs.
/// </summary>
/// <remarks>
/// Wording means text written in the source: a constant holding a letter, also inside an interpolation, a concatenation,
/// a conditional, a coalesce, or a switch. A runtime value (a customer's name) converts to a text as it always did,
/// because it has no translations to demand. The build can't tell it from wording kept in a local variable, which
/// therefore passes too. Generated code is left alone, as its author can't fix a report on it, except what Razor
/// generates from a <c>.razor</c> or <c>.cshtml</c> file, which is the user's own markup and maps back to it. So are the
/// initializer and the getter of a property marked <c>[ExcludeFromLocalizationDiscovery]</c>, whose author marked its
/// wording as the same in every language, such as a name.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OneLanguageTextAnalyzer : DiagnosticAnalyzer
{
    public const string OneLanguageDiagnosticId = "LOC500";
    public const string UnnamedLanguageDiagnosticId = "LOC501";

    /// <summary>
    /// The code a set in an assembly without languages gets too (<see cref="LocalizationSetsPart"/>).
    /// </summary>
    public const string NoLanguagesDeclaredDiagnosticId = LocalizationSetsPart.NoLanguagesDeclaredDiagnosticId;

    /// <summary>
    /// The property of <c>LOC501</c> that holds the language of each argument of the call or the attribute, in the
    /// order they're written, separated by commas: <c>en,nl</c>. Its code fix names the arguments from it, also in a
    /// workspace that hasn't run the generator that writes <c>LocalizableText.Inline</c> and <c>[Localized]</c>.
    /// </summary>
    public const string ParameterNamesProperty = "ParameterNames";

    /// <summary>
    /// The factory of a text with a key, <c>LocalizableText.Keyed(key, defaultText)</c>.
    /// </summary>
    private const string KeyedName = "Keyed";

    private static readonly DiagnosticDescriptor OneLanguageRule = new(
        id: OneLanguageDiagnosticId,
        title: "Text written in one language",
        messageFormat: "This text is in one language, but this assembly supports {0}. Write it as LocalizableText.Inline({1}).",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every text must show in every language of [assembly: SupportedLanguages]. A string converts to a text, which suits a value such as a customer's name. But wording written in the source would show in this one language to everybody. For wording that reads the same in every language, such as a name, mark the property that holds it [ExcludeFromLocalizationDiscovery]. Its initializer and its getter aren't checked then.",
        helpLinkUri: PackageNames.HelpLink(OneLanguageDiagnosticId));

    private static readonly DiagnosticDescriptor UnnamedLanguageRule = new(
        id: UnnamedLanguageDiagnosticId,
        title: "Language argument without its name",
        messageFormat: "Name the language of this wording: {0}. Without names, two languages can swap unnoticed.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every parameter of LocalizableText.Inline and of [Localized] is a string, so wording in the wrong position compiles and shows in the wrong language.",
        helpLinkUri: PackageNames.HelpLink(UnnamedLanguageDiagnosticId));

    // The code of a set in an assembly without languages, which the generator reports: both say that the attribute is
    // missing, and both have the same fix. So the code is shared on purpose.
#pragma warning disable RS1019 // Diagnostic Id is already used by another analyzer
    private static readonly DiagnosticDescriptor InlineWithoutLanguagesRule = new(
        id: NoLanguagesDeclaredDiagnosticId,
        title: "Inline text in an assembly without languages",
        messageFormat: "{0} takes a wording per language, but this assembly declares no languages. Add [assembly: SupportedLanguages(\"en\", \"nl\")] with your languages, the default first.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The source generator writes LocalizableText.Inline with one parameter per language of [assembly: SupportedLanguages]. Without the attribute it writes none, so the compiler finds no such method (CS0117).",
        helpLinkUri: PackageNames.HelpLink(NoLanguagesDeclaredDiagnosticId));
#pragma warning restore RS1019

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [OneLanguageRule, UnnamedLanguageRule, InlineWithoutLanguagesRule];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();

        // Razor's output counts as generated code, and a text written in markup must be caught there. IsHandWritten
        // leaves out the rest.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.RegisterCompilationStartAction(Start);
    }

    private static void Start(CompilationStartAnalysisContext context)
    {
        var codes = SupportedLanguages.ReadCodes(context.Compilation.Assembly);
        var text = context.Compilation.GetTypeByMetadataName(PackageNames.LocalizableText);

        if (text is null)
            return;

        if (codes.Length is 0)
        {
            // Without languages there is no factory, and the compiler only says that LocalizableText has no Inline.
            context.RegisterSyntaxNodeAction(c => AnalyzeInlineWithoutLanguages(c, text), SyntaxKind.InvocationExpression);
            return;
        }

        var languages = String.Join(", ", codes);
        var sample = String.Join(", ", codes.Select(static code => $"{SupportedLanguages.ToParameterName(code)}: \"...\""));

        // One language has nothing to be translated into, so wording in one language is no mistake then.
        var checksWording = codes.Length >= 2;

        // Null in a package without the attribute, where no property is left alone.
        var excluded = checksWording ? context.Compilation.GetTypeByMetadataName(PackageNames.ExcludeFromLocalizationDiscoveryAttribute) : null;

        // One action for every call in the compilation: the names of the factory's arguments, and the wording of Keyed.
        context.RegisterOperationAction(c =>
        {
            AnalyzeInvocation(c, sample);

            if (checksWording)
                AnalyzeKeyed(c, text, excluded, languages, sample);
        }, OperationKind.Invocation);

        context.RegisterSyntaxNodeAction(c => AnalyzeLocalized(c, sample), SyntaxKind.Attribute);

        if (!checksWording)
            return;

        context.RegisterOperationAction(c => AnalyzeConversion(c, text, excluded, languages, sample), OperationKind.Conversion);
        context.RegisterOperationAction(c => AnalyzeCompoundAssignment(c, text, excluded, languages, sample), OperationKind.CompoundAssignment);
        context.RegisterOperationAction(c => AnalyzeCreation(c, text, excluded, languages, sample), OperationKind.ObjectCreation);
    }

    /// <summary>
    /// Reports a call of <c>LocalizableText.Inline</c> that binds to nothing, in an assembly that declares no languages.
    /// </summary>
    private static void AnalyzeInlineWithoutLanguages(SyntaxNodeAnalysisContext context, INamedTypeSymbol text)
    {
        // By its shape first, which binds nothing: nearly every call is another one.
        if (context.Node is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } invocation
            || !String.Equals(access.Name.Identifier.ValueText, LocalizableTextFactoryGenerator.FactoryName, StringComparison.Ordinal)
            || !IsHandWritten(context.IsGeneratedCode, invocation.SyntaxTree))
        {
            return;
        }

        // Only on the text type, and only where nothing of that name exists, such as an extension of the consumer.
        if (context.SemanticModel.GetSymbolInfo(access.Expression, context.CancellationToken).Symbol is INamedTypeSymbol receiver
            && SymbolEqualityComparer.Default.Equals(receiver, text)
            && context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken) is { Symbol: null, CandidateSymbols.IsEmpty: true })
        {
            context.ReportDiagnostic(Diagnostic.Create(InlineWithoutLanguagesRule, access.Name.GetLocation(), $"{PackageNames.LocalizableTextName}.{LocalizableTextFactoryGenerator.FactoryName}"));
        }
    }

    /// <summary>
    /// Reports the first argument of <c>[Localized]</c> without the name of its language (<c>LOC501</c>).
    /// </summary>
    private static void AnalyzeLocalized(SyntaxNodeAnalysisContext context, string sample)
    {
        var attribute = (AttributeSyntax)context.Node;

        if (!LocalizedMembers.IsNamedLocalized(attribute)
            || attribute.ArgumentList is not { Arguments: var arguments }
            || !IsHandWritten(context.IsGeneratedCode, attribute.SyntaxTree)
            || context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol is not IMethodSymbol constructor
            || !LocalizedMembers.IsGeneratedAttribute(constructor.ContainingType, context.Compilation.Assembly))
        {
            return;
        }

        foreach (var argument in arguments)
        {
            if (argument is not { NameColon: null, NameEquals: null })
                continue;

            var properties = FindParameterNames(arguments, constructor) is { } names
                ? ImmutableDictionary<string, string?>.Empty.Add(ParameterNamesProperty, names)
                : null;

            context.ReportDiagnostic(Diagnostic.Create(UnnamedLanguageRule, argument.GetLocation(), properties, $"[{LocalizedMembers.AttributeName}({sample})]"));
            return;
        }
    }

    /// <summary>
    /// The parameter of each argument of an attribute, in the order the arguments are written, separated by commas, or
    /// <see langword="null"/> when an argument has no parameter. An argument without a name takes the parameter of
    /// its place.
    /// </summary>
    private static string? FindParameterNames(SeparatedSyntaxList<AttributeArgumentSyntax> arguments, IMethodSymbol constructor)
    {
        var names = new string[arguments.Count];

        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i].NameColon is { } nameColon)
                names[i] = nameColon.Name.Identifier.ValueText;
            else if (arguments[i].NameEquals is null && i < constructor.Parameters.Length)
                names[i] = constructor.Parameters[i].Name;
            else
                return null;
        }

        var parameterNames = String.Join(",", names);
        return parameterNames;
    }

    private static void AnalyzeConversion(OperationAnalysisContext context, INamedTypeSymbol text, INamedTypeSymbol? excluded,
        string languages, string sample)
    {
        var conversion = (IConversionOperation)context.Operation;

        if (conversion.OperatorMethod is { Parameters.Length: 1 } method
            && SymbolEqualityComparer.Default.Equals(method.ContainingType, text)
            && method.Parameters[0].Type.SpecialType is SpecialType.System_String
            && HoldsWording(conversion.Operand)
            && IsChecked(context, excluded))
        {
            context.ReportDiagnostic(Diagnostic.Create(OneLanguageRule, conversion.Syntax.GetLocation(), languages, sample));
        }
    }

    /// <summary>
    /// Reports wording added to a text, as in <c>title += " (draft)";</c>. The text becomes a string, the wording is
    /// added, and the result converts back to a text in one language.
    /// </summary>
    // The conversion back is no operation of its own, so AnalyzeConversion never sees it.
    private static void AnalyzeCompoundAssignment(OperationAnalysisContext context, INamedTypeSymbol text, INamedTypeSymbol? excluded,
        string languages, string sample)
    {
        var assignment = (ICompoundAssignmentOperation)context.Operation;

        if (assignment.OutConversion.MethodSymbol is { Parameters.Length: 1 } method
            && SymbolEqualityComparer.Default.Equals(method.ContainingType, text)
            && method.Parameters[0].Type.SpecialType is SpecialType.System_String
            && HoldsWording(assignment.Value)
            && IsChecked(context, excluded))
        {
            context.ReportDiagnostic(Diagnostic.Create(OneLanguageRule, assignment.Value.Syntax.GetLocation(), languages, sample));
        }
    }

    private static void AnalyzeCreation(OperationAnalysisContext context, INamedTypeSymbol text, INamedTypeSymbol? excluded,
        string languages, string sample)
    {
        var creation = (IObjectCreationOperation)context.Operation;

        if (!SymbolEqualityComparer.Default.Equals(creation.Type, text))
            return;

        if (IsWordingWritten(context, excluded, creation.Arguments))
            context.ReportDiagnostic(Diagnostic.Create(OneLanguageRule, creation.Syntax.GetLocation(), languages, sample));
    }

    /// <summary>
    /// <c>LocalizableText.Keyed(key, wording)</c>: the key is no wording, but the default text is.
    /// </summary>
    private static void AnalyzeKeyed(OperationAnalysisContext context, INamedTypeSymbol text, INamedTypeSymbol? excluded,
        string languages, string sample)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;

        if (!String.Equals(method.Name, KeyedName, StringComparison.Ordinal) || !SymbolEqualityComparer.Default.Equals(method.ContainingType, text))
            return;

        if (IsWordingWritten(context, excluded, invocation.Arguments))
            context.ReportDiagnostic(Diagnostic.Create(OneLanguageRule, invocation.Syntax.GetLocation(), languages, sample));
    }

    /// <summary>
    /// Whether the <c>defaultText</c> argument of a call is wording that LOC500 checks (<see cref="IsChecked"/>).
    /// </summary>
    private static bool IsWordingWritten(OperationAnalysisContext context, INamedTypeSymbol? excluded, ImmutableArray<IArgumentOperation> arguments)
    {
        var wording = arguments.FirstOrDefault(static argument => String.Equals(argument.Parameter?.Name, "defaultText", StringComparison.Ordinal));

        var isWordingWritten = wording is not null && HoldsWording(wording.Value) && IsChecked(context, excluded);
        return isWordingWritten;
    }

    /// <summary>
    /// Whether LOC500 checks the wording of an operation. It does in the user's own source
    /// (<see cref="IsHandWritten(OperationAnalysisContext)"/>), except in the initializer or the getter of a property
    /// marked <c>[ExcludeFromLocalizationDiscovery]</c> (<see cref="IsInExcludedProperty"/>).
    /// </summary>
    private static bool IsChecked(OperationAnalysisContext context, INamedTypeSymbol? excluded)
    {
        var isChecked = IsHandWritten(context) && !IsInExcludedProperty(context, excluded);
        return isChecked;
    }

    /// <summary>
    /// Whether an operation lies in the initializer or the getter of a property marked
    /// <c>[ExcludeFromLocalizationDiscovery]</c>, as in <c>public static LocalizableText Brand { get; } = "Idioma";</c> or
    /// <c>public static LocalizableText Brand => "Idioma";</c>. Its author marked that wording as the same in every
    /// language, such as a name.
    /// </summary>
    // Not what other code assigns to the property, nor its setter. A marked component parameter would otherwise leave the
    // wording of every caller unchecked.
    private static bool IsInExcludedProperty(OperationAnalysisContext context, INamedTypeSymbol? excluded)
    {
        if (excluded is null)
            return false;

        // The getter, written as an expression body or as an accessor. What it returns is the property's own wording.
        if (context.ContainingSymbol is IMethodSymbol { MethodKind: MethodKind.PropertyGet, AssociatedSymbol: IPropertySymbol property })
            return IsMarked(property, excluded);

        for (var current = context.Operation.Parent; current is not null; current = current.Parent)
        {
            if (current is not IPropertyInitializerOperation initializer)
                continue;

            var isExcluded = initializer.InitializedProperties.Any(initialized => IsMarked(initialized, excluded));
            return isExcluded;
        }

        return false;
    }

    private static bool IsMarked(IPropertySymbol property, INamedTypeSymbol excluded)
    {
        var isMarked = property.GetAttributes().Any(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, excluded));
        return isMarked;
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context, string sample)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;

        if (!IsHandWritten(context))
            return;

        if (!IsFactory(method))
            return;

        foreach (var argument in invocation.Arguments)
        {
            if (argument.Syntax is ArgumentSyntax { NameColon: null })
            {
                var properties = FindParameterNames(invocation) is { } names
                    ? ImmutableDictionary<string, string?>.Empty.Add(ParameterNamesProperty, names)
                    : null;

                context.ReportDiagnostic(Diagnostic.Create(UnnamedLanguageRule, argument.Syntax.GetLocation(), properties,
                    $"{PackageNames.LocalizableTextName}.{LocalizableTextFactoryGenerator.FactoryName}({sample})"));

                return;
            }
        }
    }

    /// <summary>
    /// The parameter of each argument of a call, in the order the arguments are written, separated by commas, or
    /// <see langword="null"/> when an argument has no parameter, as in a call that doesn't compile.
    /// </summary>
    private static string? FindParameterNames(IInvocationOperation invocation)
    {
        if (invocation.Syntax is not InvocationExpressionSyntax { ArgumentList.Arguments: var written })
            return null;

        var names = new string?[written.Count];

        foreach (var argument in invocation.Arguments)
        {
            var index = argument.Syntax is ArgumentSyntax syntax ? written.IndexOf(syntax) : -1;

            if (index < 0 || argument.Parameter is not { } parameter)
                continue;

            names[index] = parameter.Name;
        }

        var parameterNames = names.Any(static name => name is null) ? null : String.Join(",", names);
        return parameterNames;
    }

    /// <summary>
    /// Whether a method is the generated factory. An extension member's method lies in a block inside the static class.
    /// </summary>
    internal static bool IsFactory(IMethodSymbol method)
    {
        if (!String.Equals(method.Name, LocalizableTextFactoryGenerator.FactoryName, StringComparison.Ordinal))
            return false;

        for (var type = method.ContainingType; type is not null; type = type.ContainingType)
            if (String.Equals(type.Name, LocalizableTextFactoryGenerator.ClassName, StringComparison.Ordinal))
                return true;

        return false;
    }

    private static bool IsHandWritten(OperationAnalysisContext context)
    {
        var isHandWritten = IsHandWritten(context.IsGeneratedCode, context.Operation.Syntax.SyntaxTree);
        return isHandWritten;
    }

    /// <summary>
    /// Whether code is the user's own: not generated, or generated by Razor from the user's markup.
    /// </summary>
    internal static bool IsHandWritten(bool isGeneratedCode, SyntaxTree tree)
    {
        // Razor names its output after the markup file: Pages/Index.cshtml becomes Pages/Index_cshtml.g.cs.
        var path = tree.FilePath;

        var isHandWritten = !isGeneratedCode
                            || path.EndsWith("_razor.g.cs", StringComparison.Ordinal)
                            || path.EndsWith("_cshtml.g.cs", StringComparison.Ordinal);

        return isHandWritten;
    }

    /// <summary>
    /// Whether an expression holds text written in the source. Punctuation and numbers are in no language.
    /// </summary>
    private static bool HoldsWording(IOperation? operation)
    {
        var holdsWording = operation switch
        {
            null => false,
            { ConstantValue: { HasValue: true, Value: string constant } } => constant.Any(Char.IsLetter),
            IInterpolatedStringOperation interpolated => interpolated.Parts.Any(static part => part switch
            {
                IInterpolatedStringTextOperation text => HoldsWording(text.Text),
                IInterpolationOperation hole => HoldsWording(hole.Expression),
                _ => false,
            }),
            IBinaryOperation { OperatorKind: BinaryOperatorKind.Add } addition => HoldsWording(addition.LeftOperand) || HoldsWording(addition.RightOperand),
            IConditionalOperation conditional => HoldsWording(conditional.WhenTrue) || HoldsWording(conditional.WhenFalse),
            ICoalesceOperation coalesce => HoldsWording(coalesce.Value) || HoldsWording(coalesce.WhenNull),
            ISwitchExpressionOperation @switch => @switch.Arms.Any(static arm => HoldsWording(arm.Value)),
            IConversionOperation conversion => HoldsWording(conversion.Operand),
            _ => false,
        };

        return holdsWording;
    }
}
