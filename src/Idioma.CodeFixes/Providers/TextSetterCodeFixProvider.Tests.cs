using System.Collections.Immutable;
using CodeChops.Idioma.CodeFixes;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="TextSetterCodeFixProvider"/>: a text of a set that has a setter (<c>LOC206</c>) loses the
/// setter, along with its body.
/// </summary>
public sealed class TextSetterCodeFixProviderTests
{
    #region Fixture
    /// <summary>
    /// An English set and a Dutch set, each holding the given members.
    /// </summary>
    private static string Sets(string english, string dutch) => $$"""
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace TestApp;

        public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
        {
            {{Indent(english)}}
        }

        public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
        {
            {{Indent(dutch)}}
        }
        """;

    private const string EnglishGreeting = """public virtual string Greeting => "Hello!";""";

    /// <summary>
    /// A library whose English set has a text with a setter, which a set of an app can override.
    /// </summary>
    private static MetadataReference ShopLibrary { get; } = GeneratorTestHelper.CompileLibraryWithGenerators("SetterFixShopLibrary", """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en")]

        namespace ShopLibrary;

        public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
        {
            public virtual string Total { get; set; } = "Total";
        }
        """);

    /// <summary>
    /// Lines after the first of a member, at the indentation of the class body.
    /// </summary>
    private static string Indent(string members) => members.ReplaceLineEndings("\n    ");

    private static Task<ImmutableArray<(string Title, ImmutableArray<string> Sources)>> FixAsync(string source, params MetadataReference[] references)
    {
        return CodeFixTestHelper.FixAfterGeneratorsAsync(new TextSetterCodeFixProvider(), new TextSetterAnalyzer(), references, source);
    }

    private static async Task ShouldCompileWithoutSetters(string source)
    {
        GeneratorTestHelper.CompileWithGenerators(source).ShouldBeEmpty();
        (await GeneratorTestHelper.AnalyzeAsync(new TextSetterAnalyzer(), source, [])).ShouldBeEmpty();
    }
    #endregion

    [Test]
    [Arguments("""public string Greeting { get; set; } = "Hallo!";""")]
    [Arguments("""public string Greeting { get; private set; } = "Hallo!";""")]
    [Arguments("""public string Greeting { set; get; } = "Hallo!";""")]
    public async Task AutoPropertyWithSetter_ShouldLoseItsSetter(string dutch)
    {
        // Arrange & Act
        var fixes = await FixAsync(Sets(EnglishGreeting, dutch));

        // Assert: the getter and the initializer stay as they are, whatever the accessibility of the setter and
        // wherever it stood.
        var fix = fixes.ShouldHaveSingleItem();
        fix.Title.ShouldBe("Remove the setter");

        var source = fix.Sources.ShouldHaveSingleItem();
        source.ShouldBe(Sets(EnglishGreeting, """public string Greeting { get; } = "Hallo!";"""));
        await ShouldCompileWithoutSetters(source);
    }

    [Test]
    public async Task SetterWithBody_ShouldBeRemovedAlongWithItsBody()
    {
        // Arrange
        const string dutch = """
            private string greeting = "Hallo!";

            public string Greeting
            {
                get => this.greeting;

                // Changes the wording for every user.
                set
                {
                    this.greeting = value;
                }
            }
            """;
        const string expected = """
            private string greeting = "Hallo!";

            public string Greeting
            {
                get => this.greeting;
            }
            """;

        // Act
        var fixes = await FixAsync(Sets(EnglishGreeting, dutch));

        // Assert: the comment above the setter goes with it.
        var source = fixes.ShouldHaveSingleItem().Sources.ShouldHaveSingleItem();
        source.ShouldBe(Sets(EnglishGreeting, expected));
        await ShouldCompileWithoutSetters(source);
    }

    [Test]
    public async Task AssignmentInConstructor_ShouldStillCompileAfterTheFix()
    {
        // Arrange: a set that takes its wording from what it is created with.
        const string dutch = """
            public ShopLocalizationNl() => this.Greeting = "Hallo!";

            public string Greeting { get; set; }
            """;
        const string expected = """
            public ShopLocalizationNl() => this.Greeting = "Hallo!";

            public string Greeting { get; }
            """;

        // Act
        var fixes = await FixAsync(Sets(EnglishGreeting, dutch));

        // Assert: a property with a getter alone can be assigned while the set is created.
        var source = fixes.ShouldHaveSingleItem().Sources.ShouldHaveSingleItem();
        source.ShouldBe(Sets(EnglishGreeting, expected));
        await ShouldCompileWithoutSetters(source);
    }

    [Test]
    public async Task SettersInSeveralSets_ShouldAllBeRemovedByFixAll()
    {
        // Arrange & Act
        var sources = await CodeFixTestHelper.FixAllAfterGeneratorsAsync(
            new TextSetterCodeFixProvider(),
            new TextSetterAnalyzer(),
            Sets("""public virtual string Greeting { get; set; } = "Hello!";""", """public string Greeting { get; private set; } = "Hallo!";"""));

        // Assert
        sources.ShouldHaveSingleItem().ShouldBe(Sets("""public virtual string Greeting { get; } = "Hello!";""", """public string Greeting { get; } = "Hallo!";"""));
    }

    [Test]
    public async Task SetterAsOnlyAccessor_ShouldGetNoFix()
    {
        // Arrange: a set of an app that overrides the setter of a library's text, and nothing else of it.
        const string source = """
            namespace TestApp;

            public sealed class MyShopLocalizationEn : ShopLibrary.ShopLocalizationEn
            {
                public override string Total { set { } }
            }
            """;

        // Act
        var diagnostics = await GeneratorTestHelper.AnalyzeAsync(new TextSetterAnalyzer(), source, [ShopLibrary]);
        var fixes = await FixAsync(source, ShopLibrary);

        // Assert: reported, and left to its author, because a property without accessors doesn't compile.
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("LOC206");
        fixes.ShouldBeEmpty();
    }
}
