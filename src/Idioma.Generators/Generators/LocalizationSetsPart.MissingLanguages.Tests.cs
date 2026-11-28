using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

public sealed partial class LocalizationSetsPartTests
{
    [Test]
    public void LanguageWithoutSet_ShouldReportLOC201AndStillEmit()
    {
        // Arrange & Act
        var result = Run($$"""
            {{Header}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }
            """);

        // Assert: the error names the language and the fix, at the set; the contract still comes, so it stays the only
        // error.
        var diagnostic = SingleDiagnostic(result, LocalizationSetsPart.MissingLanguageDiagnosticId);
        diagnostic.Id.ShouldBe("LOC201");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("'ShopLocalization' has no set for 'nl'");
        diagnostic.GetMessage().ShouldContain("Add 'class ShopLocalizationNl : ILocalizationSet, IShopLocalization'.");
        diagnostic.Location.GetLineSpan().StartLinePosition.Line.ShouldBe(6);
        ContractText(result).ShouldContain("public interface IShopLocalization");
    }

    [Test]
    public void SetWithoutLanguage_ShouldNotHideMissingLanguage()
    {
        // Arrange & Act: the British set's name gives no language, so it serves none, and Dutch is missing.
        var result = Run($$"""
            {{Header}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            public sealed class ShopLocalizationBritish : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }
            """);

        // Assert
        SingleDiagnostic(result, LocalizationSetsPart.MissingLanguageDiagnosticId).GetMessage().ShouldContain("'ShopLocalization' has no set for 'nl'");
    }

    [Test]
    public void StructOfTheNameOfTheSet_ShouldBeNamedInLOC201()
    {
        // Arrange & Act: a struct isn't a set, and registering needs a class.
        var result = Run($$"""
            {{Header}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            public struct ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """);

        // Assert
        SingleDiagnostic(result, LocalizationSetsPart.MissingLanguageDiagnosticId).GetMessage().ShouldEndWith("neither does a struct, or a nested, generic, or file-local class.");
    }

    [Test]
    public void LanguageWithLongCodeWithoutSet_ShouldNameSetAfterCode()
    {
        // Arrange & Act
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl-BE")]

            namespace TestApp;

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }
            """);

        // Assert: the name alone gives a set nl-BE, as the assembly declares it.
        var diagnostic = SingleDiagnostic(result, LocalizationSetsPart.MissingLanguageDiagnosticId);
        diagnostic.GetMessage().ShouldContain("Add 'class ShopLocalizationNlBe : ILocalizationSet, IShopLocalization'.");
    }

    [Test]
    public void HandWrittenContractMissingLanguage_ShouldReportLOC201AtAttribute()
    {
        // Arrange & Act
        var result = Run($$"""
            {{Header}}

            public interface IGridLocalization : ILocalizationSet
            {
                string First { get; }
            }

            public sealed class GridLocalizationEn : ILocalizationSet, IGridLocalization
            {
                public string First => "First";
            }
            """);

        // Assert
        var diagnostic = SingleDiagnostic(result, LocalizationSetsPart.MissingLanguageDiagnosticId);
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("'TestApp.IGridLocalization' has no set for 'nl'");
        diagnostic.GetMessage().ShouldContain("Add 'class GridLocalizationNl : ILocalizationSet, IGridLocalization'.");
        diagnostic.Location.GetLineSpan().StartLinePosition.Line.ShouldBe(2);
    }

    [Test]
    public void HandWrittenContractInEveryLanguage_ShouldReportNothing()
    {
        // Arrange & Act: Dutch through a set of a derived contract, which serves the base contract too.
        var result = Run($$"""
            {{Header}}

            internal interface IGridLocalization : ILocalizationSet
            {
                string First { get; }
            }

            internal interface IPagedGridLocalization : IGridLocalization
            {
                string Next { get; }
            }

            internal sealed class GridLocalizationEn : ILocalizationSet, IPagedGridLocalization
            {
                public string First => "First";
                public string Next => "Next";
            }

            internal sealed class GridLocalizationNl : ILocalizationSet, IPagedGridLocalization
            {
                public string First => "Eerste";
                public string Next => "Volgende";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void HandWrittenContractWithExcludedSet_ShouldCountItAsSetOfItsLanguage()
    {
        // Arrange & Act
        var result = Run($$"""
            {{Header}}

            public interface IGridLocalization : ILocalizationSet
            {
                string First { get; }
            }

            public sealed class GridLocalizationEn : ILocalizationSet, IGridLocalization
            {
                public string First => "First";
            }

            [ExcludeFromLocalizationDiscovery]
            public sealed class GridLocalizationNl(string first) : ILocalizationSet, IGridLocalization
            {
                public string First => first;
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    [Arguments("public", "partial ")]
    [Arguments("public", "")]
    [Arguments("internal", "")]
    public void SetsListingILocalizationSetBesideHandWrittenContract_ShouldReportNothingAndEmitNoContract(string accessibility, string partial)
    {
        // Arrange & Act: listing ILocalizationSet again adds nothing to a set of a contract written by hand. The English set
        // is no default-language set to generate a second contract from (LOC200, or a duplicate IGridLocalization once
        // partial), and the Dutch set does implement its contract (LOC205).
        var result = Run($$"""
            {{Header}}

            {{accessibility}} interface IGridLocalization : ILocalizationSet
            {
                string Next { get; }
            }

            {{accessibility}} {{partial}}class GridLocalizationEn : IGridLocalization, ILocalizationSet
            {
                public string Next => "Next";
            }

            {{accessibility}} sealed class GridLocalizationNl : IGridLocalization, ILocalizationSet
            {
                public string Next => "Volgende";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
        result.Results.ShouldHaveSingleItem().GeneratedSources.ShouldNotContain(source => source.HintName.EndsWith(".Localization.g.cs", StringComparison.Ordinal));
    }

    [Test]
    public void SetsListingILocalizationSetBesideHandWrittenContractWithoutDeclaredLanguages_ShouldReportNothing()
    {
        // Arrange & Act: they start no contract, so they need no languages declared (LOC100).
        var result = Run("""
            using CodeChops.Idioma;

            namespace TestApp;

            public interface IGridLocalization : ILocalizationSet
            {
                string Next { get; }
            }

            public sealed class GridLocalizationEn : IGridLocalization, ILocalizationSet
            {
                public string Next => "Next";
            }

            public sealed class GridLocalizationNl : IGridLocalization, ILocalizationSet
            {
                public string Next => "Volgende";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void EveryLanguageMissing_ShouldReportEach()
    {
        // Arrange & Act
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]

            namespace TestApp;

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization;
            """);

        // Assert
        DiagnosticIds(result).ShouldBe(["LOC201", "LOC201"]);
    }

    [Test]
    public void ContractsOfOneNameInTwoNamespaces_ShouldEachNeedTheirOwnLanguages()
    {
        // Arrange & Act: the Dutch set implements the checkout's contract, which leaves the admin's without Dutch.
        var result = Run($$"""
            {{TwoShops}}

            namespace Shop.Checkout
            {
                public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
                {
                    public string Pay => "Betalen";
                }
            }
            """);

        // Assert
        SingleDiagnostic(result, LocalizationSetsPart.MissingLanguageDiagnosticId);
        var provided = ProvidedSets(result);
        provided.ShouldContain("LocalizationSetAttribute(\"Shop.Checkout.IShopLocalization\", \"nl\", ");
        provided.ShouldNotContain("LocalizationSetAttribute(\"Shop.Admin.IShopLocalization\", \"nl\", ");
        var admin = result.Results.Single().GeneratedSources.Single(source => source.HintName is "Shop.Admin.ShopLocalizationEn.Localization.g.cs");
        admin.SourceText.ToString().ShouldNotContain("ShopLocalizationNl");
    }

    [Test]
    [Arguments("namespace Shop.Checkout;\npublic sealed class ShopLocalizationNl : CodeChops.Idioma.ILocalizationSet, IShopLocalization;", "Shop.Checkout")]
    [Arguments("namespace Shop.Admin.Back;\npublic sealed class ShopLocalizationNl : CodeChops.Idioma.ILocalizationSet, IShopLocalization;", "Shop.Admin")]
    [Arguments("using Shop.Admin;\nnamespace Shop.Checkout;\npublic sealed class ShopLocalizationNl : CodeChops.Idioma.ILocalizationSet, IShopLocalization;", "Shop.Checkout")]
    [Arguments("using Shop.Admin;\nnamespace Other;\npublic sealed class ShopLocalizationNl : CodeChops.Idioma.ILocalizationSet, IShopLocalization;", "Shop.Admin")]
    [Arguments("namespace Other\n{\n    using Shop.Admin;\n    public sealed class ShopLocalizationNl : CodeChops.Idioma.ILocalizationSet, IShopLocalization;\n}", "Shop.Admin")]
    [Arguments("namespace Other;\npublic sealed class ShopLocalizationNl : CodeChops.Idioma.ILocalizationSet, Shop.Admin.IShopLocalization;", "Shop.Admin")]
    [Arguments("namespace Shop;\npublic sealed class ShopLocalizationNl : CodeChops.Idioma.ILocalizationSet, Admin.IShopLocalization;", "Shop.Admin")]
    [Arguments("namespace Other;\npublic sealed class ShopLocalizationNl : CodeChops.Idioma.ILocalizationSet, Shop.Admin.IShopLocalization;", "Shop.Admin")]
    public void SetImplementingGeneratedContract_ShouldCountForContractTheCompilerBinds(string dutchSet, string expectedNamespace)
    {
        // Arrange: the contract does not exist yet while the generator runs; a type of the containing namespaces wins
        // over one a using imports.
        var otherNamespace = expectedNamespace is "Shop.Admin" ? "Shop.Checkout" : "Shop.Admin";

        // Act
        var result = GeneratorTestHelper.RunOverFiles(LocalizationParts.Sets(), TwoShops, dutchSet);

        // Assert
        var provided = ProvidedSets(result);
        provided.ShouldContain($"LocalizationSetAttribute(\"{expectedNamespace}.IShopLocalization\", \"nl\", ");
        provided.ShouldNotContain($"LocalizationSetAttribute(\"{otherNamespace}.IShopLocalization\", \"nl\", ");
        DiagnosticIds(result).ShouldBe(["LOC201"]);
    }

    [Test]
    public void SetImplementingGeneratedContractThroughGlobalUsing_ShouldCount()
    {
        // Arrange & Act: the global using lives in a file of its own, as an SDK's implicit usings do.
        var result = GeneratorTestHelper.RunOverFiles(
            LocalizationParts.Sets(),
            TwoShops,
            "global using Shop.Admin;",
            "namespace Other;\npublic sealed class ShopLocalizationNl : CodeChops.Idioma.ILocalizationSet, IShopLocalization;");

        // Assert
        var provided = ProvidedSets(result);
        provided.ShouldContain("LocalizationSetAttribute(\"Shop.Admin.IShopLocalization\", \"nl\", ");
        provided.ShouldNotContain("LocalizationSetAttribute(\"Shop.Checkout.IShopLocalization\", \"nl\", ");
    }

    [Test]
    public void TextMissingFromSet_ShouldNotCompile()
    {
        // Arrange & Act
        var errors = GeneratorTestHelper.CompileWithGenerators($$"""
            {{Header}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
                public string Total => "Total";
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """);

        // Assert
        errors.ShouldHaveSingleItem().Id.ShouldBe("CS0535");
    }

    [Test]
    public void ListedRegion_ShouldNeedSetOfItsOwn()
    {
        // Arrange & Act: at run time en-GB resolves through en. Listing it says it has wording of its own, as every
        // inline text takes one, so the English set does not stand in for it.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "en-GB")]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Cart => "Cart";
            }

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public virtual string Cart => "Winkelwagen";
            }
            """);

        // Assert: the set to add derives from the English one, so that it holds only the texts that differ.
        var diagnostic = SingleDiagnostic(result, LocalizationSetsPart.MissingLanguageDiagnosticId);
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldBe("'ShopLocalization' has no set for 'en-GB', which [assembly: SupportedLanguages] requires. Add 'class ShopLocalizationEnGb : ShopLocalizationEn', which keeps the wording of 'en' for every text it doesn't override. A class without ILocalizationSet doesn't count as a set, and neither does a struct, or a nested, generic, or file-local class.");
    }

    [Test]
    [Arguments("public class ShopLocalizationEnGb : ShopLocalizationEn;")]
    [Arguments("public class ShopLocalizationEnGb : ShopLocalizationEn { public override string Cart => \"Basket\"; }")]
    [Arguments("public class ShopLocalizationEnGb : ILocalizationSet, IShopLocalization { public string Cart => \"Basket\"; }")]
    [Arguments("public class ShopLocalizationENGB : ShopLocalizationEn;")]
    public void ListedRegionWithSetOfItsOwn_ShouldReportNothing(string british)
    {
        // Arrange & Act: a set that derives from the English one and adds nothing will do, as will one that implements
        // the contract itself, or one whose name gives the code in capitals.
        var result = Run($$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "en-GB")]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Cart => "Cart";
            }

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public virtual string Cart => "Winkelwagen";
            }

            {{british}}
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    [Arguments("\"nl\", \"en\", \"en-GB\"", "public sealed class ShopLocalizationEn", "Add 'class ShopLocalizationEnGb : ILocalizationSet, IShopLocalization'. A class without ILocalizationSet doesn't count as a set, and neither does a struct, or a nested, generic, or file-local class.")]
    [Arguments("\"nl\", \"en\", \"en-GB\"", "public record ShopLocalizationEn", "Add 'record ShopLocalizationEnGb : ShopLocalizationEn', which keeps the wording of 'en' for every text it doesn't override. A class without ILocalizationSet doesn't count as a set, and neither does a struct, or a nested, generic, or file-local class.")]
    [Arguments("\"nl\", \"en-GB\"", "public class ShopLocalizationEn", "Add 'class ShopLocalizationEnGb : ShopLocalizationEn', which keeps the wording of 'en' for every text it doesn't override. A class without ILocalizationSet doesn't count as a set, and neither does a struct, or a nested, generic, or file-local class.")]
    public void ListedRegionWithoutSet_ShouldBeToldWhichSetToAdd(string languages, string english, string advice)
    {
        // Arrange & Act: here the English set is not the default one. A sealed set has nothing deriving from it, a record
        // only a record, and a set of a language that is not listed itself still gives the region its wording.
        var result = Run($$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages({{languages}})]

            namespace TestApp;

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public virtual string Cart => "Winkelwagen";
            }

            {{english}} : ILocalizationSet, IShopLocalization
            {
                public virtual string Cart => "Cart";
            }
            """);

        // Assert
        var message = SingleDiagnostic(result, LocalizationSetsPart.MissingLanguageDiagnosticId).GetMessage();
        message.ShouldContain("has no set for 'en-GB'");
        message.ShouldEndWith(advice);
    }

    [Test]
    public void ListedRegionOfRegion_ShouldBeToldToDeriveFromNearestSet()
    {
        // Arrange & Act: zh-Hans-CN has both the set of zh and the set of zh-Hans above it.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "zh", "zh-Hans", "zh-Hans-CN")]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Cart => "Cart";
            }

            public class ShopLocalizationZh : ILocalizationSet, IShopLocalization
            {
                public virtual string Cart => "购物车";
            }

            public class ShopLocalizationZhHans : ShopLocalizationZh;
            """);

        // Assert
        var message = SingleDiagnostic(result, LocalizationSetsPart.MissingLanguageDiagnosticId).GetMessage();
        message.ShouldEndWith("Add 'class ShopLocalizationZhHansCn : ShopLocalizationZhHans', which keeps the wording of 'zh-Hans' for every text it doesn't override. A class without ILocalizationSet doesn't count as a set, and neither does a struct, or a nested, generic, or file-local class.");
    }

    [Test]
    public void HandWrittenContractMissingListedRegion_ShouldReportLOC201()
    {
        // Arrange & Act: a contract written by hand is held to its languages as a generated one is.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "en-GB")]

            namespace TestApp;

            public interface IGridLocalization : ILocalizationSet
            {
                string First { get; }
            }

            public class GridLocalizationEn : ILocalizationSet, IGridLocalization
            {
                public virtual string First => "First";
            }

            public class GridLocalizationNl : ILocalizationSet, IGridLocalization
            {
                public virtual string First => "Eerste";
            }
            """);

        // Assert
        var message = SingleDiagnostic(result, LocalizationSetsPart.MissingLanguageDiagnosticId).GetMessage();
        message.ShouldContain("'TestApp.IGridLocalization' has no set for 'en-GB'");
        message.ShouldEndWith("Add 'class GridLocalizationEnGb : GridLocalizationEn', which keeps the wording of 'en' for every text it doesn't override. A class without ILocalizationSet doesn't count as a set, and neither does a struct, or a nested, generic, or file-local class.");
    }
}
