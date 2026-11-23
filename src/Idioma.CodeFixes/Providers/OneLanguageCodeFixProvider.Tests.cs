using System.Collections.Immutable;
using CodeChops.Idioma.CodeFixes;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="OneLanguageCodeFixProvider"/>: wording written in one language (<c>LOC500</c>) becomes the
/// default language's wording of an inline text, and the compiler then names each language the call lacks.
/// </summary>
public sealed class OneLanguageCodeFixProviderTests
{
    #region Fixture
    private static string Source(string text, string languages = "\"en\", \"nl\"", string usings = "using CodeChops.Idioma;\n") => $$"""
        {{usings}}
        [assembly: CodeChops.Idioma.SupportedLanguages({{languages}})]

        namespace TestApp;

        public sealed class Page
        {
            public CodeChops.Idioma.LocalizableText Title { get; set; } = CodeChops.Idioma.LocalizableText.Inline(en: "Page", nl: "Pagina");

            public void Show(bool flag, int number, System.Collections.Generic.List<CodeChops.Idioma.LocalizableText> texts)
            {
                {{text}}
            }
        }
        """;

    private static Task<ImmutableArray<(string Title, ImmutableArray<string> Sources)>> FixAsync(string source)
    {
        return CodeFixTestHelper.FixAfterGeneratorsAsync(new OneLanguageCodeFixProvider(), new OneLanguageTextAnalyzer(), source);
    }
    #endregion

    [Test]
    [Arguments("Title = \"Orders\";", "Title = LocalizableText.Inline(en: \"Orders\");")]
    [Arguments("Title = $\"Order {number}\";", "Title = LocalizableText.Inline(en: $\"Order {number}\");")]
    [Arguments("Title = \"Order \" + number;", "Title = LocalizableText.Inline(en: \"Order \" + number);")]
    [Arguments("Title = flag ? \"Yes\" : \"No\";", "Title = LocalizableText.Inline(en: flag ? \"Yes\" : \"No\");")]
    [Arguments("Title = (LocalizableText)\"Orders\";", "Title = LocalizableText.Inline(en: \"Orders\");")]
    [Arguments("Title = (\"Orders\");", "Title = (LocalizableText.Inline(en: \"Orders\"));")]
    [Arguments("texts.Add(\"Orders\");", "texts.Add(LocalizableText.Inline(en: \"Orders\"));")]
    public async Task WordingInOneLanguage_ShouldBecomeTheDefaultWordingOfAnInlineText(string text, string expectedText)
    {
        // Arrange & Act
        var fixes = await FixAsync(Source(text));

        // Assert: the wording keeps its place, and the compiler then asks for the Dutch wording.
        var fix = fixes.ShouldHaveSingleItem();
        fix.Title.ShouldBe("Write it as LocalizableText.Inline(en: ...)");

        var source = fix.Sources.ShouldHaveSingleItem();
        source.ShouldBe(Source(expectedText));
        (await GeneratorTestHelper.AnalyzeAsync(source)).ShouldBeEmpty();

        var error = GeneratorTestHelper.CompileWithGenerators(source).ShouldHaveSingleItem();
        error.Id.ShouldBe("CS7036");
        error.GetMessage(System.Globalization.CultureInfo.InvariantCulture).ShouldContain("'nl'");
    }

    [Test]
    [Arguments("\"en-GB\", \"nl\"", "enGb")]
    [Arguments("\"is\", \"en\"", "@is")]
    public async Task DefaultLanguage_ShouldNameTheArgument(string languages, string parameter)
    {
        // Arrange & Act
        var fixes = await FixAsync(Source("Title = \"Orders\";", languages));

        // Assert
        var fix = fixes.ShouldHaveSingleItem();
        fix.Title.ShouldBe($"Write it as LocalizableText.Inline({parameter}: ...)");
        fix.Sources.ShouldHaveSingleItem().ShouldBe(Source($"Title = LocalizableText.Inline({parameter}: \"Orders\");", languages));
    }

    [Test]
    public async Task FileThatCannotNameTheText_ShouldGetItsFullName()
    {
        // Arrange & Act: the file has no using of the package.
        var fixes = await FixAsync(Source("Title = \"Orders\";", usings: ""));

        // Assert
        fixes.ShouldHaveSingleItem().Sources.ShouldHaveSingleItem()
            .ShouldBe(Source("Title = CodeChops.Idioma.LocalizableText.Inline(en: \"Orders\");", usings: ""));
    }

    [Test]
    [Arguments("Title += \" (draft)\";")]
    [Arguments("const string heading = \"Orders\"; Title = heading;")]
    [Arguments("var found = texts.Contains(\"Orders\");")]
    [Arguments("var index = texts.IndexOf(\"Orders\");")]
    [Arguments("var counts = new System.Collections.Generic.Dictionary<LocalizableText, int>(); var count = counts[\"Orders\"];")]
    [Arguments("Title = LocalizableText.Keyed(\"orders\", \"Orders\");")]
    public async Task WordingThatIsNoTextOfItsOwn_ShouldGetNoFix(string text)
    {
        // Arrange
        var source = Source(text);
        (await GeneratorTestHelper.AnalyzeAsync(source)).ShouldContain(static diagnostic => diagnostic.Id == "LOC500");

        // Act
        var fixes = await FixAsync(source);

        // Assert
        fixes.ShouldBeEmpty();
    }

    [Test]
    public async Task FixAll_ShouldWriteEveryWordingAsAnInlineText()
    {
        // Arrange
        var source = Source("Title = \"Orders\"; Title = flag ? \"Yes\" : \"No\";");

        // Act
        var fixedSource = (await CodeFixTestHelper.FixAllAfterGeneratorsAsync(new OneLanguageCodeFixProvider(), new OneLanguageTextAnalyzer(), source)).ShouldHaveSingleItem();

        // Assert
        fixedSource.ShouldBe(Source("Title = LocalizableText.Inline(en: \"Orders\"); Title = LocalizableText.Inline(en: flag ? \"Yes\" : \"No\");"));
    }
}
