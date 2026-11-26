using System.Collections.Immutable;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="HiddenTextAnalyzer"/>: a set that derives from another overrides a text, and doesn't hide it,
/// unless it lists the contract again.
/// </summary>
public sealed class HiddenTextAnalyzerTests
{
    #region Fixture
    /// <summary>
    /// A library whose English text isn't virtual, so an app can't override it.
    /// </summary>
    private static MetadataReference ShopLibrary { get; } = GeneratorTestHelper.CompileLibraryWithGenerators("HiddenShopLibrary", """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace ShopLibrary;

        public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
        {
            public string Total => "Total";
        }

        public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
        {
            public string Total => "Totaal";
        }
        """);

    /// <summary>
    /// A contract generated from an English set, and a British set that derives from it, each holding the given members.
    /// The British set lists what <paramref name="britishBase"/> holds after the English set.
    /// </summary>
    private static string Sets(string english, string british, string britishBase = "") => $$"""
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "en-GB")]

        namespace TestApp;

        public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
        {
            {{english}}
        }

        public class ShopLocalizationEnGb : ShopLocalizationEn{{britishBase}}
        {
            {{british}}
        }
        """;

    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source, params MetadataReference[] references)
    {
        return GeneratorTestHelper.AnalyzeAsync(new HiddenTextAnalyzer(), source, references);
    }

    private static string At(Diagnostic diagnostic)
    {
        return diagnostic.Location.SourceTree!.ToString()[diagnostic.Location.SourceSpan.Start..];
    }
    #endregion

    [Test]
    [Arguments("""public new string Color => "Colour";""")]
    [Arguments("""public string Color => "Colour";""")] // CS0108 warns, and the IDE's fix for it adds 'new'
    public async Task PropertyHidingText_ShouldReportLOC302(string british)
    {
        // Arrange & Act: the contract calls ShopLocalizationEn.Color, so British users see "Color".
        var diagnostics = await AnalyzeAsync(Sets("""public string Color => "Color";""", british));

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC302");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldBe("'ShopLocalizationEnGb.Color' hides 'ShopLocalizationEn.Color' instead of overriding it, so users of 'en-GB' won't see its wording. Make 'ShopLocalizationEn.Color' virtual and override it, or list 'IShopLocalization' again next to 'ShopLocalizationEn'.");
        At(diagnostic).ShouldStartWith("Color => \"Colour\"");
    }

    [Test]
    public async Task MethodHidingText_ShouldReportLOC302()
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(Sets(
            """public string Greeting(string name) => $"Hello, {name}!";""",
            """public new string Greeting(string name) => $"Hiya, {name}!";"""));

        // Assert
        diagnostics.ShouldHaveSingleItem().GetMessage().ShouldStartWith("'ShopLocalizationEnGb.Greeting' hides 'ShopLocalizationEn.Greeting' instead of overriding it");
    }

    [Test]
    [Arguments("""public virtual string Color => "Color";""", """public override string Color => "Colour";""", "")]
    [Arguments("""public string Color => "Color";""", """public new string Color => "Colour";""", ", IShopLocalization")] // Listed again: the contract calls the new member
    [Arguments("""public string Greeting(string name) => $"Hello, {name}!";""", """public string Greeting(int number) => $"Hiya, {number}!";""", "")] // Another method of the name
    [Arguments("""public string Color => "Color"; [ExcludeFromLocalizationDiscovery] public string Helper => "";""", """public new string Helper => "x";""", "")] // Hides no text
    [Arguments("""public string Color => "Color";""", """private new string Color => "Colour";""", "")]
    public async Task MemberThatWordsItsTextOrHidesNone_ShouldReportNothing(string english, string british, string britishBase)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(Sets(english, british, britishBase));

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task AppSetHidingTextOfLibrarySet_ShouldReportLOC302()
    {
        // Arrange & Act: the library's text isn't virtual, so 'override' doesn't compile, and 'new' hides it.
        var diagnostics = await AnalyzeAsync("""
            namespace TestApp;

            public sealed class MyShopLocalizationEn : ShopLibrary.ShopLocalizationEn
            {
                public new string Total => "Sum";
            }
            """, ShopLibrary);

        // Assert
        diagnostics.ShouldHaveSingleItem().GetMessage().ShouldBe("'MyShopLocalizationEn.Total' hides 'ShopLocalizationEn.Total' instead of overriding it, so users of 'en' won't see its wording. List 'IShopLocalization' again next to 'ShopLocalizationEn', because 'ShopLocalizationEn.Total' isn't virtual and lies in another assembly.");
    }

    [Test]
    public async Task AppSetListingLibraryContractAgain_ShouldReportNothing()
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync("""
            namespace TestApp;

            public sealed class MyShopLocalizationEn : ShopLibrary.ShopLocalizationEn, ShopLibrary.IShopLocalization
            {
                public new string Total => "Sum";
            }
            """, ShopLibrary);

        // Assert
        diagnostics.ShouldBeEmpty();
    }
}
