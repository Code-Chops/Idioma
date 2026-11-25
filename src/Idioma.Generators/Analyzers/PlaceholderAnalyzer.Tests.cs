using System.Collections.Immutable;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="PlaceholderAnalyzer"/>: a wording holds no placeholder of a format string, in an inline text,
/// in a text on a member, and in a text of a set, also a set of another assembly's contract.
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

        public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
        {
            public virtual string Total => "Total";
        }

        public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
        {
            public virtual string Total => "Totaal";
        }
        """);

    private static string Inline(string arguments) => $$"""
        {{Header}}

        public static class Texts
        {
            private const string Wording = "Hallo, {0}!";

            public static string Text(string name, int count) => LocalizedText.Inline({{arguments}});
        }
        """;

    /// <summary>
    /// A contract generated from an English set, with a Dutch set and a British set, each holding the given members.
    /// </summary>
    private static string Sets(string english, string dutch, string british = "") => $$"""
        {{Header}}

        public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
        {
            {{english}}
        }

        public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
        {
            private const string Wording = "Hallo, {0}!";

            {{dutch}}
        }

        public class ShopLocalizationEnGb : ShopLocalizationEn
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
    [Arguments("""en: "Hello!", nl: "Hallo, {0}!", enGb: "Hello!" """, "\"Hallo, {0}!\"", "The 'nl' wording holds the placeholder {0}. Nothing fills it in")]
    [Arguments("""en: "Total", nl: "Totaal", enGb: "Total: {0:N2}" """, "\"Total: {0:N2}\"", "The 'en-GB' wording holds the placeholder {0:N2}. Nothing fills it in")]
    [Arguments("""nl: "{1,8} van {0}", en: "Some", enGb: "Some" """, "\"{1,8} van {0}\"", "The 'nl' wording holds the placeholder {1,8}. Nothing fills it in")]
    [Arguments("""en: $"{name}", nl: $"{name}: {{0}}", enGb: $"{name}" """, ": {{0}}\"", "The 'nl' wording holds the placeholder {0}. Nothing fills it in")]
    [Arguments("""en: "Hello!", nl: Wording, enGb: "Hello!" """, "nl: Wording", "The 'nl' wording holds the placeholder {0}. Nothing fills it in")]
    public async Task InlineWordingWithPlaceholder_ShouldReportLOC403(string arguments, string at, string message)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(Inline(arguments));

        // Assert: at the string that holds it, with or without an alignment or a format, also in the text of an
        // interpolated string, and at the argument for a constant it names.
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(PlaceholderAnalyzer.PlaceholderDiagnosticId);
        diagnostic.Id.ShouldBe("LOC403");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().ShouldStartWith(message);
        diagnostic.GetMessage().ShouldEndWith("Write the value into the wording of every language, as in '$\"Hello, {name}!\"'.");
        At(diagnostic).ShouldStartWith(at);
    }

    [Test]
    [Arguments("""en: "Enter {0}", nl: "Vul in", enGb: "Enter" """, "\"Enter {0}\"", "The 'en' wording holds the placeholder {0}. Nothing fills it in")]
    [Arguments("""en: "Total", nl: "Totaal", enGb: "Total: {0:N2}" """, "\"Total: {0:N2}\"", "The 'en-GB' wording holds the placeholder {0:N2}. Nothing fills it in")]
    [Arguments(""" "Name", "Naam {0}", "Name" """, "\"Naam {0}\"", "The 'nl' wording holds the placeholder {0}. Nothing fills it in")]
    [Arguments("""en: "Hello!", nl: Wording, enGb: "Hello!" """, "nl: Wording", "The 'nl' wording holds the placeholder {0}. Nothing fills it in")]
    public async Task WordingOfLocalizedWithPlaceholder_ShouldReportLOC403(string arguments, string at, string message)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync($$"""
            {{Header}}

            public class Address
            {
                private const string Wording = "Hallo, {0}!";

                [Localized({{arguments}})]
                public string City { get; } = "";
            }
            """);

        // Assert: at the string that holds it, also for an argument without a name, and at the argument for a
        // constant it names. A text on a member can't take the value, so the advice is a method of a set.
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC403");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().ShouldStartWith(message);
        diagnostic.GetMessage().ShouldEndWith("A text on a member can't take a value, so write a text with a value as a method of a set, as in 'public string Greeting(string name) => $\"Hello, {name}!\";'.");
        At(diagnostic).ShouldStartWith(at);
    }

    [Test]
    public async Task WordingOfLocalizedWithoutPlaceholder_ShouldReportNothing()
    {
        // Arrange & Act: braces that hold no placeholder.
        var diagnostics = await AnalyzeAsync($$"""
            {{Header}}

            public class Address
            {
                [Localized(en: "City {name}", nl: "Plaats {}", enGb: "City")]
                public string City { get; } = "";
            }
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("""en: $"Hello, {name}!", nl: $"Hallo, {name}!", enGb: $"Hello, {name}!" """)]
    [Arguments("""en: $"{count} items", nl: $"{count:N0} stuks", enGb: $"{count,8} items" """)]
    [Arguments("""en: $"{0} items", nl: $"{1 + 1} stuks", enGb: "Items" """)]
    [Arguments("""en: "Use {name}", nl: "Gebruik {}", enGb: "Use { 0 } or {x0}" """)]
    [Arguments("""en: "Braces { and }", nl: "Accolades }{", enGb: "{" """)]
    [Arguments("""en: string.Format("Hello, {0}!", name), nl: string.Format("Hallo, {0}!", name), enGb: $"Total: {string.Format("{0:N2}", count)}" """)]
    public async Task InlineWordingWithoutPlaceholder_ShouldReportNothing(string arguments)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(Inline(arguments));

        // Assert: a value of an interpolated string is no placeholder, also a number, and braces around anything but
        // a number are text. A string passed to a method is no wording, because the method may fill the placeholder in.
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("""public virtual string Greeting => "Hello, {0}!";""", DutchGreeting, "", "\"Hello, {0}!\"", "'ShopLocalizationEn.Greeting'")]
    [Arguments(EnglishGreeting, """public string Greeting => "Hallo, {0}!";""", "", "\"Hallo, {0}!\"", "'ShopLocalizationNl.Greeting'")]
    [Arguments(EnglishGreeting, """public string Greeting { get { return "Hallo, {0}!"; } }""", "", "\"Hallo, {0}!\"", "'ShopLocalizationNl.Greeting'")]
    [Arguments(EnglishGreeting, """public string Greeting { get; } = "Hallo, {0}!";""", "", "\"Hallo, {0}!\"", "'ShopLocalizationNl.Greeting'")]
    [Arguments(EnglishGreeting, """string IShopLocalization.Greeting => "Hallo, {0}!";""", "", "\"Hallo, {0}!\"", "'ShopLocalizationNl.Greeting'")]
    [Arguments(EnglishGreeting, """public string Greeting => Wording;""", "", "Greeting => Wording", "'ShopLocalizationNl.Greeting'")]
    [Arguments(EnglishGreeting, DutchGreeting, """public override string Greeting => "Hello there, {0}!";""", "\"Hello there, {0}!\"", "'ShopLocalizationEnGb.Greeting'")]
    [Arguments("""public virtual string Greeting(string name) => $"Hello, {name}!";""", """public string Greeting => "Hallo, {0}!";""", "", "\"Hallo, {0}!\"", "'ShopLocalizationNl.Greeting'")]
    public async Task TextOfSetWithPlaceholder_ShouldReportLOC403(string english, string dutch, string british, string at, string subject)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(Sets(english, dutch, british));

        // Assert: in every language, the default one included, at the string that holds it, whether the property
        // implements the text, implements it explicitly, or overrides the default set's, and at the property for a
        // constant it names. Also a property where the text takes a value, as the default language wrote it before.
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC403");
        diagnostic.GetMessage().ShouldStartWith($"{subject} holds the placeholder {{0}}. Nothing fills it in");
        diagnostic.GetMessage().ShouldEndWith("Write the text as a method that takes the value, as in 'public string Greeting(string name) => $\"Hello, {name}!\";'.");
        At(diagnostic).ShouldStartWith(at);
    }

    [Test]
    public async Task TextThatTakesValuesWithPlaceholder_ShouldReportLOC403AtEachString()
    {
        // Arrange & Act: a method that kept a placeholder next to the value it takes, in one of its wordings and in
        // the text of an interpolated string.
        var diagnostics = await AnalyzeAsync(Sets(
            """public virtual string Items(int count, string shop) => count is 1 ? $"1 item at {shop}" : $"{count} items at {shop}";""",
            """public string Items(int count, string shop) => count is 1 ? "1 artikel bij {1}" : $"{count} artikelen bij {{1}}";"""));

        // Assert
        diagnostics.Length.ShouldBe(2);
        diagnostics.ShouldAllBe(diagnostic => diagnostic.Id == "LOC403");
        diagnostics.ShouldAllBe(diagnostic => diagnostic.GetMessage().StartsWith("'ShopLocalizationNl.Items' holds the placeholder {1}", StringComparison.Ordinal));
        diagnostics.Select(At).ShouldContain(at => at.StartsWith("\"1 artikel bij {1}\"", StringComparison.Ordinal));
    }

    [Test]
    [Arguments(
        """public virtual string Items(int count) => string.Format("{0} items", count);""",
        """public string Items(int count) => string.Format("{0} artikelen", count);""")]
    [Arguments(
        EnglishGreeting,
        """public string Greeting => string.Format("Hallo, {0}!", "jij");""")]
    [Arguments(
        """public virtual string Items(int count) => $"{count} items";""",
        """
        public string Items(int count)
        {
            var format = count is 1 ? "{0} artikel" : "{0} artikelen";
            return string.Format(format, count);
        }
        """)]
    [Arguments(
        """public virtual string Items(int count) => $"{count} items";""",
        """
        public string Items(int count)
        {
            string Format() { return "{0} artikelen"; }
            return string.Format(Format(), count);
        }
        """)]
    [Arguments(
        EnglishGreeting,
        """
        public string Greeting
        {
            get
            {
                var greeting = "Hallo, {0}!";
                return greeting;
            }
        }
        """)]
    public async Task StringTheTextDoesNotReturnAsWritten_ShouldReportNothing(string english, string dutch)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(Sets(english, dutch));

        // Assert: a string passed to a method isn't the wording, because the method may fill the placeholder in, as
        // string.Format does. Neither is a format kept in a local variable, or one a local function returns. The build
        // doesn't follow a local variable, so it also misses the wording the last text returns through one.
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task TextWithBlockBody_ShouldReportLOC403AtEachStringItReturns()
    {
        // Arrange & Act: two returns, the second a concatenation, next to a string passed to a method.
        var diagnostics = await AnalyzeAsync(Sets(
            """public virtual string Items(int count, string shop) => $"{count} items at {shop}";""",
            """
            public string Items(int count, string shop)
            {
                if (count is 1)
                    return "1 artikel bij {1}";

                return string.Format("{0} artikelen", count) + " bij {1}";
            }
            """));

        // Assert: at the strings the text returns, and not at the one string.Format fills in.
        diagnostics.Length.ShouldBe(2);
        diagnostics.ShouldAllBe(diagnostic => diagnostic.GetMessage().StartsWith("'ShopLocalizationNl.Items' holds the placeholder {1}", StringComparison.Ordinal));
        diagnostics.Select(At).ShouldContain(at => at.StartsWith("\"1 artikel bij {1}\"", StringComparison.Ordinal));
        diagnostics.Select(At).ShouldContain(at => at.StartsWith("\" bij {1}\"", StringComparison.Ordinal));
    }

    [Test]
    public async Task MemberThatIsNoText_ShouldReportNothing()
    {
        // Arrange & Act: a member marked as no text, a private one, the language code, a string in an attribute, and
        // in the Dutch set a helper the contract does not know.
        var diagnostics = await AnalyzeAsync(Sets(
            $$"""
            {{EnglishGreeting}}

            [ExcludeFromLocalizationDiscovery]
            public string Pattern => "{0:N2}";

            [ExcludeFromLocalizationDiscovery]
            public string Fill(string value) => string.Format("{0}!", value);

            private string Hidden => "{0}";

            [System.Obsolete("Use {0}")]
            public virtual string Old => "Old";
            """,
            $$"""
            {{DutchGreeting}}
            public string Old => "Oud";
            public string LocalizationLanguageCode => "nl";
            public string Template => "{0}";
            public string Helper(string value) => string.Format("{0}?", value);
            """));

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task SetOfContractOfAnotherAssembly_ShouldReportLOC403()
    {
        // Arrange & Act: an app adds German to a library, in an assembly that declares no languages itself.
        var diagnostics = await AnalyzeAsync("""
            using ShopLibrary;

            namespace TestApp;

            public sealed class ShopLocalizationDe : CodeChops.Idioma.ILocalizationSet, IShopLocalization
            {
                public string Total => "Summe: {0}";
            }
            """, ShopLibrary);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC403");
        diagnostic.GetMessage().ShouldStartWith("'ShopLocalizationDe.Total' holds the placeholder {0}");
    }

    [Test]
    public async Task InlineWordingInMarkup_ShouldReportLOC403()
    {
        // Arrange & Act: what Razor generates from a page counts as generated code, and is the user's own markup.
        var diagnostics = await GeneratorTestHelper.AnalyzeAsync(new PlaceholderAnalyzer(), Header, [], ("Pages/Index_razor.g.cs", """
            // <auto-generated/>
            using CodeChops.Idioma;

            namespace TestApp.Pages;

            public static class Index
            {
                public static string Title => LocalizedText.Inline(en: "Orders of {0}", nl: "Bestellingen", enGb: "Orders");
            }
            """));

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("LOC403");
    }

    [Test]
    public async Task PartialPropertyWithPlaceholder_ShouldReportLOC403()
    {
        // Arrange & Act: the set names the part that declares the property as the text's implementation, and the part
        // that implements it holds the wording.
        var diagnostics = await AnalyzeAsync($$"""
            {{Header}}

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Note => "See the terms";
            }

            public partial class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public partial string Note { get; }
                public partial string Note { get => "Zie {0}"; }
            }

            public class ShopLocalizationEnGb : ShopLocalizationEn;
            """);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC403");
        diagnostic.GetMessage().ShouldStartWith("'ShopLocalizationNl.Note' holds the placeholder {0}");
    }
}
