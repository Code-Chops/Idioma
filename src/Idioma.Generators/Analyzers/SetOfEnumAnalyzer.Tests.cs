using System.Collections.Immutable;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="SetOfEnumAnalyzer"/>: every set of the contract of an enum lists
/// <c>ILocalizationSet&lt;TEnum&gt;</c>, in each language.
/// </summary>
public sealed class SetOfEnumAnalyzerTests
{
    #region Fixture
    /// <summary>
    /// A library with a set of a public enum, and one of an internal enum, whose contracts an app's set can implement.
    /// </summary>
    private static MetadataReference StatusLibrary { get; } = GeneratorTestHelper.CompileLibraryWithGenerators("SetOfEnumStatusLibrary", """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace StatusLibrary;

        public enum OrderStatus { Pending }

        public class OrderStatusLocalizationEn : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
        {
            public string Pending => "Pending";
        }

        public class OrderStatusLocalizationNl : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
        {
            public string Pending => "In behandeling";
        }

        internal enum StockStatus { InStock }

        public class StockStatusLocalizationEn : ILocalizationSet<StockStatus>, IStockStatusLocalization
        {
            public string InStock => "In stock";
        }

        public class StockStatusLocalizationNl : ILocalizationSet<StockStatus>, IStockStatusLocalization
        {
            public string InStock => "Op voorraad";
        }

        public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
        {
            public string Cart => "Cart";
        }

        public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
        {
            public string Cart => "Winkelwagen";
        }
        """);

    /// <summary>
    /// A set of the enum <c>OrderStatus</c> in English, and a Dutch set of its contract that lists what
    /// <paramref name="dutchBase"/> holds. The enum <c>PaymentStatus</c> has no texts in the contract.
    /// </summary>
    private static string Sets(string dutchBase) => $$"""
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace TestApp;

        public enum OrderStatus { Pending }
        public enum PaymentStatus { Paid }

        public class OrderStatusLocalizationEn : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
        {
            public string Pending => "Pending";
        }

        public class OrderStatusLocalizationNl : {{dutchBase}}
        {
            public string Pending => "In behandeling";
        }
        """;

    /// <summary>
    /// A set of two enums in English, and a Dutch set of its contract that lists what <paramref name="dutchBase"/>
    /// holds.
    /// </summary>
    private static string SetsOfTwoEnums(string dutchBase) => $$"""
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace TestApp;

        public enum OrderStatus { Pending }
        public enum PaymentStatus { Paid }

        public class StatusLocalizationEn : ILocalizationSet<OrderStatus>, ILocalizationSet<PaymentStatus>, IStatusLocalization
        {
            public string Pending => "Pending";
            public string Paid => "Paid";
        }

        public class StatusLocalizationNl : {{dutchBase}}
        {
            public string Pending => "In behandeling";
            public string Paid => "Betaald";
        }
        """;

    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source, params MetadataReference[] references)
    {
        return GeneratorTestHelper.AnalyzeAsync(new SetOfEnumAnalyzer(), source, references);
    }

    private static string At(Diagnostic diagnostic)
    {
        return diagnostic.Location.SourceTree!.ToString()[diagnostic.Location.SourceSpan.Start..];
    }
    #endregion

    [Test]
    [Arguments("ILocalizationSet, IOrderStatusLocalization", "ILocalizationSet, IOrderStatusLocalization")]
    [Arguments("IOrderStatusLocalization, CodeChops.Idioma.ILocalizationSet", "CodeChops.Idioma.ILocalizationSet")]
    public async Task DutchSetListingILocalizationSet_ShouldReportLOC211AtIt(string dutchBase, string at)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(Sets(dutchBase));

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC211");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().ShouldBe("'OrderStatusLocalizationNl' doesn't list 'ILocalizationSet<OrderStatus>', which every set of 'IOrderStatusLocalization' lists. List it in place of 'ILocalizationSet'.");
        At(diagnostic).ShouldStartWith(at);
    }

    [Test]
    public async Task DutchSetListingNeitherEnum_ShouldNameBoth()
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(SetsOfTwoEnums("ILocalizationSet, IStatusLocalization"));

        // Assert
        diagnostics.ShouldHaveSingleItem().GetMessage().ShouldBe("'StatusLocalizationNl' doesn't list 'ILocalizationSet<OrderStatus>' and 'ILocalizationSet<PaymentStatus>', which every set of 'IStatusLocalization' lists. List them in place of 'ILocalizationSet'.");
    }

    [Test]
    public async Task DutchSetListingOneOfTwoEnums_ShouldNameTheOther()
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(SetsOfTwoEnums("ILocalizationSet<OrderStatus>, IStatusLocalization"));

        // Assert: at the set, as it lists no ILocalizationSet to replace.
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.GetMessage().ShouldBe("'StatusLocalizationNl' doesn't list 'ILocalizationSet<PaymentStatus>', which every set of 'IStatusLocalization' lists. List it next to 'IStatusLocalization'.");
        At(diagnostic).ShouldStartWith("StatusLocalizationNl");
    }

    [Test]
    public async Task DutchSetListingEnumOfNoTextsNextToItsOwn_ShouldReportIt()
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(Sets("ILocalizationSet<OrderStatus>, ILocalizationSet<PaymentStatus>, IOrderStatusLocalization"));

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC211");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().ShouldBe("'OrderStatusLocalizationNl' lists 'ILocalizationSet<PaymentStatus>', but 'IOrderStatusLocalization' has no texts of 'PaymentStatus'. Remove it, or list it in the default-language set too.");
        At(diagnostic).ShouldStartWith("ILocalizationSet<PaymentStatus>");
    }

    [Test]
    public async Task DutchSetListingEnumOfNoTextsInPlaceOfItsOwn_ShouldReportItOnce()
    {
        // Arrange & Act: one mistake, the wrong enum, so one diagnostic.
        var diagnostics = await AnalyzeAsync(Sets("ILocalizationSet<PaymentStatus>, IOrderStatusLocalization"));

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.GetMessage().ShouldBe("'OrderStatusLocalizationNl' doesn't list 'ILocalizationSet<OrderStatus>', which every set of 'IOrderStatusLocalization' lists. List it in place of 'ILocalizationSet<PaymentStatus>'.");
        At(diagnostic).ShouldStartWith("ILocalizationSet<PaymentStatus>");
    }

    [Test]
    public async Task SetOfContractOfNoEnumListingEnum_ShouldAdviseILocalizationSet()
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public enum PaymentStatus { Paid }

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Cart => "Cart";
            }

            public class ShopLocalizationNl : ILocalizationSet<PaymentStatus>, IShopLocalization
            {
                public string Cart => "Winkelwagen";
            }
            """);

        // Assert: without it the class would be no set.
        diagnostics.ShouldHaveSingleItem().GetMessage().ShouldBe("'ShopLocalizationNl' lists 'ILocalizationSet<PaymentStatus>', but 'IShopLocalization' has no texts of 'PaymentStatus'. Replace it with 'ILocalizationSet', or list it in the default-language set too.");
    }

    [Test]
    public async Task BritishSetListingEnumOfNoTexts_ShouldAdviseToRemoveIt()
    {
        // Arrange & Act: the English set it derives from makes it a set.
        var diagnostics = await AnalyzeAsync("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "en-GB")]

            namespace TestApp;

            public enum OrderStatus { Pending }
            public enum PaymentStatus { Paid }

            public class OrderStatusLocalizationEn : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
            {
                public virtual string Pending => "Pending";
            }

            public class OrderStatusLocalizationEnGb : OrderStatusLocalizationEn, ILocalizationSet<PaymentStatus>;
            """);

        // Assert
        diagnostics.ShouldHaveSingleItem().GetMessage().ShouldBe("'OrderStatusLocalizationEnGb' lists 'ILocalizationSet<PaymentStatus>', but 'IOrderStatusLocalization' has no texts of 'PaymentStatus'. Remove it, or list it in the default-language set too.");
    }

    [Test]
    public async Task AppSetOfLibraryEnumListingEnumOfNoTexts_ShouldAdviseToRemoveIt()
    {
        // Arrange & Act: the app can't change the library's default-language set.
        var diagnostics = await AnalyzeAsync("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]

            namespace TestApp;

            public enum PaymentStatus { Paid }

            public sealed class OrderStatusLocalizationDe : ILocalizationSet<StatusLibrary.OrderStatus>, ILocalizationSet<PaymentStatus>, StatusLibrary.IOrderStatusLocalization
            {
                public string Pending => "In Bearbeitung";
            }
            """, StatusLibrary);

        // Assert
        diagnostics.ShouldHaveSingleItem().GetMessage().ShouldBe("'OrderStatusLocalizationDe' lists 'ILocalizationSet<PaymentStatus>', but 'IOrderStatusLocalization' has no texts of 'PaymentStatus'. Remove it.");
    }

    [Test]
    public async Task AppSetOfLibraryContractNamingNoEnum_ShouldReportNothing()
    {
        // Arrange & Act: a contract of a library that names no enum may be of one, generated before the enums were
        // named.
        var diagnostics = await AnalyzeAsync("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]

            namespace TestApp;

            public enum PaymentStatus { Paid }

            public sealed class ShopLocalizationDe : ILocalizationSet<PaymentStatus>, StatusLibrary.IShopLocalization
            {
                public string Cart => "Warenkorb";
            }
            """, StatusLibrary);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task AppSetOfLibraryEnumListingILocalizationSet_ShouldReportLOC211()
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]

            namespace TestApp;

            public sealed class OrderStatusLocalizationDe : ILocalizationSet, StatusLibrary.IOrderStatusLocalization
            {
                public string Pending => "In Bearbeitung";
            }
            """, StatusLibrary);

        // Assert
        diagnostics.ShouldHaveSingleItem().GetMessage().ShouldBe("'OrderStatusLocalizationDe' doesn't list 'ILocalizationSet<OrderStatus>', which every set of 'IOrderStatusLocalization' lists. List it in place of 'ILocalizationSet'.");
    }

    [Test]
    public async Task AppSetOfLibraryInternalEnum_ShouldReportNothing()
    {
        // Arrange & Act: the app can't name the enum, so it can't list its set interface.
        var diagnostics = await AnalyzeAsync("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]

            namespace TestApp;

            public sealed class StockStatusLocalizationDe : ILocalizationSet, StatusLibrary.IStockStatusLocalization
            {
                public string InStock => "Auf Lager";
            }
            """, StatusLibrary);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("ILocalizationSet<OrderStatus>, IOrderStatusLocalization")]
    [Arguments("CodeChops.Idioma.ILocalizationSet<TestApp.OrderStatus>, IOrderStatusLocalization")]
    [Arguments("OrderStatusLocalizationEn")] // Derives from the English set, which LOC203 reports
    [Arguments("OrderStatusLocalizationEn, IOrderStatusLocalization")]
    public async Task DutchSetListingOrInheritingSetOfEnum_ShouldReportNothing(string dutchBase)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(Sets(dutchBase));

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task SetsOfContractNotOfAnEnum_ShouldReportNothing()
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Cart => "Cart";
            }

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Cart => "Winkelwagen";
            }
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }
}
