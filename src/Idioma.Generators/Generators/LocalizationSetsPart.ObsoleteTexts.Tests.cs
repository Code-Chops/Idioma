using CodeChops.Idioma.Tests.TestHelpers;

namespace CodeChops.Idioma.Tests;

public sealed partial class LocalizationSetsPartTests
{
    /// <summary>
    /// Sets whose texts are marked [Obsolete], as a warning and as an error, and [Experimental], with the given code
    /// that shows them.
    /// </summary>
    private static string MarkedTexts(string usage) => $$"""
        using System;
        using System.Diagnostics.CodeAnalysis;
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace TestApp;

        public enum Step { Start }

        public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
        {
            [Obsolete("Use Pay.")]
            public string Checkout => "Checkout";

            [Obsolete("Gone.", true)]
            public string Old => "Old";

            [Obsolete("Gone too.", error: true)]
            public string Older(int count) => $"{count} old";

            [Experimental("SHOP001")]
            public string Beta => "Beta";

            public string Pay => "Pay";
        }

        public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
        {
            public string Checkout => "Afrekenen";
            public string Old => "Oud";
            public string Older(int count) => $"{count} oud";
            public string Beta => "Bèta";
            public string Pay => "Betalen";
        }

        public sealed class StepLocalizationEn : ILocalizationSet<Step>, IStepLocalization
        {
            [Obsolete("Gone.", true)]
            public string Start => "Start";
        }

        public sealed class StepLocalizationNl : ILocalizationSet<Step>, IStepLocalization
        {
            public string Start => "Begin";
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
    public void TextsMarkedObsoleteOrExperimental_ShouldCompileWithoutWarnings()
    {
        // Arrange & Act: the generated file names each text outside its member, in GetTextByKey and GetTextByValue.
        var source = MarkedTexts("public static string Pay => ShopLocalization.Pay;");

        // Assert
        GeneratorTestHelper.CompileWithGenerators(source).ShouldBeEmpty();
        GeneratorTestHelper.GetWarningsWithGenerators(source).ShouldBeEmpty();
    }

    [Test]
    [Arguments("public static string Text => ShopLocalization.Checkout;", "CS0618")]
    [Arguments("public static string Text => ShopLocalization.Old;", "CS0619")]
    [Arguments("public static string Text => ShopLocalization.Older(2);", "CS0619")]
    [Arguments("public static string Text => ShopLocalization.Beta;", "SHOP001")]
    [Arguments("public static string Text => StepLocalization.Start;", "CS0619")]
    public void CodeShowingTextMarkedObsoleteOrExperimental_ShouldBeToldAsForTheSet(string usage, string expected)
    {
        // Arrange & Act
        var source = MarkedTexts(usage);
        var reported = GeneratorTestHelper.CompileWithGenerators(source).Concat(GeneratorTestHelper.GetWarningsWithGenerators(source)).ToArray();

        // Assert: once, at the code that shows the text, and nowhere in the generated file.
        var diagnostic = reported.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(expected);
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).ShouldStartWith(usage.Split("=> ")[1].Split('(')[0].TrimEnd(';'));
    }
}
