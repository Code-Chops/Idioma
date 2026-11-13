using System.Reflection;
using CodeChops.Idioma.Internals;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="TranslationSetAttribute"/>: its usage, and the metadata the generator reads it by.
/// </summary>
public sealed class TranslationSetAttributeTests
{
    [Test]
    public void ShouldTargetAssembliesManyTimes()
    {
        // Arrange & Act
        var usage = typeof(TranslationSetAttribute).GetCustomAttribute<AttributeUsageAttribute>()!;

        // Assert
        usage.ValidOn.ShouldBe(AttributeTargets.Assembly);
        usage.AllowMultiple.ShouldBeTrue();
    }

    [Test]
    public void ShouldHoldContractAndLanguage()
    {
        // Arrange & Act
        var attribute = new TranslationSetAttribute("Shop.IShopTranslations", "nl");

        // Assert
        attribute.Contract.ShouldBe("Shop.IShopTranslations");
        attribute.Language.ShouldBe("nl");
    }

    [Test]
    public void Metadata_ShouldMatchWhatGeneratorReads()
    {
        // Arrange & Act
        var parameters = typeof(TranslationSetAttribute).GetConstructors().ShouldHaveSingleItem().GetParameters();

        // Assert: the generator matches the attribute by this name, and reads its arguments by position.
        typeof(TranslationSetAttribute).FullName.ShouldBe("CodeChops.Idioma.Internals.TranslationSetAttribute");
        parameters.Select(parameter => parameter.ParameterType).ShouldBe([typeof(string), typeof(string)]);
    }
}
