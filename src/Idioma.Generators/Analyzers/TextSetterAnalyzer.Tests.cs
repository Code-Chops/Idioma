using System.Collections.Immutable;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="TextSetterAnalyzer"/>: a text of a set has no setter, whatever the accessibility of the setter,
/// also in a set of another assembly's contract.
/// </summary>
public sealed class TextSetterAnalyzerTests
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
    private static MetadataReference ShopLibrary { get; } = GeneratorTestHelper.CompileLibraryWithGenerators("SetterShopLibrary", """
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
        return GeneratorTestHelper.AnalyzeAsync(new TextSetterAnalyzer(), source, references);
    }

    private static string At(Diagnostic diagnostic)
    {
        return diagnostic.Location.SourceTree!.ToString()[diagnostic.Location.SourceSpan.Start..];
    }
    #endregion

    [Test]
    [Arguments(EnglishGreeting, """public string Greeting { get; set; } = "Hallo!";""", "ShopLocalizationNl")]
    [Arguments(EnglishGreeting, """public string Greeting { get; internal set; } = "Hallo!";""", "ShopLocalizationNl")]
    [Arguments(EnglishGreeting, """public string Greeting { get; protected set; } = "Hallo!";""", "ShopLocalizationNl")]
    [Arguments(EnglishGreeting, """public string Greeting { get; private set; } = "Hallo!";""", "ShopLocalizationNl")]
    [Arguments(EnglishGreeting, """public string Greeting { get => "Hallo!"; set { } }""", "ShopLocalizationNl")]
    [Arguments("""public virtual string Greeting { get; private set; } = "Hello!";""", DutchGreeting, "ShopLocalizationEn")]
    public async Task TextWithSetter_ShouldReportLOC206(string english, string dutch, string set)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(Sets(english, dutch));

        // Assert: at the setter, whatever its accessibility, with or without a body, in every language, the default
        // one included.
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(TextSetterAnalyzer.TextSetterDiagnosticId);
        diagnostic.Id.ShouldBe("LOC206");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldBe(
            $"'{set}.Greeting' has a setter, so its wording can change after the set is created. Remove the setter. "
            + "To set the wording when the set is created, use the constructor or 'init'. To change it at run time, read it in the getter.");
        At(diagnostic).ShouldStartWith("set");
    }

    [Test]
    public async Task OverrideOfTextWithSetter_ShouldReportLOC206AtEach()
    {
        // Arrange & Act: a British set that overrides the English text, setter and all.
        var diagnostics = await AnalyzeAsync(Sets(
            """public virtual string Greeting { get; set; } = "Hello!";""",
            DutchGreeting,
            """public override string Greeting { get => "Hello there!"; set { } }"""));

        // Assert
        diagnostics.Length.ShouldBe(2);
        diagnostics.ShouldAllBe(diagnostic => diagnostic.Id == "LOC206");
        diagnostics.Select(static diagnostic => diagnostic.GetMessage()).ShouldContain(message => message.StartsWith("'ShopLocalizationEn.Greeting' has a setter", StringComparison.Ordinal));
        diagnostics.Select(static diagnostic => diagnostic.GetMessage()).ShouldContain(message => message.StartsWith("'ShopLocalizationEnGb.Greeting' has a setter", StringComparison.Ordinal));
    }

    [Test]
    [Arguments("""public string Greeting { get; init; } = "Hallo!";""")]
    [Arguments("""public string Greeting { get; } = "Hallo!";""")]
    [Arguments("""public string Greeting { get => "Hallo!"; }""")]
    [Arguments(DutchGreeting)]
    public async Task TextWithoutSetter_ShouldReportNothing(string dutch)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(Sets(EnglishGreeting, dutch));

        // Assert: init can only be used while the set is created.
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task MemberThatIsNoText_ShouldReportNothing()
    {
        // Arrange: a member marked as no text, a private one, an indexer, and in the Dutch set a helper the contract
        // does not know. Then a class that implements the contract without being a set, such as a test double.
        var sets = Sets(
            $$"""
            {{EnglishGreeting}}

            [ExcludeFromLocalizationDiscovery]
            public string Pattern { get; set; } = "N2";

            private string Hidden { get; set; } = "";

            public string this[int index] { get => ""; set { } }
            """,
            $$"""
            {{DutchGreeting}}
            public string Template { get; set; } = "";
            """);

        const string testDouble = """
            public class FakeShopLocalization : IShopLocalization
            {
                public string Greeting { get; set; } = "";
            }
            """;

        // Act
        var diagnostics = await AnalyzeAsync($"{sets}\n\n{testDouble}");

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task SetOfContractOfAnotherAssembly_ShouldReportLOC206()
    {
        // Arrange & Act: an app adds German to a library, in an assembly that declares no languages itself.
        var diagnostics = await AnalyzeAsync("""
            using ShopLibrary;

            namespace TestApp;

            public sealed class ShopLocalizationDe : CodeChops.Idioma.ILocalizationSet, IShopLocalization
            {
                public string Total { get; set; } = "Summe";
            }
            """, ShopLibrary);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC206");
        diagnostic.GetMessage().ShouldStartWith("'ShopLocalizationDe.Total' has a setter");
    }
}
