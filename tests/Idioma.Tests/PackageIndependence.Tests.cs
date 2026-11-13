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
