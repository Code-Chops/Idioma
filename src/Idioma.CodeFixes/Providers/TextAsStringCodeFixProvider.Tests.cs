using CodeChops.Idioma.CodeFixes;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="TextAsStringCodeFixProvider"/>: the parameter a text is passed to as a string
/// (<c>LOC303</c>) becomes a <c>LocalizableText</c> in every set.
/// </summary>
public sealed class TextAsStringCodeFixProviderTests
{
    #region Fixture
    /// <summary>
    /// An English set, a British set that overrides its text, a Dutch set that implements it explicitly, and the
    /// given usage.
    /// </summary>
    private static string Sets(string usage) => $$"""
        #nullable enable
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl", "en-GB")]

        namespace TestApp;

        public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
        {
            public string Checkout => "Checkout";
            public virtual string PressToPay(int amount, string button) => $"Press {button} to pay {amount}";
        }

        public class ShopLocalizationEnGb : ShopLocalizationEn
        {
            public override string PressToPay(int amount, string button) => $"Press {button} to pay £{amount}";
        }

        public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
        {
            public string Checkout => "Afrekenen";
            string IShopLocalization.PressToPay(int amount, string /* the button */ button) => $"Druk op {button} om {amount} te betalen";
        }

        public static class Usage
        {
            {{usage}}
        }
        """;

    private static string Fixed(string usage) => Sets(usage)
        .Replace("(int amount, string button)", "(int amount, LocalizableText button)", StringComparison.Ordinal)
        .Replace("string /* the button */ button", "LocalizableText /* the button */ button", StringComparison.Ordinal);

    private const string Hint = "public static LocalizableText Hint { get; } = ShopLocalization.PressToPay(5, ShopLocalization.Checkout);";

    private static TextAsStringCodeFixProvider Provider => new();
    private static TextAsStringAnalyzer Analyzer => new();
    #endregion

    [Test]
    public async Task TextPassedAsString_ShouldMakeTheParameterALocalizableTextInEverySet()
    {
        // Arrange & Act: the workspace holds the generated contract, as an IDE's does.
        var fixes = await CodeFixTestHelper.FixWithGeneratedCodeAsync(Provider, Analyzer, Sets(Hint));

        // Assert: also in the set that overrides the text and the one that implements it explicitly, so the build
        // passes, and the text stays a text.
        var fix = fixes.ShouldHaveSingleItem();
        fix.Title.ShouldBe("Make 'button' a LocalizableText in every set");

        var fixedSource = fix.Sources.ShouldHaveSingleItem();
        fixedSource.ShouldBe(Fixed(Hint));
        GeneratorTestHelper.CompileWithGenerators(fixedSource).ShouldBeEmpty();
        (await GeneratorTestHelper.AnalyzeAsync(Analyzer, fixedSource, [])).ShouldBeEmpty();
    }

    [Test]
    public async Task FixAll_TwoCallsOfOneText_ShouldChangeItsParameterOnce()
    {
        // Arrange
        var usage = $"""
            {Hint}
                public static LocalizableText Other => ShopLocalization.PressToPay(button: ShopLocalization.Checkout, amount: 7);
            """;

        // Act
        var fixedSource = (await CodeFixTestHelper.FixAllAfterGeneratorsAsync(Provider, Analyzer, Sets(usage))).ShouldHaveSingleItem();

        // Assert
        fixedSource.ShouldBe(Fixed(usage));
    }

    [Test]
    public async Task SetInAnotherFile_ShouldChangeToo()
    {
        // Arrange: the call in a file of its own, without the package's namespace in reach of the sets' file by name.
        const string sets = """
            [assembly: CodeChops.Idioma.SupportedLanguages("en")]

            namespace TestApp;

            public class ShopLocalizationEn : CodeChops.Idioma.ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
                public string PressToPay(string? button) => $"Press {button} to pay";
            }
            """;

        const string usage = """
            using CodeChops.Idioma;

            namespace TestApp;

            public static class Usage
            {
                public static LocalizableText Hint { get; } = ShopLocalization.PressToPay(ShopLocalization.Checkout);
            }
            """;

        // Act
        var fixes = await CodeFixTestHelper.FixWithGeneratedCodeAsync(Provider, Analyzer, sets, usage);

        // Assert: by the name the set's file reaches the type with, and as nullable as the string was.
        fixes.ShouldHaveSingleItem().Sources.ShouldBe(
        [
            sets.Replace("string? button", "CodeChops.Idioma.LocalizableText? button", StringComparison.Ordinal),
            usage,
        ]);
    }

    [Test]
    public async Task HelperWithTheNameOfTheText_ShouldStayAsItIs()
    {
        // Arrange: the Dutch set has a private method of the text's name with as many parameters, which isn't the text.
        const string source = """
            #nullable enable
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
                public string PressToPay(string button) => $"Press {button} to pay";
            }

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
                public string PressToPay(string button) => $"Druk op {button} om te betalen{PressToPay(times: 1)}";
                private string PressToPay(int times) => times > 1 ? $" ({times} keer)" : "";
            }

            public static class Usage
            {
                public static LocalizableText Hint { get; } = ShopLocalization.PressToPay(ShopLocalization.Checkout);
            }
            """;

        // Act
        var fixes = await CodeFixTestHelper.FixWithGeneratedCodeAsync(Provider, Analyzer, source);

        // Assert: the helper keeps its parameter, so it doesn't take the text's signature (CS0111).
        var fixedSource = fixes.ShouldHaveSingleItem().Sources.ShouldHaveSingleItem();
        fixedSource.ShouldBe(source.Replace("PressToPay(string button)", "PressToPay(LocalizableText button)", StringComparison.Ordinal));
        GeneratorTestHelper.CompileWithGenerators(fixedSource).ShouldBeEmpty();
    }

    [Test]
    public async Task TextOfALibrary_ShouldGetNoFix()
    {
        // Arrange: the sets and their contract lie in another assembly.
        var library = GeneratorTestHelper.CompileLibraryWithGenerators("TextAsStringFixShopLibrary", Sets(""));

        const string app = """
            using CodeChops.Idioma;
            using TestApp;

            [assembly: SupportedLanguages("en", "nl", "en-GB")]

            namespace App;

            public static class Usage
            {
                public static LocalizableText Hint { get; } = ShopLocalization.PressToPay(5, ShopLocalization.Checkout);
            }
            """;

        // Act
        var diagnostics = await GeneratorTestHelper.AnalyzeAsync(Analyzer, app, [library]);
        var fixes = await CodeFixTestHelper.FixAfterGeneratorsAsync(Provider, Analyzer, [library], app);

        // Assert: reported, but a set here can't change the library's contract.
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("LOC303");
        fixes.ShouldBeEmpty();
    }
}
