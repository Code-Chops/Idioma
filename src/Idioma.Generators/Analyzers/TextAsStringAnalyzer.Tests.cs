using System.Collections.Immutable;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="TextAsStringAnalyzer"/>: a text passed as a string value to a text of a set is reported
/// (<c>LOC303</c>), and a string that is meant is not.
/// </summary>
public sealed class TextAsStringAnalyzerTests
{
    #region Fixture
    private const string Sets = """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace TestApp;

        public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
        {
            public string Checkout => "Checkout";
            public string PressToPay(string button) => $"Press {button} to pay";
            public string PressToConfirm(LocalizableText button) => $"Press {button} to confirm";
            public string Invited(string host, string guest) => $"{host} has invited {guest}";
        }

        public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
        {
            public string Checkout => "Afrekenen";
            public string PressToPay(string button) => $"Druk op {button} om te betalen";
            public string PressToConfirm(LocalizableText button) => $"Druk op {button} om te bevestigen";
            public string Invited(string host, string guest) => $"{host} heeft {guest} uitgenodigd";
        }
        """;

    /// <summary>
    /// The sets, and a class that keeps the given text.
    /// </summary>
    private static string Usage(string text) => $$"""
        {{Sets}}

        public static class Usage
        {
            public static LocalizableText Text(string name) => {{text}};

            public static LocalizableText Own(string button) => LocalizableText.Inline(en: button, nl: button);
        }
        """;

    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source, MetadataReference[]? references = null, params (string Path, string Text)[] files)
    {
        return GeneratorTestHelper.AnalyzeAsync(new TextAsStringAnalyzer(), source, references ?? [], files);
    }

    private static string At(Diagnostic diagnostic)
    {
        return diagnostic.Location.SourceTree!.ToString().Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length);
    }
    #endregion

    [Test]
    public async Task TextPassedToStringParameterOfTextOfSet_ShouldReportLOC303AtTheText()
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(Usage("ShopLocalization.PressToPay(ShopLocalization.Checkout)"));

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(TextAsStringAnalyzer.TextAsStringDiagnosticId);
        diagnostic.Id.ShouldBe("LOC303");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().ShouldBe("'ShopLocalization.PressToPay' takes this text as the string 'button', so the text keeps the wording of the language in use at this call. Make 'button' a LocalizableText in every set, so the text follows the user's language. To keep the wording on purpose, call ToString() on the text.");
        At(diagnostic).ShouldBe("ShopLocalization.Checkout");
        diagnostic.Descriptor.HelpLinkUri.ShouldEndWith("#loc303");
    }

    [Test]
    public async Task TextsPassedToTwoStringParameters_ShouldReportEach()
    {
        // Arrange & Act: one text of a set, and one inline text.
        var diagnostics = await AnalyzeAsync(Usage("""ShopLocalization.Invited(guest: ShopLocalization.Checkout, host: LocalizableText.Inline(en: "The shop", nl: "De winkel"))"""));

        // Assert: each by the name of its own parameter, whatever order the arguments are written in.
        diagnostics.Select(static diagnostic => diagnostic.GetMessage().Split(',')[0]).ShouldBe(
        [
            "'ShopLocalization.Invited' takes this text as the string 'guest'",
            "'ShopLocalization.Invited' takes this text as the string 'host'",
        ], ignoreOrder: true);
    }

    [Test]
    [Arguments("ShopLocalization.PressToConfirm(ShopLocalization.Checkout)")]
    [Arguments("ShopLocalization.PressToPay(ShopLocalization.Checkout.ToString())")]
    [Arguments("ShopLocalization.PressToPay((string)ShopLocalization.Checkout)")]
    [Arguments("""ShopLocalization.PressToPay($"{ShopLocalization.Checkout}")""")]
    [Arguments("ShopLocalization.PressToPay(name)")]
    [Arguments("""ShopLocalization.PressToPay("OK")""")]
    [Arguments("Own(ShopLocalization.Checkout)")]
    [Arguments("LocalizableText.Inline(en: ShopLocalization.Checkout, nl: ShopLocalization.Checkout)")]
    public async Task TextThatStaysATextOrStringThatIsMeant_ShouldReportNothing(string text)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(Usage(text));

        // Assert: a parameter that takes a text keeps it, and a conversion the code writes is meant. A method written
        // by hand and an inline text are no texts of a set.
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task TextPassedToTextOfALibrary_ShouldReportLOC303()
    {
        // Arrange: the generated class and its contract lie in another assembly.
        var library = GeneratorTestHelper.CompileLibraryWithGenerators("TextAsStringShopLibrary", Sets);

        // Act
        var diagnostics = await AnalyzeAsync("""
            using CodeChops.Idioma;
            using TestApp;

            [assembly: SupportedLanguages("en", "nl")]

            namespace App;

            public static class Usage
            {
                public static LocalizableText Hint { get; } = ShopLocalization.PressToPay(ShopLocalization.Checkout);
            }
            """, [library]);

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("LOC303");
    }

    [Test]
    [Arguments("Page_razor.g.cs", 1)]
    [Arguments("Other.g.cs", 0)]
    public async Task TextInGeneratedCode_ShouldBeReportedOnlyInRazorOutput(string outputPath, int expected)
    {
        // Arrange: what Razor writes for markup is the user's own, and what another generator writes isn't its
        // author's to change.
        const string generated = """
            // <auto-generated/>
            namespace TestApp;

            internal static class PageRender
            {
                public static CodeChops.Idioma.LocalizableText Render() => ShopLocalization.PressToPay(ShopLocalization.Checkout);
            }
            """;

        // Act
        var diagnostics = await AnalyzeAsync(Usage("ShopLocalization.Checkout"), files: (outputPath, generated));

        // Assert
        diagnostics.Length.ShouldBe(expected);
    }
}
