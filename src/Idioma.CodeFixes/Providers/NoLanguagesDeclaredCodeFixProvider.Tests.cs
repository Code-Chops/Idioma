using System.Collections.Immutable;
using CodeChops.Idioma.CodeFixes;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="NoLanguagesDeclaredCodeFixProvider"/>: a call of <c>LocalizedText.Inline</c> or a
/// <c>[Localized]</c> in an assembly without languages (<c>LOC100</c>) declares the languages its arguments name, the
/// first as the default.
/// </summary>
public sealed class NoLanguagesDeclaredCodeFixProviderTests
{
    #region Fixture
    private const string Usings = "using CodeChops.Idioma;\n\n";

    private static string Inline(string arguments, string attribute = "", string usings = Usings) => $$"""
        {{usings}}{{attribute}}namespace TestApp;

        /// <summary>
        /// The texts of the shop.
        /// </summary>
        public static class Texts
        {
            public static string Orders => CodeChops.Idioma.LocalizedText.Inline({{arguments}});
        }
        """;

    private static string Localized(string arguments, string attribute = "") => $$"""
        {{Usings}}{{attribute}}namespace TestApp;

        public sealed class Address
        {
            [Localized({{arguments}})]
            public string City { get; set; } = "";
        }
        """;

    private static Task<ImmutableArray<(string Title, ImmutableArray<string> Sources)>> FixAsync(string source)
    {
        return CodeFixTestHelper.FixAfterGeneratorsAsync(new NoLanguagesDeclaredCodeFixProvider(), new UnnamedLanguageAnalyzer(), source);
    }

    /// <summary>
    /// The build reports nothing: the languages are declared, and the call or the attribute compiles.
    /// </summary>
    private static async Task ShouldReportNothingAsync(string source)
    {
        GeneratorTestHelper.CompileWithGenerators(source).ShouldBeEmpty();
        GeneratorTestHelper.GetGeneratorDiagnostics(source).ShouldBeEmpty();
        (await GeneratorTestHelper.AnalyzeAsync(source)).ShouldBeEmpty();
    }
    #endregion

    [Test]
    [Arguments("en: \"Orders\", nl: \"Bestellingen\"", "\"en\", \"nl\"")]
    [Arguments("nl: \"Bestellingen\", en: \"Orders\"", "\"nl\", \"en\"")]
    [Arguments("en: \"Color\", enGb: \"Colour\"", "\"en\", \"en-GB\"")]
    [Arguments("en: \"Orders\", zhHans: \"订单\"", "\"en\", \"zh-Hans\"")]
    [Arguments("en: \"Orders\", @is: \"Pantanir\"", "\"en\", \"is\"")]
    public async Task InlineTextWithNamedLanguages_ShouldDeclareThem(string arguments, string languages)
    {
        // Arrange & Act
        var fixes = await FixAsync(Inline(arguments));

        // Assert: the attribute stands after the usings, and the doc comment stays with its class.
        var fix = fixes.ShouldHaveSingleItem();
        fix.Title.ShouldBe($"Add [assembly: SupportedLanguages({languages})]");

        var source = fix.Sources.ShouldHaveSingleItem();
        source.ShouldBe(Inline(arguments, $"[assembly: SupportedLanguages({languages})]\n\n"));
        await ShouldReportNothingAsync(source);
    }

    [Test]
    public async Task FileThatCannotNameTheAttribute_ShouldGetItsFullName()
    {
        // Arrange & Act: the file has no using of the package, and no blank line before its namespace.
        const string arguments = "en: \"Orders\", nl: \"Bestellingen\"";
        var fixes = await FixAsync(Inline(arguments, usings: ""));

        // Assert: with the line endings of the file, which are those of this one.
        var newLine = Inline(arguments).Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var source = fixes.ShouldHaveSingleItem().Sources.ShouldHaveSingleItem();
        source.ShouldBe(Inline(arguments, $"[assembly: CodeChops.Idioma.SupportedLanguages(\"en\", \"nl\")]{newLine}{newLine}", usings: ""));
        await ShouldReportNothingAsync(source);
    }

    [Test]
    [Arguments("", "[assembly: SupportedLanguages(\"en\", \"nl\")]\n\n")]
    [Arguments("[assembly: System.CLSCompliant(false)]\n\n", "[assembly: System.CLSCompliant(false)]\n[assembly: SupportedLanguages(\"en\", \"nl\")]\n\n")]
    public async Task FileWithWindowsLineEndings_ShouldKeepThemOnTheNewLines(string other, string expected)
    {
        // Arrange: a file with CRLF throughout, whichever line endings this file has.
        const string arguments = "en: \"Orders\", nl: \"Bestellingen\"";
        var source = Inline(arguments, other).ReplaceLineEndings("\r\n");

        // Act
        var fixes = await FixAsync(source);

        // Assert: the attribute's line and the blank line after it end as the others do.
        var fixedSource = fixes.ShouldHaveSingleItem().Sources.ShouldHaveSingleItem();
        fixedSource.ShouldBe(Inline(arguments, expected).ReplaceLineEndings("\r\n"));
        fixedSource.Replace("\r\n", "", StringComparison.Ordinal).ShouldNotContain('\n');
    }

    [Test]
    public async Task FileWithAssemblyAttributes_ShouldGetItAfterThem()
    {
        // Arrange & Act
        const string arguments = "en: \"Orders\", nl: \"Bestellingen\"";
        const string other = "[assembly: System.CLSCompliant(false)]\n\n";
        var fixes = await FixAsync(Inline(arguments, other));

        // Assert
        fixes.ShouldHaveSingleItem().Sources.ShouldHaveSingleItem()
            .ShouldBe(Inline(arguments, "[assembly: System.CLSCompliant(false)]\n[assembly: SupportedLanguages(\"en\", \"nl\")]\n\n"));
    }

    [Test]
    [Arguments("\"Orders\", \"Bestellingen\"")]
    [Arguments("en: \"Orders\", \"Bestellingen\"")]
    [Arguments("en: \"Orders\", en: \"Bestellingen\"")]
    public async Task InlineTextWithoutALanguageForEachArgument_ShouldGetNoFix(string arguments)
    {
        // Arrange
        var source = Inline(arguments);
        (await GeneratorTestHelper.AnalyzeAsync(source)).ShouldContain(static diagnostic => diagnostic.Id == "LOC100");

        // Act
        var fixes = await FixAsync(source);

        // Assert
        fixes.ShouldBeEmpty();
    }

    [Test]
    public async Task MemberMarkedWithNamedLanguages_ShouldDeclareThem()
    {
        // Arrange & Act: the generator reports the attribute, which binds to nothing.
        const string arguments = "en: \"City\", nl: \"Plaats\"";
        var fixes = await CodeFixTestHelper.FixAsync(new NoLanguagesDeclaredCodeFixProvider(), new LocalizedTextFactoryGenerator(), Localized(arguments));

        // Assert
        var fix = fixes.ShouldHaveSingleItem();
        fix.Title.ShouldBe("Add [assembly: SupportedLanguages(\"en\", \"nl\")]");

        var source = fix.Sources.ShouldHaveSingleItem();
        source.ShouldBe(Localized(arguments, "[assembly: SupportedLanguages(\"en\", \"nl\")]\n\n"));
        await ShouldReportNothingAsync(source);
    }

    [Test]
    public async Task SetInAnAssemblyWithoutLanguages_ShouldGetNoFix()
    {
        // Arrange: the name gives the set its language, but not which language is the default.
        const string source = """
            using CodeChops.Idioma;

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }
            """;

        // Act
        var fixes = await CodeFixTestHelper.FixAsync(new NoLanguagesDeclaredCodeFixProvider(), LocalizationParts.Sets(), source);

        // Assert
        fixes.ShouldBeEmpty();
    }
}
