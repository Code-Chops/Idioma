using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="ReferencedVersionsGenerator"/>: the libraries built with a version of the package that this one
/// may not run, which it reports (<c>LOC702</c>).
/// </summary>
public sealed class ReferencedVersionsGeneratorTests
{
    /// <summary>
    /// The version of the package the tests reference, as the message of <c>LOC702</c> names it.
    /// </summary>
    private static Version PackageVersion { get; } = typeof(LocalizedText).Assembly.GetName().Version!;

    /// <summary>
    /// A library built with a version of the package that stands in for another release: an assembly named as the
    /// package's, with that assembly version, which the library stores in its reference to it.
    /// </summary>
    private static MetadataReference LibraryBuiltWith(string version)
    {
        var package = CSharpCompilation.Create(
            PackageNames.Assembly,
            [CSharpSyntaxTree.ParseText($"[assembly: System.Reflection.AssemblyVersion(\"{version}\")] namespace CodeChops.Idioma {{ public static class Package {{ }} }}", GeneratorTestHelper.ParseOptions)],
            GeneratorTestHelper.References.Where(static reference => reference.Display?.Contains(PackageNames.Assembly, StringComparison.Ordinal) is not true),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var library = CSharpCompilation.Create(
            "Shop",
            [CSharpSyntaxTree.ParseText("namespace Shop; public static class Texts { public static System.Type Package => typeof(CodeChops.Idioma.Package); }", GeneratorTestHelper.ParseOptions)],
            [.. GeneratorTestHelper.References.Where(static reference => reference.Display?.Contains(PackageNames.Assembly, StringComparison.Ordinal) is not true), package.ToMetadataReference()],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        library.Emit(stream).Success.ShouldBeTrue();

        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    [Test]
    public void LibraryBuiltWithThisVersion_ShouldReportNothing()
    {
        // Arrange
        var library = GeneratorTestHelper.CompileLibraryWithGenerators("Shop", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace Shop;

            public static class Texts
            {
                public static string Title => LocalizedText.Inline(en: "Shop");
            }
            """);

        // Act
        var run = GeneratorTestHelper.Run(new ReferencedVersionsGenerator(), "namespace TestApp;", library);

        // Assert
        run.Diagnostics.ShouldBeEmpty();
        run.Results.ShouldHaveSingleItem().GeneratedSources.ShouldBeEmpty();
    }

    [Test]
    public void LibraryBuiltWithAnotherMajorVersion_ShouldReportLOC702()
    {
        // Arrange: the major version before the one the tests reference, which below 1.0 is the minor version before it.
        var builtWith = PackageVersion.Major > 0 ? $"{PackageVersion.Major - 1}.0.0" : $"0.{PackageVersion.Minor - 1}.0";
        var library = LibraryBuiltWith($"{builtWith}.0");

        // Act
        var run = GeneratorTestHelper.Run(new ReferencedVersionsGenerator(), "namespace TestApp;", library);

        // Assert
        var used = PackageVersion.ToString(3);
        var diagnostic = run.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(ReferencedVersionsGenerator.OtherVersionDiagnosticId);
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.Location.ShouldBe(Location.None);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldBe(
            $"'Shop' was built with CodeChops.Idioma {builtWith}, and this project uses {used}. Code built with {builtWith} may not run with {used}, so update 'Shop' to a version built with {used}, or rebuild it.");
    }

    [Test]
    public void LibraryBuiltWithANewerVersion_ShouldReportLOC702AdvisingToUpdateThePackage()
    {
        // Arrange: the next major version, which below 1.0 is the next minor version.
        var builtWith = PackageVersion.Major > 0 ? $"{PackageVersion.Major + 1}.0.0" : $"0.{PackageVersion.Minor + 1}.0";
        var library = LibraryBuiltWith($"{builtWith}.0");

        // Act
        var run = GeneratorTestHelper.Run(new ReferencedVersionsGenerator(), "namespace TestApp;", library);

        // Assert
        var used = PackageVersion.ToString(3);
        run.Diagnostics.ShouldHaveSingleItem().GetMessage(CultureInfo.InvariantCulture).ShouldBe(
            $"'Shop' was built with CodeChops.Idioma {builtWith}, and this project uses {used}. Code built with {builtWith} may not run with {used}, so update CodeChops.Idioma in this project to {builtWith} or later.");
    }

    [Test]
    public void LibraryBuiltWithAnotherPatchOfThisVersion_ShouldReportNothing()
    {
        // Arrange
        var library = LibraryBuiltWith($"{PackageVersion.Major}.{PackageVersion.Minor}.{PackageVersion.Build + 1}.0");

        // Act
        var run = GeneratorTestHelper.Run(new ReferencedVersionsGenerator(), "namespace TestApp;", library);

        // Assert
        run.Diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("1.2.0", "1.9.4", true)]
    [Arguments("1.9.4", "1.2.0", false)]
    [Arguments("1.2.5", "1.2.0", true)]
    [Arguments("1.0.0", "2.0.0", false)]
    [Arguments("2.0.0", "1.0.0", false)]
    [Arguments("0.14.0", "0.14.3", true)]
    [Arguments("0.13.0", "0.14.0", false)]
    [Arguments("0.14.0", "1.0.0", false)]
    public void AreCompatible_ShouldCompareTheMajorVersionAndTheMinorVersion(string builtWith, string used, bool expected)
    {
        // Act & Assert: a new major version may break a library, and below 1.0 so may a new minor version, as semantic
        // versioning lets anything change there. From 1.0, a library built with a newer minor version may call what that
        // version added.
        ReferencedVersionsGenerator.AreCompatible(Version.Parse(builtWith), Version.Parse(used)).ShouldBe(expected);
    }

    [Test]
    public void AssemblyThatDoesNotReferenceThePackage_ShouldReportNothing()
    {
        // Arrange
        var library = GeneratorTestHelper.CompileLibrary("Other", "namespace Other; public static class Tools { }");

        // Act
        var run = GeneratorTestHelper.Run(new ReferencedVersionsGenerator(), "namespace TestApp;", library);

        // Assert
        run.Diagnostics.ShouldBeEmpty();
    }

    [Test]
    public void UnrelatedEdit_ShouldLeaveOutputCached()
    {
        // Arrange & Act
        var reasons = GeneratorTestHelper.GetOutputReasonsAfterUnrelatedEdit(new ReferencedVersionsGenerator(), "namespace TestApp;");

        // Assert
        reasons.ShouldNotBeEmpty();
        reasons.ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }
}
