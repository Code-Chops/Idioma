using System.Collections.Immutable;
using CodeChops.Idioma.CodeFixes;
using CodeChops.Idioma.Tests.TestHelpers;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="UnlistedLanguageCodeFixProvider"/>: a set of a language the attribute doesn't list
/// (<c>LOC202</c>) adds that language to <c>[assembly: SupportedLanguages]</c>.
/// </summary>
public sealed class UnlistedLanguageCodeFixProviderTests
{
    #region Fixture
    private static string Attribute(string languages) => $"[assembly: SupportedLanguages({languages})]";

    private static string Source(string languages, string set) => $$"""
        using CodeChops.Idioma;

        {{Attribute(languages)}}

        namespace TestApp;

        public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
        {
            public virtual string Color => "Color";
        }

        public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
        {
            public string Color => "Kleur";
        }

        {{set}}
        """;

    private static Task<ImmutableArray<(string Title, ImmutableArray<string> Sources)>> FixAsync(params string[] sources)
    {
        return CodeFixTestHelper.FixAsync(new UnlistedLanguageCodeFixProvider(), LocalizationParts.Sets(), sources);
    }
    #endregion

    [Test]
    [Arguments("public class ShopLocalizationDe : ILocalizationSet, IShopLocalization { public string Color => \"Farbe\"; }", "de")]
    [Arguments("public class ShopLocalizationEnGb : ShopLocalizationEn { public override string Color => \"Colour\"; }", "en-GB")]
    public async Task SetOfAnUnlistedLanguage_ShouldListIt(string set, string language)
    {
        // Arrange & Act
        var fixes = await FixAsync(Source("\"en\", \"nl\"", set));

        // Assert: the language comes last, and the build then reports nothing.
        var fix = fixes.ShouldHaveSingleItem();
        fix.Title.ShouldBe($"Add '{language}' to [assembly: SupportedLanguages]");

        var source = fix.Sources.ShouldHaveSingleItem();
        source.ShouldBe(Source($"\"en\", \"nl\", \"{language}\"", set));
        GeneratorTestHelper.GetGeneratorDiagnostics(source).ShouldBeEmpty();
        GeneratorTestHelper.CompileWithGenerators(source).ShouldBeEmpty();
    }

    [Test]
    public async Task AttributeThatNamesTheDefaultLanguage_ShouldListTheLanguageLast()
    {
        // Arrange: the README writes the attribute this way.
        var set = "public class ShopLocalizationDe : ILocalizationSet, IShopLocalization { public string Color => \"Farbe\"; }";

        // Act
        var fixes = await FixAsync(Source("defaultLanguage: \"en\", \"nl\"", set));

        // Assert
        var source = fixes.ShouldHaveSingleItem().Sources.ShouldHaveSingleItem();
        source.ShouldBe(Source("defaultLanguage: \"en\", \"nl\", \"de\"", set));
        GeneratorTestHelper.GetGeneratorDiagnostics(source).ShouldBeEmpty();
        GeneratorTestHelper.CompileWithGenerators(source).ShouldBeEmpty();
    }

    [Test]
    public async Task AttributeInAnotherFile_ShouldBeChangedThere()
    {
        // Arrange
        var first = $"using CodeChops.Idioma;\n\n{Attribute("\"en\", \"nl\"")} // Languages\n";
        var second = Source("\"en\", \"nl\"", "public class ShopLocalizationDe : ILocalizationSet, IShopLocalization { public string Color => \"Farbe\"; }")
            .Replace($"{Attribute("\"en\", \"nl\"")}\n\n", "", StringComparison.Ordinal);

        // Act
        var fixes = await FixAsync(first, second);

        // Assert
        var fix = fixes.ShouldHaveSingleItem();
        fix.Sources[0].ShouldBe($"using CodeChops.Idioma;\n\n{Attribute("\"en\", \"nl\", \"de\"")} // Languages\n");
        fix.Sources[1].ShouldBe(second);
    }

    [Test]
    public async Task AttributeWithArgumentsNotWrittenAsStrings_ShouldGetNoFix()
    {
        // Arrange: a language held by a constant, after which the fix can't tell how to write another.
        var source = Source("Languages.Default, \"nl\"", "public class ShopLocalizationDe : ILocalizationSet, IShopLocalization { public string Color => \"Farbe\"; }")
                     + "\n\ninternal static class Languages { public const string Default = \"en\"; }\n";

        // Act
        var fixes = await FixAsync(source);

        // Assert
        fixes.ShouldBeEmpty();
    }
}
