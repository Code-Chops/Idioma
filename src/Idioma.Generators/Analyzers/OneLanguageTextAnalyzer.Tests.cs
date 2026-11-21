using System.Collections.Immutable;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="OneLanguageTextAnalyzer"/>: in an assembly that declares languages, wording written in the source
/// can't become a text in one language, what only generated code should call can't be called by hand, and a language
/// argument must be named. A runtime value converts as it always did.
/// </summary>
public sealed class OneLanguageTextAnalyzerTests
{
    #region Fixture
    private const string Header = """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace TestApp;
        """;

    private static string Member(string member) => $$"""
        {{Header}}

        public sealed record Customer(string Name, string City);

        public static class Texts
        {
            private static void Take(LocalizableText text) { }

            {{member}}
        }
        """;

    private static Task<ImmutableArray<Diagnostic>> AnalyzeMemberAsync(string member) => GeneratorTestHelper.AnalyzeAsync(Member(member));
    #endregion

    [Test]
    [Arguments("""public static LocalizableText Bad { get; } = "Orders";""")]
    [Arguments("""private const string Title = "Orders"; public static LocalizableText Bad { get; } = Title;""")]
    [Arguments("""public static LocalizableText Bad(int number) => $"Order {number}";""")]
    [Arguments("""public static LocalizableText Bad(int number) => "Order " + number;""")]
    [Arguments("""public static LocalizableText Bad(bool flag) => flag ? "Yes" : "No";""")]
    [Arguments("""public static LocalizableText Bad(bool flag, string value) => flag ? value : "No";""")]
    [Arguments("""public static LocalizableText Bad(string? name) => name ?? "Unknown";""")]
    [Arguments("""public static LocalizableText Bad(LocalizableText? text) => text ?? "Unknown";""")]
    [Arguments("""public static LocalizableText Bad(int number) => number switch { 1 => "One", _ => "Other" };""")]
    [Arguments("""public static void Bad() => Take("Orders");""")]
    [Arguments("""public static LocalizableText Bad { get; } = new LocalizableText("Orders");""")]
    [Arguments("""public static LocalizableText Bad { get; } = LocalizableText.Keyed("Shop.Orders", "Orders");""")]
    [Arguments("""public static LocalizableText Bad { get; } = (LocalizableText)"Orders";""")]
    [Arguments("""public static LocalizableText Bad(LocalizableText text) { text += " (draft)"; return text; }""")]
    [Arguments("""public static LocalizableText Bad { get; } = "Idioma";""")]                     // A name
    [Arguments("""public static LocalizableText Bad(double distance) => $"{distance} km";""")]    // A unit
    public async Task WordingInOneLanguage_ShouldReportLOC500(string member)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeMemberAsync(member);

        // Assert: the message names the languages and writes out the fix.
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(OneLanguageTextAnalyzer.OneLanguageDiagnosticId);
        diagnostic.Id.ShouldBe("LOC500");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("en, nl");
        diagnostic.GetMessage().ShouldContain("LocalizableText.Inline(en: \"...\", nl: \"...\")");
    }

    [Test]
    [Arguments("public static LocalizableText Good(string customerName) => customerName;")]
    [Arguments("public static LocalizableText Good(LocalizableText text, string customerName) { text += customerName; return text; }")]
    [Arguments("public static LocalizableText Good(Customer customer) => customer.Name;")]
    [Arguments("public static LocalizableText Good(Customer customer) => customer.ToString();")]
    [Arguments("""public static LocalizableText Good(Customer customer) => $"{customer.Name} - {customer.City}";""")]
    [Arguments("public static LocalizableText Good(Customer customer, bool flag) => flag ? customer.Name : customer.City;")]
    [Arguments("public static LocalizableText Good(Customer customer) => new LocalizableText(customer.Name);")]
    [Arguments("""public static LocalizableText Good(Customer customer) => LocalizableText.Keyed("Customer.Name", customer.Name);""")]
    [Arguments("""public static LocalizableText Dash { get; } = "-";""")]
    [Arguments("""public static LocalizableText Number { get; } = "1234";""")]
    [Arguments("""public static LocalizableText Empty { get; } = "";""")]
    [Arguments("public static LocalizableText? Nothing { get; } = null;")]
    [Arguments("""public static LocalizableText Good { get; } = LocalizableText.Inline(en: "Orders", nl: "Bestellingen");""")]
    public async Task ValueOrInlineText_ShouldReportNothing(string member)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeMemberAsync(member);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("""public static string Wording() => "Orders"; public static LocalizableText Hidden() => Wording();""")]
    [Arguments("""public static LocalizableText Hidden() { var wording = "Orders"; return wording; }""")]
    [Arguments("""private static readonly string Wording = "Orders"; public static LocalizableText Hidden { get; } = Wording;""")]
    [Arguments("""private static string Wording => "Orders"; public static LocalizableText Hidden { get; } = Wording;""")]
    public async Task WordingKeptElsewhere_ShouldPass(string member)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeMemberAsync(member);

        // Assert: pins the known limit. To the build this is a value, like a customer's name.
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("""[ExcludeFromLocalizationDiscovery] public static LocalizableText Brand { get; } = "Idioma";""")]
    [Arguments("""[ExcludeFromLocalizationDiscovery] public static LocalizableText Distance { get; } = $"{1.5} km";""")]
    [Arguments("""[ExcludeFromLocalizationDiscovery] public static LocalizableText Brand { get; } = new LocalizableText("Idioma");""")]
    [Arguments("""[ExcludeFromLocalizationDiscovery] public static LocalizableText Brand { get; } = LocalizableText.Keyed("Shop.Brand", "Idioma");""")]
    [Arguments("""[ExcludeFromLocalizationDiscovery] public static LocalizableText Brand => "Idioma";""")]
    [Arguments("""[ExcludeFromLocalizationDiscovery] public static LocalizableText Brand { get => "Idioma"; }""")]
    [Arguments("""[ExcludeFromLocalizationDiscovery] public static LocalizableText Brand { get { return "Idioma"; } }""")]
    public async Task WordingOfExcludedProperty_ShouldReportNothing(string member)
    {
        // Arrange & Act: wording its author marked as the same in every language, such as a name or a unit, in the
        // initializer or the getter.
        var diagnostics = await AnalyzeMemberAsync(member);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task WordingTheSetterOfExcludedPropertyStores_ShouldReportLOC500()
    {
        // Arrange & Act: the setter runs for each caller, as an assignment elsewhere does.
        var diagnostics = await AnalyzeMemberAsync("""
            [ExcludeFromLocalizationDiscovery] public static LocalizableText Title { get => field; set => field = value ?? "Orders"; } = "Idioma";
            """);

        // Assert: in the setter, not in the initializer.
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(OneLanguageTextAnalyzer.OneLanguageDiagnosticId);
        diagnostic.Location.SourceTree!.ToString()[diagnostic.Location.SourceSpan.Start..].ShouldStartWith("\"Orders\"; }");
    }

    [Test]
    public async Task WordingAssignedToExcludedProperty_ShouldReportLOC500()
    {
        // Arrange & Act: the attribute covers the property's own initializer and getter alone. So a marked component
        // parameter still holds the wording of each caller to every language.
        var diagnostics = await AnalyzeMemberAsync("""
            [ExcludeFromLocalizationDiscovery] public static LocalizableText Title { get; set; } = "Idioma";
            public static void Rename() => Title = "Orders";
            """);

        // Assert: on the assignment, not on the initializer.
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(OneLanguageTextAnalyzer.OneLanguageDiagnosticId);
        diagnostic.Location.SourceTree!.ToString()[diagnostic.Location.SourceSpan.Start..].ShouldStartWith("\"Orders\"");
    }

    [Test]
    public async Task TextComparedToString_ShouldReportNothing()
    {
        // Arrange & Act: the span operator converts nothing to a text.
        var diagnostics = await AnalyzeMemberAsync("""public static bool Same(LocalizableText text) => text == "Orders";""");

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task EqualsWithWording_ShouldNotBeReported()
    {
        // Arrange & Act: Equals compares the wording, as == does, and converts nothing to a text.
        var diagnostics = await AnalyzeMemberAsync("""public static bool Same(LocalizableText text) => text.Equals("Orders");""");

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task WordingLookedUpInCollection_ShouldReportLOC500()
    {
        // Arrange & Act: the lookup takes a text, so the wording becomes a text in one language.
        var diagnostics = await AnalyzeMemberAsync("""public static bool Found(System.Collections.Generic.List<LocalizableText> texts) => texts.Contains("Orders");""");

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe(OneLanguageTextAnalyzer.OneLanguageDiagnosticId);
    }

    [Test]
    [Arguments("""LocalizableText.Inline("Orders", "Bestellingen")""")]
    [Arguments("""LocalizableText.Inline("Orders", nl: "Bestellingen")""")]
    public async Task UnnamedLanguage_ShouldReportLOC501(string text)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeMemberAsync($"public static LocalizableText Bad {{ get; }} = {text};");

        // Assert: once per call, however many arguments miss their name.
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(OneLanguageTextAnalyzer.UnnamedLanguageDiagnosticId);
        diagnostic.Id.ShouldBe("LOC501");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
    }

    [Test]
    [Arguments("""[Localized("City", "Plaats")]""", "\"City\"")]
    [Arguments("""[Localized("City", nl: "Plaats")]""", "\"City\"")]
    [Arguments("""[Localized(en: "City", "Plaats")]""", "\"Plaats\"")]
    public async Task UnnamedLanguageOfLocalized_ShouldReportLOC501(string attribute, string at)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeMemberAsync($"{attribute} public static string City {{ get; }} = \"\";");

        // Assert: once per attribute, at the first argument without a name, with the language of each argument for
        // the fix.
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC501");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldBe("Name the language of this wording: [Localized(en: \"...\", nl: \"...\")]. Without names, two languages can swap unnoticed.");
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).ShouldBe(at);
        diagnostic.Properties[OneLanguageTextAnalyzer.ParameterNamesProperty].ShouldBe("en,nl");
    }

    [Test]
    [Arguments("""[Localized(en: "City", nl: "Plaats")]""")]
    [Arguments("""[Localized(nl: "Plaats", en: "City")]""")]
    [Arguments("""[LocalizedAttribute(en: "City", nl: "Plaats")]""")]
    [Arguments("""[global::Localized(en: "City", nl: "Plaats")]""")]
    public async Task NamedLanguagesOfLocalizedInAnyOrder_ShouldReportNothing(string attribute)
    {
        // Arrange & Act: also no wording in one language (LOC500), because the attribute holds every language.
        var diagnostics = await AnalyzeMemberAsync($"{attribute} public static string City {{ get; }} = \"\";");

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task UnnamedArgumentOfAnAttributeTheCodeBringsItself_ShouldReportNothing()
    {
        // Arrange & Act: an attribute named Localized in the code's own namespace, which is not the generated one.
        var diagnostics = await GeneratorTestHelper.AnalyzeAsync($$"""
            {{Header}}

            public sealed class LocalizedAttribute(string en, string nl) : System.Attribute
            {
                public string En { get; } = en;
                public string Nl { get; } = nl;
            }

            public class Address
            {
                [Localized("City", "Plaats")]
                public string City { get; } = "";
            }
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task NamedLanguagesInAnyOrder_ShouldReportNothing()
    {
        // Arrange & Act
        var diagnostics = await AnalyzeMemberAsync("""public static LocalizableText Good { get; } = LocalizableText.Inline(nl: "Bestellingen", en: "Orders");""");

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task SetsAndGeneratedContract_ShouldReportNothing()
    {
        // Arrange & Act: the generated class calls GeneratedCode.CreateSetText, which is what that is for.
        var diagnostics = await GeneratorTestHelper.AnalyzeAsync($$"""
            {{Header}}

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("Page_razor.g.cs")]
    [Arguments("Pages/Index_cshtml.g.cs")]
    public async Task WordingInRazorOutput_ShouldReport(string outputPath)
    {
        // Arrange: what Razor writes for <Comp Caption="@("Orders")" /> in a component or a page: generated code, but
        // the user's own markup.
        const string razorOutput = """
            // <auto-generated/>
            namespace TestApp;

            internal static class PageRender
            {
                public static void Render() => Take("Orders");
                private static void Take(CodeChops.Idioma.LocalizableText text) { }
            }
            """;

        // Act
        var diagnostics = await GeneratorTestHelper.AnalyzeAsync(Member(""), (outputPath, razorOutput));

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("LOC500");
    }

    [Test]
    public async Task WordingInOtherGeneratedCode_ShouldReportNothing()
    {
        // Arrange: whoever generated this is the only one who could fix it.
        const string generated = """
            // <auto-generated/>
            namespace TestApp;

            internal static class Generated
            {
                public static void Run() => Take("Orders");
                public static void Call() => CodeChops.Idioma.Internals.GeneratedCode.CreateText("Orders");
                private static void Take(CodeChops.Idioma.LocalizableText text) { }
            }
            """;

        // Act
        var diagnostics = await GeneratorTestHelper.AnalyzeAsync(Member(""), ("Other.g.cs", generated));

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task NoAttribute_ShouldReportNothing()
    {
        // Arrange & Act: an assembly without languages has nothing to translate.
        var diagnostics = await GeneratorTestHelper.AnalyzeAsync("""
            using CodeChops.Idioma;

            namespace TestApp;

            public static class Texts
            {
                public static LocalizableText Title { get; } = "Orders";
                public static LocalizableText Other { get; } = CodeChops.Idioma.Internals.GeneratedCode.CreateText("Orders", ("nl", "Bestellingen"));
            }
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task OneLanguage_ShouldAllowWording()
    {
        // Arrange & Act: there is nothing to translate it into.
        var diagnostics = await GeneratorTestHelper.AnalyzeAsync("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public static class Texts
            {
                public static LocalizableText Title { get; } = "Orders";
            }
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task InlineText_InAssemblyWithoutLanguages_ShouldReportLOC100()
    {
        // Arrange & Act: without [assembly: SupportedLanguages] the generator writes no LocalizableText.Inline, and the
        // compiler only says that the method doesn't exist (CS0117).
        var diagnostics = await GeneratorTestHelper.AnalyzeAsync("""
            using CodeChops.Idioma;

            namespace TestApp;

            public static class Texts
            {
                public static LocalizableText Title => LocalizableText.Inline(en: "Orders", nl: "Bestellingen");
            }
            """);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC100");
        diagnostic.GetMessage().ShouldBe("LocalizableText.Inline takes a wording per language, but this assembly declares no languages. Add [assembly: SupportedLanguages(\"en\", \"nl\")] with your languages, the default first.");
    }

    [Test]
    public async Task InlineOfAnotherType_InAssemblyWithoutLanguages_ShouldNotBeReported()
    {
        // Arrange & Act
        var diagnostics = await GeneratorTestHelper.AnalyzeAsync("""
            namespace TestApp;

            public static class Markup
            {
                public static string Inline(string text) => text;
            }

            public static class Texts
            {
                public static string Title => Markup.Inline("Orders");
            }
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }
}
