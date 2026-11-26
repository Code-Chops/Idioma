using System.Reflection;
using CodeChops.Idioma.Internals;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="StandaloneTextsAttribute"/>: its usage, and the metadata the generators write and read it by.
/// </summary>
public sealed class StandaloneTextsAttributeTests
{
    [Test]
    public void ShouldTargetAssembliesMoreThanOnce()
    {
        // Arrange & Act
        var usage = typeof(StandaloneTextsAttribute).GetCustomAttribute<AttributeUsageAttribute>()!;

        // Assert: two generated files apply it, one for LocalizedText.Inline and one for [Localized].
        usage.ValidOn.ShouldBe(AttributeTargets.Assembly);
        usage.AllowMultiple.ShouldBeTrue();
    }

    [Test]
    public void Metadata_ShouldMatchWhatGeneratorsWriteAndRead()
    {
        // Arrange & Act
        var constructor = typeof(StandaloneTextsAttribute).GetConstructors().ShouldHaveSingleItem();

        // Assert: one generator writes the attribute by this name without arguments, and another matches it by name.
        typeof(StandaloneTextsAttribute).FullName.ShouldBe("CodeChops.Idioma.Internals.StandaloneTextsAttribute");
        constructor.GetParameters().ShouldBeEmpty();
    }
}
