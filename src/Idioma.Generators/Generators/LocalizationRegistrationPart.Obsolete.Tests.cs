using CodeChops.Idioma.Tests.TestHelpers;

namespace CodeChops.Idioma.Tests;

public sealed partial class LocalizationRegistrationPartTests
{
    [Test]
    [Arguments("[System.Obsolete]")]
    [Arguments("[System.Obsolete(\"Dutch is no longer sold in.\")]")]
    public void ObsoleteSet_RegistersWithoutWarningInGeneratedCode(string attribute)
    {
        // Arrange: a set on its way out still registers. A warning about naming it in generated code is one its author
        // can't act on, and stops a build that treats warnings as errors.
        var source = $$"""
            {{GeneratedShopContract}}

            {{attribute}}
            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Afrekenen";
            }
            """;

        // Act
        var text = GeneratedText(Run(source));
        var warnings = GeneratorTestHelper.GetWarningsWithGenerators(source);

        // Assert: the pragma every generated file has covers the set too.
        text.ShouldContain("#pragma warning disable CS0612, CS0618");
        text.ShouldContain("#pragma warning restore CS0612, CS0618");
        warnings.ShouldBeEmpty();
    }

    [Test]
    public void ObsoleteDefaultLanguageSet_LeavesNoWarningInGeneratedCode()
    {
        // Arrange: the generated texts name the set to take their default wording from it.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "en-GB")]

            namespace TestApp;

            [System.Obsolete("Use the texts of the new shop.")]
            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            [System.Obsolete("Use the texts of the new shop.")]
            public class ShopLocalizationEnGb : ShopLocalizationEn;
            """;

        // Act
        var warnings = GeneratorTestHelper.GetWarningsWithGenerators(source);

        // Assert
        warnings.ShouldBeEmpty();
    }
}
