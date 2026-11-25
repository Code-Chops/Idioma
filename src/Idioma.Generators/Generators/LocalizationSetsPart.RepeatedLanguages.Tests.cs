using CodeChops.Idioma.Generators;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

public sealed partial class LocalizationSetsPartTests
{
    [Test]
    public void TwoSetsOfOneLanguage_ShouldReportLOC209AtTheSecond()
    {
        // Arrange & Act: a copied file, or two people adding Dutch.
        var result = Run($$"""
            {{Header}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Cart => "Cart";
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Cart => "Winkelwagen";
            }

            public sealed class BasketNl : ILocalizationSet, IShopLocalization
            {
                public string Cart => "Mandje";
            }
            """);

        // Assert: at the set that isn't named after the contract, which sorts first by name.
        var diagnostic = SingleDiagnostic(result, LocalizationSetsPart.RepeatedLanguageDiagnosticId);
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldBe("'BasketNl' is a second set of 'TestApp.IShopLocalization' for 'nl', next to 'ShopLocalizationNl'. Users of 'nl' see only one of them. Remove one of the two, or derive 'BasketNl' from 'ShopLocalizationNl' to reword some of its texts.");
        diagnostic.Location.GetLineSpan().StartLinePosition.Line.ShouldBe(16);
    }

    [Test]
    public void SecondSetOfTheDefaultLanguage_ShouldReportLOC209AtItAndNotAtTheDefaultSet()
    {
        // Arrange & Act
        var result = Run($$"""
            {{Header}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Cart => "Cart";
            }

            public sealed class AnotherShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Cart => "Basket";
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Cart => "Winkelwagen";
            }
            """);

        // Assert
        var diagnostic = SingleDiagnostic(result, LocalizationSetsPart.RepeatedLanguageDiagnosticId);
        diagnostic.GetMessage().ShouldStartWith("'AnotherShopLocalizationEn' is a second set of 'TestApp.IShopLocalization' for 'en', next to 'ShopLocalizationEn'.");
    }

    [Test]
    public void SetDerivingFromSetOfItsLanguage_ShouldReportNoLOC209()
    {
        // Arrange & Act: it rewords the Dutch set, and always wins over it.
        var result = Run($$"""
            {{Header}}

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Cart => "Cart";
            }

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public virtual string Cart => "Winkelwagen";
            }

            public sealed class BrandedShopLocalizationNl : ShopLocalizationNl
            {
                public override string Cart => "Mandje";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void SecondSetRegisteredByHand_ShouldReportNoLOC209()
    {
        // Arrange & Act: registering it is a choice the code states.
        var result = Run($$"""
            {{Header}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Cart => "Cart";
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Cart => "Winkelwagen";
            }

            [ExcludeFromLocalizationDiscovery]
            public sealed class BasketNl : ILocalizationSet, IShopLocalization
            {
                public string Cart => "Mandje";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void TwoSetsOfOneLanguageOfHandWrittenContract_ShouldReportLOC209()
    {
        // Arrange & Act
        var result = Run($$"""
            {{Header}}

            public interface IShopLocalization : ILocalizationSet
            {
                string Cart { get; }
            }

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Cart => "Cart";
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Cart => "Winkelwagen";
            }

            public sealed class WinkelNl : ILocalizationSet, IShopLocalization
            {
                public string Cart => "Mandje";
            }
            """);

        // Assert
        var diagnostic = SingleDiagnostic(result, LocalizationSetsPart.RepeatedLanguageDiagnosticId);
        diagnostic.GetMessage().ShouldStartWith("'WinkelNl' is a second set of 'TestApp.IShopLocalization' for 'nl', next to 'ShopLocalizationNl'.");
    }

    [Test]
    public void SetsOfOneLanguageOfTwoContracts_ShouldReportNoLOC209()
    {
        // Arrange & Act: one Dutch set per contract.
        var result = Run($$"""
            {{Header}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Cart => "Cart";
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Cart => "Winkelwagen";
            }

            public sealed class MenuLocalizationEn : ILocalizationSet, IMenuLocalization
            {
                public string Home => "Home";
            }

            public sealed class MenuLocalizationNl : ILocalizationSet, IMenuLocalization
            {
                public string Home => "Start";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }
}
