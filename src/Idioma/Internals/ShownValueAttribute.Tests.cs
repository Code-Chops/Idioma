using System.Reflection;
using CodeChops.Idioma.Internals;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="ShownValueAttribute"/>: its usage, and the metadata the analyzer reads it by.
/// </summary>
public sealed class ShownValueAttributeTests
{
    [Test]
    public void ShouldTargetParametersOnce()
    {
        // Arrange & Act
        var usage = typeof(ShownValueAttribute).GetCustomAttribute<AttributeUsageAttribute>()!;

        // Assert
        usage.ValidOn.ShouldBe(AttributeTargets.Parameter);
        usage.AllowMultiple.ShouldBeFalse();
    }

    [Test]
    public void Metadata_ShouldMatchWhatGeneratorWritesAndAnalyzerReads()
    {
        // Arrange & Act
        var constructor = typeof(ShownValueAttribute).GetConstructors().ShouldHaveSingleItem();

        // Assert: the generator writes the attribute by this name, without arguments, and the analyzer finds it by it.
        typeof(ShownValueAttribute).FullName.ShouldBe("CodeChops.Idioma.Internals.ShownValueAttribute");
        constructor.GetParameters().ShouldBeEmpty();
    }
}
