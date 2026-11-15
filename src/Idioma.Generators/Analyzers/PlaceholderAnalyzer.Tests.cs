using System.Collections.Immutable;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="PlaceholderAnalyzer"/>: a wording holds no placeholder of a format string, in an inline text
/// and in a text of a set, also a set of another assembly's contract.
/// </summary>
public sealed class PlaceholderAnalyzerTests
{
    #region Fixture
    private const string Header = """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl", "en-GB")]

        namespace TestApp;
        """;

    /// <summary>
    /// A library with a contract an app can add a language to.
    /// </summary>
    private static MetadataReference ShopLibrary { get; } = GeneratorTestHelper.CompileLibraryWithGenerators("PlaceholderShopLibrary", """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace ShopLibrary;

        public partial class ShopTranslationsEN : ITranslationSet
        {
            public virtual string Total => "Total";
        }

        public class ShopTranslationsNL : IShopTranslations
        {
            public virtual string Total => "Totaal";
        }
        """);

    private static string Inline(string arguments) => $$"""
        {{Header}}

        public static class Texts
        {
            private const string Wording = "Hallo, {0}!";

            public static LocalizableText Text(string name, int count) => LocalizableText.Inline({{arguments}});
        }
        """;

    /// <summary>
    /// A contract generated from an English set, with a Dutch set and a British set, each holding the given members.
    /// </summary>
    private static string Sets(string english, string dutch, string british = "") => $$"""
        {{Header}}

        public partial class ShopTranslationsEN : ITranslationSet
        {
            {{english}}
        }

        public class ShopTranslationsNL : IShopTranslations
        {
            private const string Wording = "Hallo, {0}!";

            {{dutch}}
        }

        public class ShopTranslationsEnGB : ShopTranslationsEN
        {
            {{british}}
        }
        """;

    private const string EnglishGreeting = """public virtual string Greeting => "Hello!";""";
    private const string DutchGreeting = """public string Greeting => "Hallo!";""";

    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source, params MetadataReference[] references)
    {
        return GeneratorTestHelper.AnalyzeAsync(new PlaceholderAnalyzer(), source, references);
    }

    private static string At(Diagnostic diagnostic)
    {
        return diagnostic.Location.SourceTree!.ToString()[diagnostic.Location.SourceSpan.Start..];
    }
    #endregion

    [Test]
    [Arguments("""en: "Hello!", nl: "Hallo, {0}!", enGB: "Hello!" """, "\"Hallo, {0}!\"", "The 'nl' wording holds the placeholder {0}, which nothing fills in")]
    [Arguments("""en: "Total", nl: "Totaal", enGB: "Total: {0:N2}" """, "\"Total: {0:N2}\"", "The 'en-GB' wording holds the placeholder {0:N2}, which nothing fills in")]
    [Arguments("""nl: "{1,8} van {0}", en: "Some", enGB: "Some" """, "\"{1,8} van {0}\"", "The 'nl' wording holds the placeholder {1,8}, which nothing fills in")]
    [Arguments("""en: $"{name}", nl: $"{name}: {{0}}", enGB: $"{name}" """, ": {{0}}\"", "The 'nl' wording holds the placeholder {0}, which nothing fills in")]
    [Arguments("""en: "Hello!", nl: Wording, enGB: "Hello!" """, "nl: Wording", "The 'nl' wording holds the placeholder {0}, which nothing fills in")]
    public async Task InlineWordingWithPlaceholder_ShouldReportLOC016(string arguments, string at, string message)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(Inline(arguments));

        // Assert: at the string that holds it, with or without an alignment or a format, also in the text of an
        // interpolated string, and at the argument for a constant it names.
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(PlaceholderAnalyzer.PlaceholderDiagnosticId);
        diagnostic.Id.ShouldBe("LOC016");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldStartWith(message);
        diagnostic.GetMessage().ShouldEndWith("Write the value into the wording of every language, as in '$\"Hello, {name}!\"'.");
        At(diagnostic).ShouldStartWith(at);
    }

    [Test]
    [Arguments("""en: $"Hello, {name}!", nl: $"Hallo, {name}!", enGB: $"Hello, {name}!" """)]
    [Arguments("""en: $"{count} items", nl: $"{count:N0} stuks", enGB: $"{count,8} items" """)]
    [Arguments("""en: $"{0} items", nl: $"{1 + 1} stuks", enGB: "Items" """)]
    [Arguments("""en: "Use {name}", nl: "Gebruik {}", enGB: "Use { 0 } or {x0}" """)]
    [Arguments("""en: "Braces { and }", nl: "Accolades }{", enGB: "{" """)]
    public async Task InlineWordingWithoutPlaceholder_ShouldReportNothing(string arguments)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(Inline(arguments));

        // Assert: a value of an interpolated string is no placeholder, also a number, and braces around anything but
        // a number are text.
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("""public virtual string Greeting => "Hello, {0}!";""", DutchGreeting, "", "\"Hello, {0}!\"", "'ShopTranslationsEN.Greeting'")]
    [Arguments(EnglishGreeting, """public string Greeting => "Hallo, {0}!";""", "", "\"Hallo, {0}!\"", "'ShopTranslationsNL.Greeting'")]
    [Arguments(EnglishGreeting, """public string Greeting { get { return "Hallo, {0}!"; } }""", "", "\"Hallo, {0}!\"", "'ShopTranslationsNL.Greeting'")]
    [Arguments(EnglishGreeting, """public string Greeting { get; } = "Hallo, {0}!";""", "", "\"Hallo, {0}!\"", "'ShopTranslationsNL.Greeting'")]
    [Arguments(EnglishGreeting, """string IShopTranslations.Greeting => "Hallo, {0}!";""", "", "\"Hallo, {0}!\"", "'ShopTranslationsNL.Greeting'")]
    [Arguments(EnglishGreeting, """public string Greeting => Wording;""", "", "Greeting => Wording", "'ShopTranslationsNL.Greeting'")]
    [Arguments(EnglishGreeting, DutchGreeting, """public override string Greeting => "Hello there, {0}!";""", "\"Hello there, {0}!\"", "'ShopTranslationsEnGB.Greeting'")]
    public async Task TextOfSetWithPlaceholder_ShouldReportLOC016(string english, string dutch, string british, string at, string subject)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(Sets(english, dutch, british));

        // Assert: in every language, the default one included, at the string that holds it, whether the property
        // implements the text, implements it explicitly, or overrides the default set's, and at the property for a
        // constant it names.
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC016");
        diagnostic.GetMessage().ShouldStartWith($"{subject} holds the placeholder {{0}}, which nothing fills in");
        diagnostic.GetMessage().ShouldEndWith("Write the text as a method that takes the value, as in 'public string Greeting(string name) => $\"Hello, {name}!\";'.");
        At(diagnostic).ShouldStartWith(at);
    }

    [Test]
    public async Task TextThatTakesValuesWithPlaceholder_ShouldReportLOC016AtEachString()
    {
        // Arrange & Act: a method that kept a placeholder next to the value it takes, in one of its wordings and in
        // the text of an interpolated string.
        var diagnostics = await AnalyzeAsync(Sets(
            """public virtual string Items(int count, string shop) => count is 1 ? $"1 item at {shop}" : $"{count} items at {shop}";""",
            """public string Items(int count, string shop) => count is 1 ? "1 artikel bij {1}" : $"{count} artikelen bij {{1}}";"""));

        // Assert
        diagnostics.Length.ShouldBe(2);
        diagnostics.ShouldAllBe(diagnostic => diagnostic.Id == "LOC016");
        diagnostics.ShouldAllBe(diagnostic => diagnostic.GetMessage().StartsWith("'ShopTranslationsNL.Items' holds the placeholder {1}", StringComparison.Ordinal));
        diagnostics.Select(At).ShouldContain(at => at.StartsWith("\"1 artikel bij {1}\"", StringComparison.Ordinal));
    }

    [Test]
    public async Task MemberThatIsNoText_ShouldReportNothing()
    {
        // Arrange & Act: a member marked as no text, a private one, the language code, a string in an attribute, and
        // in the Dutch set a helper the contract does not know.
        var diagnostics = await AnalyzeAsync(Sets(
            $$"""
            {{EnglishGreeting}}

            [ExcludeFromTranslationDiscovery]
            public string Pattern => "{0:N2}";

            [ExcludeFromTranslationDiscovery]
            public string Fill(string value) => string.Format("{0}!", value);

            private string Hidden => "{0}";

            [System.Obsolete("Use {0}")]
            public virtual string Old => "Old";
            """,
            $$"""
            {{DutchGreeting}}
            public string Old => "Oud";
            public string LanguageCode => "nl";
            public string Template => "{0}";
            public string Helper(string value) => string.Format("{0}?", value);
            """));

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task SetOfContractOfAnotherAssembly_ShouldReportLOC016()
    {
        // Arrange & Act: an app adds German to a library, in an assembly that declares no languages itself.
        var diagnostics = await AnalyzeAsync("""
            using ShopLibrary;

            namespace TestApp;

            public sealed class ShopTranslationsDE : IShopTranslations
            {
                public string Total => "Summe: {0}";
            }
            """, ShopLibrary);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC016");
        diagnostic.GetMessage().ShouldStartWith("'ShopTranslationsDE.Total' holds the placeholder {0}");
    }

    [Test]
    public async Task InlineWordingInMarkup_ShouldReportLOC016()
    {
        // Arrange & Act: what Razor generates from a page counts as generated code, and is the user's own markup.
        var diagnostics = await GeneratorTestHelper.AnalyzeAsync(new PlaceholderAnalyzer(), Header, [], ("Pages/Index_razor.g.cs", """
            // <auto-generated/>
            using CodeChops.Idioma;

            namespace TestApp.Pages;

            public static class Index
            {
                public static LocalizableText Title => LocalizableText.Inline(en: "Orders of {0}", nl: "Bestellingen", enGB: "Orders");
            }
            """));

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("LOC016");
    }
}
