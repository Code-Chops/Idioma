using System.Collections.Immutable;
using CodeChops.Idioma.CodeFixes;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="HiddenTextCodeFixProvider"/>: a member of a set that hides a text (<c>LOC302</c>) overrides it,
/// with the text made virtual where this project declares it, or the set lists the contract again.
/// </summary>
public sealed class HiddenTextCodeFixProviderTests
{
    #region Fixture
    private static readonly HiddenTextCodeFixProvider Provider = new();
    private static readonly HiddenTextAnalyzer Analyzer = new();

    /// <summary>
    /// A library whose English set has a text that isn't virtual, and one that is.
    /// </summary>
    private static MetadataReference ShopLibrary { get; } = GeneratorTestHelper.CompileLibraryWithGenerators("HiddenTextFixShopLibrary", """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace ShopLibrary;

        public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
        {
            public string Total => "Total";
            public virtual string Checkout => "Checkout";
        }

        public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
        {
            public string Total => "Totaal";
            public string Checkout => "Afrekenen";
        }
        """);

    /// <summary>
    /// A contract generated from an English set, and a British set that derives from it, each holding the given members.
    /// The British set lists what <paramref name="britishBase"/> holds after the English set.
    /// </summary>
    private static string Sets(string english, string british, string britishBase = "") => $$"""
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "en-GB")]

        namespace TestApp;

        public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
        {
            {{english}}
        }

        public class ShopLocalizationEnGb : ShopLocalizationEn{{britishBase}}
        {
            {{british}}
        }
        """;

    /// <summary>
    /// An app's set of the library's English set, holding the given members, in a file that names the library's types
    /// as <paramref name="usings"/> lets it.
    /// </summary>
    private static string AppSet(string usings, string baseSet, string members) => $$"""
        {{usings}}namespace TestApp;

        public sealed class MyShopLocalizationEn : {{baseSet}}
        {
            {{members}}
        }
        """;

    private static Task<ImmutableArray<(string Title, ImmutableArray<string> Sources)>> FixAsync(string source, params MetadataReference[] references)
    {
        return CodeFixTestHelper.FixWithGeneratedCodeAsync(Provider, Analyzer, references, source);
    }

    /// <summary>
    /// The build reports nothing: no member hides a text, and the sets compile.
    /// </summary>
    private static async Task ShouldReportNothingAsync(string source, params MetadataReference[] references)
    {
        (await GeneratorTestHelper.AnalyzeAsync(Analyzer, source, references)).ShouldBeEmpty();
        GeneratorTestHelper.CompileWithGenerators(source, references).ShouldBeEmpty();
    }
    #endregion

    [Test]
    [Arguments("""public string Color => "Color";""", """public new string Color => "Colour";""", """public virtual string Color => "Color";""", """public override string Color => "Colour";""")]
    [Arguments("""public string Color => "Color";""", """public string Color => "Colour";""", """public virtual string Color => "Color";""", """public override string Color => "Colour";""")]
    [Arguments("""public string Color => "Color";""", """public new virtual string Color => "Colour";""", """public virtual string Color => "Color";""", """public override string Color => "Colour";""")]
    [Arguments("""public string Color { get; } = "Color";""", """public new string Color { get; } = "Colour";""", """public virtual string Color { get; } = "Color";""", """public override string Color { get; } = "Colour";""")]
    [Arguments("""public string Greeting(string name) => $"Hello, {name}!";""", """public new string Greeting(string name) => $"Hiya, {name}!";""", """public virtual string Greeting(string name) => $"Hello, {name}!";""", """public override string Greeting(string name) => $"Hiya, {name}!";""")]
    public async Task TextThatIsNotVirtual_ShouldBeMadeVirtualAndOverridden(string english, string british, string expectedEnglish, string expectedBritish)
    {
        // Arrange & Act
        var fixes = await FixAsync(Sets(english, british));

        // Assert: the first fix overrides, and British users then see the British wording.
        fixes.Length.ShouldBe(2);
        var name = british.Contains("Greeting", StringComparison.Ordinal) ? "Greeting" : "Color";
        fixes[0].Title.ShouldBe($"Make 'ShopLocalizationEn.{name}' virtual and override it");

        var source = fixes[0].Sources.ShouldHaveSingleItem();
        source.ShouldBe(Sets(expectedEnglish, expectedBritish));
        await ShouldReportNothingAsync(source);
    }

    [Test]
    public async Task TextThatIsVirtual_ShouldOnlyBeOverridden()
    {
        // Arrange
        const string English = """public virtual string Color => "Color";""";

        // Act
        var fixes = await FixAsync(Sets(English, """public new string Color => "Colour";"""));

        // Assert: the English set stays as it is.
        fixes[0].Title.ShouldBe("Override 'ShopLocalizationEn.Color'");

        var source = fixes[0].Sources.ShouldHaveSingleItem();
        source.ShouldBe(Sets(English, """public override string Color => "Colour";"""));
        await ShouldReportNothingAsync(source);
    }

    [Test]
    public async Task HiddenText_ShouldAlsoGetTheContractListedAgain()
    {
        // Arrange
        const string English = """public string Color => "Color";""";
        const string British = """public new string Color => "Colour";""";

        // Act
        var fixes = await FixAsync(Sets(English, British));

        // Assert: the contract then calls the British member, and the English set stays as it is.
        fixes[1].Title.ShouldBe("List 'IShopLocalization' again on 'ShopLocalizationEnGb'");

        var source = fixes[1].Sources.ShouldHaveSingleItem();
        source.ShouldBe(Sets(English, British, ", IShopLocalization"));
        await ShouldReportNothingAsync(source);
    }

    [Test]
    public async Task OverrideThatIsSealed_ShouldBeUnsealedAndOverridden()
    {
        // Arrange: a British set of the app's own, that rewords a British text the British set sealed.
        var source = Sets(
            """public virtual string Color => "Color";""",
            """public sealed override string Color => "Colour";""") + """


            public class MyShopLocalizationEnGb : ShopLocalizationEnGb
            {
                public new string Color => "Colour of the house";
            }
            """;

        // Act
        var fixes = await FixAsync(source);

        // Assert
        fixes[0].Title.ShouldBe("Remove 'sealed' from 'ShopLocalizationEnGb.Color' and override it");

        var fixedSource = fixes[0].Sources.ShouldHaveSingleItem();
        fixedSource.ShouldBe(source
            .Replace("public sealed override string Color", "public override string Color", StringComparison.Ordinal)
            .Replace("public new string Color", "public override string Color", StringComparison.Ordinal));
        (await GeneratorTestHelper.AnalyzeAsync(Analyzer, fixedSource, [])).ShouldBeEmpty();
    }

    [Test]
    public async Task TextThatAClassInBetweenHides_ShouldOnlyGetTheContractListedAgain()
    {
        // Arrange: an override in the last set would override the British member, which hides the text too.
        var source = Sets("""public string Color => "Color";""", """public new string Color => "Colour";""") + """


            public class MyShopLocalizationEnGb : ShopLocalizationEnGb
            {
                public new string Color => "Colour of the house";
            }
            """;

        // Act
        var fixes = await FixAsync(source);

        // Assert: the British set gets both fixes, and the last set only the one that lists the contract.
        fixes.Select(static fix => fix.Title).ShouldBe(
        [
            "Make 'ShopLocalizationEn.Color' virtual and override it",
            "List 'IShopLocalization' again on 'ShopLocalizationEnGb'",
            "List 'IShopLocalization' again on 'MyShopLocalizationEnGb'",
        ], ignoreOrder: true);
    }

    [Test]
    public async Task TextImplementedExplicitly_ShouldOnlyGetTheContractListedAgain()
    {
        // Arrange: a contract written by hand, which the English set implements explicitly.
        const string Source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "en-GB")]

            namespace TestApp;

            public interface IShopLocalization : ILocalizationSet
            {
                string Color { get; }
            }

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                string IShopLocalization.Color => "Color";
            }

            public class ShopLocalizationEnGb : ShopLocalizationEn
            {
                public string Color => "Colour";
            }
            """;

        // Act
        var fixes = await FixAsync(Source);

        // Assert
        var fix = fixes.ShouldHaveSingleItem();
        fix.Title.ShouldBe("List 'IShopLocalization' again on 'ShopLocalizationEnGb'");

        var source = fix.Sources.ShouldHaveSingleItem();
        source.ShouldBe(Source.Replace("ShopLocalizationEnGb : ShopLocalizationEn", "ShopLocalizationEnGb : ShopLocalizationEn, IShopLocalization", StringComparison.Ordinal));
        await ShouldReportNothingAsync(source);
    }

    [Test]
    [Arguments("", "ShopLibrary.ShopLocalizationEn", "ShopLibrary.ShopLocalizationEn, ShopLibrary.IShopLocalization")]
    [Arguments("using ShopLibrary;\n\n", "ShopLocalizationEn", "ShopLocalizationEn, IShopLocalization")]
    public async Task TextOfLibraryThatIsNotVirtual_ShouldOnlyGetTheContractListedAgain(string usings, string baseSet, string expectedBase)
    {
        // Arrange
        const string Members = """public new string Total => "Sum";""";

        // Act
        var fixes = await FixAsync(AppSet(usings, baseSet, Members), ShopLibrary);

        // Assert: the library can't change, and the file names the contract as it names the library's set.
        var fix = fixes.ShouldHaveSingleItem();
        fix.Title.ShouldBe("List 'IShopLocalization' again on 'MyShopLocalizationEn'");

        var source = fix.Sources.ShouldHaveSingleItem();
        source.ShouldBe(AppSet(usings, expectedBase, Members));
        await ShouldReportNothingAsync(source, ShopLibrary);
    }

    [Test]
    public async Task TextOfLibraryThatIsVirtual_ShouldBeOverridden()
    {
        // Arrange & Act
        var fixes = await FixAsync(AppSet("", "ShopLibrary.ShopLocalizationEn", """public new string Checkout => "Pay";"""), ShopLibrary);

        // Assert
        fixes.Length.ShouldBe(2);
        fixes[0].Title.ShouldBe("Override 'ShopLocalizationEn.Checkout'");

        var source = fixes[0].Sources.ShouldHaveSingleItem();
        source.ShouldBe(AppSet("", "ShopLibrary.ShopLocalizationEn", """public override string Checkout => "Pay";"""));
        await ShouldReportNothingAsync(source, ShopLibrary);
    }

    [Test]
    public async Task TextInAnotherFile_ShouldBeMadeVirtualThere()
    {
        // Arrange
        const string English = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "en-GB")]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Color => "Color";
            }
            """;

        const string British = """
            namespace TestApp;

            public class ShopLocalizationEnGb : ShopLocalizationEn
            {
                public new string Color => "Colour";
            }
            """;

        // Act
        var fixes = await CodeFixTestHelper.FixWithGeneratedCodeAsync(Provider, Analyzer, English, British);

        // Assert
        var fix = fixes[0];
        fix.Title.ShouldBe("Make 'ShopLocalizationEn.Color' virtual and override it");
        fix.Sources[0].ShouldBe(English.Replace("public string Color", "public virtual string Color", StringComparison.Ordinal));
        fix.Sources[1].ShouldBe(British.Replace("public new string Color", "public override string Color", StringComparison.Ordinal));
        (await GeneratorTestHelper.AnalyzeAsync(Analyzer, fix.Sources[0], [], ("British.cs", fix.Sources[1]))).ShouldBeEmpty();
    }

    [Test]
    public async Task PartialSetWithItsBaseListInAnotherFile_ShouldListTheContractThere()
    {
        // Arrange: the part that hides the text has no base list.
        var english = Sets("""public string Color => "Color";""", "").Replace(
            "public class ShopLocalizationEnGb : ShopLocalizationEn",
            "public partial class ShopLocalizationEnGb : ShopLocalizationEn",
            StringComparison.Ordinal);
        const string British = "namespace TestApp; public partial class ShopLocalizationEnGb { public new string Color => \"Colour\"; }";

        // Act
        var fixes = await CodeFixTestHelper.FixWithGeneratedCodeAsync(Provider, Analyzer, english, British);

        // Assert
        var fix = fixes.Single(static fix => fix.Title.StartsWith("List", StringComparison.Ordinal));
        fix.Sources[0].ShouldBe(english.Replace(
            "public partial class ShopLocalizationEnGb : ShopLocalizationEn",
            "public partial class ShopLocalizationEnGb : ShopLocalizationEn, IShopLocalization",
            StringComparison.Ordinal));
        fix.Sources[1].ShouldBe(British);
    }

    [Test]
    public async Task FixAll_TwoSetsHidingTwoTexts_ShouldMakeEachTextVirtualOnce()
    {
        // Arrange: a British and an American set, each hiding both texts of the English set.
        const string Source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "en-GB", "en-US")]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Color => "Color";
                public string Greeting(string name) => $"Hello, {name}!";
            }

            public class ShopLocalizationEnGb : ShopLocalizationEn
            {
                public new string Color => "Colour";
                public new string Greeting(string name) => $"Hiya, {name}!";
            }

            public class ShopLocalizationEnUs : ShopLocalizationEn
            {
                public new string Color => "Color";
                public new string Greeting(string name) => $"Howdy, {name}!";
            }
            """;

        // Act
        var source = (await CodeFixTestHelper.FixAllAfterGeneratorsAsync(Provider, Analyzer, Source)).ShouldHaveSingleItem();

        // Assert
        source.ShouldBe(Source
            .Replace("public string", "public virtual string", StringComparison.Ordinal)
            .Replace("public new string", "public override string", StringComparison.Ordinal));
        await ShouldReportNothingAsync(source);
    }

    [Test]
    public async Task FixAll_SetHidingTwoTextsImplementedExplicitly_ShouldListTheContractOnce()
    {
        // Arrange: a contract written by hand, which the English set implements explicitly, so neither text can be
        // overridden.
        const string Source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "en-GB")]

            namespace TestApp;

            public interface IShopLocalization : ILocalizationSet
            {
                string Color { get; }
                string Total { get; }
            }

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                string IShopLocalization.Color => "Color";
                string IShopLocalization.Total => "Total";
            }

            public class ShopLocalizationEnGb : ShopLocalizationEn
            {
                public string Color => "Colour";
                public string Total => "Sum";
            }
            """;

        // Act
        var source = (await CodeFixTestHelper.FixAllAfterGeneratorsAsync(Provider, Analyzer, Source)).ShouldHaveSingleItem();

        // Assert
        source.ShouldBe(Source.Replace("ShopLocalizationEnGb : ShopLocalizationEn", "ShopLocalizationEnGb : ShopLocalizationEn, IShopLocalization", StringComparison.Ordinal));
        await ShouldReportNothingAsync(source);
    }

    [Test]
    public async Task FixAll_SetHidingTextsOfTwoContracts_ShouldListBothContracts()
    {
        // Arrange: two contracts written by hand, which the English set implements explicitly, so neither text can be
        // overridden.
        const string Source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "en-GB")]

            namespace TestApp;

            public interface IShopLocalization : ILocalizationSet
            {
                string Color { get; }
            }

            public interface ICartLocalization : ILocalizationSet
            {
                string Total { get; }
            }

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization, ICartLocalization
            {
                string IShopLocalization.Color => "Color";
                string ICartLocalization.Total => "Total";
            }

            public class ShopLocalizationEnGb : ShopLocalizationEn // British
            {
                public string Color => "Colour";
                public string Total => "Sum";
            }
            """;

        // Act
        var source = (await CodeFixTestHelper.FixAllAfterGeneratorsAsync(Provider, Analyzer, Source)).ShouldHaveSingleItem();

        // Assert: each diagnostic lists its contract, both on the one base list.
        source.ShouldBe(Source.Replace("ShopLocalizationEnGb : ShopLocalizationEn //", "ShopLocalizationEnGb : ShopLocalizationEn, IShopLocalization, ICartLocalization //", StringComparison.Ordinal));
        (await GeneratorTestHelper.AnalyzeAsync(Analyzer, source, [])).ShouldBeEmpty();
    }
}
