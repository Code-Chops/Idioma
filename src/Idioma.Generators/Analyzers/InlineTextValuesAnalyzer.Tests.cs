using System.Collections.Immutable;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="InlineTextValuesAnalyzer"/>: every language of an inline text uses the values its default
/// wording shows, and nothing is reported where the build can't tell.
/// </summary>
public sealed class InlineTextValuesAnalyzerTests
{
    #region Fixture
    private const string Header = """
        using System;
        using System.Linq;
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl", "en-GB")]

        namespace TestApp;

        public sealed record User(string Name, string City);
        """;

    /// <summary>
    /// A class whose method creates an inline text of the given arguments, after the given statements.
    /// </summary>
    private static string Inline(string arguments, string statements = "") => $$"""
        {{Header}}

        public sealed class Texts
        {
            private const string Brand = "Idioma";
            private static readonly string Unit = "km";
            private readonly string shop = "Shop";

            public string Owner { get; set; } = "Owner";
            public string OwnerInCapitals => Owner.ToUpperInvariant();

            public LocalizableText Text(string name, int count, User user, string[] items)
            {
                {{statements}}
                return LocalizableText.Inline({{arguments}});
            }

            private string DescribeShop() => shop;
            private static string Format(int number) => number.ToString("N0");
        }
        """;

    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source, params (string Path, string Text)[] files)
    {
        return GeneratorTestHelper.AnalyzeAsync(new InlineTextValuesAnalyzer(), source, [], files);
    }

    private static string At(Diagnostic diagnostic)
    {
        return diagnostic.Location.SourceTree!.ToString()[diagnostic.Location.SourceSpan.Start..];
    }
    #endregion

    [Test]
    [Arguments("""en: $"Hello, {name}!", nl: "Hallo!", enGb: $"Hello, {name}!" """, "nl: \"Hallo!\"", "nl", "name")]
    [Arguments("""en: $"Hello, {name}!", nl: $"Hallo, {name}!", enGb: "Hello there!" """, "enGb: \"Hello there!\"", "en-GB", "name")]
    [Arguments("""nl: "Hallo!", enGb: $"Hello, {name}!", en: $"Hello, {name}!" """, "nl: \"Hallo!\"", "nl", "name")]
    [Arguments("""en: $"{count:N0} items", nl: "Artikelen", enGb: $"{count} items" """, "nl: \"Artikelen\"", "nl", "count")]
    [Arguments("""en: $"{count.ToString("N0")} items", nl: "Artikelen", enGb: $"{count} items" """, "nl: \"Artikelen\"", "nl", "count")]
    [Arguments("""en: $"{Format(count)} items", nl: $"{items.Length} artikelen", enGb: $"{count} items" """, "nl: $\"{items.Length} artikelen\"", "nl", "count")]
    [Arguments("""en: "You have " + $"{count} items", nl: "Je hebt artikelen", enGb: $"{count} items" """, "nl: \"Je hebt artikelen\"", "nl", "count")]
    [Arguments("""en: count == 1 ? "1 item" : $"{count} items", nl: "Artikelen", enGb: $"{count} items" """, "nl: \"Artikelen\"", "nl", "count")]
    [Arguments("""en: $"Hello, {user.Name}!", nl: "Hallo!", enGb: $"Hello, {user.Name}!" """, "nl: \"Hallo!\"", "nl", "user")]
    [Arguments("""en: $"Welcome to {shop}", nl: "Welkom", enGb: $"Welcome to {shop}" """, "nl: \"Welkom\"", "nl", "shop")]
    [Arguments("""en: $"Owned by {this.Owner}", nl: "Eigendom", enGb: $"Owned by {Owner}" """, "nl: \"Eigendom\"", "nl", "Owner")]
    [Arguments("""en: $"{count} {Unit}", nl: $"{count} kilometer", enGb: $"{count} {Unit}" """, "nl: $\"{count} kilometer\"", "nl", "Unit")]
    [Arguments("""en: $"Hello, {name}!", nl: $"Hallo, {Format(count)}!", enGb: $"Hello, {name}!" """, "nl: $\"Hallo, {Format(count)}!\"", "nl", "name")]
    public async Task WordingLeavingOutValueTheDefaultWordingShows_ShouldReportLOC302AtItsArgument(string arguments, string at, string language, string value)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(Inline(arguments));

        // Assert: at the argument of the language, whatever order the arguments are written in, for a value shown with
        // a format, through a call, or as a member of it, and for a field or property as for a parameter. A method of
        // the class can't read a parameter, so calling one doesn't excuse leaving the name out.
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(InlineTextValuesAnalyzer.ValueLeftOutDiagnosticId);
        diagnostic.Id.ShouldBe("LOC302");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldBe($"The '{language}' wording doesn't use '{value}', but the 'en' wording shows it. Use '{value}' in every language, or users of '{language}' won't see it.");
        At(diagnostic).ShouldStartWith(at);
    }

    [Test]
    public async Task WordingLeavingOutSeveralValues_ShouldReportEachOnce()
    {
        // Arrange & Act: the name is shown twice, and both other languages leave a value out.
        var diagnostics = await AnalyzeAsync(Inline("""en: $"{name}, you have {count} items, {name}", nl: "Je hebt artikelen", enGb: $"{name}, you have items" """));

        // Assert
        diagnostics.Select(static diagnostic => diagnostic.GetMessage()).ShouldBe(
        [
            "The 'nl' wording doesn't use 'name', but the 'en' wording shows it. Use 'name' in every language, or users of 'nl' won't see it.",
            "The 'nl' wording doesn't use 'count', but the 'en' wording shows it. Use 'count' in every language, or users of 'nl' won't see it.",
            "The 'en-GB' wording doesn't use 'count', but the 'en' wording shows it. Use 'count' in every language, or users of 'en-GB' won't see it.",
        ], ignoreOrder: true);
    }

    [Test]
    [Arguments("""en: $"Hello, {name}!", nl: $"Hallo, {name}!", enGb: $"Hello, {name}!" """)]
    [Arguments("""en: $"{count:N0} items", nl: $"{count.ToString("N0")} artikelen", enGb: $"{Format(count),8} items" """)]
    [Arguments("""en: $"{count} items", nl: count == 1 ? "1 artikel" : "meer artikelen", enGb: count switch { 1 => "1 item", _ => "more items" } """)]
    [Arguments("""en: $"Hello, {user.Name}!", nl: $"Hallo, {user.Name}!", enGb: $"Hello from {user.City}!" """)]
    [Arguments("""en: $"Welcome to {shop} of {Owner}", nl: $"Welkom bij {this.shop} van {this.Owner}", enGb: $"{Owner} welcomes you to {shop}" """)]
    [Arguments("""en: "Hello, " + name, nl: "Hallo", enGb: "Hello" """)]
    [Arguments("""en: "Hello!", nl: $"Hallo, {name}!", enGb: "Hello!" """)]
    public async Task WordingUsingEveryValueTheDefaultWordingShows_ShouldReportNothing(string arguments)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(Inline(arguments));

        // Assert: any use of a value counts, in a condition or in the wording, and so does another member of the value
        // shown. A default wording without a value in an interpolated string holds no language to anything, whatever
        // the others show.
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("""en: count == 1 ? "1 item" : "More items", nl: "Artikelen", enGb: "Items" """)]
    [Arguments("""en: $"{(count == 1 ? "One item" : "More items")} for you", nl: "Artikelen voor jou", enGb: "Items for you" """)]
    [Arguments("""en: $"{count switch { 1 => "One item", _ => "More items" }}", nl: "Artikelen", enGb: "Items" """)]
    [Arguments("""en: $"{(name is "" ? "Nobody" : "Somebody")} is here", nl: "Er is iemand", enGb: "Somebody is here" """)]
    public async Task ValueTheDefaultWordingOnlyChoosesBy_ShouldReportNothing(string arguments)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(Inline(arguments));

        // Assert: a value in a condition, in what a switch switches on, or in a pattern chooses the wording, also
        // inside an interpolated string, and may mean nothing in a language that makes no such distinction.
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("""en: $"{Brand} shop", nl: "Winkel", enGb: "Shop" """)]
    [Arguments("""en: $"{nameof(name)} is required", nl: "Verplicht", enGb: "Required" """)]
    [Arguments("""en: $"Today is {DateTime.Now:D}", nl: $"Vandaag is het {DateTime.Today:D}", enGb: $"It is {DateTime.Today:D} today" """)]
    [Arguments("""en: $"Line{Environment.NewLine}break", nl: "Regeleinde", enGb: "Line break" """)]
    [Arguments("""en: $"{items.Count(item => item.Length > 3)} long items", nl: $"{items.Count(x => x.Length > 3)} lange artikelen", enGb: $"{items.Length} items" """)]
    [Arguments("""en: $"{(items is [var first, ..] ? first : "Nothing")} first", nl: $"{items.Length} artikelen", enGb: $"{items.Length} items" """)]
    public async Task WhatIsNoValueOfTheCaller_ShouldReportNothing(string arguments)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(Inline(arguments));

        // Assert: a constant is wording, a member of another assembly is no value of the text, and a parameter or a
        // variable that the default argument declares itself can't be read by another language.
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("""en: $"Hello, {name}!", nl: dutch, enGb: $"Hello, {name}!" """, """var dutch = "Hallo!";""")]
    [Arguments("""en: $"Hello, {name}!", nl: DescribeShop(), enGb: $"Hello, {name}!" """, "")]
    [Arguments("""en: english, nl: "Hallo!", enGb: "Hello!" """, """var english = $"Hello, {name}!";""")]
    [Arguments("""en: string.Format("Hello, {0}!", name), nl: "Hallo!", enGb: "Hello!" """, "")]
    public async Task WordingNotWrittenInPlace_ShouldReportNothing(string arguments, string statements)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(Inline(arguments, statements));

        // Assert: a variable or a call may hold the value unseen, and a default wording that isn't written in place
        // shows nothing the build can read.
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("""en: $"Hello, {user.Name}!", nl: $"Hallo, {userName}!", enGb: $"Hello, {user.Name}!" """, """var userName = user.Name;""")]
    [Arguments("""en: $"Hello, {name} of {user.City}!", nl: $"Hallo, {greeted}!", enGb: $"Hello, {greeted}!" """, """var who = name.Trim(); var greeted = $"{who} ({user.City})";""")]
    [Arguments("""en: $"Hello, {name}!", nl: $"Hallo, {Who()}!", enGb: $"Hello, {name}!" """, """string Who() => name;""")]
    [Arguments("""en: $"Hello, {name}!", nl: $"Hallo, {who()}!", enGb: $"Hello, {name}!" """, """Func<string> who = () => name;""")]
    [Arguments("""en: $"Welcome to {shop}", nl: $"Welkom bij {DescribeShop()}", enGb: $"Welcome to {shop}" """, "")]
    [Arguments("""en: $"Owned by {Owner}", nl: $"Eigendom van {OwnerInCapitals}", enGb: $"Owned by {Owner}" """, "")]
    public async Task WordingThatMayHoldValueUnseen_ShouldReportNothing(string arguments, string statements)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(Inline(arguments, statements));

        // Assert: a local variable declared with the value holds it, also through another variable. A local function
        // or a delegate may read any value, and a method or a property with a body may read a field or property.
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("""en: $"Hello, {name}!", nl: $"Hallo, {naam}!", enGb: $"Hello, {name}!" """)]
    [Arguments("""en: $"Hello, {name}!", nl: "Hallo" + , enGb: $"Hello, {name}!" """)]
    [Arguments("""en: $"Hello, {name}!", nl: "Hallo!" """)]
    [Arguments("""en: $"Hello, {name}!", nl: "Hallo!", enGb: "Hello!", de: "Hallo!" """)]
    [Arguments("""en: $"Hello, {name.}!", nl: "Hallo!", enGb: "Hello!" """)]
    [Arguments("""en: $"Hello, {name}!", nl: "Hallo!", enGb: """)]
    [Arguments("""$"Hello, {name}!", "Hallo!", """)]
    public async Task CallThatDoesNotCompile_ShouldReportNothing(string arguments)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(Inline(arguments));

        // Assert: nothing is said about the values of a call that is half typed or binds to nothing, and nothing
        // throws.
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("string upper; upper = name.ToUpperInvariant();", "upper")]
    [Arguments("var list = \"\"; list += name;", "list")]
    [Arguments("var (first, rest) = (name, count);", "first")]
    [Arguments("string first; int rest; (first, rest) = (name, count);", "first")]
    [Arguments("object boxed = name; if (boxed is not string text) throw new InvalidOperationException();", "text")]
    [Arguments("if (user is not { Name: var userName }) throw new InvalidOperationException();", "userName")]
    [Arguments("int.TryParse(name, out var parsed);", "parsed")]
    [Arguments("int parsed; int.TryParse(name, out parsed);", "parsed")]
    public async Task WordingReadingLocalThatGetsTheValueOutsideItsDeclaration_ShouldReportNothing(string statements, string local)
    {
        // Arrange & Act: the Dutch wording shows a local variable that holds the name, or may hold it.
        var diagnostics = await AnalyzeAsync(Inline($$"""en: $"Hello, {name}!", nl: $"Hallo, {{{local}}}!", enGb: $"Hello, {name}!" """, statements));

        // Assert: an assignment is read as a declaration is, and a local variable written in a way the build can't
        // read may hold the value.
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("string upper; upper = count.ToString();", "upper")]
    [Arguments("var list = \"\"; list += count;", "list")]
    public async Task WordingReadingLocalAssignedAnotherValue_ShouldReportLOC302(string statements, string local)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(Inline($$"""en: $"Hello, {name}!", nl: $"Hallo, {{{local}}}!", enGb: $"Hello, {name}!" """, statements));

        // Assert: what the local variable is assigned doesn't read the name.
        diagnostics.ShouldHaveSingleItem().GetMessage().ShouldStartWith("The 'nl' wording doesn't use 'name'");
    }

    [Test]
    public async Task ManyInlineTextsInOneMethod_ShouldHoldEachToWhatItsOwnLocalIsAssigned()
    {
        // Arrange: one method with many texts, as Razor writes all the markup of a page in one method. The Dutch wording
        // of each shows a local variable of its own, which gets its value after its declaration in one of three ways:
        // the name through another local variable, another value, or an argument passed by reference.
        const int textCount = 90;

        static string Statements(int index) => (index % 3) switch
        {
            0 => $$"""
                string copy{{index}}, held{{index}}; copy{{index}} = name.Trim(); held{{index}} = copy{{index}};
                Show(LocalizableText.Inline(en: $"Hello, {name}!", nl: $"Hallo, {held{{index}}}!", enGb: $"Hello, {name}!"));
                """,
            1 => $$"""
                var other{{index}} = "gast"; other{{index}} = count.ToString();
                Show(LocalizableText.Inline(en: $"Hello, {name}!", nl: $"Hallo, {other{{index}}}!", enGb: $"Hello, {name}!"));
                """,
            _ => $$"""
                var parsed{{index}} = 0; int.TryParse(name, out parsed{{index}});
                Show(LocalizableText.Inline(en: $"Hello, {name}!", nl: $"Hallo, {parsed{{index}}}!", enGb: $"Hello, {name}!"));
                """,
        };

        var source = $$"""
            {{Header}}

            public static class Page
            {
                public static void Render(string name, int count)
                {
            {{string.Join(Environment.NewLine, Enumerable.Range(0, textCount).Select(Statements))}}
                }

                private static void Show(LocalizableText text) { }
            }
            """;

        // Act
        var diagnostics = await AnalyzeAsync(source);

        // Assert: each text is held to what its own local variable is assigned, whatever the method assigns to the
        // others.
        diagnostics.Select(static diagnostic => diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan)).ShouldBe(
            Enumerable.Range(0, textCount).Where(static index => index % 3 is 1).Select(static index => $"nl: $\"Hallo, {{other{index}}}!\""),
            ignoreOrder: true);
        diagnostics.Select(static diagnostic => diagnostic.GetMessage()).Distinct(StringComparer.Ordinal).ShouldHaveSingleItem()
            .ShouldBe("The 'nl' wording doesn't use 'name', but the 'en' wording shows it. Use 'name' in every language, or users of 'nl' won't see it.");
    }

    [Test]
    public async Task InlineTextInAssemblyWithOneLanguage_ShouldReportNothing()
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public static class Texts
            {
                public static LocalizableText Greeting(string name) => LocalizableText.Inline(en: $"Hello, {name}!");
            }
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task InlineTextInTopLevelStatementsAndInLambda_ShouldBeHeldToItsValues()
    {
        // Arrange & Act: a local variable of the top-level statements, and the parameter of a lambda the text is
        // created in.
        var diagnostics = await AnalyzeAsync("""
            using System;
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            var name = Console.ReadLine();
            var greeting = LocalizableText.Inline(en: $"Hello, {name}!", nl: "Hallo!");

            Func<int, LocalizableText> items = count => LocalizableText.Inline(en: $"{count} items", nl: "Artikelen");
            Func<int, LocalizableText> stock = count => LocalizableText.Inline(en: $"{count} in stock", nl: $"{count} op voorraad");
            """);

        // Assert
        diagnostics.Select(static diagnostic => diagnostic.GetMessage()).ShouldBe(
        [
            "The 'nl' wording doesn't use 'name', but the 'en' wording shows it. Use 'name' in every language, or users of 'nl' won't see it.",
            "The 'nl' wording doesn't use 'count', but the 'en' wording shows it. Use 'count' in every language, or users of 'nl' won't see it.",
        ], ignoreOrder: true);
    }

    [Test]
    [Arguments("Page_razor.g.cs", 1)]
    [Arguments("Pages/Index_cshtml.g.cs", 1)]
    [Arguments("Other.g.cs", 0)]
    public async Task InlineTextInGeneratedCode_ShouldBeReportedOnlyInRazorOutput(string outputPath, int expected)
    {
        // Arrange: what Razor writes for markup is the user's own, and what another generator writes isn't its
        // author's to change.
        const string generated = """
            // <auto-generated/>
            namespace TestApp;

            internal static class PageRender
            {
                public static CodeChops.Idioma.LocalizableText Render(string name) => CodeChops.Idioma.LocalizableText.Inline(en: $"Hello, {name}!", nl: "Hallo!", enGb: $"Hello, {name}!");
            }
            """;

        // Act
        var diagnostics = await AnalyzeAsync(Inline("""en: "Hello!", nl: "Hallo!", enGb: "Hello!" """), (outputPath, generated));

        // Assert
        diagnostics.Length.ShouldBe(expected);
        diagnostics.ShouldAllBe(diagnostic => diagnostic.Id == "LOC302");
    }
}
