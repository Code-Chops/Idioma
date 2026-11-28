using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

public sealed partial class LocalizationSetsPartTests
{
    [Test]
    public void TextInheritedByDefaultSet_ShouldReportLOC303AtTheText()
    {
        // Arrange & Act: the contract is written from what the English set declares, so Brand and Welcome would be left
        // out without a word, and Dutch would never word them. Cart is declared again, Note is marked as no text, and
        // Message comes from another assembly.
        var result = Run($$"""
            {{Header}}

            public abstract class ShopBase : System.Exception
            {
                public string Brand => "Idioma";
                public string Welcome(string name) => $"Welcome, {name}!";
                public string Welcome(int count) => $"Welcome, {count}!";
                public virtual string Cart => "Cart";

                [ExcludeFromLocalizationDiscovery]
                public string Note => "";
            }

            public class ShopLocalizationEn : ShopBase, ILocalizationSet, IShopLocalization
            {
                public override string Cart => "Cart";
            }

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Cart => "Winkelwagen";
            }
            """);

        // Assert: once per name, at the text.
        var diagnostics = result.Results.ShouldHaveSingleItem().Diagnostics;
        diagnostics.ShouldAllBe(static diagnostic => diagnostic.Id == "LOC303" && diagnostic.Severity == DiagnosticSeverity.Error);
        diagnostics.Select(static diagnostic => diagnostic.GetMessage()).ShouldBe(
        [
            "'ShopLocalizationEn' inherits the text 'Brand' from 'ShopBase', so its contract leaves 'Brand' out, and no other language has to word it. Declare 'Brand' in 'ShopLocalizationEn', or mark it [ExcludeFromLocalizationDiscovery] in 'ShopBase' if it isn't a text.",
            "'ShopLocalizationEn' inherits the text 'Welcome' from 'ShopBase', so its contract leaves 'Welcome' out, and no other language has to word it. Declare 'Welcome' in 'ShopLocalizationEn', or mark it [ExcludeFromLocalizationDiscovery] in 'ShopBase' if it isn't a text.",
        ], ignoreOrder: true);
        diagnostics.Select(static diagnostic => diagnostic.Location.SourceTree!.ToString()[diagnostic.Location.SourceSpan.Start..][..7]).ShouldBe(["Brand =", "Welcome"], ignoreOrder: true);
    }

    [Test]
    public void TextInheritedBySetOfOtherLanguage_ShouldReportNothing()
    {
        // Arrange & Act: only the default-language set gives the contract. A Dutch set that takes a text from a base
        // implements the contract with it, as any set may.
        var result = Run($$"""
            {{Header}}

            public abstract class DutchBase
            {
                public string Cart => "Winkelwagen";
            }

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Cart => "Cart";
            }

            public class ShopLocalizationNl : DutchBase, ILocalizationSet, IShopLocalization;
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void OnlyPublicInstanceMethodsReturningStringWithParametersAreTexts()
    {
        // Arrange: a virtual method is a text (so a set can derive and override), an inherited method marked as no text
        // stays out (LOC303 would report one that isn't), and the rest are not texts by shape.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public class ShopWordingBase
            {
                [ExcludeFromLocalizationDiscovery]
                public string Inherited(string name) => name;
            }

            public class ShopLocalizationEn : ShopWordingBase, ILocalizationSet, IShopLocalization
            {
                public string Greeting(string name) => $"Hello, {name}!";
                public virtual string Farewell(string name) => $"Goodbye, {name}!";
                internal string Hidden(string name) => name;
                private string Secret(string name) => name;
                public static string Shared(string name) => name;
                public int Count(string name) => name.Length;
                public void Nothing(string name) { }
                public ref readonly string Reference(string[] names) => ref names[0];
            }
            """;

        // Act
        var result = Run(source);
        var text = ContractText(result);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
        text.ShouldContain("    string Greeting(string name);");
        text.ShouldContain("    string Farewell(string name);");
        ShownValues(text, "Greeting").ShouldBe(["name"]);
        ShownValues(text, "Farewell").ShouldBe(["name"]);
        foreach (var name in new[] { "Inherited", "Hidden", "Secret", "Shared", "Count", "Nothing", "Reference" })
            text.ShouldNotContain($" {name}(", customMessage: name);
    }

    [Test]
    [Arguments("public string Greeting<T>(T name) => $\"Hello, {name}!\";", "it's generic")]
    [Arguments("public string Greeting(string name) => name; private string Greeting(string name, bool formal) => name;", "its set has another method of that name")]
    [Arguments("public string Greeting(ref string name) => name;", "its parameter 'name' is passed by reference")]
    [Arguments("public string Greeting(in int count) => \"\";", "its parameter 'count' is passed by reference")]
    [Arguments("public string Greeting(out string name) => name = \"\";", "its parameter 'name' is passed by reference")]
    [Arguments("public string Greeting(params string[] names) => \"\";", "its parameter 'names' is a params parameter")]
    [Arguments("public string Greeting(string name = \"you\") => name;", "its parameter 'name' has a default value")]
    [Arguments("public string Greeting(System.ReadOnlySpan<char> name) => name.ToString();", "its parameter 'name' is of a type the generated class can't pass on")]
    [Arguments("public unsafe string Greeting(int* count) => \"\";", "its parameter 'count' is of a type the generated class can't pass on")]
    [Arguments("public string Greeting(dynamic name) => \"\";", "its parameter 'name' is dynamic")]
    public void MethodThatCannotBeAText_ShouldReportLOC400AtItAndEmitNoTextForIt(string method, string expectedReason)
    {
        // Arrange
        var source = $$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
                {{method}}
            }
            """;

        // Act
        var result = Run(source);

        // Assert: at the method, which is what to change. The other texts are generated all the same.
        var diagnostic = SingleDiagnostic(result, LocalizationSetsPart.UnsupportedTextDiagnosticId);
        diagnostic.Id.ShouldBe("LOC400");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("'ShopLocalizationEn.Greeting' is a public method that returns a string");
        diagnostic.GetMessage().ShouldContain(expectedReason);
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).ShouldBe("Greeting");

        var text = ContractText(result);
        text.ShouldContain("    string Checkout { get; }");
        text.ShouldNotContain("Greeting");
    }

    [Test]
    public void TextWrittenAsMethodWithoutParameters_ShouldReportLOC400SayingToWriteAProperty()
    {
        // Arrange: a text without values is a property. Written as a method it is no text, and what calls it as one
        // would only be told that nothing has its name (CS0117). The Dutch set starts no contract, so its method is
        // for the compiler.
        const string source = $$"""
            {{Header}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
                public string Greeting() => "Hello";
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
                public string Greeting() => "Hallo";
            }
            """;

        // Act
        var result = Run(source);
        var errors = GeneratorTestHelper.CompileWithGenerators(source);

        // Assert: at the method, and the other texts are generated all the same.
        var diagnostic = SingleDiagnostic(result, LocalizationSetsPart.UnsupportedTextDiagnosticId);
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldBe("'ShopLocalizationEn.Greeting' is a public method that returns a string, which makes it a text with parameters, but it takes no parameters, so write it as a property. No text is generated for it. Fix the method. If it isn't a text, make it internal or private, or mark it [ExcludeFromLocalizationDiscovery].");
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).ShouldBe("Greeting");
        DiagnosticIds(result).ShouldBe(["LOC400"]);
        ContractText(result).ShouldContain("    string Checkout { get; }");
        ContractText(result).ShouldNotContain("Greeting");
        errors.Select(static error => error.Id).ShouldBe(["LOC400"]);
    }

    [Test]
    public void TextWrittenAsMethodWithArglist_ShouldReportLOC400NamingArglist()
    {
        // Arrange: __arglist has no parameter symbol, so the method looks like one that takes none. Writing it as a
        // property would be the wrong advice.
        const string source = $$"""
            {{Header}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
                public string Greeting(__arglist) => "Hello";
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """;

        // Act
        var result = Run(source);

        // Assert
        var diagnostic = SingleDiagnostic(result, LocalizationSetsPart.UnsupportedTextDiagnosticId);
        diagnostic.GetMessage().ShouldBe("'ShopLocalizationEn.Greeting' is a public method that returns a string, which makes it a text with parameters, but it takes __arglist, which the generated class can't pass on. No text is generated for it. Fix the method. If it isn't a text, make it internal or private, or mark it [ExcludeFromLocalizationDiscovery].");
        ContractText(result).ShouldNotContain("Greeting");
    }

    [Test]
    public void TextWrittenAsMethodWithParameterAndArglist_ShouldReportLOC400NamingArglist()
    {
        // Arrange: a parameter before __arglist, so the method has a parameter symbol and looks like a text.
        const string source = $$"""
            {{Header}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
                public string Greeting(string name, __arglist) => "Hello";
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """;

        // Act
        var result = Run(source);

        // Assert
        var diagnostic = SingleDiagnostic(result, LocalizationSetsPart.UnsupportedTextDiagnosticId);
        diagnostic.GetMessage().ShouldBe("'ShopLocalizationEn.Greeting' is a public method that returns a string, which makes it a text with parameters, but it takes __arglist, which the generated class can't pass on. No text is generated for it. Fix the method. If it isn't a text, make it internal or private, or mark it [ExcludeFromLocalizationDiscovery].");
        ContractText(result).ShouldNotContain("Greeting");
    }

    [Test]
    [Arguments("", "public override string ToString() => \"Shop\";")]
    [Arguments("", "public new string ToString() => \"Shop\";")]
    [Arguments("", "public new string Label() => \"Shop\";")]
    [Arguments("", "[ExcludeFromLocalizationDiscovery] public string Describe() => \"Shop\";")]
    [Arguments(", IDescribed", "public string Describe() => \"Shop\";")]
    [Arguments(", IDescribed", "string IDescribed.Describe() => \"Shop\";")]
    [Arguments("", "public static string Describe() => \"Shop\";")]
    [Arguments("", "internal string Describe() => \"Shop\";")]
    [Arguments("", "public string Describe<T>() => typeof(T).Name;")]
    [Arguments("", "public object Describe() => \"Shop\";")]
    public void MethodWithoutParametersThatPlainlyIsNoText_ShouldReportNothing(string baseType, string method)
    {
        // Arrange: an override, a method that hides one of a base class, a member marked as no text, the
        // implementation of an interface, and a method that is no text by its shape.
        var source = $$"""
            {{Header}}

            public interface IDescribed
            {
                string Describe();
            }

            public class Labelled
            {
                public string Label() => "Label";
            }

            public sealed class ShopLocalizationEn : Labelled, ILocalizationSet, IShopLocalization{{baseType}}
            {
                public string Checkout => "Checkout";
                {{method}}
            }

            {{DutchSet.Replace("public string Total => \"Totaal\";", "", StringComparison.Ordinal)}}
            """;

        // Act
        var result = Run(source);
        var errors = GeneratorTestHelper.CompileWithGenerators(source);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
        errors.ShouldBeEmpty();
        ContractText(result).ShouldNotContain("Describe");
    }

    [Test]
    public void TwoPublicMethodsOfOneName_ShouldEachReportLOC400()
    {
        // Arrange & Act: a text is told from the others by its name, which is its key, so neither can be one.
        var result = Run($$"""
            {{Header}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Greeting(string name) => $"Hello, {name}!";
                public string Greeting(string name, string title) => $"Hello, {title} {name}!";
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization;
            """);

        // Assert
        DiagnosticIds(result).ShouldBe(["LOC400", "LOC400"]);
        ContractText(result).ShouldNotContain("Greeting");
    }

    [Test]
    public void MethodThatCannotBeAText_ShouldBeNoPartOfTheContract()
    {
        // Arrange & Act: LOC400 is the only error, so the Dutch set, which has no such method, implements the contract.
        var errors = GeneratorTestHelper.CompileWithGenerators($$"""
            {{Header}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
                public string Greeting(string name = "you") => $"Hello, {name}!";
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """);

        // Assert
        errors.ShouldHaveSingleItem().Id.ShouldBe("LOC400");
    }

    [Test]
    public void SetWithoutTextThatTakesValues_ShouldNotCompile()
    {
        // Arrange & Act: the contract demands the method of every language, as it demands every property.
        var errors = GeneratorTestHelper.CompileWithGenerators($$"""
            {{Header}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Greeting(string name) => $"Hello, {name}!";
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization;
            """);

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Id.ShouldBe("CS0535");
        error.GetMessage().ShouldContain("IShopLocalization.Greeting(string)");
    }

    [Test]
    public void MemberMarkedAsExcluded_ShouldBeNoText()
    {
        // Arrange: a property and a method that have to be public and are no texts, a method that could not be a text
        // anyway, and one named like the generated class. The Dutch set has none of them.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Total(decimal amount) => $"Total: {Money(amount)}";

                [ExcludeFromLocalizationDiscovery]
                public string Pattern => "C";

                [ExcludeFromLocalizationDiscovery]
                public string Money(decimal amount) => amount.ToString(Pattern);

                [ExcludeFromLocalizationDiscovery]
                public string Generic<T>(T value) => $"{value}";

                [ExcludeFromLocalizationDiscovery]
                public string ShopLocalization(string value) => value;
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Total(decimal amount) => $"Totaal: {amount:C}";
            }
            """;

        // Act
        var result = Run(source);
        var text = ContractText(result);
        var errors = GeneratorTestHelper.CompileWithGenerators(source);

        // Assert: nothing is reported about them, the contract and the class hold the one text, and the Dutch set,
        // which has only that one, compiles.
        DiagnosticIds(result).ShouldBeEmpty();
        errors.ShouldBeEmpty();
        text.ShouldContain("    string Total(decimal amount);");
        ShownValues(text, "Total").ShouldBe(["amount"]);
        foreach (var member in new[] { " Pattern {", " Money(", " Generic", " ShopLocalization(" })
            text.ShouldNotContain(member, customMessage: member);
    }

    [Test]
    [Arguments("[ExcludeFromLocalizationDiscovery]", false)]
    [Arguments("", true)]
    public void OverrideOfPublicMemberOfBaseClass_ShouldBeTextUnlessMarkedAsExcluded(string attribute, bool isText)
    {
        // Arrange: an override can't be private or static, so only the attribute keeps it from being a text. The Dutch
        // set doesn't implement it.
        var source = $$"""
            {{Header}}

            public abstract class PriceFormatter
            {
                public abstract string Money(decimal amount);
            }

            public sealed class ShopLocalizationEn : PriceFormatter, ILocalizationSet, IShopLocalization
            {
                public string Total(decimal amount) => $"Total: {Money(amount)}";

                {{attribute}}
                public override string Money(decimal amount) => amount.ToString("C");
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Total(decimal amount) => $"Totaal: {amount:C}";
            }
            """;

        // Act
        var result = Run(source);
        var text = ContractText(result);
        var errors = GeneratorTestHelper.CompileWithGenerators(source);

        // Assert: unmarked, it's a text of the contract, so the Dutch set doesn't compile without it.
        string[] expectedErrors = isText ? ["CS0535"] : [];
        DiagnosticIds(result).ShouldBeEmpty();
        text.Contains("    string Money(decimal amount);", StringComparison.Ordinal).ShouldBe(isText);
        errors.Select(static error => error.Id).ShouldBe(expectedErrors);
    }
}
