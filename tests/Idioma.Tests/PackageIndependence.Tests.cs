using Assembly = System.Reflection.Assembly;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// The package stands on its own: Formosa builds on it, never the other way around. This project references the package
/// and no Formosa project, so a dependency that creeps into the package shows up in these tests.
/// </summary>
public sealed class PackageIndependenceTests
{
    [Test]
    [Arguments("CodeChops.Idioma")]
    [Arguments("CodeChops.Idioma.Generators")]
    public void Assembly_ShouldNotReferenceFormosa(string assemblyName)
    {
        // Arrange
        var assembly = Assembly.Load(assemblyName);

        // Act
        var references = assembly.GetReferencedAssemblies().Select(reference => reference.Name);

        // Assert
        references.ShouldNotContain(name => name!.StartsWith("CodeChops.Formosa", StringComparison.Ordinal));
    }

    [Test]
    public void Dependencies_ShouldNotBringFormosaAlong()
    {
        // Arrange & Act & Assert: a compiler drops an unused reference from an assembly's metadata, so also check that
        // no project or package in the package's dependency chain put Formosa next to these tests.
        Should.Throw<FileNotFoundException>(() => Assembly.Load("CodeChops.Formosa"));
    }

    [Test]
    [Arguments("CodeChops.Idioma.Generators")]
    [Arguments("CodeChops.Idioma.CodeFixes")]
    public void Analyzers_ShouldNotNeedANewerCompilerThanTheFirstNet10Sdk(string assemblyName)
    {
        // Arrange: a compiler skips an analyzer built against a newer Roslyn than its own (CS9057), so the generator, and
        // with it LocalizableText.Inline, would vanish from the build of a project on SDK 10.0.100. The CI builds and runs
        // these tests with a newer compiler, which accepts any, so only the references of the assembly can tell. The
        // package references of the two projects are pinned for it (VersionOverride), and a bulk package update rewrites
        // them.
        var newestCompilerThatMayRun = new Version(major: 5, minor: 0, build: 0, revision: 0);
        var assembly = Assembly.Load(assemblyName);

        // Act
        var roslyn = assembly.GetReferencedAssemblies()
            .Where(static reference => reference.Name!.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal))
            .ToArray();

        // Assert
        roslyn.ShouldNotBeEmpty();
        roslyn.ShouldAllBe(
            reference => reference.Version! <= newestCompilerThatMayRun,
            $"{assemblyName} references a Roslyn newer than the compiler of .NET SDK 10.0.100 ({newestCompilerThatMayRun}): put VersionOverride=\"5.0.0\" back on its Microsoft.CodeAnalysis package.");
    }

    [Test]
    public void Generators_ShouldCarryNoLicenseCheck()
    {
        // Arrange
        var assembly = Assembly.Load("CodeChops.Idioma.Generators");

        // Act
        var names = assembly.GetTypes().Select(type => type.Name);

        // Assert: this package is MIT and free to use, and compiles in nothing of Formosa's license enforcement.
        names.ShouldNotContain(name => name.Contains("License", StringComparison.OrdinalIgnoreCase));
    }
}
