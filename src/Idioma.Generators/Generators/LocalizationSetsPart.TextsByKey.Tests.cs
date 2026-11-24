using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;

namespace CodeChops.Idioma.Tests;

public sealed partial class LocalizationSetsPartTests
{
    [Test]
    public void GeneratedClass_ShouldGetMethodThatGivesTextByItsKey()
    {
        // Arrange & Act
        var text = ContractText(Run(GreetingSets));

        // Assert: an arm per text, by its name. A text that takes values takes each from the values passed.
        text.ShouldContain("public static string GetTextByKey(string key, params ReadOnlySpan<object?> values) => key switch");
        text.ShouldContain("        \"Checkout\" => GetKeyedText<IShopLocalization>(ShopLocalization.Checkout, key, values),");
        text.ShouldContain("        \"ItemsInCart\" => ShopLocalization.ItemsInCart(GetKeyedValue<int>(key, values, 0, 2, \"count\"), GetKeyedValue<string>(key, values, 1, 2, \"shop\")),");
        text.ShouldContain("        _ => throw CreateUnknownKeyException<IShopLocalization>(key, \"ShopLocalization\"),");
    }

    [Test]
    public void MethodThatGivesTextByKey_ShouldCompileWhateverTheTextsAreNamedOrTake()
    {
        // Arrange: texts named like the parameters of the method, like what it declares and calls, and like a
        // keyword, in a namespace with a part named System. One takes a value of a type of another namespace, and one
        // a value that may be null.
        const string source = """
            #nullable enable
            using CodeChops.Idioma;
            using Domain.Orders;

            [assembly: SupportedLanguages("en")]

            namespace Domain.Orders
            {
                public enum OrderStatus { Pending }
            }

            namespace TestApp.System
            {
                public sealed class OptionLocalizationEn : ILocalizationSet, IOptionLocalization
                {
                    public string key => "Key";
                                        public string values => "Values";
                    public string text => "Text";
                    public string @event => "Event";
                    public string GetKeyedText => "Keyed text";
                    public string GetKeyedValue => "Keyed value";
                    public string CreateUnknownKeyException => "Unknown";
                    public string Status(OrderStatus status, string? note) => $"{status}: {note}";
                }

                public static class Usage
                {
                    public static string Key => OptionLocalization.GetTextByKey("key");
                    public static string Event => OptionLocalization.GetTextByKey("event");
                    public static string Status => OptionLocalization.GetTextByKey("Status", OrderStatus.Pending, null);
                }
            }
            """;

        // Act
        var errors = GeneratorTestHelper.CompileWithGenerators(source);
        var warnings = GeneratorTestHelper.GetWarningsWithGenerators(source);
        var unresolved = GeneratorTestHelper.GetUnresolvedDocReferences(source);

        // Assert
        errors.ShouldBeEmpty();
        warnings.ShouldBeEmpty();
        unresolved.ShouldBeEmpty();
    }

    [Test]
    public void SetWithoutTexts_ShouldGetMethodThatKnowsNoKey()
    {
        // Arrange & Act
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization;
            """;

        var errors = GeneratorTestHelper.CompileWithGenerators(source);

        // Assert
        errors.ShouldBeEmpty();
        ContractText(Run(source)).ShouldContain("        _ => throw CreateUnknownKeyException<IShopLocalization>(key, \"ShopLocalization\"),");
    }

    [Test]
    [Arguments("public string GetTextByKey => \"Get\";")]
    [Arguments("public string GetTextByKey(string what) => $\"Get {what}\";")]
    public void TextNamedGetTextByKey_ShouldReportLOC300AndCompileOtherwise(string text)
    {
        // Arrange & Act: the contract keeps the text, so the set still compiles; only the generated text is left out.
        var errors = GeneratorTestHelper.CompileWithGenerators($$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
                {{text}}
            }
            """);

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Id.ShouldBe("LOC300");
        error.GetMessage().ShouldBe("'ShopLocalizationEn' has a text named 'GetTextByKey', like the generated method 'ShopLocalization.GetTextByKey', which gives a text by its key. The text 'ShopLocalization.GetTextByKey' isn't generated. Rename the text.");
        error.Location.SourceTree!.GetText().ToString(error.Location.SourceSpan).ShouldBe("GetTextByKey");
    }

    [Test]
    public void DefaultSetGeneratedCodeCannotCreate_ShouldGetNoMethodThatGivesTextByKey()
    {
        // Arrange & Act: without the texts, there is nothing a key could name.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public sealed class ShopLocalizationEn(string shop) : ILocalizationSet, IShopLocalization
            {
                public string Checkout => $"Checkout at {shop}";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBe([LocalizationSetsPart.CannotExtendDiagnosticId]);
        ContractText(result).ShouldNotContain(" GetTextByKey(string key");
    }
}
