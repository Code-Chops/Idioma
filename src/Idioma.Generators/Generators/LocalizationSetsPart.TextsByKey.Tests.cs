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

        // Assert: an arm per text, by its name. A text that takes values takes each from the values passed. A key no
        // text has gives null, and only a null key throws.
        text.ShouldContain("public static string? GetTextByKey(string key, params ReadOnlySpan<object?> values) => key switch");
        text.ShouldContain("        \"Checkout\" => ResolveKeyedWording<IShopLocalization>(ShopLocalization.Checkout, key, values),");
        text.ShouldContain("        \"ItemsInCart\" => ShopLocalization.ItemsInCart(GetKeyedValue<int>(key, values, 0, 2, \"count\", acceptsNull: true), GetKeyedValue<string>(key, values, 1, 2, \"shop\", acceptsNull: true)),");
        text.ShouldContain("        null => throw CreateNullKeyException<IShopLocalization>(key),");
        text.ShouldContain("        _ => null,");
        text.ShouldNotContain("TryGetTextByKey");
        text.ShouldContain("public static ImmutableArray<string> TextKeys => TextKeysOf<IShopLocalization>.Keys;");
        text.ShouldContain("    internal static readonly ImmutableArray<string> Keys = [\"Checkout\", \"Greeting\", \"ItemsInCart\"];");
    }

    [Test]
    public void MethodThatGivesTextByKey_ShouldAcceptNullOnlyForParameterThatMayBeNull()
    {
        // Arrange: with nullable annotations, a string that isn't nullable next to one that is, a nullable value type,
        // and a type of another namespace.
        const string source = """
            #nullable enable
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Note(string name, string? note, int? count, System.Uri link) => $"{name}: {note} {count} {link}";
            }
            """;

        // Act
        var text = ContractText(Run(source));

        // Assert
        text.ShouldContain("GetKeyedValue<string>(key, values, 0, 4, \"name\", acceptsNull: false)");
        text.ShouldContain("GetKeyedValue<string?>(key, values, 1, 4, \"note\", acceptsNull: true)");
        text.ShouldContain("GetKeyedValue<int?>(key, values, 2, 4, \"count\", acceptsNull: true)");
        text.ShouldContain("GetKeyedValue<System.Uri>(key, values, 3, 4, \"link\", acceptsNull: false)");
    }

    [Test]
    public void TextKeys_ShouldBeSortedByNameInOrdinalOrder()
    {
        // Arrange: texts declared out of order, and one with a lower-case name, which ordinal order puts after the
        // capitals.
        const string source = $$"""
            {{Header}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
                public string about => "About";
                public string Cart => "Cart";
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
                public string about => "Over";
                public string Cart => "Winkelwagen";
            }
            """;

        // Act
        var text = ContractText(Run(source));

        // Assert: so that a binary search with StringComparer.Ordinal finds each key.
        text.ShouldContain("    internal static readonly ImmutableArray<string> Keys = [\"Cart\", \"Checkout\", \"about\"];");
    }

    [Test]
    public void MethodThatGivesTextByKey_ShouldCompileWhateverTheTextsAreNamedOrTake()
    {
        // Arrange: texts named like the parameters of the method, like what it declares and calls, like a member it no
        // longer has, and like a keyword, in a namespace with a part named System. One takes a value of a type of
        // another namespace, and one a value that may be null.
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
                    public string ResolveKeyedWording => "Keyed text";
                    public string GetKeyedValue => "Keyed value";
                    public string CreateNullKeyException => "Null key";
                    public string TryGetTextByKey => "Try get";
                    public string ImmutableArray => "Immutable array";
                    public string TextKeysOf => "Text keys of";
                    public string Status(OrderStatus status, string? note) => $"{status}: {note}";
                }

                public static class Usage
                {
                    public static string? Key => OptionLocalization.GetTextByKey("key");
                    public static string? Event => OptionLocalization.GetTextByKey("event");
                    public static string Text => OptionLocalization.GetTextByKey("text") ?? "";
                    public static string TryGet => OptionLocalization.TryGetTextByKey;
                    public static int Keys => OptionLocalization.TextKeys.Length;
                    public static string? Status => OptionLocalization.GetTextByKey("Status", OrderStatus.Pending, null);
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
        ContractText(Run(source)).ShouldContain("    internal static readonly ImmutableArray<string> Keys = [];");
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
    [Arguments("TextKeys", "the generated property 'ShopLocalization.TextKeys', which lists the key of every text")]
    public void TextNamedLikeAnotherMemberForKeys_ShouldReportLOC300(string name, string taken)
    {
        // Arrange & Act
        var errors = GeneratorTestHelper.CompileWithGenerators($$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
                public string {{name}} => "Keys";
            }
            """);

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Id.ShouldBe("LOC300");
        error.GetMessage().ShouldBe($"'ShopLocalizationEn' has a text named '{name}', like {taken}. The text 'ShopLocalization.{name}' isn't generated. Rename the text.");
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
