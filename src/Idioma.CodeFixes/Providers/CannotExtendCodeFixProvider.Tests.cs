using System.Collections.Immutable;
using CodeChops.Idioma.CodeFixes;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis.CodeFixes;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="CannotExtendCodeFixProvider"/>: a default-language set the generator can't use (<c>LOC200</c>)
/// gets a fix where one edit resolves it, after which the generator writes its contract and texts.
/// </summary>
public sealed class CannotExtendCodeFixProviderTests
{
    #region Fixture
    private static string File(string declarations) => $"""
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en")]

        namespace TestApp;

        {declarations}
        """;

    private static Task<ImmutableArray<(string Title, ImmutableArray<string> Sources)>> FixAsync(params string[] sources)
    {
        return CodeFixTestHelper.FixAsync(new CannotExtendCodeFixProvider(), LocalizationParts.Sets(), sources);
    }

    private static void ShouldBeExtended(params string[] sources)
    {
        var run = GeneratorTestHelper.RunOverFiles(LocalizationParts.Sets(), sources).Results.ShouldHaveSingleItem();

        run.Diagnostics.ShouldBeEmpty();
        run.GeneratedSources.ShouldNotBeEmpty();
    }
    #endregion

    [Test]
    [Arguments(
        "public abstract class ShopLocalizationEn : ILocalizationSet, IShopLocalization { public string Checkout => \"Checkout\"; }",
        "Remove 'abstract' from 'ShopLocalizationEn'",
        "public class ShopLocalizationEn : ILocalizationSet, IShopLocalization { public string Checkout => \"Checkout\"; }")]
    [Arguments(
        "// The shop.\nabstract class ShopLocalizationEn : ILocalizationSet, IShopLocalization { public string Checkout => \"Checkout\"; }",
        "Remove 'abstract' from 'ShopLocalizationEn'",
        "// The shop.\nclass ShopLocalizationEn : ILocalizationSet, IShopLocalization { public string Checkout => \"Checkout\"; }")]
    [Arguments(
        "file sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization { public string Checkout => \"Checkout\"; }",
        "Remove 'file' from 'ShopLocalizationEn'",
        "sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization { public string Checkout => \"Checkout\"; }")]
    public async Task SetOneEditFixes_ShouldGetThatEdit(string declaration, string expectedTitle, string expectedDeclaration)
    {
        // Arrange & Act
        var fixes = await FixAsync(File(declaration));

        // Assert: nothing but the modifier changes, and the generator uses the set from then on.
        var fix = fixes.ShouldHaveSingleItem();
        fix.Title.ShouldBe(expectedTitle);
        fix.Sources.ShouldHaveSingleItem().ShouldBe(File(expectedDeclaration));
        ShouldBeExtended([.. fix.Sources]);
    }

    [Test]
    public async Task SetNotNamedAfterItsLanguage_ShouldBeRenamedEverywhere()
    {
        // Arrange
        var set = File("""
            public sealed class ShopLocalization : ILocalizationSet, IShopLocalization
            {
                public string LocalizationLanguageCode => "en";
                public string Checkout => "Checkout";
            }
            """);
        const string User = """
            namespace TestApp;

            public static class Shop
            {
                public static string Checkout => new ShopLocalization().Checkout;
            }
            """;

        // Act
        var fixes = await FixAsync(set, User);

        // Assert: the class carries its language, also where another file names it.
        var fix = fixes.ShouldHaveSingleItem();
        fix.Title.ShouldBe("Rename to 'ShopLocalizationEn'");
        fix.Sources[0].ShouldBe(set.Replace("class ShopLocalization ", "class ShopLocalizationEn ", StringComparison.Ordinal));
        fix.Sources[1].ShouldBe(User.Replace("new ShopLocalization()", "new ShopLocalizationEn()", StringComparison.Ordinal));
        ShouldBeExtended([.. fix.Sources]);
    }

    [Test]
    public async Task SetNamedAfterItsLanguageWithoutCapital_ShouldGetItsSuffixInPlaceOfThatEnding()
    {
        // Arrange: LocalizationLanguageCode gives the set its language. Its name ends in that code without the capital that starts
        // a suffix, so the name gives no language.
        var set = File("""
            public sealed class ShopLocalizationen : ILocalizationSet, IShopLocalization
            {
                public string LocalizationLanguageCode => "en";
                public string Checkout => "Checkout";
            }
            """);

        // Act
        var fixes = await FixAsync(set);

        // Assert: not ShopLocalizationenEn, whose generated class would be ShopLocalizationen.
        var fix = fixes.ShouldHaveSingleItem();
        fix.Title.ShouldBe("Rename to 'ShopLocalizationEn'");
        fix.Sources[0].ShouldBe(set.Replace("class ShopLocalizationen ", "class ShopLocalizationEn ", StringComparison.Ordinal));
        ShouldBeExtended([.. fix.Sources]);
    }

    [Test]
    public async Task PartialSetRepeatingModifier_ShouldLoseItInEveryPart()
    {
        // Arrange
        var first = File("public abstract class ShopLocalizationEn : ILocalizationSet, IShopLocalization { public string Checkout => \"Checkout\"; }");
        const string Second = "namespace TestApp; public abstract partial class ShopLocalizationEn { public string Pay => \"Pay\"; }";

        // Act
        var fixes = await FixAsync(first, Second);

        // Assert
        var fix = fixes.ShouldHaveSingleItem();
        fix.Sources[0].ShouldBe(first.Replace("abstract ", "", StringComparison.Ordinal));
        fix.Sources[1].ShouldBe(Second.Replace("abstract ", "", StringComparison.Ordinal));
        ShouldBeExtended([.. fix.Sources]);
    }

    [Test]
    public async Task AbstractSetWithAbstractMember_ShouldGetNoFix()
    {
        // Arrange: without the modifier the class would no longer compile.
        var source = File("public abstract class ShopLocalizationEn : ILocalizationSet, IShopLocalization { public abstract string Checkout { get; } }");

        // Act
        var fixes = await FixAsync(source);

        // Assert: no fix that would change nothing.
        fixes.ShouldBeEmpty();
    }

    [Test]
    [Arguments("public abstract class ShopBase { public abstract string Checkout { get; } }", 0)]
    [Arguments("public abstract class ShopRoot { public abstract string Checkout { get; } } public abstract class ShopBase : ShopRoot { public abstract override string Checkout { get; } }", 0)]
    [Arguments("public abstract class ShopRoot { public abstract string Checkout { get; } } public abstract class ShopBase : ShopRoot { public override string Checkout => \"Checkout\"; }", 1)]
    public async Task AbstractSetInheritingAbstractMember_ShouldGetFixOnlyWhereItIsOverridden(string bases, int expected)
    {
        // Arrange: a set that declares no abstract member, but inherits one. Without the modifier, a class that doesn't
        // override it doesn't compile (CS0534). A class in between that overrides it implements it.
        var source = File($"{bases} public abstract class ShopLocalizationEn : ShopBase, ILocalizationSet, IShopLocalization {{ }}");

        // Act
        var fixes = await FixAsync(source);

        // Assert
        fixes.Count(static fix => fix.Title.StartsWith("Remove 'abstract'", StringComparison.Ordinal)).ShouldBe(expected);
    }

    [Test]
    public async Task FixAll_SetsNotNamedAfterTheirLanguage_ShouldAllBeRenamedInEveryFile()
    {
        // Arrange: two sets whose names give no language, and a file that names both.
        var shop = File("public sealed class ShopLocalization : ILocalizationSet, IShopLocalization { public string Checkout => \"Checkout\"; }");
        const string Cart = "using CodeChops.Idioma; namespace TestApp; public sealed class CartLocalization : ILocalizationSet, ICartLocalization { public string Total => \"Total\"; }";
        const string User = """
            namespace TestApp;

            public static class Shop
            {
                public static string Checkout => new ShopLocalization().Checkout;
                public static string Total => new CartLocalization().Total;
            }
            """;

        // Act
        var sources = await CodeFixTestHelper.FixAllAsync(new CannotExtendCodeFixProvider(), LocalizationParts.Sets(), FixAllScope.Solution, fromFile: 0, shop, Cart, User);

        // Assert: the file that names both sets takes both renames.
        sources[0].ShouldBe(shop.Replace("class ShopLocalization ", "class ShopLocalizationEn ", StringComparison.Ordinal));
        sources[1].ShouldBe(Cart.Replace("class CartLocalization ", "class CartLocalizationEn ", StringComparison.Ordinal));
        sources[2].ShouldBe(User
            .Replace("new ShopLocalization()", "new ShopLocalizationEn()", StringComparison.Ordinal)
            .Replace("new CartLocalization()", "new CartLocalizationEn()", StringComparison.Ordinal));
        ShouldBeExtended([.. sources]);
    }

    [Test]
    [Arguments("public sealed class ShopLocalizationEn<T> : ILocalizationSet, IShopLocalization { public string Checkout => \"Checkout\"; }")]
    [Arguments("public static partial class Container { public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization { public string Checkout => \"Checkout\"; } }")]
    [Arguments("public sealed class ShopLocalizationEn(string shopName) : ILocalizationSet, IShopLocalization { public string Checkout => shopName; }")]
    public async Task SetNoSingleEditFixes_ShouldGetNoFix(string declaration)
    {
        // Arrange & Act: where the class moves to, what closes its type parameter, or what its constructor can do
        // without, is for its author to decide.
        var fixes = await FixAsync(File(declaration));

        // Assert
        fixes.ShouldBeEmpty();
    }

    [Test]
    public async Task SetWhoseNameGivesNoLanguage_ShouldBeRenamedAfterTheDefaultLanguage()
    {
        // Arrange: no LocalizationLanguageCode gives the set a language, and its name ends in none.
        var set = File("""
            public sealed class ShopLocalization : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }
            """);

        // Act
        var fixes = await FixAsync(set);

        // Assert
        var fix = fixes.ShouldHaveSingleItem();
        fix.Title.ShouldBe("Rename to 'ShopLocalizationEn'");
        fix.Sources[0].ShouldBe(set.Replace("class ShopLocalization ", "class ShopLocalizationEn ", StringComparison.Ordinal));
        ShouldBeExtended([.. fix.Sources]);
    }
}
