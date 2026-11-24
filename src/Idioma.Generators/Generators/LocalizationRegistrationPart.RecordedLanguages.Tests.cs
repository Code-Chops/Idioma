namespace CodeChops.Idioma.Tests;

public sealed partial class LocalizationRegistrationPartTests
{
    [Test]
    public void SetsNamedAfterDeclaredLongCodes_RecordTheirCodesBeforeRegistering()
    {
        // Arrange & Act
        var text = GeneratedText(Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl-BE", "zh-Hans")]

            namespace TestApp;

            public interface IShopLocalization : ILocalizationSet
            {
                string Checkout { get; }
            }

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            public sealed class ShopLocalizationNlBe : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }

            public sealed class ShopLocalizationZhHans : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Jiezhang";
            }
            """));

        // Assert: before the sets register, as the registry files each under the code it reads from it. A two-letter
        // code is recorded too, so that the run time never reads a set's name.
        text.ShouldContain("GeneratedCode.RecordSet<");
        text.ShouldContain("ShopLocalizationNlBe>(\"nl-BE\");");
        text.ShouldContain("ShopLocalizationZhHans>(\"zh-Hans\");");
        text.ShouldContain("ShopLocalizationEn>(\"en\");");
        text.LastIndexOf("RecordSet<", StringComparison.Ordinal)
            .ShouldBeLessThan(text.IndexOf("RegisterSets(asDefaults: true,", StringComparison.Ordinal));
    }

    [Test]
    public void SetDerivingFromAnother_RecordsTheTextsItsOwnClassWords()
    {
        // Arrange & Act: a regional set overriding a property and a method, an app's rewording implementing a text
        // explicitly, and a British set implementing its own LocalizationLanguageCode, which the build records no language for.
        var text = GeneratedText(Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "en-GB")]

            namespace TestApp;

            public interface IShopLocalization : ILocalizationSet
            {
                string Cart { get; }
                string Checkout { get; }
                string Greeting(string name);
            }

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Cart => "Cart";
                public virtual string Checkout => "Checkout";
                public virtual string Greeting(string name) => $"Hello, {name}!";
            }

            public sealed class ShopLocalizationEnGb : ShopLocalizationEn
            {
                public override string Cart => "Basket";
                public override string Greeting(string name) => $"Hello, {name}!";
            }

            public sealed class MyShopLocalizationEn : ShopLocalizationEn, IShopLocalization
            {
                string IShopLocalization.Checkout => "Pay";
            }

            public sealed class ShopLocalizationBritish : ShopLocalizationEn, ILocalizationSet
            {
                string ILocalizationSet.LocalizationLanguageCode => "en-GB";

                public override string Cart => "Basket";
            }
            """));

        // Assert: by name and in order, and nothing of the texts for a set deriving from none.
        text.ShouldContain("RecordSet<TestApp.ShopLocalizationEnGb>(\"en-GB\", \"Cart\", \"Greeting\");");
        text.ShouldContain("RecordSet<TestApp.MyShopLocalizationEn>(\"en\", \"Checkout\");");
        text.ShouldContain("RecordSet<TestApp.ShopLocalizationBritish>(\"en-GB\", \"Cart\");");
        text.ShouldContain("RecordSet<TestApp.ShopLocalizationEn>(\"en\");");
    }

    [Test]
    public void ExcludedSetNamedAfterDeclaredLongCode_RecordsItsCodeWithoutRegistering()
    {
        // Arrange & Act: registered by hand, it must serve the language the build counted it for.
        var text = GeneratedText(Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl-BE")]

            namespace TestApp;

            public interface IShopLocalization : ILocalizationSet
            {
                string Checkout { get; }
            }

            [ExcludeFromLocalizationDiscovery]
            public sealed class ShopLocalizationNlBe : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """));

        // Assert: a registrar with nothing to register is internal.
        text.ShouldContain("internal static class LocalizationRegistrar_TestAssembly");
        text.ShouldContain("ShopLocalizationNlBe>(\"nl-BE\");");
        text.ShouldNotContain("ShopLocalizationNlBe()");
    }

    [Test]
    [Arguments("[assembly: SupportedLanguages(\"en\", \"nl-BE\")]", "string ILocalizationSet.LocalizationLanguageCode => \"nl-BE\";", "ShopLocalizationNlBe>(\"nl-BE\");")]
    [Arguments("[assembly: SupportedLanguages(\"en\", \"nl-BE\")]", "string ILocalizationSet.LocalizationLanguageCode => System.Environment.GetEnvironmentVariable(\"SHOP_LANGUAGE\") ?? \"nl-BE\";", null)]
    [Arguments("[assembly: SupportedLanguages(\"en\", \"nl\")]", "", "ShopLocalizationNlBe>(\"be\");")]
    public void SetWhoseNameGivesNoDeclaredLongCode_RecordsWhatTheBuildReads(string attribute, string languageCode, string? recorded)
    {
        // Arrange & Act: a set implementing its code records it where it's a constant, so that the registry needn't
        // create the set to file it. One the build can't read gets no record. Without nl-BE declared, the name gives 'be'.
        var text = GeneratedText(Run($$"""
            using CodeChops.Idioma;

            {{attribute}}

            namespace TestApp;

            public interface IShopLocalization : ILocalizationSet
            {
                string Checkout { get; }
            }

            public sealed class ShopLocalizationNlBe : ILocalizationSet, IShopLocalization
            {
                {{languageCode}}
                public string Checkout => "Afrekenen";
            }
            """));

        // Assert
        if (recorded is null)
            text.ShouldNotContain("RecordSet");
        else
            text.ShouldContain(recorded);
    }

    [Test]
    public void SetNamedAfterTwoLetterCode_RecordsNoLongerCodeItsNameHappensToEndIn()
    {
        // Arrange & Act: "MessagesAr" ends in the letters of EsAr, the suffix of es-AR, but not at a capital, so it is
        // the Arabic set its last two letters say; "MessagesEsAr" is the Argentinian one.
        var text = GeneratedText(Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "ar", "es-AR")]

            namespace TestApp;

            public interface IMessages : ILocalizationSet
            {
                string Save { get; }
            }

            public sealed class MessagesEn : ILocalizationSet, IMessages
            {
                public string Save => "Save";
            }

            public sealed class MessagesAr : ILocalizationSet, IMessages
            {
                public string Save => "Hifz";
            }

            public sealed class MessagesEsAr : ILocalizationSet, IMessages
            {
                public string Save => "Guardar";
            }
            """));

        // Assert
        text.ShouldContain("MessagesEsAr>(\"es-AR\");");
        text.ShouldContain("MessagesAr>(\"ar\");");
        text.ShouldContain("new TestApp.MessagesAr()");
    }

    [Test]
    [Arguments("public string LocalizationLanguageCode => \"de\";")]
    [Arguments("string ILocalizationSet.LocalizationLanguageCode => \"de\";")]
    public void SetImplementingLanguageCode_NeedsNoLanguageInItsName(string languageCode)
    {
        // Arrange & Act
        var result = Run($$"""
            {{ShopContract}}

            public sealed class ShopLocalization1 : ILocalizationSet, IShopLocalization
            {
                {{languageCode}}
                public string Checkout => "Zur Kasse";
            }
            """);

        // Assert
        var run = result.Results.ShouldHaveSingleItem();
        run.Diagnostics.ShouldBeEmpty();
        run.GeneratedSources.ShouldHaveSingleItem().SourceText.ToString().ShouldContain("new TestApp.ShopLocalization1()");
    }

    [Test]
    public void ExcludedSet_RecordsItsLanguageWithoutRegistering()
    {
        // Arrange & Act: registered by hand, it must serve the language the build counted it for.
        var result = Run($$"""
            {{ShopContract}}

            [ExcludeFromLocalizationDiscovery]
            public sealed class ShopLocalizationDe : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Zur Kasse";
            }
            """);

        // Assert: a registrar with nothing to register is internal.
        var run = result.Results.ShouldHaveSingleItem();
        run.Diagnostics.ShouldBeEmpty();
        var text = run.GeneratedSources.ShouldHaveSingleItem().SourceText.ToString();
        text.ShouldContain("internal static class LocalizationRegistrar_TestAssembly");
        text.ShouldContain("ShopLocalizationDe>(\"de\");");
        text.ShouldNotContain("new TestApp.ShopLocalizationDe(");
    }
}
