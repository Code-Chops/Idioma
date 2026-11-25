using System.Collections.Immutable;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="SharedEnumValueAnalyzer"/>: a call that names an enum member whose value an earlier member has
/// is reported (<c>LOC600</c>), and a call that names the first member, or a value the code only has at run time, isn't.
/// </summary>
public sealed class SharedEnumValueAnalyzerTests
{
    #region Fixture
    private const string Prelude = """
        using System;
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]
        """;

    /// <summary>
    /// An enum with a set, and an enum with [Localized] members, each with two members of one value.
    /// </summary>
    private const string Types = """
        public enum OrderStatus { Pending, Waiting = Pending, Shipped }

        public sealed class OrderStatusLocalizationEn : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
        {
            public string Pending => "Pending";
            public string Waiting => "Waiting";
            public string Shipped => "Shipped";
        }

        public sealed class OrderStatusLocalizationNl : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
        {
            public string Pending => "In behandeling";
            public string Waiting => "Wachtend";
            public string Shipped => "Verzonden";
        }

        public enum ParcelStatus
        {
            [Localized(en: "Picked", nl: "Gepickt")]
            Picked,

            [Localized(en: "Collected", nl: "Opgehaald")]
            Collected = Picked,

            Default = Picked,

            [Localized(en: "Lost", nl: "Kwijt")]
            Lost,
        }
        """;

    private const string Header = $"""
        {Prelude}

        namespace TestApp;

        {Types}
        """;

    /// <summary>
    /// A class with the given members.
    /// </summary>
    private static string Members(string members) => $$"""
        {{Header}}

        public static class Labels
        {
            {{members}}
        }
        """;

    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source)
    {
        return GeneratorTestHelper.AnalyzeAsync(new SharedEnumValueAnalyzer(), source, []);
    }
    #endregion

    [Test]
    [Arguments(
        "public static string Label => OrderStatusLocalization.GetTextByValue(OrderStatus.Waiting);",
        "GetTextByValue gives the text of 'OrderStatus.Pending' for 'OrderStatus.Waiting', because the two have the same value. Use 'OrderStatusLocalization.Waiting' for the text of 'Waiting'.")]
    [Arguments(
        "public static string Label => OrderStatus.Waiting.GetText();",
        "GetText() gives the text of 'OrderStatus.Pending' for 'OrderStatus.Waiting', because the two have the same value. Use 'OrderStatusLocalization.Waiting' for the text of 'Waiting'.")]
    public async Task LaterMemberOfSet_ShouldReportLOC600WithItsOwnText(string member, string expectedMessage)
    {
        // Act
        var diagnostics = await AnalyzeAsync(Members(member));

        // Assert: a warning on the member, which carries the text the fix writes.
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC600");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).ShouldBe("OrderStatus.Waiting");
        diagnostic.GetMessage().ShouldBe(expectedMessage);
        diagnostic.Properties[SharedEnumValueAnalyzer.OwnTextProperty].ShouldBe("OrderStatusLocalization.Waiting");
    }

    [Test]
    public async Task LaterMarkedMember_ShouldReportLOC600WithoutAFix()
    {
        // Act
        var diagnostics = await AnalyzeAsync(Members("public static string Label => ParcelStatus.Collected.GetText();"));

        // Assert: GetTextByMember gives a string?, which the fix doesn't write in place of a string.
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC600");
        diagnostic.GetMessage().ShouldBe(
            "GetText() gives the text of 'ParcelStatus.Picked' for 'ParcelStatus.Collected', because the two have the same value. "
            + "Use 'LocalizedText.GetTextByMember<ParcelStatus>(nameof(ParcelStatus.Collected))' for the text of 'Collected'.");
        diagnostic.Properties.ContainsKey(SharedEnumValueAnalyzer.OwnTextProperty).ShouldBeFalse();
    }

    [Test]
    [Arguments("public static string Label => OrderStatusLocalization.GetTextByValue(OrderStatus.Pending);")]
    [Arguments("public static string Label => OrderStatus.Pending.GetText();")]
    [Arguments("public static string Label => OrderStatus.Shipped.GetText();")]
    [Arguments("public static string Label(OrderStatus status) => status.GetText();")]
    [Arguments("public static string Label(OrderStatus status) => OrderStatusLocalization.GetTextByValue(status);")]
    [Arguments("public static string Label => OrderStatusLocalization.GetTextByValue((OrderStatus)0);")]
    [Arguments("public static string Label => OrderStatusLocalization.Waiting;")]
    [Arguments("public static string Label => ParcelStatus.Picked.GetText();")]
    [Arguments("public static string Label => ParcelStatus.Default.GetText();")]
    [Arguments("public static string Label => ParcelStatus.Lost.GetText();")]
    [Arguments("public static string Label => OrderStatus.Waiting.ToString();")]
    public async Task FirstMemberOrValueOnlyKnownAtRunTime_ShouldReportNothing(string member)
    {
        // Act: the first member of a value, a value the code only has at run time, a number, the member's own text, a
        // member without a text of its own, and a method of another name.
        var diagnostics = await AnalyzeAsync(Members(member));

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("Page_razor.g.cs", 2)]
    [Arguments("Pages/Index_cshtml.g.cs", 2)]
    [Arguments("Page_razor.decl.g.cs", 2)]
    [Arguments("Other.g.cs", 0)]
    public async Task LaterMemberInGeneratedCode_ShouldBeReportedOnlyInRazorOutput(string outputPath, int expected)
    {
        // Arrange: what Razor writes for @OrderStatus.Waiting.GetText() in markup is the user's own, and what another
        // generator writes isn't its author's to change.
        const string generated = """
            // <auto-generated/>
            namespace TestApp;

            internal static class PageRender
            {
                public static string Render() => OrderStatus.Waiting.GetText() + ParcelStatus.Collected.GetText();
            }
            """;

        // Act
        var diagnostics = await GeneratorTestHelper.AnalyzeAsync(new SharedEnumValueAnalyzer(), Header, [], (outputPath, generated));

        // Assert
        diagnostics.Length.ShouldBe(expected);
        diagnostics.ShouldAllBe(diagnostic => diagnostic.Id == "LOC600");
    }

    [Test]
    public async Task CallOutsideTheNamespaceOfTheSet_ShouldNameTheClassAsTheCallCan()
    {
        // Arrange
        const string source = $$"""
            {{Prelude}}

            namespace TestApp
            {
            {{Types}}
            }

            namespace Other
            {
                public static class Labels
                {
                    public static string Label => TestApp.OrderStatusLocalization.GetTextByValue(TestApp.OrderStatus.Waiting);
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzeAsync(source);

        // Assert
        diagnostics.ShouldHaveSingleItem().Properties[SharedEnumValueAnalyzer.OwnTextProperty].ShouldBe("TestApp.OrderStatusLocalization.Waiting");
    }
}
