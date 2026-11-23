using System.Collections.Immutable;
using CodeChops.Idioma.CodeFixes;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="SetOfEnumCodeFixProvider"/>: a set of the contract of an enum that doesn't list
/// <c>ILocalizationSet&lt;TEnum&gt;</c> (<c>LOC211</c>) lists it in place of <c>ILocalizationSet</c>.
/// </summary>
public sealed class SetOfEnumCodeFixProviderTests
{
    #region Fixture
    private static readonly SetOfEnumCodeFixProvider Provider = new();
    private static readonly SetOfEnumAnalyzer Analyzer = new();

    /// <summary>
    /// A set of the enums <c>Orders.OrderStatus</c> and <c>Orders.PaymentStatus</c> in English, and a Dutch set of its
    /// contract declared as <paramref name="dutch"/>, in a file with the given using directives. The contract has no
    /// texts of <c>Orders.ShippingStatus</c>.
    /// </summary>
    private static string Sets(string dutch, string usings = "") => $$"""
        using CodeChops.Idioma;
        {{usings}}
        [assembly: SupportedLanguages("en", "nl")]

        namespace Orders
        {
            public enum OrderStatus { Pending }
            public enum PaymentStatus { Paid }
            public enum ShippingStatus { Sent }

            public class OrderStatusLocalizationEn : ILocalizationSet<OrderStatus>, ILocalizationSet<PaymentStatus>, IOrderStatusLocalization
            {
                public string Pending => "Pending";
                public string Paid => "Paid";
            }
        }

        namespace TestApp
        {
            {{dutch}}
            {
                public string Pending => "In behandeling";
                public string Paid => "Betaald";
            }
        }
        """;

    private static Task<ImmutableArray<(string Title, ImmutableArray<string> Sources)>> FixAsync(string source)
    {
        return CodeFixTestHelper.FixWithGeneratedCodeAsync(Provider, Analyzer, source);
    }
    #endregion

    [Test]
    [Arguments(
        "public class OrderStatusLocalizationNl : ILocalizationSet, Orders.IOrderStatusLocalization",
        "public class OrderStatusLocalizationNl : ILocalizationSet<Orders.OrderStatus>, ILocalizationSet<Orders.PaymentStatus>, Orders.IOrderStatusLocalization",
        "Replace 'ILocalizationSet' with 'ILocalizationSet<Orders.OrderStatus>', 'ILocalizationSet<Orders.PaymentStatus>'")]
    [Arguments(
        "public class OrderStatusLocalizationNl : Orders.IOrderStatusLocalization, CodeChops.Idioma.ILocalizationSet // Dutch",
        "public class OrderStatusLocalizationNl : Orders.IOrderStatusLocalization, CodeChops.Idioma.ILocalizationSet<Orders.OrderStatus>, CodeChops.Idioma.ILocalizationSet<Orders.PaymentStatus> // Dutch",
        "Replace 'CodeChops.Idioma.ILocalizationSet' with 'CodeChops.Idioma.ILocalizationSet<Orders.OrderStatus>', 'CodeChops.Idioma.ILocalizationSet<Orders.PaymentStatus>'")]
    [Arguments(
        "public class OrderStatusLocalizationNl : ILocalizationSet<Orders.OrderStatus>, Orders.IOrderStatusLocalization",
        "public class OrderStatusLocalizationNl : ILocalizationSet<Orders.OrderStatus>, ILocalizationSet<Orders.PaymentStatus>, Orders.IOrderStatusLocalization",
        "List 'ILocalizationSet<Orders.PaymentStatus>'")]
    [Arguments(
        "public class OrderStatusLocalizationNl : ILocalizationSet<Orders.ShippingStatus>, Orders.IOrderStatusLocalization",
        "public class OrderStatusLocalizationNl : ILocalizationSet<Orders.OrderStatus>, ILocalizationSet<Orders.PaymentStatus>, Orders.IOrderStatusLocalization",
        "Replace 'ILocalizationSet<Orders.ShippingStatus>' with 'ILocalizationSet<Orders.OrderStatus>', 'ILocalizationSet<Orders.PaymentStatus>'")]
    [Arguments(
        "public class OrderStatusLocalizationNl : ILocalizationSet<Orders.OrderStatus>, ILocalizationSet<Orders.ShippingStatus>, ILocalizationSet<Orders.PaymentStatus>, Orders.IOrderStatusLocalization",
        "public class OrderStatusLocalizationNl : ILocalizationSet<Orders.OrderStatus>, ILocalizationSet<Orders.PaymentStatus>, Orders.IOrderStatusLocalization",
        "Remove 'ILocalizationSet<Orders.ShippingStatus>'")]
    [Arguments(
        "public class OrderStatusLocalizationNl : ILocalizationSet<Orders.OrderStatus>, ILocalizationSet<Orders.PaymentStatus>, Orders.IOrderStatusLocalization, ILocalizationSet<Orders.ShippingStatus> // Dutch",
        "public class OrderStatusLocalizationNl : ILocalizationSet<Orders.OrderStatus>, ILocalizationSet<Orders.PaymentStatus>, Orders.IOrderStatusLocalization // Dutch",
        "Remove 'ILocalizationSet<Orders.ShippingStatus>'")]
    [Arguments(
        "public class OrderStatusLocalizationNl : ILocalizationSet<Orders.ShippingStatus>, ILocalizationSet, Orders.IOrderStatusLocalization",
        "public class OrderStatusLocalizationNl : ILocalizationSet<Orders.OrderStatus>, ILocalizationSet<Orders.PaymentStatus>, Orders.IOrderStatusLocalization",
        "Replace 'ILocalizationSet<Orders.ShippingStatus>', 'ILocalizationSet' with 'ILocalizationSet<Orders.OrderStatus>', 'ILocalizationSet<Orders.PaymentStatus>'")]
    public async Task SetWhoseEnumsDontMatch_ShouldMatchItsContract(string declaration, string expected, string title)
    {
        // Arrange & Act
        var fixes = await FixAsync(Sets(declaration));

        // Assert: one fix for each diagnostic, as a set can have two, and each fixes the whole set.
        fixes.ShouldNotBeEmpty();

        foreach (var fix in fixes)
        {
            fix.Title.ShouldBe(title);
            fix.Sources.ShouldHaveSingleItem().ShouldBe(Sets(expected));
        }
    }

    [Test]
    public async Task EnumInImportedNamespace_ShouldBeNamedByItsName()
    {
        // Arrange & Act
        var fixes = await FixAsync(Sets("public class OrderStatusLocalizationNl : ILocalizationSet, IOrderStatusLocalization", "using Orders;\n"));

        // Assert
        fixes.ShouldHaveSingleItem().Sources.ShouldHaveSingleItem()
            .ShouldBe(Sets("public class OrderStatusLocalizationNl : ILocalizationSet<OrderStatus>, ILocalizationSet<PaymentStatus>, IOrderStatusLocalization", "using Orders;\n"));
    }

    [Test]
    public async Task SetOfContractOfNoEnumListingEnum_ShouldListILocalizationSetInItsPlace()
    {
        // Arrange
        const string Source = """
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
            """;

        // Act
        var fixes = await FixAsync(Source);

        // Assert
        var fix = fixes.ShouldHaveSingleItem();
        fix.Title.ShouldBe("Replace 'ILocalizationSet<PaymentStatus>' with 'ILocalizationSet'");
        fix.Sources.ShouldHaveSingleItem().ShouldBe(Source.Replace("ShopLocalizationNl : ILocalizationSet<PaymentStatus>,", "ShopLocalizationNl : ILocalizationSet,", StringComparison.Ordinal));
    }

    [Test]
    public async Task SetsWithoutSetOfEnum_ShouldAllBeFixedByFixAll()
    {
        // Arrange
        const string Source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]

            namespace TestApp;

            public enum OrderStatus { Pending }

            public class OrderStatusLocalizationEn : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
            {
                public string Pending => "Pending";
            }

            public class OrderStatusLocalizationNl : ILocalizationSet, IOrderStatusLocalization
            {
                public string Pending => "In behandeling";
            }

            public class OrderStatusLocalizationDe : ILocalizationSet, IOrderStatusLocalization
            {
                public string Pending => "In Bearbeitung";
            }
            """;

        // Act
        var sources = await CodeFixTestHelper.FixAllAfterGeneratorsAsync(Provider, Analyzer, Source);

        // Assert
        sources.ShouldHaveSingleItem().ShouldBe(Source.Replace(": ILocalizationSet, IOrderStatusLocalization", ": ILocalizationSet<OrderStatus>, IOrderStatusLocalization", StringComparison.Ordinal));
    }
}
