using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;

namespace CodeChops.Idioma.Tests;

public sealed partial class LocalizationSetsPartTests
{
    [Test]
    [Arguments("\"\"")]
    [Arguments("\"-\"")]
    [Arguments("\"en\\nx\"")]
    public void SetWithLanguageCodeThatIsNoCultureName_ShouldNotStopTheGeneratorOrBeHeldToALanguage(string languageCode)
    {
        // Arrange: what a LocalizationLanguageCode holds while it is being typed. The contract must still be written: without it
        // every IShopLocalization of the project stops compiling.
        var source = $$"""
            {{ShopSets}}

            public class ShopLocalizationBritish : ShopLocalizationEn, ILocalizationSet
            {
                string ILocalizationSet.LocalizationLanguageCode => {{languageCode}};
            }
            """;

        // Act
        var result = Run(source);

        // Assert: nothing about its language from here; LOC207 says what is wrong with the code.
        DiagnosticIds(result).ShouldBeEmpty();
        ContractText(result).ShouldContain("interface IShopLocalization");
        GeneratorTestHelper.GetGeneratorDiagnostics(source).Select(diagnostic => diagnostic.Id).ShouldBe(["LOC207"]);
    }

    [Test]
    [Arguments("\"e\", \"nl\"")]
    [Arguments("\"en_US\", \"nl\"")]
    [Arguments("null, \"nl\"")]
    [Arguments("\"e\"")]
    public void InvalidDefaultLanguage_ShouldNotMakeTheNextLanguageTheDefault(string languages)
    {
        // Arrange: the first language is wrong, which LOC101 reports. With the next one as the default, the English set
        // would be reported as unlisted, no contract would be written, and every use of it would stop compiling.
        var source = ShopSets.Replace("\"en\", \"nl\"", languages, StringComparison.Ordinal);

        // Act
        var result = Run(source);
        var errors = GeneratorTestHelper.CompileWithGenerators(source);

        // Assert: the contract is still written, from the set that starts it, and LOC101 stays the only error.
        DiagnosticIds(result).ShouldBeEmpty();
        ContractText(result).ShouldContain("interface IShopLocalization");
        errors.ShouldHaveSingleItem().Id.ShouldBe(LocalizedTextFactoryGenerator.InvalidLanguagesDiagnosticId);
    }

    [Test]
    public void InvalidDefaultLanguage_ShouldWriteContractOfTwoSetsStartingItOnce()
    {
        // Arrange: without a default language to tell them apart, the Dutch set starts the contract as the English
        // one does. Written twice it would not compile.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("e", "nl")]

            namespace TestApp;

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """;

        // Act
        var run = Run(source).Results.ShouldHaveSingleItem();
        var errors = GeneratorTestHelper.CompileWithGenerators(source);

        // Assert
        run.GeneratedSources.Count(generated => generated.HintName.EndsWith(".Localization.g.cs", StringComparison.Ordinal)).ShouldBe(1);
        errors.ShouldHaveSingleItem().Id.ShouldBe("LOC101");
    }

    [Test]
    public void SetImplementingLanguageCodeOfAnotherInterface_ShouldServeLanguageOfItsName()
    {
        // Arrange & Act: the explicit implementation is not that of ILocalizationSet.LocalizationLanguageCode, which the set leaves
        // to its name. Taken for it, the set would count as German: unlisted, and the Dutch one missing.
        var result = Run($$"""
            {{Header}}

            public interface IHasCode
            {
                string LocalizationLanguageCode { get; }
            }

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization, IHasCode
            {
                string IHasCode.LocalizationLanguageCode => "de";

                public virtual string Checkout => "Afrekenen";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    [Arguments("zh-Hant", "zh-TW", true)]
    [Arguments("zh-Hant", "zh-HK", true)]
    [Arguments("zh-Hant", "zh-MO", true)]
    [Arguments("zh-Hans", "zh-CN", true)]
    [Arguments("zh-Hans", "zh-SG", true)]
    [Arguments("ZH-hans", "zh-cn", true)]
    [Arguments("zh", "zh-TW", true)]
    [Arguments("zh-Hans", "zh-TW", false)]
    [Arguments("zh-Hant", "zh-CN", false)]
    [Arguments("zh-TW", "zh-Hant", false)]
    [Arguments("en", "en-GB", true)]
    [Arguments("en-GB", "en", false)]
    [Arguments("en", "zh-TW", false)]
    public void Language_ShouldServeCulturesThatResolveThroughItAtRunTime(string provided, string requested, bool expected)
    {
        // Arrange & Act: a culture resolves through its parents, which for a Chinese region written without its script
        // is the script (zh-TW under zh-Hant), and no start of its name.
        var serves = SupportedLanguages.Serves(provided, requested);

        // Assert
        serves.ShouldBe(expected);
    }

    [Test]
    public void ChineseRegionDerivedFromSetOfItsScript_ShouldBeAllowed()
    {
        // Arrange & Act: users in Taiwan get the Traditional Chinese wording at run time, so the set derives from a
        // set of its own language, and nothing is left in another one (LOC203).
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "zh-Hant", "zh-TW")]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            public class ShopLocalizationZhHant : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "結帳";
            }

            public class ShopLocalizationZhTw : ShopLocalizationZhHant;
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void LibraryServingChineseRegionThroughItsScript_ShouldReportNothing()
    {
        // Arrange
        var library = GeneratorTestHelper.CompileLibraryWithGenerators("ChineseLibrary", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "zh-Hans")]

            namespace ChineseLibrary;

            public class GridLocalizationEn : ILocalizationSet, IGridLocalization
            {
                public virtual string First => "First";
            }

            public class GridLocalizationZhHans : ILocalizationSet, IGridLocalization
            {
                public virtual string First => "第一";
            }
            """);

        // Act: users in China get the library's Simplified Chinese set at run time.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "zh-CN")]
            """, library);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }
}
