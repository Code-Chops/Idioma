using System.Reflection;
using CodeChops.Idioma.Internals;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="FormatPlaceholdersAttribute"/>: its usage, and the metadata the analyzer reads it by.
/// </summary>
public sealed class FormatPlaceholdersAttributeTests
{
    [Test]
    public void ShouldTargetPropertiesOnce()
    {
        // Arrange & Act
        var usage = typeof(FormatPlaceholdersAttribute).GetCustomAttribute<AttributeUsageAttribute>()!;

        // Assert
        usage.ValidOn.ShouldBe(AttributeTargets.Property);
        usage.AllowMultiple.ShouldBeFalse();
    }

    [Test]
    public void ShouldHoldPlaceholders()
    {
        // Arrange & Act
        var attribute = new FormatPlaceholdersAttribute(0, 1);
        var none = new FormatPlaceholdersAttribute();

        // Assert
        attribute.Placeholders.ShouldBe([0, 1]);
        none.Placeholders.ShouldBeEmpty();
    }

    [Test]
    public void Metadata_ShouldMatchWhatAnalyzerReads()
    {
        // Arrange & Act
        var parameters = typeof(FormatPlaceholdersAttribute).GetConstructors().ShouldHaveSingleItem().GetParameters();

        // Assert: the generator writes the attribute by this name, and the analyzer reads its argument by position.
        typeof(FormatPlaceholdersAttribute).FullName.ShouldBe("CodeChops.Idioma.Internals.FormatPlaceholdersAttribute");
        parameters.Select(parameter => parameter.ParameterType).ShouldBe([typeof(int[])]);
    }
}
