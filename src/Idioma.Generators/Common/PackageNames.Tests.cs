using System.Reflection;
using CodeChops.Idioma.Generators;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="PackageNames"/>: every build error and warning links to its section of the page that lists
/// them.
/// </summary>
public sealed class PackageNamesTests
{
    [Test]
    public void HelpLink_ShouldPointAtTheSectionOfTheCode_InTheDocsOfThisRelease()
    {
        // Arrange: the publish workflow tags a release v plus its package version, which the assembly carries.
        var version = typeof(PackageNames).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];

        // Act
        var helpLink = PackageNames.HelpLink("LOC201");

        // Assert
        helpLink.ShouldBe($"https://github.com/Code-Chops/Idioma/blob/v{version}/docs/build-errors/LOC2XX-sets.md#loc201");
    }

    [Test]
    public void ReleaseTag_ShouldBeThePackageVersion_WithoutTheCommit()
    {
        // Act
        var tag = PackageNames.ReadReleaseTag();

        // Assert: such as v0.17.0-preview, as Directory.Build.props sets the version of every project.
        tag.ShouldMatch(@"^v\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$");
    }

    [Test]
    public void EveryDiagnostic_ShouldLinkToItsOwnSection()
    {
        // Arrange: every descriptor the generators and analyzers declare.
        var descriptors = typeof(PackageNames).Assembly.GetTypes()
            .SelectMany(static type => type.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
            .Where(static field => field.FieldType == typeof(DiagnosticDescriptor))
            .Select(static field => (DiagnosticDescriptor)field.GetValue(null)!)
            .ToArray();

        // Assert: all thirty-four codes, each with one descriptor, so that an IDE lists each code once, with one title.
        var codes = descriptors.Select(static descriptor => descriptor.Id).Distinct(StringComparer.Ordinal).ToArray();
        codes.Length.ShouldBe(34);
        descriptors.Length.ShouldBe(codes.Length);

        foreach (var descriptor in descriptors)
            descriptor.HelpLinkUri.ShouldBe(PackageNames.HelpLink(descriptor.Id), descriptor.Id);
    }
}
