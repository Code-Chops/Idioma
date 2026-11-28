namespace CodeChops.Idioma.Generators;

/// <summary>
/// Reports each referenced library built with a package version this one may not run (<c>LOC702</c>,
/// <see cref="AreCompatible"/>). It writes no code.
/// </summary>
/// <remarks>
/// <para>
/// NuGet silently gives an app one package version for every library. A library's generated code may call what that
/// version lacks, and its registrar runs from the app's module initializer, so the app would stop before <c>Main</c>
/// with an exception that names no library.
/// </para>
/// <para>
/// The version is read from the library's reference to the package's assembly, which has the package version
/// (Directory.Build.props). A generator rather than an analyzer, because the diagnostic belongs to no syntax or symbol.
/// </para>
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class ReferencedVersionsGenerator : IIncrementalGenerator
{
    public const string OtherVersionDiagnosticId = "LOC702";

    private static readonly DiagnosticDescriptor OtherVersionRule = new(
        id: OtherVersionDiagnosticId,
        title: "Library built with an incompatible version of Idioma",
        messageFormat: "'{0}' was built with CodeChops.Idioma {1}, and this project uses {2}. Code built with {1} may not run with {2}, so {3}.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "An app runs every library with the one version of the package it references. A new major version may change what a library built with an earlier one calls: the code the source generator wrote into it, and the package's own API. Below 1.0, so may a new minor version. A library built with a newer version may call what the app's version doesn't have yet. So such a library could fail when the app starts, and the build reports it instead.",
        helpLinkUri: PackageNames.HelpLink(OtherVersionDiagnosticId));

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Runs on every compilation change, so it reads only assembly references into a plain, cacheable value.
        var otherVersions = context.CompilationProvider.Select(static (compilation, cancellationToken) => ReadOtherVersions(compilation, cancellationToken));

        // Without a location, because nothing in the source names the library.
        context.RegisterSourceOutput(otherVersions, static (context, libraries) =>
        {
            foreach (var library in libraries)
                context.ReportDiagnostic(Diagnostic.Create(OtherVersionRule, Location.None, library.AssemblyName, library.BuiltWith, library.Used, GetAdvice(library)));
        });
    }

    /// <summary>
    /// The referenced assemblies built with an incompatible package version, sorted by name.
    /// </summary>
    internal static EquatableArray<OtherVersionLibrary> ReadOtherVersions(Compilation compilation, CancellationToken cancellationToken)
    {
        // The app runs every library with the version this compilation references.
        if (compilation.ReferencedAssemblyNames.FirstOrDefault(static name => String.Equals(name.Name, PackageNames.Assembly, StringComparison.Ordinal)) is not { } used)
            return default;

        var libraries = ImmutableArray.CreateBuilder<OtherVersionLibrary>();

        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var builtWith = assembly.Modules.First().ReferencedAssemblies
                .FirstOrDefault(static identity => String.Equals(identity.Name, PackageNames.Assembly, StringComparison.Ordinal));

            if (builtWith is null || AreCompatible(builtWith.Version, used.Version))
                continue;

            libraries.Add(new OtherVersionLibrary(assembly.Identity.Name, FormatVersion(builtWith.Version), FormatVersion(used.Version), IsNewer: builtWith.Version > used.Version));
        }

        return new EquatableArray<OtherVersionLibrary>([.. libraries.OrderBy(static library => library.AssemblyName, StringComparer.Ordinal)]);
    }

    /// <summary>
    /// Whether a library built with one version runs with another. The major version must match. Below 1.0, the minor
    /// version must match too, because semantic versioning lets anything change there. From 1.0, the library's minor
    /// version must be no newer. So 1.2 runs with 1.9, but 1.9 doesn't run with 1.2.
    /// </summary>
    internal static bool AreCompatible(Version builtWith, Version used)
    {
        var areCompatible = builtWith.Major == used.Major
                            && (used.Major > 0 ? builtWith.Minor <= used.Minor : builtWith.Minor == used.Minor);

        return areCompatible;
    }

    /// <summary>
    /// The advice that ends the <c>LOC702</c> message.
    /// </summary>
    private static string GetAdvice(OtherVersionLibrary library)
    {
        var advice = library.IsNewer
            ? $"update CodeChops.Idioma in this project to {library.BuiltWith} or later"
            : $"update '{library.AssemblyName}' to a version built with {library.Used}, or rebuild it";

        return advice;
    }

    /// <summary>
    /// A version such as <c>0.15.0</c>, without the assembly version's revision.
    /// </summary>
    private static string FormatVersion(Version version)
    {
        var formatted = version.Revision > 0 ? version.ToString() : version.ToString(3);
        return formatted;
    }
}

/// <summary>
/// A referenced library built with an incompatible package version (<c>LOC702</c>).
/// </summary>
/// <param name="Used">The package version the compilation references.</param>
internal readonly record struct OtherVersionLibrary(string AssemblyName, string BuiltWith, string Used, bool IsNewer);
