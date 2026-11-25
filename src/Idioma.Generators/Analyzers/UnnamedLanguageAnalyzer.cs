using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Closes what the generated factory can't in an assembly that declares <c>[assembly: SupportedLanguages(...)]</c>:
/// <c>LOC500</c> for a language argument without its name, of <c>LocalizedText.Inline</c> or of <c>[Localized]</c>.
/// In an assembly without the attribute, it reports a call of <c>LocalizedText.Inline</c> (<c>LOC100</c>), which the
/// compiler alone reports as a method that doesn't exist. The generator reports a member marked <c>[Localized]</c>
/// there (<see cref="LocalizedTextFactoryGenerator"/>), because the compiler stops at an attribute that doesn't exist
/// before any analyzer runs.
/// </summary>
/// <remarks>
/// Generated code is left alone, as its author can't fix a report on it, except what Razor generates from a
/// <c>.razor</c> or <c>.cshtml</c> file, which is the user's own markup and maps back to it.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnnamedLanguageAnalyzer : DiagnosticAnalyzer
{
    // An earlier rule reported wording written in one language where a text was expected. A text is a string now, so
    // the build can't tell such wording from any other string, and that rule is gone.
    public const string UnnamedLanguageDiagnosticId = "LOC500";

    /// <summary>
    /// The code a set in an assembly without languages gets too (<see cref="LocalizationSetsPart"/>).
    /// </summary>
    public const string NoLanguagesDeclaredDiagnosticId = LocalizationSetsPart.NoLanguagesDeclaredDiagnosticId;

    /// <summary>
    /// The property of <c>LOC500</c> that holds the language of each argument of the call or the attribute, in the
    /// order they're written, separated by commas: <c>en,nl</c>. Its code fix names the arguments from it, also in a
    /// workspace that hasn't run the generator that writes <c>LocalizedText.Inline</c> and <c>[Localized]</c>.
    /// </summary>
    public const string ParameterNamesProperty = "ParameterNames";

    private static readonly DiagnosticDescriptor UnnamedLanguageRule = new(
        id: UnnamedLanguageDiagnosticId,
        title: "Language argument without its name",
        messageFormat: "Name the language of this wording: {0}. Without names, two languages can swap unnoticed.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every parameter of LocalizedText.Inline and of [Localized] is a string, so wording in the wrong position compiles and shows in the wrong language.",
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
        description: "The source generator writes LocalizedText.Inline with one parameter per language of [assembly: SupportedLanguages]. Without the attribute it writes none, so the compiler finds no such method (CS0117).",
        helpLinkUri: PackageNames.HelpLink(NoLanguagesDeclaredDiagnosticId));
#pragma warning restore RS1019

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [UnnamedLanguageRule, InlineWithoutLanguagesRule];

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
        var text = context.Compilation.GetTypeByMetadataName(PackageNames.LocalizedText);

        if (text is null)
            return;

        if (codes.Length is 0)
        {
            // Without languages there is no factory, and the compiler only says that LocalizedText has no Inline.
            context.RegisterSyntaxNodeAction(c => AnalyzeInlineWithoutLanguages(c, text), SyntaxKind.InvocationExpression);
            return;
        }

        var sample = String.Join(", ", codes.Select(static code => $"{SupportedLanguages.ToParameterName(code)}: \"...\""));

        context.RegisterOperationAction(c => AnalyzeInvocation(c, sample), OperationKind.Invocation);
        context.RegisterSyntaxNodeAction(c => AnalyzeLocalized(c, sample), SyntaxKind.Attribute);
    }

    /// <summary>
    /// Reports a call of <c>LocalizedText.Inline</c> that binds to nothing, in an assembly that declares no languages.
    /// </summary>
    private static void AnalyzeInlineWithoutLanguages(SyntaxNodeAnalysisContext context, INamedTypeSymbol text)
    {
        // By its shape first, which binds nothing: nearly every call is another one.
        if (context.Node is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } invocation
            || !String.Equals(access.Name.Identifier.ValueText, LocalizedTextFactoryGenerator.FactoryName, StringComparison.Ordinal)
            || !IsHandWritten(context.IsGeneratedCode, invocation.SyntaxTree))
        {
            return;
        }

        // Only on the text type, and only where nothing of that name exists, such as an extension of the consumer.
        if (context.SemanticModel.GetSymbolInfo(access.Expression, context.CancellationToken).Symbol is INamedTypeSymbol receiver
            && SymbolEqualityComparer.Default.Equals(receiver, text)
            && context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken) is { Symbol: null, CandidateSymbols.IsEmpty: true })
        {
            context.ReportDiagnostic(Diagnostic.Create(InlineWithoutLanguagesRule, access.Name.GetLocation(), $"{PackageNames.LocalizedTextName}.{LocalizedTextFactoryGenerator.FactoryName}"));
        }
    }

    /// <summary>
    /// Reports the first argument of <c>[Localized]</c> without the name of its language (<c>LOC500</c>).
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
                    $"{PackageNames.LocalizedTextName}.{LocalizedTextFactoryGenerator.FactoryName}({sample})"));

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
        if (!String.Equals(method.Name, LocalizedTextFactoryGenerator.FactoryName, StringComparison.Ordinal))
            return false;

        for (var type = method.ContainingType; type is not null; type = type.ContainingType)
            if (String.Equals(type.Name, LocalizedTextFactoryGenerator.ClassName, StringComparison.Ordinal))
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
}
