using System.Collections.Immutable;
using CodeChops.Idioma.CodeFixes;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="UnnamedLanguageCodeFixProvider"/>: a call of <c>LocalizableText.Inline</c> with a language
/// argument without its name (<c>LOC501</c>) gets each such argument named after its language.
/// </summary>
public sealed class UnnamedLanguageCodeFixProviderTests
{
    #region Fixture
    private static string Source(string languages, string text) => $$"""
        using CodeChops.Idioma;

        [assembly: SupportedLanguages({{languages}})]

        namespace TestApp;

        public static class Texts
        {
            public static LocalizableText Orders { get; } = {{text}};
        }
        """;

    private static Task<ImmutableArray<(string Title, ImmutableArray<string> Sources)>> FixAsync(string source)
    {
        return CodeFixTestHelper.FixAfterGeneratorsAsync(new UnnamedLanguageCodeFixProvider(), new OneLanguageTextAnalyzer(), source);
    }
    #endregion

    [Test]
    [Arguments("\"en\", \"nl\"", """LocalizableText.Inline("Orders", "Bestellingen")""", """LocalizableText.Inline(en: "Orders", nl: "Bestellingen")""")]
    [Arguments("\"en\", \"nl\"", """LocalizableText.Inline("Orders", nl: "Bestellingen")""", """LocalizableText.Inline(en: "Orders", nl: "Bestellingen")""")]
    [Arguments("\"en\", \"nl\"", """LocalizableText.Inline(en: "Orders", "Bestellingen")""", """LocalizableText.Inline(en: "Orders", nl: "Bestellingen")""")]
    [Arguments("\"en\", \"nl\", \"en-GB\"", """LocalizableText.Inline("Color", "Kleur", "Colour")""", """LocalizableText.Inline(en: "Color", nl: "Kleur", enGb: "Colour")""")]
    [Arguments("\"en\", \"is\"", """LocalizableText.Inline("Orders", "Pantanir")""", """LocalizableText.Inline(en: "Orders", @is: "Pantanir")""")]
    public async Task UnnamedLanguage_ShouldGetTheNameOfItsLanguage(string languages, string text, string expectedText)
    {
        // Arrange & Act
        var fixes = await FixAsync(Source(languages, text));

        // Assert: every argument without a name gets the one of its position, and the call then passes.
        var fix = fixes.ShouldHaveSingleItem();
        fix.Title.ShouldBe("Name each language");

        var source = fix.Sources.ShouldHaveSingleItem();
        source.ShouldBe(Source(languages, expectedText));
        (await GeneratorTestHelper.AnalyzeAsync(source)).ShouldBeEmpty();
    }

    [Test]
    public async Task ArgumentsOnLinesOfTheirOwn_ShouldKeepTheirIndentation()
    {
        // Arrange
        const string text = """
            LocalizableText.Inline(
                    "Orders",
                    "Bestellingen")
            """;
        const string expectedText = """
            LocalizableText.Inline(
                    en: "Orders",
                    nl: "Bestellingen")
            """;

        // Act
        var fixes = await FixAsync(Source("\"en\", \"nl\"", text));

        // Assert: each name goes after the line break and the indentation, before the wording.
        fixes.ShouldHaveSingleItem().Sources.ShouldHaveSingleItem().ShouldBe(Source("\"en\", \"nl\"", expectedText));
    }
}
