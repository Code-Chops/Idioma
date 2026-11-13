using System.Collections.Immutable;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="OneLanguageTextAnalyzer"/>: in an assembly that declares languages, wording written in the source
/// cannot become a text in one language, what only generated code should call cannot be called by hand, and a language
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
    [Arguments("""public static LocalizableText Bad { get; } = new ResourceText("Shop.Orders", "Orders");""")]
    [Arguments("""public static LocalizableText Bad { get; } = (LocalizableText)"Orders";""")]
    public async Task WordingInOneLanguage_ShouldReportLOC011(string member)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeMemberAsync(member);

        // Assert: the message names the languages and writes out the fix.
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(OneLanguageTextAnalyzer.OneLanguageDiagnosticId);
        diagnostic.Id.ShouldBe("LOC011");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("en, nl");
        diagnostic.GetMessage().ShouldContain("LocalizableText.Translated(en: \"...\", nl: \"...\")");
    }

    [Test]
    [Arguments("""public static LocalizableText Good(string customerName) => customerName;""")]
    [Arguments("""public static LocalizableText Good(Customer customer) => customer.Name;""")]
    [Arguments("""public static LocalizableText Good(Customer customer) => customer.ToString();""")]
    [Arguments("""public static LocalizableText Good(Customer customer) => $"{customer.Name} - {customer.City}";""")]
    [Arguments("""public static LocalizableText Good(Customer customer, bool flag) => flag ? customer.Name : customer.City;""")]
    [Arguments("""public static LocalizableText Good(Customer customer) => new LocalizableText(customer.Name);""")]
    [Arguments("""public static LocalizableText Good(Customer customer) => new ResourceText("Customer.Name", customer.Name);""")]
    [Arguments("""public static LocalizableText Dash { get; } = "-";""")]
    [Arguments("""public static LocalizableText Number { get; } = "1234";""")]
    [Arguments("""public static LocalizableText Empty { get; } = "";""")]
    [Arguments("""public static LocalizableText? Nothing { get; } = null;""")]
    [Arguments("""public static LocalizableText Good { get; } = LocalizableText.Translated(en: "Orders", nl: "Bestellingen");""")]
    public async Task ValueOrTranslatedText_ShouldReportNothing(string member)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeMemberAsync(member);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("""public static string Wording() => "Orders"; public static LocalizableText Hidden() => Wording();""")]
    [Arguments("""public static LocalizableText Hidden() { var wording = "Orders"; return wording; }""")]
    public async Task WordingBehindVariableOrMethod_ShouldPass(string member)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeMemberAsync(member);

        // Assert: pins the known limit. To the build this is a value, like a customer's name.
        diagnostics.ShouldBeEmpty();
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
    [Arguments("""public static LocalizableText Bad { get; } = CodeChops.Idioma.Internals.GeneratedCode.CreateText("Orders");""", "GeneratedCode.CreateText")]
    [Arguments("""public static LocalizableText Bad(string value) => CodeChops.Idioma.Internals.GeneratedCode.CreateResourceText<ITranslations>("Shop.Orders", static translations => translations.LanguageCode, null!);""", "GeneratedCode.CreateResourceText")]
    public async Task CalledByHand_ShouldReportLOC012(string member, string expected)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeMemberAsync(member);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(OneLanguageTextAnalyzer.GeneratedCodeOnlyDiagnosticId);
        diagnostic.Id.ShouldBe("LOC012");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain($"'{expected}'");
    }

    [Test]
    [Arguments("""LocalizableText.Translated("Orders", "Bestellingen")""")]
    [Arguments("""LocalizableText.Translated("Orders", nl: "Bestellingen")""")]
    public async Task UnnamedLanguage_ShouldReportLOC013(string text)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeMemberAsync($"public static LocalizableText Bad {{ get; }} = {text};");

        // Assert: once per call, however many arguments miss their name.
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(OneLanguageTextAnalyzer.UnnamedLanguageDiagnosticId);
        diagnostic.Id.ShouldBe("LOC013");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
    }

    [Test]
    public async Task NamedLanguagesInAnyOrder_ShouldReportNothing()
    {
        // Arrange & Act
        var diagnostics = await AnalyzeMemberAsync("""public static LocalizableText Good { get; } = LocalizableText.Translated(nl: "Bestellingen", en: "Orders");""");

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task SetsAndGeneratedContract_ShouldReportNothing()
    {
        // Arrange & Act: the generated class calls GeneratedCode.CreateResourceText, which is what that is for.
        var diagnostics = await GeneratorTestHelper.AnalyzeAsync($$"""
            {{Header}}

            public partial class ShopTranslationsEN : ITranslations
            {
                public string Checkout => "Checkout";
            }

            public sealed class ShopTranslationsNL : IShopTranslations
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
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("LOC011");
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
}
