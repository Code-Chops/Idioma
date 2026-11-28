using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Reports a placeholder of a format string, such as <c>{0}</c>, in a wording (<c>LOC403</c>). Nothing fills it in, so
/// the user would see the braces.
/// </summary>
/// <remarks>
/// <para>
/// Only where the wording is written is read (<see cref="WrittenWording"/>), plus a constant a property or an argument
/// names. A string passed to a method isn't read, because the method may fill the placeholder in, as
/// <c>string.Format</c> does. Only a member that implements a text of a contract is read, so a helper of a set may hold
/// what it likes.
/// </para>
/// <para>
/// A text that an attribute names as its error message isn't reported, because the attribute fills in its placeholders
/// (<see cref="ResourceNamedTexts"/>). The IDE fix is <c>PlaceholderCodeFixProvider</c>.
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

        // Razor's output counts as generated code, but a text written in markup must be read.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.RegisterCompilationStartAction(Start);
    }

    private static void Start(CompilationStartAnalysisContext context)
    {
        if (context.Compilation.GetTypeByMetadataName(PackageNames.LocalizationSet) is not { } localizationSet)
            return;

        // Once per compilation, because an app may write hundreds of inline texts.
        var codes = SupportedLanguages.ReadCodes(context.Compilation.Assembly);

        context.RegisterOperationAction(c => AnalyzeInvocation(c, codes), OperationKind.Invocation);
        context.RegisterSyntaxNodeAction(c => AnalyzeLocalized(c, codes), SyntaxKind.Attribute);
        context.RegisterSyntaxNodeAction(c => AnalyzeMember(c, localizationSet), SyntaxKind.PropertyDeclaration, SyntaxKind.MethodDeclaration);
    }

    /// <summary>
    /// Reads the wording of each language of <c>[Localized]</c> on a member.
    /// </summary>
    /// <param name="codes">The languages the assembly declares, which name the wording of each parameter.</param>
    private static void AnalyzeLocalized(SyntaxNodeAnalysisContext context, ImmutableArray<string> codes)
    {
        var attribute = (AttributeSyntax)context.Node;

        // By its name first, which binds nothing, because nearly every attribute is another one.
        if (!LocalizedMembers.IsNamedLocalized(attribute)
            || attribute.ArgumentList is not { Arguments: var arguments }
            || !AnalyzerSupport.IsHandWritten(context.IsGeneratedCode, attribute.SyntaxTree)
            || context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol is not IMethodSymbol constructor
            || !LocalizedMembers.IsGeneratedAttribute(constructor.ContainingType, context.Compilation.Assembly))
        {
            return;
        }

        for (var i = 0; i < arguments.Count; i++)
        {
            var argument = arguments[i];

            if (AnalyzerSupport.FindParameterOfArgument(arguments, i, constructor) is not { } parameter)
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
    /// Reads the wording of each language of <c>LocalizedText.Inline</c>, unless what it gives is formatted
    /// (<see cref="IsPassedAsFormat"/>).
    /// </summary>
    private static void AnalyzeInvocation(OperationAnalysisContext context, ImmutableArray<string> codes)
    {
        var invocation = (IInvocationOperation)context.Operation;

        if (!AnalyzerSupport.IsFactory(invocation.TargetMethod)
            || !AnalyzerSupport.IsHandWritten(context.IsGeneratedCode, invocation.Syntax.SyntaxTree)
            || IsPassedAsFormat(invocation))
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

        // By its shape first, which binds nothing.
        if (!AnalyzerSupport.IsHandWritten(context.IsGeneratedCode, declaration.SyntaxTree)
            || declaration is MethodDeclarationSyntax { ExpressionBody: null, Body: null }
            || context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not { IsStatic: false, ContainingType: { TypeKind: TypeKind.Class } set } member
            || !set.AllInterfaces.Contains(localizationSet, SymbolEqualityComparer.Default)
            || !ContractTexts.ImplementsText(member, set))
        {
            return;
        }

        // What the member returns, not its attributes or parameters. Else a constant the property returns.
        var found = FindWrittenPlaceholders(WrittenWording.FindStrings(declaration)).ToList();

        if (found.Count is 0
            && declaration is PropertyDeclarationSyntax { Identifier: var identifier } constantProperty
            && ConstantWording.Read(constantProperty, context.SemanticModel, context.CancellationToken) is { } constant
            && Placeholder.Find(constant) is { } placeholderOfConstant)
        {
            found.Add((identifier.GetLocation(), placeholderOfConstant));
        }

        // Something fills in a text marked as a format, or one an attribute names as its error message. The attributes
        // are asked last, because they're searched in every file.
        if (found.Count is 0
            || (declaration is PropertyDeclarationSyntax
                && (IsMarkedAsFormat(member, set) || IsNamedAsErrorMessage(member, set, context.Compilation, context.CancellationToken))))
        {
            return;
        }

        var subject = $"'{set.Name}.{AnalyzerSupport.GetTextName(member)}'";

        foreach (var (location, placeholder) in found)
            context.ReportDiagnostic(Diagnostic.Create(PlaceholderRule, location, subject, placeholder, SetAdvice));
    }

    /// <summary>
    /// Whether a property is marked as a composite format, so something outside the set fills in its placeholders. The
    /// mark counts on the property, and on the text of the generated class, which takes it from the default language.
    /// </summary>
    private static bool IsMarkedAsFormat(ISymbol member, INamedTypeSymbol set)
    {
        if (IsCompositeFormat(member))
            return true;

        foreach (var text in ContractTexts.FindImplementedTexts(member, set))
            if (ResourceNamedTexts.FindGeneratedClass(text) is { } generatedClass && generatedClass.GetMembers(text.Name).Any(IsCompositeFormat))
                return true;

        return false;
    }

    /// <summary>
    /// Whether an inline text is passed straight to a parameter that takes a composite format, such as the format of
    /// <c>string.Format</c> or <c>StringBuilder.AppendFormat</c>, which fills its placeholders in.
    /// </summary>
    private static bool IsPassedAsFormat(IInvocationOperation invocation)
    {
        var parent = invocation.Parent;

        while (parent is IConversionOperation or IParenthesizedOperation)
            parent = parent.Parent;

        var isPassedAsFormat = parent is IArgumentOperation { Parameter: { } parameter } && IsCompositeFormat(parameter);
        return isPassedAsFormat;
    }

    /// <summary>
    /// Whether a member or a parameter is marked <c>[StringSyntax(StringSyntaxAttribute.CompositeFormat)]</c>.
    /// </summary>
    internal static bool IsCompositeFormat(ISymbol member)
    {
        foreach (var attribute in member.GetAttributes())
        {
            if (attribute.AttributeClass is
                {
                    Name: "StringSyntaxAttribute",
                    ContainingNamespace:
                    {
                        Name: "CodeAnalysis",
                        ContainingNamespace: { Name: "Diagnostics", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } },
                    },
                }
                && attribute.ConstructorArguments is [{ Value: "CompositeFormat" }, ..])
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether an attribute names the text a property implements as its error message (<see cref="ResourceNamedTexts"/>).
    /// </summary>
    private static bool IsNamedAsErrorMessage(ISymbol member, INamedTypeSymbol set, Compilation compilation, CancellationToken cancellationToken)
    {
        var named = ResourceNamedTexts.Of(compilation);

        foreach (var text in ContractTexts.FindImplementedTexts(member, set))
            if (ResourceNamedTexts.FindGeneratedClass(text) is { } generatedClass && named.IsErrorMessage(generatedClass, text.Name, cancellationToken))
                return true;

        return false;
    }

    /// <summary>
    /// The first placeholder of each string of a wording (<see cref="WrittenWording"/>), with its location.
    /// </summary>
    private static IEnumerable<(Location Location, string Placeholder)> FindWrittenPlaceholders(IEnumerable<SyntaxNode> strings)
    {
        foreach (var written in strings)
            if (Placeholder.Find(WrittenWording.Read(written)) is { } placeholder)
                yield return (written.GetLocation(), placeholder);
    }
}
