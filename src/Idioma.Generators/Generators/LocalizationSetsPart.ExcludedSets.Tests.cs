namespace CodeChops.Idioma.Tests;

public sealed partial class LocalizationSetsPartTests
{
    [Test]
    [Arguments("public abstract class ShopLocalizationEn : ILocalizationSet { public string Checkout => \"Checkout\"; }")]
    [Arguments("file sealed class ShopLocalizationEn : ILocalizationSet { public string Checkout => \"Checkout\"; }")]
    [Arguments("public static class Container { public sealed class ShopLocalizationEn : ILocalizationSet { public string Checkout => \"Checkout\"; } }")]
    [Arguments("public static partial class Container { [ExcludeFromLocalizationDiscovery] public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization { public string Checkout => \"Checkout\"; } }")]
    [Arguments("public static partial class Container { public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization { public string Checkout => \"Afrekenen\"; } }")]
    [Arguments("[ExcludeFromLocalizationDiscovery] public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization { public string Checkout => \"Checkout\"; }")]
    public void SetTakingNoPart_ShouldReportNothingAndEmitNothing(string declaration)
    {
        // Arrange & Act: registered by hand, if at all, so it has no contract to generate. Nothing marks it as one: it
        // is not partial (a test fixture), excluded, or in another language than the default.
        var result = Run($"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            {declaration}
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
        result.Results.ShouldHaveSingleItem().GeneratedSources.ShouldBeEmpty();
    }

    [Test]
    public void ExcludedSet_ShouldCountAsSetOfItsLanguage()
    {
        // Arrange & Act: the fix LOC207 names for a set that needs a constructor argument; registering it is up to the
        // app.
        var result = Run($$"""
            {{Header}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            [ExcludeFromLocalizationDiscovery]
            public sealed class ShopLocalizationNl(string checkout) : ILocalizationSet, IShopLocalization
            {
                public string Checkout => checkout;
            }
            """);

        // Assert: no LOC201, the language is listed for the assemblies referencing this one, and the generated text
        // links to its wording.
        DiagnosticIds(result).ShouldBeEmpty();
        ProvidedSets(result).ShouldContain("LocalizationSetAttribute(\"TestApp.IShopLocalization\", \"nl\", ");
        ContractText(result).ShouldContain("<see cref=\"ShopLocalizationNl.Checkout\">nl</see>");
    }

    [Test]
    public void ExcludedSetNamedAfterUnlistedLanguage_ShouldNotCountForListedLanguage()
    {
        // Arrange & Act: this set serves be, not the listed nl-BE, so the language stays missing, and LOC202 says what
        // the set serves instead, excluded or not.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl-BE")]

            namespace TestApp;

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            [ExcludeFromLocalizationDiscovery]
            public sealed class ShopLocalizationBe : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBe(["LOC201", "LOC202"]);
    }

    [Test]
    public void ExcludedSetNamedAfterListedLongCode_ShouldCountForThatLanguage()
    {
        // Arrange & Act: registered by hand, it serves nl-BE, as the generated registrar describes it with that code too.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl-BE")]

            namespace TestApp;

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            [ExcludeFromLocalizationDiscovery]
            public sealed class ShopLocalizationNlBe : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
        ProvidedSets(result).ShouldContain("LocalizationSetAttribute(\"TestApp.IShopLocalization\", \"nl-BE\", ");
    }

    [Test]
    [Arguments("[ExcludeFromLocalizationDiscovery] public class GiftLocalizationNl : ILocalizationSet, IShopLocalization { public virtual string Checkout => \"Afrekenen\"; public virtual string Total => \"Totaal\"; } [ExcludeFromLocalizationDiscovery] public sealed class GiftLocalizationEn : GiftLocalizationNl;")]
    [Arguments("[ExcludeFromLocalizationDiscovery] public sealed class CartLocalizationNl : ILocalizationSet { public string Checkout => \"Afrekenen\"; }")]
    public void ExcludedSet_ShouldReportNothingAboutItself(string declaration)
    {
        // Arrange & Act: a set deriving from a set of another language (LOC204), and one listing ILocalizationSet in
        // another language than the default (LOC205), were either not excluded. A language not listed is still LOC202.
        var result = Run($"""
            {ShopSets}

            {declaration}
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }
}
