using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Reports a language argument without its name, of <c>LocalizedText.Inline</c> or of <c>[Localized]</c>
/// (<c>LOC500</c>). In an assembly without languages, it reports a call of <c>LocalizedText.Inline</c> (<c>LOC100</c>),
/// which the compiler alone reports as a method that doesn't exist.
/// </summary>
/// <remarks>
/// <c>[Localized]</c> without languages is reported by <see cref="LocalizedTextFactoryGenerator"/>, because the
/// compiler stops at an attribute that doesn't exist before any analyzer runs. Generated code is left alone, except
/// what Razor generates from the user's markup.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnnamedLanguageAnalyzer : DiagnosticAnalyzer
{
    public const string UnnamedLanguageDiagnosticId = "LOC500";

    /// <summary>
    /// The property of <c>LOC500</c> that holds the parameter of each argument in written order, as <c>en,nl</c>. The
    /// code fix reads it, so it works also where the generator hasn't run.
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

    // The generators report LOC100 too (SharedRules).
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [UnnamedLanguageRule, SharedRules.NoLanguagesDeclared];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();

        // Razor's output counts as generated code, but a text written in markup must be caught (AnalyzerSupport.IsHandWritten).
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
        // By its shape first, which binds nothing, because nearly every call is another one.
        if (context.Node is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } invocation
            || !String.Equals(access.Name.Identifier.ValueText, LocalizedTextFactoryGenerator.FactoryName, StringComparison.Ordinal)
            || !AnalyzerSupport.IsHandWritten(context.IsGeneratedCode, invocation.SyntaxTree))
        {
            return;
        }

        // Only on the text type, and only where nothing of that name exists, such as an extension of the consumer.
        if (context.SemanticModel.GetSymbolInfo(access.Expression, context.CancellationToken).Symbol is INamedTypeSymbol receiver
            && SymbolEqualityComparer.Default.Equals(receiver, text)
            && context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken) is { Symbol: null, CandidateSymbols.IsEmpty: true })
        {
            context.ReportDiagnostic(Diagnostic.Create(SharedRules.NoLanguagesDeclared, access.Name.GetLocation(), $"{PackageNames.LocalizedTextName}.{LocalizedTextFactoryGenerator.FactoryName} takes a wording per language"));
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
            || !AnalyzerSupport.IsHandWritten(context.IsGeneratedCode, attribute.SyntaxTree)
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
    /// The parameter of each argument of an attribute, comma-separated, or <see langword="null"/> when an argument has
    /// none.
    /// </summary>
    private static string? FindParameterNames(SeparatedSyntaxList<AttributeArgumentSyntax> arguments, IMethodSymbol constructor)
    {
        var names = new string[arguments.Count];

        for (var i = 0; i < arguments.Count; i++)
        {
            if (AnalyzerSupport.FindParameterOfArgument(arguments, i, constructor) is not { } name)
                return null;

            names[i] = name;
        }

        var parameterNames = String.Join(",", names);
        return parameterNames;
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context, string sample)
    {
        var invocation = (IInvocationOperation)context.Operation;

        if (!AnalyzerSupport.IsHandWritten(context.IsGeneratedCode, invocation.Syntax.SyntaxTree) || !AnalyzerSupport.IsFactory(invocation.TargetMethod))
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
    /// The parameter of each argument of a call, comma-separated, or <see langword="null"/> when an argument has none.
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

}
