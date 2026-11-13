using System.Reflection;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="InlineTextsAttribute"/>: its usage, and the metadata the generators write and read it by.
/// </summary>
public sealed class InlineTextsAttributeTests
{
    [Test]
    public void ShouldTargetAssembliesOnce()
    {
        // Arrange & Act
        var usage = typeof(InlineTextsAttribute).GetCustomAttribute<AttributeUsageAttribute>()!;

        // Assert
        usage.ValidOn.ShouldBe(AttributeTargets.Assembly);
        usage.AllowMultiple.ShouldBeFalse();
    }

    [Test]
    public void Metadata_ShouldMatchWhatGeneratorsWriteAndRead()
    {
        // Arrange & Act
        var constructor = typeof(InlineTextsAttribute).GetConstructors().ShouldHaveSingleItem();

        // Assert: one generator writes the attribute by this name without arguments, and another matches it by name.
        typeof(InlineTextsAttribute).FullName.ShouldBe("CodeChops.Idioma.InlineTextsAttribute");
        constructor.GetParameters().ShouldBeEmpty();
    }
}
