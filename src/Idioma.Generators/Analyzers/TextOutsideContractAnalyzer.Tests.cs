using System.Collections.Immutable;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="TextOutsideContractAnalyzer"/>: a set has no text that its contract doesn't have, because no
/// user would see it, and a set of an enum has no text that isn't a member, because <c>GetText()</c> never gives it.
/// </summary>
public sealed class TextOutsideContractAnalyzerTests
{
    #region Fixture
    /// <summary>
    /// A library whose contract an app's set can implement for a language of its own.
    /// </summary>
    private static MetadataReference ShopLibrary { get; } = GeneratorTestHelper.CompileLibraryWithGenerators("OutsideShopLibrary", """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace ShopLibrary;

        public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
        {
            public string Total => "Total";
        }

        public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
        {
            public string Total => "Totaal";
        }
        """);

    /// <summary>
    /// A library with a set of its enum, which has a text that isn't a member: one an app's set of it has to implement.
    /// </summary>
    private static MetadataReference StatusLibrary { get; } = GeneratorTestHelper.CompileLibraryWithGenerators("OutsideStatusLibrary", """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace StatusLibrary;

        public enum OrderStatus { Pending }

        public class OrderStatusLocalizationEn : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
        {
            public string Pending => "Pending";
            public string Caption => "Order status";
        }

        public class OrderStatusLocalizationNl : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
        {
            public string Pending => "In behandeling";
            public string Caption => "Bestelstatus";
        }
        """);

    /// <summary>
    /// A contract generated from an English set, and a Dutch set of it, each holding two texts and the given members.
    /// </summary>
    private static string Sets(string english, string dutch) => $$"""
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace TestApp;

        public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
        {
            public string Checkout => "Checkout";
            public string Greeting(string name) => $"Hello, {name}!";
            {{english}}
        }

        public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
        {
            public string Checkout => "Afrekenen";
            public string Greeting(string name) => $"Hallo, {name}!";
            {{dutch}}
        }
        """;

    /// <summary>
    /// A set of the enum <c>OrderStatus</c> in English and Dutch, each holding the given members.
    /// </summary>
    private static string EnumSets(string english, string dutch) => $$"""
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace TestApp;

        public enum OrderStatus { Pending, Shipped }

        public class OrderStatusLocalizationEn : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
        {
            public string Pending => "Pending";
            {{english}}
        }

        public class OrderStatusLocalizationNl : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
        {
            public string Pending => "In behandeling";
            public string Shipped => "Verzonden";
            {{dutch}}
        }
        """;

    /// <summary>
    /// A contract generated from an English set, and a British set that derives from it, holding the given members.
    /// </summary>
    private static string RegionalSets(string english, string british) => $$"""
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "en-GB")]

        namespace TestApp;

        public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
        {
            {{english}}
        }

        public class ShopLocalizationEnGb : ShopLocalizationEn
        {
            {{british}}
        }
        """;

    /// <summary>
    /// What a build reports: this analyzer's diagnostics, and the <c>LOC305</c> the generator reports in the
    /// default-language set in its place.
    /// </summary>
    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source, params MetadataReference[] references)
    {
        var fromGenerator = GeneratorTestHelper.GetGeneratorDiagnostics(source, references)
            .Where(static diagnostic => String.Equals(diagnostic.Id, TextOutsideContractAnalyzer.TextOutsideEnumDiagnosticId, StringComparison.Ordinal));

        return [.. fromGenerator, .. await GeneratorTestHelper.AnalyzeAsync(new TextOutsideContractAnalyzer(), source, references)];
    }

    private static string At(Diagnostic diagnostic)
    {
        return diagnostic.Location.SourceTree!.ToString()[diagnostic.Location.SourceSpan.Start..];
    }
    #endregion

    [Test]
    [Arguments("""public string Basket => "Mandje";""", "Basket")]
    [Arguments("""public string ItemsInBasket(int count) => $"{count} in je mandje";""", "ItemsInBasket")]
    public async Task TextOnlyInDutch_ShouldReportLOC304(string dutch, string text)
    {
        // Arrange & Act: the contract comes from the English set, which has no such text.
        var diagnostics = await AnalyzeAsync(Sets("", dutch));

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC304");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().ShouldBe($"'ShopLocalizationNl.{text}' isn't a text of 'IShopLocalization', so users of 'nl' won't see it. Add '{text}' to the default-language set, or mark it [ExcludeFromLocalizationDiscovery] if it isn't a text.");
        At(diagnostic).ShouldStartWith(text);
    }

    [Test]
    public async Task TextOnlyInDutchSetOfEnum_ShouldReportLOC305InPlaceOfLOC304()
    {
        // Arrange & Act: adding it to the English set would make it a text that isn't a member there.
        var diagnostics = await AnalyzeAsync(EnumSets("""public string Shipped => "Shipped";""", """public string Lost { get; } = "Kwijt";"""));

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC305");
        diagnostic.GetMessage().ShouldBe("'OrderStatusLocalizationNl.Lost' isn't a member of 'OrderStatus', so GetText() never gives it. Add 'Lost' to 'OrderStatus', move it to a set of its own, or mark it [ExcludeFromLocalizationDiscovery] if it isn't a text.");
    }

    [Test]
    [Arguments("""public string Caption => "Order status";""", """public string Caption => "Bestelstatus";""", "Caption")]
    [Arguments("""public string Orders(int count) => $"{count} orders";""", """public string Orders(int count) => $"{count} bestellingen";""", "Orders")]
    public async Task TextThatIsNoMemberInSetsOfEnum_ShouldReportLOC305InEachSet(string english, string dutch, string text)
    {
        // Arrange & Act: the contract has the text, because the English set declares it.
        var diagnostics = await AnalyzeAsync(EnumSets($"""public string Shipped => "Shipped"; {english}""", dutch));

        // Assert
        diagnostics.Length.ShouldBe(2);
        diagnostics.ShouldAllBe(diagnostic => diagnostic.Id == "LOC305" && diagnostic.Severity == DiagnosticSeverity.Warning);
        diagnostics.Select(static diagnostic => diagnostic.GetMessage()).OrderBy(static message => message, StringComparer.Ordinal).ShouldBe([
            $"'OrderStatusLocalizationEn.{text}' isn't a member of 'OrderStatus', so GetText() never gives it. Add '{text}' to 'OrderStatus', move it to a set of its own, or mark it [ExcludeFromLocalizationDiscovery] if it isn't a text.",
            $"'OrderStatusLocalizationNl.{text}' isn't a member of 'OrderStatus', so GetText() never gives it. Add '{text}' to 'OrderStatus', move it to a set of its own, or mark it [ExcludeFromLocalizationDiscovery] if it isn't a text.",
        ]);
        diagnostics.ShouldAllBe(diagnostic => At(diagnostic).StartsWith(text, StringComparison.Ordinal));
    }

    [Test]
    public async Task TextThatIsNoMemberInDefaultSetOfEnum_ShouldBeLeftToTheGenerator()
    {
        // Arrange: the generator reports it, because the Dutch set that lacks it doesn't compile, and a build drops an
        // analyzer's diagnostics next to that error.
        var source = EnumSets("""public string Shipped => "Shipped"; public string Lost => "Lost";""", """public string Lost => "Kwijt";""");

        // Act
        var fromAnalyzer = await GeneratorTestHelper.AnalyzeAsync(new TextOutsideContractAnalyzer(), source, []);
        var fromGenerator = GeneratorTestHelper.GetGeneratorDiagnostics(source);

        // Assert: once each, at the text of its own set.
        fromAnalyzer.ShouldHaveSingleItem().GetMessage().ShouldStartWith("'OrderStatusLocalizationNl.Lost' isn't a member of 'OrderStatus'");
        fromGenerator.ShouldHaveSingleItem().GetMessage().ShouldStartWith("'OrderStatusLocalizationEn.Lost' isn't a member of 'OrderStatus'");
    }

    [Test]
    public async Task TextThatIsNoMemberOfEitherEnum_ShouldNameBoth()
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public enum OrderStatus { Pending }
            public enum PaymentStatus { Paid }

            public class StatusLocalizationEn : ILocalizationSet<OrderStatus>, ILocalizationSet<PaymentStatus>, IStatusLocalization
            {
                public string Pending => "Pending";
                public string Paid => "Paid";
                public string Caption => "Status";
            }
            """);

        // Assert
        diagnostics.ShouldHaveSingleItem().GetMessage().ShouldBe("'StatusLocalizationEn.Caption' isn't a member of 'OrderStatus' or 'PaymentStatus', so GetText() never gives it. Add 'Caption' to 'OrderStatus' or 'PaymentStatus', move it to a set of its own, or mark it [ExcludeFromLocalizationDiscovery] if it isn't a text.");
    }

    [Test]
    public async Task TextThatIsNoMemberOfEnumOfAnotherAssembly_ShouldAdviseToMoveIt()
    {
        // Arrange & Act: DayOfWeek can't get a member.
        var diagnostics = await AnalyzeAsync("""
            using System;
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public class DayLocalizationEn : ILocalizationSet<DayOfWeek>, IDayLocalization
            {
                public string Sunday => "Sunday";
                public string Monday => "Monday";
                public string Tuesday => "Tuesday";
                public string Wednesday => "Wednesday";
                public string Thursday => "Thursday";
                public string Friday => "Friday";
                public string Saturday => "Saturday";
                public string Caption => "Day";
            }
            """);

        // Assert
        diagnostics.ShouldHaveSingleItem().GetMessage().ShouldBe("'DayLocalizationEn.Caption' isn't a member of 'DayOfWeek', so GetText() never gives it. Move it to a set of its own, or mark it [ExcludeFromLocalizationDiscovery] if it isn't a text.");
    }

    [Test]
    public async Task AppSetOfLibraryEnum_ShouldReportOnlyTextOutsideLibraryContract()
    {
        // Arrange & Act: the library's contract has Caption, which the app's set has to implement.
        var diagnostics = await AnalyzeAsync("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]

            namespace TestApp;

            public sealed class OrderStatusLocalizationDe : ILocalizationSet<StatusLibrary.OrderStatus>, StatusLibrary.IOrderStatusLocalization
            {
                public string Pending => "In Bearbeitung";
                public string Caption => "Bestellstatus";
                public string Lost => "Verloren";
            }
            """, StatusLibrary);

        // Assert
        diagnostics.ShouldHaveSingleItem().GetMessage().ShouldBe("'OrderStatusLocalizationDe.Lost' isn't a member of 'OrderStatus', so GetText() never gives it. Move it to a set of its own, or mark it [ExcludeFromLocalizationDiscovery] if it isn't a text.");
    }

    [Test]
    public async Task BritishSetOfEnumOverridingTextThatIsNoMember_ShouldReportItInEnglishSetOnly()
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "en-GB")]

            namespace TestApp;

            public enum OrderStatus { Pending }

            public class OrderStatusLocalizationEn : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
            {
                public string Pending => "Pending";
                public virtual string Caption => "Order status";
            }

            public class OrderStatusLocalizationEnGb : OrderStatusLocalizationEn
            {
                public override string Caption => "Order state";
            }
            """);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.GetMessage().ShouldStartWith("'OrderStatusLocalizationEn.Caption' isn't a member of 'OrderStatus'");
    }

    [Test]
    public async Task TextThatIsNoMemberOverridingBaseThatIsNoSet_ShouldReportLOC305()
    {
        // Arrange & Act: the base isn't a set, so nothing reports the text where it's declared.
        var diagnostics = await AnalyzeAsync("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public enum OrderStatus { Pending }

            public abstract class StatusTexts
            {
                public abstract string Caption { get; }
            }

            public class OrderStatusLocalizationEn : StatusTexts, ILocalizationSet<OrderStatus>, IOrderStatusLocalization
            {
                public string Pending => "Pending";
                public override string Caption => "Order status";
            }
            """);

        // Assert
        diagnostics.ShouldHaveSingleItem().GetMessage().ShouldStartWith("'OrderStatusLocalizationEn.Caption' isn't a member of 'OrderStatus'");
    }

    [Test]
    public async Task TextOnlyInBritishSet_ShouldReportLOC304()
    {
        // Arrange & Act: the British set rewords Cart, and adds Trolley, which English doesn't have.
        var diagnostics = await AnalyzeAsync(RegionalSets(
            """public virtual string Cart => "Cart";""",
            """public override string Cart => "Basket"; public string Trolley => "Trolley";"""));

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.GetMessage().ShouldBe("'ShopLocalizationEnGb.Trolley' isn't a text of 'IShopLocalization', so users of 'en-GB' won't see it. Add 'Trolley' to the default-language set, or mark it [ExcludeFromLocalizationDiscovery] if it isn't a text.");
        At(diagnostic).ShouldStartWith("Trolley");
    }

    [Test]
    public async Task TextOnlyInAppSetOfLibraryContract_ShouldAdviseToRemoveIt()
    {
        // Arrange & Act: the app can't add a text to the library's contract.
        var diagnostics = await AnalyzeAsync("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]

            namespace TestApp;

            public sealed class ShopLocalizationDe : ILocalizationSet, ShopLibrary.IShopLocalization
            {
                public string Total => "Summe";
                public string Basket => "Warenkorb";
            }
            """, ShopLibrary);

        // Assert
        diagnostics.ShouldHaveSingleItem().GetMessage().ShouldBe("'ShopLocalizationDe.Basket' isn't a text of 'IShopLocalization', so users of 'de' won't see it. Remove it, or mark it [ExcludeFromLocalizationDiscovery] if it isn't a text.");
    }

    [Test]
    public async Task TextOnlyInSetOfContractWrittenByHand_ShouldAdviseToAddItToTheContract()
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public interface IShopTexts : ILocalizationSet
            {
                string Cart { get; }
            }

            public class ShopTextsEn : IShopTexts
            {
                public string Cart => "Cart";
            }

            public class ShopTextsNl : IShopTexts
            {
                public string Cart => "Winkelwagen";
                public string Basket => "Mandje";
            }
            """);

        // Assert
        diagnostics.ShouldHaveSingleItem().GetMessage().ShouldBe("'ShopTextsNl.Basket' isn't a text of 'IShopTexts', so users of 'nl' won't see it. Add 'Basket' to 'IShopTexts', or mark it [ExcludeFromLocalizationDiscovery] if it isn't a text.");
    }

    [Test]
    [Arguments("""public string Basket => "Basket";""", """public string Basket => "Mandje";""")] // A text of the contract
    [Arguments("", """[ExcludeFromLocalizationDiscovery] public string Helper => "";""")]
    [Arguments("", """internal string Basket => "Mandje";""")]
    [Arguments("", """private string Basket => "Mandje";""")]
    [Arguments("", """public static string Basket => "Mandje";""")]
    [Arguments("", """public int Count => 2;""")]
    [Arguments("", """public string this[int index] => "";""")]
    [Arguments("", """public string Basket() => "Mandje";""")] // No text that takes values, as it takes none
    [Arguments("", """public string Format<T>(T value) => "";""")] // Can't be a text (LOC400)
    [Arguments("", """public string Greeting(string name, int visits) => "";""")] // Another method of a text's name
    [Arguments("", """public string LocalizationLanguageCode => "nl";""")]
    [Arguments("""public string Format<T>(T value) => "";""", "")] // In the default-language set: LOC400
    [Arguments("""public string GetTextByKey => "Key";""", """public string GetTextByKey => "Sleutel";""")] // LOC300, still in the contract
    public async Task MemberThatIsATextOfTheContractOrNoText_ShouldReportNothing(string english, string dutch)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(Sets(english, dutch));

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("""public string Shipped => "Shipped";""", "")]
    [Arguments("""public string Shipped(string carrier) => $"Shipped by {carrier}";""", "")] // LOC400: a member's text takes no values
    [Arguments("""public string Shipped => "Shipped"; [ExcludeFromLocalizationDiscovery] public string Helper => "";""", "")]
    [Arguments("""public string Shipped => "Shipped"; public string GetText => "Text";""", """public string GetText => "Tekst";""")] // LOC300
    public async Task SetOfEnumWithTextsOfItsMembersOrNoText_ShouldReportNothing(string english, string dutch)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(EnumSets(english, dutch));

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("""public string Cart => "Cart";""", """public new string Cart => "Basket";""")] // LOC302
    [Arguments("""public string Cart => "Cart"; [ExcludeFromLocalizationDiscovery] public string Helper => "";""", """public new string Helper => "x";""")]
    [Arguments("""public string Cart => "Cart"; [ExcludeFromLocalizationDiscovery] public virtual string Helper => "";""", """public override string Helper => "x";""")]
    public async Task BritishMemberOfANameItInherits_ShouldReportNothing(string english, string british)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(RegionalSets(english, british));

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task PropertyNamedLikeTextThatTakesValues_ShouldReportNothing()
    {
        // Arrange & Act: the Dutch text is still a property, which LOC403 reports.
        var diagnostics = await AnalyzeAsync("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Greeting(string name) => $"Hello, {name}!";
            }

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Greeting => "Hallo {0}!";
            }
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task SetWithoutContract_ShouldReportNothing()
    {
        // Arrange & Act: LOC204 tells the set to list its contract.
        var diagnostics = await AnalyzeAsync("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Cart => "Cart";
            }

            public class ShopLocalizationNl : ILocalizationSet
            {
                public string Cart => "Winkelwagen";
                public string Basket => "Mandje";
            }
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }
}
