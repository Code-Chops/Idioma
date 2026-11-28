namespace CodeChops.Idioma.Tests;

public sealed partial class LocalizationRegistrationPartTests
{
    [Test]
    public void SetOfLanguageWithDefaultScript_IsDescribedWithThatScript()
    {
        // Arrange & Act: Serbian is Cyrillic in Serbia, which the registry can't tell from the name sr-RS.
        var text = GeneratedText(Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("sr", "sr-Latn")]

            namespace TestApp;

            public interface IShopLocalization : ILocalizationSet
            {
                string Checkout { get; }
            }

            public class ShopLocalizationSr : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Каса";
            }

            public class ShopLocalizationSrLatn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Kasa";
            }

            public class ShopLocalizationSrRs : ShopLocalizationSr;
            """));

        // Assert: so a user of sr-Latn-RS gets the Latin set, not the regional one.
        text.ShouldContain("DescribeSet<TestApp.ShopLocalizationSrRs>(\"sr-RS\", \"Cyrl\");");
        text.ShouldContain("DescribeSet<TestApp.ShopLocalizationSr>(\"sr\");");
        text.ShouldContain("DescribeSet<TestApp.ShopLocalizationSrLatn>(\"sr-Latn\");");
    }

    [Test]
    public void EverySet_IsDescribedWithTheLanguageOfItsNameBeforeRegistering()
    {
        // Arrange & Act: a plain suffix, a listed longer code, an unlisted region of a listed language, a region of a
        // listed script, a region that doesn't exist (EnUk gives uk), a name whose ui isn't a listed language, and an
        // inherited language.
        var text = GeneratedText(Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "nl-BE", "zh-Hans")]

            namespace TestApp;

            public interface IShopLocalization : ILocalizationSet
            {
                string Checkout { get; }
            }

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            public class ShopLocalizationNlBe : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Afrekenen";
            }

            public class ShopLocalizationEnGb : ShopLocalizationEn
            {
                public override string Checkout => "Pay";
            }

            public sealed class ShopLocalizationZhHansTw : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Jiezhang";
            }

            public sealed class ShopLocalizationEnUk : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            public sealed class AdminUiNl : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }

            public sealed class BelgianRewording : ShopLocalizationNlBe;

            public sealed class BritishRewording : ShopLocalizationEnGb;
            """));

        // Assert: the registry reads no name, so each set is described before the sets register.
        text.ShouldContain("DescribeSet<TestApp.ShopLocalizationEn>(\"en\");");
        text.ShouldContain("DescribeSet<TestApp.ShopLocalizationNlBe>(\"nl-BE\");");
        text.ShouldContain("DescribeSet<TestApp.ShopLocalizationEnGb>(\"en-GB\");");
        text.ShouldContain("DescribeSet<TestApp.ShopLocalizationZhHansTw>(\"zh-Hans-TW\");");
        text.ShouldContain("DescribeSet<TestApp.ShopLocalizationEnUk>(\"uk\");");
        text.ShouldContain("DescribeSet<TestApp.AdminUiNl>(\"nl\");");
        text.ShouldContain("DescribeSet<TestApp.BelgianRewording>(\"nl-BE\");");
        text.ShouldContain("DescribeSet<TestApp.BritishRewording>(\"en-GB\");");
        text.LastIndexOf("DescribeSet<", StringComparison.Ordinal)
            .ShouldBeLessThan(text.IndexOf("RegisterSets(CodeChops.Idioma.Internals.SetTier.LibrarySets,", StringComparison.Ordinal));
    }

    [Test]
    [Arguments("\"en\", \"nl\"", "ShopLocalizationNlBe>(\"nl-BE\");")]
    [Arguments("\"en\", \"nl-BE\"", "ShopLocalizationNlBe>(\"nl-BE\");")]
    [Arguments("\"en\"", "ShopLocalizationNlBe>(\"be\");")]
    public void SetWhoseNameEndsInRegion_IsDescribedWithWhatTheBuildReads(string languages, string described)
    {
        // Arrange & Act: Dutch or Belgian Dutch declared gives nl-BE. Without either, the name alone gives 'be'.
        var text = GeneratedText(Run($$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages({{languages}})]

            namespace TestApp;

            public interface IShopLocalization : ILocalizationSet
            {
                string Checkout { get; }
            }

            public sealed class ShopLocalizationNlBe : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """));

        // Assert
        text.ShouldContain(described);
    }

    [Test]
    public void SetNamedAfterTwoLetterCode_IsNotDescribedWithLongerCodeItsNameHappensToEndIn()
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
    public void ExcludedSet_IsDescribedWithoutRegistering()
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
        var text = GeneratedText(result);
        text.ShouldContain("internal static class LocalizationRegistrar_TestAssembly");
        text.ShouldContain("DescribeSet<TestApp.ShopLocalizationDe>(\"de\");");
        text.ShouldNotContain("ShopLocalizationDe()");
    }

    [Test]
    public void NestedSetGeneratedCodeCanName_IsDescribedWithoutRegistering()
    {
        // Arrange & Act: nested sets are registered by hand. Only those that generated code can name are described.
        var result = Run($$"""
            {{ShopContract}}

            public static class Sets
            {
                public sealed class ShopLocalizationDe : ILocalizationSet, IShopLocalization
                {
                    public string Checkout => "Zur Kasse";
                }

                internal sealed class ShopLocalizationFr : ILocalizationSet, IShopLocalization
                {
                    public string Checkout => "Payer";
                }

                private sealed class ShopLocalizationIt : ILocalizationSet, IShopLocalization
                {
                    public string Checkout => "Paga";
                }

                public sealed class Generic<T> : ILocalizationSet, IShopLocalization
                {
                    public string Checkout => "Checkout";
                }
            }

            internal static class Hidden
            {
                private static class Inner
                {
                    public sealed class ShopLocalizationEs : ILocalizationSet, IShopLocalization
                    {
                        public string Checkout => "Pagar";
                    }
                }
            }
            """);

        // Assert
        var run = result.Results.ShouldHaveSingleItem();
        run.Diagnostics.ShouldBeEmpty();
        var text = GeneratedText(result);
        text.ShouldContain("DescribeSet<TestApp.Sets.ShopLocalizationDe>(\"de\");");
        text.ShouldContain("DescribeSet<TestApp.Sets.ShopLocalizationFr>(\"fr\");");
        text.ShouldNotContain("ShopLocalizationIt");
        text.ShouldNotContain("Generic");
        text.ShouldNotContain("ShopLocalizationEs");
        text.ShouldNotContain("RegisterSets(");
    }
}
