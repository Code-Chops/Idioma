using System.Collections.Immutable;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="TextOutsideContractAnalyzer"/>: a set has no text that its contract doesn't have, because no
/// user would see it.
/// </summary>
public sealed class TextOutsideContractAnalyzerTests
{
    #region Fixture
    /// <summary>
    /// A library whose contract an app's set can implement for a language of its own.
    /// </summary>
    private static MetadataReference ShopLibrary { get; } = GeneratorTestHelper.CompileLibraryWithGenerators("OutsideShopLibrary", """
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
    /// A contract generated from an English set, and a Dutch set of it, each holding two texts and the given members.
    /// </summary>
    private static string Sets(string english, string dutch) => $$"""
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace TestApp;

        public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
        {
            public string Checkout => "Checkout";
            public string Greeting(string name) => $"Hello, {name}!";
            {{english}}
        }

        public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
        {
            public string Checkout => "Afrekenen";
            public string Greeting(string name) => $"Hallo, {name}!";
            {{dutch}}
        }
        """;

    /// <summary>
    /// A set of the enum <c>OrderStatus</c> in English and Dutch, each holding the given members.
    /// </summary>
    private static string EnumSets(string english, string dutch) => $$"""
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace TestApp;

        public enum OrderStatus { Pending, Shipped }

        public class OrderStatusLocalizationEn : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
        {
            public string Pending => "Pending";
            {{english}}
        }

        public class OrderStatusLocalizationNl : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
        {
            public string Pending => "In behandeling";
            public string Shipped => "Verzonden";
            {{dutch}}
        }
        """;

    /// <summary>
    /// A contract generated from an English set, and a British set that derives from it, holding the given members.
    /// </summary>
    private static string RegionalSets(string english, string british) => $$"""
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "en-GB")]

        namespace TestApp;

        public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
        {
            {{english}}
        }

        public class ShopLocalizationEnGb : ShopLocalizationEn
        {
            {{british}}
        }
        """;

    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source, params MetadataReference[] references)
    {
        return GeneratorTestHelper.AnalyzeAsync(new TextOutsideContractAnalyzer(), source, references);
    }

    private static string At(Diagnostic diagnostic)
    {
        return diagnostic.Location.SourceTree!.ToString()[diagnostic.Location.SourceSpan.Start..];
    }
    #endregion

    [Test]
    [Arguments("""public string Basket => "Mandje";""", "Basket")]
    [Arguments("""public string ItemsInBasket(int count) => $"{count} in je mandje";""", "ItemsInBasket")]
    public async Task TextOnlyInDutch_ShouldReportLOC209(string dutch, string text)
    {
        // Arrange & Act: the contract comes from the English set, which has no such text.
        var diagnostics = await AnalyzeAsync(Sets("", dutch));

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC209");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().ShouldBe($"'ShopLocalizationNl.{text}' isn't a text of 'IShopLocalization', so users of 'nl' won't see it. Add '{text}' to the default-language set, or mark it [ExcludeFromLocalizationDiscovery] if it isn't a text.");
        At(diagnostic).ShouldStartWith(text);
    }

    [Test]
    public async Task TextOnlyInDutchSetOfEnum_ShouldReportLOC209()
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(EnumSets("""public string Shipped => "Shipped";""", """public string Lost { get; } = "Kwijt";"""));

        // Assert
        diagnostics.ShouldHaveSingleItem().GetMessage().ShouldBe("'OrderStatusLocalizationNl.Lost' isn't a text of 'IOrderStatusLocalization', so users of 'nl' won't see it. Add 'Lost' to the default-language set, or mark it [ExcludeFromLocalizationDiscovery] if it isn't a text.");
    }

    [Test]
    public async Task TextOnlyInBritishSet_ShouldReportLOC209()
    {
        // Arrange & Act: the British set rewords Cart, and adds Trolley, which English doesn't have.
        var diagnostics = await AnalyzeAsync(RegionalSets(
            """public virtual string Cart => "Cart";""",
            """public override string Cart => "Basket"; public string Trolley => "Trolley";"""));

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.GetMessage().ShouldBe("'ShopLocalizationEnGb.Trolley' isn't a text of 'IShopLocalization', so users of 'en-GB' won't see it. Add 'Trolley' to the default-language set, or mark it [ExcludeFromLocalizationDiscovery] if it isn't a text.");
        At(diagnostic).ShouldStartWith("Trolley");
    }

    [Test]
    public async Task TextOnlyInAppSetOfLibraryContract_ShouldAdviseToRemoveIt()
    {
        // Arrange & Act: the app can't add a text to the library's contract.
        var diagnostics = await AnalyzeAsync("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]

            namespace TestApp;

            public sealed class ShopLocalizationDe : ILocalizationSet, ShopLibrary.IShopLocalization
            {
                public string Total => "Summe";
                public string Basket => "Warenkorb";
            }
            """, ShopLibrary);

        // Assert
        diagnostics.ShouldHaveSingleItem().GetMessage().ShouldBe("'ShopLocalizationDe.Basket' isn't a text of 'IShopLocalization', so users of 'de' won't see it. Remove it, or mark it [ExcludeFromLocalizationDiscovery] if it isn't a text.");
    }

    [Test]
    public async Task TextOnlyInSetOfContractWrittenByHand_ShouldAdviseToAddItToTheContract()
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public interface IShopTexts : ILocalizationSet
            {
                string Cart { get; }
            }

            public class ShopTextsEn : IShopTexts
            {
                public string Cart => "Cart";
            }

            public class ShopTextsNl : IShopTexts
            {
                public string Cart => "Winkelwagen";
                public string Basket => "Mandje";
            }
            """);

        // Assert
        diagnostics.ShouldHaveSingleItem().GetMessage().ShouldBe("'ShopTextsNl.Basket' isn't a text of 'IShopTexts', so users of 'nl' won't see it. Add 'Basket' to 'IShopTexts', or mark it [ExcludeFromLocalizationDiscovery] if it isn't a text.");
    }

    [Test]
    [Arguments("""public string Basket => "Basket";""", """public string Basket => "Mandje";""")] // A text of the contract
    [Arguments("", """[ExcludeFromLocalizationDiscovery] public string Helper => "";""")]
    [Arguments("", """internal string Basket => "Mandje";""")]
    [Arguments("", """private string Basket => "Mandje";""")]
    [Arguments("", """public static string Basket => "Mandje";""")]
    [Arguments("", """public int Count => 2;""")]
    [Arguments("", """public string this[int index] => "";""")]
    [Arguments("", """public string Basket() => "Mandje";""")] // No text that takes values, as it takes none
    [Arguments("", """public string Format<T>(T value) => "";""")] // Can't be a text (LOC300)
    [Arguments("", """public string Greeting(string name, int visits) => "";""")] // Another method of a text's name
    [Arguments("", """public string LocalizationLanguageCode => "nl";""")]
    [Arguments("""public string Format<T>(T value) => "";""", "")] // In the default-language set: LOC300
    [Arguments("""public string GetTextByKey => "Key";""", """public string GetTextByKey => "Sleutel";""")] // LOC205, still in the contract
    public async Task MemberThatIsATextOfTheContractOrNoText_ShouldReportNothing(string english, string dutch)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(Sets(english, dutch));

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("""public string Shipped => "Shipped";""", "")]
    [Arguments("""public string Shipped(string carrier) => $"Shipped by {carrier}";""", "")] // LOC300: a member's text takes no values
    [Arguments("""public string Shipped => "Shipped"; public string Caption => "Order status";""", """public string Caption => "Bestelstatus";""")]
    public async Task SetOfEnumWithTextsOfTheContract_ShouldReportNothing(string english, string dutch)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(EnumSets(english, dutch));

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("""public string Cart => "Cart";""", """public new string Cart => "Basket";""")] // LOC207
    [Arguments("""public string Cart => "Cart"; [ExcludeFromLocalizationDiscovery] public string Helper => "";""", """public new string Helper => "x";""")]
    [Arguments("""public string Cart => "Cart"; [ExcludeFromLocalizationDiscovery] public virtual string Helper => "";""", """public override string Helper => "x";""")]
    public async Task BritishMemberOfANameItInherits_ShouldReportNothing(string english, string british)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(RegionalSets(english, british));

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task PropertyNamedLikeTextThatTakesValues_ShouldReportNothing()
    {
        // Arrange & Act: the Dutch text is still a property, which LOC502 reports.
        var diagnostics = await AnalyzeAsync("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Greeting(string name) => $"Hello, {name}!";
            }

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Greeting => "Hallo {0}!";
            }
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task SetWithoutContract_ShouldReportNothing()
    {
        // Arrange & Act: LOC204 tells the set to list its contract.
        var diagnostics = await AnalyzeAsync("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Cart => "Cart";
            }

            public class ShopLocalizationNl : ILocalizationSet
            {
                public string Cart => "Winkelwagen";
                public string Basket => "Mandje";
            }
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }
}
