using System.Collections.Immutable;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="KeptTextAnalyzer"/>: a text in the initializer of a static field or property is reported
/// (<c>LOC602</c>), and a text that is read later isn't.
/// </summary>
public sealed class KeptTextAnalyzerTests
{
    #region Fixture
    private const string Header = """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace TestApp;

        public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
        {
            public string Checkout => "Checkout";
            public string Greeting(string name) => $"Hello, {name}!";
        }

        public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
        {
            public string Checkout => "Afrekenen";
            public string Greeting(string name) => $"Hallo, {name}!";
        }

        public enum ParcelStatus
        {
            [Localized(en: "Picked", nl: "Gepickt")]
            Picked,
        }

        public class Address
        {
            [Localized(en: "City", nl: "Plaats")]
            public string City { get; set; } = "";
        }

        public static partial class ShopLocalization
        {
            public static int TextCount => 2;
            public static string Separator => " / ";
        }
        """;

    /// <summary>
    /// A class with the given members.
    /// </summary>
    private static string Members(string members) => $$"""
        {{Header}}

        public class Labels
        {
            {{members}}
        }
        """;

    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source)
    {
        return GeneratorTestHelper.AnalyzeAsync(new KeptTextAnalyzer(), source, []);
    }
    #endregion

    [Test]
    [Arguments("private static readonly string Label = ShopLocalization.Checkout;", "ShopLocalization.Checkout", "Label")]
    [Arguments("public static string Label { get; } = ShopLocalization.Checkout;", "ShopLocalization.Checkout", "Label")]
    [Arguments("""private static readonly string Label = ShopLocalization.Greeting("Ada");""", """ShopLocalization.Greeting("Ada")""", "Label")]
    [Arguments("""private static readonly string Label = ShopLocalization.GetTextByKey("Checkout");""", """ShopLocalization.GetTextByKey("Checkout")""", "Label")]
    [Arguments("""private static readonly string Label = LocalizedText.Inline(en: "Orders", nl: "Bestellingen");""", """LocalizedText.Inline(en: "Orders", nl: "Bestellingen")""", "Label")]
    [Arguments("private static readonly string? Label = LocalizedText.GetTextByMember<Address>(nameof(Address.City));", "LocalizedText.GetTextByMember<Address>(nameof(Address.City))", "Label")]
    [Arguments("private static readonly string? Label = LocalizedText.GetTextOfType<Address>();", "LocalizedText.GetTextOfType<Address>()", "Label")]
    [Arguments("private static readonly string Label = ParcelStatus.Picked.GetText();", "ParcelStatus.Picked.GetText()", "Label")]
    [Arguments("private static readonly string[] All = [ShopLocalization.Checkout];", "ShopLocalization.Checkout", "All")]
    [Arguments("""private static readonly Dictionary<string, string> ByKey = new() { ["checkout"] = ShopLocalization.Checkout };""", "ShopLocalization.Checkout", "ByKey")]
    [Arguments("""private static readonly string Label = "Go to " + ShopLocalization.Checkout.ToLowerInvariant();""", "ShopLocalization.Checkout", "Label")]
    [Arguments("private static readonly List<string> All = Enum.GetValues<ParcelStatus>().Select(status => status.GetText()).ToList();", "status.GetText()", "All")]
    [Arguments("private static readonly string[] All = [.. new[] { 1, 2 }.Select(_ => ShopLocalization.Checkout)];", "ShopLocalization.Checkout", "All")]
    [Arguments("private static readonly string First = new[] { 1, 2 }.Select(_ => ShopLocalization.Checkout).First();", "ShopLocalization.Checkout", "First")]
    [Arguments("private static readonly Lazy<string> Label = new(() => ShopLocalization.Checkout);", "ShopLocalization.Checkout", "Label")]
    public async Task TextInStaticInitializer_ShouldReportLOC602(string member, string expectedText, string expectedMember)
    {
        // Act
        var diagnostics = await AnalyzeAsync(Members(member));

        // Assert: one warning, on the text, that names the text and the member that keeps it.
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC602");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.Location.SourceTree!.ToString().Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length).ShouldBe(expectedText);
        diagnostic.GetMessage().ShouldBe(
            $"'{expectedText}' is read once, when '{expectedMember}' is initialized, so every user gets the wording of the first user's language. Read the text where you show it, such as in a property written with '=>'.");
    }

    [Test]
    [Arguments("public static string Label => ShopLocalization.Checkout;")]
    [Arguments("public static string Label { get { return ShopLocalization.Checkout; } }")]
    [Arguments("private static readonly Func<string> Label = () => ShopLocalization.Checkout;")]
    [Arguments("private static readonly Func<string, string> Greet = static name => ShopLocalization.Greeting(name);")]
    [Arguments("private readonly string label = ShopLocalization.Checkout;")]
    [Arguments("public string Label { get; } = ShopLocalization.Checkout;")]
    [Arguments("""private static readonly string Key = nameof(ShopLocalization.Checkout);""")]
    [Arguments("""private static readonly string Brand = "Idioma";""")]
    [Arguments("""private const string Brand = "Idioma";""")]
    [Arguments("public static string Describe() { var label = ShopLocalization.Checkout; return label; }")]
    [Arguments("private static readonly string Machine = Environment.MachineName;")]
    [Arguments("private static readonly IEnumerable<string> All = new[] { 1, 2 }.Select(_ => ShopLocalization.Checkout);")] // Read as it's enumerated
    [Arguments("private static readonly List<Func<string>> All = new[] { 1, 2 }.Select(_ => (Func<string>)(() => ShopLocalization.Checkout)).ToList();")]
    [Arguments("private static readonly int Count = ShopLocalization.TextCount;")] // A helper next to the texts
    [Arguments("private static readonly string Separator = ShopLocalization.Separator;")]
    public async Task TextReadLaterOrKeptPerObject_ShouldReportNothing(string member)
    {
        // Act
        var diagnostics = await AnalyzeAsync(Members(member));

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task StaticMemberOfClassThatIsNoGeneratedClass_ShouldReportNothing()
    {
        // Arrange: a static class of the user's own, next to an interface of its name that isn't a contract.
        var source = Members("private static readonly string Label = Own.Name;") + """

            public interface IOwn
            {
                string Name { get; }
            }

            public static class Own
            {
                public static string Name => "Own";
            }
            """;

        // Act
        var diagnostics = await AnalyzeAsync(source);

        // Assert
        diagnostics.ShouldBeEmpty();
    }
}
