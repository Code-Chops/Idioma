using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

public sealed partial class LocalizationSetsPartTests
{
    [Test]
    public void SetsDerivedFromAbstractBaseOfGeneratedContract_ShouldCountForTheirLanguage()
    {
        // Arrange & Act: only the abstract base names the contract generated here. The Dutch set used to be missed,
        // so the build asked for the class it already had (LOC201).
        var result = Run($$"""
            {{Header}}

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            public abstract class ShopLocalizationBase : ILocalizationSet, IShopLocalization
            {
                public abstract string Checkout { get; }
            }

            public sealed class ShopLocalizationNl : ShopLocalizationBase
            {
                public override string Checkout => "Afrekenen";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
        ContractText(result).ShouldContain("<see cref=\"ShopLocalizationNl.Checkout\">nl</see>");
    }

    [Test]
    public void DefaultSetListingLocalizationSetThroughAbstractBase_ShouldGiveTheContract()
    {
        // Arrange: only the abstract base lists ILocalizationSet and the contract. The English set used to give no
        // contract, without a word, so the build showed only CS0246 for IShopLocalization.
        const string source = $$"""
            {{Header}}

            public abstract class ShopBase : ILocalizationSet, IShopLocalization
            {
                public abstract string Checkout { get; }
            }

            public sealed class ShopLocalizationEn : ShopBase
            {
                public override string Checkout => "Checkout";
            }

            public sealed class ShopLocalizationNl : ShopBase
            {
                public override string Checkout => "Afrekenen";
            }

            public static class Texts
            {
                public static string Checkout => ShopLocalization.Checkout;
            }
            """;

        // Act
        var result = Run(source);
        var errors = GeneratorTestHelper.CompileWithGenerators(source);

        // Assert: the contract comes from the texts the English set declares, and the Dutch set is a set of it.
        DiagnosticIds(result).ShouldBeEmpty();
        var contract = ContractText(result);
        contract.ShouldContain("public interface IShopLocalization");
        contract.ShouldContain("string Checkout { get; }");
        contract.ShouldContain("<see cref=\"ShopLocalizationEn.Checkout\">en</see>: Checkout");
        contract.ShouldContain("<see cref=\"ShopLocalizationNl.Checkout\">nl</see>: Afrekenen");
        errors.ShouldBeEmpty();
    }

    [Test]
    public void DefaultSetListingLocalizationSetThroughAbstractBase_ShouldReportTextItOnlyInheritsAsLOC303()
    {
        // Arrange & Act: the contract is written from the texts the default-language set declares itself, as for a set
        // that lists ILocalizationSet itself.
        var result = Run($$"""
            {{Header}}

            public abstract class ShopBase : ILocalizationSet, IShopLocalization
            {
                public string Brand => "Idioma";
                public abstract string Checkout { get; }
            }

            public sealed class ShopLocalizationEn : ShopBase
            {
                public override string Checkout => "Checkout";
            }

            public sealed class ShopLocalizationNl : ShopBase
            {
                public override string Checkout => "Afrekenen";
            }
            """);

        // Assert
        var diagnostic = SingleDiagnostic(result, "LOC303");
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).ShouldBe("Brand");
        ContractText(result).ShouldNotContain("string Brand");
    }

    [Test]
    public void SetsListingLocalizationSetThroughAbstractBase_ShouldBeHeldToTheLanguages()
    {
        // Arrange & Act: German has no set, and the base, which is no set itself, isn't taken for one.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]

            namespace TestApp;

            public abstract class ShopBase : ILocalizationSet, IShopLocalization
            {
                public abstract string Checkout { get; }
            }

            public sealed class ShopLocalizationEn : ShopBase
            {
                public override string Checkout => "Checkout";
            }

            public sealed class ShopLocalizationNl : ShopBase
            {
                public override string Checkout => "Afrekenen";
            }
            """);

        // Assert
        SingleDiagnostic(result, "LOC201").GetMessage().ShouldStartWith("'ShopLocalization' has no set for 'de'");
    }

    [Test]
    public void SetListingLocalizationSetThroughAbstractBaseWithoutLanguages_ShouldReportLOC100()
    {
        // Arrange & Act: without the attribute no contract is written, and the set says why.
        var result = Run("""
            using CodeChops.Idioma;

            namespace TestApp;

            public abstract class ShopBase : ILocalizationSet, IShopLocalization
            {
                public abstract string Checkout { get; }
            }

            public sealed class ShopLocalizationEn : ShopBase
            {
                public override string Checkout => "Checkout";
            }
            """);

        // Assert
        SingleDiagnostic(result, "LOC100").GetMessage().ShouldContain("'ShopLocalizationEn'");
    }

    [Test]
    public void SetListingLocalizationSetThroughAbstractBaseOfConcreteSet_ShouldStartNoContract()
    {
        // Arrange & Act: the abstract class rewords the English set, so what it lists starts no second contract.
        var result = Run($$"""
            {{Header}}

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }

            public abstract class PlainWording : ShopLocalizationEn, ILocalizationSet
            {
                public override string Checkout => "Pay";
            }

            public sealed class ShopLocalizationPlainEn : PlainWording;
            """);

        // Assert: one contract, that of the English set.
        DiagnosticIds(result).ShouldBeEmpty();
        ContractText(result).ShouldContain("public interface IShopLocalization");
    }

    [Test]
    public void SetDerivedFromOtherLanguageOfGeneratedContract_ShouldReportLOC203Only()
    {
        // Arrange & Act: the Dutch set implements the contract generated here, so the German set only reaches it through
        // its base; deriving still leaves its texts in Dutch.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Afrekenen";
            }

            public sealed class ShopLocalizationDe : ShopLocalizationNl
            {
            }
            """);

        // Assert
        SingleDiagnostic(result, LocalizationSetsPart.DerivedFromOtherLanguageDiagnosticId);
    }

    [Test]
    public void SetDerivedFromOtherLanguageWithoutDeclaredLanguages_ShouldReportLOC203()
    {
        // Arrange & Act: an app rewording a library needs no languages of its own, and this set's name makes it Dutch.
        // Without a word, it would register under nl, and every text it doesn't override would stay English there.
        var result = Run("""
            namespace TestApp;

            public sealed class MyGridTextsNl : GridLibrary.GridLocalizationEn
            {
                public override string First => "Begin";
            }
            """, GridLibrary);

        // Assert
        var diagnostic = SingleDiagnostic(result, LocalizationSetsPart.DerivedFromOtherLanguageDiagnosticId);
        diagnostic.GetMessage().ShouldContain("'MyGridTextsNl' is a set for 'nl' but derives from 'GridLocalizationEn'");
        diagnostic.GetMessage().ShouldContain("To reword 'GridLocalizationEn', give 'MyGridTextsNl' the language");
    }

    [Test]
    public void SetListingILocalizationSetAgainWithoutDeclaredLanguages_ShouldReportNothing()
    {
        // Arrange & Act: listing ILocalizationSet again is how a set deriving from another implements LocalizationLanguageCode, so
        // it does not start a contract that would need languages (LOC100).
        var result = Run("""
            using CodeChops.Idioma;

            namespace TestApp;

            public sealed class MyGridTexts : GridLibrary.GridLocalizationEn, ILocalizationSet
            {
                public string LocalizationLanguageCode => "en";
                public override string First => "Begin";
            }
            """, GridLibrary);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void SetWithLanguageCodeImplementingNothing_ShouldReportNoLanguageErrorOfItsOwn()
    {
        // Arrange & Act: the property implements nothing (LOC206, reported by the registration), and the language checks
        // go by what it says rather than adding LOC202 for the language its name gives (sh).
        var result = Run($$"""
            {{Header}}

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            {{DutchSet}}

            public sealed class ShopLocalizationBritish : ShopLocalizationEn
            {
                public string LocalizationLanguageCode => "en-GB";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void SetDerivedFromOtherLanguage_ShouldReportLOC203Only()
    {
        // Arrange & Act: compiles, with every text it does not override left in English.
        var result = Run($$"""
            {{Header}}

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
                public virtual string Total => "Total";
            }

            public sealed class ShopLocalizationNl : ShopLocalizationEn
            {
                public override string Checkout => "Afrekenen";
            }
            """);

        // Assert: this already says what to do about Dutch, so no LOC201 on top.
        var diagnostic = SingleDiagnostic(result, LocalizationSetsPart.DerivedFromOtherLanguageDiagnosticId);
        diagnostic.Id.ShouldBe("LOC203");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("'ShopLocalizationNl' is a set for 'nl' but derives from 'ShopLocalizationEn'");
        diagnostic.GetMessage().ShouldContain("stays in 'en'");
    }

    [Test]
    public void SetDerivedFromOtherLanguageOfLibrary_ShouldReportLOC203()
    {
        // Arrange & Act
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]

            namespace TestApp;

            public sealed class GridLocalizationDe : GridLibrary.GridLocalizationEn;
            """, GridLibrary);

        // Assert
        SingleDiagnostic(result, LocalizationSetsPart.DerivedFromOtherLanguageDiagnosticId);
    }

    [Test]
    public void SetRewordingItsOwnLanguage_ShouldBeAllowed()
    {
        // Arrange & Act
        var result = Run($$"""
            {{Header}}

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }

            public sealed class MyShopLocalizationEn : ShopLocalizationEn
            {
                public override string Checkout => "Pay";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }
}
