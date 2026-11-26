using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;

namespace CodeChops.Idioma.Tests;

public sealed partial class LocalizationSetsPartTests
{
    [Test]
    public void TextsNamedLikeTheGeneratedHelpers_ShouldCompile()
    {
        // Arrange & Act: the set behind the defaults is named with a type argument, which no text stands before, and it
        // is file-local, so a type of that name elsewhere, with or without a type parameter, can't take its place.
        var errors = GeneratorTestHelper.CompileWithGenerators("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public sealed class OptionLocalizationEn : ILocalizationSet, IOptionLocalization
            {
                public string Default => "Default";
                public string DefaultSet => "Default set";
                public string Instance => "Instance";
                public string KeyPrefix => "Prefix";
            }

            public sealed class OptionLocalizationNl : ILocalizationSet, IOptionLocalization
            {
                public string Default => "Standaard";
                public string DefaultSet => "Standaardset";
                public string Instance => "Exemplaar";
                public string KeyPrefix => "Voorvoegsel";
            }

            public static class DefaultSet;

            public static class DefaultSet<T>;
            """);

        // Assert
        errors.ShouldBeEmpty();
    }

    [Test]
    [Arguments("namespace Checkout;", "Checkout")]
    [Arguments("namespace Orders.Texts;", "Orders")]
    [Arguments("", "ResolveWording")]
    [Arguments("namespace TestApp;", "CodeChops")]
    [Arguments("namespace TestApp;", "GetLocalizationSet")]
    [Arguments("namespace TestApp;", "ResolveWording")]
    [Arguments("namespace TestApp;", "ResolveKeyedWording")]
    public void TextNamedLikeNameGeneratedClassWrites_ShouldCompileWithResolvedCrefs(string @namespace, string text)
    {
        // Arrange: inside the generated class a text stands before every namespace and type of its name. So the class
        // names the set behind the defaults without its namespace, as a type argument, which a text named like the
        // namespace doesn't stand before. It calls what finds a set and what words a text by name alone, with a type
        // argument, without the package's namespace, which a text named CodeChops would stand before.
        var source = $$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            {{@namespace}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string {{text}} => "Text";
                public string Other => "Other";
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string {{text}} => "Tekst";
                public string Other => "Ander";
            }
            """;

        // Act
        var errors = GeneratorTestHelper.CompileWithGenerators(source);
        var unresolved = GeneratorTestHelper.GetUnresolvedDocReferences(source);

        // Assert
        errors.ShouldBeEmpty();
        unresolved.ShouldBeEmpty();
    }

    [Test]
    [Arguments("Idioma.Demo", "")]
    [Arguments("Generated.Texts", "")]
    [Arguments("Internals.Texts", "")]
    [Arguments("Shop", "namespace Shop { public sealed class Shop; }")]
    public void NamespaceOrClassStandingBeforeFullNameOfSet_ShouldCompileWithResolvedCrefs(string @namespace, string other)
    {
        // Arrange: where the full name of a set is looked for from the wrong place. Inside the registrar's namespace,
        // Idioma.Demo.ShopLocalizationEn is looked for in CodeChops.Idioma, so the registrar names the sets outside it,
        // at the top level of its file. Inside the namespace Shop, a class Shop stands before the namespace, so the file
        // of the contract names the sets of its own namespace without it. A set and an inline text, so that all three
        // generators write their code.
        var source = $$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            {{other}}

            namespace {{@namespace}}
            {
                public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
                {
                    public virtual string Cart => "Cart";
                }

                public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
                {
                    public virtual string Cart => "Winkelwagen";
                }

                public static class InlineTexts
                {
                    public static string Orders => LocalizedText.Inline(en: "Orders", nl: "Bestellingen");
                }
            }
            """;

        // Act
        var errors = GeneratorTestHelper.CompileWithGenerators(source);
        var unresolved = GeneratorTestHelper.GetUnresolvedDocReferences(source);

        // Assert
        errors.ShouldBeEmpty();
        unresolved.ShouldBeEmpty();
    }

    [Test]
    [Arguments("OptionLocalization")]
    public void TextNamedLikeGeneratedClass_ShouldReportLOC300AndCompileOtherwise(string name)
    {
        // Arrange & Act: the contract keeps the text, so the sets still compile; only the generated text is left out.
        var errors = GeneratorTestHelper.CompileWithGenerators($$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public sealed class OptionLocalizationEn : ILocalizationSet, IOptionLocalization
            {
                public string {{name}} => "Text";
                public string Other => "Other";
            }

            public sealed class OptionLocalizationNl : ILocalizationSet, IOptionLocalization
            {
                public string {{name}} => "Tekst";
                public string Other => "Ander";
            }
            """);

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Id.ShouldBe(LocalizationSetsPart.ReservedTextNameDiagnosticId);
        error.Id.ShouldBe("LOC300");
        error.GetMessage().ShouldContain($"'OptionLocalizationEn' has a text named '{name}'");
        error.GetMessage().ShouldContain($"'OptionLocalization.{name}' isn't generated");

        // At the text of the default-language set, which is what to rename.
        error.Location.SourceTree!.GetText().ToString(error.Location.SourceSpan).ShouldBe(name);
        error.Location.GetLineSpan().StartLinePosition.Line.ShouldBe(8);
    }

    [Test]
    public void TextNamedLikeMemberOfObject_ShouldCompileWithoutWarnings()
    {
        // Arrange: a text named like a member every class has, as a filter's "Equals" is. The generated class hides
        // that member as the set does, which without the new modifier is a warning (CS0108) in generated code. Finalize
        // hides nothing, and neither does any other text, where the modifier would be a warning itself (CS0109).
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public sealed class FilterLocalizationEn : ILocalizationSet, IFilterLocalization
            {
                public new string Equals => "Equals";
                public new string GetHashCode => "Hash code";
                public new string GetType => "Type";
                public new string MemberwiseClone => "Copy";
                public new string ReferenceEquals => "Is the same as";
                public new string ToString => "As text";
                public string Finalize => "Finalize";
                public string Contains => "Contains";
            }

            public sealed class FilterLocalizationNl : ILocalizationSet, IFilterLocalization
            {
                public new string Equals => "Is gelijk aan";
                public new string GetHashCode => "Hashcode";
                public new string GetType => "Type";
                public new string MemberwiseClone => "Kopie";
                public new string ReferenceEquals => "Is hetzelfde als";
                public new string ToString => "Als tekst";
                public string Finalize => "Afronden";
                public string Contains => "Bevat";
            }
            """;

        // Act
        var text = ContractText(Run(source));
        var warnings = GeneratorTestHelper.GetWarningsWithGenerators(source);
        var errors = GeneratorTestHelper.CompileWithGenerators(source);

        // Assert
        text.ShouldContain("public static new string Equals => GetLocalizationSet<IFilterLocalization>(");
        text.ShouldContain("public static string Contains => GetLocalizationSet<IFilterLocalization>(");
        warnings.ShouldBeEmpty();
        errors.ShouldBeEmpty();
    }
}
