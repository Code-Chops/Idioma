using System.Collections.Immutable;
using CodeChops.Idioma.CodeFixes;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="MissingContractCodeFixProvider"/>: a set that lists <c>ILocalizationSet</c> without its contract
/// (<c>LOC204</c>) gets the contract next to it, when the build generates one.
/// </summary>
public sealed class MissingContractCodeFixProviderTests
{
    #region Fixture
    private const string Header = """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace TestApp;
        """;

    private const string DefaultSet = """
        public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
        {
            public string Checkout => "Checkout";
        }
        """;

    private static Task<ImmutableArray<(string Title, ImmutableArray<string> Sources)>> FixAsync(params string[] sources)
    {
        return CodeFixTestHelper.FixAsync(new MissingContractCodeFixProvider(), new LocalizationSetsGenerator(), sources);
    }

    private static void ShouldReportNothing(params string[] sources)
    {
        var run = GeneratorTestHelper.RunOverFiles(new LocalizationSetsGenerator(), sources).Results.ShouldHaveSingleItem();

        run.Diagnostics.ShouldBeEmpty();
    }
    #endregion

    [Test]
    [Arguments("public sealed class ShopLocalizationNl : ILocalizationSet", "public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization")]
    [Arguments("public sealed class ShopLocalizationNl : CodeChops.Idioma.ILocalizationSet // Dutch", "public sealed class ShopLocalizationNl : CodeChops.Idioma.ILocalizationSet, IShopLocalization // Dutch")]
    [Arguments("public sealed class ShopLocalizationNl : ILocalizationSet, System.IDisposable", "public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization, System.IDisposable")]
    public async Task SetListingOnlyILocalizationSet_ShouldGetItsContractNextToIt(string declaration, string expectedDeclaration)
    {
        // Arrange
        const string Body = """
            {
                public string Checkout => "Afrekenen";
                public void Dispose() { }
            }
            """;
        var source = $"{Header}\n\n{DefaultSet}\n\n{declaration}\n{Body}";

        // Act
        var fixes = await FixAsync(source);

        // Assert: only the base list changes, and the set then counts as the Dutch set of its contract.
        var fix = fixes.ShouldHaveSingleItem();
        fix.Title.ShouldBe("Implement 'IShopLocalization'");
        fix.Sources.ShouldHaveSingleItem().ShouldBe($"{Header}\n\n{DefaultSet}\n\n{expectedDeclaration}\n{Body}");
        ShouldReportNothing([.. fix.Sources]);
    }

    [Test]
    public async Task PartialSetListingILocalizationSetInAnotherFile_ShouldBeFixedThere()
    {
        // Arrange: the part the diagnostic sits on is not the part that lists the interface.
        const string first = $$"""
            {{Header}}

            {{DefaultSet}}

            public sealed partial class ShopLocalizationNl
            {
                public string Checkout => "Afrekenen";
            }
            """;
        const string Second = "using CodeChops.Idioma; namespace TestApp; public sealed partial class ShopLocalizationNl : ILocalizationSet;";

        // Act
        var fixes = await FixAsync(first, Second);

        // Assert
        var fix = fixes.ShouldHaveSingleItem();
        fix.Sources[0].ShouldBe(first);
        fix.Sources[1].ShouldBe(Second.Replace(": ILocalizationSet;", ": ILocalizationSet, IShopLocalization;", StringComparison.Ordinal));
        ShouldReportNothing([.. fix.Sources]);
    }

    [Test]
    public async Task DefaultSetListingOnlyILocalizationSet_ShouldGetItsContract()
    {
        // Arrange: the shape a default-language set had while the generator declared its contract for it.
        const string source = $$"""
            {{Header}}

            public sealed partial class ShopLocalizationEn : ILocalizationSet
            {
                public string Checkout => "Checkout";
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """;

        // Act
        var fixes = await FixAsync(source);

        // Assert: partial does no harm, so it stays.
        var fix = fixes.ShouldHaveSingleItem();
        fix.Title.ShouldBe("Implement 'IShopLocalization'");
        fix.Sources.ShouldHaveSingleItem().ShouldBe(source.Replace("ShopLocalizationEn : ILocalizationSet", "ShopLocalizationEn : ILocalizationSet, IShopLocalization", StringComparison.Ordinal));
        ShouldReportNothing([.. fix.Sources]);
    }

    [Test]
    public async Task SetWithoutGeneratedContract_ShouldGetNoFix()
    {
        // Arrange: no default-language set, so the build generates no IShopLocalization to implement.
        const string source = $$"""
            {{Header}}

            public sealed class ShopLocalizationNl : ILocalizationSet
            {
                public string Checkout => "Afrekenen";
            }
            """;

        // Act
        var fixes = await FixAsync(source);

        // Assert
        fixes.ShouldBeEmpty();
    }
}
