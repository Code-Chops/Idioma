using System.Reflection;
using CodeChops.Idioma.Internals;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="ShownValueAttribute"/>: its usage, and the metadata the generator writes and the analyzer reads
/// it by.
/// </summary>
public sealed class ShownValueAttributeTests
{
    [Test]
    public void ShouldTargetMethodsManyTimes()
    {
        // Arrange & Act
        var usage = typeof(ShownValueAttribute).GetCustomAttribute<AttributeUsageAttribute>()!;

        // Assert: once on a method for each value its default wording shows.
        usage.ValidOn.ShouldBe(AttributeTargets.Method);
        usage.AllowMultiple.ShouldBeTrue();
    }

    [Test]
    public void ShouldHoldParameter()
    {
        // Arrange & Act
        var attribute = new ShownValueAttribute("name");

        // Assert
        attribute.Parameter.ShouldBe("name");
    }

    [Test]
    public void Metadata_ShouldMatchWhatGeneratorWritesAndAnalyzerReads()
    {
        // Arrange & Act
        var parameters = typeof(ShownValueAttribute).GetConstructors().ShouldHaveSingleItem().GetParameters();

        // Assert: the generator writes the attribute by this name with the name of a parameter, and the analyzer finds
        // it by this name and reads that argument by its position.
        typeof(ShownValueAttribute).FullName.ShouldBe("CodeChops.Idioma.Internals.ShownValueAttribute");
        parameters.Select(parameter => parameter.ParameterType).ShouldBe([typeof(string)]);
    }
}
