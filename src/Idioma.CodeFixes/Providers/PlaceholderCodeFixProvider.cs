using System.Collections.Concurrent;
using System.Composition;
using System.Runtime.CompilerServices;
using System.Text;
using CodeChops.Idioma.Generators;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;

namespace CodeChops.Idioma.CodeFixes;

/// <summary>
/// Fixes a text of a set whose wording holds a placeholder of a format string (<c>LOC403</c>). It writes a parameter in
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
/// property. A set without a placeholder then leaves the value out, which the build reports (<c>LOC402</c>). A wording
/// in a raw string keeps its placeholder, and its warning.
/// </para>
/// <para>
/// A set that reads the text of the set it derives from, as a regional set does in
/// <c>base.Greeting.Replace("color", "colour")</c>, passes the values on: <c>base.Greeting(arg0)</c>.
/// </para>
/// <para>
/// The fix changes every language or none. There's no fix for:
/// </para>
/// <list type="bullet">
/// <item>An inline text, because its wording has no parameter to take the value.</item>
/// <item>A wording in a raw string, unless the fix of another language makes its property a method.</item>
/// <item>A text that a language can't write as a method, such as one with a setter or one the constructor assigns.</item>
/// <item>A text that other code of the project uses as a property of a set, such as another text that shows it. That code would no longer compile.</item>
/// <item>A text that code names as a property of the generated class: in <c>nameof</c>, or as a resource of an attribute, as <c>[Display(Name = nameof(ShopLocalization.Name), ResourceType = typeof(ShopLocalization))]</c> does. That code would go on compiling, and fail at run time.</item>
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
    private const string EquivalenceKey = "FillInPlaceholders";

    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [PlaceholderAnalyzer.PlaceholderDiagnosticId];

    /// <summary>
    /// Changes each text once, however many of its wordings and languages hold a placeholder, because each such wording
    /// has a diagnostic of its own. A diagnostic of a text that an earlier fix changed already is skipped, and the
    /// project is read once for all of them (<see cref="ProjectTexts"/>). A migration from resx files has hundreds of
    /// such texts.
    /// </summary>
    private static readonly FixAllProvider FixAll = new ReplaceNodesFixAllProvider<MemberDeclarationSyntax>(
        "Replace the placeholders with parameters",
        EquivalenceKey,
        static (root, semanticModel, diagnostic, cancellationToken) => FindText(root, diagnostic) is { } text ? Fix(text, semanticModel, cancellationToken) : null,
        prepare: static (root, semanticModel, diagnostic) =>
        {
            // The name of each property that may become a method, so that one pass finds the code that uses any of them.
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
            // Whether a property gets the fix depends on the other languages and on the code that uses the text, so
            // the fix is found here. It's found once per text and compilation (ProjectTexts), so asking again is cheap.
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
    /// diagnostic sits on the argument of a language: of <c>LocalizedText.Inline</c>, or of <c>[Localized]</c> on a
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

            return FillIn(text, wordings, number => number < parameters.Count ? parameters[number].Identifier.ValueText : null, [], []) is { } filled
                ? new Dictionary<MemberDeclarationSyntax, MemberDeclarationSyntax> { [text] = filled }
                : null;
        }

        var property = (PropertyDeclarationSyntax)text;
        var projectTexts = ProjectTexts.Of(semanticModel.Compilation);

        return projectTexts.GetOrAddFix(property, () => ToMethods(property, semanticModel, projectTexts, cancellationToken));
    }

    /// <summary>
    /// A property and the same text of every other set of this assembly, each as the method it becomes, or
    /// <see langword="null"/> when there's no fix (see the remarks of this class).
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

        // Every language or none, also where the wording holds no placeholder. A set that kept the property would no
        // longer implement the contract, or override the text.
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
    /// A property as the method that takes <paramref name="parameters"/>, with a parameter in place of each placeholder
    /// of <paramref name="wordings"/>, and each of <paramref name="baseReads"/> as a call that passes them on.
    /// <see langword="null"/> when the property can't become one.
    /// </summary>
    private static MethodDeclarationSyntax? ToMethod(PropertyDeclarationSyntax property, ImmutableArray<ExpressionSyntax> wordings,
        ImmutableArray<MemberAccessExpressionSyntax> baseReads, ParameterListSyntax parameters)
    {
        var names = parameters.Parameters.Select(static parameter => parameter.Identifier.ValueText).ToArray();

        // A name the text already uses, such as a field of the set, would then mean the parameter.
        if (property.DescendantNodes().OfType<IdentifierNameSyntax>().Any(identifier => names.Contains(identifier.Identifier.ValueText, StringComparer.Ordinal)))
            return null;

        return FillIn(property, wordings, number => number < names.Length ? names[number] : null, baseReads, names) is PropertyDeclarationSyntax filled
            ? ToMethod(filled, parameters)
            : null;
    }

    /// <summary>
    /// Where a property reads the text of the set its set derives from, as in <c>base.Greeting</c>, which becomes a method
    /// too. Each such read becomes a call that passes the values on (<see cref="PassOn"/>).
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
    /// Whether an identifier is the name in a read of <c>base</c> inside the text in one of the languages, as in
    /// <c>base.Greeting</c>. Such a read becomes a call along with the text (<see cref="FindBaseReads"/>).
    /// </summary>
    private static bool IsReadOfBaseInLanguage(IdentifierNameSyntax identifier, ImmutableArray<PropertyDeclarationSyntax> languages)
    {
        var isReadOfBase = identifier.Parent is MemberAccessExpressionSyntax { Expression: BaseExpressionSyntax } access
                           && access.Name == identifier
                           && languages.Any(language => language.SyntaxTree == access.SyntaxTree && language.Span.Contains(access.Span));

        return isReadOfBase;
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

            // An override is a member of a class that derives from the property's class, so only those are read.
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

                // A set that derives from another implements the text through the property of that set. Its own
                // property is found as the override of that one.
                foreach (var set in projectTexts.FindClassesImplementing(contract))
                    if (set.FindImplementationForInterfaceMember(contractText) is IPropertySymbol implementation)
                        pending.Push(implementation);
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
    /// <para>
    /// Code that reads the text through the contract or the generated class, as in <c>ShopLocalization.Greeting</c>,
    /// doesn't count. It reads another member, which becomes a method that takes the value, and the compiler then
    /// points at each place that has to pass it. Neither does a reference in a doc comment, nor a read of
    /// <c>base.Greeting</c> in the text itself, which passes the values on once both are methods.
    /// </para>
    /// <para>
    /// Code that names the text of the generated class does count, because it goes on compiling while it no longer
    /// works:
    /// </para>
    /// <list type="bullet">
    /// <item><c>nameof(ShopLocalization.NameRequired)</c>, which names a method as well as a property.</item>
    /// <item>An attribute that reads the text as a resource, as <c>[Required]</c> with <c>ErrorMessageResourceName</c>
    /// and <c>[Display]</c> with <c>Name</c> do (<see cref="ResourceNamedTexts"/>). It finds the property by its name
    /// at run time, and throws where there's a method.</item>
    /// <item><c>ShopLocalization.GetTextByKey("Greeting")</c>, which passes no values, and throws once the text takes
    /// them (<see cref="IsAskedForByKeyWithoutValues"/>). A key the code only has at run time, such as one from a
    /// database, can't be seen here.</item>
    /// </list>
    /// </remarks>
    // By name first, which binds nothing, and then by the compiler for a name that matches: one of a few in a project.
    private static bool IsUsedAsProperty(ImmutableHashSet<IPropertySymbol> properties, ImmutableArray<PropertyDeclarationSyntax> languages,
        ProjectTexts projectTexts, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        var models = new Dictionary<SyntaxTree, SemanticModel> { [semanticModel.SyntaxTree] = semanticModel };

        // The generated class of each contract whose text the properties implement.
        var generatedClasses = properties
            .SelectMany(FindImplementedMembers)
            .Select(ResourceNamedTexts.FindGeneratedClass)
            .OfType<INamedTypeSymbol>()
            .ToImmutableHashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        var resourceNamed = ResourceNamedTexts.Of(projectTexts.Compilation);
        var names = properties.Select(GetTextName).ToImmutableHashSet(StringComparer.Ordinal);

        // So that one pass over each file finds the uses of every name.
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
    /// Whether code asks a generated class for one of the texts by its key, written as a constant, and passes no values,
    /// as in <c>ShopLocalization.GetTextByKey("Greeting")</c>. That goes on compiling once the text is a method, and
    /// throws, because the text then takes values.
    /// </summary>
    private static bool IsAskedForByKeyWithoutValues(ImmutableHashSet<string> names, ImmutableHashSet<INamedTypeSymbol> generatedClasses,
        ProjectTexts projectTexts, Dictionary<SyntaxTree, SemanticModel> models, CancellationToken cancellationToken)
    {
        foreach (var identifier in projectTexts.FindUsesOf(LocalizationSetsAnalysis.GetName, cancellationToken))
        {
            // The method by its name alone, inside the generated class, or after the class.
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

    /// <summary>
    /// Whether a name stands inside <c>nameof(...)</c> in the expression around it.
    /// </summary>
    private static bool IsInNameof(IdentifierNameSyntax identifier)
    {
        for (var node = identifier.Parent; node is ExpressionSyntax or ArgumentSyntax or ArgumentListSyntax; node = node.Parent)
            if (node is InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" } })
                return true;

        return false;
    }

    /// <summary>
    /// What the fix reads of a whole project, read once per compilation: the classes that derive from a class or
    /// implement a contract, the code that uses a name, and the fix of each text it was asked for. A compilation
    /// doesn't change, so neither does what's read of it.
    /// </summary>
    /// <remarks>
    /// The IDE asks for the fix of a text each time it shows the lightbulb, and "Fix all" asks for one per diagnostic.
    /// Each of them would otherwise read every class and every file of the project again.
    /// </remarks>
    private sealed class ProjectTexts(Compilation compilation)
    {
        private static readonly ConditionalWeakTable<Compilation, ProjectTexts> PerCompilation = new();

        private object Gate { get; } = new();
        private Dictionary<PropertyDeclarationSyntax, Dictionary<MemberDeclarationSyntax, MemberDeclarationSyntax>?> Fixes { get; } = [];
        private HashSet<string> ExpectedNames { get; } = new(StringComparer.Ordinal);
        private Dictionary<string, ImmutableArray<IdentifierNameSyntax>> UsesByName { get; } = new(StringComparer.Ordinal);
        private Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>>? ClassesByBase { get; set; }
        private Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>>? ClassesByInterface { get; set; }

        public Compilation Compilation { get; } = compilation;

        public static ProjectTexts Of(Compilation compilation) => PerCompilation.GetValue(compilation, static compilation => new ProjectTexts(compilation));

        /// <summary>
        /// The fix of a property, found with <paramref name="find"/> the first time it's asked for.
        /// </summary>
        public Dictionary<MemberDeclarationSyntax, MemberDeclarationSyntax>? GetOrAddFix(PropertyDeclarationSyntax property,
            Func<Dictionary<MemberDeclarationSyntax, MemberDeclarationSyntax>?> find)
        {
            lock (this.Gate)
            {
                if (this.Fixes.TryGetValue(property, out var known))
                    return known;
            }

            var found = find();

            lock (this.Gate)
                this.Fixes[property] = found;

            return found;
        }

        /// <summary>
        /// The top-level classes of this assembly that derive from a class, directly or through another class.
        /// </summary>
        public IReadOnlyList<INamedTypeSymbol> FindClassesDerivingFrom(INamedTypeSymbol @base)
        {
            lock (this.Gate)
            {
                this.ReadClasses();
                return this.ClassesByBase!.TryGetValue(@base, out var classes) ? classes : [];
            }
        }

        /// <summary>
        /// The top-level classes of this assembly that implement an interface, themselves or through a class they
        /// derive from.
        /// </summary>
        public IReadOnlyList<INamedTypeSymbol> FindClassesImplementing(INamedTypeSymbol @interface)
        {
            lock (this.Gate)
            {
                this.ReadClasses();
                return this.ClassesByInterface!.TryGetValue(@interface, out var classes) ? classes : [];
            }
        }

        private void ReadClasses()
        {
            if (this.ClassesByBase is not null)
                return;

            var byBase = new Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>>(SymbolEqualityComparer.Default);
            var byInterface = new Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>>(SymbolEqualityComparer.Default);

            foreach (var type in CodeFixSupport.GetClasses(this.Compilation.Assembly.GlobalNamespace))
            {
                for (var @base = type.BaseType; @base is { SpecialType: not SpecialType.System_Object }; @base = @base.BaseType)
                    Add(byBase, @base, type);

                foreach (var @interface in type.AllInterfaces)
                    Add(byInterface, @interface, type);
            }

            this.ClassesByInterface = byInterface;
            this.ClassesByBase = byBase;

            static void Add(Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>> classes, INamedTypeSymbol key, INamedTypeSymbol type)
            {
                if (!classes.TryGetValue(key, out var found))
                    classes.Add(key, found = []);

                found.Add(type);
            }
        }

        /// <summary>
        /// Notes a name that <see cref="FindUsesOf"/> will be asked for, so that one pass over the project finds the
        /// uses of every such name.
        /// </summary>
        public void ExpectUsesOf(string name)
        {
            lock (this.Gate)
            {
                if (!this.UsesByName.ContainsKey(name))
                    this.ExpectedNames.Add(name);
            }
        }

        /// <summary>
        /// Each place in the code of the project, generated code included, where a name is used, outside comments.
        /// </summary>
        /// <remarks>
        /// Each file is searched once for a name, and where it uses the name is kept with its syntax tree, which an
        /// edit of another file leaves as it was. So after an edit, only the file it changed is searched again.
        /// </remarks>
        public ImmutableArray<IdentifierNameSyntax> FindUsesOf(string name, CancellationToken cancellationToken)
        {
            lock (this.Gate)
            {
                if (this.UsesByName.TryGetValue(name, out var known))
                    return known;

                this.ExpectedNames.Add(name);

                var expected = this.ExpectedNames.ToArray();
                var found = expected.ToDictionary(static expected => expected, static _ => ImmutableArray.CreateBuilder<IdentifierNameSyntax>(), StringComparer.Ordinal);

                foreach (var tree in this.Compilation.SyntaxTrees)
                {
                    var spansByName = UsesPerTree.GetValue(tree, static _ => new ConcurrentDictionary<string, ImmutableArray<TextSpan>>(StringComparer.Ordinal));
                    var root = FindSpans(tree, spansByName, expected, cancellationToken);

                    foreach (var expectedName in expected)
                    {
                        var spans = spansByName[expectedName];

                        if (spans.IsEmpty)
                            continue;

                        root ??= tree.GetRoot(cancellationToken);

                        foreach (var span in spans)
                            if (root.FindNode(span, getInnermostNodeForTie: true) is IdentifierNameSyntax identifier)
                                found[expectedName].Add(identifier);
                    }
                }

                foreach (var uses in found)
                    this.UsesByName[uses.Key] = uses.Value.ToImmutable();

                this.ExpectedNames.Clear();

                return this.UsesByName[name];
            }
        }

        /// <summary>
        /// Per file, where each name that was asked for is used.
        /// </summary>
        // Spans only, so the syntax of the file isn't kept alive by this.
        private static readonly ConditionalWeakTable<SyntaxTree, ConcurrentDictionary<string, ImmutableArray<TextSpan>>> UsesPerTree = new();

        /// <summary>
        /// Searches a file for the names it hasn't been searched for yet, in one pass, and keeps where it uses each. Gives
        /// the root of the file where it was searched, else <see langword="null"/>.
        /// </summary>
        private static SyntaxNode? FindSpans(SyntaxTree tree, ConcurrentDictionary<string, ImmutableArray<TextSpan>> spansByName, string[] names,
            CancellationToken cancellationToken)
        {
            var missing = names.Where(name => !spansByName.ContainsKey(name)).ToDictionary(static name => name, static _ => ImmutableArray.CreateBuilder<TextSpan>(), StringComparer.Ordinal);

            if (missing.Count is 0)
                return null;

            var root = tree.GetRoot(cancellationToken);

            foreach (var identifier in root.DescendantNodes().OfType<IdentifierNameSyntax>())
                if (missing.TryGetValue(identifier.Identifier.ValueText, out var spans))
                    spans.Add(identifier.Span);

            foreach (var spans in missing)
                spansByName.TryAdd(spans.Key, spans.Value.ToImmutable());

            return root;
        }
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
    /// The text with each of its wordings filled in (<see cref="FillIn(ExpressionSyntax, Func{int, string?})"/>), and
    /// each of <paramref name="baseReads"/> as a call that passes <paramref name="arguments"/> on. <see langword="null"/>
    /// when a placeholder has no parameter.
    /// </summary>
    private static MemberDeclarationSyntax? FillIn(MemberDeclarationSyntax text, ImmutableArray<ExpressionSyntax> wordings, Func<int, string?> name,
        ImmutableArray<MemberAccessExpressionSyntax> baseReads, IReadOnlyList<string> arguments)
    {
        // Checked on the wordings as written. A wording inside another changes what that one holds in a value, and not
        // its text, so the filled-in one has the same placeholders.
        if (wordings.Any(wording => FillIn(wording, name) is null))
            return null;

        // Each from what it holds after the nodes inside it changed, so that a wording inside another one, as in
        // $"Hey {{0}}{(more ? " and {1}" : "")}", or a read of base inside a wording, changes too.
        return text.ReplaceNodes(
            wordings.Cast<SyntaxNode>().Concat(baseReads),
            (original, changed) => original is MemberAccessExpressionSyntax
                ? PassOn((ExpressionSyntax)changed, arguments)
                : SyntaxFactory.ParseExpression(FillIn((ExpressionSyntax)changed, name)!).WithTriviaFrom(original));
    }

    /// <summary>
    /// A read of the text of the set a set derives from as a call that passes the values on: <c>base.Greeting</c>
    /// becomes <c>base.Greeting(arg0)</c>.
    /// </summary>
    private static InvocationExpressionSyntax PassOn(ExpressionSyntax read, IReadOnlyList<string> arguments)
    {
        var argumentList = SyntaxFactory.ParseArgumentList($"({String.Join(", ", arguments.Select(Escape))})");

        return SyntaxFactory.InvocationExpression(read.WithoutTrailingTrivia(), argumentList).WithTrailingTrivia(read.GetTrailingTrivia());
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
