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
        var constructors = typeof(LocalizationContractAttribute).GetConstructors()
            .Select(static constructor => constructor.GetParameters().Select(static parameter => parameter.ParameterType).ToArray())
            .OrderBy(static parameters => parameters.Length)
            .ToArray();

        // Assert: the generator writes the attribute by this name, without arguments or with the enums of a set of an
        // enum, and the build matches it by name and reads the enums by position.
        typeof(LocalizationContractAttribute).FullName.ShouldBe("CodeChops.Idioma.Internals.LocalizationContractAttribute");
        constructors.Length.ShouldBe(2);
        constructors[0].ShouldBeEmpty();
        constructors[1].ShouldBe([typeof(Type[])]);
    }

    [Test]
    public void Constructor_WithEnums_ShouldKeepThem()
    {
        // Act
        var attribute = new LocalizationContractAttribute(typeof(DayOfWeek), typeof(StringComparison));

        // Assert
        attribute.Enums.ShouldBe([typeof(DayOfWeek), typeof(StringComparison)]);
    }

    [Test]
    public void Constructor_WithoutEnums_ShouldHaveNone()
    {
        // Act
        var attribute = new LocalizationContractAttribute();

        // Assert
        attribute.Enums.ShouldBeEmpty();
    }

    [Test]
    public void Constructor_WithNull_ShouldThrow()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new LocalizationContractAttribute(null!));
    }
}
