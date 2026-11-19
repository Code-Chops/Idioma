using CodeChops.Idioma.CodeFixes;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="MissingLanguageCodeFixProvider"/>: a contract without a set for one of the languages
/// (<c>LOC201</c>) gets that set in a file of its own, without texts, so the compiler names each text it lacks.
/// </summary>
public sealed class MissingLanguageCodeFixProviderTests
{
    #region Fixture
    private static string Source(string languages, string accessibility = "public") => $$"""
        using CodeChops.Idioma;

        [assembly: SupportedLanguages({{languages}})]

        namespace TestApp;

        {{accessibility}} class ShopLocalizationEn : ILocalizationSet, IShopLocalization
        {
            public virtual string Checkout => "Checkout";
        }

        {{accessibility}} class ShopLocalizationNl : ILocalizationSet, IShopLocalization
        {
            public string Checkout => "Afrekenen";
        }
        """;

    private static void ShouldReportNothing(params string[] sources)
    {
        var run = GeneratorTestHelper.RunOverFiles(new LocalizationSetsGenerator(), sources).Results.ShouldHaveSingleItem();

        run.Diagnostics.ShouldBeEmpty();
    }
    #endregion

    [Test]
    [Arguments("public")]
    [Arguments("internal")]
    public async Task MissingLanguage_ShouldGetASetThatImplementsTheContract(string accessibility)
    {
        // Arrange
        var source = Source("\"en\", \"nl\", \"de\"", accessibility);

        // Act
        var fixes = await CodeFixTestHelper.FixWithAddedFilesAsync(new MissingLanguageCodeFixProvider(), new LocalizationSetsGenerator(), source);

        // Assert: a file of its own next to the default-language set, as accessible as it, and the language is no longer
        // missing. Its texts are the compiler's to name (CS0535), not the generator's.
        var fix = fixes.ShouldHaveSingleItem();
        fix.Title.ShouldBe("Add 'ShopLocalizationDe'");
        fix.Sources.ShouldHaveSingleItem().ShouldBe(source);

        var (path, text) = fix.Added.ShouldHaveSingleItem();
        path.ShouldBe("/TestAssembly/ShopLocalizationDe.cs");
        text.ShouldBe($$"""
            using CodeChops.Idioma;

            namespace TestApp;

            {{accessibility}} class ShopLocalizationDe : ILocalizationSet, IShopLocalization
            {
            }

            """);

        ShouldReportNothing(source, text);
    }

    [Test]
    public async Task MissingRegion_ShouldGetASetDerivingFromItsLanguage()
    {
        // Arrange
        var source = Source("\"en\", \"nl\", \"en-GB\"");

        // Act
        var fixes = await CodeFixTestHelper.FixWithAddedFilesAsync(new MissingLanguageCodeFixProvider(), new LocalizationSetsGenerator(), source);

        // Assert: it keeps the English wording for every text it doesn't override, and needs no using of its own.
        var (_, text) = fixes.ShouldHaveSingleItem().Added.ShouldHaveSingleItem();
        text.ShouldBe("""
            namespace TestApp;

            public class ShopLocalizationEnGb : ShopLocalizationEn
            {
            }

            """);

        ShouldReportNothing(source, text);
    }

    [Test]
    public async Task GlobalUsingOfThePackage_ShouldLeaveTheUsingOut()
    {
        // Arrange
        var source = Source("\"en\", \"nl\", \"de\"");

        // Act
        var fixes = await CodeFixTestHelper.FixWithAddedFilesAsync(new MissingLanguageCodeFixProvider(), new LocalizationSetsGenerator(), source,
            "global using CodeChops.Idioma;");

        // Assert
        fixes.ShouldHaveSingleItem().Added.ShouldHaveSingleItem().Text.ShouldStartWith("namespace TestApp;");
    }

    [Test]
    public async Task ClassOfThatNameWithoutILocalizationSet_ShouldGetNoFix()
    {
        // Arrange: a class that only needs ILocalizationSet. Adding a second one of its name wouldn't compile.
        var source = $$"""
            {{Source("\"en\", \"nl\", \"de\"")}}

            public class ShopLocalizationDe : IShopLocalization
            {
                public string Checkout => "Zur Kasse";
            }
            """;

        // Act
        var fixes = await CodeFixTestHelper.FixWithAddedFilesAsync(new MissingLanguageCodeFixProvider(), new LocalizationSetsGenerator(), source);

        // Assert
        fixes.ShouldBeEmpty();
    }
}
