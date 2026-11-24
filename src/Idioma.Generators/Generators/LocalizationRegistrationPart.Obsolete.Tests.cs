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

    [Test]
    public void SetObsoleteAsError_ShouldReportLOC208AndLeaveGeneratedCodeCompiling()
    {
        // Arrange: naming the set is an error (CS0619), which no pragma turns off, so generated code mustn't name it.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            [System.Obsolete("Use the new Dutch set.", error: true)]
            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """;

        // Act
        var errors = GeneratorTestHelper.CompileWithGenerators(source);
        var warnings = GeneratorTestHelper.GetWarningsWithGenerators(source);

        // Assert: it isn't registered, and the warning says why.
        errors.ShouldBeEmpty();
        var warning = warnings.ShouldHaveSingleItem();
        warning.Id.ShouldBe("LOC208");
        warning.GetMessage().ShouldContain("because it's marked [Obsolete] as an error, and generated code can't name such a class");
    }

    [Test]
    public void DefaultLanguageSetObsoleteAsError_ShouldReportLOC200AsItsOnlyError()
    {
        // Arrange & Act
        var errors = GeneratorTestHelper.CompileWithGenerators("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            [System.Obsolete("Use the texts of the new shop.", error: true)]
            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """);

        // Assert: the contract is still written, so the Dutch set compiles.
        var error = errors.ShouldHaveSingleItem();
        error.Id.ShouldBe("LOC200");
        error.GetMessage().ShouldEndWith("Mark it [Obsolete] as a warning while code still uses it, or remove it.");
    }

    [Test]
    [Arguments("[System.Diagnostics.CodeAnalysis.Experimental(\"SHOP001\")]", "")]
    [Arguments("", "[System.Diagnostics.CodeAnalysis.Experimental(\"SHOP001\")]")]
    [Arguments("[System.Obsolete(\"Use the new shop.\", DiagnosticId = \"SHOP002\")]", "")]
    [Arguments("", "[System.Obsolete(\"Use the new shop.\", DiagnosticId = \"SHOP002\")]")]
    public void SetWithDiagnosticOfItsOwn_LeavesNothingInGeneratedCode(string english, string dutch)
    {
        // Arrange: naming the set reports a diagnostic of its author's choosing, which a build treats as an error
        // (Experimental) or stops at when it treats warnings as errors (Obsolete).
        var source = $$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            {{english}}
            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            {{dutch}}
            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """;

        // Act
        var errors = GeneratorTestHelper.CompileWithGenerators(source);
        var warnings = GeneratorTestHelper.GetWarningsWithGenerators(source);

        // Assert
        errors.ShouldBeEmpty();
        warnings.ShouldBeEmpty();
    }
}
