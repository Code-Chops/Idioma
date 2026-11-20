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
/// The same text of every other set of this assembly becomes a method with it, also one without a placeholder, so
/// that each set still implements the contract. The fix finds each through the contract, so the default-language set
/// changes whichever language is fixed. An explicit implementation changes too, and so do an override and an abstract
/// property. A set without a placeholder then leaves the value out, which the build reports (<c>LOC302</c>). A wording
/// in a raw string keeps its placeholder, and its warning.
/// </para>
/// <para>
/// The fix changes every language or none. There's no fix for:
/// </para>
/// <list type="bullet">
/// <item>An inline text, because its wording has no parameter to take the value.</item>
/// <item>A wording in a raw string, unless the fix of another language makes its property a method.</item>
/// <item>A text that a language can't write as a method, such as one with a setter or one the constructor assigns.</item>
/// <item>A text that code of the project uses as a property of a set, such as another text that shows it. That code would no longer compile.</item>
/// <item>A text of a member of an enum, because the contract takes it from the enum.</item>
/// <item>A text that takes no values, of another assembly's contract or one written by hand, because the fix doesn't change a contract.</item>
/// <item>A property that overrides one of another assembly, because that property doesn't change with it.</item>
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
    /// remarks of this class). For a property, also the same text of every other set of this assembly, each as the
    /// method it becomes.
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
        var compilation = semanticModel.Compilation;

        if (semanticModel.GetDeclaredSymbol(property, cancellationToken) is not { } symbol
            || FindLanguages(symbol, compilation, cancellationToken) is not var (languages, properties, implementsContractText)
            || ReadParameters(symbol, implementsContractText, languages) is not { } writeParameters
            || IsUsedAsProperty(properties, compilation, semanticModel, cancellationToken))
        {
            return null;
        }

        var fixedTexts = new Dictionary<MemberDeclarationSyntax, MemberDeclarationSyntax>();

        // Every language or none, also where the wording holds no placeholder. A set that kept the property would no
        // longer implement the contract, or override the text.
        foreach (var language in languages)
        {
            var languageModel = language.SyntaxTree == property.SyntaxTree ? semanticModel : compilation.GetSemanticModel(language.SyntaxTree);

            // A raw string keeps its placeholders, and a wording without any has none to fill in.
            if (ToMethod(language, FindWordings(language) ?? [], writeParameters(language, languageModel)) is not { } fixedText)
                return null;

            fixedTexts.Add(language, fixedText);
        }

        return fixedTexts;
    }

    /// <summary>
    /// A property as the method that takes <paramref name="parameters"/>, with a parameter in place of each placeholder
    /// of <paramref name="wordings"/>. <see langword="null"/> when the property can't become one.
    /// </summary>
    private static MethodDeclarationSyntax? ToMethod(PropertyDeclarationSyntax property, ImmutableArray<ExpressionSyntax> wordings, ParameterListSyntax parameters)
    {
        var names = parameters.Parameters.Select(static parameter => parameter.Identifier.ValueText).ToArray();

        // A name the text already uses, such as a field of the set, would then mean the parameter.
        if (property.DescendantNodes().OfType<IdentifierNameSyntax>().Any(identifier => names.Contains(identifier.Identifier.ValueText, StringComparer.Ordinal)))
            return null;

        return FillIn(property, wordings, number => number < names.Length ? names[number] : null) is PropertyDeclarationSyntax filled
            ? ToMethod(filled, parameters)
            : null;
    }

    /// <summary>
    /// The parameters each language's property takes as a method, as the file of that property writes them: those of
    /// the contract's method by the name of the text, or one per placeholder number of the text in any language.
    /// <see langword="null"/> when the text takes no value, or more than a text can take.
    /// </summary>
    /// <param name="text">The property the fix starts from.</param>
    /// <param name="implementsContractText">Whether the properties implement a property of a contract.</param>
    /// <param name="languages">The text in every language that changes with it (<see cref="FindLanguages"/>).</param>
    private static Func<PropertyDeclarationSyntax, SemanticModel, ParameterListSyntax>? ReadParameters(IPropertySymbol text, bool implementsContractText,
        ImmutableArray<PropertyDeclarationSyntax> languages)
    {
        // The default language made the text a method, and this language still words it as a property.
        if (!implementsContractText && FindContractMethod(text) is { } method)
        {
            return (property, semanticModel) => ParameterList(method.Parameters.Select(parameter =>
                (Type(parameter.Type, property, semanticModel, AnnotationsEnabled(property, semanticModel)), parameter.Name)));
        }

        // One parameter per number up to the highest in any language, also for a number no wording of a language
        // shows. Dutch that shows {0} alone then takes what English takes for {0} and {1}.
        var count = 0;

        // A raw string gets no fix, and so no say in the count.
        foreach (var language in languages)
            foreach (var wording in FindWordings(language) ?? [])
                FillIn(wording, Count);

        return count is > 0 and <= MaxValues
            ? (property, semanticModel) => ParameterList(Enumerable.Range(0, count).Select(number =>
                (AnnotationsEnabled(property, semanticModel) ? "object?" : "object", $"arg{number}")))
            : null;

        string Count(int number)
        {
            count = Math.Max(count, number + 1);
            return "_";
        }
    }

    private static bool AnnotationsEnabled(SyntaxNode at, SemanticModel semanticModel) => semanticModel.GetNullableContext(at.SpanStart).AnnotationsEnabled();

    /// <summary>
    /// The method of a contract of the property's set that has the name of the text and returns a string, or
    /// <see langword="null"/> when no contract has one.
    /// </summary>
    private static IMethodSymbol? FindContractMethod(IPropertySymbol text)
    {
        foreach (var contract in text.ContainingType.AllInterfaces)
        {
            if (!PackageNames.IsContract(contract))
                continue;

            foreach (var member in contract.GetMembers(GetTextName(text)))
                if (member is IMethodSymbol { ReturnType.SpecialType: SpecialType.System_String } method)
                    return method;
        }

        return null;
    }

    /// <summary>
    /// The text as each set of this assembly words it, starting from the property of one language: the property itself,
    /// the property each set implements the same text of the contract with, also explicitly, and each property one of
    /// these overrides or is overridden by. Each is found through the symbol, so an explicit implementation counts too.
    /// </summary>
    /// <returns>
    /// The declaration of each property, and whether they implement a property of a contract. <see langword="null"/>
    /// when one of them can't change with the others:
    /// <list type="bullet">
    /// <item>A property of another assembly, such as one that a set here overrides.</item>
    /// <item>A text of a contract the build doesn't write here, such as another assembly's or one written by hand.</item>
    /// <item>A text of a member of an enum, which the contract takes from the enum.</item>
    /// <item>A property that's declared other than as a property, such as a parameter of a record.</item>
    /// </list>
    /// </returns>
    private static (ImmutableArray<PropertyDeclarationSyntax> Languages, ImmutableHashSet<IPropertySymbol> Properties, bool ImplementsContractText)? FindLanguages(
        IPropertySymbol text, Compilation compilation, CancellationToken cancellationToken)
    {
        var classes = CodeFixSupport.GetClasses(compilation.Assembly.GlobalNamespace).ToImmutableArray();
        var found = new HashSet<IPropertySymbol>(SymbolEqualityComparer.Default);
        var contractTexts = new HashSet<IPropertySymbol>(SymbolEqualityComparer.Default);
        var languages = ImmutableArray.CreateBuilder<PropertyDeclarationSyntax>();
        var pending = new Stack<IPropertySymbol>();

        pending.Push(text);

        while (pending.Count > 0)
        {
            var property = pending.Pop();

            if (!found.Add(property))
                continue;

            if (!IsOwn(property, compilation) || IsTextOfEnumMember(property) || property.DeclaringSyntaxReferences.IsEmpty)
                return null;

            foreach (var reference in property.DeclaringSyntaxReferences)
            {
                if (reference.GetSyntax(cancellationToken) is not PropertyDeclarationSyntax declaration)
                    return null;

                languages.Add(declaration);
            }

            if (property.OverriddenProperty is { } overridden)
                pending.Push(overridden);

            foreach (var set in classes)
                foreach (var member in set.GetMembers(property.Name))
                    if (member is IPropertySymbol overriding && SymbolEqualityComparer.Default.Equals(overriding.OverriddenProperty, property))
                        pending.Push(overriding);

            foreach (var contractText in FindImplementedMembers(property))
            {
                var contract = contractText.ContainingType;

                if (!IsOwn(contract, compilation) || !PackageNames.IsGeneratedContract(contract))
                    return null;

                if (!contractTexts.Add(contractText))
                    continue;

                // A set that derives from another implements the text through the property of that set. Its own
                // property is found as the override of that one.
                foreach (var set in classes)
                {
                    if (set.AllInterfaces.Contains(contract, SymbolEqualityComparer.Default)
                        && set.FindImplementationForInterfaceMember(contractText) is IPropertySymbol implementation)
                    {
                        pending.Push(implementation);
                    }
                }
            }
        }

        return (languages.ToImmutable(), ImmutableHashSet.CreateRange<IPropertySymbol>(SymbolEqualityComparer.Default, found), contractTexts.Count > 0);
    }

    /// <summary>
    /// Whether code of this project uses one of the properties as a property, other than the contract does: another
    /// text that shows it, an assignment in a constructor, or code that reads it from a set. As a method, the property
    /// is gone, and that code would no longer compile.
    /// </summary>
    /// <remarks>
    /// Code that reads the text through the contract or the generated class, as in <c>ShopLocalization.Greeting</c>,
    /// doesn't count. It reads another member, which becomes a method that takes the value, and the compiler then
    /// points at each place that has to pass it. Neither does a reference in a doc comment.
    /// </remarks>
    // By name first, which binds nothing, and then by the compiler for a name that matches: one of a few in a project.
    private static bool IsUsedAsProperty(ImmutableHashSet<IPropertySymbol> properties, Compilation compilation, SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var names = properties.Select(GetTextName).ToImmutableHashSet(StringComparer.Ordinal);

        foreach (var tree in compilation.SyntaxTrees)
        {
            SemanticModel? model = null;

            foreach (var identifier in tree.GetRoot(cancellationToken).DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                if (!names.Contains(identifier.Identifier.ValueText))
                    continue;

                model ??= tree == semanticModel.SyntaxTree ? semanticModel : compilation.GetSemanticModel(tree);

                if (model.GetSymbolInfo(identifier, cancellationToken).Symbol is IPropertySymbol used && properties.Contains(used.OriginalDefinition))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The properties of interfaces that a property implements: explicitly, or as the member its class implements
    /// them through.
    /// </summary>
    private static IEnumerable<IPropertySymbol> FindImplementedMembers(IPropertySymbol property)
    {
        if (!property.ExplicitInterfaceImplementations.IsEmpty)
            return property.ExplicitInterfaceImplementations;

        var set = property.ContainingType;

        return set.AllInterfaces
            .SelectMany(@interface => @interface.GetMembers(property.Name).OfType<IPropertySymbol>())
            .Where(member => SymbolEqualityComparer.Default.Equals(set.FindImplementationForInterfaceMember(member), property));
    }

    /// <summary>
    /// Whether a property is the text of a member of an enum, in a set that lists <c>ILocalizationSet&lt;TEnum&gt;</c>.
    /// The contract takes such a text from the enum, so it stays a property.
    /// </summary>
    private static bool IsTextOfEnumMember(IPropertySymbol property)
    {
        var name = GetTextName(property);

        return property.ContainingType.AllInterfaces.Any(@interface => PackageNames.IsLocalizationSetOfEnum(@interface)
                                                                       && @interface.TypeArguments[0] is INamedTypeSymbol { TypeKind: TypeKind.Enum } @enum
                                                                       && @enum.GetMembers(name).OfType<IFieldSymbol>().Any());
    }

    /// <summary>
    /// The name of the text a property words, without the contract an explicit implementation carries in front of it.
    /// </summary>
    private static string GetTextName(IPropertySymbol property) => property.Name.Substring(property.Name.LastIndexOf('.') + 1);

    /// <summary>
    /// Whether a symbol is declared in the assembly being fixed. A project an IDE references is compiled from source
    /// too, but its symbols don't change with this one.
    /// </summary>
    private static bool IsOwn(ISymbol symbol, Compilation compilation) => SymbolEqualityComparer.Default.Equals(symbol.ContainingAssembly, compilation.Assembly);

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
    /// getter. An abstract property becomes an abstract method. <see langword="null"/> for a property with a setter,
    /// which is no text, and for one whose wording the constructor assigns, which has no body to take.
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

        // public abstract string Greeting { get; }
        if (getter is { Body: null, ExpressionBody: null })
        {
            return property.Modifiers.Any(SyntaxKind.AbstractKeyword)
                ? method.WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken).WithTrailingTrivia(property.GetTrailingTrivia()))
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
