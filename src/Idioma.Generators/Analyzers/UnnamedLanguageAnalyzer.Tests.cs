using System.Collections.Immutable;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="UnnamedLanguageAnalyzer"/>: in an assembly that declares languages, a language argument must
/// be named (<c>LOC500</c>). In an assembly without languages, a call of <c>LocalizedText.Inline</c> is reported
/// (<c>LOC100</c>). A plain string is never reported, because a text is a string.
/// </summary>
public sealed class UnnamedLanguageAnalyzerTests
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
            private static void Take(string text) { }

            {{member}}
        }
        """;

    private static Task<ImmutableArray<Diagnostic>> AnalyzeMemberAsync(string member) => GeneratorTestHelper.AnalyzeAsync(Member(member));
    #endregion

    [Test]
    [Arguments("""public static string Title { get; } = "Orders";""")]
    [Arguments("""public static string Title(int number) => $"Order {number}";""")]
    [Arguments("""public static string Title(bool flag) => flag ? "Yes" : "No";""")]
    [Arguments("""public static void Title() => Take("Orders");""")]
    [Arguments("public static string Title(Customer customer) => customer.Name;")]
    [Arguments("""public static string Title => LocalizedText.Inline(en: "Orders", nl: "Bestellingen");""")]
    public async Task StringOrInlineText_ShouldReportNothing(string member)
    {
        // Arrange & Act: a text is a string, so the build can't tell wording in one language from any other string.
        var diagnostics = await AnalyzeMemberAsync(member);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("""LocalizedText.Inline("Orders", "Bestellingen")""")]
    [Arguments("""LocalizedText.Inline("Orders", nl: "Bestellingen")""")]
    public async Task UnnamedLanguage_ShouldReportLOC500(string text)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeMemberAsync($"public static string Bad => {text};");

        // Assert: once per call, however many arguments miss their name, with the language of each argument for the
        // fix.
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(UnnamedLanguageAnalyzer.UnnamedLanguageDiagnosticId);
        diagnostic.Id.ShouldBe("LOC500");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldBe("Name the language of this wording: LocalizedText.Inline(en: \"...\", nl: \"...\"). Without names, two languages can swap unnoticed.");
        diagnostic.Properties[UnnamedLanguageAnalyzer.ParameterNamesProperty].ShouldBe("en,nl");
    }

    [Test]
    [Arguments("""[Localized("City", "Plaats")]""", "\"City\"")]
    [Arguments("""[Localized("City", nl: "Plaats")]""", "\"City\"")]
    [Arguments("""[Localized(en: "City", "Plaats")]""", "\"Plaats\"")]
    public async Task UnnamedLanguageOfLocalized_ShouldReportLOC500(string attribute, string at)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeMemberAsync($"{attribute} public static string City {{ get; }} = \"\";");

        // Assert: once per attribute, at the first argument without a name, with the language of each argument for
        // the fix.
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC500");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldBe("Name the language of this wording: [Localized(en: \"...\", nl: \"...\")]. Without names, two languages can swap unnoticed.");
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).ShouldBe(at);
        diagnostic.Properties[UnnamedLanguageAnalyzer.ParameterNamesProperty].ShouldBe("en,nl");
    }

    [Test]
    [Arguments("""[Localized(en: "City", nl: "Plaats")]""")]
    [Arguments("""[Localized(nl: "Plaats", en: "City")]""")]
    [Arguments("""[LocalizedAttribute(en: "City", nl: "Plaats")]""")]
    [Arguments("""[global::Localized(en: "City", nl: "Plaats")]""")]
    public async Task NamedLanguagesOfLocalizedInAnyOrder_ShouldReportNothing(string attribute)
    {
        // Arrange & Act
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
        var diagnostics = await AnalyzeMemberAsync("""public static string Good => LocalizedText.Inline(nl: "Bestellingen", en: "Orders");""");

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task SetsAndGeneratedContract_ShouldReportNothing()
    {
        // Arrange & Act: the generated class calls GeneratedCode.ResolveWording, which is what that is for.
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
    public async Task UnnamedLanguageInRazorOutput_ShouldReportLOC500(string outputPath)
    {
        // Arrange: what Razor writes for <Comp Caption="@LocalizedText.Inline("Orders", "Bestellingen")" /> in a
        // component or a page: generated code, but the user's own markup.
        const string razorOutput = """
            // <auto-generated/>
            namespace TestApp;

            internal static class PageRender
            {
                public static void Render() => Take(CodeChops.Idioma.LocalizedText.Inline("Orders", "Bestellingen"));
                private static void Take(string text) { }
            }
            """;

        // Act
        var diagnostics = await GeneratorTestHelper.AnalyzeAsync(Member(""), (outputPath, razorOutput));

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("LOC500");
    }

    [Test]
    public async Task UnnamedLanguageInOtherGeneratedCode_ShouldReportNothing()
    {
        // Arrange: whoever generated this is the only one who could fix it.
        const string generated = """
            // <auto-generated/>
            namespace TestApp;

            internal static class Generated
            {
                public static void Run() => Take(CodeChops.Idioma.LocalizedText.Inline("Orders", "Bestellingen"));
                private static void Take(string text) { }
            }
            """;

        // Act
        var diagnostics = await GeneratorTestHelper.AnalyzeAsync(Member(""), ("Other.g.cs", generated));

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task StringInAssemblyWithoutLanguages_ShouldReportNothing()
    {
        // Arrange & Act: an assembly without languages has nothing to translate.
        var diagnostics = await GeneratorTestHelper.AnalyzeAsync("""
            namespace TestApp;

            public static class Texts
            {
                public static string Title { get; } = "Orders";
            }
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task InlineText_InAssemblyWithoutLanguages_ShouldReportLOC100()
    {
        // Arrange & Act: without [assembly: SupportedLanguages] the generator writes no LocalizedText.Inline, and the
        // compiler only says that the method doesn't exist (CS0117).
        var diagnostics = await GeneratorTestHelper.AnalyzeAsync("""
            using CodeChops.Idioma;

            namespace TestApp;

            public static class Texts
            {
                public static string Title => LocalizedText.Inline(en: "Orders", nl: "Bestellingen");
            }
            """);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC100");
        diagnostic.GetMessage().ShouldBe("LocalizedText.Inline takes a wording per language, but this assembly declares no languages. Add [assembly: SupportedLanguages(\"en\", \"nl\")] with your languages, the default first.");
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
