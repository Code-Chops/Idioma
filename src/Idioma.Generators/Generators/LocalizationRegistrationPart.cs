namespace CodeChops.Idioma.Generators;

/// <summary>
/// Emits a module initializer that adds the assembly's localization sets to <c>LocalizationRegistry</c> when the
/// assembly's code first runs, so a text finds its languages without any registration call, and without a scan of the
/// assembly by reflection. A set in the same assembly as its contract goes to <c>RegisterDefaults</c>, and a set
/// rewording something of another assembly goes to <c>Register</c>, because load order is not registration order. An
/// app's override must win over the library it rewords even when the library loads later. Within a tier a set comes
/// after the sets it derives from, so it wins over them. Before that it records what the build read about each set
/// (<c>GeneratedCode.RecordSet</c>). That's the language of each set that doesn't implement <c>LocalizationLanguageCode</c>, so that
/// the run time never reads a set's name, and the constant of each set that implements it, so that the registry needn't
/// create the set to file it. A renamed type, as an obfuscator leaves it, keeps its language. So does a set
/// named after a longer code, such as <c>ShopLocalizationEnGb</c>, whose last two letters alone would give <c>gb</c>.
/// For a set deriving from another, it's also the texts its own class words, so that the registry knows which texts
/// the set rewords without calling it. It also writes the texts of the assembly's members marked <c>[Localized]</c>
/// (<see cref="LocalizedMembersSource"/>), which the registrar records.
/// </summary>
/// <remarks>
/// An assembly that nothing uses never loads, so its module initializer never runs, while its sets already count for
/// the language checks of the build referencing it (<c>LOC700</c>). So the registrar of an assembly with sets is
/// public, and so is that of an assembly with texts on its members. An app, an assembly that declares languages, and an
/// assembly with sets of its own call the registrar of every assembly they reference first. They call it directly, not
/// through a forced module constructor, because a direct call keeps the assembly when trimming, and works under
/// NativeAOT. Each call is guarded, because a referenced assembly need not be there when the app starts (a Blazor
/// WebAssembly assembly loaded lazily, one deployed later). Such an assembly registers its sets itself when its code
/// first runs, and the app starts as it would without the call. So does an assembly referenced under an extern alias
/// only, whose registrar this code can't name. The guard looks the assembly up by name, and makes the call only when
/// it is there. Catching the call when it fails is not enough, because the WebAssembly runtime remembers that this
/// assembly could not reach the other one, and refuses every later use of it from here, also once it has loaded.
/// <para>
/// The registrar of an application also registers its languages (<c>GeneratedCode.RegisterLanguages</c>), before
/// calling any other registrar. A culture none of them serves, or a text lacks, then gets the app's default language,
/// also for the texts of the libraries it references, even a library that has the culture's language. Only the first
/// registration counts, and the app's own registrar runs first. So an application it references never decides, such
/// as the client of a Blazor Web App's server, or the app a test project tests. An application is an assembly built
/// as an executable, or an Android app, which the Android SDK builds as a library and marks with its
/// <c>AndroidApplication</c> property.
/// </para>
/// <para>
/// So an application must declare its languages, also one without texts of its own (<c>LOC103</c>). Without them, its
/// process would have no languages, and each text would fall back to the default language of its own assembly.
/// </para>
/// <para>
/// It's a part of <see cref="LocalizationGenerator"/>, which reads the sets once for this part and the contracts'.
/// </para>
/// </remarks>
public static class LocalizationRegistrationPart
{
    public const string AppWithoutLanguagesDiagnosticId = "LOC103";
    public const string CannotRegisterDiagnosticId = "LOC208";
    public const string UnmappedLanguageCodeDiagnosticId = "LOC206";
    public const string InvalidLanguageCodeDiagnosticId = "LOC207";

    /// <summary>
    /// The MSBuild property with which the Android SDK marks the project of an app (<see langword="true"/>), as the compiler's
    /// options hand it to a generator.
    /// </summary>
    internal const string AndroidApplicationProperty = "build_property.AndroidApplication";

    private const string RegistrarNamespace = PackageNames.Namespace + ".Generated";

    /// <summary>
    /// The class next to the registrar, outside its namespace, that registers the sets of the assembly itself.
    /// </summary>
    private const string OwnSetsName = "OwnLocalizationSets";

    /// <summary>
    /// The warnings about naming something marked <c>[Obsolete]</c>, without and with a message. Every generated file
    /// turns them off for all of its code, which names the package's own obsolete types and any set marked
    /// <c>[Obsolete]</c>.
    /// </summary>
    internal const string ObsoleteWarnings = "CS0612, CS0618";

    /// <summary>
    /// <see cref="ObsoleteWarnings"/>, followed by what naming the sets a file names reports besides
    /// (<see cref="DiscoveredLocalization.SuppressedDiagnostics"/>), as a pragma lists them.
    /// </summary>
    internal static string WithSuppressed(string warnings, EquatableArray<string> suppressed)
    {
        var withSuppressed = suppressed.Length is 0 ? warnings : $"{warnings}, {String.Join(", ", suppressed)}";
        return withSuppressed;
    }

    private static readonly DiagnosticDescriptor AppWithoutLanguagesRule = new(
        id: AppWithoutLanguagesDiagnosticId,
        title: "App without languages",
        messageFormat: "'{0}' is an app, but it declares no languages. Add [assembly: SupportedLanguages(\"en\", \"nl\")] with your languages, the default first.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "An app's [assembly: SupportedLanguages] decides the languages its users get. Its first language is the fallback for every text, including the texts of the libraries the app uses. So every app declares its languages, even one without texts of its own, such as a test project or the server project of a Blazor Web App. A library's attribute only describes the library's own texts.",
        helpLinkUri: PackageNames.HelpLink(AppWithoutLanguagesDiagnosticId));

    private static readonly DiagnosticDescriptor CannotRegisterRule = new(
        id: CannotRegisterDiagnosticId,
        title: "Localization set can't be registered automatically",
        messageFormat: "'{0}' can't be registered automatically because {1}, so its texts won't be found. Fix that, or mark it [ExcludeFromLocalizationDiscovery] and pass an instance to LocalizationRegistry.Register.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Sets are registered automatically, by generated code that creates an instance of each. That needs a top-level class with a parameterless constructor, and a language code. The code is the end of the class name, starting with a capital (two letters, as in ShopLocalizationNl, or a longer code that [assembly: SupportedLanguages] lists, as in ShopLocalizationEnGb), or an implementation of ILocalizationSet.LocalizationLanguageCode. Without this warning, a set that can't be registered would silently leave its texts in their default language. A nested or file-local set is reported only when it rewords another assembly's texts.",
        helpLinkUri: PackageNames.HelpLink(CannotRegisterDiagnosticId));

    private static readonly DiagnosticDescriptor UnmappedLanguageCodeRule = new(
        id: UnmappedLanguageCodeDiagnosticId,
        title: "LocalizationLanguageCode that implements nothing",
        messageFormat: "'{0}' inherits ILocalizationSet without listing it, so its LocalizationLanguageCode implements nothing and the set serves the language of its name. List the interface again: 'class {0} : {1}, ILocalizationSet'.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A class implements an interface member only if it lists the interface. A set that derives from another set inherits that set's implementation of ILocalizationSet.LocalizationLanguageCode. A LocalizationLanguageCode the set declares itself is never read, unless it overrides the inherited one, so the set registers under the language of its name.",
        helpLinkUri: PackageNames.HelpLink(UnmappedLanguageCodeDiagnosticId));

    private static readonly DiagnosticDescriptor InvalidLanguageCodeRule = new(
        id: InvalidLanguageCodeDiagnosticId,
        title: "LocalizationLanguageCode that isn't a culture name",
        messageFormat: "'{0}' gives \"{1}\" as its LocalizationLanguageCode, which isn't a culture name, so registering the set throws when the app starts. Return a culture name such as en or en-GB.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A set is registered under its language code, which is matched against the user's UI culture. A culture name is two to eight letters, optionally followed by parts of letters or digits after a hyphen each, as in en, en-GB, or zh-Hans. A code that isn't one matches no culture, so the registry refuses the set, and the generated registration, which runs when the assembly loads, would throw. Reported in every assembly, with or without [assembly: SupportedLanguages]. Also reported for a set marked [ExcludeFromLocalizationDiscovery], which throws where it's registered by hand.",
        helpLinkUri: PackageNames.HelpLink(InvalidLanguageCodeDiagnosticId));

    /// <summary>
    /// Adds this part's steps to the pipeline of <see cref="LocalizationGenerator"/>.
    /// </summary>
    internal static void Initialize(IncrementalGeneratorInitializationContext context, LocalizationInputs inputs)
    {
        var languages = inputs.Languages;
        var discovery = inputs.RecordDiscovery;

        // Every set, an excluded one included. Discovery finds only classes that implement ILocalizationSet.
        var allSets = discovery.Select(static (discovery, _) => discovery.Sets);

        // An excluded set is registered by hand, so it gets no registration and no warning about one.
        var sets = allSets.Select(static (sets, _) => new EquatableArray<DiscoveredLocalization>(sets
            .Where(static set => !set.IsExcluded)
            .ToImmutableArray()));

        // Every set whose language comes from its name, or that derives from another set, an excluded one included.
        // Registered by hand, it must serve the language the build counted it for, and reword what the build saw.
        // Not a set marked [Obsolete] as an error, which no generated code may name (LOC208).
        var records = allSets.Select(static (sets, _) => new EquatableArray<SetRecord>(sets
            .Where(static set => (set.RecordedLanguageCode is not null || set.Depth > 0) && !set.IsObsoleteAsError)
            .Select(static set => new SetRecord(set.MetadataName, set.FullyQualifiedName, set.RecordedLanguageCode, set.OwnTexts))
            .Distinct()
            .OrderBy(static set => set.MetadataName, StringComparer.Ordinal)
            .ToImmutableArray()));

        // Two outputs, so that an edit in the file of a set, which renews its location, reports the diagnostic anew
        // without rewriting the generated code: the registrations hold no location, and so compare equal.
        var registrations = sets.Select(static (sets, _) => new EquatableArray<RegisteredLocalization>(sets
            .Where(set => set.RegistrationProblem is null)
            .Select(set => new RegisteredLocalization(set.MetadataName, set.FullyQualifiedName, set.IsOverride, set.Depth))
            .Distinct()
            .OrderBy(set => set.Depth)
            .ThenBy(set => set.MetadataName, StringComparer.Ordinal)
            .ToImmutableArray()));

        // Not a default-language set: one the generated code can't create gets no contract and no texts either, which
        // LOC200 reports, and the advice of LOC208, to exclude the set and register it by hand, is no way out for it.
        var unregistrable = sets
            .Combine(languages)
            .Combine(inputs.Contracts)
            .Select(static (data, _) =>
            {
                var ((sets, languages), contracts) = data;

                // Nor a default-language set whose name gives it no language, which LOC200 reports with the name to
                // give it.
                return new EquatableArray<UnregistrableLocalization>(sets
                    .Where(set => set.RegistrationProblem is not null
                                  && (languages is not { } declared
                                      || (!LocalizationDiscovery.IsDefaultSet(set, declared.DefaultLanguage, contracts)
                                          && !(declared.DefaultLanguage is not null && LocalizationDiscovery.IsDefaultSetWithoutLanguage(set, contracts)))))
                    .Select(set => new UnregistrableLocalization(set.MetadataName, set.RegistrationProblem!, set.Location))
                    .Distinct()
                    .OrderBy(set => set.MetadataName, StringComparer.Ordinal)
                    .ToImmutableArray());
            });

        // An Android app builds as a library: the Android SDK turns its OutputType from Exe into Library, as it needs
        // no Main. The output kind alone would take it for a library, and leave its languages unregistered, so the SDK's
        // own property says which project is the app. The package makes that property visible to the compiler
        // (build/CodeChops.Idioma.props).
        var isAndroidApplication = context.AnalyzerConfigOptionsProvider.Select(static (options, _) =>
            options.GlobalOptions.TryGetValue(AndroidApplicationProperty, out var value)
            && String.Equals(value, "true", StringComparison.OrdinalIgnoreCase));

        // Runs on every change of the compilation in an IDE, so it reads no more than a type lookup per referenced
        // assembly, and ends in a plain value that keeps everything after it cached.
        var assembly = context.CompilationProvider
            .Select(static (compilation, _) => ReadAssembly(compilation))
            .Combine(isAndroidApplication)
            .Select(static (data, _) => data.Right ? data.Left with { IsApplication = true } : data.Left);

        // The texts of members marked [Localized]. Read here alone, and not by a part or a generator of their own,
        // because every attribute of that name is bound again on each edit.
        // An enum that a set holds the texts of gets its GetText from that set's generated class alone.
        var enumsWithSets = allSets.Select(static (sets, _) => new EquatableArray<string>(sets
            .SelectMany(static set => set.EnumNames)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToImmutableArray()));

        var members = LocalizedMembers.ReadModel(context, languages)
            .Combine(enumsWithSets)
            .Select(static (data, _) => LocalizedMembers.WithoutExtensionsOf(data.Left, data.Right));

        // The class that records those texts, where the assembly has one. Its name alone, so that an edit of a wording
        // rewrites no registrar.
        var memberTexts = members.Select(static (model, _) => model.HasTexts ? model.RecorderName : null);

        // What naming a set marked [Experimental], or [Obsolete] with an id of its own, reports.
        var suppressedDiagnostics = allSets.Select(static (sets, _) => new EquatableArray<string>(sets
            .SelectMany(static set => set.SuppressedDiagnostics)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToImmutableArray()));

        var registrar = registrations
            .Combine(records)
            .Combine(assembly)
            .Combine(languages)
            .Combine(memberTexts)
            .Combine(suppressedDiagnostics)
            .Select(static (data, _) =>
            {
                var (((((sets, records), assembly), languages), memberTexts), suppressedDiagnostics) = data;
                var forwards = sets.Length > 0 || assembly.IsApplication || languages is { Codes.Length: > 0 };
                // Not without a default language (LOC101): the first valid language must not become the app's default.
                var appLanguages = assembly.IsApplication && languages is { DefaultLanguage: not null } declared ? declared.Codes : default;

                return new Registrar(
                    Name: assembly.RegistrarName,
                    Sets: sets,
                    ReferencedRegistrars: forwards ? assembly.ReferencedRegistrars : default,
                    Languages: appLanguages,
                    Records: records,
                    MemberTexts: memberTexts,
                    SuppressedDiagnostics: suppressedDiagnostics);
            });

        // Once per class that declares the property, where the fix is, however many sets derive from it.
        var unmapped = sets.Select(static (sets, _) => new EquatableArray<UnmappedLanguageCode>(sets
            .Where(set => set.UnmappedLanguageCode is not null)
            .Select(set => set.UnmappedLanguageCode!.Value)
            .Distinct()
            .OrderBy(set => set.DeclaringType, StringComparer.Ordinal)
            .ToImmutableArray()));

        // An excluded set included: registered by hand, it throws just the same. Once per property, as each set that
        // inherits it carries it.
        var invalidLanguageCodes = allSets.Select(static (sets, _) => new EquatableArray<InvalidLanguageCode>(sets
            .Where(static set => set.InvalidLanguageCode is not null)
            .Select(static set => set.InvalidLanguageCode!.Value)
            .Distinct()
            .OrderBy(static code => code.DeclaringType, StringComparer.Ordinal)
            .ToImmutableArray()));

        // An application without the attribute, by its name. Not one whose attribute is invalid, which LOC101 reports.
        var appWithoutLanguages = assembly
            .Combine(languages)
            .Select(static (data, _) => data.Left.IsApplication && data.Right is null ? data.Left.AssemblyName : null);

        // Without a location, because nothing in the source says that the project builds an app.
        context.RegisterSourceOutput(appWithoutLanguages, static (context, assemblyName) =>
        {
            if (assemblyName is not null)
                context.ReportDiagnostic(Diagnostic.Create(AppWithoutLanguagesRule, Location.None, assemblyName));
        });

        context.RegisterSourceOutput(unregistrable, static (context, sets) =>
        {
            foreach (var set in sets.AsImmutableArray())
                context.ReportDiagnostic(Diagnostic.Create(CannotRegisterRule, set.Location.ToLocation(), set.MetadataName, set.Problem));
        });

        context.RegisterSourceOutput(discovery.Select(static (discovery, _) => discovery.UnnameableOverrides), static (context, sets) =>
        {
            foreach (var set in sets.AsImmutableArray())
                context.ReportDiagnostic(Diagnostic.Create(CannotRegisterRule, set.Location.ToLocation(), set.MetadataName, set.Problem));
        });

        // No code below C# 14, which LOC102 reports. What is found about the sets is reported all the same.
        var compiles = inputs.Compiles;

        context.RegisterSourceOutput(members.WhereItCompiles(compiles), static (context, model) =>
        {
            if (model.HasTexts)
                context.AddSource(LocalizedMembersSource.HintName, LocalizedMembersSource.Generate(model));
        });

        context.RegisterSourceOutput(unmapped, static (context, sets) =>
        {
            foreach (var set in sets.AsImmutableArray())
                context.ReportDiagnostic(Diagnostic.Create(UnmappedLanguageCodeRule, set.Location.ToLocation(), set.DeclaringType, set.DeclaringTypeBase));
        });

        context.RegisterSourceOutput(invalidLanguageCodes, static (context, codes) =>
        {
            foreach (var code in codes.AsImmutableArray())
                context.ReportDiagnostic(Diagnostic.Create(InvalidLanguageCodeRule, code.Location.ToLocation(), code.DeclaringType, code.Code));
        });

        context.RegisterSourceOutput(registrar.WhereItCompiles(compiles), static (context, registrar) =>
        {
            // Nothing of its own to register or record, and nothing referenced to register first.
            if (registrar.Sets.Length is 0 && registrar.ReferencedRegistrars.Length is 0 && registrar.Languages.Length is 0 && registrar.Records.Length is 0
                && registrar.MemberTexts is null)
            {
                return;
            }

            context.AddSource("LocalizationRegistrar.g.cs", GenerateSource(registrar));
        });
    }

    /// <summary>
    /// The name of an assembly's registrar: <c>LocalizationRegistrar_My_App</c> for <c>My-App</c>. Built from the
    /// assembly's name, so that an app referencing several libraries can name each one's registrar unambiguously.
    /// </summary>
    private static string GetRegistrarName(string? assemblyName)
    {
        var name = new StringBuilder("LocalizationRegistrar_");

        foreach (var c in assemblyName ?? "")
            name.Append(SyntaxFacts.IsIdentifierPartCharacter(c) ? c : '_');

        return name.ToString();
    }

    /// <summary>
    /// This assembly's registrar name, whether it builds as an executable, and the public registrars of the assemblies
    /// it references in the global namespace, in a fixed order.
    /// </summary>
    private static RegistrarAssembly ReadAssembly(Compilation compilation)
    {
        var ownName = GetRegistrarName(compilation.AssemblyName);
        var registrars = new List<ReferencedRegistrar>();

        foreach (var reference in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            if (!LocalizationDiscovery.ReferencesPackage(reference) || !IsInGlobalAlias(compilation, reference))
                continue;

            var name = GetRegistrarName(reference.Identity.Name);

            if (reference.GetTypeByMetadataName($"{RegistrarNamespace}.{name}") is { DeclaredAccessibility: Accessibility.Public })
                registrars.Add(new ReferencedRegistrar(reference.Identity.Name, name));
        }

        // A name two assemblies share (My-App and My_App), or this assembly's own, would bind ambiguously or to itself;
        // such an assembly registers its sets when it loads, as without this.
        var referenced = registrars
            .GroupBy(static registrar => registrar.RegistrarName, StringComparer.Ordinal)
            .Where(group => group.Count() is 1 && !String.Equals(group.Key, ownName, StringComparison.Ordinal))
            .Select(static group => group.Single())
            .OrderBy(static registrar => registrar.RegistrarName, StringComparer.Ordinal)
            .ToImmutableArray();

        var assembly = new RegistrarAssembly(
            compilation.AssemblyName ?? "",
            ownName,
            compilation.Options.OutputKind is OutputKind.ConsoleApplication or OutputKind.WindowsApplication or OutputKind.WindowsRuntimeApplication,
            new EquatableArray<ReferencedRegistrar>(referenced));

        return assembly;
    }

    /// <summary>
    /// Whether code can name the assembly's types without an extern alias: a reference without aliases, or one whose
    /// aliases include <c>global</c>.
    /// </summary>
    private static bool IsInGlobalAlias(Compilation compilation, IAssemblySymbol assembly)
    {
        if (compilation.GetMetadataReference(assembly) is not { } reference)
            return true;

        var aliases = reference.Properties.Aliases;

        var isInGlobalAlias = aliases.IsDefaultOrEmpty || aliases.Contains("global", StringComparer.Ordinal);
        return isInGlobalAlias;
    }

    private static string GenerateSource(Registrar registrar)
    {
        // Public only with something another assembly must register before it starts: sets of its own, or the texts of
        // its members. A member's text is asked for by its type, which an app can hold before any code of the type's
        // assembly ran. Not for the languages of an application: the application that starts registers its own first,
        // so those of one it references never count.
        var accessibility = registrar.Sets.Length > 0 || registrar.MemberTexts is not null ? "public" : "internal";
        var hasOwnSets = registrar.Sets.Length > 0 || registrar.Records.Length > 0;

        // The pragma also covers a set marked [Obsolete] or [Experimental], which registers for as long as it exists.
        // The warning about naming it is one its author can't act on in generated code, and a build that treats
        // warnings as errors would stop at it.
        var warnings = WithSuppressed(ObsoleteWarnings, registrar.SuppressedDiagnostics);
        var sb = new StringBuilder();
        sb.AppendLine($$"""
            // <auto-generated/>
            #nullable enable
            #pragma warning disable {{warnings}} // Only generated code calls GeneratedCode.

            namespace {{RegistrarNamespace}}
            {
                /// <summary>
                /// Adds the localization sets of this assembly, and of the assemblies it references, to the sets of the whole app.
            """);

        if (registrar.Languages.Length > 0)
            sb.AppendLine("    /// Registers the app's languages first.");

        if (registrar.MemberTexts is not null)
            sb.AppendLine("    /// Records the texts of this assembly's members marked <c>[Localized]</c> too.");

        sb.AppendLine($$"""
                /// </summary>
                [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
                {{accessibility}} static class {{registrar.Name}}
                {
                    private static int registered;

                    /// <summary>
                    /// Registers the sets once. That happens when this assembly's code first runs, or earlier, when an assembly that references it registers its own sets.
                    /// </summary>
                    [System.Runtime.CompilerServices.ModuleInitializer]
                    public static void Register()
                    {
                        if (System.Threading.Interlocked.Exchange(ref registered, 1) is 1)
                            return;

            """);

        // Before the referenced registrars: only the first registration of languages counts, so a referenced application
        // (the app a test project tests) must not get there first.
        if (registrar.Languages.Length > 0)
        {
            var languages = String.Join(", ", registrar.Languages.Select(static language => SymbolDisplay.FormatLiteral(language, quote: true)));
            sb.AppendLine($"            {PackageNames.GeneratedCode}.RegisterLanguages({languages});");
        }

        // Each call inside a lambda of its own: the runtime loads the referenced assembly when it compiles the method that
        // calls into it, so a call in another method is one RegisterReferenced can leave out for an assembly that is not
        // there, and one that fails inside its try when the assembly can't load after all.
        foreach (var referenced in registrar.ReferencedRegistrars)
            sb.AppendLine($"            RegisterReferenced({SymbolDisplay.FormatLiteral(referenced.AssemblyName, quote: true)}, static () => {RegistrarNamespace}.{referenced.RegistrarName}.Register());");

        // The texts of this assembly's own members. Their order among the registrations doesn't matter, because a
        // member belongs to one assembly.
        if (registrar.MemberTexts is { } memberTexts)
            sb.AppendLine($"            {memberTexts}.Register();");

        // Last, after the sets of the assemblies it references: a set of this assembly rewording one of theirs wins.
        if (hasOwnSets)
            sb.AppendLine($"            {OwnSetsName}.Register();");

        sb.AppendLine("        }");

        if (registrar.ReferencedRegistrars.Length > 0)
        {
            sb.AppendLine("""

                        /// <summary>
                        /// Calls the registrar of a referenced assembly, unless that assembly isn't there yet. An assembly the app loads
                        /// later registers its own sets when its code first runs.
                        /// </summary>
                        private static void RegisterReferenced(string assemblyName, System.Action register)
                        {
                            try
                            {
                                // Looked up by name before the call. A call into an assembly that is not there fails, and
                                // the WebAssembly runtime then refuses every later use of that assembly from this one, also
                                // once it has loaded. A lookup that fails leaves nothing behind.
                                System.Reflection.Assembly.Load(new System.Reflection.AssemblyName { Name = assemblyName });
                                register();
                            }
                            catch (System.IO.FileNotFoundException)
                            {
                            }
                            catch (System.IO.FileLoadException)
                            {
                            }
                        }
                """);
        }

        sb.AppendLine("""
                }
            }
            """);

        // The sets of the assembly itself in a class outside the registrar's namespace, at the top level of the file.
        // A name in there is looked for in the namespaces around it first, CodeChops.Idioma.Generated, CodeChops.Idioma,
        // and CodeChops, so the set Idioma.Demo.ShopLocalizationEn of a consumer would be looked for in CodeChops.Idioma.
        // At the top level it is looked for from the global namespace on, where its full name is right. File-local, so
        // it stands in nobody's way.
        if (hasOwnSets)
        {
            sb.AppendLine($$"""

                /// <summary>
                /// The localization sets of this assembly itself. This class is outside the namespace above, so a set's name is
                /// resolved from the global namespace, not from that namespace first.
                /// </summary>
                file static class {{OwnSetsName}}
                {
                    internal static void Register()
                    {
                """);

            // Before the sets register: the registry files a set under the language code it reads from it.
            foreach (var recorded in registrar.Records)
            {
                var arguments = new StringBuilder(recorded.Code is null ? "null" : SymbolDisplay.FormatLiteral(recorded.Code, quote: true));

                foreach (var text in recorded.OwnTexts)
                    arguments.Append(", ").Append(SymbolDisplay.FormatLiteral(text, quote: true));

                sb.AppendLine($"        {PackageNames.GeneratedCode}.RecordSet<{recorded.FullyQualifiedName}>({arguments});");
            }

            AppendRegistration(sb, asDefaults: true, registrar.Sets.AsImmutableArray().Where(static set => !set.IsOverride));
            AppendRegistration(sb, asDefaults: false, registrar.Sets.AsImmutableArray().Where(static set => set.IsOverride));

            sb.AppendLine("""
                    }
                }
                """);
        }

        sb.AppendLine($"#pragma warning restore {warnings}");
        sb.AppendLine("#nullable restore");

        return sb.ToString();
    }

    /// <summary>
    /// Registers sets by their types, with what creates each. The registry creates them when the first text is shown,
    /// and not here, in the module initializer, where a constructor that throws would take the app down before Main.
    /// </summary>
    private static void AppendRegistration(StringBuilder sb, bool asDefaults, IEnumerable<RegisteredLocalization> sets)
    {
        var registrations = sets.Select(
            static set => $"(typeof({set.FullyQualifiedName}), static () => new {set.FullyQualifiedName}())").ToArray();

        if (registrations.Length is 0)
            return;

        sb.AppendLine($"        {PackageNames.GeneratedCode}.RegisterSets(asDefaults: {(asDefaults ? "true" : "false")},");

        for (var i = 0; i < registrations.Length; i++)
            sb.AppendLine($"            {registrations[i]}{(i < registrations.Length - 1 ? "," : ");")}");
    }
}

/// <summary>
/// What the registrar of an assembly needs to know about it: the assembly's name, the registrar's name, whether the
/// assembly is an application, and the public registrars of the assemblies it references.
/// </summary>
internal readonly record struct RegistrarAssembly(
    string AssemblyName,
    string RegistrarName,
    bool IsApplication,
    EquatableArray<ReferencedRegistrar> ReferencedRegistrars);

/// <summary>
/// The public registrar of a referenced assembly: the name of the assembly, by which the generated code looks it up
/// before it calls the registrar, and the name of the registrar.
/// </summary>
internal readonly record struct ReferencedRegistrar(
    string AssemblyName,
    string RegistrarName);

/// <summary>
/// A class declaring a <c>LocalizationLanguageCode</c> that implements nothing, with the class it derives from and where to report
/// it: what <c>LOC206</c> reports.
/// </summary>
internal readonly record struct UnmappedLanguageCode(
    string DeclaringType,
    string DeclaringTypeBase,
    DiagnosticLocation Location);

/// <summary>
/// The registrar to write: its name, the sets it registers, the referenced registrars it calls first, the languages of
/// an application that declares them, the default first, what it records about the sets, and the class that records
/// the texts of members marked <c>[Localized]</c>, or <see langword="null"/> for an assembly without such a text.
/// </summary>
internal readonly record struct Registrar(
    string Name,
    EquatableArray<RegisteredLocalization> Sets,
    EquatableArray<ReferencedRegistrar> ReferencedRegistrars,
    EquatableArray<string> Languages,
    EquatableArray<SetRecord> Records,
    string? MemberTexts,
    EquatableArray<string> SuppressedDiagnostics);

/// <summary>
/// What the registrar records about a set before it registers: the language that comes from its name, or from the name
/// of a set it derives from (<c>en-GB</c> for <c>ShopLocalizationEnGb</c>), else <see langword="null"/>; and, for a
/// set deriving from another, the texts its own class words. The metadata name orders the records.
/// </summary>
internal readonly record struct SetRecord(
    string MetadataName,
    string FullyQualifiedName,
    string? Code,
    EquatableArray<string> OwnTexts);
