using System.Reflection;
using CodeChops.Idioma.Internals;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="LocalizationContractAttribute"/>: its usage, and the metadata the generators write and read
/// it by.
/// </summary>
public sealed class LocalizationContractAttributeTests
{
    [Test]
    public void ShouldTargetInterfacesOnce()
    {
        // Arrange & Act
        var usage = typeof(LocalizationContractAttribute).GetCustomAttribute<AttributeUsageAttribute>()!;

        // Assert
        usage.ValidOn.ShouldBe(AttributeTargets.Interface);
        usage.AllowMultiple.ShouldBeFalse();
    }

    [Test]
    public void Metadata_ShouldMatchWhatGeneratorWritesAndReads()
    {
        // Arrange & Act
        var constructor = typeof(LocalizationContractAttribute).GetConstructors().ShouldHaveSingleItem();

        // Assert: the generator writes the attribute by this name without arguments, and the build matches it by name.
        typeof(LocalizationContractAttribute).FullName.ShouldBe("CodeChops.Idioma.Internals.LocalizationContractAttribute");
        constructor.GetParameters().ShouldBeEmpty();
    }
}
