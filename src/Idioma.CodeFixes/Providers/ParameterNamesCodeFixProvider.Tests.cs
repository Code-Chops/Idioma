using System.Collections.Immutable;
using CodeChops.Idioma.CodeFixes;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="ParameterNamesCodeFixProvider"/>: a text of a set that names a parameter differently than its
/// contract (<c>LOC401</c>) gets the contract's names. Names in another order move and leave the body as it is, and a
/// name the contract lacks is renamed along with its uses.
/// </summary>
public sealed class ParameterNamesCodeFixProviderTests
{
    #region Fixture
    /// <summary>
    /// A contract generated from an English set, with a Dutch set and a British set holding the given members.
    /// </summary>
    private static string Sets(string dutch, string british = "") => $$"""
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl", "en-GB")]

        namespace TestApp;

        public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
        {
            public virtual string Invited(string host, string guest) => $"{host} invited {guest}";
            public virtual string ItemsInCart(int count, string shop) => $"{count} items in your cart at {shop}";
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

    private const string DutchInvited = """public string Invited(string host, string guest) => $"{host} heeft {guest} uitgenodigd";""";
    private const string DutchItemsInCart = """public string ItemsInCart(int count, string shop) => $"{count} artikelen in je winkelwagen bij {shop}";""";

    private static ParameterNamesCodeFixProvider Provider => new();
    private static TextParametersAnalyzer Analyzer => new();

    private static Task<ImmutableArray<(string Title, ImmutableArray<string> Sources)>> FixAsync(string source)
    {
        return CodeFixTestHelper.FixWithGeneratedCodeAsync(Provider, Analyzer, source);
    }

    /// <summary>
    /// Neither the analyzer nor the compiler reports anything, so every set has the contract's names and compiles.
    /// </summary>
    private static async Task ShouldReportNothingAsync(string source)
    {
        (await GeneratorTestHelper.AnalyzeAsync(Analyzer, source, [])).ShouldBeEmpty();
        GeneratorTestHelper.CompileWithGenerators(source).ShouldBeEmpty();
    }
    #endregion

    [Test]
    [Arguments(
        """public string Invited(string guest, string host) => $"{host} heeft {guest} uitgenodigd";""",
        """public string Invited(string host, string guest) => $"{host} heeft {guest} uitgenodigd";""",
        "")]
    [Arguments(
        """string IShopLocalization.Invited(string guest, string host) => $"{host} heeft {guest} uitgenodigd";""",
        """string IShopLocalization.Invited(string host, string guest) => $"{host} heeft {guest} uitgenodigd";""",
        "")]
    [Arguments(
        """public override string Invited(string guest, string host) => $"{host} has invited {guest}";""",
        """public override string Invited(string host, string guest) => $"{host} has invited {guest}";""",
        "british")]
    public async Task ParametersTheOtherWayRound_ShouldTakeTheContractsOrderAndKeepTheBody(string declaration, string expectedDeclaration, string set)
    {
        // Arrange
        string Source(string text) => set is "british" ? Sets($"{DutchInvited}\n{DutchItemsInCart}", text) : Sets($"{text}\n{DutchItemsInCart}");

        // Act
        var fixes = await FixAsync(Source(declaration));

        // Assert: one fix per parameter reported, each the same. Each use reads the parameter of its name, whether the
        // method implements the text, implements it explicitly, or overrides the default set's.
        fixes.Length.ShouldBe(2);

        foreach (var fix in fixes)
        {
            fix.Title.ShouldBe("Put the parameter names in the contract's order");
            fix.Sources.ShouldHaveSingleItem().ShouldBe(Source(expectedDeclaration));
        }

        await ShouldReportNothingAsync(Source(expectedDeclaration));
    }

    [Test]
    public async Task ParametersTheOtherWayRound_ShouldKeepTheirDocComments()
    {
        // Arrange: three parameters, each a name of the contract, with doc comments that describe each by its name.
        const string Contract = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Shipped(string shop, string order, int days) => $"{shop} shipped {order} in {days} days";
            }
            """;

        const string Dutch = """

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                /// <param name="order">The order.</param>
                /// <param name="shop">The shop.</param>
                public string Shipped(string order, string shop, int days) => $"{shop} heeft {order} in {days} dagen verzonden";
            }
            """;

        // Act
        var fixes = await FixAsync(Contract + Dutch);

        // Assert
        var expected = Contract + Dutch.Replace("Shipped(string order, string shop, int days)", "Shipped(string shop, string order, int days)", StringComparison.Ordinal);

        fixes.Length.ShouldBe(2);
        fixes.ShouldAllBe(fix => fix.Sources.Single() == expected);
        await ShouldReportNothingAsync(expected);
    }

    [Test]
    public async Task ParameterWithANameTheContractLacks_ShouldBeRenamedWithItsUses()
    {
        // Arrange: uses in the body, in nameof, and in doc comments, next to a lambda parameter of the same name.
        const string Dutch = """
            /// <summary>Hoeveel artikelen, met <paramref name="aantal"/>.</summary>
            /// <param name="aantal">Het aantal artikelen.</param>
            /// <param name="shop">De winkel.</param>
            public string ItemsInCart(int aantal, string shop)
            {
                System.Func<int, int> twice = aantal => aantal * 2;
                return $"{aantal} artikelen ({nameof(aantal)}, {twice(1)}) in je winkelwagen bij {shop}";
            }
            """;

        var source = Sets($"{DutchInvited}\n{Dutch}");

        // Act
        var fixes = await FixAsync(source);

        // Assert: the parameter and what reads it are renamed, and the lambda's own parameter stays.
        var renamed = Dutch
            .Replace("<paramref name=\"aantal\"/>", "<paramref name=\"count\"/>", StringComparison.Ordinal)
            .Replace("<param name=\"aantal\">", "<param name=\"count\">", StringComparison.Ordinal)
            .Replace("(int aantal, string shop)", "(int count, string shop)", StringComparison.Ordinal)
            .Replace("$\"{aantal} artikelen ({nameof(aantal)}", "$\"{count} artikelen ({nameof(count)}", StringComparison.Ordinal);

        var expected = Sets($"{DutchInvited}\n{renamed}");

        var fix = fixes.ShouldHaveSingleItem();
        fix.Title.ShouldBe("Rename 'aantal' to 'count'");
        fix.Sources.ShouldHaveSingleItem().ShouldBe(expected);
        await ShouldReportNothingAsync(expected);
    }

    [Test]
    public async Task ParametersWithNamesTheContractLacks_ShouldBeRenamedAtOnce()
    {
        // Arrange
        const string Dutch = """public string Invited(string gastheer, string gast) => $"{gastheer} heeft {gast} uitgenodigd";""";

        // Act
        var fixes = await FixAsync(Sets($"{Dutch}\n{DutchItemsInCart}"));

        // Assert
        var expected = Sets($"{DutchInvited}\n{DutchItemsInCart}");

        fixes.Length.ShouldBe(2);
        fixes.ShouldAllBe(fix => fix.Title == "Rename the parameters as the contract names them" && fix.Sources.Single() == expected);
        await ShouldReportNothingAsync(expected);
    }

    [Test]
    [Arguments("""public string Invited(string guest, string gast) => $"{guest} heeft {gast} uitgenodigd";""")]
    [Arguments("""public string Invited(string gastheer, string host) => $"{gastheer} heeft {host} uitgenodigd";""")]
    public async Task NamesThatMixAnOrderAndARename_ShouldGetNoFix(string dutch)
    {
        // Arrange & Act: a name of the contract at another position, next to one the contract lacks.
        var fixes = await FixAsync(Sets($"{dutch}\n{DutchItemsInCart}"));

        // Assert
        fixes.ShouldBeEmpty();
    }

    [Test]
    public async Task NamesTheOtherWayRoundOnParametersOfDifferentTypes_ShouldGetNoFix()
    {
        // Arrange & Act: the contract's names swapped over a number and a string. The body is written for the
        // positions, so moving the names alone would show the shop where the count belongs.
        var fixes = await FixAsync(Sets($$"""
            {{DutchInvited}}
            public string ItemsInCart(int shop, string count) => $"{shop} artikelen bij {count}";
            """));

        // Assert
        fixes.ShouldBeEmpty();
    }

    [Test]
    [Arguments("""public string ItemsInCart(int aantal, string shop) { var count = aantal; return $"{count} artikelen bij {shop}"; }""")]
    [Arguments("""public string ItemsInCart(int aantal, string shop) => $"{aantal} van {count} artikelen bij {shop}"; private int count = 3;""")]
    public async Task NewNameThatTheMethodUses_ShouldGetNoFix(string dutch)
    {
        // Arrange & Act: a local variable, or a field the method reads, of the contract's name.
        var fixes = await FixAsync(Sets($"{DutchInvited}\n{dutch}"));

        // Assert
        fixes.ShouldBeEmpty();
    }

    [Test]
    public async Task PartialMethod_ShouldGetNoFix()
    {
        // Arrange & Act
        var fixes = await FixAsync(Sets($$"""
            {{DutchInvited}}
            public partial string ItemsInCart(int aantal, string shop);
            public partial string ItemsInCart(int aantal, string shop) => $"{aantal} artikelen bij {shop}";
            """).Replace("public class ShopLocalizationNl", "public partial class ShopLocalizationNl", StringComparison.Ordinal));

        // Assert
        fixes.ShouldBeEmpty();
    }

    [Test]
    public async Task SetOfContractOfAnotherAssembly_ShouldGetTheNamesOfThatContract()
    {
        // Arrange: an app adds German to a library, in an assembly that declares no languages itself.
        var library = GeneratorTestHelper.CompileLibraryWithGenerators("ParameterNamesFixShopLibrary", Sets($"{DutchInvited}\n{DutchItemsInCart}"));

        const string App = """
            using TestApp;

            namespace App;

            public sealed class ShopLocalizationDe : CodeChops.Idioma.ILocalizationSet, IShopLocalization
            {
                public string Invited(string guest, string host) => $"{host} hat {guest} eingeladen";
                public string ItemsInCart(int anzahl, string shop) => $"{anzahl} Artikel im Warenkorb bei {shop}";
            }
            """;

        // Act
        var fixes = await CodeFixTestHelper.FixAfterGeneratorsAsync(Provider, Analyzer, [library], App);

        // Assert: two diagnostics for the swap and one for the rename.
        fixes.Length.ShouldBe(3);
        fixes.Select(static fix => fix.Sources.Single()).Distinct(StringComparer.Ordinal).ShouldBe(
        [
            App.Replace("Invited(string guest, string host)", "Invited(string host, string guest)", StringComparison.Ordinal),
            App.Replace("(int anzahl, string shop) => $\"{anzahl}", "(int count, string shop) => $\"{count}", StringComparison.Ordinal),
        ], ignoreOrder: true);
    }

    [Test]
    public async Task FixAll_ShouldNameEveryTextOnce()
    {
        // Arrange: a swap with two diagnostics, and a rename in another set.
        var source = Sets(
            $$"""
            public string Invited(string guest, string host) => $"{host} heeft {guest} uitgenodigd";
            public string ItemsInCart(int aantal, string shop) => $"{aantal} artikelen in je winkelwagen bij {shop}";
            """,
            """public override string ItemsInCart(int number, string shop) => $"{number} items in your basket at {shop}";""");

        // Act
        var fixedSource = (await CodeFixTestHelper.FixAllAfterGeneratorsAsync(Provider, Analyzer, source)).ShouldHaveSingleItem();

        // Assert
        var expected = Sets(
            $$"""
            {{DutchInvited}}
            {{DutchItemsInCart}}
            """,
            """public override string ItemsInCart(int count, string shop) => $"{count} items in your basket at {shop}";""");

        fixedSource.ShouldBe(expected);
        await ShouldReportNothingAsync(expected);
    }
}
