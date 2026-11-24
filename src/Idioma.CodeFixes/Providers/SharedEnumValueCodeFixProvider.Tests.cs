using System.Collections.Immutable;
using CodeChops.Idioma.CodeFixes;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="SharedEnumValueCodeFixProvider"/>: a call that names an enum member whose value an earlier
/// member has (<c>LOC600</c>) becomes the member's own text in the generated class of its set.
/// </summary>
public sealed class SharedEnumValueCodeFixProviderTests
{
    #region Fixture
    private static string Source(string label) => $$"""
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace Shop.Orders;

        public enum OrderStatus { Default, None = Default, Shipped }

        public sealed class OrderStatusLocalizationEn : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
        {
            public string Default => "Default";
            public string None => "None";
            public string Shipped => "Shipped";
        }

        public sealed class OrderStatusLocalizationNl : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
        {
            public string Default => "Standaard";
            public string None => "Geen";
            public string Shipped => "Verzonden";
        }

        public enum ParcelStatus
        {
            [Localized(en: "Picked", nl: "Gepickt")]
            Picked,

            [Localized(en: "Collected", nl: "Opgehaald")]
            Collected = Picked,
        }

        public static class Labels
        {
            public static string Status => {{label}};
        }
        """;

    private static Task<ImmutableArray<(string Title, ImmutableArray<string> Sources)>> FixAsync(string source)
    {
        return CodeFixTestHelper.FixWithGeneratedCodeAsync(new SharedEnumValueCodeFixProvider(), new SharedEnumValueAnalyzer(), source);
    }
    #endregion

    [Test]
    [Arguments("OrderStatus.None.GetText()")]
    [Arguments("OrderStatusLocalization.GetTextByValue(OrderStatus.None)")]
    public async Task LaterMemberOfSet_ShouldBecomeItsOwnText(string label)
    {
        // Act
        var fixes = await FixAsync(Source(label));

        // Assert: the text of None, which the build no longer warns about.
        var fix = fixes.ShouldHaveSingleItem();
        fix.Title.ShouldBe("Use OrderStatusLocalization.None");

        var source = fix.Sources.ShouldHaveSingleItem();
        source.ShouldBe(Source("OrderStatusLocalization.None"));

        (await GeneratorTestHelper.AnalyzeAsync(new SharedEnumValueAnalyzer(), source, [])).ShouldBeEmpty();
        GeneratorTestHelper.CompileWithGenerators(source).ShouldBeEmpty();
    }

    [Test]
    public async Task LaterMarkedMember_ShouldGetNoFix()
    {
        // Arrange & Act
        var fixes = await FixAsync(Source("ParcelStatus.Collected.GetText()"));

        // Assert: its text would be a string?, which doesn't fit where a string was.
        fixes.ShouldBeEmpty();
    }

    [Test]
    public async Task FixAll_ShouldGiveEveryCallTheOwnTextOfItsMember()
    {
        // Arrange: two calls in one expression, and one that has no fix.
        var source = Source("OrderStatus.None.GetText() + OrderStatusLocalization.GetTextByValue(OrderStatus.None) + ParcelStatus.Collected.GetText()");

        // Act
        var fixedSource = (await CodeFixTestHelper.FixAllAfterGeneratorsAsync(new SharedEnumValueCodeFixProvider(), new SharedEnumValueAnalyzer(), source)).ShouldHaveSingleItem();

        // Assert: the marked member keeps its call, and its warning.
        fixedSource.ShouldBe(Source("OrderStatusLocalization.None + OrderStatusLocalization.None + ParcelStatus.Collected.GetText()"));
        (await GeneratorTestHelper.AnalyzeAsync(new SharedEnumValueAnalyzer(), fixedSource, [])).ShouldHaveSingleItem();
        GeneratorTestHelper.CompileWithGenerators(fixedSource).ShouldBeEmpty();
    }
}
