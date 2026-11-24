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
    public void HelpLink_ShouldPointAtTheSectionOfTheCode()
    {
        // Act
        var helpLink = PackageNames.HelpLink("LOC201");

        // Assert
        helpLink.ShouldBe("https://github.com/Code-Chops/Idioma/blob/main/docs/build-errors.md#loc201");
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

        // Assert: all thirty-one codes, some of which two descriptors share.
        var codes = descriptors.Select(static descriptor => descriptor.Id).Distinct(StringComparer.Ordinal).ToArray();
        codes.Length.ShouldBe(31);

        foreach (var descriptor in descriptors)
            descriptor.HelpLinkUri.ShouldBe(PackageNames.HelpLink(descriptor.Id), descriptor.Id);
    }
}
