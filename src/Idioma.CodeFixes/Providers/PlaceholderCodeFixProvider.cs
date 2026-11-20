using System.Composition;
using System.Text;
using CodeChops.Idioma.Generators;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Formatting;

namespace CodeChops.Idioma.CodeFixes;

/// <summary>
/// Fixes a text of a set whose wording holds a placeholder of a format string (<c>LOC502</c>). It writes a parameter in
/// place of each placeholder, so that the wording shows the value. A property becomes a method that takes the values,
/// in every language of this assembly at once, and a method uses its own parameters, the first for <c>{0}</c>.
/// </summary>
/// <remarks>
/// <para>
/// A property takes the parameters of the contract's method by its name, when the contract has one. Otherwise it takes
/// one parameter per placeholder number, <c>arg0</c> for <c>{0}</c>, of type <c>object?</c>, as
/// <see cref="string.Format(string, object?)"/> does. It counts the numbers in every language's wording of the text,
/// so each language gets the same parameters, whichever is fixed. That holds for a wording that has its
/// placeholders in another order, and for one that shows fewer of them.
/// </para>
/// <para>
/// The same property of every other set of this assembly becomes a method with it, also one without a placeholder, so
/// that each set still implements the contract. Such a set then leaves the value out, which the build reports
/// (<c>LOC302</c>). A wording in a raw string keeps its placeholder, and its warning.
/// </para>
/// <para>
/// There's no fix for:
/// </para>
/// <list type="bullet">
/// <item>An inline text, because its wording has no parameter to take the value.</item>
/// <item>A wording in a raw string, unless the fix of another language makes its property a method.</item>
/// <item>A text of another assembly's contract that takes no values, because a set can't change it.</item>
/// <item>A placeholder whose number has no parameter, or a ninth value, which a text can't take.</item>
/// </list>
/// <para>
/// A string passed to a method, such as the format of <c>string.Format</c>, stays as it is. The method may fill its
/// placeholders in, so the build doesn't report it.
/// </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PlaceholderCodeFixProvider))]
[Shared]
public sealed class PlaceholderCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// The most values a text takes (<c>LOC300</c>).
    /// </summary>
    private const int MaxValues = 8;

    private const string EquivalenceKey = "FillInPlaceholders";

    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [PlaceholderAnalyzer.PlaceholderDiagnosticId];

    /// <summary>
    /// Changes each text once, however many of its wordings and languages hold a placeholder, because each such wording
    /// has a diagnostic of its own.
    /// </summary>
    private static readonly FixAllProvider FixAll = new ReplaceNodesFixAllProvider<MemberDeclarationSyntax>(
        "Replace the placeholders with parameters",
        EquivalenceKey,
        static (root, semanticModel, diagnostic, cancellationToken) => FindText(root, diagnostic) is { } text ? Fix(text, semanticModel, cancellationToken) : null);

    public override FixAllProvider GetFixAllProvider() => FixAll;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var document = context.Document;
        var root = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

        if (root is null || semanticModel is null)
            return;

        foreach (var diagnostic in context.Diagnostics)
        {
            if (FindText(root, diagnostic) is not { } text || Fix(text, semanticModel, context.CancellationToken) is not { } fixedTexts)
                continue;

            var title = text is PropertyDeclarationSyntax property
                ? $"Make '{property.Identifier.ValueText}' a method"
                : "Replace the placeholders with the parameters";

            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    createChangedSolution: cancellationToken => CodeFixSupport.ReplaceNodesAsync(document.Project.Solution, fixedTexts.Keys, changed => fixedTexts[changed], cancellationToken),
                    equivalenceKey: EquivalenceKey),
                diagnostic);
        }
    }

    /// <summary>
    /// The property or method of a set that a diagnostic sits in, or <see langword="null"/> for an inline text, whose
    /// diagnostic sits on the argument of a language: of <c>LocalizableText.Inline</c>, or of <c>[Localized]</c> on a
    /// member, which is no text of a set either.
    /// </summary>
    private static MemberDeclarationSyntax? FindText(SyntaxNode root, Diagnostic diagnostic)
    {
        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

        foreach (var ancestor in node.AncestorsAndSelf())
        {
            switch (ancestor)
            {
                case ArgumentSyntax or AttributeArgumentSyntax:
                    return null;
                case PropertyDeclarationSyntax or MethodDeclarationSyntax:
                    return (MemberDeclarationSyntax)ancestor;
                case MemberDeclarationSyntax:
                    return null;
            }
        }

        return null;
    }

    /// <summary>
    /// The text with a parameter in place of each placeholder, or <see langword="null"/> when there's no fix (see the
    /// remarks of this class). For a property, also the same property of every other set of this assembly, each as
    /// the method it becomes.
    /// </summary>
    private static Dictionary<MemberDeclarationSyntax, MemberDeclarationSyntax>? Fix(MemberDeclarationSyntax text, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        if (FindWordings(text) is not { IsEmpty: false } wordings)
            return null;

        if (text is MethodDeclarationSyntax method)
        {
            var parameters = method.ParameterList.Parameters;

            return FillIn(text, wordings, number => number < parameters.Count ? parameters[number].Identifier.ValueText : null) is { } filled
                ? new Dictionary<MemberDeclarationSyntax, MemberDeclarationSyntax> { [text] = filled }
                : null;
        }

        var property = (PropertyDeclarationSyntax)text;
        if (semanticModel.GetDeclaredSymbol(property, cancellationToken) is not { } symbol
            || ToMethod(property, symbol, wordings, semanticModel, cancellationToken) is not { } fixedText)
        {
            return null;
        }

        var fixedTexts = new Dictionary<MemberDeclarationSyntax, MemberDeclarationSyntax> { [text] = fixedText };

        // The same text in the other languages, also where the wording holds no placeholder: a set that kept the
        // property would no longer implement the contract, or override the text.
        foreach (var other in FindOtherLanguages(symbol, semanticModel.Compilation, cancellationToken))
        {
            if (fixedTexts.ContainsKey(other))
                continue;

            var otherModel = semanticModel.Compilation.GetSemanticModel(other.SyntaxTree);

            // A raw string keeps its placeholders, and a wording without any has none to fill in.
            if (otherModel.GetDeclaredSymbol(other, cancellationToken) is { } otherSymbol
                && ToMethod(other, otherSymbol, FindWordings(other) ?? [], otherModel, cancellationToken) is { } otherMethod)
            {
                fixedTexts.Add(other, otherMethod);
            }
        }

        return fixedTexts;
    }

    /// <summary>
    /// A property as the method that takes its values, with a parameter in place of each placeholder of
    /// <paramref name="wordings"/>. <see langword="null"/> when the property can't become one.
    /// </summary>
    private static MethodDeclarationSyntax? ToMethod(PropertyDeclarationSyntax property, IPropertySymbol symbol,
        ImmutableArray<ExpressionSyntax> wordings, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        if (ReadParameters(property, symbol, wordings, semanticModel, cancellationToken) is not { } parameterList)
            return null;

        var names = parameterList.Parameters.Select(static parameter => parameter.Identifier.ValueText).ToArray();

        // A name the text already uses, such as a field of the set, would then mean the parameter.
        if (property.DescendantNodes().OfType<IdentifierNameSyntax>().Any(identifier => names.Contains(identifier.Identifier.ValueText, StringComparer.Ordinal)))
            return null;

        return FillIn(property, wordings, number => number < names.Length ? names[number] : null) is PropertyDeclarationSyntax filled
            ? ToMethod(filled, parameterList)
            : null;
    }

    /// <summary>
    /// The parameters a property takes as a method: those of the contract's method by its name, or one per placeholder
    /// number of the text in any language. <see langword="null"/> when the property implements a text of another
    /// assembly's contract, or overrides another assembly's property, because neither changes with it.
    /// </summary>
    private static ParameterListSyntax? ReadParameters(PropertyDeclarationSyntax property, IPropertySymbol symbol,
        ImmutableArray<ExpressionSyntax> wordings, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        if (symbol.OverriddenProperty is { } overridden && !IsInSource(overridden))
            return null;

        var annotationsEnabled = semanticModel.GetNullableContext(property.SpanStart).AnnotationsEnabled();

        foreach (var contract in symbol.ContainingType.AllInterfaces)
        {
            if (!PackageNames.IsContract(contract))
                continue;

            foreach (var member in contract.GetMembers(symbol.Name))
            {
                if (member is IMethodSymbol { ReturnType.SpecialType: SpecialType.System_String } method)
                    return ParameterList(method.Parameters.Select(parameter => (Type(parameter.Type, property, semanticModel, annotationsEnabled), parameter.Name)));

                if (member is IPropertySymbol && !IsInSource(contract))
                    return null;
            }
        }

        // One parameter per number up to the highest in any language, also for a number no wording of this language
        // shows. Dutch that shows {0} alone then takes what English takes for {0} and {1}.
        var count = 0;
        foreach (var wording in wordings)
            FillIn(wording, Count);

        foreach (var other in FindOtherLanguages(symbol, semanticModel.Compilation, cancellationToken))
        {
            // A raw string gets no fix, and so no say in the count.
            if (FindWordings(other) is not { } otherWordings)
                continue;

            foreach (var wording in otherWordings)
                FillIn(wording, Count);
        }

        return count is > 0 and <= MaxValues
            ? ParameterList(Enumerable.Range(0, count).Select(number => (annotationsEnabled ? "object?" : "object", $"arg{number}")))
            : null;

        string Count(int number)
        {
            count = Math.Max(count, number + 1);
            return "_";
        }
    }

    private static bool IsInSource(ISymbol symbol) => symbol.Locations.Any(static location => location.IsInSource);

    /// <summary>
    /// The same text as the other sets of this assembly word it: each property by the name of <paramref name="text"/>
    /// in a class that implements a contract of <paramref name="text"/>'s class which has a property of that name.
    /// </summary>
    private static IEnumerable<PropertyDeclarationSyntax> FindOtherLanguages(IPropertySymbol text, Compilation compilation, CancellationToken cancellationToken)
    {
        var contracts = text.ContainingType.AllInterfaces
            .Where(contract => PackageNames.IsContract(contract) && contract.GetMembers(text.Name).OfType<IPropertySymbol>().Any())
            .ToImmutableArray();

        if (contracts.IsEmpty)
            yield break;

        foreach (var set in CodeFixSupport.GetClasses(compilation.Assembly.GlobalNamespace))
        {
            if (!set.AllInterfaces.Any(contract => contracts.Contains(contract, SymbolEqualityComparer.Default)))
                continue;

            foreach (var other in set.GetMembers(text.Name).OfType<IPropertySymbol>())
            {
                if (SymbolEqualityComparer.Default.Equals(other, text))
                    continue;

                foreach (var reference in other.DeclaringSyntaxReferences)
                    if (reference.GetSyntax(cancellationToken) is PropertyDeclarationSyntax declaration)
                        yield return declaration;
            }
        }
    }

    private static string Type(ITypeSymbol type, SyntaxNode at, SemanticModel semanticModel, bool annotationsEnabled)
    {
        var format = annotationsEnabled
            ? SymbolDisplayFormat.MinimallyQualifiedFormat.AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier)
            : SymbolDisplayFormat.MinimallyQualifiedFormat;

        return type.ToMinimalDisplayString(semanticModel, at.SpanStart, format);
    }

    private static ParameterListSyntax ParameterList(IEnumerable<(string Type, string Name)> parameters)
    {
        var written = String.Join(", ", parameters.Select(static parameter => $"{parameter.Type} {Escape(parameter.Name)}"));
        return SyntaxFactory.ParseParameterList($"({written})");
    }

    /// <summary>
    /// A name as code writes it: with an <c>@</c> in front of a keyword, as in <c>@class</c>.
    /// </summary>
    private static string Escape(string name) => SyntaxFacts.GetKeywordKind(name) is SyntaxKind.None ? name : "@" + name;

    /// <summary>
    /// The strings and interpolated strings of a text that hold a placeholder, where the analyzer reads them
    /// (<see cref="WrittenWording"/>). <see langword="null"/> when one of them is a raw string, which the fix leaves
    /// alone.
    /// </summary>
    // A string passed to a method isn't among them, so it stays as it is: the method may fill its placeholders in.
    private static ImmutableArray<ExpressionSyntax>? FindWordings(MemberDeclarationSyntax text)
    {
        var wordings = ImmutableArray.CreateBuilder<ExpressionSyntax>();

        foreach (var written in WrittenWording.FindStrings(text))
        {
            if (Placeholder.Find(WrittenWording.Read(written)) is null)
                continue;

            // The whole interpolated string, for a placeholder in its text.
            var wording = written is InterpolatedStringTextSyntax ? written.Parent as InterpolatedStringExpressionSyntax : written as ExpressionSyntax;

            if (wording is null || wordings.Contains(wording))
                continue;

            if (IsRaw(wording))
                return null;

            wordings.Add(wording);
        }

        return wordings.ToImmutable();
    }

    private static bool IsRaw(ExpressionSyntax wording)
    {
        var start = wording switch
        {
            LiteralExpressionSyntax literal => literal.Token,
            InterpolatedStringExpressionSyntax interpolated => interpolated.StringStartToken,
            _ => default,
        };

        return start.Kind() is SyntaxKind.SingleLineRawStringLiteralToken or SyntaxKind.MultiLineRawStringLiteralToken
            or SyntaxKind.InterpolatedSingleLineRawStringStartToken or SyntaxKind.InterpolatedMultiLineRawStringStartToken;
    }

    /// <summary>
    /// The text with each of its wordings filled in (<see cref="FillIn(ExpressionSyntax, Func{int, string?})"/>), or
    /// <see langword="null"/> when a placeholder has no parameter.
    /// </summary>
    private static MemberDeclarationSyntax? FillIn(MemberDeclarationSyntax text, ImmutableArray<ExpressionSyntax> wordings, Func<int, string?> name)
    {
        var filled = new Dictionary<ExpressionSyntax, ExpressionSyntax>();

        foreach (var wording in wordings)
        {
            if (FillIn(wording, name) is not { } written)
                return null;

            filled.Add(wording, SyntaxFactory.ParseExpression(written).WithTriviaFrom(wording));
        }

        return text.ReplaceNodes(filled.Keys, (original, _) => filled[original]);
    }

    /// <summary>
    /// A wording as an interpolated string that shows the parameter <paramref name="name"/> gives for each placeholder
    /// number: <c>"Hey, {0}!"</c> becomes <c>$"Hey, {arg0}!"</c>. Its other braces are doubled, so they still show.
    /// <see langword="null"/> when a placeholder has no parameter.
    /// </summary>
    private static string? FillIn(ExpressionSyntax wording, Func<int, string?> name)
    {
        if (wording is LiteralExpressionSyntax literal)
        {
            var written = literal.Token.Text;
            var start = written.StartsWith("@", StringComparison.Ordinal) ? "@\"" : "\"";

            return FillIn(written.Substring(start.Length, written.Length - start.Length - 1), bracesWrittenTwice: false, name) is { } filled
                ? $"${start}{filled}\""
                : null;
        }

        var interpolated = (InterpolatedStringExpressionSyntax)wording;
        var builder = new StringBuilder(interpolated.StringStartToken.Text);

        foreach (var content in interpolated.Contents)
        {
            if (content is not InterpolatedStringTextSyntax part)
            {
                builder.Append(content.ToFullString());
                continue;
            }

            if (FillIn(part.TextToken.Text, bracesWrittenTwice: true, name) is not { } filled)
                return null;

            builder.Append(filled);
        }

        return builder.Append(interpolated.StringEndToken.Text).ToString();
    }

    /// <summary>
    /// The written text of a string, or of the text of an interpolated string, with an interpolation in place of each
    /// placeholder, as <see cref="Placeholder.Read"/> reads them, and every other brace written twice.
    /// </summary>
    /// <param name="written">The text as the source writes it, without its quotes.</param>
    /// <param name="bracesWrittenTwice">
    /// Whether the source writes a brace that's text twice, as the text of an interpolated string does.
    /// </param>
    /// <param name="name">The parameter for a placeholder number, or <see langword="null"/> when there's none.</param>
    private static string? FillIn(string written, bool bracesWrittenTwice, Func<int, string?> name)
    {
        var filled = new StringBuilder(written.Length + 8);
        var width = bracesWrittenTwice ? 2 : 1;

        for (var i = 0; i < written.Length;)
        {
            var character = written[i];

            if (character is not ('{' or '}'))
            {
                filled.Append(character);
                i++;
                continue;
            }

            if (character is '{' && Placeholder.Read(written, i, width) is var (number, alignmentAndFormat, end))
            {
                if (name(number) is not { } parameter)
                    return null;

                filled.Append('{').Append(Escape(parameter)).Append(alignmentAndFormat).Append('}');
                i = end;
                continue;
            }

            // A brace that's text, written twice in an interpolated string.
            filled.Append(character, 2);
            i += IsRun(written, i, character, width) ? width : 1;
        }

        return filled.ToString();
    }

    private static bool IsRun(string written, int start, char character, int width)
    {
        for (var i = start; i < start + width; i++)
        {
            if (i >= written.Length || written[i] != character)
                return false;
        }

        return true;
    }

    /// <summary>
    /// The property as a method that takes <paramref name="parameters"/>, with the body of the property or of its
    /// getter, or <see langword="null"/> for a property with a setter, which is no text.
    /// </summary>
    private static MethodDeclarationSyntax? ToMethod(PropertyDeclarationSyntax property, ParameterListSyntax parameters)
    {
        var method = SyntaxFactory.MethodDeclaration(
            property.AttributeLists,
            property.Modifiers,
            property.Type,
            property.ExplicitInterfaceSpecifier,
            property.Identifier.WithTrailingTrivia(),
            typeParameterList: null,
            parameters,
            SyntaxFactory.List<TypeParameterConstraintClauseSyntax>(),
            body: null,
            expressionBody: null);

        // public string Greeting => "Hey, {0}!";
        if (property.ExpressionBody is { } expressionBody)
        {
            return method
                .WithParameterList(parameters.WithTrailingTrivia(property.Identifier.TrailingTrivia))
                .WithExpressionBody(expressionBody)
                .WithSemicolonToken(property.SemicolonToken);
        }

        if (property.AccessorList is not { Accessors: [{ Keyword.RawKind: (int)SyntaxKind.GetKeyword } getter] })
            return null;

        // public string Greeting { get; } = "Hey, {0}!";
        if (property.Initializer is { } initializer)
        {
            return getter is { Body: null, ExpressionBody: null }
                ? Arrow(method, initializer.Value, property.SemicolonToken.TrailingTrivia)
                : null;
        }

        // public string Greeting { get => "Hey, {0}!"; }
        if (getter.ExpressionBody is { } getterExpressionBody)
            return Arrow(method, getterExpressionBody.Expression, property.GetTrailingTrivia());

        // public string Greeting { get { return "Hey, {0}!"; } }: the block sits deeper than a method's, so it's formatted.
        return getter.Body is { } body
            ? method
                .WithParameterList(parameters.WithTrailingTrivia(SyntaxFactory.Space))
                .WithBody(body.WithoutTrivia().WithTrailingTrivia(property.GetTrailingTrivia()))
                .WithAdditionalAnnotations(Formatter.Annotation)
            : null;
    }

    private static MethodDeclarationSyntax Arrow(MethodDeclarationSyntax method, ExpressionSyntax expression, SyntaxTriviaList trailingTrivia)
    {
        var arrow = SyntaxFactory.ArrowExpressionClause(
            SyntaxFactory.Token(SyntaxKind.EqualsGreaterThanToken).WithTrailingTrivia(SyntaxFactory.Space),
            expression.WithoutTrivia());

        return method
            .WithParameterList(method.ParameterList.WithTrailingTrivia(SyntaxFactory.Space))
            .WithExpressionBody(arrow)
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken).WithTrailingTrivia(trailingTrivia));
    }
}
