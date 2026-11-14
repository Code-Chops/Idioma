using System.Collections.Immutable;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="FormatPlaceholdersAnalyzer"/>: the wording of a text in every language holds the placeholders of
/// its default wording, in an inline text and in a set, also a set of another assembly's contract.
/// </summary>
public sealed class FormatPlaceholdersAnalyzerTests
{
    #region Fixture
    private const string Header = """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl", "en-GB")]

        namespace TestApp;
        """;

    /// <summary>
    /// A library whose contract records the placeholders of its English wording.
    /// </summary>
    private static MetadataReference ShopLibrary { get; } = GeneratorTestHelper.CompileLibraryWithGenerators("PlaceholderShopLibrary", """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace ShopLibrary;

        public partial class ShopTranslationsEN : ITranslationSet
        {
            public virtual string Greeting => "Hello, {0}!";
            public virtual string Total => "Total";
        }

        public class ShopTranslationsNL : IShopTranslations
        {
            public virtual string Greeting => "Hallo, {0}!";
            public virtual string Total => "Totaal";
        }
        """);

    private static string Inline(string arguments) => $$"""
        {{Header}}

        public static class Texts
        {
            public static LocalizableText Text { get; } = LocalizableText.Inline({{arguments}});
        }
        """;

    /// <summary>
    /// A contract generated from an English set, with a Dutch set and a British set holding the given members.
    /// </summary>
    private static string Sets(string dutch, string british = "") => $$"""
        {{Header}}

        public partial class ShopTranslationsEN : ITranslationSet
        {
            public virtual string Greeting => "Hello, {0}!";
            public virtual string Summary => "{1} of {0}";
            public virtual string Total => "Total";
        }

        public class ShopTranslationsNL : IShopTranslations
        {
            {{dutch}}
        }

        public class ShopTranslationsEnGB : ShopTranslationsEN
        {
            {{british}}
        }
        """;

    private const string DutchTexts = """
        public string Greeting => "Hallo, {0}!";
        public string Summary => "{1} van {0}";
        public string Total => "Totaal";
        """;

    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source, params MetadataReference[] references)
    {
        return GeneratorTestHelper.AnalyzeAsync(new FormatPlaceholdersAnalyzer(), source, references);
    }
    #endregion

    [Test]
    [Arguments("""en: "Hello, {0}!", nl: "Hallo!", enGB: "Hello, {0}!" """, "nl", "The 'nl' wording has no placeholders, while the 'en' wording has placeholder {0}.")]
    [Arguments("""en: "Hello, {0}!", nl: "Hallo, {1}!", enGB: "Hello, {0}!" """, "nl", "The 'nl' wording has placeholder {1}, while the 'en' wording has placeholder {0}.")]
    [Arguments("""en: "Hello", nl: "Hallo", enGB: "Hello, {0}" """, "enGB", "The 'en-GB' wording has placeholder {0}, while the 'en' wording has no placeholders.")]
    [Arguments("""nl: "Totaal: {0} {", en: "Total: {0}", enGB: "Total: {0}" """, "nl", "The 'nl' wording has a brace that is no placeholder (write a literal brace as {{ or }}), while the 'en' wording has placeholder {0}.")]
    public async Task InlineWordingWithOtherPlaceholders_ShouldReportLOC016(string arguments, string parameter, string message)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(Inline(arguments));

        // Assert: reported at the argument of the language, also when named out of order.
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(FormatPlaceholdersAnalyzer.PlaceholdersDifferDiagnosticId);
        diagnostic.Id.ShouldBe("LOC016");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldStartWith(message);
        diagnostic.Location.SourceTree!.ToString()[diagnostic.Location.SourceSpan.Start..].ShouldStartWith($"{parameter}:");
    }

    [Test]
    [Arguments("""en: "{0} of {1}", nl: "{1} van {0}", enGB: "{0} of {1}" """)]
    [Arguments("""en: "Use braces", nl: "Gebruik {", enGB: "Use }" """)]
    [Arguments("""en: "Total: {0:N2}", nl: "Totaal: {0,8}", enGB: "{{Total}}: {0}" """)]
    [Arguments("""en: System.DateTime.Now.ToString(), nl: "{1}", enGB: "x" """)]
    [Arguments("""en: "Hello, {0}!", nl: System.DateTime.Now.ToString(), enGB: "Hello, {0}!" """)]
    public async Task InlineWordingWithSamePlaceholdersOrNoConstant_ShouldReportNothing(string arguments)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(Inline(arguments));

        // Assert: order, alignment, format, and escaped braces do not count, a lone brace is fine where the default
        // has no placeholders (no Format to throw), and wording that is no constant is not read.
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task SetsWithSamePlaceholders_ShouldReportNothing()
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(Sets(DutchTexts, """public override string Greeting => "Hiya, {0}!";"""));

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("""public string Greeting => "Hallo!"; public string Summary => "{1} van {0}"; public string Total => "Totaal";""", "", "'ShopTranslationsNL.Greeting' has no placeholders, while the default wording of 'IShopTranslations.Greeting' has placeholder {0}.")]
    [Arguments("""public string Greeting { get { return "Hallo, {0}!"; } } public string Summary { get; } = "{0}"; public string Total => "Totaal";""", "", "'ShopTranslationsNL.Summary' has placeholder {0}, while the default wording of 'IShopTranslations.Summary' has placeholders {0} and {1}.")]
    [Arguments("""string IShopTranslations.Greeting => "Hallo, {0}!"; string IShopTranslations.Summary => "{1} van {0}"; string IShopTranslations.Total => "Totaal {0}";""", "", "'ShopTranslationsNL.Total' has placeholder {0}, while the default wording of 'IShopTranslations.Total' has no placeholders.")]
    [Arguments(DutchTexts, """public override string Greeting => "Hiya {1}!";""", "'ShopTranslationsEnGB.Greeting' has placeholder {1}, while the default wording of 'IShopTranslations.Greeting' has placeholder {0}.")]
    public async Task SetWordingWithOtherPlaceholders_ShouldReportLOC016(string dutch, string british, string message)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(Sets(dutch, british));

        // Assert: reported at the property, whether it implements the text, implements it explicitly, or overrides the
        // default set's.
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC016");
        diagnostic.GetMessage().ShouldStartWith(message);
    }

    [Test]
    public async Task SetInheritingWordingWithOtherPlaceholders_ShouldReportLOC016AtTheSet()
    {
        // Arrange: the Dutch wording lies in a class that is no set, where it is no text yet. It becomes one in the set
        // that inherits it, and Dutch users would lose the value Format fills in.
        var source = $$"""
            {{Header}}

            public partial class ShopTranslationsEN : ITranslationSet
            {
                public virtual string Greeting => "Hello, {0}!";
                public virtual string Total => "Total";
            }

            public abstract class DutchWording
            {
                private const string Hello = "Hallo!";

                public string Greeting => Hello;
                public string Total => "Totaal";
            }

            public class ShopTranslationsNL : DutchWording, IShopTranslations;

            public class ShopTranslationsEnGB : ShopTranslationsEN;
            """;

        // Act
        var diagnostics = await AnalyzeAsync(source);

        // Assert: once, at the set, and not again at a class deriving from it.
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC016");
        diagnostic.GetMessage().ShouldStartWith("'ShopTranslationsNL.Greeting', inherited from 'DutchWording', has no placeholders, while the default wording of 'IShopTranslations.Greeting' has placeholder {0}.");
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).ShouldBe("ShopTranslationsNL");
    }

    [Test]
    public async Task SetInheritingWordingWithSamePlaceholders_ShouldReportNothing()
    {
        // Arrange & Act: also a class deriving from the set that brings the contract in, which is held to nothing twice.
        var diagnostics = await AnalyzeAsync($$"""
            {{Header}}

            public partial class ShopTranslationsEN : ITranslationSet
            {
                public virtual string Greeting => "Hello, {0}!";
            }

            public abstract class DutchWording
            {
                public virtual string Greeting => "Hallo, {0}!";
            }

            public class ShopTranslationsNL : DutchWording, IShopTranslations;

            public class GiftTranslationsNL : ShopTranslationsNL;

            public class ShopTranslationsEnGB : ShopTranslationsEN;
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task SetWordingWithOtherPlaceholdersThanDefaultWordingInInitializer_ShouldReportLOC016()
    {
        // Arrange: the default wording as the initializer of a property, which the contract records as it records the
        // wording a getter returns.
        var source = $$"""
            {{Header}}

            public partial class GreetingTranslationsEN : ITranslationSet
            {
                public virtual string Greeting { get; } = "Hello, {0}!";
            }

            public class GreetingTranslationsNL : IGreetingTranslations
            {
                public virtual string Greeting => "Hallo!";
            }
            """;

        // Act
        var diagnostics = await AnalyzeAsync(source);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC016");
        diagnostic.GetMessage().ShouldStartWith("'GreetingTranslationsNL.Greeting' has no placeholders, while the default wording of 'IGreetingTranslations.Greeting' has placeholder {0}.");
    }

    [Test]
    public async Task SetOfReferencedContract_ShouldBeHeldToPlaceholdersItRecords()
    {
        // Arrange: an app adding German to a library's contract, and rewording its English set, in an assembly that
        // declares no languages.
        var source = """
            namespace TestApp;

            public sealed class ShopTranslationsDE : ShopLibrary.IShopTranslations
            {
                public string Greeting => "Hallo!";
                public string Total => "Summe";
            }

            public sealed class MyShopTranslationsEN : ShopLibrary.ShopTranslationsEN
            {
                public override string Greeting => "Hi, {0} and {1}!";
                public override string Total => "Sum";
            }
            """;

        // Act
        var diagnostics = await AnalyzeAsync(source, ShopLibrary);

        // Assert
        diagnostics.Select(static diagnostic => diagnostic.GetMessage()).OrderBy(static message => message, StringComparer.Ordinal).ShouldBe([
            "'MyShopTranslationsEN.Greeting' has placeholders {0} and {1}, while the default wording of 'IShopTranslations.Greeting' has placeholder {0}. Give every language the same placeholders: otherwise Format throws, or leaves out a value, for the users of one language.",
            "'ShopTranslationsDE.Greeting' has no placeholders, while the default wording of 'IShopTranslations.Greeting' has placeholder {0}. Give every language the same placeholders: otherwise Format throws, or leaves out a value, for the users of one language.",
        ]);
    }

    [Test]
    public async Task InlineWordingInRazorOutput_ShouldReportLOC016()
    {
        // Arrange: a file named as Razor's output, which counts as generated code.
        const string razorOutput = """
            // <auto-generated/>
            using CodeChops.Idioma;

            namespace TestApp;

            public static class Page
            {
                public static LocalizableText Text => LocalizableText.Inline(en: "{0} items", nl: "Items", enGB: "{0} items");
            }
            """;

        // Act
        var diagnostics = await GeneratorTestHelper.AnalyzeAsync(new FormatPlaceholdersAnalyzer(), Inline("""en: "x", nl: "x", enGB: "x" """), [], ("Pages/Orders_razor.g.cs", razorOutput));

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("LOC016");
    }
}
