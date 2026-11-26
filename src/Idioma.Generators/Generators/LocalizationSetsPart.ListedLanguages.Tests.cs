using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

public sealed partial class LocalizationSetsPartTests
{
    [Test]
    public void SetWithoutDeclaredLanguages_ShouldReportLOC100AndEmitNoContract()
    {
        // Arrange & Act
        var result = Run("""
            using CodeChops.Idioma;

            namespace TestApp;

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }
            """);

        // Assert
        var diagnostic = SingleDiagnostic(result, LocalizationSetsPart.NoLanguagesDeclaredDiagnosticId);
        diagnostic.Id.ShouldBe("LOC100");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("'ShopLocalizationEn'");
        diagnostic.GetMessage().ShouldContain("[assembly: SupportedLanguages(\"en\", \"nl\")]");
        diagnostic.Location.GetLineSpan().StartLinePosition.Line.ShouldBe(4);
        result.Results.ShouldHaveSingleItem().GeneratedSources.ShouldBeEmpty();
    }

    [Test]
    public void AppWithoutDeclaredLanguages_ShouldBeFreeToAddToLibrary()
    {
        // Arrange & Act: rewording a text, and adding a language, in an app that declares nothing.
        var result = Run("""
            namespace TestApp;

            public sealed class MyGridLocalizationEn : GridLibrary.GridLocalizationEn
            {
                public override string First => "Start";
            }

            public sealed class GridLocalizationDe : CodeChops.Idioma.ILocalizationSet, GridLibrary.IGridLocalization
            {
                public string First => "Erste";
            }
            """, GridLibrary);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    [Arguments("")]
    [Arguments("[ExcludeFromLocalizationDiscovery]")]
    public void SetForUnlistedLanguage_ShouldReportLOC202(string attribute)
    {
        // Arrange & Act: excluded or not, as an application shows a culture outside its list in its default language,
        // so registering the set by hand would not make it show.
        var result = Run($$"""
            {{ShopSets}}

            {{attribute}}
            public sealed class ShopLocalizationDe : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Zur Kasse";
                public string Total => "Summe";
            }
            """);

        // Assert
        var diagnostic = SingleDiagnostic(result, LocalizationSetsPart.UnlistedLanguageDiagnosticId);
        diagnostic.Id.ShouldBe("LOC202");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("'ShopLocalizationDe' is a set for 'de'");
        diagnostic.GetMessage().ShouldContain("Add the language to the attribute, or remove the set.");
    }

    [Test]
    public void ClassDerivingFromTypeOfOtherGenerator_ShouldReportNothing()
    {
        // Arrange & Act: OrderConfigurationBase stands for a type another generator writes, which binds to nothing in
        // the compilation the generators see. The class is no set, so its name ending in 'on' is no language.
        var result = Run($$"""
            {{ShopSets}}

            public sealed class OrderConfiguration : OrderConfigurationBase;
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void SetNamedAfterListedLongCode_ShouldServeIt()
    {
        // Arrange & Act: the name gives nl-BE, as the assembly declares it, and zh-Hans in any letter case.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl-BE", "zh-Hans")]

            namespace TestApp;

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            public sealed class ShopLocalizationNlBe : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }

            public sealed class ShopLocalizationZHHANS : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Jiezhang";
            }
            """);

        // Assert: no LOC201 or LOC202, each code listed for the assemblies referencing this one, and linked.
        DiagnosticIds(result).ShouldBeEmpty();
        ProvidedSets(result).ShouldContain("LocalizationSetAttribute(\"TestApp.IShopLocalization\", \"nl-BE\")");
        ProvidedSets(result).ShouldContain("LocalizationSetAttribute(\"TestApp.IShopLocalization\", \"zh-Hans\")");
        ContractText(result).ShouldContain("ShopLocalizationNlBe.Checkout\">nl-BE</see>");
    }

    [Test]
    public void SetNamedAfterTwoLetterCode_ShouldNotBeTakenForLongerCodeItsNameHappensToEndIn()
    {
        // Arrange & Act: "MessagesAr" ends in the letters of EsAr, the suffix of es-AR, but not at a capital: its "es"
        // belongs to "Messages", and the set is Arabic. The Argentinian set is the one named MessagesEsAr.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "ar", "es", "es-AR")]

            namespace TestApp;

            public sealed class MessagesEn : ILocalizationSet, IMessages
            {
                public string Save => "Save";
            }

            public sealed class MessagesAr : ILocalizationSet, IMessages
            {
                public string Save => "Hifz";
            }

            public class MessagesEs : ILocalizationSet, IMessages
            {
                public virtual string Save => "Guardar";
            }

            public sealed class MessagesEsAr : MessagesEs;
            """);

        // Assert: no LOC201 asking for the Arabic set that is there, and each set listed and linked under its own
        // language.
        DiagnosticIds(result).ShouldBeEmpty();
        var provided = ProvidedSets(result);
        provided.ShouldContain("LocalizationSetAttribute(\"TestApp.IMessages\", \"ar\")");
        provided.ShouldContain("LocalizationSetAttribute(\"TestApp.IMessages\", \"es-AR\")");
        var contract = ContractText(result);
        contract.ShouldContain("MessagesAr.Save\">ar</see>");
        contract.ShouldContain("MessagesEs.Save\">es-AR</see>");
    }

    [Test]
    public void SetNamedAfterUnlistedLongCode_ShouldReportLOC202NamingBothWays()
    {
        // Arrange & Act: nl-BE is not declared, so the last two letters count, and the set serves 'be'.
        var result = Run($$"""
            {{Header}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            {{DutchSet}}

            public sealed class ShopLocalizationNlBe : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """);

        // Assert: list the code, or implement it.
        var diagnostic = SingleDiagnostic(result, LocalizationSetsPart.UnlistedLanguageDiagnosticId);
        diagnostic.GetMessage().ShouldContain("'ShopLocalizationNlBe' is a set for 'be'");
        diagnostic.GetMessage().ShouldContain("If it's meant for 'nl-BE', list 'nl-BE' in the attribute");
        diagnostic.GetMessage().ShouldContain("implement 'string ILocalizationSet.LocalizationLanguageCode => \"nl-BE\";'");
    }

    [Test]
    [Arguments("ShopLocalizationEnGb", "If it's meant for 'en-GB'", "Otherwise remove the set, because 'gb' is no language.")]
    [Arguments("ShopLocalizationQq", "'qq' is no language", "so name the set after one that the attribute lists, or remove the set.")]
    public void SetNamedAfterNoLanguage_ShouldReportLOC202WithoutAdvisingToListIt(string name, string advice, string otherwise)
    {
        // Arrange & Act: en-GB is not declared, so the last two letters count, and 'gb' is no language. Listed, it would
        // only give LOC104.
        var result = Run($$"""
            {{Header}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            {{DutchSet}}

            public sealed class {{name}} : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }
            """);

        // Assert: and the fix lists nothing but a code the name was meant for.
        var diagnostic = SingleDiagnostic(result, LocalizationSetsPart.UnlistedLanguageDiagnosticId);
        diagnostic.GetMessage().ShouldContain(advice);
        diagnostic.GetMessage().ShouldEndWith(otherwise);
        diagnostic.GetMessage().ShouldNotContain("Otherwise add");
        diagnostic.Properties.TryGetValue(LocalizationSetsPart.SuggestedNameProperty, out var suggested);
        suggested.ShouldBe(name.EndsWith("EnGb", StringComparison.Ordinal) ? "en-GB" : null);
    }

    [Test]
    [Arguments("AdminUiNl")]
    [Arguments("MyAppNl")]
    public void SetWhoseNameEndsInWordAndUnlistedLanguage_ShouldReportLOC202WithoutGuessingLongerCode(string name)
    {
        // Arrange & Act: Ui and App aren't languages the attribute lists, so the name ends in nl alone.
        var result = Run($$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "de")]

            namespace TestApp;

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            public sealed class ShopLocalizationDe : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Zur Kasse";
            }

            public sealed class {{name}} : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """);

        // Assert
        var diagnostic = SingleDiagnostic(result, LocalizationSetsPart.UnlistedLanguageDiagnosticId);
        diagnostic.GetMessage().ShouldBe($"'{name}' is a set for 'nl', which [assembly: SupportedLanguages] doesn't list. Add the language to the attribute, or remove the set.");
    }

    [Test]
    public void SetDerivedFromLibrarySetNamedAfterLongCode_ShouldServeThatCode()
    {
        // Arrange: the library declares nl-BE, so its registrar describes its set with nl-BE; the app declares only nl.
        var library = GeneratorTestHelper.CompileLibraryWithGenerators("RegionalLibrary", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl-BE")]

            namespace RegionalLibrary;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            public class ShopLocalizationNlBe : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Afrekenen";
            }
            """);

        // Act: a rewording whose own name the app's languages give nothing longer than 'be'.
        var result = Run($$"""
            {{Header}}

            public sealed class MyShopLocalizationNlBe : RegionalLibrary.ShopLocalizationNlBe
            {
                public override string Checkout => "Betalen";
            }
            """, library);

        // Assert: it inherits nl-BE, which nl serves, as at run time; no LOC202 for 'be'.
        DiagnosticIds(result).ShouldNotContain("LOC202", StringComparer.Ordinal);
        ProvidedSets(result).ShouldContain("LocalizationSetAttribute(\"RegionalLibrary.IShopLocalization\", \"nl-BE\")");
    }

    [Test]
    public void DefaultSetNamedAfterLongCode_ShouldGenerateContractThatCompiles()
    {
        // Arrange & Act: a regional default language, with no LocalizationLanguageCode written by hand.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("nl-BE", "en")]

            namespace TestApp;

            public class ShopLocalizationNlBe : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }
            """;

        // Assert
        DiagnosticIds(Run(source)).ShouldBeEmpty();
        GeneratorTestHelper.CompileWithGenerators(source).ShouldBeEmpty();
    }

    [Test]
    public void SetOfLongCodeImplementingLanguageCode_ShouldReportNothing()
    {
        // Arrange & Act
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl-BE")]

            namespace TestApp;

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            public sealed class ShopLocalizationNlBe : ILocalizationSet, IShopLocalization
            {
                string ILocalizationSet.LocalizationLanguageCode => "nl-BE";
                public string Checkout => "Afrekenen";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void RegionalSetOfListedLanguage_ShouldBeAllowed()
    {
        // Arrange & Act: a regional set on top of its language keeps every other text in that language.
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

            public sealed class ShopLocalizationGb : ShopLocalizationEn, ILocalizationSet
            {
                string ILocalizationSet.LocalizationLanguageCode => "en-GB";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void RegionalSetOfNonDefaultLanguage_ShouldBeAllowed()
    {
        // Arrange & Act: the Dutch set implements the contract generated here, so the Belgian set only reaches it through
        // its base. It used to be reported as a set implementing no contract (LOC204).
        var result = Run($$"""
            {{Header}}

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Afrekenen";
            }

            public sealed class ShopLocalizationNlBe : ShopLocalizationNl, ILocalizationSet
            {
                string ILocalizationSet.LocalizationLanguageCode => "nl-BE";
                public override string Checkout => "Betalen";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void ListedRegionalLanguageServedBySetDerivedFromDefaultSet_ShouldReportNothing()
    {
        // Arrange & Act: deriving from the default set implements its contract. It used to count as no set of en-GB
        // (LOC201).
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "en-GB")]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Colour => "Color";
            }

            public sealed class ShopLocalizationGb : ShopLocalizationEn, ILocalizationSet
            {
                string ILocalizationSet.LocalizationLanguageCode => "en-GB";
                public override string Colour => "Colour";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
        ContractText(result).ShouldContain("<see cref=\"ShopLocalizationGb.Colour\">en-GB</see>");
    }

    [Test]
    public void ListedRegionOfDefaultLanguage_ShouldNeedSetInEveryContract()
    {
        // Arrange & Act: en-GB is listed, so every contract has a British set, also the one with nothing British about
        // it. The set of the default language does not stand in for it.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "en-GB")]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Cart => "Cart";
            }

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Cart => "Winkelwagen";
            }

            public sealed class ShopLocalizationEnGb : ShopLocalizationEn
            {
                public override string Cart => "Basket";
            }

            public class GridLocalizationEn : ILocalizationSet, IGridLocalization
            {
                public string Rows => "Rows";
            }

            public class GridLocalizationNl : ILocalizationSet, IGridLocalization
            {
                public string Rows => "Rijen";
            }
            """);

        // Assert: only the contract without a British set is reported, with the set that derives from its English one.
        var message = SingleDiagnostic(result, LocalizationSetsPart.MissingLanguageDiagnosticId).GetMessage();
        message.ShouldContain("'GridLocalization' has no set for 'en-GB'");
        message.ShouldEndWith("Add 'class GridLocalizationEnGb : GridLocalizationEn', which keeps the wording of 'en' for every text it doesn't override. A class without ILocalizationSet doesn't count as a set, and neither does a struct, or a nested, generic, or file-local class.");
    }
}
