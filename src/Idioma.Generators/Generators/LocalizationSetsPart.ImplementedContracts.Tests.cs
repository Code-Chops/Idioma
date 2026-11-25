using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

public sealed partial class LocalizationSetsPartTests
{
    [Test]
    public void DefaultSetWithoutItsContract_ShouldReportLOC204AndStillGenerateIt()
    {
        // Arrange & Act: the shape a default-language set had while the generator declared its contract for it.
        var result = Run($$"""
            {{Header}}

            public partial class ShopLocalizationEn : ILocalizationSet
            {
                public string Checkout => "Checkout";
                public string Total => "Total";
            }

            {{DutchSet}}
            """);

        // Assert: the other languages keep compiling against the contract meanwhile.
        var diagnostic = SingleDiagnostic(result, LocalizationSetsPart.MissingContractDiagnosticId);
        diagnostic.GetMessage().ShouldBe("'ShopLocalizationEn' is a set for 'en' but doesn't implement 'IShopLocalization', so no text reads it. Add 'IShopLocalization', the contract generated from 'ShopLocalizationEn', next to ILocalizationSet.");
        diagnostic.Properties[LocalizationSetsPart.SuggestedNameProperty].ShouldBe("IShopLocalization");
        ContractText(result).ShouldContain("public interface IShopLocalization");
        ContractText(result).ShouldNotContain("partial class ShopLocalizationEn");
    }

    [Test]
    public void DefaultSetWithoutItsContract_ShouldLeaveLOC204TheOnlyErrorOfTheBuild()
    {
        // Arrange & Act: a property and a method, and a text named like a member of a class, which the class that
        // implements the contract for the set mustn't clash with.
        var errors = GeneratorTestHelper.CompileWithGenerators("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public sealed class ShopLocalizationEn : ILocalizationSet
            {
                public string Checkout => "Checkout";
                public string ToString(int count) => $"{count} items";
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
                public string ToString(int count) => $"{count} artikelen";
            }

            public static class Page
            {
                public static string Title => ShopLocalization.Checkout + ShopLocalization.ToString(2);
            }
            """);

        // Assert: no error in the generated file, where nothing can be fixed.
        errors.Select(static error => error.Id).ShouldBe([LocalizationSetsPart.MissingContractDiagnosticId]);
    }

    [Test]
    public void ClassImplementingContractWithoutILocalizationSet_ShouldNotCountAsSet()
    {
        // Arrange & Act: the shape every other language's set had while the contract derived from ILocalizationSet.
        var result = Run($$"""
            {{Header}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            public sealed class ShopLocalizationNl : IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """);

        // Assert: it isn't the Dutch set, so Dutch is missing, and the message says why the class doesn't count.
        var message = SingleDiagnostic(result, LocalizationSetsPart.MissingLanguageDiagnosticId).GetMessage();
        message.ShouldContain("Add 'class ShopLocalizationNl : ILocalizationSet, IShopLocalization'.");
        message.ShouldEndWith("A class without ILocalizationSet doesn't count as a set, and neither does a struct, or a nested, generic, or file-local class.");
    }

    [Test]
    [Arguments("ShopLocalizationFake")]   // Its name ends in 'ke', a language that isn't listed
    [Arguments("ShopLocalizationScreen")] // Its name ends in 'en', the default language
    public void TestDoubleImplementingContract_ShouldNotCountAsSet(string name)
    {
        // Arrange & Act
        var result = Run($$"""
            {{Header}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }

            public sealed class {{name}} : IShopLocalization
            {
                public string Checkout => "Test";
            }
            """);

        // Assert: no language is read from its name, and neither the texts nor the recorded sets name it.
        DiagnosticIds(result).ShouldBeEmpty();
        ContractText(result).ShouldNotContain(name);
        RecordedSets(result).ShouldNotContain(name);
    }

    [Test]
    public void ClassImplementingLibraryContractWithoutILocalizationSet_ShouldReportLOC700()
    {
        // Arrange & Act: the library's contract is marked, and doesn't derive from ILocalizationSet.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]

            namespace TestApp;

            public sealed class GridLocalizationDe : GridLibrary.IGridLocalization
            {
                public string First => "Erste";
            }
            """, GridLibrary);

        // Assert: it isn't the German set, so the library lacks German.
        SingleDiagnostic(result, LocalizationSetsPart.LibraryMissingLanguageDiagnosticId).GetMessage().ShouldContain("Add 'class GridLocalizationDe : ILocalizationSet, GridLibrary.IGridLocalization'.");
    }

    [Test]
    [Arguments(false, "")]
    [Arguments(false, """[assembly: SupportedLanguages("en", "nl", "de")]""")]
    [Arguments(true, "")]
    [Arguments(true, """[assembly: SupportedLanguages("en", "nl", "de")]""")]
    public void SetOfInternalContractOfLibrary_ShouldTranslateIt(bool asProject, string languages)
    {
        // Arrange: the library's sets are internal, so its contract is too, and only the test project sees it.
        const string librarySource = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]
            [assembly: System.Runtime.CompilerServices.InternalsVisibleTo("TestAssembly")]

            namespace ShopLibrary;

            internal sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            internal sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """;

        var library = asProject
            ? GeneratorTestHelper.CompileProjectWithGenerators("ShopLibrary", librarySource)
            : GeneratorTestHelper.CompileLibraryWithGenerators("ShopLibrary", librarySource);

        // Act: a set the test passes to LocalizationRegistry.Create, which takes only an ILocalizationSet.
        var result = Run($$"""
            using CodeChops.Idioma;

            {{languages}}

            namespace TestApp;

            internal sealed class ShopLocalizationDe : ILocalizationSet, ShopLibrary.IShopLocalization
            {
                public string Checkout => "Zur Kasse";
            }
            """, library);

        // Assert: it translates the library's contract, so it needs no languages (LOC100), and lists its contract (LOC204).
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void SetTranslatingLibraryContract_ShouldNotReportLOC200()
    {
        // Arrange & Act: a set of the library's contract in the default language, listing nothing else.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public sealed class GridLocalizationEn : ILocalizationSet, GridLibrary.IGridLocalization
            {
                public string First => "First";
            }
            """, GridLibrary);

        // Assert
        DiagnosticIds(result).Contains(LocalizationSetsPart.CannotExtendDiagnosticId, StringComparer.Ordinal).ShouldBeFalse();
    }

    [Test]
    public void SetListingILocalizationSetInOtherLanguage_ShouldReportLOC204Only()
    {
        // Arrange & Act: it compiles, and no text would ever read it.
        var result = Run($$"""
            {{Header}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            public sealed class ShopLocalizationNl : ILocalizationSet
            {
                public string Checkout => "Afrekenen";
            }
            """);

        // Assert
        var diagnostic = SingleDiagnostic(result, LocalizationSetsPart.MissingContractDiagnosticId);
        diagnostic.Id.ShouldBe("LOC204");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("Add 'IShopLocalization', the contract generated from 'ShopLocalizationEn', next to ILocalizationSet.");
    }
}
