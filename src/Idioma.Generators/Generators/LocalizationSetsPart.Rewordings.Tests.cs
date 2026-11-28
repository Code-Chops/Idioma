using System.Collections.Immutable;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for the rewordings the generator reports: one that doesn't reach a regional set of the library it rewords
/// (<c>LOC208</c>), and rewordings of one set that don't add up (<c>LOC209</c>). The libraries are compiled with the
/// generators, so the build of the rewording reads their sets from <c>[assembly: LocalizationSet]</c>.
/// </summary>
public sealed class LocalizationSetsPartRewordingsTests
{
    #region Fixture
    /// <summary>
    /// A library with an English set and a British set deriving from it.
    /// </summary>
    private static MetadataReference RegionalShop { get; } = GeneratorTestHelper.CompileLibraryWithGenerators("RegionalShop", """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "en-GB")]

        namespace Shop;

        public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
        {
            public virtual string Cart => "Cart";
            public virtual string Checkout => "Checkout";
        }

        public class ShopLocalizationEnGb : ShopLocalizationEn
        {
            public override string Cart => "Basket";
        }
        """);

    /// <summary>
    /// A library with an English set alone.
    /// </summary>
    private static MetadataReference Shop { get; } = GeneratorTestHelper.CompileLibraryWithGenerators("Shop", """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en")]

        namespace Shop;

        public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
        {
            public virtual string Cart => "Cart";
            public virtual string Checkout => "Checkout";
        }
        """);

    /// <summary>
    /// A shared library that rewords the checkout of <see cref="Shop"/>.
    /// </summary>
    private static MetadataReference Shared { get; } = Rewording("Shared", "SharedShopLocalizationEn", "Checkout", "Pay");

    /// <summary>
    /// Another library that rewords the cart of <see cref="Shop"/>, unaware of <see cref="Shared"/>.
    /// </summary>
    private static MetadataReference Other { get; } = Rewording("Other", "OtherShopLocalizationEn", "Cart", "Bag");

    /// <summary>
    /// A library with an English set and a special English set deriving from it.
    /// </summary>
    private static MetadataReference SpecialShop { get; } = GeneratorTestHelper.CompileLibraryWithGenerators("SpecialShop", """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en")]

        namespace Shop;

        public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
        {
            public virtual string Cart => "Cart";
            public virtual string Checkout => "Checkout";
        }

        public class SpecialShopLocalizationEn : ShopLocalizationEn
        {
            public override string Cart => "Special cart";
        }
        """);

    private static MetadataReference Rewording(string assemblyName, string setName, string text, string wording)
    {
        return GeneratorTestHelper.CompileLibraryWithGenerators(assemblyName, $$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace {{assemblyName}};

            public class {{setName}} : Shop.ShopLocalizationEn
            {
                public override string {{text}} => "{{wording}}";
            }
            """, Shop);
    }

    private static string App(string sets) => $"""
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en")]

        namespace TestApp;

        {sets}
        """;

    /// <summary>
    /// The rewording diagnostics the generators report for the app's sets.
    /// </summary>
    private static ImmutableArray<Diagnostic> Analyze(string sets, params MetadataReference[] references)
    {
        return [.. GeneratorTestHelper.GetGeneratorDiagnostics(App(sets), references).Where(static diagnostic => diagnostic.Id is "LOC208" or "LOC209")];
    }

    private static string At(Diagnostic diagnostic)
    {
        return diagnostic.Location.SourceTree!.ToString()[diagnostic.Location.SourceSpan.Start..];
    }
    #endregion

    [Test]
    public void RewordingOfLanguageWithRegionalSet_ShouldReportLOC208()
    {
        // Arrange & Act: British users see the library's British set, which the rewording of English doesn't reach.
        var diagnostics = Analyze("""
            public sealed class MyShopLocalizationEn : Shop.ShopLocalizationEn
            {
                public override string Checkout => "Pay";
            }
            """, RegionalShop);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(LocalizationSetsPart.UnreachedRegionalSetDiagnosticId);
        diagnostic.Id.ShouldBe("LOC208");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().ShouldBe("'MyShopLocalizationEn' rewords 'ShopLocalizationEn', but users of 'en-GB' see 'ShopLocalizationEnGb', which it doesn't reach. Add 'class MyShopLocalizationEnGb : ShopLocalizationEnGb' to reword it too.");
        At(diagnostic).ShouldStartWith("MyShopLocalizationEn :");
    }

    [Test]
    [Arguments("""
        public sealed class MyShopLocalizationEnGb : Shop.ShopLocalizationEnGb
        {
            public override string Checkout => "Pay";
        }
        """)]
    [Arguments("""
        [ExcludeFromLocalizationDiscovery]
        public sealed class MyShopLocalizationEnGb : Shop.ShopLocalizationEnGb
        {
            public override string Checkout => "Pay";
        }
        """)]
    [Arguments("""
        public sealed class OwnShopLocalizationEnGb : ILocalizationSet, Shop.IShopLocalization
        {
            public string Cart => "Basket";
            public string Checkout => "Pay";
        }
        """)]
    public void RewordingWithSetOfTheRegion_ShouldReportNothing(string british)
    {
        // Arrange & Act: a rewording of the British set, one registered by hand, or a British set of its own.
        var diagnostics = Analyze($$"""
            public sealed class MyShopLocalizationEn : Shop.ShopLocalizationEn
            {
                public override string Checkout => "Pay";
            }

            {{british}}
            """, RegionalShop);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public void RewordingRegisteredByHand_ShouldReportNothing()
    {
        // Arrange: it wins over every other set, next to a regional set and next to a library's rewording.
        const string rewording = """
            [ExcludeFromLocalizationDiscovery]
            public sealed class MyShopLocalizationEn : Shop.ShopLocalizationEn
            {
                public override string Checkout => "Pay";
            }
            """;

        // Act
        var nextToRegionalSet = Analyze(rewording, RegionalShop);
        var nextToRewording = Analyze(rewording, Shop, Shared);

        // Assert
        nextToRegionalSet.ShouldBeEmpty();
        nextToRewording.ShouldBeEmpty();
    }

    [Test]
    public void RewordingThatCannotBeRegistered_ShouldReportNothing()
    {
        // Arrange: without a parameterless constructor it's never registered (LOC207), so it doesn't show next to a
        // regional set or next to a library's rewording.
        const string rewording = """
            public sealed class MyShopLocalizationEn(string wording) : Shop.ShopLocalizationEn
            {
                public override string Checkout => wording;
            }
            """;

        // Act
        var nextToRegionalSet = Analyze(rewording, RegionalShop);
        var nextToRewording = Analyze(rewording, Shop, Shared);

        // Assert
        nextToRegionalSet.ShouldBeEmpty();
        nextToRewording.ShouldBeEmpty();
    }

    [Test]
    public void TwoRewordingsOfOneSetInThisProject_ShouldReportLOC209AtTheOneThatDoesNotShow()
    {
        // Arrange & Act: registered by name, so the second one wins.
        var diagnostics = Analyze("""
            public sealed class AppShopLocalizationEn : Shop.ShopLocalizationEn
            {
                public override string Checkout => "Pay";
            }

            public sealed class OtherShopLocalizationEn : Shop.ShopLocalizationEn
            {
                public override string Cart => "Bag";
            }
            """, Shop);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(LocalizationSetsPart.RewordingsDontAddUpDiagnosticId);
        diagnostic.Id.ShouldBe("LOC209");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().ShouldBe("'OtherShopLocalizationEn' and 'AppShopLocalizationEn' both reword 'ShopLocalizationEn', so only 'OtherShopLocalizationEn' shows. Merge them into one set, or derive one from the other, so both show.");
        At(diagnostic).ShouldStartWith("AppShopLocalizationEn :");
    }

    [Test]
    public void RewordingOfThisProjectNextToOneOfALibrary_ShouldReportLOC209AtThisProjectsRewording()
    {
        // Arrange & Act: the shared library's rewording, read from metadata.
        var diagnostics = Analyze("""
            public sealed class AppShopLocalizationEn : Shop.ShopLocalizationEn
            {
                public override string Cart => "Bag";
            }
            """, Shop, Shared);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC209");
        diagnostic.GetMessage().ShouldBe("'AppShopLocalizationEn' and 'SharedShopLocalizationEn' both reword 'ShopLocalizationEn', so only 'AppShopLocalizationEn' shows. Derive 'AppShopLocalizationEn' from 'SharedShopLocalizationEn', so both show.");
        At(diagnostic).ShouldStartWith("AppShopLocalizationEn :");
    }

    [Test]
    public void RewordingThatDerivesFromTheReferencedOne_ShouldReportNothing()
    {
        // Arrange & Act: the app's rewording wins, with the texts of the shared one.
        var diagnostics = Analyze("""
            public sealed class AppShopLocalizationEn : Shared.SharedShopLocalizationEn
            {
                public override string Cart => "Bag";
            }
            """, Shop, Shared);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public void TwoRewordingsOfThisProjectAtDifferentDepths_ShouldNameTheDeeperOneAsTheOneThatShows()
    {
        // Arrange & Act: the registrar orders by the depth of every set base, so the rewording of the special set
        // registers last and wins, though its name sorts first.
        var diagnostics = Analyze("""
            public sealed class ZuluShopLocalizationEn : Shop.ShopLocalizationEn
            {
                public override string Checkout => "Pay";
            }

            public sealed class AlphaShopLocalizationEn : Shop.SpecialShopLocalizationEn
            {
                public override string Checkout => "Pay now";
            }
            """, SpecialShop);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC209");
        diagnostic.GetMessage().ShouldBe("'AlphaShopLocalizationEn' and 'ZuluShopLocalizationEn' both reword 'ShopLocalizationEn', so only 'AlphaShopLocalizationEn' shows. Merge them into one set, or derive one from the other, so both show.");
        At(diagnostic).ShouldStartWith("ZuluShopLocalizationEn :");
    }

    [Test]
    public void RewordingThatDerivesFromAnotherOfThisProject_ShouldReportNothing()
    {
        // Arrange & Act: the derived one registers after the one it derives from, and wins with the texts of both.
        var diagnostics = Analyze("""
            public class AppShopLocalizationEn : Shop.ShopLocalizationEn
            {
                public override string Cart => "Bag";
            }

            public sealed class OtherShopLocalizationEn : AppShopLocalizationEn
            {
                public override string Checkout => "Pay";
            }
            """, Shop);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public void RewordingsOfReferencedLibrariesAlone_ShouldReportLOC209OnceWithoutLocation()
    {
        // Arrange & Act: neither library references the other, so the build can't tell which registers last.
        var diagnostics = Analyze("public static class Anchor;", Shop, Shared, Other);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC209");
        diagnostic.Location.ShouldBe(Location.None);
        diagnostic.GetMessage().ShouldBe("'OtherShopLocalizationEn' and 'SharedShopLocalizationEn' both reword 'ShopLocalizationEn', so only one of them shows. Derive one from the other, so both show.");
    }

    [Test]
    public void TwoRewordingsThroughAnAbstractClassOfThisProject_ShouldReportLOC209AndNotLOC203()
    {
        // Arrange & Act: both reword the library's English set through a base of the app's own.
        var diagnostics = GeneratorTestHelper.GetGeneratorDiagnostics(App("""
            public abstract class AppShopBase : Shop.ShopLocalizationEn;

            public sealed class CheckoutShopLocalizationEn : AppShopBase
            {
                public override string Checkout => "Pay";
            }

            public sealed class CartShopLocalizationEn : AppShopBase
            {
                public override string Cart => "Bag";
            }
            """), Shop);

        // Assert: two rewordings that don't add up, not two sets of one language.
        diagnostics.Select(static diagnostic => diagnostic.Id).ShouldBe(["LOC209"]);
    }

    [Test]
    public void TwoRewordingsOfOneReferencedLibrary_ShouldBeLeftToThatLibrary()
    {
        // Arrange: a library with two rewordings of one set, which its own build reports.
        var twice = GeneratorTestHelper.CompileLibraryWithGenerators("Twice", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace Twice;

            public class CheckoutShopLocalizationEn : Shop.ShopLocalizationEn
            {
                public override string Checkout => "Pay";
            }

            public class CartShopLocalizationEn : Shop.ShopLocalizationEn
            {
                public override string Cart => "Bag";
            }
            """, Shop);

        // Act
        var diagnostics = Analyze("public static class Anchor;", Shop, twice);

        // Assert: nothing an app that references it could fix.
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public void RewordingOfThisProjectDerivedFromOneOfTwoReferencedRewordings_ShouldReportItAlone()
    {
        // Arrange & Act: the app's rewording of the shared one shows, and the other library's rewording doesn't.
        var diagnostics = Analyze("""
            public sealed class AppShopLocalizationEn : Shared.SharedShopLocalizationEn
            {
                public override string Cart => "Basket";
            }
            """, Shop, Shared, Other);

        // Assert: at the app's rewording, and not once more for the two referenced rewordings.
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.GetMessage().ShouldBe("'AppShopLocalizationEn' and 'OtherShopLocalizationEn' both reword 'ShopLocalizationEn', so only 'AppShopLocalizationEn' shows. Derive 'AppShopLocalizationEn' from 'OtherShopLocalizationEn', so both show.");
    }
}
