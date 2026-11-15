using System.Collections.Immutable;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="TextParametersAnalyzer"/>: a text that takes values has the parameter names of its contract in
/// every language, and uses every value the default wording shows, in a set of this assembly's contract and of another
/// assembly's.
/// </summary>
public sealed class TextParametersAnalyzerTests
{
    #region Fixture
    private const string Header = """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl", "en-GB")]

        namespace TestApp;
        """;

    /// <summary>
    /// A library whose contract has a text that takes two values of one type.
    /// </summary>
    private static MetadataReference ShopLibrary { get; } = GeneratorTestHelper.CompileLibraryWithGenerators("ParameterShopLibrary", """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace ShopLibrary;

        public partial class ShopTranslationsEN : ITranslationSet
        {
            public virtual string Invited(string host, string guest) => $"{host} invited {guest}";
        }

        public class ShopTranslationsNL : IShopTranslations
        {
            public virtual string Invited(string host, string guest) => $"{host} heeft {guest} uitgenodigd";
        }
        """);

    /// <summary>
    /// A contract generated from an English set, with a Dutch set and a British set holding the given members.
    /// </summary>
    private static string Sets(string dutch, string british = "") => $$"""
        {{Header}}

        public partial class ShopTranslationsEN : ITranslationSet
        {
            public virtual string Invited(string host, string guest) => $"{host} invited {guest}";
            public virtual string ItemsInCart(int count, string shop) => $"{count} items in your cart at {shop}";
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

    /// <summary>
    /// A contract generated from an English set whose wording shows a name, chooses by a form of address, and chooses
    /// by a number without showing it, with a Dutch set and a British set holding the given members.
    /// </summary>
    private static string Greetings(string dutch, string british = "") => $$"""
        {{Header}}

        public enum Salutation { Unknown, Madam, Sir }

        public partial class GreetingTranslationsEN : ITranslationSet
        {
            public virtual string Greeting(string name) => $"Hello, {name}!";

            public virtual string Dear(Salutation salutation, string name) => salutation switch
            {
                Salutation.Madam => $"Dear Mrs {name}",
                Salutation.Sir => $"Dear Mr {name}",
                _ => $"Dear {name}",
            };

            public virtual string Stock(int count) => count is 0 ? "Sold out" : count < 5 ? "Almost gone" : "In stock";
        }

        public class GreetingTranslationsNL : IGreetingTranslations
        {
            {{dutch}}
        }

        public class GreetingTranslationsEnGB : GreetingTranslationsEN
        {
            {{british}}
        }
        """;

    private const string DutchGreeting = """public string Greeting(string name) => $"Hallo, {name}!";""";
    private const string DutchDear = """public string Dear(Salutation salutation, string name) => $"Beste {name}";""";
    private const string DutchStock = """public string Stock(int count) => "Op voorraad";""";

    private const string DutchItemsInCart = """public string ItemsInCart(int count, string shop) => $"{count} artikelen in je winkelwagen bij {shop}";""";

    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source, params MetadataReference[] references)
    {
        return GeneratorTestHelper.AnalyzeAsync(new TextParametersAnalyzer(), source, references);
    }
    #endregion

    [Test]
    public async Task SetsNamingParametersAsContractDoes_ShouldReportNothing()
    {
        // Arrange & Act: the Dutch wording names the guest first, with the parameters in the order of the contract.
        var diagnostics = await AnalyzeAsync(Sets(
            $$"""
            public string Invited(string host, string guest) => $"{guest} is uitgenodigd door {host}";
            {{DutchItemsInCart}}
            """,
            """public override string Invited(string host, string guest) => $"{host} has invited {guest}";"""));

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("""public string Invited(string guest, string host) => $"{host} heeft {guest} uitgenodigd";""", "", "ShopTranslationsNL")]
    [Arguments("""string IShopTranslations.Invited(string guest, string host) => $"{host} heeft {guest} uitgenodigd";""", "", "ShopTranslationsNL")]
    [Arguments("""public string Invited(string host, string guest) => $"{host} heeft {guest} uitgenodigd";""", """public override string Invited(string guest, string host) => $"{host} has invited {guest}";""", "ShopTranslationsEnGB")]
    public async Task SetNamingParametersTheOtherWayRound_ShouldReportLOC020AtEach(string dutch, string british, string set)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(Sets(
            $$"""
            {{dutch}}
            {{DutchItemsInCart}}
            """,
            british));

        // Assert: at each parameter, whether the method implements the text, implements it explicitly, or overrides the
        // default set's.
        diagnostics.Length.ShouldBe(2);
        diagnostics.ShouldAllBe(diagnostic => diagnostic.Id == TextParametersAnalyzer.ParameterNamesDifferDiagnosticId);
        diagnostics.ShouldAllBe(diagnostic => diagnostic.Id == "LOC020");
        diagnostics.ShouldAllBe(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

        var first = diagnostics.OrderBy(static diagnostic => diagnostic.Location.SourceSpan.Start).First();
        first.GetMessage().ShouldStartWith($"'{set}.Invited' names this parameter 'guest', and 'IShopTranslations.Invited' names it 'host'.");
        first.Location.SourceTree!.ToString()[first.Location.SourceSpan.Start..].ShouldStartWith("guest, string host)");
    }

    [Test]
    public async Task SetNamingOneParameterDifferently_ShouldReportLOC020AtThatParameter()
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(Sets("""
            public string Invited(string host, string guest) => $"{host} heeft {guest} uitgenodigd";
            public string ItemsInCart(int aantal, string shop) => $"{aantal} artikelen in je winkelwagen bij {shop}";
            """));

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.GetMessage().ShouldStartWith("'ShopTranslationsNL.ItemsInCart' names this parameter 'aantal', and 'IShopTranslations.ItemsInCart' names it 'count'.");
        diagnostic.Location.SourceTree!.ToString()[diagnostic.Location.SourceSpan.Start..].ShouldStartWith("aantal, string shop)");
    }

    [Test]
    public async Task SetOfContractOfAnotherAssembly_ShouldBeHeldToItsParameterNames()
    {
        // Arrange & Act: an app adds German to a library, in an assembly that declares no languages itself.
        var diagnostics = await AnalyzeAsync("""
            using ShopLibrary;

            namespace TestApp;

            public sealed class ShopTranslationsDE : IShopTranslations
            {
                public string Invited(string guest, string host) => $"{host} hat {guest} eingeladen";
            }
            """, ShopLibrary);

        // Assert
        diagnostics.Length.ShouldBe(2);
        diagnostics.ShouldAllBe(diagnostic => diagnostic.Id == "LOC020");
    }

    [Test]
    public async Task MethodThatImplementsNoText_ShouldReportNothing()
    {
        // Arrange & Act: a helper of the text's name with other parameters, a method of another name, a method of a
        // class that is no set, and one that returns no string.
        var diagnostics = await AnalyzeAsync(Sets($$"""
            public string Invited(string host, string guest) => Invited(guest, host, formal: false);
            {{DutchItemsInCart}}
            private static string Invited(string guest, string host, bool formal) => $"{host} heeft {guest} uitgenodigd";
            public string Helper(string guest, string host) => guest + host;
            public int Count(string guest) => guest.Length;
            """) + """

            public class NoSet
            {
                public string Invited(string guest, string host) => guest + host;
            }
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task SetUsingEveryValueTheDefaultWordingShows_ShouldReportNothing()
    {
        // Arrange & Act: the Dutch set makes no difference between forms of address and none between numbers in
        // stock, which the English wording only chooses by, and the British set shows the name in a wording of its
        // own.
        var diagnostics = await AnalyzeAsync(Greetings(
            $$"""
            {{DutchGreeting}}
            {{DutchDear}}
            {{DutchStock}}
            """,
            """public override string Greeting(string name) => name.Length is 0 ? "Hello!" : $"Hello there, {name}!";"""));

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("""public string Greeting(string name) => "Hallo!";""", DutchDear, "", "GreetingTranslationsNL", "Greeting")]
    [Arguments("""public string Greeting(string name) { return "Hallo!"; }""", DutchDear, "", "GreetingTranslationsNL", "Greeting")]
    [Arguments("""string IGreetingTranslations.Greeting(string name) => "Hallo!";""", DutchDear, "", "GreetingTranslationsNL", "Greeting")]
    [Arguments(DutchGreeting, """public string Dear(Salutation salutation, string name) => salutation is Salutation.Madam ? "Geachte mevrouw" : "Geachte klant";""", "", "GreetingTranslationsNL", "Dear")]
    [Arguments(DutchGreeting, DutchDear, """public override string Greeting(string name) => "Hello there!";""", "GreetingTranslationsEnGB", "Greeting")]
    public async Task SetLeavingOutValueTheDefaultWordingShows_ShouldReportLOC021AtItsParameter(string greeting, string dear, string british, string set, string text)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(Greetings(
            $$"""
            {{greeting}}
            {{dear}}
            {{DutchStock}}
            """,
            british));

        // Assert: at the parameter, whether the method implements the text, implements it explicitly, or overrides the
        // default set's, and whatever shape its body has.
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(TextParametersAnalyzer.ValueLeftOutDiagnosticId);
        diagnostic.Id.ShouldBe("LOC021");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldStartWith($"'{set}.{text}' does not use its parameter 'name', which the default wording of 'IGreetingTranslations.{text}' shows.");
        diagnostic.Location.SourceTree!.ToString()[diagnostic.Location.SourceSpan.Start..].ShouldStartWith("name)");
    }

    [Test]
    public async Task SetNamingValueDifferentlyAndLeavingItOut_ShouldReportBoth()
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(Greetings($$"""
            public string Greeting(string naam) => "Hallo!";
            {{DutchDear}}
            {{DutchStock}}
            """));

        // Assert
        diagnostics.Select(static diagnostic => diagnostic.Id).ShouldBe(["LOC020", "LOC021"], ignoreOrder: true);
    }

    [Test]
    public async Task SetOfContractOfAnotherAssembly_ShouldBeHeldToValuesItsDefaultWordingShows()
    {
        // Arrange & Act: an app adds German to a library, whose contract records what its English wording shows.
        var diagnostics = await AnalyzeAsync("""
            using ShopLibrary;

            namespace TestApp;

            public sealed class ShopTranslationsDE : IShopTranslations
            {
                public string Invited(string host, string guest) => $"{host} hat jemanden eingeladen";
            }
            """, ShopLibrary);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC021");
        diagnostic.GetMessage().ShouldStartWith("'ShopTranslationsDE.Invited' does not use its parameter 'guest', which the default wording of 'IShopTranslations.Invited' shows.");
    }

    [Test]
    public async Task MethodWithoutBody_ShouldReportNothingAboutItsValues()
    {
        // Arrange & Act: a base the Dutch set takes its other texts from leaves the greeting to it.
        var diagnostics = await AnalyzeAsync(Greetings($$"""
            {{DutchGreeting}}
            {{DutchDear}}
            {{DutchStock}}
            """) + """

            public abstract class GermanBase : IGreetingTranslations
            {
                public abstract string Greeting(string name);
                public string Dear(Salutation salutation, string name) => $"Hallo {name}";
                public string Stock(int count) => "Auf Lager";
            }
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }
}
