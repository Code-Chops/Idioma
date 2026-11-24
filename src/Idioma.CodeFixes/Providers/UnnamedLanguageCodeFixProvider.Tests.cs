using System.Collections.Immutable;
using CodeChops.Idioma.CodeFixes;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis.CodeFixes;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="UnnamedLanguageCodeFixProvider"/>: a call of <c>LocalizableText.Inline</c> with a language
/// argument without its name (<c>LOC500</c>) gets each such argument named after its language, and so does
/// <c>[Localized]</c> on a member.
/// </summary>
public sealed class UnnamedLanguageCodeFixProviderTests
{
    #region Fixture
    private static readonly UnnamedLanguageCodeFixProvider Provider = new();
    private static readonly UnnamedLanguageAnalyzer Analyzer = new();

    private static string Source(string languages, string text) => $$"""
        using CodeChops.Idioma;

        [assembly: SupportedLanguages({{languages}})]

        namespace TestApp;

        public static class Texts
        {
            public static string Orders => {{text}};
        }
        """;

    private static Task<ImmutableArray<(string Title, ImmutableArray<string> Sources)>> FixAsync(string source)
    {
        return CodeFixTestHelper.FixAfterGeneratorsAsync(new UnnamedLanguageCodeFixProvider(), new UnnamedLanguageAnalyzer(), source);
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
    [Arguments("\"en\", \"nl\"", """[Localized("City", "Plaats")]""", """[Localized(en: "City", nl: "Plaats")]""")]
    [Arguments("\"en\", \"nl\"", """[Localized("City", nl: "Plaats")]""", """[Localized(en: "City", nl: "Plaats")]""")]
    [Arguments("\"en\", \"nl\"", """[Localized(en: "City", "Plaats")]""", """[Localized(en: "City", nl: "Plaats")]""")]
    [Arguments("\"en\", \"nl\", \"en-GB\"", """[Localized("Color", "Kleur", "Colour")]""", """[Localized(en: "Color", nl: "Kleur", enGb: "Colour")]""")]
    [Arguments("\"en\", \"is\"", """[Localized("City", "Borg")]""", """[Localized(en: "City", @is: "Borg")]""")]
    public async Task UnnamedLanguageOfLocalized_ShouldGetTheNameOfItsLanguage(string languages, string attribute, string expectedAttribute)
    {
        // Arrange
        static string Member(string languages, string attribute) => $$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages({{languages}})]

            namespace TestApp;

            public class Address
            {
                {{attribute}}
                public string City { get; } = "";
            }
            """;

        // Act
        var fixes = await FixAsync(Member(languages, attribute));

        // Assert: every argument without a name gets the one of its position, and the attribute then passes.
        var fix = fixes.ShouldHaveSingleItem();
        fix.Title.ShouldBe("Name each language");

        var source = fix.Sources.ShouldHaveSingleItem();
        source.ShouldBe(Member(languages, expectedAttribute));
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

    [Test]
    public async Task FixAll_InDocument_ShouldNameEveryCallOfThatFileOnly()
    {
        // Arrange: a file with two calls, and another file with one.
        var first = Source("\"en\", \"nl\"", """LocalizableText.Inline("Orders", "Bestellingen") + LocalizableText.Inline("Total", "Totaal")""");
        const string Second = """
            using CodeChops.Idioma;

            namespace TestApp;

            public static class MoreTexts
            {
                public static string Cart => LocalizableText.Inline("Cart", "Winkelwagen");
            }
            """;

        // Act
        var sources = await CodeFixTestHelper.FixAllAfterGeneratorsAsync(Provider, Analyzer, FixAllScope.Document, fromFile: 0, first, Second);

        // Assert
        sources[0].ShouldBe(Source("\"en\", \"nl\"", """LocalizableText.Inline(en: "Orders", nl: "Bestellingen") + LocalizableText.Inline(en: "Total", nl: "Totaal")"""));
        sources[1].ShouldBe(Second);
    }

    [Test]
    [Arguments(FixAllScope.Project, false)]
    [Arguments(FixAllScope.Solution, true)]
    public async Task FixAll_InAppThatReferencesLibrary_ShouldChangeTheLibraryOnlyInSolution(FixAllScope scope, bool libraryChanges)
    {
        // Arrange: a library and an app that references it, each with a call that names no language.
        var library = Source("\"en\", \"nl\"", """LocalizableText.Inline("Orders", "Bestellingen")""").Replace("TestApp", "ShopLibrary", StringComparison.Ordinal);
        var app = Source("\"en\", \"nl\"", """LocalizableText.Inline("Cart", "Winkelwagen") + ShopLibrary.Texts.Orders""");

        // Act: "Fix all" is chosen in the file of the app.
        var projects = await CodeFixTestHelper.FixAllAfterGeneratorsAsync(Provider, Analyzer, scope, from: (Project: 1, File: 0),
            new TestProject("ShopLibrary", [library]),
            new TestProject("TestApp", [app], "ShopLibrary"));

        // Assert: a fix of the project leaves the other project as it is.
        projects[0].ShouldHaveSingleItem().ShouldBe(libraryChanges ? library.Replace("(\"Orders\", \"Bestellingen\")", "(en: \"Orders\", nl: \"Bestellingen\")", StringComparison.Ordinal) : library);
        projects[1].ShouldHaveSingleItem().ShouldBe(Source("\"en\", \"nl\"", """LocalizableText.Inline(en: "Cart", nl: "Winkelwagen") + ShopLibrary.Texts.Orders"""));
    }
}
