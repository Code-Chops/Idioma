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

        public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
        {
            public virtual string Invited(string host, string guest) => $"{host} invited {guest}";
        }

        public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
        {
            public virtual string Invited(string host, string guest) => $"{host} heeft {guest} uitgenodigd";
        }
        """);

    /// <summary>
    /// A contract generated from an English set, with a Dutch set and a British set holding the given members.
    /// </summary>
    private static string Sets(string dutch, string british = "") => $$"""
        {{Header}}

        public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
        {
            public virtual string Invited(string host, string guest) => $"{host} invited {guest}";
            public virtual string ItemsInCart(int count, string shop) => $"{count} items in your cart at {shop}";
        }

        public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
        {
            {{dutch}}
        }

        public class ShopLocalizationEnGb : ShopLocalizationEn
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

        public class GreetingLocalizationEn : ILocalizationSet, IGreetingLocalization
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

        public class GreetingLocalizationNl : ILocalizationSet, IGreetingLocalization
        {
            {{dutch}}
        }

        public class GreetingLocalizationEnGb : GreetingLocalizationEn
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
    [Arguments("""public string Invited(string guest, string host) => $"{host} heeft {guest} uitgenodigd";""", "", "ShopLocalizationNl")]
    [Arguments("""string IShopLocalization.Invited(string guest, string host) => $"{host} heeft {guest} uitgenodigd";""", "", "ShopLocalizationNl")]
    [Arguments("""public string Invited(string host, string guest) => $"{host} heeft {guest} uitgenodigd";""", """public override string Invited(string guest, string host) => $"{host} has invited {guest}";""", "ShopLocalizationEnGb")]
    public async Task SetNamingParametersTheOtherWayRound_ShouldReportLOC301AtEach(string dutch, string british, string set)
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
        diagnostics.ShouldAllBe(diagnostic => diagnostic.Id == "LOC301");
        diagnostics.ShouldAllBe(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

        var first = diagnostics.OrderBy(static diagnostic => diagnostic.Location.SourceSpan.Start).First();
        first.GetMessage().ShouldStartWith($"'{set}.Invited' names this parameter 'guest', but 'IShopLocalization.Invited' names it 'host'.");
        first.Location.SourceTree!.ToString()[first.Location.SourceSpan.Start..].ShouldStartWith("guest, string host)");
    }

    [Test]
    public async Task SetNamingOneParameterDifferently_ShouldReportLOC301AtThatParameter()
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(Sets("""
            public string Invited(string host, string guest) => $"{host} heeft {guest} uitgenodigd";
            public string ItemsInCart(int aantal, string shop) => $"{aantal} artikelen in je winkelwagen bij {shop}";
            """));

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.GetMessage().ShouldStartWith("'ShopLocalizationNl.ItemsInCart' names this parameter 'aantal', but 'IShopLocalization.ItemsInCart' names it 'count'.");
        diagnostic.Location.SourceTree!.ToString()[diagnostic.Location.SourceSpan.Start..].ShouldStartWith("aantal, string shop)");
    }

    [Test]
    public async Task SetOfContractOfAnotherAssembly_ShouldBeHeldToItsParameterNames()
    {
        // Arrange & Act: an app adds German to a library, in an assembly that declares no languages itself.
        var diagnostics = await AnalyzeAsync("""
            using ShopLibrary;

            namespace TestApp;

            public sealed class ShopLocalizationDe : CodeChops.Idioma.ILocalizationSet, IShopLocalization
            {
                public string Invited(string guest, string host) => $"{host} hat {guest} eingeladen";
            }
            """, ShopLibrary);

        // Assert
        diagnostics.Length.ShouldBe(2);
        diagnostics.ShouldAllBe(diagnostic => diagnostic.Id == "LOC301");
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
    public async Task SetUsingValueOnlyInCondition_ShouldReportNothing()
    {
        // Arrange & Act: the English wording shows the count, and the Dutch one only chooses by it. Any use of a value
        // counts, in a condition or in the wording.
        var diagnostics = await AnalyzeAsync(Sets("""
            public string Invited(string host, string guest) => $"{host} heeft {guest} uitgenodigd";
            public string ItemsInCart(int count, string shop) => count is 1 ? $"Eén artikel bij {shop}" : $"Meer artikelen bij {shop}";
            """));

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("""public string Greeting(string name) => "Hallo!";""", DutchDear, "", "GreetingLocalizationNl", "Greeting", "nl")]
    [Arguments("""public string Greeting(string name) { return "Hallo!"; }""", DutchDear, "", "GreetingLocalizationNl", "Greeting", "nl")]
    [Arguments("""string IGreetingLocalization.Greeting(string name) => "Hallo!";""", DutchDear, "", "GreetingLocalizationNl", "Greeting", "nl")]
    [Arguments(DutchGreeting, """public string Dear(Salutation salutation, string name) => salutation is Salutation.Madam ? "Geachte mevrouw" : "Geachte klant";""", "", "GreetingLocalizationNl", "Dear", "nl")]
    [Arguments(DutchGreeting, DutchDear, """public override string Greeting(string name) => "Hello there!";""", "GreetingLocalizationEnGb", "Greeting", "en-GB")]
    public async Task SetLeavingOutValueTheDefaultWordingShows_ShouldReportLOC302AtItsParameter(string greeting, string dear, string british, string set, string text, string language)
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
        diagnostic.Id.ShouldBe("LOC302");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldBe($"'{set}.{text}' doesn't use its parameter 'name', but the default wording of 'IGreetingLocalization.{text}' shows it. Use 'name' in every language, or users of '{language}' won't see it.");
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
        diagnostics.Select(static diagnostic => diagnostic.Id).ShouldBe(["LOC301", "LOC302"], ignoreOrder: true);
    }

    [Test]
    public async Task SetOfContractOfAnotherAssembly_ShouldBeHeldToValuesItsDefaultWordingShows()
    {
        // Arrange & Act: an app adds German to a library, whose contract records what its English wording shows.
        var diagnostics = await AnalyzeAsync("""
            using ShopLibrary;

            namespace TestApp;

            public sealed class ShopLocalizationDe : CodeChops.Idioma.ILocalizationSet, IShopLocalization
            {
                public string Invited(string host, string guest) => $"{host} hat jemanden eingeladen";
            }
            """, ShopLibrary);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC302");
        diagnostic.GetMessage().ShouldBe("'ShopLocalizationDe.Invited' doesn't use its parameter 'guest', but the default wording of 'IShopLocalization.Invited' shows it. Use 'guest' in every language, or users of 'de' won't see it.");
    }

    [Test]
    public async Task SetWhoseLanguageTheBuildCannotRead_ShouldReportLOC302WithoutNamingIt()
    {
        // Arrange & Act: the German set's LanguageCode is no constant, so only the running app knows its language.
        var diagnostics = await AnalyzeAsync("""
            using ShopLibrary;

            namespace TestApp;

            public sealed class GermanShopLocalization : CodeChops.Idioma.ILocalizationSet, IShopLocalization
            {
                string CodeChops.Idioma.ILocalizationSet.LanguageCode => System.Environment.GetEnvironmentVariable("SHOP_LANGUAGE") ?? "de";

                public string Invited(string host, string guest) => $"{host} hat jemanden eingeladen";
            }
            """, ShopLibrary);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC302");
        diagnostic.GetMessage().ShouldEndWith("Use 'guest' in every language, or users of that language won't see it.");
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

            public abstract class GermanBase : CodeChops.Idioma.ILocalizationSet, IGreetingLocalization
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
