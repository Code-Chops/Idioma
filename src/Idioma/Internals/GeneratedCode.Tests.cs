using System.Reflection;
using CodeChops.Idioma.Internals;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="GeneratedCode"/>: the names generated code calls it by, and that it is all generated code needs.
/// </summary>
public sealed class GeneratedCodeTests
{
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
        members.ShouldBe(["CreateText", "CreateResourceText", "RegisterLanguages", "RegisterLanguageCode"], ignoreOrder: true);
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
}
