using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="LocalizationSetsPart"/>: a partial <see cref="ILocalizationSet"/> class named after the default
/// language yields its contract, the partial declaration implementing it, and the static class holding the texts, with
/// crefs to every translation; every language of <c>[assembly: SupportedLanguages]</c> must have a set, in its own
/// assembly and in the libraries it references.
/// </summary>
public sealed partial class LocalizationSetsPartTests
{
    #region Fixture
    private const string Header = """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace TestApp;
        """;

    private const string DutchSet = """
        public sealed class ShopLocalizationNl : CodeChops.Idioma.ILocalizationSet, IShopLocalization
        {
            public string Checkout => "Afrekenen";
            public string Total => "Totaal";
        }
        """;

    private const string ShopSets = $$"""
        {{Header}}

        public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
        {
            /// <summary>
            /// The caption of the checkout button.
            /// </summary>
            public string Checkout => "Checkout";
            public string Total => "Total";
        }

        {{DutchSet}}
        """;

    /// <summary>
    /// A library in English and Dutch, with a public contract an app could add a language to.
    /// </summary>
    private static MetadataReference GridLibrary { get; } = GeneratorTestHelper.CompileLibraryWithGenerators("GridLibrary", """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace GridLibrary;

        public class GridLocalizationEn : ILocalizationSet, IGridLocalization
        {
            public virtual string First => "First";
        }

        public sealed class GridLocalizationNl : ILocalizationSet, IGridLocalization
        {
            public string First => "Eerste";
        }
        """);

    /// <summary>
    /// A library in English and Dutch with only inline texts, so nothing an app could add a language to.
    /// </summary>
    private static MetadataReference InlineLibrary { get; } = GeneratorTestHelper.CompileLibraryWithGenerators("InlineLibrary", """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace InlineLibrary;

        public static class InlineTexts
        {
            public static string Orders => LocalizedText.Inline(en: "Orders", nl: "Bestellingen");
        }
        """);

    /// <summary>
    /// Two default-language sets of one name in two namespaces, so two contracts named <c>IShopLocalization</c>, and
    /// neither has a Dutch set yet.
    /// </summary>
    private const string TwoShops = """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace Shop.Checkout
        {
            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Pay => "Pay";
            }
        }

        namespace Shop.Admin
        {
            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Delete => "Delete";
            }
        }
        """;

    private static GeneratorDriverRunResult Run(string source, params MetadataReference[] references)
    {
        return GeneratorTestHelper.Run(LocalizationParts.Sets(), source, references);
    }

    private static string ProvidedSets(GeneratorDriverRunResult result)
    {
        var run = result.Results.ShouldHaveSingleItem();
        run.Exception.ShouldBeNull();

        return run.GeneratedSources.Single(source => source.HintName is "LocalizationSets.g.cs").SourceText.ToString();
    }

    private static string ContractText(GeneratorDriverRunResult result)
    {
        var run = result.Results.ShouldHaveSingleItem();
        run.Exception.ShouldBeNull();

        return run.GeneratedSources.Single(source => source.HintName.EndsWith(".Localization.g.cs", StringComparison.Ordinal)).SourceText.ToString();
    }

    /// <summary>
    /// The values the contract marks as shown on one of its methods (<c>[ShownValue]</c>), in the order it writes them.
    /// </summary>
    private static string[] ShownValues(string contractText, string method)
    {
        const string prefix = "    [CodeChops.Idioma.Internals.ShownValue(\"";
        const string suffix = "\")]";

        var lines = contractText.Split('\n').Select(static line => line.TrimEnd('\r')).ToArray();
        var index = Array.FindIndex(lines, line => line.StartsWith($"    string {method}(", StringComparison.Ordinal));
        index.ShouldBeGreaterThanOrEqualTo(0, customMessage: method);

        var shown = new List<string>();

        for (var i = index - 1; i >= 0 && lines[i].StartsWith(prefix, StringComparison.Ordinal) && lines[i].EndsWith(suffix, StringComparison.Ordinal); i--)
            shown.Insert(0, lines[i][prefix.Length..^suffix.Length]);

        return [.. shown];
    }

    private static string[] DiagnosticIds(GeneratorDriverRunResult result)
    {
        var run = result.Results.ShouldHaveSingleItem();
        run.Exception.ShouldBeNull();

        return run.Diagnostics.Select(diagnostic => diagnostic.Id).ToArray();
    }

    private static Diagnostic SingleDiagnostic(GeneratorDriverRunResult result, string id)
    {
        var run = result.Results.ShouldHaveSingleItem();
        run.Exception.ShouldBeNull();

        var diagnostic = run.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(id);

        return diagnostic;
    }
    #endregion

    [Test]
    public void Members_ShouldFollowTheOrderTheyAreWrittenIn()
    {
        // Arrange & Act: not alphabetical, so an order by name would fail this.
        var text = ContractText(Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Zeta => "Zeta";
                public string Alpha => "Alpha";
                public string Mid(int count) => $"Mid {count}";
            }
            """));

        // Assert
        text.IndexOf("string Zeta", StringComparison.Ordinal).ShouldBeLessThan(text.IndexOf("string Alpha", StringComparison.Ordinal));
        text.IndexOf("string Alpha", StringComparison.Ordinal).ShouldBeLessThan(text.IndexOf("string Mid", StringComparison.Ordinal));
    }

    [Test]
    public Task DefaultSet_ShouldEmitContractAndTexts()
    {
        // Arrange & Act
        var result = Run(ShopSets);

        // Assert
        var run = result.Results.ShouldHaveSingleItem();
        run.Diagnostics.ShouldBeEmpty();
        run.GeneratedSources.Select(source => source.HintName).ShouldBe(["TestApp.ShopLocalizationEn.Localization.g.cs", "LocalizationSets.g.cs"]);

        return Verify(ContractText(result));
    }

    [Test]
    public void GeneratedSource_ShouldNotMentionFormosa()
    {
        // Arrange & Act
        var text = ContractText(Run(ShopSets));

        // Assert: neither a name nor a license marker; the package stands on its own.
        text.ShouldNotContain("Formosa");
        text.ShouldNotContain("Obsolete");
    }

    [Test]
    public void GeneratedClass_ShouldComeBeforeTheContract()
    {
        // Arrange & Act
        var text = ContractText(Run(ShopSets));

        // Assert: the texts are what someone who opens the file looks for.
        text.IndexOf("public static partial class ShopLocalization", StringComparison.Ordinal).ShouldBeInRange(0, text.IndexOf("public interface IShopLocalization", StringComparison.Ordinal));
    }

    [Test]
    public void BaseName_ShouldDropLanguageSuffix()
    {
        // Arrange & Act & Assert: the suffix is the language in PascalCase, and counts in any letter case that starts
        // with a capital, which marks where the suffix starts: the "es" of Messages is no part of EsAr.
        LocalizationDiscovery.DeriveBaseName("ShopLocalizationEn", "en").ShouldBe("ShopLocalization");
        LocalizationDiscovery.DeriveBaseName("ShopLocalizationEN", "en").ShouldBe("ShopLocalization");
        LocalizationDiscovery.DeriveBaseName("ShopTextsEn", "en").ShouldBe("ShopTexts");
        LocalizationDiscovery.DeriveBaseName("ShopEn", "en").ShouldBe("Shop");
        LocalizationDiscovery.DeriveBaseName("ShopLocalizationZhHans", "zh-Hans").ShouldBe("ShopLocalization");
        LocalizationDiscovery.DeriveBaseName("ShopLocalizationFIL", "fil").ShouldBe("ShopLocalization");
        LocalizationDiscovery.DeriveBaseName("MessagesEsAr", "es-AR").ShouldBe("Messages");
        LocalizationDiscovery.DeriveBaseName("MessagesEsAR", "es-AR").ShouldBe("Messages");
        LocalizationDiscovery.DeriveBaseName("MessagesAr", "es-AR").ShouldBeNull();
        LocalizationDiscovery.DeriveBaseName("ShopLocalizationfil", "fil").ShouldBeNull();
        LocalizationDiscovery.DeriveBaseName("ShopLocalizationen", "en").ShouldBeNull();
        LocalizationDiscovery.DeriveBaseName("Screen", "en").ShouldBeNull();
        LocalizationDiscovery.DeriveBaseName("ShopLocalization", "en").ShouldBeNull();
        LocalizationDiscovery.DeriveBaseName("En", "en").ShouldBeNull();
    }

    [Test]
    [Arguments("ShopLocalizationEn", "IShopLocalization", "ShopLocalization")]
    [Arguments("ShopTextsEn", "IShopTexts", "ShopTexts")]
    [Arguments("LocalizationEn", "ILocalization", "Localization")]
    [Arguments("LocalizationSetEn", "ILocalizationSet", "LocalizationSet")]
    public void Names_ShouldDeriveFromClassName(string className, string contract, string texts)
    {
        // Arrange & Act: the generated class is named as the set is, without its language, a trailing "Localization"
        // included. The last set gets a contract named as the library's own interface is, so each set lists its
        // contract by its full name.
        var text = ContractText(Run($$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public sealed class {{className}} : CodeChops.Idioma.ILocalizationSet, TestApp.{{contract}}
            {
                public string Checkout => "Checkout";
            }
            """));

        // Assert
        text.ShouldContain($"public interface {contract}{Environment.NewLine}");
        text.ShouldContain($"public static partial class {texts}");
        text.ShouldContain($"public static string Checkout => GetLocalizationSet<{contract}>(0, 1)?.Checkout ?? ResolveWording<{contract}, {className}>(0, 1, \"Checkout\", static set => set.Checkout);");
    }

    [Test]
    [Arguments("namespace TestApp;")]
    [Arguments("namespace TestApp.Orders;")]
    [Arguments("namespace TestApp.@event;")]
    [Arguments("")]
    public void Text_ShouldPassItsNameAndItsPositionWhateverItsNamespace(string @namespace)
    {
        // Arrange & Act: a text is told from the others by its name alone, which is its key, in a namespace named
        // like a keyword and in the global namespace too.
        var text = ContractText(Run($$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            {{@namespace}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }
            """));

        // Assert
        text.ShouldContain("public static string Checkout => GetLocalizationSet<IShopLocalization>(0, 1)?.Checkout ?? ResolveWording<IShopLocalization, ShopLocalizationEn>(0, 1, \"Checkout\", static set => set.Checkout);");
    }

    [Test]
    public void TextsOfOneNameInSetsOfOneName_ShouldEachBeWordedByTheirOwnContract()
    {
        // Arrange & Act: two sets of one name in two namespaces, and a third whose name differs from theirs only in a
        // trailing "Localization", each with a text named Checkout.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp.Orders
            {
                public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
                {
                    public string Checkout => "Place order";
                }

                public sealed class ShopEn : ILocalizationSet, IShop
                {
                    public string Checkout => "To the shop";
                }
            }

            namespace TestApp.Billing
            {
                public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
                {
                    public string Checkout => "Pay invoice";
                }
            }
            """);

        // Assert: each text names its own contract and set, in its own namespace, so a set of one never words another.
        var generated = result.Results.ShouldHaveSingleItem().GeneratedSources;
        string Contract(string set) => generated.Single(source => String.Equals(source.HintName, $"{set}.Localization.g.cs", StringComparison.Ordinal)).SourceText.ToString();

        Contract("TestApp.Orders.ShopLocalizationEn").ShouldContain("namespace TestApp.Orders;");
        Contract("TestApp.Orders.ShopLocalizationEn").ShouldContain("ResolveWording<IShopLocalization, ShopLocalizationEn>(0, 1, \"Checkout\",");
        Contract("TestApp.Orders.ShopEn").ShouldContain("namespace TestApp.Orders;");
        Contract("TestApp.Orders.ShopEn").ShouldContain("ResolveWording<IShop, ShopEn>(0, 1, \"Checkout\",");
        Contract("TestApp.Billing.ShopLocalizationEn").ShouldContain("namespace TestApp.Billing;");
        Contract("TestApp.Billing.ShopLocalizationEn").ShouldContain("ResolveWording<IShopLocalization, ShopLocalizationEn>(0, 1, \"Checkout\",");
    }

    [Test]
    public void DefaultLanguageWithLongCode_ShouldWork()
    {
        // Arrange & Act
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("zh-Hans", "en")]

            namespace TestApp;

            public sealed class ShopLocalizationZhHans : ILocalizationSet, IShopLocalization
            {
                string ILocalizationSet.LocalizationLanguageCode => "zh-Hans";
                public string Checkout => "结账";
            }

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
        ContractText(result).ShouldContain("<see cref=\"ShopLocalizationZhHans.Checkout\">zh-Hans</see>");
    }

    [Test]
    public void OnlyPublicInstanceStringPropertiesAreTexts()
    {
        // Arrange: LocalizationLanguageCode is the ILocalizationSet member, a virtual property is a text (so a set can derive and
        // override), an inherited property belongs to the base (it is not redeclared), a property without a public
        // getter could not implement the contract member, and the rest are not texts by shape.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public class ShopWordingBase
            {
                public string Inherited => "Inherited";
            }

            public class ShopLocalizationEn : ShopWordingBase, ILocalizationSet, IShopLocalization
            {
                public string LocalizationLanguageCode => "en";
                public string Checkout => "Checkout";
                public virtual string Total => "Total";
                internal string Hidden => "Hidden";
                public string Secret { private get; set; } = "Secret";
                public static string Shared => "Shared";
                public int Count => 1;
                public string this[int index] => "";
                public string Field = "Field";
                public string Method() => "Method";
            }
            """;

        // Act
        var text = ContractText(Run(source));

        // Assert
        text.ShouldContain("    string Checkout { get; }");
        text.ShouldContain("    string Total { get; }");
        foreach (var name in new[] { "LocalizationLanguageCode", "Inherited", "Hidden", "Secret", "Shared", "Count", "Field", "Method" })
            text.ShouldNotContain($"string {name} {{ get; }}", customMessage: name);
    }

    [Test]
    public void InternalSet_ShouldEmitInternalContractAndTextsClass()
    {
        // Arrange: the generated types are the rest of the set's contract, so they are no more visible than the set.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            internal sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }
            """;

        // Act
        var result = Run(source);

        // Assert: no other assembly could implement it, so it is not listed for them.
        var text = ContractText(result);
        text.ShouldContain($"internal interface IShopLocalization{Environment.NewLine}");
        text.ShouldContain("internal static partial class ShopLocalization");
        text.ShouldNotContain("public interface");
        text.ShouldNotContain("public static partial class");
        result.Results.ShouldHaveSingleItem().GeneratedSources.ShouldNotContain(source => source.HintName == "LocalizationSets.g.cs");
    }

    [Test]
    public void Contract_ShouldBeMarkedAndNotDeriveFromILocalizationSet()
    {
        // Arrange & Act
        var text = ContractText(Run(ShopSets));

        // Assert: a set lists ILocalizationSet itself, so neither of the two it lists is redundant.
        text.ShouldContain($"[CodeChops.Idioma.Internals.LocalizationContract]{Environment.NewLine}{GeneratedFile.ImportedGeneratedCodeAttribute}{Environment.NewLine}public interface IShopLocalization{Environment.NewLine}");
        text.ShouldNotContain("IShopLocalization :");
        text.ShouldNotContain("partial class ShopLocalizationEn");
    }

    [Test]
    public void RecordSet_ShouldGetContractAndTexts()
    {
        // Arrange
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public sealed record ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }
            """;

        // Act
        var text = ContractText(Run(source));

        // Assert: the record lists its contract itself, so nothing is declared for it.
        text.ShouldContain("public interface IShopLocalization");
        text.ShouldContain("public static partial class ShopLocalization");
        text.ShouldNotContain("partial record");
        GeneratorTestHelper.CompileWithGenerators(source).ShouldBeEmpty();
    }

    [Test]
    public void SetInGlobalNamespace_ShouldEmitNoNamespaceDeclaration()
    {
        // Arrange & Act
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }
            """);

        // Assert
        var generated = result.Results.ShouldHaveSingleItem().GeneratedSources;
        var text = generated.Single(source => source.HintName is "ShopLocalizationEn.Localization.g.cs").SourceText.ToString();
        text.ShouldNotContain("namespace ");
        text.ShouldContain("ResolveWording<IShopLocalization, ShopLocalizationEn>(0, 1, \"Checkout\", static set => set.Checkout);");
        generated.Single(source => source.HintName is "LocalizationSets.g.cs").SourceText.ToString().ShouldContain("LocalizationSetAttribute(\"IShopLocalization\", \"en\")");
    }

    [Test]
    public void SetWithoutTexts_ShouldEmitEmptyContractAndTextsClass()
    {
        // Arrange & Act
        var text = ContractText(Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization;
            """));

        // Assert
        text.ShouldContain("public interface IShopLocalization");
        text.ShouldContain("public static partial class ShopLocalization");
        text.ShouldNotContain("GetLocalizationSet<");
        text.ShouldNotContain("ResolveWording<");
    }

    [Test]
    public Task SetsOfPublicContract_ShouldBeListedForOtherAssemblies()
    {
        // Arrange & Act
        var text = ProvidedSets(Run(ShopSets));

        // Assert
        return Verify(text);
    }

    [Test]
    public void GeneratedCode_ShouldCompile()
    {
        // Arrange & Act
        var errors = GeneratorTestHelper.CompileWithGenerators(ShopSets);

        // Assert
        errors.ShouldBeEmpty();
    }

    [Test]
    public void SetDerivingFromSetOfProjectReferencedAsCompilation_ShouldNotStopTheGenerator()
    {
        // Arrange: an IDE hands a generator a project of the same solution as its compilation, where a build hands over
        // the assembly. The LocalizationLanguageCode the library's set implements then has syntax, in a tree of that other
        // compilation, which this one has no semantic model for.
        var library = GeneratorTestHelper.CompileProjectWithGenerators("RegionalLibrary", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace RegionalLibrary;

            public class GridLocalizationEn : ILocalizationSet, IGridLocalization
            {
                public virtual string Colour => "Color";
            }

            public class GridLocalizationNl : ILocalizationSet, IGridLocalization
            {
                public virtual string Colour => "Kleur";
            }

            public class GridLocalizationEnGb : GridLocalizationEn, ILocalizationSet
            {
                string ILocalizationSet.LocalizationLanguageCode => "en-GB";

                public override string Colour => "Colour";
            }
            """);

        // Act
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public sealed class MyGridLocalizationEnGb : RegionalLibrary.GridLocalizationEnGb
            {
                public override string Colour => "Hue";
            }
            """, library);

        // Assert: as in a build, where the language of such a set can't be read, so nothing is held against it.
        result.Results.ShouldHaveSingleItem().Exception.ShouldBeNull();
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void File_ShouldImportTheFrameworkAndNoAlias()
    {
        // Arrange & Act
        var text = ContractText(Run(ShopSets));
        var usings = text.Split('\n').Where(static line => line.StartsWith("using ", StringComparison.Ordinal)).Select(static line => line.TrimEnd()).ToArray();

        // Assert: the namespaces of what the method that gives a text by its key names, of the attributes that mark
        // generated code, and what words a text.
        usings.ShouldBe(["using System;", "using System.Collections.Immutable;", "using System.CodeDom.Compiler;", "using System.Diagnostics.CodeAnalysis;", "using static CodeChops.Idioma.Internals.GeneratedCode;"]);
    }
}
