using System.Collections.Immutable;
using CodeChops.Idioma.CodeFixes;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="MissingContractCodeFixProvider"/>: a set that lists <c>ITranslations</c> instead of its contract
/// (<c>LOC007</c>) gets the contract in its place, when the build generates one.
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
        public sealed partial class ShopTranslationsEN : ITranslations
        {
            public string Checkout => "Checkout";
        }
        """;

    private static Task<ImmutableArray<(string Title, ImmutableArray<string> Sources)>> FixAsync(params string[] sources)
    {
        return CodeFixTestHelper.FixAsync(new MissingContractCodeFixProvider(), new TranslationSetsGenerator(), sources);
    }

    private static void ShouldReportNothing(params string[] sources)
    {
        var run = GeneratorTestHelper.RunOverFiles(new TranslationSetsGenerator(), sources).Results.ShouldHaveSingleItem();

        run.Diagnostics.ShouldBeEmpty();
    }
    #endregion

    [Test]
    [Arguments("public sealed class ShopTranslationsNL : ITranslations", "public sealed class ShopTranslationsNL : IShopTranslations")]
    [Arguments("public sealed class ShopTranslationsNL : CodeChops.Idioma.ITranslations // Dutch", "public sealed class ShopTranslationsNL : IShopTranslations // Dutch")]
    [Arguments("public sealed class ShopTranslationsNL : System.IDisposable, ITranslations", "public sealed class ShopTranslationsNL : System.IDisposable, IShopTranslations")]
    public async Task SetListingITranslations_ShouldImplementItsContractInstead(string declaration, string expectedDeclaration)
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
        fix.Title.ShouldBe("Implement 'IShopTranslations'");
        fix.Sources.ShouldHaveSingleItem().ShouldBe($"{Header}\n\n{DefaultSet}\n\n{expectedDeclaration}\n{Body}");
        ShouldReportNothing([.. fix.Sources]);
    }

    [Test]
    public async Task PartialSetListingITranslationsInAnotherFile_ShouldBeFixedThere()
    {
        // Arrange: the part the diagnostic sits on is not the part that lists the interface.
        var first = $$"""
            {{Header}}

            {{DefaultSet}}

            public sealed partial class ShopTranslationsNL
            {
                public string Checkout => "Afrekenen";
            }
            """;
        const string Second = "using CodeChops.Idioma; namespace TestApp; public sealed partial class ShopTranslationsNL : ITranslations;";

        // Act
        var fixes = await FixAsync(first, Second);

        // Assert
        var fix = fixes.ShouldHaveSingleItem();
        fix.Sources[0].ShouldBe(first);
        fix.Sources[1].ShouldBe(Second.Replace(": ITranslations", ": IShopTranslations", StringComparison.Ordinal));
        ShouldReportNothing([.. fix.Sources]);
    }

    [Test]
    public async Task SetWithoutGeneratedContract_ShouldGetNoFix()
    {
        // Arrange: no default-language set, so the build generates no IShopTranslations to implement.
        var source = $$"""
            {{Header}}

            public sealed class ShopTranslationsNL : ITranslations
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
