using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Closes what the generated factory cannot in an assembly that declares <c>[assembly: SupportedLanguages(...)]</c>:
/// <c>LOC011</c> for wording written in one language, <c>LOC012</c> for calling by hand what only generated code should
/// call, and <c>LOC013</c> for a language argument without its name.
/// </summary>
/// <remarks>
/// Wording means text written in the source: a constant holding a letter, also inside an interpolation, a concatenation,
/// a conditional, a coalesce, or a switch. A runtime value (a customer's name) converts to a text as it always did: it
/// has no translations to demand, and the build cannot tell it from wording kept in a local variable, which therefore
/// passes too. Generated code is left alone, as its author cannot fix a report on it, except what Razor generates from a
/// <c>.razor</c> or <c>.cshtml</c> file, which is the user's own markup and maps back to it.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OneLanguageTextAnalyzer : DiagnosticAnalyzer
{
    public const string OneLanguageDiagnosticId = "LOC011";
    public const string GeneratedCodeOnlyDiagnosticId = "LOC012";
    public const string UnnamedLanguageDiagnosticId = "LOC013";

    private const string TextFullName = "CodeChops.Idioma.LocalizableText";
    private const string ResourceTextFullName = "CodeChops.Idioma.ResourceText";

    private static readonly DiagnosticDescriptor OneLanguageRule = new(
        id: OneLanguageDiagnosticId,
        title: "Text written in one language",
        messageFormat: "This text is written in one language, and this assembly supports {0}. Write it as LocalizableText.Translated({1}).",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every language of [assembly: SupportedLanguages] must be able to show every text. A string converts to a text, which suits a value such as a customer's name, but wording written in the source would show in this one language to everybody.");

    private static readonly DiagnosticDescriptor GeneratedCodeOnlyRule = new(
        id: GeneratedCodeOnlyDiagnosticId,
        title: "Called by hand what only generated code should call",
        messageFormat: "'{0}' is for generated code, and would let a text miss a language. Write the text as LocalizableText.Translated({1}), or in a set per language.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "These members exist because generated code in another assembly has to call them. Called by hand they can create a text without all the languages of [assembly: SupportedLanguages].");

    private static readonly DiagnosticDescriptor UnnamedLanguageRule = new(
        id: UnnamedLanguageDiagnosticId,
        title: "Language argument without its name",
        messageFormat: "Name the language of this wording: LocalizableText.Translated({0}). Without names two languages can swap unnoticed.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every parameter of LocalizableText.Translated is a string, so wording in the wrong position compiles and shows in the wrong language.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(OneLanguageRule, GeneratedCodeOnlyRule, UnnamedLanguageRule);

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
        var text = context.Compilation.GetTypeByMetadataName(TextFullName);
        var resourceText = context.Compilation.GetTypeByMetadataName(ResourceTextFullName);

        if (codes.Length is 0 || text is null)
            return;

        var languages = String.Join(", ", codes);
        var sample = String.Join(", ", codes.Select(static code => $"{SupportedLanguages.ToParameterName(code)}: \"...\""));

        context.RegisterOperationAction(c => AnalyzeInvocation(c, text, resourceText, sample), OperationKind.Invocation);

        // One language has nothing to be translated into.
        if (codes.Length < 2)
            return;

        context.RegisterOperationAction(c => AnalyzeConversion(c, text, languages, sample), OperationKind.Conversion);
        context.RegisterOperationAction(c => AnalyzeCreation(c, text, resourceText, languages, sample), OperationKind.ObjectCreation);
    }

    private static void AnalyzeConversion(OperationAnalysisContext context, INamedTypeSymbol text, string languages, string sample)
    {
        var conversion = (IConversionOperation)context.Operation;

        if (conversion.OperatorMethod is { Parameters.Length: 1 } method
            && SymbolEqualityComparer.Default.Equals(method.ContainingType, text)
            && method.Parameters[0].Type.SpecialType is SpecialType.System_String
            && IsHandWritten(context)
            && HoldsWording(conversion.Operand))
        {
            context.ReportDiagnostic(Diagnostic.Create(OneLanguageRule, conversion.Syntax.GetLocation(), languages, sample));
        }
    }

    private static void AnalyzeCreation(OperationAnalysisContext context, INamedTypeSymbol text, INamedTypeSymbol? resourceText, string languages, string sample)
    {
        var creation = (IObjectCreationOperation)context.Operation;
        var type = creation.Type;

        if (!SymbolEqualityComparer.Default.Equals(type, text) && !SymbolEqualityComparer.Default.Equals(type, resourceText))
            return;

        // The wording of new LocalizableText(wording) and new ResourceText(key, wording): a key is no wording.
        var wording = creation.Arguments.FirstOrDefault(static argument => String.Equals(argument.Parameter?.Name, "defaultText", StringComparison.Ordinal));

        if (wording is not null && IsHandWritten(context) && HoldsWording(wording.Value))
            context.ReportDiagnostic(Diagnostic.Create(OneLanguageRule, creation.Syntax.GetLocation(), languages, sample));
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context, INamedTypeSymbol text, INamedTypeSymbol? resourceText, string sample)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;

        if (!IsHandWritten(context))
            return;

        if (IsGeneratedCodeOnly(method, text, resourceText))
        {
            context.ReportDiagnostic(Diagnostic.Create(GeneratedCodeOnlyRule, invocation.Syntax.GetLocation(), $"{method.ContainingType.Name}.{method.Name}", sample));
            return;
        }

        if (!IsFactory(method))
            return;

        foreach (var argument in invocation.Arguments)
        {
            if (argument.Syntax is ArgumentSyntax { NameColon: null })
            {
                context.ReportDiagnostic(Diagnostic.Create(UnnamedLanguageRule, argument.Syntax.GetLocation(), sample));
                return;
            }
        }
    }

    private static bool IsGeneratedCodeOnly(IMethodSymbol method, INamedTypeSymbol text, INamedTypeSymbol? resourceText)
    {
        if (SymbolEqualityComparer.Default.Equals(method.ContainingType, text))
            return method.Name is "FromGeneratedCode" or "WithTranslation";

        return SymbolEqualityComparer.Default.Equals(method.ContainingType, resourceText)
               && method.Name is "Create" or "WithTranslation";
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
        return IsHandWritten(context.IsGeneratedCode, context.Operation.Syntax.SyntaxTree);
    }

    /// <summary>
    /// Whether code is the user's own: not generated, or generated by Razor from the user's markup.
    /// </summary>
    internal static bool IsHandWritten(bool isGeneratedCode, SyntaxTree tree)
    {
        // Razor names its output after the markup file: Pages/Index.cshtml becomes Pages/Index_cshtml.g.cs.
        var path = tree.FilePath;

        return !isGeneratedCode
               || path.EndsWith("_razor.g.cs", StringComparison.Ordinal)
               || path.EndsWith("_cshtml.g.cs", StringComparison.Ordinal);
    }

    /// <summary>
    /// Whether an expression holds text written in the source. Punctuation and numbers are in no language.
    /// </summary>
    private static bool HoldsWording(IOperation? operation)
    {
        return operation switch
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
    }
}
