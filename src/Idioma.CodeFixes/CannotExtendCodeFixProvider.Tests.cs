using System.Collections.Immutable;
using CodeChops.Idioma.CodeFixes;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="CannotExtendCodeFixProvider"/>: a default-language set the generator cannot extend
/// (<c>LOC002</c>) gets a fix where one edit resolves it, after which the generator extends the set.
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
        return CodeFixTestHelper.FixAsync(new CannotExtendCodeFixProvider(), new TranslationSetsGenerator(), sources);
    }

    private static void ShouldBeExtended(params string[] sources)
    {
        var run = GeneratorTestHelper.RunOverFiles(new TranslationSetsGenerator(), sources).Results.ShouldHaveSingleItem();

        run.Diagnostics.ShouldBeEmpty();
        run.GeneratedSources.ShouldNotBeEmpty();
    }
    #endregion

    [Test]
    [Arguments(
        "public sealed class ShopTranslationsEN : ITranslationSet { public string Checkout => \"Checkout\"; }",
        "Make 'ShopTranslationsEN' partial",
        "public sealed partial class ShopTranslationsEN : ITranslationSet { public string Checkout => \"Checkout\"; }")]
    [Arguments(
        "/// <summary>The shop.</summary>\nclass ShopTranslationsEN : ITranslationSet { public string Checkout => \"Checkout\"; }",
        "Make 'ShopTranslationsEN' partial",
        "/// <summary>The shop.</summary>\npartial class ShopTranslationsEN : ITranslationSet { public string Checkout => \"Checkout\"; }")]
    [Arguments(
        "public sealed record ShopTranslationsEN : ITranslationSet { public string Checkout => \"Checkout\"; }",
        "Make 'ShopTranslationsEN' partial",
        "public sealed partial record ShopTranslationsEN : ITranslationSet { public string Checkout => \"Checkout\"; }")]
    [Arguments(
        "public abstract partial class ShopTranslationsEN : ITranslationSet { public string Checkout => \"Checkout\"; }",
        "Remove 'abstract' from 'ShopTranslationsEN'",
        "public partial class ShopTranslationsEN : ITranslationSet { public string Checkout => \"Checkout\"; }")]
    [Arguments(
        "// The shop.\nabstract partial class ShopTranslationsEN : ITranslationSet { public string Checkout => \"Checkout\"; }",
        "Remove 'abstract' from 'ShopTranslationsEN'",
        "// The shop.\npartial class ShopTranslationsEN : ITranslationSet { public string Checkout => \"Checkout\"; }")]
    [Arguments(
        "file sealed partial class ShopTranslationsEN : ITranslationSet { public string Checkout => \"Checkout\"; }",
        "Remove 'file' from 'ShopTranslationsEN'",
        "sealed partial class ShopTranslationsEN : ITranslationSet { public string Checkout => \"Checkout\"; }")]
    public async Task SetOneEditFixes_ShouldGetThatEdit(string declaration, string expectedTitle, string expectedDeclaration)
    {
        // Arrange & Act
        var fixes = await FixAsync(File(declaration));

        // Assert: nothing but the modifier changes, and the generator extends the set from then on.
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
            public sealed partial class ShopTranslations : ITranslationSet
            {
                public string LanguageCode => "en";
                public string Checkout => "Checkout";
            }
            """);
        const string User = """
            namespace TestApp;

            public static class Shop
            {
                public static string Checkout => new ShopTranslations().Checkout;
            }
            """;

        // Act
        var fixes = await FixAsync(set, User);

        // Assert: the class carries its language, also where another file names it.
        var fix = fixes.ShouldHaveSingleItem();
        fix.Title.ShouldBe("Rename to 'ShopTranslationsEN'");
        fix.Sources[0].ShouldBe(set.Replace("class ShopTranslations ", "class ShopTranslationsEN ", StringComparison.Ordinal));
        fix.Sources[1].ShouldBe(User.Replace("new ShopTranslations()", "new ShopTranslationsEN()", StringComparison.Ordinal));
        ShouldBeExtended([.. fix.Sources]);
    }

    [Test]
    public async Task SetNamedAfterItsLanguageInOtherLetterCase_ShouldGetItsSuffixInPlaceOfThatEnding()
    {
        // Arrange: the last two letters give the set its language, and the generator takes the suffix in upper case.
        var set = File("""
            public sealed partial class ShopTranslationsEn : ITranslationSet
            {
                public string Checkout => "Checkout";
            }
            """);

        // Act
        var fixes = await FixAsync(set);

        // Assert: not ShopTranslationsEnEN, whose generated class would be ShopTranslationsEn.
        var fix = fixes.ShouldHaveSingleItem();
        fix.Title.ShouldBe("Rename to 'ShopTranslationsEN'");
        fix.Sources[0].ShouldBe(set.Replace("class ShopTranslationsEn ", "class ShopTranslationsEN ", StringComparison.Ordinal));
        ShouldBeExtended([.. fix.Sources]);
    }

    [Test]
    public async Task PartialSetRepeatingModifier_ShouldLoseItInEveryPart()
    {
        // Arrange
        var first = File("public abstract partial class ShopTranslationsEN : ITranslationSet { public string Checkout => \"Checkout\"; }");
        const string Second = "namespace TestApp; public abstract partial class ShopTranslationsEN { public string Pay => \"Pay\"; }";

        // Act
        var fixes = await FixAsync(first, Second);

        // Assert
        var fix = fixes.ShouldHaveSingleItem();
        fix.Sources[0].ShouldBe(first.Replace("abstract ", "", StringComparison.Ordinal));
        fix.Sources[1].ShouldBe(Second.Replace("abstract ", "", StringComparison.Ordinal));
        ShouldBeExtended([.. fix.Sources]);
    }

    [Test]
    public async Task AbstractSetWithAbstractMember_ShouldStayAsItIs()
    {
        // Arrange: without the modifier the class would no longer compile.
        var source = File("public abstract partial class ShopTranslationsEN : ITranslationSet { public abstract string Checkout { get; } }");

        // Act
        var fixes = await FixAsync(source);

        // Assert
        fixes.ShouldHaveSingleItem().Sources.ShouldHaveSingleItem().ShouldBe(source);
    }

    [Test]
    [Arguments("public sealed partial class ShopTranslationsEN<T> : ITranslationSet { public string Checkout => \"Checkout\"; }")]
    [Arguments("public static partial class Container { public sealed partial class ShopTranslationsEN : ITranslationSet { public string Checkout => \"Checkout\"; } }")]
    [Arguments("public sealed partial class ShopTranslationsEN(string shopName) : ITranslationSet { public string Checkout => shopName; }")]
    public async Task SetNoSingleEditFixes_ShouldGetNoFix(string declaration)
    {
        // Arrange & Act: where the class moves to, what closes its type parameter, or what its constructor can do
        // without, is for its author to decide.
        var fixes = await FixAsync(File(declaration));

        // Assert
        fixes.ShouldBeEmpty();
    }
}
