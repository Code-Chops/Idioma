namespace CodeChops.Idioma.Generators;

/// <summary>
/// Emits a module initializer that adds the assembly's translation sets to <c>TranslationsRegistry</c> when the
/// assembly's code first runs, so a text finds its languages without any registration call, and without a scan of the
/// assembly by reflection. A set in the same assembly as its contract goes to <c>RegisterDefaults</c>, and a set
/// rewording something of another assembly goes to <c>Register</c>, because load order is not registration order: an
/// app's override must win over the library it rewords even when the library loads later. Within a tier a set comes
/// after the sets it derives from, so it wins over them. Before that it records the language code of each set named
/// after a declared code that is not two letters (<c>GeneratedCode.RegisterLanguageCode</c>): at run time a
/// set's name gives only its last two letters, and <c>ShopTranslationsEnGB</c> would serve <c>gb</c>.
/// </summary>
/// <remarks>
/// An assembly that nothing uses never loads, so its module initializer never runs, while its sets already count for
/// the language checks of the build referencing it (<c>LOC008</c>). So the registrar of an assembly with sets is
/// public, and so is that of an application declaring languages, which a host referencing it (a Blazor Web App's server
/// references its client) must register before it renders anything. An app, an assembly that declares languages, and an
/// assembly with sets of its own call the registrar of every assembly they reference first. A direct call, not a forced
/// module constructor: it keeps the assembly when trimming, and works under NativeAOT. Each call is guarded, because a
/// referenced assembly need not be there when the app starts (a Blazor WebAssembly assembly loaded lazily, one deployed
/// later): such an assembly registers its sets itself when its code first runs, and the app starts as it would without
/// the call. So does an assembly referenced under an extern alias only, whose registrar this code cannot name. The
/// guard looks the assembly up by name, and makes the call only when it is there. Catching the call when it fails is
/// not enough: the WebAssembly runtime remembers that this assembly could not reach the other one, and refuses every
/// later use of it from here, also once it has loaded.
/// <para>
/// The registrar of an application that declares languages also registers them
/// (<c>GeneratedCode.RegisterLanguages</c>), before calling any other registrar: a culture none of them serves,
/// or a text lacks, then gets the app's default language, also for the texts of the libraries it references, even a
/// library that has the culture's language. Only the first registration counts, and the app's own registrar runs
/// first. An application is an assembly built as an executable, or an Android app, which the Android SDK builds as a
/// library and marks with its <c>AndroidApplication</c> property.
/// </para>
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class TranslationsRegistrationGenerator : IIncrementalGenerator
{
    public const string CannotRegisterDiagnosticId = "LOC003";
    public const string UnmappedLanguageCodeDiagnosticId = "LOC015";

    /// <summary>
    /// The MSBuild property with which the Android SDK marks the project of an app (<see langword="true"/>), as the compiler's
    /// options hand it to a generator.
    /// </summary>
    internal const string AndroidApplicationProperty = "build_property.AndroidApplication";

    private const string RegistrarNamespace = "CodeChops.Idioma.Generated";
    private const string RegistryName = "CodeChops.Idioma.TranslationsRegistry";
    private const string GeneratedCodeName = "CodeChops.Idioma.Internals.GeneratedCode";

    /// <summary>
    /// The class next to the registrar, outside its namespace, that registers the sets of the assembly itself.
    /// </summary>
    private const string OwnSetsName = "OwnTranslationSets";

    private static readonly DiagnosticDescriptor CannotRegisterRule = new(
        id: CannotRegisterDiagnosticId,
        title: "Translations set cannot register itself",
        messageFormat: "'{0}' cannot register itself because {1}, so its texts would not be found. Fix that, or mark it [ExcludeFromTranslationDiscovery] and pass an instance to TranslationsRegistry.Register.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A translations set registers itself through generated code that creates an instance. That takes a top-level class with a parameterless constructor, and a language code: the end of the type's name (two letters, or a longer code [assembly: SupportedLanguages] lists, as in ShopTranslationsEnGB), or an implementation of ITranslations.LanguageCode. A set that cannot register itself would otherwise leave its texts in their default language without a word of warning. A nested or file-local set is reported only when it rewords another assembly's texts.");

    private static readonly DiagnosticDescriptor UnmappedLanguageCodeRule = new(
        id: UnmappedLanguageCodeDiagnosticId,
        title: "LanguageCode that implements nothing",
        messageFormat: "The LanguageCode of '{0}' implements nothing, because '{0}' inherits ITranslations without listing it, so the set serves the language of its name instead. List the interface again: 'class {0} : {1}, ITranslations'.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A class implements an interface member only where it lists the interface. A set deriving from another set inherits that set's mapping of ITranslations.LanguageCode, so a LanguageCode it declares, unless it overrides the inherited one, is never read, and the set registers under the language its name gives it.");

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var languages = SupportedLanguages.Read(context);
        var discovery = TranslationsDiscovery.Read(context);

        // A contract the generator writes in this compilation does not exist here (generators cannot see each other's
        // output), so a set implementing one binds it to an error type, and is recognized by name and namespace instead.
        var candidates = discovery
            .Combine(languages)
            .Select(static (data, _) =>
            {
                var (discovery, languages) = data;
                var contracts = TranslationsDiscovery.GetGeneratedContractNames(discovery.Sets, languages);

                return new EquatableArray<DiscoveredTranslations>(
                    discovery.Sets.Where(set => TranslationsDiscovery.IsSet(set, contracts)).ToImmutableArray());
            });

        // An excluded set is registered by hand, so it gets no registration and no warning about one.
        var sets = candidates.Select(static (sets, _) => new EquatableArray<DiscoveredTranslations>(sets
            .Where(static set => !set.IsExcluded)
            .ToImmutableArray()));

        // Every set its name gives a code the last two letters cannot hold, an excluded one included: registered by hand,
        // it must serve the language the build counted it for.
        var languageCodes = candidates.Select(static (sets, _) => new EquatableArray<RecordedLanguageCode>(sets
            .Where(static set => set.RecordedLanguageCode is not null)
            .Select(static set => new RecordedLanguageCode(set.MetadataName, set.FullyQualifiedName, set.RecordedLanguageCode!))
            .Distinct()
            .OrderBy(static set => set.MetadataName, StringComparer.Ordinal)
            .ToImmutableArray()));

        // Two outputs, so that an edit in the file of a set, which renews its location, reports the diagnostic anew
        // without rewriting the generated code: the registrations hold no location, and so compare equal.
        var registrations = sets.Select(static (sets, _) => new EquatableArray<RegisteredTranslations>(sets
            .Where(set => set.RegistrationProblem is null)
            .Select(set => new RegisteredTranslations(set.MetadataName, set.FullyQualifiedName, set.IsOverride, set.Depth))
            .Distinct()
            .OrderBy(set => set.Depth)
            .ThenBy(set => set.MetadataName, StringComparer.Ordinal)
            .ToImmutableArray()));

        // Not a default-language set: one the generated code cannot create gets no contract and no texts either, which
        // LOC002 reports, and the advice of LOC003, to exclude the set and register it by hand, is no way out for it.
        var unregistrable = sets
            .Combine(languages)
            .Select(static (data, _) =>
            {
                var (sets, languages) = data;
                var defaultLanguage = languages is { Codes.Length: > 0 } declared ? declared.Codes[0] : null;

                return new EquatableArray<UnregistrableTranslations>(sets
                    .Where(set => set.RegistrationProblem is not null
                                  && (defaultLanguage is null || !TranslationsDiscovery.IsDefaultSet(set, defaultLanguage)))
                    .Select(set => new UnregistrableTranslations(set.MetadataName, set.RegistrationProblem!, set.Location))
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

        var registrar = registrations
            .Combine(languageCodes)
            .Combine(assembly)
            .Combine(languages)
            .Select(static (data, _) =>
            {
                var (((sets, languageCodes), assembly), languages) = data;
                var forwards = sets.Length > 0 || assembly.IsApplication || languages is { Codes.Length: > 0 };
                var appLanguages = assembly.IsApplication && languages is { Codes.Length: > 0 } declared ? declared.Codes : default;

                return new Registrar(
                    Name: assembly.RegistrarName,
                    Sets: sets,
                    ReferencedRegistrars: forwards ? assembly.ReferencedRegistrars : default,
                    Languages: appLanguages,
                    LanguageCodes: languageCodes);
            });

        var unmapped = sets.Select(static (sets, _) => new EquatableArray<UnmappedLanguageCode>(sets
            .Where(set => set.UnmappedLanguageCodeType is not null)
            .Select(set => new UnmappedLanguageCode(set.Name, set.UnmappedLanguageCodeType!, set.UnmappedLanguageCodeBaseType ?? "object", set.Location))
            .Distinct()
            .OrderBy(set => set.SetName, StringComparer.Ordinal)
            .ToImmutableArray()));

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

        context.RegisterSourceOutput(unmapped, static (context, sets) =>
        {
            foreach (var set in sets.AsImmutableArray())
                context.ReportDiagnostic(Diagnostic.Create(UnmappedLanguageCodeRule, set.Location.ToLocation(), set.DeclaringType, set.DeclaringTypeBase));
        });

        context.RegisterSourceOutput(registrar, static (context, registrar) =>
        {
            // Nothing of its own to register or record, and nothing referenced to register first.
            if (registrar.Sets.Length is 0 && registrar.ReferencedRegistrars.Length is 0 && registrar.Languages.Length is 0 && registrar.LanguageCodes.Length is 0)
                return;

            context.AddSource("TranslationsRegistrar.g.cs", GenerateSource(registrar));
        });
    }

    /// <summary>
    /// The name of an assembly's registrar: <c>TranslationsRegistrar_My_App</c> for <c>My-App</c>. Built from the
    /// assembly's name, so that an app referencing several libraries can name each one's registrar unambiguously.
    /// </summary>
    internal static string GetRegistrarName(string? assemblyName)
    {
        var name = new StringBuilder("TranslationsRegistrar_");

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
            if (!TranslationsDiscovery.ReferencesPackage(reference) || !IsInGlobalAlias(compilation, reference))
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

        return new RegistrarAssembly(
            ownName,
            compilation.Options.OutputKind is OutputKind.ConsoleApplication or OutputKind.WindowsApplication or OutputKind.WindowsRuntimeApplication,
            new EquatableArray<ReferencedRegistrar>(referenced));
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

        return aliases.IsDefaultOrEmpty || aliases.Contains("global", StringComparer.Ordinal);
    }

    private static string GenerateSource(Registrar registrar)
    {
        // Public only with something another assembly must register before it starts: sets of its own, or the languages
        // of an application, which a host referencing it (a Blazor Web App's server references its client) would
        // otherwise not have until the application's own code first runs.
        var accessibility = registrar.Sets.Length > 0 || registrar.Languages.Length > 0 ? "public" : "internal";
        var hasOwnSets = registrar.Sets.Length > 0 || registrar.LanguageCodes.Length > 0;

        var sb = new StringBuilder();
        sb.AppendLine($$"""
            // <auto-generated/>
            #nullable enable

            namespace {{RegistrarNamespace}}
            {
                /// <summary>
                /// Adds the translation sets of this assembly, and of the assemblies it references, to the sets of the whole app.
            """);

        if (registrar.Languages.Length > 0)
            sb.AppendLine("    /// Registers the app's languages first.");

        sb.AppendLine($$"""
                /// </summary>
                [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
                {{accessibility}} static class {{registrar.Name}}
                {
                    private static int registered;

                    /// <summary>
                    /// Registers the sets, once: when this assembly's code first runs, or before that, when an assembly referencing it registers its own.
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
            sb.AppendLine($"            {GeneratedCodeName}.RegisterLanguages({languages});");
        }

        // Each call inside a lambda of its own: the runtime loads the referenced assembly when it compiles the method that
        // calls into it, so a call in another method is one RegisterReferenced can leave out for an assembly that is not
        // there, and one that fails inside its try when the assembly cannot load after all.
        foreach (var referenced in registrar.ReferencedRegistrars)
            sb.AppendLine($"            RegisterReferenced({SymbolDisplay.FormatLiteral(referenced.AssemblyName, quote: true)}, static () => {RegistrarNamespace}.{referenced.RegistrarName}.Register());");

        // Last, after the sets of the assemblies it references: a set of this assembly rewording one of theirs wins.
        if (hasOwnSets)
            sb.AppendLine($"            {OwnSetsName}.Register();");

        sb.AppendLine("        }");

        if (registrar.ReferencedRegistrars.Length > 0)
        {
            sb.AppendLine("""

                        /// <summary>
                        /// Calls the registrar of a referenced assembly, unless that assembly is not there yet: one the app loads later
                        /// registers its sets itself when its code first runs.
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
        // and CodeChops, so the set Idioma.Demo.ShopTranslationsEN of a consumer would be looked for in CodeChops.Idioma.
        // At the top level it is looked for from the global namespace on, where its full name is right. File-local, so
        // it stands in nobody's way.
        if (hasOwnSets)
        {
            sb.AppendLine($$"""

                /// <summary>
                /// The translation sets of this assembly itself. Outside the namespace above, so that the name of a set is looked
                /// for from the global namespace on, and not in that namespace first.
                /// </summary>
                file static class {{OwnSetsName}}
                {
                    internal static void Register()
                    {
                """);

            // Before the sets register: the registry files a set under the language code it reads from it.
            foreach (var recorded in registrar.LanguageCodes)
                sb.AppendLine($"        {GeneratedCodeName}.RegisterLanguageCode<{recorded.FullyQualifiedName}>({SymbolDisplay.FormatLiteral(recorded.Code, quote: true)});");

            AppendRegistration(sb, "RegisterDefaults", registrar.Sets.AsImmutableArray().Where(static set => !set.IsOverride));
            AppendRegistration(sb, "Register", registrar.Sets.AsImmutableArray().Where(static set => set.IsOverride));

            sb.AppendLine("""
                    }
                }
                """);
        }

        sb.AppendLine("#nullable restore");

        return sb.ToString();
    }

    private static void AppendRegistration(StringBuilder sb, string method, IEnumerable<RegisteredTranslations> sets)
    {
        var creations = sets.Select(static set => $"new {set.FullyQualifiedName}()").ToArray();

        if (creations.Length is 0)
            return;

        sb.AppendLine($"        {RegistryName}.{method}(");

        for (var i = 0; i < creations.Length; i++)
            sb.AppendLine($"            {creations[i]}{(i < creations.Length - 1 ? "," : ");")}");
    }
}

/// <summary>
/// What the registrar of an assembly needs to know about it: the registrar's name, whether the assembly is an
/// application, and the public registrars of the assemblies it references.
/// </summary>
internal readonly record struct RegistrarAssembly(
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
/// A set whose class declares a <c>LanguageCode</c> that implements nothing: what <c>LOC015</c> reports.
/// </summary>
internal readonly record struct UnmappedLanguageCode(
    string SetName,
    string DeclaringType,
    string DeclaringTypeBase,
    DiagnosticLocation Location);

/// <summary>
/// The registrar to write: its name, the sets it registers, the referenced registrars it calls first, the languages of
/// an application that declares them, the default first, and the language codes it records for sets named after them.
/// </summary>
internal readonly record struct Registrar(
    string Name,
    EquatableArray<RegisteredTranslations> Sets,
    EquatableArray<ReferencedRegistrar> ReferencedRegistrars,
    EquatableArray<string> Languages,
    EquatableArray<RecordedLanguageCode> LanguageCodes);

/// <summary>
/// A set whose name gives it a code the last two letters cannot hold (<c>ShopTranslationsEnGB</c>: <c>en-GB</c>), which
/// the registrar records before the set registers. The metadata name orders the records.
/// </summary>
internal readonly record struct RecordedLanguageCode(
    string MetadataName,
    string FullyQualifiedName,
    string Code);
