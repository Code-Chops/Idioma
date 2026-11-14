using System.Collections.Immutable;
using CodeChops.Idioma.CodeFixes;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="StringComparedAsTextCodeFixProvider"/>: <c>text.Equals(wording)</c>, a string compared as a text
/// (<c>LOC017</c>), becomes <c>text == wording</c>, which compares the wording, and its negation <c>text != wording</c>.
/// A lookup of a collection, and an <c>Equals</c> that is not the text's own, get no fix.
/// </summary>
public sealed class StringComparedAsTextCodeFixProviderTests
{
    #region Fixture
    private static string Member(string member) => $$"""
        using System;
        using System.Collections.Generic;
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace TestApp;

        public static class Comparisons
        {
            {{member}}
        }
        """;

    private static Task<ImmutableArray<(string Title, ImmutableArray<string> Sources)>> FixAsync(string member)
    {
        return CodeFixTestHelper.FixAsync(new StringComparedAsTextCodeFixProvider(), new TextComparisonAnalyzer(), Member(member));
    }
    #endregion

    [Test]
    [Arguments("public static bool Same(LocalizableText text, string wording) => text.Equals(wording);", "public static bool Same(LocalizableText text, string wording) => text == wording;")]
    [Arguments("""public static bool Same(LocalizableText text) => text.Equals("Afrekenen");""", """public static bool Same(LocalizableText text) => text == "Afrekenen";""")]
    [Arguments("public static bool Same(ResourceText text, string wording) => text.Equals(wording);", "public static bool Same(ResourceText text, string wording) => text == wording;")]
    [Arguments("public static int Same(LocalizableText text, string wording) { if (text.Equals(wording)) return 1; return 0; }", "public static int Same(LocalizableText text, string wording) { if (text == wording) return 1; return 0; }")]
    [Arguments("public static bool Same(LocalizableText text, string wording, bool flag) => flag && text.Equals(wording);", "public static bool Same(LocalizableText text, string wording, bool flag) => flag && text == wording;")]
    [Arguments("public static string Same(LocalizableText text, string wording) => text.Equals(wording).ToString();", "public static string Same(LocalizableText text, string wording) => (text == wording).ToString();")]
    [Arguments("""public static bool Same(LocalizableText text, string wording, bool flag) => text.Equals(flag ? wording : "-");""", """public static bool Same(LocalizableText text, string wording, bool flag) => text == (flag ? wording : "-");""")]
    [Arguments("""public static bool Same(LocalizableText text, string? wording) => text.Equals(wording ?? "-");""", """public static bool Same(LocalizableText text, string? wording) => text == (wording ?? "-");""")]
    public async Task EqualsOnText_ShouldBecomeEqualityOperator(string before, string after)
    {
        // Arrange & Act
        var fix = (await FixAsync(before)).ShouldHaveSingleItem();

        // Assert: the fix compiles, and leaves nothing for LOC017, or for LOC011 with wording written in the source.
        fix.Title.ShouldBe("Compare the wording with '=='");
        var fixedSource = fix.Sources.ShouldHaveSingleItem();
        fixedSource.ShouldBe(Member(after));
        GeneratorTestHelper.CompileWithGenerators(fixedSource).ShouldBeEmpty();
        (await GeneratorTestHelper.AnalyzeAsync(new TextComparisonAnalyzer(), fixedSource, [])).ShouldBeEmpty();
        (await GeneratorTestHelper.AnalyzeAsync(fixedSource)).ShouldBeEmpty();
    }

    [Test]
    [Arguments("public static bool Differs(LocalizableText text, string wording) => !text.Equals(wording);", "public static bool Differs(LocalizableText text, string wording) => text != wording;")]
    [Arguments("public static bool Differs(LocalizableText text, string wording) => !(text.Equals(wording));", "public static bool Differs(LocalizableText text, string wording) => text != wording;")]
    [Arguments("public static bool Differs(LocalizableText text, string wording, bool flag) => flag && !text.Equals(wording);", "public static bool Differs(LocalizableText text, string wording, bool flag) => flag && text != wording;")]
    public async Task NegatedEqualsOnText_ShouldBecomeInequalityOperator(string before, string after)
    {
        // Arrange & Act
        var fix = (await FixAsync(before)).ShouldHaveSingleItem();

        // Assert
        fix.Title.ShouldBe("Compare the wording with '!='");
        var fixedSource = fix.Sources.ShouldHaveSingleItem();
        fixedSource.ShouldBe(Member(after));
        GeneratorTestHelper.CompileWithGenerators(fixedSource).ShouldBeEmpty();
        (await GeneratorTestHelper.AnalyzeAsync(new TextComparisonAnalyzer(), fixedSource, [])).ShouldBeEmpty();
    }

    [Test]
    [Arguments("public static bool Found(List<LocalizableText> texts, string wording) => texts.Contains(wording);")]
    [Arguments("public static bool Same(LocalizableText text, string wording) => EqualityComparer<LocalizableText>.Default.Equals(text, wording);")]
    [Arguments("public static bool Same(LocalizableText text, string wording) => ((IEquatable<LocalizableText>)text).Equals(wording);")]
    [Arguments("public static bool Same(LocalizableText? text, string wording) => text?.Equals(wording) == true;")]
    [Arguments("public static bool Same(Tuple<LocalizableText>? pair, string wording) => pair?.Item1.Equals(wording) == true;")]
    [Arguments("public static bool Same(LocalizableText[]? texts, string wording) => texts?[0].Equals(wording) == true;")]
    [Arguments("public static bool Same(Tuple<Tuple<LocalizableText>>? pair, string wording) => pair?.Item1.Item1!.Equals(wording) == true;")]
    public async Task OtherComparison_ShouldOfferNoFix(string member)
    {
        // Arrange: LOC017 is reported, so the missing fix is the provider's choice, not a missing diagnostic.
        var diagnostics = await GeneratorTestHelper.AnalyzeAsync(new TextComparisonAnalyzer(), Member(member), []);
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("LOC017");

        // Act
        var fixes = await FixAsync(member);

        // Assert
        fixes.ShouldBeEmpty();
    }
}
