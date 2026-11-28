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
    public void SetWhoseNameGivesNoLanguage_ShouldNotHideMissingLanguages()
    {
        // Arrange & Act: a Dutch set renamed so that its name gives no language. It serves none (LOC207), and isn't a
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
