using Microsoft.CodeAnalysis.Diagnostics;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Writes a module initializer that registers the assembly's sets with <c>LocalizationRegistry</c>, without a
/// registration call or reflection. Each set registers with its tier (<c>SetTier</c>), because load order isn't
/// registration order: a library set of its own contract's assembly, a rewording of another assembly's set, or a
/// rewording of the app. Within a tier, a set comes after the sets it derives from, so it wins over them.
/// </summary>
/// <remarks>
/// <para>
/// Every set is first described with the language the build reads from its name (<c>GeneratedCode.DescribeSet</c>),
/// such as <c>ShopLocalizationEnGb</c> with <c>en-GB</c>. A nested set the registrar can name is described too, for a
/// registration by hand. The registry reads no name, so a renamed class keeps its language. It also writes the texts of
/// the <c>[Localized]</c> members (<see cref="LocalizedMembersSource"/>).
/// </para>
/// <para>
/// An unused assembly never loads, so the registrar of an assembly with sets or member texts is public. An app, an
/// assembly that declares languages and an assembly with sets call each referenced registrar first. They call it
/// directly, because that survives trimming and NativeAOT. Each call is guarded by a lookup by name, because the
/// assembly may be absent, and the WebAssembly runtime refuses every later use after one failed call.
/// </para>
/// <para>
/// An app's registrar first registers its languages (<c>GeneratedCode.RegisterLanguages</c>), with the default script
/// of each that <see cref="LikelyScripts"/> lists. Only the first registration counts, so a referenced app,
/// such as the one a test project tests, never decides. An app is an executable, or a library with the MSBuild property
/// <c>AndroidApplication</c> or <c>IdiomaApplication</c>. It must declare its languages (<c>LOC101</c>).
/// </para>
/// </remarks>
public static class LocalizationRegistrationPart
{
    public const string AppWithoutLanguagesDiagnosticId = "LOC101";
    public const string CannotRegisterDiagnosticId = "LOC207";

    /// <summary>
    /// The MSBuild property with which the Android SDK marks an app's project.
    /// </summary>
    internal const string AndroidApplicationProperty = "build_property.AndroidApplication";

    /// <summary>
    /// The MSBuild property with which a library project, such as an AWS Lambda function, says it's the app.
    /// </summary>
    internal const string IdiomaApplicationProperty = "build_property.IdiomaApplication";

    internal const string RegistrarNamespace = PackageNames.Namespace + ".Generated";

    /// <summary>
    /// The pipeline step of the registrar, for a test to read it.
    /// </summary>
    internal const string RegistrarTrackingName = "Registrar";

    /// <summary>
    /// The class outside the registrar's namespace that registers the assembly's own sets.
    /// </summary>
    private const string OwnSetsName = "OwnLocalizationSets";

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
        title: "Localization set can't be registered",
        messageFormat: "'{0}' can't be registered {1} because {2}, so its texts won't be found. {3}.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Sets are registered automatically, by generated code that creates an instance of each. That needs a top-level class with a parameterless constructor, and a language code. The code is the end of the class name, starting with a capital (two letters, as in ShopLocalizationNl, or a longer code that [assembly: SupportedLanguages] lists, as in ShopLocalizationEnGb, or a region of a language it lists). The build reads it, because the registry files a set under its language without creating it. Without this warning, a set that can't be registered would silently leave its texts in their default language. A nested or file-local set is reported only when it rewords another assembly's texts. A set marked [ExcludeFromLocalizationDiscovery] is registered by hand, and it's reported only without a language code, because LocalizationRegistry.Register throws for it.",
        helpLinkUri: PackageNames.HelpLink(CannotRegisterDiagnosticId));

    /// <summary>
    /// What <c>LOC207</c> advises for a set the generated code can't register. The message format ends it with a period.
    /// </summary>
    private const string CannotRegisterAutomaticallyAdvice = "Fix that, or mark it [ExcludeFromLocalizationDiscovery] and pass an instance to LocalizationRegistry.Register";

    /// <summary>
    /// What <c>LOC207</c> advises for an excluded set without a language. The message format ends it with a period.
    /// </summary>
    private const string CannotRegisterByHandAdvice = "LocalizationRegistry.Register throws for it. Rename it, or derive a class from it whose name ends in the code";

    internal static void Initialize(IncrementalGeneratorInitializationContext context, LocalizationInputs inputs)
    {
        var languages = inputs.RegistrarLanguages;

        // The registrar is written from a share without locations or wordings, and diagnostics come from the other.
        var discovery = inputs.RegistrarDiscovery;
        var reported = inputs.ContractDiscovery;

        // Excluded sets included.
        var allSets = discovery.Select(static (discovery, _) => discovery.Sets);
        var allReportedSets = reported.Select(static (discovery, _) => discovery.Sets);

        // An excluded set is registered by hand, so it gets no registration and no warning about one.
        var sets = allSets.Select(static (sets, _) => new EquatableArray<DiscoveredLocalization>(sets
            .Where(static set => !set.IsExcluded)
            .ToImmutableArray()));

        var reportedSets = allReportedSets.Select(static (sets, _) => new EquatableArray<DiscoveredLocalization>(sets
            .Where(static set => !set.IsExcluded)
            .ToImmutableArray()));

        // Every set with a language, because the registry reads a set's language from its description alone. Excluded and
        // nested sets too, which are registered by hand. Each code is cased as culture names are, because the registry
        // takes it as it is.
        var descriptions = discovery.Select(static (discovery, _) => new EquatableArray<SetDescription>(discovery.Sets
            .Where(static set => set.Language is not null)
            .Select(static set => new SetDescription(set.MetadataName, set.FullyQualifiedName, set.Language!))
            .Concat(discovery.NestedSets)
            .Select(static set => set with { Code = SupportedLanguages.Normalize(set.Code) })
            .Distinct()
            .OrderBy(static set => set.MetadataName, StringComparer.Ordinal)
            .ToImmutableArray()));

        var registrations = sets.Select(static (sets, _) => new EquatableArray<RegisteredLocalization>(sets
            .Where(set => set.RegistrationProblem is null)
            .Select(set => new RegisteredLocalization(set.MetadataName, set.FullyQualifiedName, set.IsOverride, set.Depth))
            .Distinct()
            .OrderBy(set => set.Depth)
            .ThenBy(set => set.MetadataName, StringComparer.Ordinal)
            .ToImmutableArray()));

        // Not a default-language set, which LOC200 reports, because LOC207's advice to exclude it is no way out.
        var unregistrable = reportedSets
            .Combine(languages)
            .Combine(inputs.Contracts)
            .Select(static (data, _) =>
            {
                var ((sets, languages), contracts) = data;

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

        // A library project can be the app, which an MSBuild property says (build/CodeChops.Idioma.props). The Android
        // SDK sets AndroidApplication, and a hosted project, such as an AWS Lambda function, sets IdiomaApplication.
        var isMarkedApplication = context.AnalyzerConfigOptionsProvider.Select(static (options, _) =>
            IsTrue(options.GlobalOptions, AndroidApplicationProperty) || IsTrue(options.GlobalOptions, IdiomaApplicationProperty));

        // Runs on every compilation change, so it does one type lookup per reference into a plain, cacheable value.
        var assembly = context.CompilationProvider
            .Select(static (compilation, _) => ReadAssembly(compilation))
            .Combine(isMarkedApplication)
            .Select(static (data, _) => data.Right ? data.Left with { IsApplication = true } : data.Left);

        // Read only here, because each [Localized] is bound again on every edit. An enum with a set gets its GetText()
        // from that set's generated class.
        var enumsWithSets = allSets.Select(static (sets, _) => new EquatableArray<string>(sets
            .SelectMany(static set => set.EnumNames)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToImmutableArray()));

        var members = LocalizedMembers.ReadModel(context, languages)
            .Combine(enumsWithSets)
            .Select(static (data, _) => LocalizedMembers.WithoutExtensionsOf(data.Left, data.Right));

        // Only the class name, so editing a wording doesn't rewrite the registrar.
        var memberTexts = members.Select(static (model, _) => model.HasTexts ? model.MemberTextsClassName : null);

        var registrar = registrations
            .Combine(descriptions)
            .Combine(assembly)
            .Combine(languages)
            .Combine(memberTexts)
            .Select(static (data, _) =>
            {
                var ((((sets, descriptions), assembly), languages), memberTexts) = data;
                var forwards = sets.Length > 0 || assembly.IsApplication || languages is { Codes.Length: > 0 };
                // Not without a default language (LOC102), so the next language doesn't become the app's default.
                var appLanguages = assembly.IsApplication && languages is { DefaultLanguage: not null } declared
                    ? new EquatableArray<string>(declared.Codes.AsImmutableArray().Select(SupportedLanguages.Normalize).ToImmutableArray())
                    : default;

                return new Registrar(
                    Name: assembly.RegistrarName,
                    IsApplication: assembly.IsApplication,
                    Sets: sets,
                    ReferencedRegistrars: forwards ? assembly.ReferencedRegistrars : default,
                    Languages: appLanguages,
                    Descriptions: descriptions,
                    MemberTexts: memberTexts);
            })
            .WithTrackingName(RegistrarTrackingName);

        // Not an app whose attribute is invalid, which LOC102 reports.
        var appWithoutLanguages = assembly
            .Combine(languages)
            .Select(static (data, _) => data.Left.IsApplication && data.Right is null ? data.Left.AssemblyName : null);

        // Without a location, because nothing in the source says the project is an app.
        context.RegisterSourceOutput(appWithoutLanguages, static (context, assemblyName) =>
        {
            if (assemblyName is not null)
                context.ReportDiagnostic(Diagnostic.Create(AppWithoutLanguagesRule, Location.None, assemblyName));
        });

        context.RegisterSourceOutput(unregistrable, static (context, sets) =>
        {
            foreach (var set in sets.AsImmutableArray())
                context.ReportDiagnostic(Diagnostic.Create(CannotRegisterRule, set.Location.ToLocation(), set.MetadataName, "automatically", set.Problem, CannotRegisterAutomaticallyAdvice));
        });

        // An excluded set is registered by hand, which throws for a set without a language.
        var excludedWithoutLanguage = allReportedSets.Select(static (sets, _) => new EquatableArray<UnregistrableLocalization>(sets
            .Where(static set => set is { IsExcluded: true, Language: null })
            .Select(static set => new UnregistrableLocalization(set.MetadataName, LocalizationDiscovery.NoLanguageCodeProblem, set.Location))
            .Distinct()
            .OrderBy(static set => set.MetadataName, StringComparer.Ordinal)
            .ToImmutableArray()));

        context.RegisterSourceOutput(excludedWithoutLanguage, static (context, sets) =>
        {
            foreach (var set in sets.AsImmutableArray())
                context.ReportDiagnostic(Diagnostic.Create(CannotRegisterRule, set.Location.ToLocation(), set.MetadataName, "by hand", set.Problem, CannotRegisterByHandAdvice));
        });

        context.RegisterSourceOutput(reported.Select(static (discovery, _) => discovery.UnnameableOverrides), static (context, sets) =>
        {
            foreach (var set in sets.AsImmutableArray())
                context.ReportDiagnostic(Diagnostic.Create(CannotRegisterRule, set.Location.ToLocation(), set.MetadataName, "automatically", set.Problem, CannotRegisterAutomaticallyAdvice));
        });

        // No code below C# 14 (LOC104), but diagnostics are still reported.
        var compiles = inputs.Compiles;

        context.RegisterSourceOutput(members.WhereItCompiles(compiles), static (context, model) =>
        {
            if (model.HasTexts)
                context.AddSource(LocalizedMembersSource.HintName, LocalizedMembersSource.Generate(model));
        });

        context.RegisterSourceOutput(registrar.WhereItCompiles(compiles), static (context, registrar) =>
        {
            if (registrar.Sets.Length is 0 && registrar.ReferencedRegistrars.Length is 0 && registrar.Languages.Length is 0 && registrar.Descriptions.Length is 0
                && registrar.MemberTexts is null)
            {
                return;
            }

            context.AddSource("LocalizationRegistrar.g.cs", GenerateSource(registrar));
        });
    }

    /// <summary>
    /// <c>LocalizationRegistrar_My_App</c> for <c>My-App</c>, so an app can name each library's registrar unambiguously.
    /// </summary>
    internal static string GetRegistrarName(string? assemblyName)
    {
        var name = $"LocalizationRegistrar_{CodeEscaping.ToIdentifierPart(assemblyName)}";
        return name;
    }

    /// <summary>
    /// This assembly's registrar name, whether it's an executable, and the referenced public registrars it can name.
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

        // A name two assemblies share (My-App and My_App), or this assembly's own, would bind ambiguously or to itself.
        // Such an assembly registers its sets when it loads.
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
    /// Whether a property is <see langword="true"/> in any case, as MSBuild reads a boolean.
    /// </summary>
    private static bool IsTrue(AnalyzerConfigOptions options, string key)
    {
        var isTrue = options.TryGetValue(key, out var value) && String.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        return isTrue;
    }

    /// <summary>
    /// Whether code can name the assembly's types without an extern alias.
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
        // Public only with sets or member texts, which a referencing assembly must register before this one's code runs.
        // An app's languages don't count, because a referenced app's languages never do.
        var accessibility = registrar.Sets.Length > 0 || registrar.MemberTexts is not null ? "public" : "internal";
        var hasOwnSets = registrar.Sets.Length > 0 || registrar.Descriptions.Length > 0;

        // The pragma also covers an obsolete set, because its author can't act on a warning in generated code.
        const string warnings = GeneratedFile.ObsoleteWarnings;
        var sb = new StringBuilder();
        sb.AppendLine($$"""
            {{GeneratedFile.GetHeader(warnings, "Only generated code calls GeneratedCode.")}}

            namespace {{RegistrarNamespace}}
            {
                /// <summary>
                /// Adds the localization sets of this assembly, and of the assemblies it references, to the sets of the whole app.
            """);

        if (registrar.Languages.Length > 0)
            sb.AppendLine("    /// Registers the app's languages first.");

        if (registrar.MemberTexts is not null)
            sb.AppendLine("    /// Registers the texts of this assembly's members marked <c>[Localized]</c> too.");

        sb.AppendLine($$"""
                /// </summary>
                {{GeneratedFile.CodeAttributes}}
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

        // Before the referenced registrars, because only the first registration of languages counts, and it marks the
        // app, whose enum texts win.
        // With the default script of each language, so a culture with its script, such as zh-Hant-TW, reaches zh-TW only
        // where that's the same script.
        if (registrar.Languages.Length > 0)
        {
            var languages = String.Join(", ", registrar.Languages.Select(static language => SymbolDisplay.FormatLiteral(language, quote: true)));
            var scripts = String.Join(", ", registrar.Languages.Select(static language => LikelyScripts.Find(language) is { } script ? SymbolDisplay.FormatLiteral(script, quote: true) : "null"));
            sb.AppendLine($"            {PackageNames.GeneratedCode}.RegisterLanguages(typeof({registrar.Name}), [{languages}], [{scripts}]);");
        }

        // Each call in its own lambda, because the runtime loads an assembly when it compiles the method that calls it.
        // So RegisterReferenced can skip an absent assembly, and a failed load stays inside its try.
        foreach (var referenced in registrar.ReferencedRegistrars)
            sb.AppendLine($"            RegisterReferenced({SymbolDisplay.FormatLiteral(referenced.AssemblyName, quote: true)}, static () => {RegistrarNamespace}.{referenced.RegistrarName}.Register());");

        // Their order doesn't matter, because a member belongs to one assembly.
        if (registrar.MemberTexts is { } memberTexts)
            sb.AppendLine($"            {memberTexts}.Register();");

        // Last, so a rewording of a referenced assembly's set wins.
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

        // At the top level, so a set's name resolves from the global namespace. Inside CodeChops.Idioma.Generated,
        // Idioma.Demo.ShopLocalizationEn would be looked for in CodeChops.Idioma.
        if (hasOwnSets)
        {
            sb.AppendLine($$"""

                /// <summary>
                /// The localization sets of this assembly itself. This class is outside the namespace above, so a set's name is
                /// resolved from the global namespace, not from that namespace first.
                /// </summary>
                {{GeneratedFile.CodeAttributes}}
                file static class {{OwnSetsName}}
                {
                    internal static void Register()
                    {
                """);

            // Before the sets register, because the registry files a set under its described language.
            // With the default script of its language, so a culture with another script, such as sr-Latn-RS, doesn't
            // reach a set for sr-RS.
            foreach (var description in registrar.Descriptions)
            {
                var script = LikelyScripts.Find(description.Code) is { } found ? $", {SymbolDisplay.FormatLiteral(found, quote: true)}" : "";
                sb.AppendLine($"        {PackageNames.GeneratedCode}.DescribeSet<{description.FullyQualifiedName}>({SymbolDisplay.FormatLiteral(description.Code, quote: true)}{script});");
            }

            AppendRegistration(sb, "LibrarySets", registrar.Sets.AsImmutableArray().Where(static set => !set.IsOverride));
            AppendRegistration(sb, registrar.IsApplication ? "ApplicationRewordings" : "Rewordings", registrar.Sets.AsImmutableArray().Where(static set => set.IsOverride));

            sb.AppendLine("""
                    }
                }
                """);
        }

        GeneratedFile.AppendFooter(sb, warnings);

        return sb.ToString();
    }

    /// <summary>
    /// Registers sets with a factory each. The registry creates them on first use, because a throwing constructor in the
    /// module initializer would take the app down before Main.
    /// </summary>
    private static void AppendRegistration(StringBuilder sb, string tier, IEnumerable<RegisteredLocalization> sets)
    {
        var registrations = sets.Select(
            static set => $"(typeof({set.FullyQualifiedName}), static () => new {set.FullyQualifiedName}())").ToArray();

        if (registrations.Length is 0)
            return;

        sb.AppendLine($"        {PackageNames.GeneratedCode}.RegisterSets({PackageNames.InternalsNamespace}.SetTier.{tier},");

        for (var i = 0; i < registrations.Length; i++)
            sb.AppendLine($"            {registrations[i]}{(i < registrations.Length - 1 ? "," : ");")}");
    }
}

/// <summary>
/// What the registrar needs to know about its assembly.
/// </summary>
internal readonly record struct RegistrarAssembly(
    string AssemblyName,
    string RegistrarName,
    bool IsApplication,
    EquatableArray<ReferencedRegistrar> ReferencedRegistrars);

/// <summary>
/// The public registrar of a referenced assembly, which the generated code looks up by assembly name before calling it.
/// </summary>
internal readonly record struct ReferencedRegistrar(
    string AssemblyName,
    string RegistrarName);

/// <summary>
/// The registrar to write. <see cref="Languages"/> are an app's, the default first, each cased as culture names are
/// (<see cref="SupportedLanguages.Normalize"/>). <see cref="MemberTexts"/> is
/// <see langword="null"/> without <c>[Localized]</c> members.
/// </summary>
internal readonly record struct Registrar(
    string Name,
    bool IsApplication,
    EquatableArray<RegisteredLocalization> Sets,
    EquatableArray<ReferencedRegistrar> ReferencedRegistrars,
    EquatableArray<string> Languages,
    EquatableArray<SetDescription> Descriptions,
    string? MemberTexts);

/// <summary>
/// A set's language (<see cref="DiscoveredLocalization.Language"/>), described before it registers.
/// </summary>
internal readonly record struct SetDescription(
    string MetadataName,
    string FullyQualifiedName,
    string Code);
