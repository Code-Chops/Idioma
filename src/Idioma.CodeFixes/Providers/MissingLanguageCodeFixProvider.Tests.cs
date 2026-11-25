using System.Collections.Immutable;
using CodeChops.Idioma.CodeFixes;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="MissingLanguageCodeFixProvider"/>: a contract without a set for one of the languages
/// (<c>LOC201</c>) gets that set in a file of its own, without texts, so the compiler names each text it lacks.
/// </summary>
public sealed class MissingLanguageCodeFixProviderTests
{
    #region Fixture
    private static string Source(string languages, string accessibility = "public") => $$"""
        using CodeChops.Idioma;

        [assembly: SupportedLanguages({{languages}})]

        namespace TestApp;

        {{accessibility}} class ShopLocalizationEn : ILocalizationSet, IShopLocalization
        {
            public virtual string Checkout => "Checkout";
        }

        {{accessibility}} class ShopLocalizationNl : ILocalizationSet, IShopLocalization
        {
            public string Checkout => "Afrekenen";
        }
        """;

    /// <summary>
    /// A library with an English set alone, which an app that supports German adds a set to.
    /// </summary>
    private static MetadataReference ShopLibrary { get; } = GeneratorTestHelper.CompileLibraryWithGenerators("MissingLanguageFixShop", """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en")]

        namespace MissingLanguageFixShop;

        public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
        {
            public virtual string Checkout => "Checkout";
        }
        """);

    /// <summary>
    /// A library with a set of its enum in English alone, which an app that supports German adds a set to.
    /// </summary>
    private static MetadataReference StatusLibrary { get; } = GeneratorTestHelper.CompileLibraryWithGenerators("MissingLanguageFixStatus", """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en")]

        namespace MissingLanguageFixStatus;

        public enum OrderStatus { Pending }

        public class OrderStatusLocalizationEn : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
        {
            public virtual string Pending => "Pending";
        }
        """);

    /// <summary>
    /// The errors of the files of one project as a build compiles them, with the generators run over them.
    /// </summary>
    private static ImmutableArray<Diagnostic> CompileFilesWithGenerators(MetadataReference[] references, params string[] sources)
    {
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            sources.Select(static source => CSharpSyntaxTree.ParseText(source, GeneratorTestHelper.ParseOptions)),
            [.. GeneratorTestHelper.References, .. references],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorTestHelper.CreateFullDriver(GeneratorTestHelper.ParseOptions).RunGeneratorsAndUpdateCompilation(compilation, out var updated, out var diagnostics);

        return [.. diagnostics.Concat(updated.GetDiagnostics()).Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Error)];
    }

    private static void ShouldReportNothing(params string[] sources)
    {
        var run = GeneratorTestHelper.RunOverFiles(LocalizationParts.Sets(), sources).Results.ShouldHaveSingleItem();

        run.Diagnostics.ShouldBeEmpty();
    }
    #endregion

    [Test]
    [Arguments("public")]
    [Arguments("internal")]
    public async Task MissingLanguage_ShouldGetASetThatImplementsTheContract(string accessibility)
    {
        // Arrange
        var source = Source("\"en\", \"nl\", \"de\"", accessibility);

        // Act
        var fixes = await CodeFixTestHelper.FixWithAddedFilesAsync(new MissingLanguageCodeFixProvider(), LocalizationParts.Sets(), source);

        // Assert: a file of its own next to the default-language set, as accessible as it, and the language is no longer
        // missing. Its texts are the compiler's to name (CS0535), not the generator's.
        var fix = fixes.ShouldHaveSingleItem();
        fix.Title.ShouldBe("Add 'ShopLocalizationDe'");
        fix.Sources.ShouldHaveSingleItem().ShouldBe(source);

        // The path has the separators of the platform, as the folder of a real file has.
        var (path, text) = fix.Added.ShouldHaveSingleItem();
        path.Replace(oldChar: '\\', newChar: '/').ShouldBe("/TestAssembly/ShopLocalizationDe.cs");
        text.ShouldBe($$"""
            using CodeChops.Idioma;

            namespace TestApp;

            {{accessibility}} class ShopLocalizationDe : ILocalizationSet, IShopLocalization
            {
            }

            """);

        ShouldReportNothing(source, text);
    }

    [Test]
    public async Task MissingRegion_ShouldGetASetDerivingFromItsLanguage()
    {
        // Arrange
        var source = Source("\"en\", \"nl\", \"en-GB\"");

        // Act
        var fixes = await CodeFixTestHelper.FixWithAddedFilesAsync(new MissingLanguageCodeFixProvider(), LocalizationParts.Sets(), source);

        // Assert: it keeps the English wording for every text it doesn't override, and needs no using of its own.
        var (_, text) = fixes.ShouldHaveSingleItem().Added.ShouldHaveSingleItem();
        text.ShouldBe("""
            namespace TestApp;

            public class ShopLocalizationEnGb : ShopLocalizationEn
            {
            }

            """);

        ShouldReportNothing(source, text);
    }

    [Test]
    public async Task GlobalUsingOfThePackage_ShouldLeaveTheUsingOut()
    {
        // Arrange
        var source = Source("\"en\", \"nl\", \"de\"");

        // Act
        var fixes = await CodeFixTestHelper.FixWithAddedFilesAsync(new MissingLanguageCodeFixProvider(), LocalizationParts.Sets(), source,
            "global using CodeChops.Idioma;");

        // Assert
        fixes.ShouldHaveSingleItem().Added.ShouldHaveSingleItem().Text.ShouldStartWith("namespace TestApp;");
    }

    [Test]
    public async Task FixAll_ShouldAddASetPerMissingLanguage()
    {
        // Arrange: German and French are missing, and British English is a region of a language that has a set.
        var source = Source("\"en\", \"nl\", \"de\", \"fr\", \"en-GB\"");

        // Act
        var (sources, added) = await CodeFixTestHelper.FixAllWithAddedFilesAsync(new MissingLanguageCodeFixProvider(), LocalizationParts.Sets(), [], source);

        // Assert: a file each, and no language is missing after it.
        sources.ShouldHaveSingleItem().ShouldBe(source);
        added.Select(static file => Path.GetFileName(file.Path)).ShouldBe(["ShopLocalizationDe.cs", "ShopLocalizationFr.cs", "ShopLocalizationEnGb.cs"], ignoreOrder: true);
        added.Single(static file => file.Path.EndsWith("ShopLocalizationEnGb.cs", StringComparison.Ordinal)).Text.ShouldContain("public class ShopLocalizationEnGb : ShopLocalizationEn");

        ShouldReportNothing([source, .. added.Select(static file => file.Text)]);
    }

    [Test]
    public async Task LibraryMissingLanguage_ShouldGetASetInTheNamespaceOfTheFile()
    {
        // Arrange: the app supports German, which the library has no set for.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "de")]

            namespace TestApp.Localization;

            public static class Marker;
            """;

        // Act
        var fixes = await CodeFixTestHelper.FixWithAddedFilesAsync(new MissingLanguageCodeFixProvider(), LocalizationParts.Sets(), [ShopLibrary], source);

        // Assert: the set the message names, next to the file that lists the languages. It names the contract with a
        // using of its namespace.
        var fix = fixes.ShouldHaveSingleItem();
        fix.Title.ShouldBe("Add 'ShopLocalizationDe'");

        var (path, text) = fix.Added.ShouldHaveSingleItem();
        path.Replace(oldChar: '\\', newChar: '/').ShouldBe("/TestAssembly/ShopLocalizationDe.cs");
        text.ShouldBe("""
            using CodeChops.Idioma;
            using MissingLanguageFixShop;

            namespace TestApp.Localization;

            public class ShopLocalizationDe : ILocalizationSet, IShopLocalization
            {
            }

            """);

        // The library's language is no longer missing. The texts the set lacks are the compiler's to name (CS0535).
        var run = GeneratorTestHelper.RunOverFiles(LocalizationParts.Sets(), [ShopLibrary], source, text).Results.ShouldHaveSingleItem();
        run.Diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task LibraryMissingLanguage_FileWithoutNamespace_ShouldGetASetInTheGlobalNamespace()
    {
        // Arrange: the test project has no default namespace.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "de")]
            """;

        // Act
        var fixes = await CodeFixTestHelper.FixWithAddedFilesAsync(new MissingLanguageCodeFixProvider(), LocalizationParts.Sets(), [ShopLibrary], source);

        // Assert
        fixes.ShouldHaveSingleItem().Added.ShouldHaveSingleItem().Text.ShouldBe("""
            using CodeChops.Idioma;
            using MissingLanguageFixShop;

            public class ShopLocalizationDe : ILocalizationSet, IShopLocalization
            {
            }

            """);
    }

    [Test]
    public async Task LibraryMissingLanguage_NamespaceThatEndsInTheLibrarysFirstPart_ShouldNameTheLibrarysContract()
    {
        // Arrange: inside namespace Contoso.MissingLanguageFixShop, the full name MissingLanguageFixShop.IShopLocalization
        // would mean Contoso.MissingLanguageFixShop.IShopLocalization.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "de")]

            namespace Contoso.MissingLanguageFixShop;

            public static class Marker;
            """;

        // Act
        var fixes = await CodeFixTestHelper.FixWithAddedFilesAsync(new MissingLanguageCodeFixProvider(), LocalizationParts.Sets(), [ShopLibrary], source);

        // Assert: a using at the top of the file reads the library's namespace from the global namespace.
        var text = fixes.ShouldHaveSingleItem().Added.ShouldHaveSingleItem().Text;
        text.ShouldBe("""
            using CodeChops.Idioma;
            using MissingLanguageFixShop;

            namespace Contoso.MissingLanguageFixShop;

            public class ShopLocalizationDe : ILocalizationSet, IShopLocalization
            {
            }

            """);

        // The set implements the library's contract, so the only error is the text it lacks, which is the compiler's to
        // name (CS0535).
        CompileFilesWithGenerators([ShopLibrary], source, text).ShouldHaveSingleItem().Id.ShouldBe("CS0535");
    }

    [Test]
    public async Task LibraryMissingLanguageOfEnum_NamespaceThatEndsInTheLibrarysFirstPart_ShouldNameTheEnumAsTheContract()
    {
        // Arrange: inside namespace Contoso.MissingLanguageFixStatus, the full name MissingLanguageFixStatus.OrderStatus
        // would mean Contoso.MissingLanguageFixStatus.OrderStatus, as for the contract.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "de")]

            namespace Contoso.MissingLanguageFixStatus;

            public static class Marker;
            """;

        // Act
        var fixes = await CodeFixTestHelper.FixWithAddedFilesAsync(new MissingLanguageCodeFixProvider(), LocalizationParts.Sets(), [StatusLibrary], source);

        // Assert: the enum by its name alone, through the using the contract needs.
        var text = fixes.ShouldHaveSingleItem().Added.ShouldHaveSingleItem().Text;
        text.ShouldBe("""
            using CodeChops.Idioma;
            using MissingLanguageFixStatus;

            namespace Contoso.MissingLanguageFixStatus;

            public class OrderStatusLocalizationDe : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
            {
            }

            """);

        CompileFilesWithGenerators([StatusLibrary], source, text).ShouldHaveSingleItem().Id.ShouldBe("CS0535");
    }

    [Test]
    public async Task LibraryMissingLanguage_TypeOfTheContractsNameInTheAppsNamespace_ShouldNameTheContractInFull()
    {
        // Arrange: the app has an IShopLocalization of its own in the namespace the set goes to, which the name alone
        // would mean, also with a using.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "de")]

            namespace TestApp;

            public interface IShopLocalization;
            """;

        // Act
        var fixes = await CodeFixTestHelper.FixWithAddedFilesAsync(new MissingLanguageCodeFixProvider(), LocalizationParts.Sets(), [ShopLibrary], source);

        // Assert
        fixes.ShouldHaveSingleItem().Added.ShouldHaveSingleItem().Text.ShouldBe("""
            using CodeChops.Idioma;

            namespace TestApp;

            public class ShopLocalizationDe : ILocalizationSet, MissingLanguageFixShop.IShopLocalization
            {
            }

            """);
    }

    [Test]
    public async Task ClassOfThatNameWithoutILocalizationSet_ShouldGetNoFix()
    {
        // Arrange: a class that only needs ILocalizationSet. Adding a second one of its name wouldn't compile.
        var source = $$"""
            {{Source("\"en\", \"nl\", \"de\"")}}

            public class ShopLocalizationDe : IShopLocalization
            {
                public string Checkout => "Zur Kasse";
            }
            """;

        // Act
        var fixes = await CodeFixTestHelper.FixWithAddedFilesAsync(new MissingLanguageCodeFixProvider(), LocalizationParts.Sets(), source);

        // Assert
        fixes.ShouldBeEmpty();
    }
}
