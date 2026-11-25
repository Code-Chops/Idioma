using System.Collections.Immutable;
using CodeChops.Idioma.CodeFixes;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="PlaceholderCodeFixProvider"/>: a text of a set whose wording holds a placeholder (<c>LOC403</c>)
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
    /// <see cref="EnglishGreeting"/> as the fix of the Dutch text leaves it: a method that takes the value, which it
    /// leaves out.
    /// </summary>
    private const string EnglishGreetingTakingValue = """public virtual string Greeting(object? arg0) => "Hello!";""";

    /// <summary>
    /// A library whose texts an app adds German to: one that takes a value, and one that doesn't.
    /// </summary>
    private const string ShopLibrarySource = """
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
        """;

    /// <summary>
    /// The library as a build references it: by its assembly.
    /// </summary>
    private static MetadataReference ShopLibrary { get; } = GeneratorTestHelper.CompileLibraryWithGenerators("PlaceholderFixShopLibrary", ShopLibrarySource);

    /// <summary>
    /// The library as an IDE references a project of the same solution: by its compilation, so its symbols have source
    /// too.
    /// </summary>
    private static MetadataReference ShopLibraryProject { get; } = GeneratorTestHelper.CompileProjectWithGenerators("PlaceholderFixShopProject", ShopLibrarySource);

    private static MetadataReference GetShopLibrary(bool referencedAsProject) => referencedAsProject ? ShopLibraryProject : ShopLibrary;

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

    /// <summary>
    /// Offers the fix as an IDE does, in a workspace that holds the generated contract, through which the fix finds the
    /// other languages.
    /// </summary>
    private static Task<ImmutableArray<(string Title, ImmutableArray<string> Sources)>> FixAsync(string source, params MetadataReference[] references)
    {
        return CodeFixTestHelper.FixWithGeneratedCodeAsync(new PlaceholderCodeFixProvider(), new PlaceholderAnalyzer(), references, source);
    }

    /// <summary>
    /// Asserts that a fixed source compiles, and that no wording of it holds a placeholder any more.
    /// </summary>
    private static async Task ShouldCompileWithoutPlaceholders(string fixedSource)
    {
        GeneratorTestHelper.CompileWithGenerators(fixedSource).ShouldBeEmpty();
        (await GeneratorTestHelper.AnalyzeAsync(new PlaceholderAnalyzer(), fixedSource, [])).ShouldNotContain(diagnostic => diagnostic.Id == "LOC403");
    }

    /// <summary>
    /// The errors of the files of one project as a build compiles them, with the generators run over them.
    /// </summary>
    private static ImmutableArray<Diagnostic> CompileFilesWithGenerators(params string[] sources)
    {
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            sources.Select(static source => CSharpSyntaxTree.ParseText(source, GeneratorTestHelper.ParseOptions)),
            GeneratorTestHelper.References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorTestHelper.CreateFullDriver(GeneratorTestHelper.ParseOptions).RunGeneratorsAndUpdateCompilation(compilation, out var updated, out var diagnostics);

        return [.. diagnostics.Concat(updated.GetDiagnostics()).Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Error)];
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

        // Assert: a parameter in place of the placeholder, with the body of the property or of its getter. The default
        // language takes the value too, also when the Dutch set implements the text explicitly, so the build passes.
        var fix = fixes.ShouldHaveSingleItem();
        fix.Title.ShouldBe("Make 'Greeting' a method");
        var fixedSource = fix.Sources.ShouldHaveSingleItem();
        fixedSource.ShouldBe(Sets(EnglishGreetingTakingValue, expected));
        await ShouldCompileWithoutPlaceholders(fixedSource);
    }

    private const string Method = """public string Greeting(object? arg0) => $"Hallo, {arg0}!";""";

    [Test]
    [Arguments("\"Hey, {0}! You have {1:N0} items\"", "(object? arg0, object? arg1)", "$\"Hey, {arg0}! You have {arg1:N0} items\"")]
    [Arguments("\"{1} invited {0}\"", "(object? arg0, object? arg1)", "$\"{arg1} invited {arg0}\"")]
    [Arguments("\"Only {1}\"", "(object? arg0, object? arg1)", "$\"Only {arg1}\"")]
    [Arguments("\"Total: {0,8}\"", "(object? arg0)", "$\"Total: {arg0,8}\"")]
    [Arguments("\"Hey {ac}, {0}\"", "(object? arg0)", "$\"Hey {{ac}}, {arg0}\"")]
    [Arguments("@\"Say \"\"{0}\"\"\"", "(object? arg0)", "$@\"Say \"\"{arg0}\"\"\"")]
    [Arguments("$\"{1 + 1} items: {{0}}\"", "(object? arg0)", "$\"{1 + 1} items: {arg0}\"")]
    [Arguments("1 > 0 ? \"Morning, {0}\" : \"Evening, {0}\"", "(object? arg0)", "1 > 0 ? $\"Morning, {arg0}\" : $\"Evening, {arg0}\"")]
    public async Task Placeholders_ShouldBecomeOneParameterPerNumber(string wording, string parameters, string expected)
    {
        // Arrange & Act
        var fixes = await FixAsync(Sets($"public virtual string Greeting => {wording};", DutchGreeting));

        // Assert: arg0 for {0}, in any order and up to the highest number, with its alignment and format. A brace
        // that's text is written twice, and a string without a placeholder stays as it is. Each string with one has a
        // diagnostic, and the fix of each changes the whole text. Dutch takes the same values.
        var fixedSource = fixes.Select(static fix => fix.Sources.ShouldHaveSingleItem()).Distinct(StringComparer.Ordinal).ShouldHaveSingleItem();
        fixedSource.ShouldBe(Sets($"public virtual string Greeting{parameters} => {expected};", $"public string Greeting{parameters} => \"Hallo!\";"));
        await ShouldCompileWithoutPlaceholders(fixedSource);
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
        var fixedSource = fixes.ShouldHaveSingleItem().Sources.ShouldHaveSingleItem();
        fixedSource.ReplaceLineEndings().ShouldBe(Sets(EnglishGreetingTakingValue, expected).ReplaceLineEndings());
        await ShouldCompileWithoutPlaceholders(fixedSource);
    }

    [Test]
    public async Task PropertyWhereNullableAnnotationsAreOff_ShouldTakeObject()
    {
        // Arrange & Act
        var fixes = await FixAsync(Sets(EnglishGreeting, """
            #nullable disable
            public string Greeting => "Hallo, {0}!";
            """));

        // Assert: object? would be warning CS8632 there. English, where they're on, takes object?.
        var fixedSource = fixes.ShouldHaveSingleItem().Sources.ShouldHaveSingleItem();
        fixedSource.ShouldContain("""public string Greeting(object arg0) => $"Hallo, {arg0}!";""");
        fixedSource.ShouldContain(EnglishGreetingTakingValue);
        await ShouldCompileWithoutPlaceholders(fixedSource);
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
        var fixedSource = fixes.Select(static fix => fix.Sources.ShouldHaveSingleItem()).Distinct(StringComparer.Ordinal).ShouldHaveSingleItem();
        fixedSource.ShouldBe(Sets(english,
            """public string Items(int count, string shop) => count is 1 ? $"1 artikel bij {shop}" : $"{count} artikelen bij {shop}";"""));
        await ShouldCompileWithoutPlaceholders(fixedSource);
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
        (await GeneratorTestHelper.AnalyzeAsync(new PlaceholderAnalyzer(), source, [])).ShouldContain(diagnostic => diagnostic.Id == "LOC403");
        fixes.ShouldBeEmpty();
    }

    private const string EnglishGreetingWithPlaceholder = """public virtual string Greeting => "Hello, {0}!";""";

    [Test]
    [Arguments("""public string Greeting { get; init; } = "Hallo!";""")]
    [Arguments("""
        public string Greeting { get; }
        public ShopLocalizationNl() => Greeting = "Hallo!";
        """)]
    [Arguments("""
        private readonly string arg0 = "Hallo!";
        public string Greeting => arg0;
        """)]
    public async Task LanguageThatCantBecomeMethod_ShouldGetNoFixInAnyLanguage(string dutch)
    {
        // Arrange: English holds the placeholder, and the Dutch property can't become a method. It has an init
        // accessor, a wording the constructor assigns, or a field by the name of the parameter.
        var source = Sets(EnglishGreetingWithPlaceholder, dutch);

        // Act
        var fixes = await FixAsync(source);

        // Assert: a method in English alone would leave Dutch without the text of the contract (CS0535).
        (await GeneratorTestHelper.AnalyzeAsync(new PlaceholderAnalyzer(), source, [])).ShouldContain(diagnostic => diagnostic.Id == "LOC403");
        fixes.ShouldBeEmpty();
    }

    [Test]
    [Arguments("""public string Greeting => "Hallo!"; public string Welcome => $"{Greeting} Welkom!";""", """public virtual string Welcome => "Welcome!";""")]
    [Arguments("""public string Greeting => "Hallo!"; public string Welcome => this.Greeting + " Welkom!";""", """public virtual string Welcome => "Welcome!";""")]
    [Arguments("""public string Greeting => "Hallo!"; public string Welcome => "Welkom!";""", """public virtual string Welcome => "Welcome!"; public static string Read() => new ShopLocalizationNl().Greeting;""")]
    public async Task TextThatCodeUsesAsProperty_ShouldGetNoFix(string dutch, string englishMore)
    {
        // Arrange: another text shows it, or code reads it from a set. As a method, the property is gone, and that code
        // would no longer compile.
        var source = Sets($"{EnglishGreetingWithPlaceholder} {englishMore}", dutch);

        // Act
        var fixes = await FixAsync(source);

        // Assert
        fixes.ShouldBeEmpty();
    }

    [Test]
    public async Task TextThatCodeReadsThroughGeneratedClass_ShouldStillGetFix()
    {
        // Arrange: that code reads another member, which becomes a method that takes the value. The compiler then points
        // at it, which is what the fix is for.
        var source = Sets(EnglishGreetingWithPlaceholder, """public string Greeting => "Hallo!";""") + """

            public static class Page
            {
                public static string Title => ShopLocalization.Greeting;
            }
            """;

        // Act
        var fixes = await FixAsync(source);

        // Assert
        fixes.ShouldHaveSingleItem().Sources.ShouldHaveSingleItem().ShouldContain("public virtual string Greeting(object? arg0) => $\"Hello, {arg0}!\";");
    }

    [Test]
    public async Task ExplicitImplementationInAnotherLanguage_ShouldBecomeMethodToo()
    {
        // Arrange: Dutch implements the text explicitly, so its property has another name.
        var source = Sets(EnglishGreetingWithPlaceholder, """string IShopLocalization.Greeting => "Hallo!";""");

        // Act
        var fixes = await FixAsync(source);

        // Assert
        var fixedSource = fixes.ShouldHaveSingleItem().Sources.ShouldHaveSingleItem();
        fixedSource.ShouldBe(Sets(
            """public virtual string Greeting(object? arg0) => $"Hello, {arg0}!";""",
            """string IShopLocalization.Greeting(object? arg0) => "Hallo!";"""));
        await ShouldCompileWithoutPlaceholders(fixedSource);
    }

    [Test]
    public async Task AbstractPropertyOfBaseSet_ShouldBecomeAbstractMethod()
    {
        // Arrange: Dutch and German derive from a base that leaves the text to them.
        const string source = """
            #nullable enable
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Greeting => "Hello, {0}!";
            }

            public abstract class ShopLocalizationBase : ILocalizationSet, IShopLocalization
            {
                public abstract string Greeting { get; }
            }

            public sealed class ShopLocalizationNl : ShopLocalizationBase
            {
                public override string Greeting => "Hallo!";
            }

            public sealed class ShopLocalizationDe : ShopLocalizationBase
            {
                public override string Greeting => "Hallo, {0}!";
            }
            """;

        // Act: English and German each have a fix.
        var fixes = await FixAsync(source);

        // Assert: the base takes the value too, so each override still overrides it.
        var fixedSource = fixes.Select(static fix => fix.Sources.ShouldHaveSingleItem()).Distinct(StringComparer.Ordinal).ShouldHaveSingleItem();
        fixedSource.ShouldBe(source
            .Replace("""Greeting => "Hello, {0}!";""", """Greeting(object? arg0) => $"Hello, {arg0}!";""", StringComparison.Ordinal)
            .Replace("public abstract string Greeting { get; }", "public abstract string Greeting(object? arg0);", StringComparison.Ordinal)
            .Replace("""Greeting => "Hallo!";""", """Greeting(object? arg0) => "Hallo!";""", StringComparison.Ordinal)
            .Replace("""Greeting => "Hallo, {0}!";""", """Greeting(object? arg0) => $"Hallo, {arg0}!";""", StringComparison.Ordinal));
        await ShouldCompileWithoutPlaceholders(fixedSource);
    }

    [Test]
    public async Task TextOfEnumMember_ShouldGetNoFix()
    {
        // Arrange: a set of an enum, with a text of a member and a text of its own. Only the default-language set lists
        // ILocalizationSet<OrderStatus>, and both languages hold a placeholder in the text of the member.
        const string source = """
            #nullable enable
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public enum OrderStatus { Pending, Shipped }

            public class OrderStatusLocalizationEn : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
            {
                public string Pending => "Pending";
                public string Shipped => "Shipped on {0}";
                public string Updated => "Updated on {0}";
            }

            public class OrderStatusLocalizationNl : ILocalizationSet, IOrderStatusLocalization
            {
                public string Pending => "In behandeling";
                public string Shipped => "Verzonden op {0}";
                public string Updated => "Bijgewerkt";
            }
            """;

        // Act
        var fixes = await FixAsync(source);

        // Assert: the contract takes the text of a member from the enum, so 'Shipped' stays a property in each
        // language. The text of the set's own still gets the fix.
        var fix = fixes.ShouldHaveSingleItem();
        fix.Title.ShouldBe("Make 'Updated' a method");
        var fixedSource = fix.Sources.ShouldHaveSingleItem();
        fixedSource.ShouldBe(source
            .Replace("""Updated => "Updated on {0}";""", """Updated(object? arg0) => $"Updated on {arg0}";""", StringComparison.Ordinal)
            .Replace("""Updated => "Bijgewerkt";""", """Updated(object? arg0) => "Bijgewerkt";""", StringComparison.Ordinal));
        GeneratorTestHelper.CompileWithGenerators(fixedSource).ShouldBeEmpty();
    }

    [Test]
    public async Task TextOfContractWrittenByHand_ShouldGetNoFix()
    {
        // Arrange
        const string source = """
            #nullable enable
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public interface IGridLocalization : ILocalizationSet
            {
                string First { get; }
            }

            public sealed class GridLocalizationEn : ILocalizationSet, IGridLocalization
            {
                public string First => "First of {0}";
            }

            public sealed class GridLocalizationNl : ILocalizationSet, IGridLocalization
            {
                public string First => "Eerste";
            }
            """;

        // Act
        var fixes = await FixAsync(source);

        // Assert: the fix doesn't change the contract, which would then no longer match the sets (CS0535).
        (await GeneratorTestHelper.AnalyzeAsync(new PlaceholderAnalyzer(), source, [])).ShouldHaveSingleItem().Id.ShouldBe("LOC403");
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
        var fixedSource = fixes.ShouldHaveSingleItem().Sources.ShouldHaveSingleItem();
        fixedSource.ShouldBe(Sets(english,
            """public string Items(int count) => count is 1 ? $"{count} artikel" : string.Format("{0} artikelen", count);"""));
        await ShouldCompileWithoutPlaceholders(fixedSource);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task TextOfContractOfAnotherAssemblyThatTakesNoValue_ShouldGetNoFix(bool referencedAsProject)
    {
        // Arrange: a method would no longer implement the library's property. A project reference has source too.
        var library = GetShopLibrary(referencedAsProject);
        var source = GermanSet("""
            public string Greeting(string name) => $"Hallo, {name}!";
            public string Total => "Summe: {0}";
            """);

        // Act
        var fixes = await FixAsync(source, library);

        // Assert
        (await GeneratorTestHelper.AnalyzeAsync(new PlaceholderAnalyzer(), source, [library])).ShouldHaveSingleItem().Id.ShouldBe("LOC403");
        fixes.ShouldBeEmpty();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task PropertyOverridingSetOfAnotherAssembly_ShouldGetNoFix(bool referencedAsProject)
    {
        // Arrange: a British set rewords the library's English text. A method would override nothing.
        var library = GetShopLibrary(referencedAsProject);
        const string source = """
            using ShopLibrary;

            namespace TestApp;

            public sealed class ShopLocalizationEnGb : ShopLocalizationEn
            {
                public override string Total => "Total: {0}";
            }
            """;

        // Act
        var fixes = await FixAsync(source, library);

        // Assert
        (await GeneratorTestHelper.AnalyzeAsync(new PlaceholderAnalyzer(), source, [library])).ShouldHaveSingleItem().Id.ShouldBe("LOC403");
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
                public static string Greeting => LocalizedText.Inline(en: "Hello, {0}!", nl: "Hallo!");
            }
            """;

        // Act
        var fixes = await FixAsync(source);

        // Assert: its wording has no parameter to take the value.
        (await GeneratorTestHelper.AnalyzeAsync(new PlaceholderAnalyzer(), source, [])).ShouldHaveSingleItem().Id.ShouldBe("LOC403");
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
        (await GeneratorTestHelper.AnalyzeAsync(new PlaceholderAnalyzer(), source, [])).ShouldHaveSingleItem().Id.ShouldBe("LOC403");
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
        var fixedSources = fixes.ShouldHaveSingleItem().Sources;
        fixedSources.ShouldBe(
        [
            english.Replace("""Greeting => "Hello!";""", """Greeting(object? arg0) => "Hello!";""", StringComparison.Ordinal),
            dutch.Replace("""Greeting => "Hallo, {0}!";""", """Greeting(object? arg0) => $"Hallo, {arg0}!";""", StringComparison.Ordinal),
        ]);
        CompileFilesWithGenerators([.. fixedSources]).ShouldBeEmpty();
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

        // Assert: Dutch takes both values, so it still implements the contract. That it leaves one out is LOC402's to say.
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
        var fixedSource = fixes.ShouldHaveSingleItem().Sources.ShouldHaveSingleItem();
        fixedSource.ShouldBe(Sets("""public virtual string Total(decimal amount) => $"Total: {amount, -8:N2}";""", dutch));
        await ShouldCompileWithoutPlaceholders(fixedSource);
    }

    [Test]
    public async Task WordingInsideAnotherWording_ShouldBeFilledInToo()
    {
        // Arrange: the text of the interpolated string holds {0}, and a string in one of its values holds {1}.
        var source = Sets(
            """public virtual string Greeting => $"Hello, {{0}}{(System.DateTime.Now.Hour < 12 ? " and good morning, {1}" : "")}!";""",
            """public string Greeting => "Hallo, {0} en {1}!";""");

        // Act: each of the three strings has a diagnostic, and the fix of each changes the whole text.
        var fixes = await FixAsync(source);

        // Assert: both get a parameter, in the order of their numbers.
        var fixedSource = fixes.Select(static fix => fix.Sources.ShouldHaveSingleItem()).Distinct(StringComparer.Ordinal).ShouldHaveSingleItem();
        fixedSource.ShouldBe(Sets(
            """public virtual string Greeting(object? arg0, object? arg1) => $"Hello, {arg0}{(System.DateTime.Now.Hour < 12 ? $" and good morning, {arg1}" : "")}!";""",
            """public string Greeting(object? arg0, object? arg1) => $"Hallo, {arg0} en {arg1}!";"""));
        await ShouldCompileWithoutPlaceholders(fixedSource);
    }

    /// <summary>
    /// A British set that rewords the English text it derives from, reading that text with <c>base</c>.
    /// </summary>
    private static string SetsWithBritish(string british) => $$"""
        #nullable enable
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl", "en-GB")]

        namespace TestApp;

        public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
        {
            public virtual string Greeting => "Hello, {0}! Pick a color.";
            public virtual string Farewell => "Bye!";
        }

        public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
        {
            public string Greeting => "Hallo, {0}! Kies een kleur.";
            public string Farewell => "Doei!";
        }

        public class ShopLocalizationEnGb : ShopLocalizationEn
        {
            {{Indent(british)}}
        }
        """;

    [Test]
    [Arguments("""public override string Greeting => base.Greeting.Replace("color", "colour");""", """public override string Greeting(object? arg0) => base.Greeting(arg0).Replace("color", "colour");""")]
    [Arguments("""public override string Greeting { get { return base.Greeting.Replace("color", "colour"); } }""", """public override string Greeting(object? arg0) { return base.Greeting(arg0).Replace("color", "colour"); }""")]
    [Arguments("""public override string Greeting => $"{base.Greeting} Cheers.";""", """public override string Greeting(object? arg0) => $"{base.Greeting(arg0)} Cheers.";""")]
    public async Task OverrideThatReadsTheTextOfItsBaseSet_ShouldPassTheValuesOn(string british, string expected)
    {
        // Arrange
        var source = SetsWithBritish(british);

        // Act: English and Dutch each have a fix.
        var fixes = await FixAsync(source);

        // Assert: the British text reads the English one with the value it takes itself.
        var fixedSource = fixes.Select(static fix => fix.Sources.ShouldHaveSingleItem()).Distinct(StringComparer.Ordinal).ShouldHaveSingleItem();
        fixedSource.ReplaceLineEndings().ShouldBe(SetsWithBritish(expected)
            .Replace("""Greeting => "Hello, {0}! Pick a color.";""", """Greeting(object? arg0) => $"Hello, {arg0}! Pick a color.";""", StringComparison.Ordinal)
            .Replace("""Greeting => "Hallo, {0}! Kies een kleur.";""", """Greeting(object? arg0) => $"Hallo, {arg0}! Kies een kleur.";""", StringComparison.Ordinal)
            .ReplaceLineEndings());
        await ShouldCompileWithoutPlaceholders(fixedSource);
    }

    [Test]
    public async Task OtherTextThatReadsTheTextOfTheBaseSet_ShouldGetNoFix()
    {
        // Arrange: the British farewell shows the English greeting. It takes no value to pass on.
        var source = SetsWithBritish("""public override string Farewell => base.Greeting + " Bye!";""");

        // Act
        var fixes = await FixAsync(source);

        // Assert
        fixes.ShouldBeEmpty();
    }

    [Test]
    public async Task FixAll_TextThatCodeUsesAsPropertyNextToOthers_ShouldFixTheOthersOnly()
    {
        // Arrange: three texts with a placeholder in both languages. Another text shows the farewell, so that one
        // can't become a method.
        var source = Sets(
            """
            public string Greeting => "Hello, {0}!";
            public string Total => "Total: {0}";
            public string Farewell => "Bye, {0}!";
            public string Closing => Farewell + " See you.";
            """,
            """
            public string Greeting => "Hallo, {0}!";
            public string Total => "Totaal: {0}";
            public string Farewell => "Dag, {0}!";
            public string Closing => "Tot ziens.";
            """);

        // Act
        var fixedSource = (await CodeFixTestHelper.FixAllAfterGeneratorsAsync(new PlaceholderCodeFixProvider(), new PlaceholderAnalyzer(), source)).ShouldHaveSingleItem();

        // Assert: each of the other texts changes once, in both languages.
        fixedSource.ShouldBe(Sets(
            """
            public string Greeting(object? arg0) => $"Hello, {arg0}!";
            public string Total(object? arg0) => $"Total: {arg0}";
            public string Farewell => "Bye, {0}!";
            public string Closing => Farewell + " See you.";
            """,
            """
            public string Greeting(object? arg0) => $"Hallo, {arg0}!";
            public string Total(object? arg0) => $"Totaal: {arg0}";
            public string Farewell => "Dag, {0}!";
            public string Closing => "Tot ziens.";
            """));
        GeneratorTestHelper.CompileWithGenerators(fixedSource).ShouldBeEmpty();
    }

    [Test]
    public async Task FixAll_InSolution_AppWithSetOfLibrarysContract_ShouldChangeTheLibraryOnly()
    {
        // Arrange: a library project with a placeholder in both its languages, and an app that adds German to it.
        var library = Sets("""public string Greeting => "Hello, {0}!";""", """public string Greeting => "Hallo, {0}!";""").Replace("TestApp", "ShopLibrary", StringComparison.Ordinal);
        var app = $$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]

            {{GermanSet("""public string Greeting => "Hallo, {0}!";""")}}
            """;

        // Act
        var projects = await CodeFixTestHelper.FixAllAfterGeneratorsAsync(new PlaceholderCodeFixProvider(), new PlaceholderAnalyzer(), FixAllScope.Solution, from: (Project: 0, File: 0),
            new TestProject("ShopLibrary", [library]),
            new TestProject("TestApp", [app], "ShopLibrary"));

        // Assert: the fix of the library's text doesn't reach into the app, and the app's set of another assembly's
        // contract has no fix of its own until that contract takes the value.
        projects[0].ShouldHaveSingleItem().ShouldBe(Sets("""public string Greeting(object? arg0) => $"Hello, {arg0}!";""", """public string Greeting(object? arg0) => $"Hallo, {arg0}!";""")
            .Replace("TestApp", "ShopLibrary", StringComparison.Ordinal));
        projects[1].ShouldHaveSingleItem().ShouldBe(app);
    }
}
