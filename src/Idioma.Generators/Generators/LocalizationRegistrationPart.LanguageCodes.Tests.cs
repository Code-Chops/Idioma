using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

public sealed partial class LocalizationRegistrationPartTests
{
    [Test]
    public void LanguageCodeOfDerivedSetNotListingInterface_ReportsLOC206()
    {
        // Arrange & Act: the set inherits the mapping of ILocalizationSet from its base, so its own LocalizationLanguageCode is
        // never read, and it registers under the last two letters of its name: uk, which is Ukrainian.
        var result = Run($$"""
            {{ShopContract}}

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            public sealed class ShopLocalizationUk : ShopLocalizationEn
            {
                public string LocalizationLanguageCode => "en-GB";
            }
            """);

        // Assert
        var diagnostic = result.Results.ShouldHaveSingleItem().Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(LocalizationRegistrationPart.UnmappedLanguageCodeDiagnosticId);
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("'ShopLocalizationUk' inherits ILocalizationSet without listing it, so its LocalizationLanguageCode implements nothing");
        diagnostic.GetMessage().ShouldContain("'class ShopLocalizationUk : ShopLocalizationEn, ILocalizationSet'");
    }

    [Test]
    public void LanguageCodeOfSetDerivedFromSetOfGeneratedContract_ReportsLOC206()
    {
        // Arrange & Act: the base implements the contract generated here, which binds to nothing yet. The set registers
        // under be, which is Belarusian.
        var result = Run($$"""
            {{GeneratedShopContract}}

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Afrekenen";
            }

            public sealed class ShopLocalizationBe : ShopLocalizationNl
            {
                public string LocalizationLanguageCode => "nl-BE";
            }
            """);

        // Assert
        var diagnostic = result.Results.ShouldHaveSingleItem().Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(LocalizationRegistrationPart.UnmappedLanguageCodeDiagnosticId);
    }

    [Test]
    [Arguments("ShopLocalizationEn, ILocalizationSet", "public string LocalizationLanguageCode => \"en-GB\";")]
    [Arguments("ShopLocalizationEn, ILocalizationSet", "string ILocalizationSet.LocalizationLanguageCode => \"en-GB\";")]
    [Arguments("ShopLocalizationEn, IShopLocalization", "public string LocalizationLanguageCode => \"en-GB\";")]
    public void LanguageCodeOfDerivedSetListingInterface_ReportsNothing(string baseList, string languageCode)
    {
        // Arrange & Act
        var result = Run($$"""
            {{ShopContract}}

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            public sealed class ShopLocalizationBritish : {{baseList}}
            {
                {{languageCode}}
            }
            """);

        // Assert
        var run = result.Results.ShouldHaveSingleItem();
        run.Diagnostics.ShouldBeEmpty();
        run.GeneratedSources.ShouldHaveSingleItem().SourceText.ToString().ShouldContain("new TestApp.ShopLocalizationBritish()");
    }

    [Test]
    public void LanguageCodeOverridingImplementation_ReportsNothing()
    {
        // Arrange & Act: the override is what the base's implementation runs.
        var result = Run($$"""
            {{ShopContract}}

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string LocalizationLanguageCode => "en";
                public virtual string Checkout => "Checkout";
            }

            public sealed class ShopLocalizationBritish : ShopLocalizationEn
            {
                public override string LocalizationLanguageCode => "en-GB";
            }
            """);

        // Assert
        result.Results.ShouldHaveSingleItem().Diagnostics.ShouldBeEmpty();
    }

    [Test]
    public void LanguageCodeImplementingNothingOnSetWithoutLanguageInName_ReportsLOC206AloneAndLeavesItUnregistered()
    {
        // Arrange & Act: the name gives no language either, so registering it would throw while the assembly loads. It
        // used to be registered, as its property seemed to implement LocalizationLanguageCode. LOC206 says why, and its
        // fix gives the set its code, so no LOC208 says the same again.
        var result = Run($$"""
            {{ShopContract}}

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            public sealed class ShopLocalizationBritish2 : ShopLocalizationEn
            {
                public string LocalizationLanguageCode => "en-GB";
            }
            """);

        // Assert
        var run = result.Results.ShouldHaveSingleItem();
        run.Diagnostics.Select(diagnostic => diagnostic.Id).ShouldBe([LocalizationRegistrationPart.UnmappedLanguageCodeDiagnosticId]);
        run.GeneratedSources.ShouldHaveSingleItem().SourceText.ToString().ShouldNotContain("new TestApp.ShopLocalizationBritish2(");
    }

    [Test]
    [Arguments("public string LocalizationLanguageCode => \"en_GB\";", "en_GB")]
    [Arguments("string ILocalizationSet.LocalizationLanguageCode => \"\";", "")]
    [Arguments("public string LocalizationLanguageCode => \"english\" + \" \";", "english ")]
    public void SetWithLanguageCodeThatIsNoCultureName_ReportsLOC207(string languageCode, string expectedCode)
    {
        // Arrange & Act: in an assembly without [assembly: SupportedLanguages], where no language check looks at the
        // set. Registering it throws when the assembly loads, far from the cause.
        var result = Run($$"""
            {{ShopContract}}

            public sealed class ShopLocalizationBritish : ILocalizationSet, IShopLocalization
            {
                {{languageCode}}
                public string Checkout => "Checkout";
            }
            """);

        // Assert: at the property that holds the code.
        var diagnostic = result.Results.ShouldHaveSingleItem().Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(LocalizationRegistrationPart.InvalidLanguageCodeDiagnosticId);
        diagnostic.Id.ShouldBe("LOC207");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldStartWith($"'ShopLocalizationBritish' gives \"{expectedCode}\" as its LocalizationLanguageCode, which isn't a culture name");
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).ShouldBe("LocalizationLanguageCode");
    }

    [Test]
    [Arguments("public string LocalizationLanguageCode => \"en-GB\";")]
    [Arguments("public string LocalizationLanguageCode => \"EN\";")]
    [Arguments("public string LocalizationLanguageCode => \"zh-Hans-CN\";")]
    [Arguments("public string LocalizationLanguageCode => System.Globalization.CultureInfo.CurrentUICulture.Name;")]
    public void SetWithLanguageCodeThatIsCultureNameOrNoConstant_ReportsNothing(string languageCode)
    {
        // Arrange & Act
        var result = Run($$"""
            {{ShopContract}}

            public sealed class ShopLocalizationBritish : ILocalizationSet, IShopLocalization
            {
                {{languageCode}}
                public string Checkout => "Checkout";
            }
            """);

        // Assert
        result.Results.ShouldHaveSingleItem().Diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("public string LocalizationLanguageCode => \"dk\";", "'dk' won't match any user's culture, because 'dk' isn't an ISO 639-1 language code. Danish is 'da', so write 'da'.")]
    [Arguments("string ILocalizationSet.LocalizationLanguageCode => \"jp\";", "'jp' won't match any user's culture, because 'jp' isn't an ISO 639-1 language code. Japanese is 'ja', so write 'ja'.")]
    [Arguments("public string LocalizationLanguageCode => \"cz\";", "'cz' won't match any user's culture, because 'cz' isn't an ISO 639-1 language code. Czech is 'cs', so write 'cs'.")]
    [Arguments("public string LocalizationLanguageCode => \"gr\";", "'gr' won't match any user's culture, because 'gr' isn't an ISO 639-1 language code. Greek is 'el', so write 'el'.")]
    [Arguments("public string LocalizationLanguageCode => \"ua\";", "'ua' won't match any user's culture, because 'ua' isn't an ISO 639-1 language code. Ukrainian is 'uk', so write 'uk'.")]
    [Arguments("public string LocalizationLanguageCode => \"en-\" + \"UK\";", "'en-UK' won't match any user's culture, because 'UK' isn't an ISO 3166-1 region code. The United Kingdom is 'GB', so write 'en-GB'.")]
    public void SetWithLanguageCodeNoCultureHas_ReportsLOC104AndIsRegistered(string languageCode, string expectedMessage)
    {
        // Arrange & Act: the code has the form of a culture name, so the registry takes the set, and no user's culture
        // ever asks for it.
        var result = Run($$"""
            {{ShopContract}}

            public sealed class ShopLocalizationOther : ILocalizationSet, IShopLocalization
            {
                {{languageCode}}
                public string Checkout => "Checkout";
            }
            """);

        // Assert: a warning at the property that holds the code.
        var run = result.Results.ShouldHaveSingleItem();
        var diagnostic = run.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(LocalizableTextFactoryGenerator.UnknownLanguageCodeDiagnosticId);
        diagnostic.Id.ShouldBe("LOC104");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().ShouldBe(expectedMessage);
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).ShouldBe("LocalizationLanguageCode");
        GeneratedText(result).ShouldContain("new TestApp.ShopLocalizationOther()");
    }

    [Test]
    [Arguments("en")]
    [Arguments("nl")]
    [Arguments("zh-Hans")]
    [Arguments("zh-Hant-TW")]
    [Arguments("es-419")]
    [Arguments("sr-Latn-RS")]
    [Arguments("fil")]
    [Arguments("sq-XK")]
    [Arguments("uk")]
    public void SetWithLanguageCodeACultureHas_ReportsNoLOC104(string code)
    {
        // Arrange & Act
        var result = Run($$"""
            {{ShopContract}}

            public sealed class ShopLocalizationOther : ILocalizationSet, IShopLocalization
            {
                public string LocalizationLanguageCode => "{{code}}";
                public string Checkout => "Checkout";
            }
            """);

        // Assert
        result.Results.ShouldHaveSingleItem().Diagnostics.ShouldBeEmpty();
    }

    [Test]
    public void LanguageCodeNoCultureHas_IsReportedOnceForTheSetsInheritingItAndTurnedOffByAPragma()
    {
        // Arrange: each of the two sets carries the code of the class they derive from, and one is registered by hand,
        // as two sets of one language would be LOC209. The second class turns the warning off, as for a code the build's
        // tables don't know yet.
        const string source = $$"""
            {{ShopContract}}

            public abstract class DanishWording : ILocalizationSet, IShopLocalization
            {
                public string LocalizationLanguageCode => "dk";
                public virtual string Checkout => "Til kassen";
            }

            public sealed class ShopWording : DanishWording;

            [ExcludeFromLocalizationDiscovery]
            public sealed class GiftWording : DanishWording;

            public sealed class NewWording : ILocalizationSet, IShopLocalization
            {
            #pragma warning disable LOC104
                public string LocalizationLanguageCode => "qq";
            #pragma warning restore LOC104
                public string Checkout => "Checkout";
            }
            """;

        // Act
        var diagnostics = GeneratorTestHelper.GetGeneratorDiagnostics(source);

        // Assert
        diagnostics.Select(static diagnostic => (diagnostic.Id, diagnostic.IsSuppressed, Code: diagnostic.GetMessage().Substring(startIndex: 0, length: 4)))
            .ShouldBe([("LOC104", false, "'dk'"), ("LOC104", true, "'qq'")], ignoreOrder: true);
    }

    [Test]
    public void LanguageCodeThatIsNoCultureName_IsReportedOnceForTheSetsInheritingIt()
    {
        // Arrange & Act: each of the three sets carries the code of the class they derive from.
        var result = Run($$"""
            {{ShopContract}}

            public abstract class BritishWording : ILocalizationSet, IShopLocalization
            {
                public string LocalizationLanguageCode => "en_GB";
                public virtual string Checkout => "Checkout";
            }

            public sealed class ShopWording : BritishWording;

            public sealed class GiftWording : BritishWording;

            [ExcludeFromLocalizationDiscovery]
            public sealed class BookWording : BritishWording;
            """);

        // Assert
        var diagnostic = result.Results.ShouldHaveSingleItem().Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC207");
        diagnostic.GetMessage().ShouldStartWith("'BritishWording' gives \"en_GB\" as its LocalizationLanguageCode");
    }

    [Test]
    public void ExcludedSetWithLanguageCodeThatIsNoCultureName_ReportsLOC207()
    {
        // Arrange & Act: registered by hand, the set throws just the same, where it is registered.
        var result = Run($$"""
            {{ShopContract}}

            [ExcludeFromLocalizationDiscovery]
            public sealed class ShopWording(string checkout) : ILocalizationSet, IShopLocalization
            {
                public string LocalizationLanguageCode => "en_GB";
                public string Checkout => checkout;
            }
            """);

        // Assert
        result.Results.ShouldHaveSingleItem().Diagnostics.ShouldHaveSingleItem().Id.ShouldBe("LOC207");
    }

    [Test]
    public void SetWithLanguageCodeThatIsNoCultureNameInAssemblyDeclaringLanguages_ReportsLOC207Alone()
    {
        // Arrange & Act: with every generator at work. Taken for a language, the code would be reported as unlisted,
        // with the advice to list it, which the attribute then refuses (LOC101).
        var diagnostics = GeneratorTestHelper.GetGeneratorDiagnostics($$"""
            {{GeneratedShopContract}}

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Afrekenen";
            }

            public class ShopLocalizationBritish : ShopLocalizationEn, ILocalizationSet
            {
                string ILocalizationSet.LocalizationLanguageCode => "en_GB";
            }
            """);

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("LOC207");
    }

    [Test]
    public void LanguageCodeImplementingNothing_IsReportedOnceAtTheClassDeclaringIt()
    {
        // Arrange & Act: the two sets deriving from the class carry its LocalizationLanguageCode, and have nothing to fix.
        var result = Run($$"""
            {{ShopContract}}

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            public class ShopLocalizationUk : ShopLocalizationEn
            {
                public string LocalizationLanguageCode => "en-GB";
            }

            public class GiftLocalizationUk : ShopLocalizationUk;

            public sealed class BookLocalizationUk : GiftLocalizationUk;
            """);

        // Assert
        var diagnostic = result.Results.ShouldHaveSingleItem().Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC206");
        diagnostic.GetMessage().ShouldContain("'ShopLocalizationUk' inherits ILocalizationSet without listing it, so its LocalizationLanguageCode implements nothing");
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).ShouldBe("ShopLocalizationUk");
    }

    [Test]
    public void LanguageCodeImplementingNothingOnAbstractClass_IsReportedAtThatClass()
    {
        // Arrange & Act: the class that declares it is no set itself, and still where the interface belongs.
        var result = Run($$"""
            {{ShopContract}}

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            public abstract class BritishWording : ShopLocalizationEn
            {
                public string LocalizationLanguageCode => "en-GB";
            }

            public sealed class ShopLocalizationUk : BritishWording;
            """);

        // Assert
        var diagnostic = result.Results.ShouldHaveSingleItem().Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC206");
        diagnostic.GetMessage().ShouldContain("'class BritishWording : ShopLocalizationEn, ILocalizationSet'");
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).ShouldBe("BritishWording");
    }
}
