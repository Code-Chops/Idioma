using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;

namespace CodeChops.Idioma.Tests;

public sealed partial class LocalizationSetsPartTests
{
    [Test]
    public void SetsWhoseNamesGiveNoLanguage_ShouldBeToldBothNames()
    {
        // Arrange & Act: neither name gives a language, so the build can't tell which one is English.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public sealed class ShopLocalizationEnglish : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            public sealed class ShopLocalizationDutch : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """);

        // Assert: the Dutch set isn't only told to end in En.
        var dutch = result.Results.ShouldHaveSingleItem().Diagnostics.Single(diagnostic => diagnostic.Id is "LOC200" && diagnostic.GetMessage().StartsWith("'ShopLocalizationDutch'", StringComparison.Ordinal));
        dutch.GetMessage().ShouldEndWith("If it's the set of another language, end its name in that language instead, as in ShopLocalizationNl.");
    }

    [Test]
    [Arguments("private static readonly string Code = \"nl\"; public string LocalizationLanguageCode => Code;")]
    [Arguments("public string LocalizationLanguageCode => System.Globalization.CultureInfo.GetCultureInfo(\"nl\").Name;")]
    public void SetOfGeneratedContractWithUnreadableLanguage_ShouldNotBeReportedAsMissing(string languageCode)
    {
        // Arrange & Act: the build can't tell which language the set serves, so it may be the Dutch one, and asking
        // for a Dutch set would ask for the set that exists.
        var result = Run($$"""
            {{Header}}

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            public class ShopWording : ILocalizationSet, IShopLocalization
            {
                {{languageCode}}
                public virtual string Checkout => "Afrekenen";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void ExcludedSetOfGeneratedContractWithUnreadableLanguage_ShouldNotBeReportedAsMissing()
    {
        // Arrange & Act: one class for every other language, created with its language and registered by hand, which
        // is what LOC208 advises for a set that takes constructor arguments.
        var result = Run($$"""
            {{Header}}

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            [ExcludeFromLocalizationDiscovery]
            public class ShopWording(string language) : ILocalizationSet, IShopLocalization
            {
                public string LocalizationLanguageCode => language;
                public virtual string Checkout => "Afrekenen";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void LibrarySetWithUnreadableLanguage_ShouldNotBeReportedAsMissingInApp()
    {
        // Arrange: the library's build could not read the language of its second set, and lists the set as one of a
        // language unknown, so that the app's build does not ask for a set the library has.
        var library = GeneratorTestHelper.CompileLibraryWithGenerators("WordingLibrary", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace WordingLibrary;

            public class GridLocalizationEn : ILocalizationSet, IGridLocalization
            {
                public virtual string First => "First";
            }

            public class GridWording : ILocalizationSet, IGridLocalization
            {
                private static readonly string Code = "nl";

                public string LocalizationLanguageCode => Code;
                public virtual string First => "Eerste";
            }
            """);

        // Act
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]
            """, library);

        // Assert: neither Dutch nor German, as the set may be of either.
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void SetWithUnreadableLanguage_ShouldBeListedForOtherAssembliesAsOfLanguageUnknown()
    {
        // Arrange & Act
        var provided = ProvidedSets(Run($$"""
            {{Header}}

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            public class ShopWording : ILocalizationSet, IShopLocalization
            {
                private static readonly string Code = "nl";

                public string LocalizationLanguageCode => Code;
                public virtual string Checkout => "Afrekenen";
            }
            """));

        // Assert: under what is no culture name, so that a build that does not know it takes it for no language.
        provided.ShouldContain("""LocalizationSetAttribute("TestApp.IShopLocalization", "en")""");
        provided.ShouldContain("""LocalizationSetAttribute("TestApp.IShopLocalization", "*")""");
    }

    [Test]
    public void SetWhoseNameGivesNoLanguage_ShouldNotHideMissingLanguages()
    {
        // Arrange & Act: a Dutch set renamed so that its name gives no language. It serves none (LOC208), and isn't a
        // set of a language the build can't read, which would leave Dutch and German unreported.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            public class ShopLocalizationDutch : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """);

        // Assert: Dutch and German, each named.
        var messages = result.Results.ShouldHaveSingleItem().Diagnostics
            .Select(static diagnostic => $"{diagnostic.Id}: {diagnostic.GetMessage()}")
            .ToArray();

        messages.Length.ShouldBe(2);
        messages.ShouldContain(message => message.StartsWith("LOC201: 'ShopLocalization' has no set for 'nl'", StringComparison.Ordinal));
        messages.ShouldContain(message => message.StartsWith("LOC201: 'ShopLocalization' has no set for 'de'", StringComparison.Ordinal));
    }

    [Test]
    public void SetWhoseNameGivesNoLanguage_ShouldNotBeListedForOtherAssemblies()
    {
        // Arrange & Act
        var provided = ProvidedSets(Run($$"""
            {{Header}}

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Afrekenen";
            }

            public class ShopWording : ShopLocalizationEn
            {
                public override string Checkout => "Pay";
            }
            """));

        // Assert: it serves no language, so an assembly referencing this one doesn't take it for a set of any.
        provided.ShouldNotContain("\"*\"");
    }

    [Test]
    public void LibrarySetWhoseNameGivesNoLanguage_ShouldNotHideLanguageMissingInApp()
    {
        // Arrange: a library that rewords the grid under a name without a language. That set serves no language, so the
        // library doesn't list it as one of a language unknown.
        var rewording = GeneratorTestHelper.CompileLibraryWithGenerators("RewordingLibrary", """
            namespace RewordingLibrary;

            public class MyGridTexts : GridLibrary.GridLocalizationEn
            {
                public override string First => "Mine";
            }
            """, GridLibrary);

        // Act
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]
            """, GridLibrary, rewording);

        // Assert: German is still missing, as it is without the rewording library.
        SingleDiagnostic(result, LocalizationSetsPart.LibraryMissingLanguageDiagnosticId).GetMessage().ShouldContain("No set serves 'GridLibrary.IGridLocalization' in 'de'");
    }
}
