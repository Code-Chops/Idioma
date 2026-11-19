namespace CodeChops.Idioma.Generators;

/// <summary>
/// Holds the language sets of an assembly to the languages of <c>[assembly: SupportedLanguages(...)]</c>, and writes the
/// contract of each default-language set. For a top-level class listing <c>ILocalizationSet</c> and named after the
/// default language (<c>ShopLocalizationEn : ILocalizationSet, IShopLocalization</c>) it emits the contract interface
/// (<c>IShopLocalization</c>, one <see cref="string"/> member per public string property, marked
/// <c>[LocalizationContract]</c>), which the set lists itself, and the static partial class named after the set without
/// its language suffix
/// (<c>ShopLocalization</c>, one <c>LocalizableText</c> per member, keyed by its full name, as in
/// <c>MyApp.Localization.ShopLocalization.Checkout</c>, and worded by the set, and no other member); the interface and
/// the class take the set's accessibility. A public method of the set that returns a <see cref="string"/> and takes
/// parameters is a text that takes values: the contract repeats it, and the class holds a method of its name and
/// parameters that creates the <c>LocalizableText</c> with the values it is called with. Each
/// generated member inherits the property's doc comment through <c>&lt;inheritdoc cref/&gt;</c>, or else is documented
/// with its default-language wording, and links to its wording in every language, which it shows next to the link.
/// </summary>
/// <remarks>
/// It also records every set of a public contract in an assembly attribute, so that the build of an assembly referencing
/// this one can tell which languages exist without searching its types, and reports the languages referenced libraries
/// leave open. The generator sees the compilation before its own output is added, so the interfaces of a set are exactly
/// the handwritten ones.
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class LocalizationSetsGenerator : IIncrementalGenerator
{
    public const string NoLanguagesDeclaredDiagnosticId = "LOC100";
    public const string CannotExtendDiagnosticId = "LOC200";
    public const string MissingLanguageDiagnosticId = "LOC201";
    public const string UnlistedLanguageDiagnosticId = "LOC202";
    public const string DerivedFromOtherLanguageDiagnosticId = "LOC203";
    public const string MissingContractDiagnosticId = "LOC204";
    public const string LibraryMissingLanguageDiagnosticId = "LOC600";
    public const string LibraryCannotBeTranslatedDiagnosticId = "LOC601";
    public const string ReservedTextNameDiagnosticId = "LOC205";
    public const string UnsupportedTextDiagnosticId = "LOC300";

    /// <summary>
    /// The property of a diagnostic that holds the name that resolves it, for its code fix to apply: the name the class
    /// should carry (<c>LOC200</c>, when a rename resolves it), the contract to add next to <c>ILocalizationSet</c>
    /// (<c>LOC204</c>, when this build generates that contract), or the set to add (<c>LOC201</c>).
    /// </summary>
    public const string SuggestedNameProperty = "SuggestedName";

    /// <summary>
    /// The property of <c>LOC201</c> that holds the namespace of the set to add, empty for the global namespace.
    /// </summary>
    public const string SuggestedNamespaceProperty = "SuggestedNamespace";

    /// <summary>
    /// The property of <c>LOC201</c> that holds the declaration of the set to add, without a body:
    /// <c>public class ShopLocalizationDe : ILocalizationSet, IShopLocalization</c>.
    /// </summary>
    public const string SuggestedDeclarationProperty = "SuggestedDeclaration";

    /// <summary>
    /// The line terminators of C#, the pair first so that it counts as one break.
    /// </summary>
    private static readonly string[] LineBreaks = ["\r\n", "\r", "\n", "\u0085", "\u2028", "\u2029"];

    /// <summary>
    /// The longest a wording stands next to its link, in characters. A tooltip lists every language, and a long wording
    /// in each would bury the rest.
    /// </summary>
    private const int MaxWordingLength = 80;

    private static readonly DiagnosticDescriptor NoLanguagesDeclaredRule = Error(
        NoLanguagesDeclaredDiagnosticId,
        "Localization set in an assembly without languages",
        "'{0}' is a localization set, but this assembly declares no languages. Add [assembly: SupportedLanguages(\"en\", \"nl\")] with your languages, the default first.",
        "The generator writes the contract from the default-language set and holds the other sets to it, so it needs to know the languages. A set that rewords a library's set, or adds a language to its contract, needs no declaration.");

    private static readonly DiagnosticDescriptor CannotExtendRule = Error(
        CannotExtendDiagnosticId,
        "Default-language set the generator can't use",
        "'{0}' is the default-language set of a contract, but {1}. The generator can't use it, so it writes no texts for it. Make it a top-level class that ends in its language (ShopLocalizationEn) and has a public parameterless constructor. It can't be generic, abstract, or file-local.",
        "The generator writes the contract from the default-language set. It names the generated class after the set without its language suffix, and it creates the set for the default wording of every text. So the set must be a class that generated code in another file can name and create.");

    private static readonly DiagnosticDescriptor MissingLanguageRule = Error(
        MissingLanguageDiagnosticId,
        "Contract lacks a language",
        "'{0}' has no set for '{1}', which [assembly: SupportedLanguages] requires. Add {2}. A class without ILocalizationSet doesn't count as a set, and neither does a nested, generic, or file-local one.",
        "Every contract needs a set for each language of [assembly: SupportedLanguages], just as every inline text takes wording for each. That includes a listed region, such as en-GB next to en. Its set derives from the set of its language and holds only the texts that differ.");

    private static readonly DiagnosticDescriptor UnlistedLanguageRule = Error(
        UnlistedLanguageDiagnosticId,
        "Localization set for an unlisted language",
        "'{0}' is a set for '{1}', which [assembly: SupportedLanguages] doesn't list. {2}",
        "[assembly: SupportedLanguages] is the only list of languages the assembly supports, and only sets of those languages are checked for completeness. This also holds for a set marked [ExcludeFromLocalizationDiscovery]. An app shows a culture outside its list in its default language, so registering such a set by hand wouldn't make it show.");

    private static readonly DiagnosticDescriptor DerivedFromOtherLanguageRule = Error(
        DerivedFromOtherLanguageDiagnosticId,
        "Localization set derives from a set of another language",
        "'{0}' is a set for '{1}' but derives from '{2}', so every text it doesn't override stays in '{3}'. To translate, implement the contract instead, which requires every text. To reword '{2}', give '{0}' the language '{3}': end its name in it, or implement LanguageCode.",
        "Derive from a set only to reword some of its texts in the same language. In another language, it compiles with texts left untranslated. A regional set may derive from the set of its language, as en-GB from en. Reported in every assembly, with or without [assembly: SupportedLanguages].");

    private static readonly DiagnosticDescriptor MissingContractRule = Error(
        MissingContractDiagnosticId,
        "Localization set doesn't implement its contract",
        "'{0}' is a set for '{1}' but doesn't implement '{2}', so no text reads it. Add '{2}', the contract generated from '{3}', next to ILocalizationSet.",
        "Every set lists ILocalizationSet and its contract, the default-language set included. The contract is what requires every text in every language, and what a text reads its wording through.");

    private static readonly DiagnosticDescriptor LibraryMissingLanguageRule = Error(
        LibraryMissingLanguageDiagnosticId,
        "Referenced contract lacks a language",
        "No set serves '{0}' in '{1}', which this assembly supports. Add {2}.",
        "An assembly that supports a language must show the texts of the libraries it references in that language too. A set added here is registered automatically and serves the library's texts.");

    private static readonly DiagnosticDescriptor LibraryCannotBeTranslatedRule = new(
        id: LibraryCannotBeTranslatedDiagnosticId,
        title: "Referenced library's inline texts can't be translated",
        messageFormat: "'{0}' has inline texts without '{1}', which this assembly supports. No set added here can translate them, so users of '{1}' will see them in the default language of '{0}'.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Nothing outside a library can add a language to its inline texts. A library that wants to be translatable writes its texts in sets.",
        helpLinkUri: PackageNames.HelpLink(LibraryCannotBeTranslatedDiagnosticId));

    private static readonly DiagnosticDescriptor ReservedTextNameRule = Error(
        ReservedTextNameDiagnosticId,
        "Text named like the generated class",
        "'{0}' has a text named '{1}', like the generated class '{2}', which can't hold a member of its own name. '{2}.{1}' isn't generated. Rename the text.",
        "In C#, a member can't have the name of its own class, so no text can have the name of the generated class.");

    private static readonly DiagnosticDescriptor UnsupportedTextRule = Error(
        UnsupportedTextDiagnosticId,
        "Method that can't be a text",
        "'{0}.{1}' is a public method that returns a string, which makes it a text with parameters, but {2}. No text is generated for it. Fix the method. If it isn't a text, make it internal or private, or mark it [ExcludeFromLocalizationDiscovery].",
        "A public method of a default-language set that returns a string and takes parameters is a text with parameters. The generated class gets a method with the same name and parameters. It keeps the values and passes them to the set of the user's language when the text is shown. That only works for a method that isn't generic, has a name no other method in its set has, and takes at most 8 parameters, each by value and without a default. A method without parameters is no text, because a text without parameters is a property.");

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var languages = SupportedLanguages.Read(context);
        var discovery = LocalizationDiscovery.Read(context, DiscoveredTexts.OfContract);
        var bindings = LocalizationDiscovery.BindContracts(context, discovery, languages);

        var analysis = discovery
            .Combine(languages)
            .Combine(bindings)
            .Select(static (data, _) => LocalizationSetsAnalysis.Analyze(
                data.Left.Left.Sets,
                data.Left.Left.UnextendableSets,
                data.Left.Right,
                data.Right));

        // Separate outputs, so that an edit in the file of a set, which renews its location, reports its diagnostics
        // anew without rewriting the generated code: the contracts and the recorded sets hold no location.
        context.RegisterSourceOutput(analysis.Select(static (analysis, _) => analysis.Findings), Report);

        // Each contract under a hint name of its own, which the analysis gives it (LocalizationContract.HintName).
        context.RegisterSourceOutput(analysis.SelectMany(static (analysis, _) => analysis.Contracts.AsImmutableArray()), static (context, contract) =>
            context.AddSource($"{contract.HintName}.Localization.g.cs", GenerateContract(contract)));

        var providedSets = analysis.Select(static (analysis, _) => analysis.ProvidedSets);

        context.RegisterSourceOutput(providedSets, static (context, sets) =>
        {
            if (sets.Length > 0)
                context.AddSource("LocalizationSets.g.cs", GenerateProvidedSets(sets));
        });

        // Runs on every change of the compilation in an IDE, so it reads no more than a few assembly attributes, and
        // ends in a plain value that keeps everything after it cached.
        var references = context.CompilationProvider.Select(static (compilation, _) => ReadReferences(compilation));

        var gaps = languages
            .Combine(providedSets)
            .Combine(references)
            .Select(static (data, _) => LocalizationSetsAnalysis.FindLibraryGaps(data.Left.Left, data.Left.Right, data.Right));

        context.RegisterSourceOutput(gaps, Report);
    }

    private static DiagnosticDescriptor Error(string id, string title, string message, string description)
    {
        var error = new DiagnosticDescriptor(id, title, message, "Design", DiagnosticSeverity.Error, isEnabledByDefault: true, description, PackageNames.HelpLink(id));
        return error;
    }

    private static void Report(SourceProductionContext context, EquatableArray<LocalizationSetsFinding> findings)
    {
        foreach (var finding in findings)
        {
            var rule = finding.Rule switch
            {
                LocalizationSetsRule.NoLanguagesDeclared => NoLanguagesDeclaredRule,
                LocalizationSetsRule.CannotExtend => CannotExtendRule,
                LocalizationSetsRule.MissingLanguage => MissingLanguageRule,
                LocalizationSetsRule.UnlistedLanguage => UnlistedLanguageRule,
                LocalizationSetsRule.DerivedFromOtherLanguage => DerivedFromOtherLanguageRule,
                LocalizationSetsRule.MissingContract => MissingContractRule,
                LocalizationSetsRule.LibraryMissingLanguage => LibraryMissingLanguageRule,
                LocalizationSetsRule.LibraryCannotBeTranslated => LibraryCannotBeTranslatedRule,
                LocalizationSetsRule.UnsupportedText => UnsupportedTextRule,
                LocalizationSetsRule.ReservedTextName => ReservedTextNameRule,
                // A rule added without a descriptor here. The generator fails with CS8785, and a test of the new rule
                // shows it, where reporting it under another code would go unnoticed.
                _ => throw new ArgumentOutOfRangeException(nameof(findings), finding.Rule, "A rule without a descriptor."),
            };

            var properties = ImmutableDictionary<string, string?>.Empty;

            if (finding.SuggestedName is { } suggestedName)
                properties = properties.Add(SuggestedNameProperty, suggestedName);

            if (finding.NewSet is { } newSet)
            {
                properties = properties
                    .Add(SuggestedNamespaceProperty, newSet.Namespace)
                    .Add(SuggestedDeclarationProperty, newSet.Declaration);
            }

            context.ReportDiagnostic(Diagnostic.Create(rule, finding.Location.ToLocation(), properties.IsEmpty ? null : properties,
                finding.Arguments.Cast<object>().ToArray()));
        }
    }

    /// <summary>
    /// What the referenced assemblies say about their languages, in a fixed order. Only assemblies that reference this
    /// package can hold its attributes, which keeps the framework's assemblies out of this.
    /// </summary>
    private static EquatableArray<ReferencedLocalization> ReadReferences(Compilation compilation)
    {
        var references = ImmutableArray.CreateBuilder<ReferencedLocalization>();

        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            if (!LocalizationDiscovery.ReferencesPackage(assembly))
                continue;

            var sets = ImmutableArray.CreateBuilder<ProvidedLocalizationSet>();
            var hasInlineTexts = false;

            foreach (var attribute in assembly.GetAttributes())
            {
                var name = attribute.AttributeClass?.ToDisplayString();

                if (String.Equals(name, PackageNames.LocalizationSetAttribute, StringComparison.Ordinal)
                    && attribute.ConstructorArguments.Length is 2
                    && attribute.ConstructorArguments[0].Value is string contract
                    && attribute.ConstructorArguments[1].Value is string language)
                {
                    sets.Add(new ProvidedLocalizationSet(contract, language));
                }
                else if (String.Equals(name, PackageNames.InlineTextsAttribute, StringComparison.Ordinal))
                {
                    hasInlineTexts = true;
                }
            }

            var languages = SupportedLanguages.ReadCodes(assembly);

            if (sets.Count > 0 || languages.Length > 0)
            {
                references.Add(new ReferencedLocalization(
                    assembly.Identity.Name,
                    new EquatableArray<string>(languages),
                    new EquatableArray<ProvidedLocalizationSet>(sets.ToImmutable()),
                    hasInlineTexts));
            }
        }

        var referencedLocalization = new EquatableArray<ReferencedLocalization>(
            references.OrderBy(static reference => reference.AssemblyName, StringComparer.Ordinal)
                .ToImmutableArray());

        return referencedLocalization;
    }

    private static string GenerateProvidedSets(EquatableArray<ProvidedLocalizationSet> sets)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine($"#pragma warning disable {LocalizationRegistrationGenerator.ObsoleteWarnings} // Only generated code applies LocalizationSet.");

        foreach (var set in sets)
            sb.AppendLine($"[assembly: {PackageNames.LocalizationSetAttribute}({SymbolDisplay.FormatLiteral(set.Contract, quote: true)}, {SymbolDisplay.FormatLiteral(set.Language, quote: true)})]");

        sb.AppendLine($"#pragma warning restore {LocalizationRegistrationGenerator.ObsoleteWarnings}");

        return sb.ToString();
    }

    private static string GenerateContract(LocalizationContract contract)
    {
        // The set by its simple name: this file lies in the set's namespace. Its full name would start with a name the
        // namespace may hold something else under, as a namespace Shop that holds a class Shop does.
        var className = EscapeIdentifier(contract.ClassName);
        var name = contract.ContractName;
        var textsName = EscapeIdentifier(contract.TextsName);
        var @namespace = EscapeNamespace(contract.Namespace);
        var texts = contract.Members.Where(member => !LocalizationSetsAnalysis.IsReservedTextName(member.Name, contract.TextsName)).ToArray();
        var accessibility = contract.IsPublic ? "public" : "internal";
        var sampleMember = GetSampleText(texts);

        var aliases = GetNamespaceAliases(contract);

        // What creates a text is imported here, at the top of the file, and called by its name alone. Inside the
        // generated class a text stands before every namespace of its name, so behind a text named CodeChops the
        // package's namespace could not be reached to call it. The pragma also covers a set marked [Obsolete], whose texts
        // read it for as long as it exists.
        var sb = new StringBuilder();
        sb.AppendLine($"""
            // <auto-generated/>
            #nullable enable
            #pragma warning disable {LocalizationRegistrationGenerator.ObsoleteWarnings} // Only generated code calls GeneratedCode.

            using static {PackageNames.GeneratedCode};
            """);

        // The namespace of a set that lies outside the contract's own is named here too, where a name is looked for
        // from the global namespace on, and reached through its alias below. Inside the contract's namespace its full
        // name would start with a name that namespace may hold something else under, as a namespace Shop that holds a
        // class Shop does, and the link to the set would name nothing (CS1574).
        foreach (var alias in aliases)
            sb.AppendLine($"using {alias.Value} = {EscapeNamespace(alias.Key)};");

        // The using directives of the default-language set, for a text that takes a value of a type that bound to
        // nothing when this was written, as one another generator writes. Such a type stands here by the words the
        // set writes it in, and those name it only next to the directives the set has. A type that did bind needs
        // none of them: it stands here by its own name, through the alias of its namespace.
        AppendUsings(sb, contract.Usings.OfFile);
        sb.AppendLine();

        if (@namespace.Length > 0)
        {
            sb.AppendLine($"""
                namespace {@namespace};

                """);

            // Those the set has inside its namespace, where a name may be written from that namespace on.
            if (contract.Usings.OfNamespace.Length > 0)
            {
                AppendUsings(sb, contract.Usings.OfNamespace);
                sb.AppendLine();
            }
        }

        // The contract.
        sb.AppendLine($$"""
            /// <summary>
            /// The contract of <see cref="{{className}}"/>, with one member per text.
            /// Every language in <c>[assembly: SupportedLanguages]</c> has a set that implements it next to <see cref="{{PackageNames.LocalizationSet}}"/>, named with its language as suffix.
            /// A language without a set, or a set without a text, is a build error rather than a fallback at run time.
            /// Each set is registered automatically, so there is nothing to register.
            /// </summary>
            [{{PackageNames.InternalsNamespace}}.LocalizationContract]
            {{accessibility}} interface {{name}}
            {
            """);
        foreach (var member in contract.Members)
        {
            AppendMemberDocs(sb, contract, aliases, className, member);

            if (!member.IsMethod)
            {
                sb.AppendLine($"    string {EscapeIdentifier(member.Name)} {{ get; }}");
                continue;
            }

            // Each value the default wording shows, which TextParametersAnalyzer holds every other set to, in this
            // assembly and in those referencing it. On the method and not on the parameter, because an IDE's "Implement
            // interface" copies the attributes of a parameter into the set it writes, where this obsolete one would warn.
            foreach (var parameter in member.Parameters)
            {
                if (parameter.IsShown)
                    sb.AppendLine($"    [{PackageNames.InternalsNamespace}.ShownValue({SymbolDisplay.FormatLiteral(parameter.Name, quote: true)})]");
            }

            // A text that takes values is a method: its parameters are what the compiler holds every language to.
            sb.AppendLine($"    string {EscapeIdentifier(member.Name)}({RenderParameters(member, contract.Namespace, aliases)});");
        }

        // The end of the contract, which every set lists itself. Then the texts, partial so that a helper can sit next
        // to them, unless the generated code can't create the set for their default wording (LOC200).
        sb.AppendLine("}");

        if (contract.HasTexts)
            AppendTexts(sb, contract, aliases, className, name, textsName, texts, accessibility, sampleMember);

        sb.AppendLine($"#pragma warning restore {LocalizationRegistrationGenerator.ObsoleteWarnings}");
        sb.AppendLine("#nullable restore");

        return sb.ToString();
    }

    /// <summary>
    /// Writes the using directives a contract's file repeats (<see cref="RepeatedUsings"/>), but for the one it has
    /// already.
    /// </summary>
    private static void AppendUsings(StringBuilder sb, EquatableArray<string> usings)
    {
        foreach (var directive in usings)
            if (!String.Equals(directive, $"using static {PackageNames.GeneratedCode};", StringComparison.Ordinal))
                sb.AppendLine(directive);
    }

    /// <summary>
    /// Writes the generated class with the texts of a contract, and the set they take their default wording from.
    /// </summary>
    private static void AppendTexts(StringBuilder sb, LocalizationContract contract, SortedDictionary<string, string> aliases, string className,
        string name, string textsName, LocalizationMember[] texts, string accessibility, string sampleMember)
    {
        sb.AppendLine();
        sb.AppendLine($$"""
            /// <summary>
            /// The texts of <see cref="{{className}}"/>, one <see cref="{{PackageNames.LocalizableText}}"/> per text.
            /// </summary>
            /// <remarks>
            /// Each text follows the current UI culture. <c>@{{contract.TextsName}}.{{sampleMember}}</c> in markup shows the wording of the first of these:
            /// <list type="number">
            /// <item><description>The <see cref="{{name}}"/> set registered for the current UI culture.</description></item>
            /// <item><description>The set registered for the app's <see cref="{{PackageNames.LocalizationRegistry}}.DefaultLanguage"/>.</description></item>
            /// <item><description>The set registered for the language of <see cref="{{className}}"/>.</description></item>
            /// <item><description>The wording in <see cref="{{className}}"/>.</description></item>
            /// </list>
            /// </remarks>
            {{accessibility}} static partial class {{textsName}}
            {
            """);

        // Nothing but the texts, so that a text can take any name but the class's own (LOC205). Each key is written out
        // in full, as in "MyApp.Localization.ShopLocalization.Checkout": a constant for the prefix would take a name
        // from the texts.
        for (var i = 0; i < texts.Length; i++)
        {
            var member = texts[i];
            var memberName = EscapeIdentifier(member.Name);
            var keyLiteral = SymbolDisplay.FormatLiteral(contract.KeyPrefix + member.Name, quote: true);

            if (i > 0)
                sb.AppendLine();

            AppendMemberDocs(sb, contract, aliases, className, member);

            if (!member.IsMethod)
            {
                sb.AppendLine($"    public static {(HidesObjectMember(member) ? "new " : "")}{PackageNames.LocalizableText} {memberName} {{ get; }} = CreateSetText<{name}>({keyLiteral}, static set => set.{memberName}, DefaultSet<{className}>.Instance);");
                continue;
            }

            // A text that takes values is created on every call, with the values it is called with: the text keeps
            // them, each as its own type, and hands them to the set of the user's language when it is shown. So the
            // set is picked by a function that is handed the values back and holds none itself: one function then
            // serves every call, where one holding the values would be a new object per call. Its parameters repeat
            // the names of the method's, which they shadow, and the set is named apart from them.
            var types = String.Join(", ", member.Parameters.Select(parameter => RenderType(parameter.Type, contract.Namespace, aliases)));
            var arguments = String.Join(", ", member.Parameters.Select(static parameter => EscapeIdentifier(parameter.Name)));
            var set = GetNameApartFrom(member, "set");

            sb.AppendLine($"    public static {(HidesObjectMember(member) ? "new " : "")}{PackageNames.LocalizableText} {memberName}({RenderParameters(member, contract.Namespace, aliases)}) => CreateSetText<{name}, {types}>({keyLiteral}, static ({set}, {arguments}) => {set}.{memberName}({arguments}), DefaultSet<{className}>.Instance, {arguments});");
        }

        // The end of the texts, and the set every text takes its default wording from. File-local, so that inside this
        // file its name binds to it even next to another type of that name. Generic, so that no text can stand before
        // it: a name with a type argument is not looked for among the texts. DefaultSet alone would find a text named
        // DefaultSet first, and with its namespace in front, Checkout.DefaultSet, a text named Checkout.
        sb.AppendLine("""
            }

            file static class DefaultSet<TSet>
                where TSet : new()
            {
                internal static TSet Instance { get; } = new();
            }
            """);
    }

    /// <summary>
    /// The alias the file of a contract gives each namespace outside its own that holds a set it links to, a class a
    /// set inherits a text from, or the type of a parameter of a text that takes values: <c>Namespace_Shop_Dutch</c>
    /// for <c>Shop.Dutch</c>. In the order of the namespaces, so that the file is written the same way each time.
    /// </summary>
    // Named after the namespace, with a prefix that keeps it apart from the namespace itself: an alias named like
    // something the global namespace holds can't be used.
    private static SortedDictionary<string, string> GetNamespaceAliases(LocalizationContract contract)
    {
        var aliases = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var member in contract.Members)
        {
            foreach (var set in contract.Sets)
            {
                Add(GetCrefTarget(set, member.Name).Namespace);

                // The set itself is named next to its wording when it shares its language (GetWordingNote).
                if (SharesLanguage(contract, set))
                    Add(set.Namespace);
            }

            foreach (var parameter in member.Parameters)
                foreach (var part in parameter.Type)
                    if (part.Namespace is { } typeNamespace)
                        Add(typeNamespace);
        }

        return aliases;

        void Add(string @namespace)
        {
            if (!NeedsAlias(contract.Namespace, @namespace) || aliases.ContainsKey(@namespace))
                return;

            // Shop.Dutch_Sets and Shop_Dutch.Sets would share an alias.
            var alias = $"Namespace_{@namespace.Replace("@", "").Replace('.', '_')}";
            while (!names.Add(alias))
                alias += "_";

            aliases.Add(@namespace, alias);
        }
    }

    /// <summary>
    /// Whether another set linked from a contract has the language of this one, as a set rewording its own language
    /// has. The links to both then name the set.
    /// </summary>
    private static bool SharesLanguage(LocalizationContract contract, SetLink set)
    {
        var sharesLanguage = contract.Sets.Count(other => String.Equals(other.Language, set.Language, StringComparison.OrdinalIgnoreCase)) > 1;
        return sharesLanguage;
    }

    /// <summary>
    /// Whether the file of a contract reaches a namespace through an alias: every namespace but its own, where a name
    /// stands as it is, and the global namespace, which has no name to give an alias.
    /// </summary>
    private static bool NeedsAlias(string contractNamespace, string @namespace)
    {
        var needsAlias = @namespace.Length > 0 && !String.Equals(@namespace, contractNamespace, StringComparison.Ordinal);
        return needsAlias;
    }

    /// <summary>
    /// Where the wording a set gives a text is found: the set's own property or method when it declares the text, that
    /// of the nearest class declaring it when the set inherits the text (<c>ShopLocalizationEn.Checkout</c> for a
    /// British set that leaves <c>Checkout</c> as it is), and else the set itself.
    /// </summary>
    // A cref does not find a member through a class that only inherits it (CS1574), a text implemented explicitly has
    // no name to link to at all, and a method that shares its name with another has none of its own (CS0419).
    private static (string Namespace, string TypeName, bool HasMember, bool IsInherited) GetCrefTarget(SetLink set, string member)
    {
        if (set.DeclaredTexts.AsImmutableArray().Contains(member, StringComparer.Ordinal))
            return (set.Namespace, EscapeIdentifier(set.Name), HasMember: true, IsInherited: false);

        foreach (var inherited in set.InheritedTexts)
            if (String.Equals(inherited.Name, member, StringComparison.Ordinal))
                return (inherited.Namespace, inherited.TypeName, HasMember: true, IsInherited: true);

        return (set.Namespace, EscapeIdentifier(set.Name), HasMember: false, IsInherited: false);
    }

    /// <summary>
    /// The wording a set gives a text (<see cref="GetCrefTarget"/>), as the file of a contract names it: a class of the
    /// contract's own namespace by its name alone, as the file itself lies there, and a class of another namespace
    /// through the alias of that namespace.
    /// </summary>
    private static string GetCref(string @namespace, SortedDictionary<string, string> aliases, SetLink set, string member)
    {
        var target = GetCrefTarget(set, member);
        var type = GetCrefType(@namespace, aliases, target.Namespace, target.TypeName);

        var cref = target.HasMember ? $"{type}.{EscapeIdentifier(member)}" : type;
        return cref;
    }

    /// <summary>
    /// A class as the file of a contract names it (<see cref="GetCref"/>).
    /// </summary>
    private static string GetCrefType(string @namespace, SortedDictionary<string, string> aliases, string typeNamespace, string typeName)
    {
        var type = NeedsAlias(@namespace, typeNamespace) ? $"{aliases[typeNamespace]}.{typeName}" : typeName;
        return type;
    }

    /// <summary>
    /// What follows the link to the wording a set gives a text: the wording, when it is a constant, as one line of at
    /// most <see cref="MaxWordingLength"/> characters, then, in parentheses, the set when another set has the same
    /// language, as a set rewording its own language has, whether it is marked <c>[ExcludeFromLocalizationDiscovery]</c>,
    /// and the class the wording comes from when the set inherits it (<c>: Checkout (inherited from
    /// ShopLocalizationEn)</c> for a British set that leaves <c>Checkout</c> as it is).
    /// </summary>
    private static string GetWordingNote(string @namespace, SortedDictionary<string, string> aliases, SetLink set, string member, bool sharesLanguage)
    {
        string? wording = null;
        foreach (var candidate in set.Wordings)
        {
            if (String.Equals(candidate.Name, member, StringComparison.Ordinal))
                wording = candidate.Wording;
        }

        var details = new List<string>();

        if (sharesLanguage)
            details.Add($"<see cref=\"{GetCrefType(@namespace, aliases, set.Namespace, EscapeIdentifier(set.Name))}\"/>");

        if (set.IsExcluded)
            details.Add("excluded from discovery");

        var target = GetCrefTarget(set, member);
        if (target.IsInherited && !String.IsNullOrEmpty(wording))
            details.Add($"inherited from <see cref=\"{GetCrefType(@namespace, aliases, target.Namespace, target.TypeName)}\"/>");

        var note = String.IsNullOrEmpty(wording) ? "" : $": {EscapeXml(Shorten(wording!))}";
        if (details.Count > 0)
            note += $" ({String.Join(", ", details)})";

        return note;
    }

    /// <summary>
    /// A wording as it stands next to its link: its first line, cut at <see cref="MaxWordingLength"/> characters, and
    /// an ellipsis where anything was left out.
    /// </summary>
    private static string Shorten(string wording)
    {
        var lines = wording.Split(LineBreaks, StringSplitOptions.None);
        var line = lines[0];
        var isCut = lines.Length > 1;

        if (line.Length > MaxWordingLength)
        {
            // Not between the halves of a surrogate pair, which would leave a character XML can't hold.
            var length = Char.IsHighSurrogate(line[MaxWordingLength - 1]) ? MaxWordingLength - 1 : MaxWordingLength;
            line = line.Substring(0, length);
            isCut = true;
        }

        var shortened = isCut ? line + "…" : line;
        return shortened;
    }

    /// <summary>
    /// Writes the doc comment of a generated member: the property's own comment when it has one
    /// (<c>&lt;inheritdoc cref/&gt;</c>), else its default-language wording, else a plain description, followed by a link
    /// to its wording in every language, with that wording next to it (<see cref="GetWordingNote"/>).
    /// </summary>
    private static void AppendMemberDocs(StringBuilder sb, LocalizationContract contract, SortedDictionary<string, string> aliases, string className, LocalizationMember member)
    {
        if (member.HasDocComment)
        {
            sb.AppendLine($"    /// <inheritdoc cref=\"{className}.{EscapeIdentifier(member.Name)}\"/>");
        }
        else
        {
            sb.AppendLine("    /// <summary>");
            if (member.DefaultText is { } text)
            {
                // A multi-line wording stays a valid doc comment: one /// line per line of text, split at every line
                // break C# knows, since any of them would end the comment and leave the rest as code.
                foreach (var line in EscapeXml(text).Split(LineBreaks, StringSplitOptions.None))
                    sb.AppendLine($"    /// {line}");
            }
            else
            {
                sb.AppendLine($"    /// The <c>{member.Name}</c> text of <see cref=\"{className}\"/>.");
            }
            sb.AppendLine("    /// </summary>");
        }

        sb.AppendLine("""
                /// <remarks>
                /// Wording:
                /// <list type="bullet">
            """);
        foreach (var set in contract.Sets)
        {
            var cref = GetCref(contract.Namespace, aliases, set, member.Name);
            var note = GetWordingNote(contract.Namespace, aliases, set, member.Name, SharesLanguage(contract, set));

            sb.AppendLine($"    /// <item><description><see cref=\"{cref}\">{set.Language}</see>{note}</description></item>");
        }
        sb.AppendLine("""
                /// </list>
                /// </remarks>
            """);
    }

    /// <summary>
    /// Whether a text carries the name of a member every class has, such as <c>Equals</c>, which the generated class
    /// then hides. Its member says so with <see langword="new"/>, because without it the build warns (<c>CS0108</c>)
    /// about code its author can't change. A text that takes values hides such a member only when it takes what that
    /// member takes: <c>Equals(object)</c>, <c>Equals(object, object)</c>, or <c>ReferenceEquals(object, object)</c>.
    /// </summary>
    private static bool HidesObjectMember(LocalizationMember member)
    {
        if (!member.IsMethod)
            return member.Name is "Equals" or "GetHashCode" or "GetType" or "MemberwiseClone" or "ReferenceEquals" or "ToString";

        var hidesObjectMember = member.Parameters.All(static parameter => parameter.Type is [{ Namespace: null, Text: "object" or "object?" }])
               && member is { Name: "Equals", Parameters.Length: 1 or 2 } or { Name: "ReferenceEquals", Parameters.Length: 2 };

        return hidesObjectMember;
    }

    /// <summary>
    /// The text the doc comment of the generated class shows as its example: the first text that takes no values, as
    /// it stands in markup by its name alone, and else the first text, with an ellipsis for its values.
    /// </summary>
    private static string GetSampleText(LocalizationMember[] texts)
    {
        foreach (var text in texts)
            if (!text.IsMethod)
                return text.Name;

        var sample = texts.Length > 0 ? $"{texts[0].Name}(...)" : "Name";
        return sample;
    }

    /// <summary>
    /// A name for something the generated method of a text declares itself, which none of the text's parameters
    /// carries: the name asked for, with an underscore for each parameter that took it.
    /// </summary>
    private static string GetNameApartFrom(LocalizationMember member, string name)
    {
        while (member.Parameters.Any(parameter => String.Equals(parameter.Name, name, StringComparison.Ordinal)))
            name += "_";

        return name;
    }

    /// <summary>
    /// The parameters of a text that takes values, as the contract and the generated class declare them.
    /// </summary>
    private static string RenderParameters(LocalizationMember member, string @namespace, SortedDictionary<string, string> aliases)
    {
        var parameters = String.Join(", ", member.Parameters.Select(parameter
            => $"{RenderType(parameter.Type, @namespace, aliases)} {EscapeIdentifier(parameter.Name)}"));

        return parameters;
    }

    /// <summary>
    /// The type of a parameter as the file of a contract names it: like a class a cref names (<see cref="GetCrefType"/>),
    /// a type of the contract's own namespace by its name alone, and a type of another namespace through the alias of
    /// that namespace.
    /// </summary>
    private static string RenderType(EquatableArray<TypeNamePart> type, string @namespace, SortedDictionary<string, string> aliases)
    {
        var name = new StringBuilder();

        foreach (var part in type)
        {
            if (part.Namespace is { } typeNamespace && NeedsAlias(@namespace, typeNamespace))
                name.Append(aliases[typeNamespace]).Append('.');

            name.Append(part.Text);
        }

        return name.ToString();
    }

    /// <summary>
    /// Text as a doc comment can hold it: markup characters as entities, and a character XML does not allow, such as a
    /// control character or half of a surrogate pair, as the escape C# writes it with (<c>\u0001</c>). Such a character
    /// would make the comment invalid (<c>CS1570</c>), also when written as an entity.
    /// </summary>
    private static string EscapeXml(string text)
    {
        var escaped = new StringBuilder(text.Length);

        for (var i = 0; i < text.Length; i++)
        {
            var character = text[i];

            if (character is '&')
                escaped.Append("&amp;");
            else if (character is '<')
                escaped.Append("&lt;");
            else if (character is '>')
                escaped.Append("&gt;");
            else if (Char.IsHighSurrogate(character) && i + 1 < text.Length && Char.IsLowSurrogate(text[i + 1]))
                escaped.Append(character).Append(text[++i]);
            else if (character is '\t' or '\n' or '\r' or (>= ' ' and <= '\uD7FF') or (>= '\uE000' and <= '\uFFFD'))
                escaped.Append(character);
            else
                escaped.Append("\\u").Append(((int)character).ToString("X4", System.Globalization.CultureInfo.InvariantCulture));
        }

        return escaped.ToString();
    }

    /// <summary>
    /// A name as code can write it: a keyword gets an <c>@</c>, as for a text declared as <c>@event</c>.
    /// </summary>
    internal static string EscapeIdentifier(string name)
    {
        var escapeIdentifier = SyntaxFacts.GetKeywordKind(name) is SyntaxKind.None ? name : $"@{name}";
        return escapeIdentifier;
    }

    /// <summary>
    /// A namespace as code can write it, each part escaped like a name (<c>@event.Shop</c>).
    /// </summary>
    private static string EscapeNamespace(string @namespace)
    {
        var escapeNamespace = @namespace.Length is 0 ? @namespace : String.Join(".", @namespace.Split('.').Select(EscapeIdentifier));
        return escapeNamespace;
    }
}
