using System.Collections.Immutable;
using CodeChops.Idioma.CodeFixes;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="PlaceholderCodeFixProvider"/>: a text of a set whose wording holds a placeholder (<c>LOC502</c>)
/// gets a parameter in place of each placeholder. A property becomes a method that takes the values.
/// </summary>
public sealed class PlaceholderCodeFixProviderTests
{
    #region Fixture
    /// <summary>
    /// An English set and a Dutch set, each holding the given members, with nullable annotations on, as in an app with
    /// <c>&lt;Nullable&gt;enable&lt;/Nullable&gt;</c>.
    /// </summary>
    private static string Sets(string english, string dutch) => $$"""
        #nullable enable
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
    private const string DutchGreeting = """public string Greeting => "Hallo!";""";

    /// <summary>
    /// A library whose texts an app adds German to: one that takes a value, and one that doesn't.
    /// </summary>
    private static MetadataReference ShopLibrary { get; } = GeneratorTestHelper.CompileLibraryWithGenerators("PlaceholderFixShopLibrary", """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace ShopLibrary;

        public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
        {
            public virtual string Greeting(string name) => $"Hello, {name}!";
            public virtual string Total => "Total";
        }

        public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
        {
            public virtual string Greeting(string name) => $"Hallo, {name}!";
            public virtual string Total => "Totaal";
        }
        """);

    private static string GermanSet(string members) => $$"""
        using ShopLibrary;

        namespace TestApp;

        public sealed class ShopLocalizationDe : CodeChops.Idioma.ILocalizationSet, IShopLocalization
        {
            {{Indent(members)}}
        }
        """;

    /// <summary>
    /// Lines after the first of a member, at the indentation of the class body.
    /// </summary>
    private static string Indent(string members) => members.ReplaceLineEndings("\n    ");

    private static Task<ImmutableArray<(string Title, ImmutableArray<string> Sources)>> FixAsync(string source, params MetadataReference[] references)
    {
        return CodeFixTestHelper.FixAfterGeneratorsAsync(new PlaceholderCodeFixProvider(), new PlaceholderAnalyzer(), references, source);
    }
    #endregion

    [Test]
    [Arguments("""public string Greeting => "Hallo, {0}!";""", Method)]
    [Arguments("""public string Greeting { get; } = "Hallo, {0}!";""", Method)]
    [Arguments("""public string Greeting { get => "Hallo, {0}!"; }""", Method)]
    [Arguments("""public string Greeting { get { return "Hallo, {0}!"; } }""", """public string Greeting(object? arg0) { return $"Hallo, {arg0}!"; }""")]
    [Arguments("""string IShopLocalization.Greeting => "Hallo, {0}!";""", """string IShopLocalization.Greeting(object? arg0) => $"Hallo, {arg0}!";""")]
    public async Task PropertyWithPlaceholder_ShouldBecomeMethodTakingTheValue(string dutch, string expected)
    {
        // Arrange & Act
        var fixes = await FixAsync(Sets(EnglishGreeting, dutch));

        // Assert: a parameter in place of the placeholder, with the body of the property or of its getter.
        var fix = fixes.ShouldHaveSingleItem();
        fix.Title.ShouldBe("Make 'Greeting' a method");
        fix.Sources.ShouldHaveSingleItem().ShouldBe(Sets(EnglishGreeting, expected));
    }

    private const string Method = """public string Greeting(object? arg0) => $"Hallo, {arg0}!";""";

    [Test]
    [Arguments("\"Hey, {0}! You have {1:N0} items\"", "(object? arg0, object? arg1) => $\"Hey, {arg0}! You have {arg1:N0} items\"")]
    [Arguments("\"{1} invited {0}\"", "(object? arg0, object? arg1) => $\"{arg1} invited {arg0}\"")]
    [Arguments("\"Only {1}\"", "(object? arg0, object? arg1) => $\"Only {arg1}\"")]
    [Arguments("\"Total: {0,8}\"", "(object? arg0) => $\"Total: {arg0,8}\"")]
    [Arguments("\"Hey {ac}, {0}\"", "(object? arg0) => $\"Hey {{ac}}, {arg0}\"")]
    [Arguments("@\"Say \"\"{0}\"\"\"", "(object? arg0) => $@\"Say \"\"{arg0}\"\"\"")]
    [Arguments("$\"{1 + 1} items: {{0}}\"", "(object? arg0) => $\"{1 + 1} items: {arg0}\"")]
    [Arguments("1 > 0 ? \"Morning, {0}\" : \"Evening, {0}\"", "(object? arg0) => 1 > 0 ? $\"Morning, {arg0}\" : $\"Evening, {arg0}\"")]
    public async Task Placeholders_ShouldBecomeOneParameterPerNumber(string wording, string expected)
    {
        // Arrange & Act
        var fixes = await FixAsync(Sets($"public virtual string Greeting => {wording};", DutchGreeting));

        // Assert: arg0 for {0}, in any order and up to the highest number, with its alignment and format. A brace
        // that's text is written twice, and a string without a placeholder stays as it is. Each string with one has a
        // diagnostic, and the fix of each changes the whole text.
        fixes.Select(static fix => fix.Sources.ShouldHaveSingleItem()).Distinct(StringComparer.Ordinal).ShouldHaveSingleItem()
            .ShouldBe(Sets($"public virtual string Greeting{expected};", DutchGreeting));
    }

    [Test]
    public async Task PropertyWithDocCommentAndGetterBlock_ShouldKeepTheCommentAndGetTheBlockOfAMethod()
    {
        // Arrange
        const string dutch = """
            /// <summary>The greeting.</summary>
            public string Greeting
            {
                get
                {
                    return "Hallo, {0}!";
                }
            }
            """;
        const string expected = """
            /// <summary>The greeting.</summary>
            public string Greeting(object? arg0)
            {
                return $"Hallo, {arg0}!";
            }
            """;

        // Act
        var fixes = await FixAsync(Sets(EnglishGreeting, dutch));

        // Assert: the block is formatted, which writes the line endings of the platform, so those don't count here.
        fixes.ShouldHaveSingleItem().Sources.ShouldHaveSingleItem().ReplaceLineEndings().ShouldBe(Sets(EnglishGreeting, expected).ReplaceLineEndings());
    }

    [Test]
    public async Task PropertyWhereNullableAnnotationsAreOff_ShouldTakeObject()
    {
        // Arrange & Act
        var fixes = await FixAsync(Sets(EnglishGreeting, """
            #nullable disable
            public string Greeting => "Hallo, {0}!";
            """));

        // Assert: object? would be warning CS8632 there.
        fixes.ShouldHaveSingleItem().Sources.ShouldHaveSingleItem().ShouldContain("""public string Greeting(object arg0) => $"Hallo, {arg0}!";""");
    }

    [Test]
    public async Task MethodWithPlaceholders_ShouldUseItsParameters()
    {
        // Arrange
        const string english = """public virtual string Items(int count, string shop) => count is 1 ? $"1 item at {shop}" : $"{count} items at {shop}";""";
        const string dutch = """public string Items(int count, string shop) => count is 1 ? "1 artikel bij {1}" : $"{count} artikelen bij {{1}}";""";

        // Act: each string has a diagnostic of its own.
        var fixes = await FixAsync(Sets(english, dutch));

        // Assert: {1} is the second parameter, in a string and in the text of an interpolated string.
        fixes.Length.ShouldBe(2);
        fixes.ShouldAllBe(fix => fix.Title == "Replace the placeholders with the parameters");
        fixes.Select(static fix => fix.Sources.ShouldHaveSingleItem()).Distinct(StringComparer.Ordinal).ShouldHaveSingleItem().ShouldBe(Sets(english,
            """public string Items(int count, string shop) => count is 1 ? $"1 artikel bij {shop}" : $"{count} artikelen bij {shop}";"""));
    }

    [Test]
    public async Task PropertyOfLanguageAfterDefaultMadeItAMethod_ShouldTakeTheParametersOfTheContract()
    {
        // Arrange: the library's text takes a name, and the German set still words it as a property.
        var source = GermanSet("""
            public string Greeting => "Hallo, {0}!";
            public string Total => "Summe";
            """);

        // Act
        var fixes = await FixAsync(source, ShopLibrary);

        // Assert: the contract's parameter, after which the set implements the text.
        var fixedSource = fixes.ShouldHaveSingleItem().Sources.ShouldHaveSingleItem();
        fixedSource.ShouldBe(GermanSet("""
            public string Greeting(string name) => $"Hallo, {name}!";
            public string Total => "Summe";
            """));
        GeneratorTestHelper.CompileWithGenerators(fixedSource, ShopLibrary).ShouldBeEmpty();
    }

    [Test]
    [Arguments("""public virtual string Greeting(string name) => $"Hello, {name}!";""", """public string Greeting(string name) => "Hallo, {1}!";""")]
    [Arguments("public virtual string Greeting => \"\"\"Hello, {0}!\"\"\";", DutchGreeting)]
    [Arguments("""public virtual string Greeting => "Hello, {8}!";""", DutchGreeting)]
    [Arguments(EnglishGreeting, """public string Greeting { get; set; } = "Hallo, {0}!";""")]
    [Arguments(EnglishGreeting, """
        private readonly string arg0 = "Hallo";
        public string Greeting => arg0 + ", {0}!";
        """)]
    public async Task PlaceholderTheFixCantFillIn_ShouldGetNoFix(string english, string dutch)
    {
        // Arrange
        var source = Sets(english, dutch);

        // Act
        var fixes = await FixAsync(source);

        // Assert: a number without a parameter, a raw string, a ninth value, a property with a setter, and a name the
        // text already uses.
        (await GeneratorTestHelper.AnalyzeAsync(new PlaceholderAnalyzer(), source, [])).ShouldContain(diagnostic => diagnostic.Id == "LOC502");
        fixes.ShouldBeEmpty();
    }

    [Test]
    public async Task WordingNextToStringPassedToMethod_ShouldBeFilledInAlone()
    {
        // Arrange: one wording the text returns as written, and one that string.Format fills in.
        const string english = """public virtual string Items(int count) => $"{count} items";""";
        const string dutch = """public string Items(int count) => count is 1 ? "{0} artikel" : string.Format("{0} artikelen", count);""";

        // Act
        var fixes = await FixAsync(Sets(english, dutch));

        // Assert: the string passed to a method has no diagnostic, and stays as it is.
        fixes.ShouldHaveSingleItem().Sources.ShouldHaveSingleItem().ShouldBe(Sets(english,
            """public string Items(int count) => count is 1 ? $"{count} artikel" : string.Format("{0} artikelen", count);"""));
    }

    [Test]
    public async Task TextOfContractOfAnotherAssemblyThatTakesNoValue_ShouldGetNoFix()
    {
        // Arrange: a method would no longer implement the library's property.
        var source = GermanSet("""
            public string Greeting(string name) => $"Hallo, {name}!";
            public string Total => "Summe: {0}";
            """);

        // Act
        var fixes = await FixAsync(source, ShopLibrary);

        // Assert
        (await GeneratorTestHelper.AnalyzeAsync(new PlaceholderAnalyzer(), source, [ShopLibrary])).ShouldHaveSingleItem().Id.ShouldBe("LOC502");
        fixes.ShouldBeEmpty();
    }

    [Test]
    public async Task InlineText_ShouldGetNoFix()
    {
        // Arrange
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public static class Texts
            {
                public static LocalizableText Greeting => LocalizableText.Inline(en: "Hello, {0}!", nl: "Hallo!");
            }
            """;

        // Act
        var fixes = await FixAsync(source);

        // Assert: its wording has no parameter to take the value.
        (await GeneratorTestHelper.AnalyzeAsync(new PlaceholderAnalyzer(), source, [])).ShouldHaveSingleItem().Id.ShouldBe("LOC502");
        fixes.ShouldBeEmpty();
    }

    [Test]
    public async Task TextOnAMember_ShouldGetNoFix()
    {
        // Arrange: the placeholder is in the attribute of a property, which is no text of a set.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public class Address
            {
                [Localized(en: "Enter {0}", nl: "Vul in")]
                public string City { get; } = "";
            }
            """;

        // Act
        var fixes = await FixAsync(source);

        // Assert: the property isn't turned into a method.
        (await GeneratorTestHelper.AnalyzeAsync(new PlaceholderAnalyzer(), source, [])).ShouldHaveSingleItem().Id.ShouldBe("LOC502");
        fixes.ShouldBeEmpty();
    }

    [Test]
    public async Task FixAll_ShouldGiveEveryLanguageTheSameParameters()
    {
        // Arrange: Dutch has the values in another order, and the British set overrides the English text.
        const string source = """
            #nullable enable
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "en-GB")]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Greeting => "Hello, {0}! You have {1:N0} items.";
            }

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Greeting => "{1:N0} artikelen voor {0}.";
            }

            public class ShopLocalizationEnGb : ShopLocalizationEn
            {
                public override string Greeting => "Hiya, {0}! You've {1:N0} items.";
            }
            """;

        // Act
        var fixedSource = (await CodeFixTestHelper.FixAllAfterGeneratorsAsync(new PlaceholderCodeFixProvider(), new PlaceholderAnalyzer(), source)).ShouldHaveSingleItem();

        // Assert: the texts take the same values in every language, so the build passes.
        fixedSource.ShouldBe(source
            .Replace("""Greeting => "Hello, {0}! You have {1:N0} items.";""", """Greeting(object? arg0, object? arg1) => $"Hello, {arg0}! You have {arg1:N0} items.";""", StringComparison.Ordinal)
            .Replace("""Greeting => "{1:N0} artikelen voor {0}.";""", """Greeting(object? arg0, object? arg1) => $"{arg1:N0} artikelen voor {arg0}.";""", StringComparison.Ordinal)
            .Replace("""Greeting => "Hiya, {0}! You've {1:N0} items.";""", """Greeting(object? arg0, object? arg1) => $"Hiya, {arg0}! You've {arg1:N0} items.";""", StringComparison.Ordinal));
        GeneratorTestHelper.CompileWithGenerators(fixedSource).ShouldBeEmpty();
        (await GeneratorTestHelper.AnalyzeAsync(new PlaceholderAnalyzer(), fixedSource, [])).ShouldBeEmpty();
    }

    /// <summary>
    /// A text with a placeholder in English alone: Dutch has none, the British set overrides the text without one, and
    /// German holds one in a raw string.
    /// </summary>
    private const string OneLanguageWithPlaceholder = """"
        #nullable enable
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl", "en-GB", "de")]

        namespace TestApp;

        public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
        {
            public virtual string Greeting => "Hello, {0}!";
        }

        public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
        {
            public string Greeting => "Hallo!";
        }

        public class ShopLocalizationEnGb : ShopLocalizationEn
        {
            public override string Greeting => "Hi there";
        }

        public class ShopLocalizationDe : ILocalizationSet, IShopLocalization
        {
            public string Greeting => """Hallo, {0}!""";
        }
        """";

    private static string OneLanguageWithPlaceholderFixed { get; } = OneLanguageWithPlaceholder
        .Replace("""Greeting => "Hello, {0}!";""", """Greeting(object? arg0) => $"Hello, {arg0}!";""", StringComparison.Ordinal)
        .Replace("""Greeting => "Hallo!";""", """Greeting(object? arg0) => "Hallo!";""", StringComparison.Ordinal)
        .Replace("""Greeting => "Hi there";""", """Greeting(object? arg0) => "Hi there";""", StringComparison.Ordinal)
        .Replace("Greeting => \"\"\"Hallo, {0}!\"\"\";", "Greeting(object? arg0) => \"\"\"Hallo, {0}!\"\"\";", StringComparison.Ordinal);

    [Test]
    public async Task PropertyBecomingMethod_ShouldBecomeOneInEveryLanguage()
    {
        // Arrange & Act: the workspace holds the generated contract, as an IDE's does.
        var fixes = await CodeFixTestHelper.FixWithGeneratedCodeAsync(new PlaceholderCodeFixProvider(), new PlaceholderAnalyzer(), OneLanguageWithPlaceholder);

        // Assert: every set still implements the contract, so the build passes. A wording without a placeholder gets
        // the parameter all the same, and a raw string keeps its placeholder. German alone has no fix of its own.
        var fix = fixes.ShouldHaveSingleItem();
        fix.Title.ShouldBe("Make 'Greeting' a method");
        fix.Sources.ShouldHaveSingleItem().ShouldBe(OneLanguageWithPlaceholderFixed);
        GeneratorTestHelper.CompileWithGenerators(OneLanguageWithPlaceholderFixed).ShouldBeEmpty();
    }

    [Test]
    public async Task FixAll_LanguageWithoutPlaceholder_ShouldBecomeMethodToo()
    {
        // Arrange & Act
        var fixedSource = (await CodeFixTestHelper.FixAllAfterGeneratorsAsync(new PlaceholderCodeFixProvider(), new PlaceholderAnalyzer(), OneLanguageWithPlaceholder)).ShouldHaveSingleItem();

        // Assert
        fixedSource.ShouldBe(OneLanguageWithPlaceholderFixed);
    }

    [Test]
    public async Task PropertyBecomingMethod_ShouldChangeTheSetsOfOtherFiles()
    {
        // Arrange: each set in a file of its own.
        const string english = """
            #nullable enable
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Greeting => "Hello!";
            }
            """;

        const string dutch = """
            #nullable enable
            using CodeChops.Idioma;

            namespace TestApp;

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Greeting => "Hallo, {0}!";
            }
            """;

        // Act
        var fixes = await CodeFixTestHelper.FixWithGeneratedCodeAsync(new PlaceholderCodeFixProvider(), new PlaceholderAnalyzer(), english, dutch);

        // Assert
        fixes.ShouldHaveSingleItem().Sources.ShouldBe(
        [
            english.Replace("""Greeting => "Hello!";""", """Greeting(object? arg0) => "Hello!";""", StringComparison.Ordinal),
            dutch.Replace("""Greeting => "Hallo, {0}!";""", """Greeting(object? arg0) => $"Hallo, {arg0}!";""", StringComparison.Ordinal),
        ]);
    }

    [Test]
    public async Task FixAll_LanguageWithFewerPlaceholders_ShouldGetTheParametersOfTheOthers()
    {
        // Arrange: Dutch shows the name alone, where English also shows the number of items.
        var source = Sets(
            """public virtual string Greeting => "Hello, {0}! You have {1} items.";""",
            """public string Greeting => "Hallo, {0}!";""");

        // Act
        var fixedSource = (await CodeFixTestHelper.FixAllAfterGeneratorsAsync(new PlaceholderCodeFixProvider(), new PlaceholderAnalyzer(), source)).ShouldHaveSingleItem();

        // Assert: Dutch takes both values, so it still implements the contract. That it leaves one out is LOC302's to say.
        fixedSource.ShouldBe(Sets(
            """public virtual string Greeting(object? arg0, object? arg1) => $"Hello, {arg0}! You have {arg1} items.";""",
            """public string Greeting(object? arg0, object? arg1) => $"Hallo, {arg0}!";"""));
        GeneratorTestHelper.CompileWithGenerators(fixedSource).ShouldBeEmpty();
    }

    [Test]
    [Arguments("Choose from {1, 2, 3}")]
    [Arguments("Sizes {40,42,44}")]
    [Arguments("Between {1,} and {2, x}")]
    public async Task BracesThatHoldNoPlaceholder_ShouldGetNoWarningAndNoFix(string wording)
    {
        // Arrange: a comma after the number, but no whole number after it, so string.Format wouldn't read it either.
        var source = Sets($"public virtual string Hint => \"{wording}\";", """public string Hint => "Kies";""");

        // Act
        var fixes = await FixAsync(source);

        // Assert
        (await GeneratorTestHelper.AnalyzeAsync(new PlaceholderAnalyzer(), source, [])).ShouldBeEmpty();
        fixes.ShouldBeEmpty();
    }

    [Test]
    public async Task PlaceholderWithSpacedAlignment_ShouldKeepItAsWritten()
    {
        // Arrange
        const string english = """public virtual string Total(decimal amount) => "Total: {0, -8:N2}";""";
        const string dutch = """public string Total(decimal amount) => $"Totaal: {amount}";""";

        // Act
        var fixes = await FixAsync(Sets(english, dutch));

        // Assert
        fixes.ShouldHaveSingleItem().Sources.ShouldHaveSingleItem().ShouldBe(Sets(
            """public virtual string Total(decimal amount) => $"Total: {amount, -8:N2}";""", dutch));
    }
}
