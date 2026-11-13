namespace CodeChops.Idioma.Generators;

/// <summary>
/// Holds the language sets of an assembly to the languages of <c>[assembly: SupportedLanguages(...)]</c>, and writes the
/// contract of each default-language set. For a top-level partial class listing <c>ITranslations</c> and named after the
/// default language (<c>ShopTranslationsEN</c>) it emits the contract interface (<c>IShopTranslations</c>, one
/// <see cref="string"/> member per public string property, deriving from <c>ITranslations</c>), a partial declaration
/// that makes the class implement it, and the static partial class named after the set without its language suffix
/// (<c>ShopTranslations</c>, one <c>ResourceText</c> per member keyed <c>Shop.&lt;Name&gt;</c> and worded by the set,
/// next to the constant <c>ResourceKeyPrefix</c>); the interface and the class take the set's accessibility. Each
/// generated member inherits the property's doc comment through <c>&lt;inheritdoc cref/&gt;</c>, or else is documented
/// with its default-language wording, and links to its wording in every language. Each member of the contract whose
/// default wording is a constant records its placeholders (<c>[FormatPlaceholders]</c>), which
/// <see cref="FormatPlaceholdersAnalyzer"/> holds every other set to, in this assembly and in those referencing it.
/// </summary>
/// <remarks>
/// It also records every set of a public contract in an assembly attribute, so that the build of an assembly referencing
/// this one can tell which languages exist without searching its types, and reports the languages referenced libraries
/// leave open. The generator sees the compilation before its own output is added, so the interfaces of a set are exactly
/// the handwritten ones.
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class TranslationSetsGenerator : IIncrementalGenerator
{
    public const string NoLanguagesDeclaredDiagnosticId = "LOC001";
    public const string CannotExtendDiagnosticId = "LOC002";
    public const string MissingLanguageDiagnosticId = "LOC004";
    public const string UnlistedLanguageDiagnosticId = "LOC005";
    public const string DerivedFromOtherLanguageDiagnosticId = "LOC006";
    public const string MissingContractDiagnosticId = "LOC007";
    public const string LibraryMissingLanguageDiagnosticId = "LOC008";
    public const string LibraryCannotBeTranslatedDiagnosticId = "LOC009";
    public const string ReservedTextNameDiagnosticId = "LOC014";

    /// <summary>
    /// The property of a diagnostic that holds the name that resolves it, for its code fix to apply: the name the class
    /// should carry (<c>LOC002</c>, when a rename resolves it), or the contract it should implement instead of
    /// <c>ITranslations</c> (<c>LOC007</c>, when this build generates that contract).
    /// </summary>
    public const string SuggestedNameProperty = "SuggestedName";

    private const string PackageNamespace = "CodeChops.Idioma";
    private const string InternalsNamespace = PackageNamespace + ".Internals";
    private const string TranslationSetAttributeFullName = "CodeChops.Idioma.Internals.TranslationSetAttribute";
    private const string InlineTextsAttributeFullName = "CodeChops.Idioma.Internals.InlineTextsAttribute";

    /// <summary>
    /// The line terminators of C#, the pair first so that it counts as one break.
    /// </summary>
    private static readonly string[] LineBreaks = ["\r\n", "\r", "\n", "\u0085", "\u2028", "\u2029"];

    private static readonly DiagnosticDescriptor NoLanguagesDeclaredRule = Error(
        NoLanguagesDeclaredDiagnosticId,
        "Translations set in an assembly without languages",
        "'{0}' is a translations set, and this assembly declares no languages. Add [assembly: SupportedLanguages(\"en\", \"nl\")] with your languages, the default first.",
        "The generator writes a contract from the set of the default language, and holds the other sets to it, so it has to know the languages. A set rewording a library's set, or adding a language to its contract, does not list ITranslations itself and needs no declaration.");

    private static readonly DiagnosticDescriptor CannotExtendRule = Error(
        CannotExtendDiagnosticId,
        "Default-language set cannot be extended by the generator",
        "'{0}' is the default-language set of a translations contract, but {1}, so no contract or texts are generated for it. Declare it as a top-level partial class named with its language as suffix (ShopTranslationsEN), with a parameterless constructor, neither generic, abstract, nor file-local.",
        "The generator adds a partial declaration to the set, making it implement the generated contract, names the generated class after the set without its language suffix, and creates the set for the default wording of every text, so the set must be one generated code in another file can name and create.");

    private static readonly DiagnosticDescriptor MissingLanguageRule = Error(
        MissingLanguageDiagnosticId,
        "Translations contract misses a language",
        "'{0}' has no translation into '{1}', which [assembly: SupportedLanguages] requires. Add {2}.",
        "Every language of [assembly: SupportedLanguages] has a set of its own in every contract, as every inline text takes its wording. That goes for a region of a listed language too, such as en-GB next to en: its set derives from the set of its language, and holds only the texts that differ.");

    private static readonly DiagnosticDescriptor UnlistedLanguageRule = Error(
        UnlistedLanguageDiagnosticId,
        "Translations set for an unlisted language",
        "'{0}' translates into '{1}', which [assembly: SupportedLanguages] does not list. {2}",
        "The attribute is the one list of languages this assembly supports, and only sets of those languages are checked for completeness. A set marked [ExcludeFromTranslationDiscovery] is held to it too: an application shows a culture outside its list in its default language, so registering such a set by hand would not make it show.");

    private static readonly DiagnosticDescriptor DerivedFromOtherLanguageRule = Error(
        DerivedFromOtherLanguageDiagnosticId,
        "Translations set derives from a set of another language",
        "'{0}' translates into '{1}' but derives from '{2}', so every text it does not override stays in '{3}'. To reword '{2}', give the set its language (the end of its name, unless it implements LanguageCode); to translate, implement the contract instead, which demands every text.",
        "Deriving from a set is the way to reword some of its texts in the same language. In another language it compiles with texts left untranslated. Reported in every assembly, with or without [assembly: SupportedLanguages].");

    private static readonly DiagnosticDescriptor MissingContractRule = Error(
        MissingContractDiagnosticId,
        "Translations set does not implement its contract",
        "'{0}' translates into '{1}' but implements ITranslations, and no text reads a set outside its contract. Implement '{2}', generated from '{3}'.",
        "Only the set of the default language lists ITranslations: the generator writes the contract from it. Every other language implements that contract, which is what demands every text.");

    private static readonly DiagnosticDescriptor LibraryMissingLanguageRule = Error(
        LibraryMissingLanguageDiagnosticId,
        "Referenced translations contract misses a language",
        "Nothing translates '{0}' into '{1}', which this assembly supports. Add {2}.",
        "An assembly supporting a language must show the texts of the libraries it references in that language too. A set added here registers itself, and serves the library's texts.");

    private static readonly DiagnosticDescriptor LibraryCannotBeTranslatedRule = new(
        id: LibraryCannotBeTranslatedDiagnosticId,
        title: "Referenced library's inline texts cannot be translated",
        messageFormat: "'{0}' writes texts inline that do not exist in '{1}', which this assembly supports, and no set added here can translate them. Its users will see those texts in its default language.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "An inline text cannot be extended from outside the library that writes it. A library that wants to be translated declares its texts in sets per language.");

    private static readonly DiagnosticDescriptor ReservedTextNameRule = Error(
        ReservedTextNameDiagnosticId,
        "Text named like a member of the generated class",
        "'{0}' has a text named '{1}', which the generated class '{2}' cannot hold, so '{2}.{1}' is not generated. Rename the text.",
        "The generated texts class declares the constant ResourceKeyPrefix, and a member cannot carry the name of its own class.");

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var languages = SupportedLanguages.Read(context);

        var analysis = TranslationsDiscovery.Read(context)
            .Combine(languages)
            .Select(static (data, _) => TranslationSetsAnalysis.Analyze(data.Left.Sets, data.Left.UnextendableSets, data.Right));

        // Separate outputs, so that an edit in the file of a set, which renews its location, reports its diagnostics
        // anew without rewriting the generated code: the contracts and the recorded sets hold no location.
        context.RegisterSourceOutput(analysis.Select(static (analysis, _) => analysis.Findings), Report);

        context.RegisterSourceOutput(analysis.SelectMany(static (analysis, _) => analysis.Contracts.AsImmutableArray()), static (context, contract) =>
        {
            // Qualified by namespace: two sets sharing a simple name must not collide on the hint name. Without the @ of
            // an escaped keyword (namespace Shop.@event), which a hint name cannot hold.
            var hintName = (contract.Namespace.Length > 0 ? $"{contract.Namespace}.{contract.ClassName}" : contract.ClassName).Replace("@", "");
            context.AddSource($"{hintName}.Translations.g.cs", GenerateContract(contract));
        });

        var providedSets = analysis.Select(static (analysis, _) => analysis.ProvidedSets);

        context.RegisterSourceOutput(providedSets, static (context, sets) =>
        {
            if (sets.Length > 0)
                context.AddSource("TranslationSets.g.cs", GenerateProvidedSets(sets));
        });

        // Runs on every change of the compilation in an IDE, so it reads no more than a few assembly attributes, and
        // ends in a plain value that keeps everything after it cached.
        var references = context.CompilationProvider.Select(static (compilation, _) => ReadReferences(compilation));

        var gaps = languages
            .Combine(providedSets)
            .Combine(references)
            .Select(static (data, _) => TranslationSetsAnalysis.FindLibraryGaps(data.Left.Left, data.Left.Right, data.Right));

        context.RegisterSourceOutput(gaps, Report);
    }

    private static DiagnosticDescriptor Error(string id, string title, string message, string description)
    {
        return new DiagnosticDescriptor(id, title, message, "Design", DiagnosticSeverity.Error, isEnabledByDefault: true, description);
    }

    private static void Report(SourceProductionContext context, EquatableArray<TranslationSetsFinding> findings)
    {
        foreach (var finding in findings)
        {
            var rule = finding.Rule switch
            {
                TranslationSetsRule.NoLanguagesDeclared => NoLanguagesDeclaredRule,
                TranslationSetsRule.CannotExtend => CannotExtendRule,
                TranslationSetsRule.MissingLanguage => MissingLanguageRule,
                TranslationSetsRule.UnlistedLanguage => UnlistedLanguageRule,
                TranslationSetsRule.DerivedFromOtherLanguage => DerivedFromOtherLanguageRule,
                TranslationSetsRule.MissingContract => MissingContractRule,
                TranslationSetsRule.LibraryMissingLanguage => LibraryMissingLanguageRule,
                TranslationSetsRule.LibraryCannotBeTranslated => LibraryCannotBeTranslatedRule,
                _ => ReservedTextNameRule,
            };

            var properties = finding.SuggestedName is { } suggestedName
                ? ImmutableDictionary<string, string?>.Empty.Add(SuggestedNameProperty, suggestedName)
                : null;

            context.ReportDiagnostic(Diagnostic.Create(rule, finding.Location.ToLocation(), properties, finding.Arguments.Cast<object>().ToArray()));
        }
    }

    /// <summary>
    /// What the referenced assemblies say about their languages, in a fixed order. Only assemblies that reference this
    /// package can hold its attributes, which keeps the framework's assemblies out of this.
    /// </summary>
    private static EquatableArray<ReferencedTranslations> ReadReferences(Compilation compilation)
    {
        var references = ImmutableArray.CreateBuilder<ReferencedTranslations>();

        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            if (!TranslationsDiscovery.ReferencesPackage(assembly))
                continue;

            var sets = ImmutableArray.CreateBuilder<ProvidedTranslationSet>();
            var hasInlineTexts = false;

            foreach (var attribute in assembly.GetAttributes())
            {
                var name = attribute.AttributeClass?.ToDisplayString();

                if (String.Equals(name, TranslationSetAttributeFullName, StringComparison.Ordinal)
                    && attribute.ConstructorArguments.Length is 2
                    && attribute.ConstructorArguments[0].Value is string contract
                    && attribute.ConstructorArguments[1].Value is string language)
                {
                    sets.Add(new ProvidedTranslationSet(contract, language));
                }
                else if (String.Equals(name, InlineTextsAttributeFullName, StringComparison.Ordinal))
                {
                    hasInlineTexts = true;
                }
            }

            var languages = SupportedLanguages.ReadCodes(assembly);

            if (sets.Count > 0 || languages.Length > 0)
            {
                references.Add(new ReferencedTranslations(
                    assembly.Identity.Name,
                    new EquatableArray<string>(languages),
                    new EquatableArray<ProvidedTranslationSet>(sets.ToImmutable()),
                    hasInlineTexts));
            }
        }

        return new EquatableArray<ReferencedTranslations>(references.OrderBy(static reference => reference.AssemblyName, StringComparer.Ordinal).ToImmutableArray());
    }

    private static string GenerateProvidedSets(EquatableArray<ProvidedTranslationSet> sets)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");

        foreach (var set in sets)
            sb.AppendLine($"[assembly: {InternalsNamespace}.TranslationSetAttribute({SymbolDisplay.FormatLiteral(set.Contract, quote: true)}, {SymbolDisplay.FormatLiteral(set.Language, quote: true)})]");

        return sb.ToString();
    }

    private static string GenerateContract(TranslationContract contract)
    {
        // The set by its simple name: this file lies in the set's namespace. Its full name would start with a name the
        // namespace may hold something else under, as a namespace Shop that holds a class Shop does.
        var className = EscapeIdentifier(contract.ClassName);
        var name = contract.ContractName;
        var textsName = EscapeIdentifier(contract.TextsName);
        var @namespace = EscapeNamespace(contract.Namespace);
        var keyPrefixLiteral = SymbolDisplay.FormatLiteral(contract.KeyPrefix, quote: true);
        var texts = contract.Members.Where(member => !TranslationSetsAnalysis.IsReservedTextName(member.Name, contract.TextsName)).ToArray();
        var typeKeyword = contract.IsRecord ? "record" : "class";
        var accessibility = contract.IsPublic ? "public" : "internal";
        var sampleMember = texts.Length > 0 ? texts[0].Name : "Name";

        // What creates a text is imported here, at the top of the file, and called by its name alone. Inside the
        // generated class a text stands before every namespace of its name, so behind a text named CodeChops the
        // package's namespace could not be reached to call it.
        var sb = new StringBuilder();
        sb.AppendLine($$"""
            // <auto-generated/>
            #nullable enable

            using static {{InternalsNamespace}}.GeneratedCode;

            """);

        if (@namespace.Length > 0)
        {
            sb.AppendLine($$"""
                namespace {{@namespace}};

                """);
        }

        // The contract.
        sb.AppendLine($$"""
            /// <summary>
            /// The translations contract declared by <see cref="{{className}}"/>: one member per text, for one language.
            /// Every language in <c>[assembly: SupportedLanguages]</c> implements it, named with its language as suffix, and the build
            /// reports a language without a set. Because every member must be implemented, a text without a translation is a
            /// compile error rather than a fallback at run time. Each set registers itself, so there is nothing to register.
            /// </summary>
            {{accessibility}} interface {{name}} : {{PackageNamespace}}.ITranslations
            {
            """);
        foreach (var member in contract.Members)
        {
            AppendMemberDocs(sb, contract, className, member);

            // Not for wording that is no constant, or no valid format string: nothing then to hold the others to.
            if (member.DefaultText is { } defaultText && FormatPlaceholders.TryRead(defaultText, out var placeholders))
                sb.AppendLine($"    [{InternalsNamespace}.FormatPlaceholders({String.Join(", ", placeholders)})]");

            sb.AppendLine($"    string {EscapeIdentifier(member.Name)} {{ get; }}");
        }

        // The end of the contract, and the default-language set implementing it (the other languages implement it by
        // hand). Then the texts, partial so that a helper can sit next to them.
        sb.AppendLine($$"""
            }

            partial {{typeKeyword}} {{className}} : {{name}};

            /// <summary>
            /// The texts declared by <see cref="{{className}}"/>, one <see cref="{{PackageNamespace}}.ResourceText"/> per text,
            /// each following the current UI culture: <c>@{{contract.TextsName}}.{{sampleMember}}</c> in markup shows the <see cref="{{name}}"/> set
            /// registered for <see cref="System.Globalization.CultureInfo.CurrentUICulture"/>, else the set registered for
            /// the app's <see cref="{{PackageNamespace}}.TranslationsRegistry.DefaultLanguage"/>, else the set registered for the
            /// language of <see cref="{{className}}"/>, else its wording.
            /// </summary>
            {{accessibility}} static partial class {{textsName}}
            {
                /// <summary>
                /// The prefix of every text's resource key: <c>{{EscapeXml(contract.KeyPrefix)}}</c>.
                /// </summary>
                public const string {{TranslationSetsAnalysis.ResourceKeyPrefixName}} = {{keyPrefixLiteral}};
            """);

        foreach (var member in texts)
        {
            var memberName = EscapeIdentifier(member.Name);

            sb.AppendLine();
            AppendMemberDocs(sb, contract, className, member);
            sb.AppendLine($"    public static {PackageNamespace}.ResourceText {memberName} {{ get; }} = CreateResourceText<{name}>({TranslationSetsAnalysis.ResourceKeyPrefixName} + nameof({memberName}), static translations => translations.{memberName}, DefaultSet<{className}>.Instance);");
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
            #nullable restore
            """);

        return sb.ToString();
    }

    /// <summary>
    /// A set as the file of a contract names it: by its simple name when it lies in the contract's namespace, as the
    /// file itself does, else by its full name.
    /// </summary>
    private static string GetNameIn(string @namespace, TranslationLink set)
    {
        return String.Equals(set.Namespace, @namespace, StringComparison.Ordinal) ? EscapeIdentifier(set.Name) : set.FullyQualifiedName;
    }

    /// <summary>
    /// Writes the doc comment of a generated member: the property's own comment when it has one
    /// (<c>&lt;inheritdoc cref/&gt;</c>), else its default-language wording, else a plain description, followed by a link
    /// to its wording in every language.
    /// </summary>
    private static void AppendMemberDocs(StringBuilder sb, TranslationContract contract, string className, TranslationsMember member)
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
                /// Translations:
                /// <list type="bullet">
            """);
        foreach (var translation in contract.Translations)
        {
            var set = GetNameIn(contract.Namespace, translation);

            // A set implementing the member explicitly has no such name to link to, so that link goes to the set.
            var cref = translation.StringProperties.AsImmutableArray().Contains(member.Name, StringComparer.Ordinal)
                ? $"{set}.{EscapeIdentifier(member.Name)}"
                : set;

            sb.AppendLine($"    /// <item><description><see cref=\"{cref}\">{translation.Language}</see></description></item>");
        }
        sb.AppendLine("""
                /// </list>
                /// </remarks>
            """);
    }

    private static string EscapeXml(string text)
    {
        return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }

    /// <summary>
    /// A name as code can write it: a keyword gets an <c>@</c>, as for a text declared as <c>@event</c>.
    /// </summary>
    private static string EscapeIdentifier(string name)
    {
        return SyntaxFacts.GetKeywordKind(name) is SyntaxKind.None ? name : $"@{name}";
    }

    /// <summary>
    /// A namespace as code can write it, each part escaped like a name (<c>@event.Shop</c>).
    /// </summary>
    private static string EscapeNamespace(string @namespace)
    {
        return @namespace.Length is 0 ? @namespace : String.Join(".", @namespace.Split('.').Select(EscapeIdentifier));
    }
}
