namespace CodeChops.Idioma.Generators;

/// <summary>
/// Reports each referenced library that was built with a version of the package that the version this compilation
/// references may not run (<c>LOC702</c>): another major version, or below 1.0 another minor version. It writes no
/// code.
/// </summary>
/// <remarks>
/// <para>
/// NuGet gives an app one version of the package for every library, the highest any of them asks for, without a word.
/// A library built with such a version may call what this version no longer has: the code the source generator
/// wrote into it, which calls the package's <c>Internals</c>, and the package's own API. Such a library fails where
/// that code first runs. Its registrar runs from the app's module initializer, so the app stops before <c>Main</c>,
/// with a <c>TypeInitializationException</c> that names no library.
/// </para>
/// <para>
/// A library stores the version of the package it was built with in its reference to the package's assembly, whose
/// version is that of the package (Directory.Build.props). A new major version may break it, and below 1.0 so may a new
/// minor version, as semantic versioning lets anything change there (<see cref="AreCompatible"/>). A version that changed
/// nothing the library calls is reported all the same.
/// </para>
/// <para>
/// A generator rather than an analyzer, because the diagnostic belongs to no syntax or symbol of the compilation, which
/// an analyzer is handed. A generator reports it on every run, in an IDE too.
/// </para>
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class ReferencedVersionsGenerator : IIncrementalGenerator
{
    public const string OtherVersionDiagnosticId = "LOC702";

    private static readonly DiagnosticDescriptor OtherVersionRule = new(
        id: OtherVersionDiagnosticId,
        title: "Library built with an incompatible version of Idioma",
        messageFormat: "'{0}' was built with CodeChops.Idioma {1}, and this project uses {2}. Code built with {1} may not run with {2}, so update '{0}' to a version built with {2}, or rebuild it.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "An app runs every library with the one version of the package it references. A new major version may change what a library built with an earlier one calls: the code the source generator wrote into it, and the package's own API. Below 1.0, so may a new minor version. So such a library could fail when the app starts, and the build reports it instead.",
        helpLinkUri: PackageNames.HelpLink(OtherVersionDiagnosticId));

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Runs on every change of the compilation in an IDE, so it reads no more than the references of the referenced
        // assemblies, and ends in a plain value that keeps everything after it cached.
        var otherVersions = context.CompilationProvider.Select(static (compilation, cancellationToken) => ReadOtherVersions(compilation, cancellationToken));

        // Without a location, because nothing in the source names the library.
        context.RegisterSourceOutput(otherVersions, static (context, libraries) =>
        {
            foreach (var library in libraries)
                context.ReportDiagnostic(Diagnostic.Create(OtherVersionRule, Location.None, library.AssemblyName, library.BuiltWith, library.Used));
        });
    }

    /// <summary>
    /// The referenced assemblies that were built with a version of the package that the version this compilation
    /// references may not run, in a fixed order.
    /// </summary>
    internal static EquatableArray<OtherVersionLibrary> ReadOtherVersions(Compilation compilation, CancellationToken cancellationToken)
    {
        // The version this compilation references, which the app runs every library with.
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

            libraries.Add(new OtherVersionLibrary(assembly.Identity.Name, FormatVersion(builtWith.Version), FormatVersion(used.Version)));
        }

        return new EquatableArray<OtherVersionLibrary>([.. libraries.OrderBy(static library => library.AssemblyName, StringComparer.Ordinal)]);
    }

    /// <summary>
    /// Whether a library built with one version of the package runs with another: both of one major version, and below
    /// 1.0, of one minor version too, as semantic versioning lets anything change there. So 1.2 runs with 1.9, and 0.14.0
    /// with 0.14.3, but 0.13 doesn't run with 0.14, nor 1.x with 2.x.
    /// </summary>
    internal static bool AreCompatible(Version builtWith, Version used)
    {
        var areCompatible = builtWith.Major == used.Major && (used.Major > 0 || builtWith.Minor == used.Minor);
        return areCompatible;
    }

    /// <summary>
    /// A version as the package numbers its releases, such as <c>0.15.0</c>, without the revision the assembly version
    /// adds.
    /// </summary>
    private static string FormatVersion(Version version)
    {
        var formatted = version.Revision > 0 ? version.ToString() : version.ToString(3);
        return formatted;
    }
}

/// <summary>
/// A referenced library that was built with a version of the package that the version the compilation references may
/// not run (<c>LOC702</c>).
/// </summary>
/// <param name="AssemblyName">The name of the library's assembly.</param>
/// <param name="BuiltWith">The version of the package the library was built with.</param>
/// <param name="Used">The version of the package the compilation references.</param>
internal readonly record struct OtherVersionLibrary(string AssemblyName, string BuiltWith, string Used);
