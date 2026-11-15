using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Reports a wording that holds a placeholder of a format string, such as <c>{0}</c> (<c>LOC016</c>): in a text of a
/// set, in any language, and in a language of <c>LocalizableText.Inline</c>.
/// </summary>
/// <remarks>
/// <para>
/// Nothing fills a placeholder in: a text shows its wording as it is written, so <c>"Hello, {0}!"</c> reaches the user
/// with its braces. A text takes a value as a method of its set, <c>Greeting(string name)</c>, and an inline text as a
/// value written into every language, which the build then holds every language to.
/// </para>
/// <para>
/// Read where the wording is written: a string in the body of the property or method, or in the argument of a
/// language, also the text of an interpolated string, and for a property and an argument a constant they name. A value
/// of an interpolated string is no placeholder, whatever it holds. Only a member that implements a text of a contract
/// is read, so a helper of a set may hold what it likes.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PlaceholderAnalyzer : DiagnosticAnalyzer
{
    public const string PlaceholderDiagnosticId = "LOC016";

    private const string SetAdvice = "Write the text as a method that takes the value, as in 'public string Greeting(string name) => $\"Hello, {name}!\";'";
    private const string InlineAdvice = "Write the value into the wording of every language, as in '$\"Hello, {name}!\"'";

    private static readonly DiagnosticDescriptor PlaceholderRule = new(
        id: PlaceholderDiagnosticId,
        title: "Placeholder in the wording of a text",
        messageFormat: "{0} holds the placeholder {1}, which nothing fills in: the user would see it as it is written. {2}.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A text shows its wording as it is written, so a placeholder of a format string, such as {0}, reaches the user with its braces. A text that takes a value is a method of its set, which every language implements with the same parameters, and an inline text takes the value in the wording of every language.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(PlaceholderRule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();

        // Razor's output counts as generated code, and a text written in markup must be read there.
        // OneLanguageTextAnalyzer.IsHandWritten leaves out the rest.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.RegisterCompilationStartAction(Start);
    }

    private static void Start(CompilationStartAnalysisContext context)
    {
        if (context.Compilation.GetTypeByMetadataName(PackageNames.TranslationSet) is not { } translations)
            return;

        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
        context.RegisterSyntaxNodeAction(c => AnalyzeMember(c, translations), SyntaxKind.PropertyDeclaration, SyntaxKind.MethodDeclaration);
    }

    /// <summary>
    /// Reads the wording of each language of <c>LocalizableText.Inline</c>.
    /// </summary>
    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;

        if (!OneLanguageTextAnalyzer.IsFactory(invocation.TargetMethod)
            || !OneLanguageTextAnalyzer.IsHandWritten(context.IsGeneratedCode, invocation.Syntax.SyntaxTree))
        {
            return;
        }

        foreach (var argument in invocation.Arguments)
        {
            if (argument.Parameter is not { } parameter)
                continue;

            var subject = $"The '{SupportedLanguages.FindLanguageOfParameter(parameter.Name, SupportedLanguages.ReadCodes(context.Compilation.Assembly))}' wording";
            var reported = false;

            foreach (var (location, placeholder) in FindWrittenPlaceholders(argument.Value.Syntax))
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
    private static void AnalyzeMember(SyntaxNodeAnalysisContext context, INamedTypeSymbol translations)
    {
        var declaration = (MemberDeclarationSyntax)context.Node;

        // Where the wording is written: not the attributes or the parameters of the member, which may hold a string too.
        SyntaxNode?[] bodies = declaration switch
        {
            PropertyDeclarationSyntax property => [property.ExpressionBody, property.Initializer, property.AccessorList],
            MethodDeclarationSyntax method => [method.ExpressionBody, method.Body],
            _ => [],
        };

        if (!OneLanguageTextAnalyzer.IsHandWritten(context.IsGeneratedCode, declaration.SyntaxTree)
            || bodies.All(static body => body is null)
            || context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not { IsStatic: false, ContainingType: { TypeKind: TypeKind.Class } set } member
            || !set.AllInterfaces.Contains(translations, SymbolEqualityComparer.Default)
            || !ImplementsText(member, set, translations))
        {
            return;
        }

        // The name of the text, without the contract an explicit implementation carries in front of it.
        var subject = $"'{set.Name}.{member.Name.Substring(member.Name.LastIndexOf('.') + 1)}'";
        var reported = false;

        foreach (var body in bodies)
        {
            if (body is null)
                continue;

            foreach (var (location, placeholder) in FindWrittenPlaceholders(body))
            {
                reported = true;
                context.ReportDiagnostic(Diagnostic.Create(PlaceholderRule, location, subject, placeholder, SetAdvice));
            }
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
    /// The placeholders written in a piece of code, each with where it is: in a string, or in the text of an
    /// interpolated string, as it reads. One per string.
    /// </summary>
    private static IEnumerable<(Location Location, string Placeholder)> FindWrittenPlaceholders(SyntaxNode code)
    {
        foreach (var node in code.DescendantNodesAndSelf())
        {
            var text = node switch
            {
                LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression) => literal.Token.ValueText,
                InterpolatedStringTextSyntax part => MethodWording.ReadText(part),
                _ => null,
            };

            if (text is not null && Placeholder.Find(text) is { } placeholder)
                yield return (node.GetLocation(), placeholder);
        }
    }

    /// <summary>
    /// Whether a property or method of a set implements a text of a contract, or overrides the implementation of one:
    /// explicitly, or as the member the set implements the text through.
    /// </summary>
    private static bool ImplementsText(ISymbol member, INamedTypeSymbol set, INamedTypeSymbol translations)
    {
        var explicitly = member switch
        {
            IPropertySymbol property => property.ExplicitInterfaceImplementations.CastArray<ISymbol>(),
            IMethodSymbol method => method.ExplicitInterfaceImplementations.CastArray<ISymbol>(),
            _ => ImmutableArray<ISymbol>.Empty,
        };

        if (!explicitly.IsEmpty)
            return explicitly.Any(text => IsContract(text.ContainingType, translations));

        if (member.DeclaredAccessibility is not Accessibility.Public)
            return false;

        // Asked of the set which member implements the text, and not told by the name alone.
        foreach (var contract in set.AllInterfaces)
        {
            if (!IsContract(contract, translations))
                continue;

            foreach (var text in contract.GetMembers(member.Name))
                if (IsOrOverrides(member, set.FindImplementationForInterfaceMember(text)))
                    return true;
        }

        return false;
    }

    /// <summary>
    /// Whether a member is the one that implements a text, or overrides it: a set deriving from another names the
    /// member of that set as the implementation, and its own member is what words the text.
    /// </summary>
    private static bool IsOrOverrides(ISymbol member, ISymbol? implementation)
    {
        for (var current = member; current is not null; current = current switch
                 {
                     IPropertySymbol property => property.OverriddenProperty,
                     IMethodSymbol method => method.OverriddenMethod,
                     _ => null,
                 })
        {
            if (SymbolEqualityComparer.Default.Equals(current, implementation))
                return true;
        }

        return false;
    }

    private static bool IsContract(INamedTypeSymbol @interface, INamedTypeSymbol translations)
    {
        var isContract = !SymbolEqualityComparer.Default.Equals(@interface, translations)
                         && @interface.AllInterfaces.Contains(translations, SymbolEqualityComparer.Default);
        return isContract;
    }
}
