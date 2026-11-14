using System.Collections.Immutable;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="TextComparisonAnalyzer"/>: a string that <c>Equals</c> or a lookup of a collection compares as a
/// text is reported, whatever languages the assembly declares, while <c>==</c>, a cast, and a string that becomes a text
/// without being compared are not.
/// </summary>
public sealed class TextComparisonAnalyzerTests
{
    #region Fixture
    private const string Usings = """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using CodeChops.Idioma;
        """;

    private const string TwoLanguages = """[assembly: SupportedLanguages("en", "nl")]""";

    private static string Member(string member, string languages = TwoLanguages) => $$"""
        {{Usings}}

        {{languages}}

        namespace TestApp;

        public static class Comparisons
        {
            private static void Take(LocalizableText text) { }

            {{member}}
        }
        """;

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeMemberAsync(string member, string languages = TwoLanguages)
    {
        var source = Member(member, languages);

        // A case that does not compile would pass, or fail, for the wrong reason.
        GeneratorTestHelper.CompileWithGenerators(source).ShouldBeEmpty();

        return await GeneratorTestHelper.AnalyzeAsync(new TextComparisonAnalyzer(), source, []);
    }
    #endregion

    [Test]
    [Arguments("""public static bool Bad(LocalizableText text) => text.Equals("Afrekenen");""", "LocalizableText.Equals")]
    [Arguments("public static bool Bad(LocalizableText text, string wording) => text.Equals(wording);", "LocalizableText.Equals")]
    [Arguments("public static bool Bad(ResourceText text, string wording) => text.Equals(wording);", "ResourceText.Equals")]
    [Arguments("public static bool Bad(LocalizableText text, string wording) => ((IEquatable<LocalizableText>)text).Equals(wording);", "IEquatable.Equals")]
    [Arguments("public static bool Bad(LocalizableText text, string wording) => EqualityComparer<LocalizableText>.Default.Equals(text, wording);", "EqualityComparer.Equals")]
    [Arguments("public static bool Bad(LocalizableText text, string wording) => EqualityComparer<LocalizableText>.Default.Equals(wording, text);", "EqualityComparer.Equals")]
    [Arguments("public static bool Bad(List<LocalizableText> texts, string wording) => texts.Contains(wording);", "List.Contains")]
    [Arguments("public static int Bad(List<LocalizableText> texts, string wording) => texts.IndexOf(wording);", "List.IndexOf")]
    [Arguments("public static int Bad(List<LocalizableText> texts, string wording) => texts.LastIndexOf(wording);", "List.LastIndexOf")]
    [Arguments("public static bool Bad(List<LocalizableText> texts, string wording) => texts.Remove(wording);", "List.Remove")]
    [Arguments("public static bool Bad(HashSet<LocalizableText> texts, string wording) => texts.Contains(wording);", "HashSet.Contains")]
    [Arguments("public static bool Bad(ICollection<LocalizableText> texts, string wording) => texts.Contains(wording);", "ICollection.Contains")]
    [Arguments("public static bool Bad(Dictionary<LocalizableText, int> counts, string wording) => counts.ContainsKey(wording);", "Dictionary.ContainsKey")]
    [Arguments("public static bool Bad(Dictionary<LocalizableText, int> counts, string wording) => counts.TryGetValue(wording, out _);", "Dictionary.TryGetValue")]
    [Arguments("public static bool Bad(Dictionary<LocalizableText, int> counts, string wording) => counts.Remove(wording);", "Dictionary.Remove")]
    [Arguments("public static int Bad(Dictionary<LocalizableText, int> counts, string wording) => counts.GetValueOrDefault(wording);", "CollectionExtensions.GetValueOrDefault")]
    [Arguments("public static bool Bad(Dictionary<int, LocalizableText> texts, string wording) => texts.ContainsValue(wording);", "Dictionary.ContainsValue")]
    [Arguments("public static bool Bad(IEnumerable<LocalizableText> texts, string wording) => texts.Contains<LocalizableText>(wording);", "Enumerable.Contains")]
    // The default comparer, passed or not, compares by Equals.
    [Arguments("public static bool Bad(IEnumerable<LocalizableText> texts, string wording) => texts.Contains<LocalizableText>(wording, null);", "Enumerable.Contains")]
    [Arguments("public static bool Bad(IEnumerable<LocalizableText> texts, string wording) => texts.Contains<LocalizableText>(wording, EqualityComparer<LocalizableText>.Default);", "Enumerable.Contains")]
    [Arguments("public static bool Bad(System.Collections.Concurrent.ConcurrentDictionary<LocalizableText, int> counts, string wording) => counts.TryRemove(wording, out _);", "ConcurrentDictionary.TryRemove")]
    [Arguments("public static bool Bad(System.Collections.Concurrent.ConcurrentDictionary<LocalizableText, int> counts, string wording) => counts.TryUpdate(wording, 2, 1);", "ConcurrentDictionary.TryUpdate")]
    // These add an entry beside the one they do not find.
    [Arguments("public static int Bad(System.Collections.Concurrent.ConcurrentDictionary<LocalizableText, int> counts, string wording) => counts.GetOrAdd(wording, 1);", "ConcurrentDictionary.GetOrAdd")]
    [Arguments("public static int Bad(System.Collections.Concurrent.ConcurrentDictionary<LocalizableText, int> counts, string wording) => counts.AddOrUpdate(wording, 1, (_, count) => count + 1);", "ConcurrentDictionary.AddOrUpdate")]
    [Arguments("public static int Bad(SortedList<LocalizableText, int> counts, string wording) => counts.IndexOfKey(wording);", "SortedList.IndexOfKey")]
    [Arguments("public static int Bad(SortedList<int, LocalizableText> texts, string wording) => texts.IndexOfValue(wording);", "SortedList.IndexOfValue")]
    // object.Equals asks for objects, so no conversion shows the string, either way round.
    [Arguments("public static bool Bad(LocalizableText text, string wording) => object.Equals(text, wording);", "Object.Equals")]
    [Arguments("public static bool Bad(LocalizableText text, string wording) => object.Equals(wording, text);", "Object.Equals")]
    [Arguments("""public static bool Bad(ResourceText text) => Equals(text, "Orders");""", "Object.Equals")]
    // A static method takes the collection first, as an extension does.
    [Arguments("public static int Bad(LocalizableText[] texts, string wording) => Array.IndexOf<LocalizableText>(texts, wording);", "Array.IndexOf")]
    [Arguments("public static int Bad(LocalizableText[] texts, string wording) => Array.LastIndexOf<LocalizableText>(texts, wording);", "Array.LastIndexOf")]
    // Without the type argument the compiler takes the overload of Array and object: no conversion, and no match either.
    [Arguments("public static int Bad(LocalizableText[] texts, string wording) => Array.IndexOf(texts, wording);", "Array.IndexOf")]
    [Arguments("public static int Bad(LocalizableText[] texts, string wording) => Array.LastIndexOf(texts, wording);", "Array.LastIndexOf")]
    [Arguments("""public static int Bad(ResourceText[] texts) => Array.IndexOf(texts, "Orders");""", "Array.IndexOf")]
    // The key of a dictionary, read through its indexer.
    [Arguments("public static int Bad(Dictionary<LocalizableText, int> counts, string wording) => counts[wording];", "Dictionary.this[]")]
    [Arguments("public static int Bad(IDictionary<LocalizableText, int> counts, string wording) => counts[wording];", "IDictionary.this[]")]
    [Arguments("public static int Bad(IReadOnlyDictionary<LocalizableText, int> counts, string wording) => counts[wording];", "IReadOnlyDictionary.this[]")]
    [Arguments("public static void Bad(Dictionary<LocalizableText, int> counts, string wording) => counts[wording] += 1;", "Dictionary.this[]")]
    [Arguments("public static int? Bad(Dictionary<LocalizableText, int>? counts, string wording) => counts?[wording];", "Dictionary.this[]")]
    public async Task StringComparedAsText_ShouldReportLOC017(string member, string method)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeMemberAsync(member);

        // Assert: the message names what compares, and how to compare the wording instead.
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(TextComparisonAnalyzer.StringComparedAsTextDiagnosticId);
        diagnostic.Id.ShouldBe("LOC017");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain($"'{method}'");
        diagnostic.GetMessage().ShouldContain("==");
    }

    [Test]
    public async Task StringComparedAsText_ShouldBeReportedAtTheString()
    {
        // Arrange & Act
        var diagnostics = await AnalyzeMemberAsync("public static bool Bad(LocalizableText text, string wording) => text.Equals(wording);");

        // Assert: the string is what to change, not the call.
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).ShouldBe("wording");
    }

    [Test]
    [Arguments("public static bool Good(LocalizableText text, string wording) => text == wording;")]
    [Arguments("public static bool Good(LocalizableText text, string wording) => wording == text;")]
    [Arguments("public static bool Good(LocalizableText text, string wording) => text != wording;")]
    [Arguments("""public static bool Good(LocalizableText text) => text == "Afrekenen";""")]
    [Arguments("public static bool Good(List<LocalizableText> texts, string wording) => texts.Any(text => text == wording);")]
    [Arguments("public static bool Good(LocalizableText text, string wording) => wording.Equals(text);")]
    [Arguments("public static bool Good(LocalizableText text, string wording) => string.Equals(wording, text);")]
    [Arguments("public static bool Good(LocalizableText text, LocalizableText other) => text.Equals(other);")]
    [Arguments("public static bool Good(LocalizableText text, string wording) => text.Equals((LocalizableText)wording);")]
    [Arguments("public static bool Good(LocalizableText text, string wording) => text.Equals(new LocalizableText(wording));")]
    [Arguments("public static bool Good(List<LocalizableText> texts, LocalizableText text) => texts.Contains(text);")]
    [Arguments("public static bool Good(List<LocalizableText> texts, string wording) => texts.Contains((LocalizableText)wording);")]
    [Arguments("public static void Good(List<LocalizableText> texts, string wording) => texts.Add(wording);")]
    [Arguments("public static void Good(Dictionary<LocalizableText, int> counts, string wording) => counts[wording] = 1;")]
    // An entry added in an initializer is stored, not found.
    [Arguments("public static Dictionary<LocalizableText, int> Good(string wording) => new() { [wording] = 1 };")]
    // So is one a deconstruction assigns, also in a tuple of a tuple.
    [Arguments("public static void Good(Dictionary<LocalizableText, int> counts, string wording) { int other; (counts[wording], other) = (1, 2); }")]
    [Arguments("public static void Good(Dictionary<LocalizableText, int> counts, string wording) { int a, b; (a, (counts[wording], b)) = (1, (2, 3)); }")]
    // A comparer of the caller's may compare the wording.
    [Arguments("public static bool Good(IEnumerable<LocalizableText> texts, string wording, IEqualityComparer<LocalizableText> comparer) => texts.Contains<LocalizableText>(wording, comparer);")]
    [Arguments("public static bool Good(LocalizableText text, string wording, IEqualityComparer<LocalizableText> comparer) => comparer.Equals(text, wording);")]
    [Arguments("public static bool Good(LocalizableText text, string wording, EqualityComparer<LocalizableText> comparer) => comparer.Equals(text, wording);")]
    [Arguments("public static bool Good(LocalizableText text, string wording) => object.Equals(text, (LocalizableText)wording);")]
    [Arguments("public static bool Good(LocalizableText text, LocalizableText other) => object.Equals(text, other);")]
    [Arguments("public static bool Good(string wording, string other) => object.Equals(wording, other);")]
    // Positions are no texts, and a text key is a text.
    [Arguments("public static LocalizableText Good(List<LocalizableText> texts) => texts[0];")]
    [Arguments("public static int Good(Dictionary<LocalizableText, int> counts, LocalizableText text) => counts[text];")]
    [Arguments("public static int Good(Dictionary<LocalizableText, int> counts, string wording) => counts[(LocalizableText)wording];")]
    [Arguments("public static int Good(Dictionary<string, int> counts, string wording) => counts[wording];")]
    [Arguments("public static int Good(LocalizableText[] texts, LocalizableText text) => Array.IndexOf(texts, text);")]
    [Arguments("public static int Good(string[] names, string wording) => Array.IndexOf(names, wording);")]
    [Arguments("public static int Good(LocalizableText[] texts, string wording) => Array.IndexOf(texts, (LocalizableText)wording);")]
    [Arguments("public static bool Good(LocalizableText[] texts, string wording) => Array.Exists(texts, text => text == wording);")]
    [Arguments("public static LocalizableText Good(Dictionary<int, LocalizableText> texts, string wording) => texts.GetValueOrDefault(1, wording);")]
    [Arguments("public static void Good(string wording) => Take(wording);")]
    [Arguments("private static bool Remove(LocalizableText reason) => true; public static bool Good(string wording) => Remove(wording);")]
    public async Task WordingComparedOrStringNotCompared_ShouldReportNothing(string member)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeMemberAsync(member);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("")]
    [Arguments("""[assembly: SupportedLanguages("en")]""")]
    public async Task OneLanguageOrNone_ShouldStillReport(string languages)
    {
        // Arrange & Act: a text of a set equals no string, however many languages the assembly declares.
        var diagnostics = await AnalyzeMemberAsync("""public static bool Bad(LocalizableText text) => text.Equals("Checkout");""", languages);

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("LOC017");
    }

    [Test]
    [Arguments("Page_razor.g.cs")]
    [Arguments("Pages/Index_cshtml.g.cs")]
    public async Task InRazorOutput_ShouldReport(string outputPath)
    {
        // Arrange: what Razor writes for @if (Text.Equals(Search)) in a component or a page: generated code, but the
        // user's own markup.
        const string razorOutput = """
            // <auto-generated/>
            namespace TestApp;

            internal static class PageRender
            {
                public static bool Render(CodeChops.Idioma.LocalizableText text, string search) => text.Equals(search);
            }
            """;

        // Act
        var diagnostics = await GeneratorTestHelper.AnalyzeAsync(new TextComparisonAnalyzer(), Member(""), [], (outputPath, razorOutput));

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("LOC017");
    }

    [Test]
    public async Task InOtherGeneratedCode_ShouldReportNothing()
    {
        // Arrange: whoever generated this is the only one who could fix it.
        const string generated = """
            // <auto-generated/>
            namespace TestApp;

            internal static class Generated
            {
                public static bool Run(CodeChops.Idioma.LocalizableText text, string search) => text.Equals(search);
            }
            """;

        // Act
        var diagnostics = await GeneratorTestHelper.AnalyzeAsync(new TextComparisonAnalyzer(), Member(""), [], ("Other.g.cs", generated));

        // Assert
        diagnostics.ShouldBeEmpty();
    }
}
