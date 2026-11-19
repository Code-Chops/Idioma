using System.Reflection;
using CodeChops.Idioma.Internals;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="LocalizationSetAttribute"/>: its usage, and the metadata the generator reads it by.
/// </summary>
public sealed class LocalizationSetAttributeTests
{
    [Test]
    public void ShouldTargetAssembliesManyTimes()
    {
        // Arrange & Act
        var usage = typeof(LocalizationSetAttribute).GetCustomAttribute<AttributeUsageAttribute>()!;

        // Assert
        usage.ValidOn.ShouldBe(AttributeTargets.Assembly);
        usage.AllowMultiple.ShouldBeTrue();
    }

    [Test]
    public void ShouldHoldContractAndLanguage()
    {
        // Arrange & Act
        var attribute = new LocalizationSetAttribute("Shop.IShopLocalization", "nl");

        // Assert
        attribute.Contract.ShouldBe("Shop.IShopLocalization");
        attribute.Language.ShouldBe("nl");
    }

    [Test]
    public void Metadata_ShouldMatchWhatGeneratorReads()
    {
        // Arrange & Act
        var parameters = typeof(LocalizationSetAttribute).GetConstructors().ShouldHaveSingleItem().GetParameters();

        // Assert: the generator matches the attribute by this name, and reads its arguments by position.
        typeof(LocalizationSetAttribute).FullName.ShouldBe("CodeChops.Idioma.Internals.LocalizationSetAttribute");
        parameters.Select(parameter => parameter.ParameterType).ShouldBe([typeof(string), typeof(string)]);
    }
}
