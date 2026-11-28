using CodeChops.Idioma.Tests.TestHelpers;

namespace CodeChops.Idioma.Tests;

public sealed partial class LocalizationSetsPartTests
{
    /// <summary>
    /// Sets with a text marked [Obsolete], with the given code that shows a text.
    /// </summary>
    private static string MarkedTexts(string usage) => $$"""
        using System;
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace TestApp;

        public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
        {
            [Obsolete("Use Pay.")]
            public string Checkout => "Checkout";

            public string Pay => "Pay";
        }

        public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
        {
            public string Checkout => "Afrekenen";
            public string Pay => "Betalen";
        }

        public static class Page
        {
            {{usage}}
        }
        """;

    [Test]
    public void TextMarkedAsCompositeFormat_ShouldBeMarkedSoInGeneratedClass()
    {
        // Arrange: an error message that an attribute of another project formats.
        const string source = """
            using System.Diagnostics.CodeAnalysis;
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                [StringSyntax(StringSyntaxAttribute.CompositeFormat)]
                public string NameRequired => "Enter {0}";
            }
            """;

        // Act
        var text = ContractText(Run(source));

        // Assert: so the code that reads it is told, and the analyzer leaves its placeholders alone in every language.
        text.Replace("\r\n", "\n", StringComparison.Ordinal).ShouldContain("    [System.Diagnostics.CodeAnalysis.StringSyntax(\"CompositeFormat\")]\n    public static string NameRequired =>");
        GeneratorTestHelper.CompileWithGenerators(source).ShouldBeEmpty();
        GeneratorTestHelper.GetWarningsWithGenerators(source).ShouldBeEmpty();
    }

    [Test]
    public void TextMarkedObsolete_ShouldCompileWithoutWarnings()
    {
        // Arrange & Act: the generated file names each text outside its member, in GetTextByKey.
        var source = MarkedTexts("public static string Pay => ShopLocalization.Pay;");

        // Assert
        GeneratorTestHelper.CompileWithGenerators(source).ShouldBeEmpty();
        GeneratorTestHelper.GetWarningsWithGenerators(source).ShouldBeEmpty();
    }

    [Test]
    public void CodeShowingTextMarkedObsolete_ShouldBeToldAsForTheSet()
    {
        // Arrange & Act
        const string usage = "public static string Text => ShopLocalization.Checkout;";
        var source = MarkedTexts(usage);
        var reported = GeneratorTestHelper.CompileWithGenerators(source).Concat(GeneratorTestHelper.GetWarningsWithGenerators(source)).ToArray();

        // Assert: once, at the code that shows the text, and nowhere in the generated file.
        var diagnostic = reported.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("CS0618");
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).ShouldBe("ShopLocalization.Checkout");
    }
}
