using System.Reflection;
using CodeChops.Idioma.Internals;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="GeneratedCode"/>: the names generated code calls it by, and that it is all generated code needs.
/// </summary>
public sealed class GeneratedCodeTests
{
    #region Fixture
    private interface IGeneratedCodeTranslations : ITranslationSet
    {
        string Greeting(string name);
    }

    private sealed class GeneratedCodeTranslationsEN : IGeneratedCodeTranslations
    {
        public string Greeting(string name) => $"Hello, {name}!";
    }
    #endregion

    [Test]
    public void MetadataName_ShouldMatchSourceGeneratorReferences()
    {
        // Arrange & Act
        var members = typeof(GeneratedCode)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(static method => method.Name);

        // Assert: generated code calls these by name, so a rename or namespace change must update the generators'
        // emission in the same commit, and the analyzer's constant (LOC012).
        typeof(GeneratedCode).FullName.ShouldBe("CodeChops.Idioma.Internals.GeneratedCode");
        members.ShouldBe(["CreateText", "CreateResourceText", "CreateResourceText", "RegisterLanguages", "RegisterLanguageCode"], ignoreOrder: true);
    }

    [Test]
    public void CreateResourceText_WithArguments_ShouldGiveTextThatKeepsThem()
    {
        // Arrange
        var name = "Anna";

        // Act: as a method of the class the source generator writes calls it.
        var text = GeneratedCode.CreateResourceText<IGeneratedCodeTranslations>("Shop.Greeting", static (set, values) => ((IGeneratedCodeTranslations)set).Greeting((string)values[0]!), new GeneratedCodeTranslationsEN(), [name]);

        // Assert
        text.Key.ShouldBe("Shop.Greeting");
        text.Contract.ShouldBe(typeof(IGeneratedCodeTranslations));
        text.DefaultText.ShouldBe("Hello, Anna!");
        text.Arguments.ShouldBe(["Anna"]);
        Should.Throw<ArgumentException>(() => GeneratedCode.CreateResourceText<IGeneratedCodeTranslations>(" ", static (set, values) => ((IGeneratedCodeTranslations)set).Greeting((string)values[0]!), new GeneratedCodeTranslationsEN(), [name]));
    }

    [Test]
    public void CreateText_ShouldGiveTextWithItsTranslations()
    {
        // Arrange & Act
        var text = GeneratedCode.CreateText("Orders", ("nl", "Bestellingen"));

        // Assert
        text.DefaultText.ShouldBe("Orders");
        text.GetTranslation(new CultureInfo("nl-NL")).ShouldBe("Bestellingen");
    }

    [Test]
    [Arguments("", "en")]
    [Arguments("s", "")]
    public void CreateText_ShouldTakeEmptyWordingInAnyLanguage(string english, string dutch)
    {
        // Arrange & Act: a suffix one language has and another lacks. The default language is no exception.
        var text = GeneratedCode.CreateText(english, ("en", english), ("nl", dutch));

        // Assert
        text.DefaultText.ShouldBe(english);
        text.GetTranslation(new CultureInfo("nl-NL")).ShouldBe(dutch);
    }

    [Test]
    public void CreateText_ShouldRefuseNullWording()
    {
        // Arrange, Act & Assert
        Should.Throw<ArgumentNullException>(() => GeneratedCode.CreateText(null!, ("nl", "Bestellingen")));
        Should.Throw<ArgumentNullException>(() => GeneratedCode.CreateText("Orders", ("nl", null!)));
    }
}
