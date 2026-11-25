using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Reports a wording that holds a placeholder of a format string, such as <c>{0}</c> (<c>LOC403</c>): in a text of a
/// set, in any language, in a language of <c>LocalizedText.Inline</c>, and in a language of <c>[Localized]</c> on a
/// member.
/// </summary>
/// <remarks>
/// <para>
/// Nothing fills a placeholder in. A text shows its wording as it is written, so <c>"Hello, {0}!"</c> reaches the user
/// with its braces. A text takes a value as a method of its set, <c>Greeting(string name)</c>, and an inline text as a
/// value written into every language, which the build then holds every language to.
/// </para>
/// <para>
/// Read where the wording is written: the strings a property or method returns, or the argument of a language holds
/// (<see cref="WrittenWording"/>), and for a property and an argument a constant they name. A string passed to a
/// method isn't read, because the method may fill the placeholder in, as <c>string.Format("{0} items", count)</c>
/// does. A value of an interpolated string is no placeholder, whatever it holds. Only a member that implements a text
/// of a contract is read, so a helper of a set may hold what it likes. A public property is read too when the contract
/// has a method by its name that the set doesn't implement. It's that text's wording in a language still written as a
/// property, after the default language made the text a method.
/// </para>
/// <para>
/// The IDE fix writes a parameter in place of each placeholder (<c>PlaceholderCodeFixProvider</c> in the code fixes).
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PlaceholderAnalyzer : DiagnosticAnalyzer
{
    public const string PlaceholderDiagnosticId = "LOC403";

    private const string SetAdvice = "Write the text as a method that takes the value, as in 'public string Greeting(string name) => $\"Hello, {name}!\";'";
    private const string InlineAdvice = "Write the value into the wording of every language, as in '$\"Hello, {name}!\"'";
    private const string MemberAdvice = "A text on a member can't take a value, so write a text with a value as a method of a set, as in 'public string Greeting(string name) => $\"Hello, {name}!\";'";

    private static readonly DiagnosticDescriptor PlaceholderRule = new(
        id: PlaceholderDiagnosticId,
        title: "Placeholder in the wording of a text",
        messageFormat: "{0} holds the placeholder {1}. Nothing fills it in, so users would see {1} as written. {2}.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A text shows its wording as written, so a format-string placeholder such as {0} reaches the user with its braces. In a set, write a text with a value as a method, which every language implements with the same parameters. In an inline text, write the value into the wording of every language. A text on a member, written with [Localized], can't take a value.",
        helpLinkUri: PackageNames.HelpLink(PlaceholderDiagnosticId));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [PlaceholderRule];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();

        // Razor's output counts as generated code, and a text written in markup must be read there.
        // UnnamedLanguageAnalyzer.IsHandWritten leaves out the rest.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.RegisterCompilationStartAction(Start);
    }

    private static void Start(CompilationStartAnalysisContext context)
    {
        if (context.Compilation.GetTypeByMetadataName(PackageNames.LocalizationSet) is not { } localizationSet)
            return;

        // Read once per compilation, and not per argument: an app may write hundreds of inline texts.
        var codes = SupportedLanguages.ReadCodes(context.Compilation.Assembly);

        context.RegisterOperationAction(c => AnalyzeInvocation(c, codes), OperationKind.Invocation);
        context.RegisterSyntaxNodeAction(c => AnalyzeLocalized(c, codes), SyntaxKind.Attribute);
        context.RegisterSyntaxNodeAction(c => AnalyzeMember(c, localizationSet), SyntaxKind.PropertyDeclaration, SyntaxKind.MethodDeclaration);
    }

    /// <summary>
    /// Reads the wording of each language of <c>[Localized]</c> on a member.
    /// </summary>
    /// <param name="context">The attribute.</param>
    /// <param name="codes">The languages the assembly declares, which name the wording of each parameter.</param>
    private static void AnalyzeLocalized(SyntaxNodeAnalysisContext context, ImmutableArray<string> codes)
    {
        var attribute = (AttributeSyntax)context.Node;

        // By its name first, which binds nothing: nearly every attribute is another one.
        if (!LocalizedMembers.IsNamedLocalized(attribute)
            || attribute.ArgumentList is not { Arguments: var arguments }
            || !UnnamedLanguageAnalyzer.IsHandWritten(context.IsGeneratedCode, attribute.SyntaxTree)
            || context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol is not IMethodSymbol constructor
            || !LocalizedMembers.IsGeneratedAttribute(constructor.ContainingType, context.Compilation.Assembly))
        {
            return;
        }

        for (var i = 0; i < arguments.Count; i++)
        {
            var argument = arguments[i];

            // An argument without a name takes the parameter of its place.
            var parameter = argument.NameColon?.Name.Identifier.ValueText
                            ?? (argument.NameEquals is null && i < constructor.Parameters.Length ? constructor.Parameters[i].Name : null);

            if (parameter is null)
                continue;

            var subject = $"The '{SupportedLanguages.FindLanguageOfParameter(parameter, codes)}' wording";
            var reported = false;

            foreach (var (location, placeholder) in FindWrittenPlaceholders(WrittenWording.FindStrings(argument.Expression)))
            {
                reported = true;
                context.ReportDiagnostic(Diagnostic.Create(PlaceholderRule, location, subject, placeholder, MemberAdvice));
            }

            // A constant the argument names, which is written elsewhere.
            if (!reported
                && context.SemanticModel.GetConstantValue(argument.Expression, context.CancellationToken) is { HasValue: true, Value: string constant }
                && Placeholder.Find(constant) is { } found)
            {
                context.ReportDiagnostic(Diagnostic.Create(PlaceholderRule, argument.GetLocation(), subject, found, MemberAdvice));
            }
        }
    }

    /// <summary>
    /// Reads the wording of each language of <c>LocalizedText.Inline</c>.
    /// </summary>
    /// <param name="context">The invocation.</param>
    /// <param name="codes">The languages the assembly declares, which name the wording of each parameter.</param>
    private static void AnalyzeInvocation(OperationAnalysisContext context, ImmutableArray<string> codes)
    {
        var invocation = (IInvocationOperation)context.Operation;

        if (!UnnamedLanguageAnalyzer.IsFactory(invocation.TargetMethod)
            || !UnnamedLanguageAnalyzer.IsHandWritten(context.IsGeneratedCode, invocation.Syntax.SyntaxTree))
        {
            return;
        }

        foreach (var argument in invocation.Arguments)
        {
            if (argument.Parameter is not { } parameter)
                continue;

            var subject = $"The '{SupportedLanguages.FindLanguageOfParameter(parameter.Name, codes)}' wording";
            var reported = false;

            foreach (var (location, placeholder) in FindWrittenPlaceholders(WrittenWording.FindStrings(argument.Value.Syntax as ExpressionSyntax)))
            {
                reported = true;
                context.ReportDiagnostic(Diagnostic.Create(PlaceholderRule, location, subject, placeholder, InlineAdvice));
            }

            // A constant the argument names, which is written elsewhere.
            if (!reported && argument.Value.ConstantValue is { HasValue: true, Value: string constant } && Placeholder.Find(constant) is { } found)
                context.ReportDiagnostic(Diagnostic.Create(PlaceholderRule, argument.Syntax.GetLocation(), subject, found, InlineAdvice));
        }
    }

    /// <summary>
    /// Reads the wording of a property or method of a set that implements a text of a contract.
    /// </summary>
    private static void AnalyzeMember(SyntaxNodeAnalysisContext context, INamedTypeSymbol localizationSet)
    {
        var declaration = (MemberDeclarationSyntax)context.Node;

        // By its shape first, which binds nothing: a method without a body returns no wording.
        if (!UnnamedLanguageAnalyzer.IsHandWritten(context.IsGeneratedCode, declaration.SyntaxTree)
            || declaration is MethodDeclarationSyntax { ExpressionBody: null, Body: null }
            || context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not { IsStatic: false, ContainingType: { TypeKind: TypeKind.Class } set } member
            || !set.AllInterfaces.Contains(localizationSet, SymbolEqualityComparer.Default)
            || !ContractTexts.ImplementsText(member, set))
        {
            return;
        }

        // The name of the text, without the contract an explicit implementation carries in front of it.
        var subject = $"'{set.Name}.{member.Name.Substring(member.Name.LastIndexOf('.') + 1)}'";
        var reported = false;

        // Where the wording is written: what the member returns, and not its attributes or its parameters, which may
        // hold a string too.
        foreach (var (location, placeholder) in FindWrittenPlaceholders(WrittenWording.FindStrings(declaration)))
        {
            reported = true;
            context.ReportDiagnostic(Diagnostic.Create(PlaceholderRule, location, subject, placeholder, SetAdvice));
        }

        // A constant the property returns, which is written elsewhere.
        if (!reported
            && declaration is PropertyDeclarationSyntax { Identifier: var identifier } constantProperty
            && ConstantWording.Read(constantProperty, context.SemanticModel, context.CancellationToken) is { } constant
            && Placeholder.Find(constant) is { } found)
        {
            context.ReportDiagnostic(Diagnostic.Create(PlaceholderRule, identifier.GetLocation(), subject, found, SetAdvice));
        }
    }

    /// <summary>
    /// The placeholders written in the strings of a wording (<see cref="WrittenWording"/>), each with where it is: in a
    /// string, or in the text of an interpolated string, as it reads. One per string.
    /// </summary>
    private static IEnumerable<(Location Location, string Placeholder)> FindWrittenPlaceholders(IEnumerable<SyntaxNode> strings)
    {
        foreach (var written in strings)
            if (Placeholder.Find(WrittenWording.Read(written)) is { } placeholder)
                yield return (written.GetLocation(), placeholder);
    }
}
