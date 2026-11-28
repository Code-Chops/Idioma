namespace CodeChops.Idioma.Tests;

public sealed partial class LocalizationRegistrationPartTests
{
    /// <summary>
    /// A British set whose region the assembly doesn't declare, which the sets in a test derive from.
    /// </summary>
    private const string BritishShopSets = """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace TestApp;

        public interface IShopLocalization : ILocalizationSet
        {
            string Checkout { get; }
        }

        public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
        {
            public virtual string Checkout => "Checkout";
        }

        public class ShopLocalizationEnGb : ShopLocalizationEn
        {
            public override string Checkout => "Pay";
        }
        """;

    [Test]
    [Arguments("ShopLocalizationEnGb")]
    [Arguments("ShopLocalizationEnGb, ILocalizationSet")]
    [Arguments("ShopLocalizationEnGb, IShopLocalization")]
    public void RewordingOfRegionalSet_TakesItsLanguageWhateverItLists(string baseList)
    {
        // Arrange & Act: the name gives no language, and the set needn't list ILocalizationSet again.
        var result = Run($$"""
            {{BritishShopSets}}

            public sealed class BritishRewording : {{baseList}}
            {
            }
            """);

        // Assert: it takes en-GB from the set it derives from.
        var run = result.Results.ShouldHaveSingleItem();
        run.Diagnostics.ShouldBeEmpty();

        var text = run.GeneratedSources.ShouldHaveSingleItem().SourceText.ToString();
        text.ShouldContain("DescribeSet<TestApp.ShopLocalizationEnGb>(\"en-GB\");");
        text.ShouldContain("DescribeSet<TestApp.BritishRewording>(\"en-GB\");");
        text.ShouldContain("new TestApp.BritishRewording()");
    }

    [Test]
    public void SetsDerivingFromRegionalSet_TakeItsLanguageOverTheirOwnLastTwoLetters()
    {
        // Arrange & Act: each name alone would give uk, which is Ukrainian, because UK isn't a region.
        var result = Run($$"""
            {{BritishShopSets}}

            public class GiftLocalizationUk : ShopLocalizationEnGb;

            public sealed class BookLocalizationUk : GiftLocalizationUk;
            """);

        // Assert
        var run = result.Results.ShouldHaveSingleItem();
        run.Diagnostics.ShouldBeEmpty();

        var text = run.GeneratedSources.ShouldHaveSingleItem().SourceText.ToString();
        text.ShouldContain("DescribeSet<TestApp.GiftLocalizationUk>(\"en-GB\");");
        text.ShouldContain("DescribeSet<TestApp.BookLocalizationUk>(\"en-GB\");");
    }
}
