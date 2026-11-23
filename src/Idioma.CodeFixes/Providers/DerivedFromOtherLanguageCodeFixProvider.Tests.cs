using System.Collections.Immutable;
using CodeChops.Idioma.CodeFixes;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="DerivedFromOtherLanguageCodeFixProvider"/>: a set that derives from a set of another language
/// (<c>LOC203</c>) implements the contract instead, and a region that derives from another region derives from the set
/// of its language.
/// </summary>
public sealed class DerivedFromOtherLanguageCodeFixProviderTests
{
    #region Fixture
    private const string Header = """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace TestApp;

        public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
        {
            public virtual string Checkout => "Checkout";

            public virtual string Cart => "Cart";
        }
        """;

    private static Task<ImmutableArray<(string Title, ImmutableArray<string> Sources)>> FixAsync(params string[] sources)
    {
        return CodeFixTestHelper.FixWithGeneratedCodeAsync(new DerivedFromOtherLanguageCodeFixProvider(), LocalizationParts.Sets(), sources);
    }
    #endregion

    [Test]
    public async Task SetOfAnotherLanguage_ShouldImplementTheContractInstead()
    {
        // Arrange: a Dutch set that overrides one English text, hides another, and overrides ToString().
        const string set = """
            public class ShopLocalizationNl : ShopLocalizationEn // Dutch
            {
                public override string Checkout => "Afrekenen";

                public new string Cart => "Winkelwagen";

                public override string ToString() => "Nederlands";
            }
            """;

        // Act: written with the line endings of this file, as the expected source below is.
        var fixes = await FixAsync($$"""
            {{Header}}

            {{set}}
            """);

        // Assert: the texts implement the contract, and ToString() still overrides.
        var fix = fixes.ShouldHaveSingleItem();
        fix.Title.ShouldBe("Implement 'IShopLocalization' instead");

        var source = fix.Sources.ShouldHaveSingleItem();
        source.ShouldBe($$"""
            {{Header}}

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization // Dutch
            {
                public string Checkout => "Afrekenen";

                public string Cart => "Winkelwagen";

                public override string ToString() => "Nederlands";
            }
            """);

        GeneratorTestHelper.GetGeneratorDiagnostics(source).ShouldBeEmpty();
        GeneratorTestHelper.CompileWithGenerators(source).ShouldBeEmpty();
    }

    [Test]
    public async Task SetThatLacksTexts_ShouldBeToldWhichByTheCompiler()
    {
        // Arrange
        const string set = "public sealed class ShopLocalizationNl : ShopLocalizationEn\n{\n    public sealed override string Checkout => \"Afrekenen\";\n}";

        // Act
        var fixes = await FixAsync($"{Header}\n\n{set}");

        // Assert: 'sealed' goes with 'override', and the compiler names the text Dutch lacks.
        var source = fixes.ShouldHaveSingleItem().Sources.ShouldHaveSingleItem();
        source.ShouldBe($"{Header}\n\npublic sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization\n{{\n    public string Checkout => \"Afrekenen\";\n}}");

        var error = GeneratorTestHelper.CompileWithGenerators(source).ShouldHaveSingleItem();
        error.Id.ShouldBe("CS0535");
        error.GetMessage(System.Globalization.CultureInfo.InvariantCulture).ShouldContain("Cart");
    }

    [Test]
    public async Task PartialSet_ShouldChangeEveryPart()
    {
        // Arrange: the base list in one file, the texts in another.
        const string first = "namespace TestApp;\n\npublic partial class ShopLocalizationNl : ShopLocalizationEn;\n";
        const string second = "namespace TestApp;\n\npublic partial class ShopLocalizationNl\n{\n    public override string Checkout => \"Afrekenen\";\n\n    public override string Cart => \"Winkelwagen\";\n}\n";

        // Act
        var fixes = await FixAsync(Header, first, second);

        // Assert: the file without a using of the package names the interface in full.
        var fix = fixes.ShouldHaveSingleItem();
        fix.Sources[0].ShouldBe(Header);
        fix.Sources[1].ShouldBe("namespace TestApp;\n\npublic partial class ShopLocalizationNl : CodeChops.Idioma.ILocalizationSet, IShopLocalization;\n");
        fix.Sources[2].ShouldBe(second.Replace("public override", "public", StringComparison.Ordinal));
    }

    [Test]
    public async Task RegionThatDerivesFromAnotherRegion_ShouldAlsoBeOfferedTheSetOfItsLanguage()
    {
        // Arrange: American English derives from British English, which derives from English.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "en-GB", "en-US")]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Color => "Color";

                public virtual string Total => "Total";
            }

            public class ShopLocalizationEnGb : ShopLocalizationEn
            {
                public override string Color => "Colour";
            }

            public class ShopLocalizationEnUs : ShopLocalizationEnGb
            {
                public override string Total => "Total due";
            }
            """;

        // Act
        var fixes = await FixAsync(source);

        // Assert: deriving from English comes first, and leaves the build nothing to report.
        fixes.Length.ShouldBe(2);
        fixes[0].Title.ShouldBe("Derive from 'ShopLocalizationEn' instead");
        fixes[1].Title.ShouldBe("Implement 'IShopLocalization' instead");

        var derived = fixes[0].Sources.ShouldHaveSingleItem();
        derived.ShouldBe(source.Replace("ShopLocalizationEnUs : ShopLocalizationEnGb", "ShopLocalizationEnUs : ShopLocalizationEn", StringComparison.Ordinal));
        GeneratorTestHelper.GetGeneratorDiagnostics(derived).ShouldBeEmpty();
        GeneratorTestHelper.CompileWithGenerators(derived).ShouldBeEmpty();
    }
}
