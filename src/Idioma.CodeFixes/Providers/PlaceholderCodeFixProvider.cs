using System.Composition;
using System.Text;
using CodeChops.Idioma.Generators;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace CodeChops.Idioma.CodeFixes;

/// <summary>
/// Fixes a text of a set whose wording holds a format-string placeholder (<c>LOC403</c>) by writing a parameter in
/// place of each placeholder. A property becomes a method in every language of this assembly at once. A method uses
/// its own parameters, the first for <c>{0}</c>.
/// </summary>
/// <remarks>
/// <para>
/// A property takes the parameters of the contract's method of that name, if there is one. Otherwise it takes one
/// <c>object?</c> parameter per placeholder number (<c>arg0</c> for <c>{0}</c>), counted over every language, so each
/// language gets the same parameters.
/// </para>
/// <para>
/// The same text of every other set of this assembly becomes a method too, so each set still implements the contract.
/// A set without a placeholder then leaves the value out, which the build reports (<c>LOC402</c>). A read of
/// <c>base.Greeting</c> becomes <c>base.Greeting(arg0)</c>.
/// </para>
/// <para>The fix changes every language or none. There's no fix for:</para>
/// <list type="bullet">
/// <item>a standalone text, which has no parameter to take the value.</item>
/// <item>a wording in a raw string, unless another language's fix makes its property a method.</item>
/// <item>a text that can't be a method, such as one with a setter or one the constructor assigns.</item>
/// <item>a text other code uses as a property of a set, because that code would stop compiling.</item>
/// <item>a text code names on the generated class, in <c>nameof</c> or as an attribute's resource, because that would fail at run time.</item>
/// <item>a text of an enum member, which the contract takes from the enum.</item>
/// <item>a text of a contract the build doesn't generate here, because the fix doesn't change a contract.</item>
/// <item>a property that overrides one of another assembly.</item>
/// <item>a placeholder without a parameter.</item>
/// </list>
/// <para>
/// A string passed to a method, such as <c>string.Format</c>, stays as it is, because the method may fill it in.
/// </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PlaceholderCodeFixProvider))]
[Shared]
public sealed partial class PlaceholderCodeFixProvider : CodeFixProvider
{
    private const string EquivalenceKey = "FillInPlaceholders";

    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [PlaceholderAnalyzer.PlaceholderDiagnosticId];

    /// <summary>
    /// Changes each text once, though each of its wordings has a diagnostic. The project is read once for all of them
    /// (<see cref="ProjectTexts"/>).
    /// </summary>
    private static readonly FixAllProvider FixAll = new ReplaceNodesFixAllProvider<MemberDeclarationSyntax>(
        "Replace the placeholders with parameters",
        EquivalenceKey,
        static (root, semanticModel, diagnostic, cancellationToken) => FindText(root, diagnostic) is { } text ? Fix(text, semanticModel, cancellationToken) : null,
        prepare: static (root, semanticModel, diagnostic) =>
        {
            // So one pass finds the uses of every property that may become a method.
            if (FindText(root, diagnostic) is PropertyDeclarationSyntax property)
                ProjectTexts.Of(semanticModel.Compilation).ExpectUsesOf(property.Identifier.ValueText);
        },
        findStart: FindText);

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
            // Whether there's a fix depends on the other languages and the text's uses. It's cached (ProjectTexts).
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
    /// The property or method of a set a diagnostic sits in, or <see langword="null"/> for a standalone text, whose
    /// diagnostic sits on an argument.
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
    /// The text with a parameter in place of each placeholder, and for a property also every other set's text. Gives
    /// <see langword="null"/> when there's no fix (see the class remarks).
    /// </summary>
    private static Dictionary<MemberDeclarationSyntax, MemberDeclarationSyntax>? Fix(MemberDeclarationSyntax text, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        if (FindWordings(text) is not { IsEmpty: false } wordings)
            return null;

        if (text is MethodDeclarationSyntax method)
        {
            var parameters = method.ParameterList.Parameters;

            return FillIn(text, wordings, number => number < parameters.Count ? parameters[number].Identifier.ValueText : null, [], []) is { } filled
                ? new Dictionary<MemberDeclarationSyntax, MemberDeclarationSyntax> { [text] = filled }
                : null;
        }

        var property = (PropertyDeclarationSyntax)text;
        var projectTexts = ProjectTexts.Of(semanticModel.Compilation);

        return projectTexts.GetOrAddFix(property, () => ToMethods(property, semanticModel, projectTexts, cancellationToken));
    }

    /// <summary>
    /// A property and the same text of every other set of this assembly, each as a method, or <see langword="null"/>.
    /// </summary>
    private static Dictionary<MemberDeclarationSyntax, MemberDeclarationSyntax>? ToMethods(PropertyDeclarationSyntax property, SemanticModel semanticModel,
        ProjectTexts projectTexts, CancellationToken cancellationToken)
    {
        var compilation = semanticModel.Compilation;

        if (semanticModel.GetDeclaredSymbol(property, cancellationToken) is not { } symbol
            || FindLanguages(symbol, projectTexts, cancellationToken) is not var (languages, properties, implementsContractText)
            || ReadParameters(symbol, implementsContractText, languages) is not { } writeParameters)
        {
            return null;
        }

        var fixedTexts = new Dictionary<MemberDeclarationSyntax, MemberDeclarationSyntax>();

        // Every language or none, because a set that kept the property would no longer implement the contract.
        foreach (var language in languages)
        {
            var languageModel = language.SyntaxTree == property.SyntaxTree ? semanticModel : compilation.GetSemanticModel(language.SyntaxTree);
            var baseReads = FindBaseReads(language, properties, languageModel, cancellationToken);

            // A raw string keeps its placeholders, and a wording without any has none to fill in.
            if (ToMethod(language, FindWordings(language) ?? [], baseReads, writeParameters(language, languageModel)) is not { } fixedText)
                return null;

            fixedTexts.Add(language, fixedText);
        }

        // Last, because it reads every file of the project.
        return IsUsedAsProperty(properties, languages, projectTexts, semanticModel, cancellationToken) ? null : fixedTexts;
    }

    /// <summary>
    /// A property as a method with its placeholders filled in and its <paramref name="baseReads"/> passing the values
    /// on, or <see langword="null"/> when it can't become one.
    /// </summary>
    private static MethodDeclarationSyntax? ToMethod(PropertyDeclarationSyntax property, ImmutableArray<ExpressionSyntax> wordings,
        ImmutableArray<MemberAccessExpressionSyntax> baseReads, ParameterListSyntax parameters)
    {
        var names = parameters.Parameters.Select(static parameter => parameter.Identifier.ValueText).ToArray();

        // A name the text already uses, such as a field, would then bind to the parameter.
        if (property.DescendantNodes().OfType<IdentifierNameSyntax>().Any(identifier => names.Contains(identifier.Identifier.ValueText, StringComparer.Ordinal)))
            return null;

        return FillIn(property, wordings, number => number < names.Length ? names[number] : null, baseReads, names) is PropertyDeclarationSyntax filled
            ? ToMethod(filled, parameters)
            : null;
    }

    /// <summary>
    /// The reads of the base set's text, as in <c>base.Greeting</c>, which become calls (<see cref="PassOn"/>).
    /// </summary>
    private static ImmutableArray<MemberAccessExpressionSyntax> FindBaseReads(PropertyDeclarationSyntax language, ImmutableHashSet<IPropertySymbol> properties,
        SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        return
        [
            .. language.DescendantNodes()
                .OfType<MemberAccessExpressionSyntax>()
                .Where(access => access.Expression is BaseExpressionSyntax
                                 && semanticModel.GetSymbolInfo(access, cancellationToken).Symbol is IPropertySymbol read
                                 && properties.Contains(read.OriginalDefinition)),
        ];
    }

    /// <summary>
    /// Whether an identifier is a <c>base.Greeting</c> read inside one of the languages (<see cref="FindBaseReads"/>).
    /// </summary>
    private static bool IsReadOfBaseInLanguage(IdentifierNameSyntax identifier, ImmutableArray<PropertyDeclarationSyntax> languages)
    {
        var isReadOfBase = identifier.Parent is MemberAccessExpressionSyntax { Expression: BaseExpressionSyntax } access
                           && access.Name == identifier
                           && languages.Any(language => language.SyntaxTree == access.SyntaxTree && language.Span.Contains(access.Span));

        return isReadOfBase;
    }

    /// <summary>
    /// Writes the parameter list for each language's method: the contract method's parameters, or one per placeholder
    /// number in any language. Gives <see langword="null"/> when the text takes no value.
    /// </summary>
    /// <param name="implementsContractText">Whether the properties implement a property of a contract.</param>
    /// <param name="languages">The text in every language (<see cref="FindLanguages"/>).</param>
    private static Func<PropertyDeclarationSyntax, SemanticModel, ParameterListSyntax>? ReadParameters(IPropertySymbol text, bool implementsContractText,
        ImmutableArray<PropertyDeclarationSyntax> languages)
    {
        // The contract already has a method, and this language still has a property.
        if (!implementsContractText && FindContractMethod(text) is { } method)
        {
            return (property, semanticModel) => ParameterList(method.Parameters.Select(parameter =>
                (Type(parameter.Type, property, semanticModel, AnnotationsEnabled(property, semanticModel)), parameter.Name)));
        }

        // One parameter per number up to the highest in any language, so Dutch with only {0} takes English's {1} too.
        var count = 0;

        // A raw string gets no fix, so it doesn't count.
        foreach (var language in languages)
            foreach (var wording in FindWordings(language) ?? [])
                FillIn(wording, Count);

        return count > 0
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
    /// The contract method with the text's name that returns a string, or <see langword="null"/>.
    /// </summary>
    private static IMethodSymbol? FindContractMethod(IPropertySymbol text)
    {
        foreach (var contract in text.ContainingType.AllInterfaces)
        {
            if (!PackageNames.IsContract(contract))
                continue;

            foreach (var member in contract.GetMembers(AnalyzerSupport.GetTextName(text)))
                if (member is IMethodSymbol { ReturnType.SpecialType: SpecialType.System_String } method)
                    return method;
        }

        return null;
    }

    /// <summary>
    /// The text in every set of this assembly, starting from one language's property: the implementations of the same
    /// contract text, explicit ones included, and the properties they override or are overridden by.
    /// </summary>
    /// <returns>
    /// The declarations, and whether they implement a contract text. <see langword="null"/> when one can't change:
    /// <list type="bullet">
    /// <item>a property of another assembly.</item>
    /// <item>a text of a contract the build doesn't generate here.</item>
    /// <item>a text of an enum member.</item>
    /// <item>a property not declared as a property, such as a record parameter.</item>
    /// </list>
    /// </returns>
    private static (ImmutableArray<PropertyDeclarationSyntax> Languages, ImmutableHashSet<IPropertySymbol> Properties, bool ImplementsContractText)? FindLanguages(
        IPropertySymbol text, ProjectTexts projectTexts, CancellationToken cancellationToken)
    {
        var compilation = projectTexts.Compilation;
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

            // Only derived classes can hold an override.
            foreach (var set in projectTexts.FindClassesDerivingFrom(property.ContainingType))
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

                // A derived set implements the text through its base's property, so its own is found as an override.
                foreach (var set in projectTexts.FindClassesImplementing(contract))
                    if (set.FindImplementationForInterfaceMember(contractText) is IPropertySymbol implementation)
                        pending.Push(implementation);
            }
        }

        return (languages.ToImmutable(), ImmutableHashSet.CreateRange<IPropertySymbol>(SymbolEqualityComparer.Default, found), contractTexts.Count > 0);
    }

    /// <summary>
    /// Whether project code uses one of the properties as a property of a set, which would stop compiling once it's a
    /// method.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A read through the contract or the generated class doesn't count, because the compiler then points at each
    /// place that must pass the value. Neither does a <c>base.Greeting</c> read in the text itself.
    /// </para>
    /// <para>These uses of the generated class do count, because they keep compiling but fail at run time:</para>
    /// <list type="bullet">
    /// <item><c>nameof(ShopLocalization.NameRequired)</c>.</item>
    /// <item>an attribute that reads the text as a resource (<see cref="ResourceNamedTexts"/>).</item>
    /// <item><c>ShopLocalization.GetTextByKey("Greeting")</c> with a constant key (<see cref="IsAskedForByKeyWithoutValues"/>).</item>
    /// </list>
    /// </remarks>
    // Matches by name first, which binds nothing, and binds only the few names that match.
    private static bool IsUsedAsProperty(ImmutableHashSet<IPropertySymbol> properties, ImmutableArray<PropertyDeclarationSyntax> languages,
        ProjectTexts projectTexts, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        var models = new Dictionary<SyntaxTree, SemanticModel> { [semanticModel.SyntaxTree] = semanticModel };

        var generatedClasses = properties
            .SelectMany(FindImplementedMembers)
            .Select(ResourceNamedTexts.FindGeneratedClass)
            .OfType<INamedTypeSymbol>()
            .ToImmutableHashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        var resourceNamed = ResourceNamedTexts.Of(projectTexts.Compilation);
        var names = properties.Select(AnalyzerSupport.GetTextName).ToImmutableHashSet(StringComparer.Ordinal);

        // So one pass over each file finds every name.
        foreach (var name in names)
            projectTexts.ExpectUsesOf(name);

        if (IsAskedForByKeyWithoutValues(names, generatedClasses, projectTexts, models, cancellationToken))
            return true;

        foreach (var name in names)
        {
            if (generatedClasses.Any(generatedClass => resourceNamed.IsNamed(generatedClass, name, cancellationToken)))
                return true;

            foreach (var identifier in projectTexts.FindUsesOf(name, cancellationToken))
            {
                if (IsReadOfBaseInLanguage(identifier, languages))
                    continue;

                if (!models.TryGetValue(identifier.SyntaxTree, out var model))
                    models.Add(identifier.SyntaxTree, model = projectTexts.Compilation.GetSemanticModel(identifier.SyntaxTree));

                if (model.GetSymbolInfo(identifier, cancellationToken).Symbol is not IPropertySymbol used)
                    continue;

                if (properties.Contains(used.OriginalDefinition)
                    || (used.IsStatic && generatedClasses.Contains(used.ContainingType) && IsInNameof(identifier)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Whether code gets one of the texts by a constant key without values, as in
    /// <c>ShopLocalization.GetTextByKey("Greeting")</c>, which would throw once the text takes values.
    /// </summary>
    private static bool IsAskedForByKeyWithoutValues(ImmutableHashSet<string> names, ImmutableHashSet<INamedTypeSymbol> generatedClasses,
        ProjectTexts projectTexts, Dictionary<SyntaxTree, SemanticModel> models, CancellationToken cancellationToken)
    {
        foreach (var identifier in projectTexts.FindUsesOf(LocalizationSetsAnalysis.GetName, cancellationToken))
        {
            // Called by its simple name or after the class name.
            var invocation = identifier.Parent is MemberAccessExpressionSyntax access && access.Name == identifier
                ? access.Parent as InvocationExpressionSyntax
                : identifier.Parent as InvocationExpressionSyntax;

            if (invocation?.ArgumentList.Arguments is not [var key])
                continue;

            if (!models.TryGetValue(identifier.SyntaxTree, out var model))
                models.Add(identifier.SyntaxTree, model = projectTexts.Compilation.GetSemanticModel(identifier.SyntaxTree));

            if (model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol method
                && generatedClasses.Contains(method.ContainingType)
                && model.GetConstantValue(key.Expression, cancellationToken) is { HasValue: true, Value: string name }
                && names.Contains(name))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInNameof(IdentifierNameSyntax identifier)
    {
        for (var node = identifier.Parent; node is ExpressionSyntax or ArgumentSyntax or ArgumentListSyntax; node = node.Parent)
            if (node is InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" } })
                return true;

        return false;
    }

    /// <summary>
    /// The interface properties a property implements, explicitly or implicitly.
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
    /// Whether a property is an enum member's text in a set that lists <c>ILocalizationSet&lt;TEnum&gt;</c>. It stays
    /// a property, because the contract takes it from the enum.
    /// </summary>
    private static bool IsTextOfEnumMember(IPropertySymbol property)
    {
        var name = AnalyzerSupport.GetTextName(property);

        return property.ContainingType.AllInterfaces.Any(@interface => PackageNames.IsLocalizationSetOfEnum(@interface)
                                                                       && @interface.TypeArguments[0] is INamedTypeSymbol { TypeKind: TypeKind.Enum } @enum
                                                                       && @enum.GetMembers(name).OfType<IFieldSymbol>().Any());
    }

    /// <summary>
    /// Whether a symbol is declared in the assembly being fixed, and not in a referenced project.
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
    /// Prefixes a keyword with <c>@</c>.
    /// </summary>
    private static string Escape(string name) => SyntaxFacts.GetKeywordKind(name) is SyntaxKind.None ? name : "@" + name;

    /// <summary>
    /// The strings of a text that hold a placeholder, as the analyzer reads them (<see cref="WrittenWording"/>). Gives
    /// <see langword="null"/> when one is a raw string.
    /// </summary>
    // Leaves out a string passed to a method, because the method may fill it in.
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
    /// The text with its wordings filled in (<see cref="FillIn(ExpressionSyntax, Func{int, string?})"/>) and its
    /// <paramref name="baseReads"/> passing <paramref name="arguments"/> on. Gives <see langword="null"/> when a
    /// placeholder has no parameter.
    /// </summary>
    private static MemberDeclarationSyntax? FillIn(MemberDeclarationSyntax text, ImmutableArray<ExpressionSyntax> wordings, Func<int, string?> name,
        ImmutableArray<MemberAccessExpressionSyntax> baseReads, IReadOnlyList<string> arguments)
    {
        // Checked on the wordings as written, because filling in a nested wording doesn't change the outer one's text.
        if (wordings.Any(wording => FillIn(wording, name) is null))
            return null;

        // Each from its changed node, so a nested wording or base read changes too, as in
        // $"Hey {{0}}{(more ? " and {1}" : "")}".
        return text.ReplaceNodes(
            wordings.Cast<SyntaxNode>().Concat(baseReads),
            (original, changed) => original is MemberAccessExpressionSyntax
                ? PassOn((ExpressionSyntax)changed, arguments)
                : SyntaxFactory.ParseExpression(FillIn((ExpressionSyntax)changed, name)!).WithTriviaFrom(original));
    }

    /// <summary>
    /// <c>base.Greeting</c> becomes <c>base.Greeting(arg0)</c>.
    /// </summary>
    private static InvocationExpressionSyntax PassOn(ExpressionSyntax read, IReadOnlyList<string> arguments)
    {
        var argumentList = SyntaxFactory.ParseArgumentList($"({String.Join(", ", arguments.Select(Escape))})");

        return SyntaxFactory.InvocationExpression(read.WithoutTrailingTrivia(), argumentList).WithTrailingTrivia(read.GetTrailingTrivia());
    }

    /// <summary>
    /// A wording as an interpolated string: <c>"Hey, {0}!"</c> becomes <c>$"Hey, {arg0}!"</c>, with other braces
    /// doubled. Gives <see langword="null"/> when a placeholder has no parameter.
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
    /// Source text with an interpolation in place of each placeholder (<see cref="Placeholder.Read"/>) and every other
    /// brace doubled.
    /// </summary>
    /// <param name="written">The source text, without quotes.</param>
    /// <param name="bracesWrittenTwice">Whether a literal brace is written twice, as in an interpolated string.</param>
    /// <param name="name">The parameter for a placeholder number, or <see langword="null"/>.</param>
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

            // A literal brace.
            filled.Append(character, 2);
            i += IsRun(written, i, character, width) ? width : 1;
        }

        return filled.ToString();
    }

    private static bool IsRun(string written, int start, char character, int width)
    {
        for (var i = start; i < start + width; i++)
            if (i >= written.Length || written[i] != character)
                return false;

        return true;
    }

    /// <summary>
    /// The property as a method with the body of the property or its getter. Gives <see langword="null"/> for a
    /// property with a setter, or one the constructor assigns.
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

        // public string Greeting { get { return "Hey, {0}!"; } }, formatted because the block sat deeper.
        return getter.Body is { } body
            ? method
                .WithParameterList(parameters.WithTrailingTrivia(SyntaxFactory.Space))
                .WithBody(body.WithoutTrivia().WithTrailingTrivia(property.GetTrailingTrivia()))
                .WithAdditionalAnnotations(CodeFixSupport.FormatAnnotation)
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
