using System.Reflection;
using System.Runtime.Loader;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="LocalizableTextFactoryGenerator"/>: <c>LocalizableText.Inline</c> gets one required parameter
/// per language of <c>[assembly: SupportedLanguages]</c>, so leaving one out does not compile, and an invalid language
/// gets <c>LOC010</c>; an assembly calling it is marked <c>[assembly: InlineTexts]</c>.
/// </summary>
public sealed class LocalizableTextFactoryGeneratorTests
{
    #region Fixture
    private static string Source(string languages, string body = "") => $$"""
        using CodeChops.Idioma;

        [assembly: SupportedLanguages({{languages}})]

        namespace TestApp;

        public static class Texts
        {
            {{body}}
        }
        """;

    private static GeneratorDriverRunResult Run(string source) => GeneratorTestHelper.Run(new LocalizableTextFactoryGenerator(), source);

    /// <summary>
    /// The marker for texts written inline, or <see langword="null"/> when none is emitted.
    /// </summary>
    private static string? InlineTextsMarker(GeneratorDriverRunResult result)
    {
        var run = result.Results.ShouldHaveSingleItem();
        run.Exception.ShouldBeNull();

        return run.GeneratedSources.SingleOrDefault(source => source.HintName is "InlineTexts.g.cs").SourceText?.ToString();
    }

    /// <summary>
    /// Loads an assembly from its image, to run what the generators wrote.
    /// </summary>
    private static Assembly Load(byte[] image) => new AssemblyLoadContext(name: null).LoadFromStream(new MemoryStream(image));

    /// <summary>
    /// The factory, leaving out the definition of the attribute it is marked with.
    /// </summary>
    private static string? Factory(GeneratorDriverRunResult result)
    {
        var run = result.Results.ShouldHaveSingleItem();
        run.Exception.ShouldBeNull();

        return run.GeneratedSources.SingleOrDefault(source => source.HintName is "LocalizableText.Factory.g.cs").SourceText?.ToString();
    }
    #endregion

    [Test]
    public Task Languages_ShouldBecomeRequiredParametersInOrder()
    {
        // Arrange & Act
        var text = Factory(Run(Source("\"en\", \"nl\", \"de\"")))!;

        // Assert: the first is the default wording, and every language, the default included, a translation under its
        // code.
        return Verify(text);
    }

    [Test]
    public void Factory_ShouldBeInvisibleToOtherAssemblies()
    {
        // Arrange & Act
        var text = Factory(Run(Source("\"en\", \"nl\"")))!;

        // Assert
        text.ShouldContain("[Microsoft.CodeAnalysis.EmbeddedAttribute]");
        text.ShouldContain("internal static class GeneratedLocalizableText");
    }

    [Test]
    public void OneLanguage_ShouldBecomeOneParameter()
    {
        // Arrange & Act
        var text = Factory(Run(Source("\"en\"")))!;

        // Assert
        text.ShouldContain("Inline(string en)");
        text.ShouldContain("GeneratedCode.CreateText(en, (\"en\", en));");
    }

    [Test]
    public void LanguageThatIsNoIdentifier_ShouldKeepItsCodeAsLanguage()
    {
        // Arrange & Act: a hyphen cannot be in a parameter name, and "is" (Icelandic) is a keyword.
        var text = Factory(Run(Source("\"en\", \"zh-Hans\", \"is\"")))!;

        // Assert
        text.ShouldContain("Inline(string en, string zhHans, string @is)");
        text.ShouldContain("GeneratedCode.CreateText(en, (\"en\", en), (\"zh-Hans\", zhHans), (\"is\", @is));");
        text.ShouldContain("<param name=\"is\">");
    }

    [Test]
    [Arguments("nl", "nl", "NL")]
    [Arguments("NL", "nl", "NL")]
    [Arguments("fil", "fil", "Fil")]
    [Arguments("nl-BE", "nlBE", "NlBE")]
    [Arguments("NL-be", "nlBe", "NLBe")]
    [Arguments("zh-Hans", "zhHans", "ZhHans")]
    [Arguments("sr-Latn-RS", "srLatnRS", "SrLatnRS")]
    [Arguments("es-419", "es419", "Es419")]
    public void Language_ShouldBecomeParameterNamedAsItsSetEnds(string code, string parameter, string suffix)
    {
        // Arrange & Act
        var text = Factory(Run(Source($"\"en\", \"{code}\"")))!;

        // Assert: a language reads alike in both places. The parameter is what the name of a set of the language ends
        // in, in camel case: nlBE next to ShopTranslationsNlBE.
        text.ShouldContain($"Inline(string en, string {parameter})");
        text.ShouldContain($"(\"{code}\", {parameter})");
        text.ShouldContain($"<param name=\"{parameter}\">The wording in <c>{code}</c>.</param>");
        SupportedLanguages.ToTypeSuffix(code).ShouldBe(suffix);
        parameter.ShouldBe(suffix, StringCompareShould.IgnoreCase);
    }

    [Test]
    public void RegionalLanguage_ShouldCompileInSetAndInlineText()
    {
        // Arrange & Act: nl-BE as the end of a set's name, and as an argument.
        var errors = GeneratorTestHelper.CompileWithGenerators("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "nl-BE")]

            namespace TestApp;

            public partial class ShopTranslationsEN : ITranslationSet
            {
                public virtual string Cart => "Cart";
            }

            public class ShopTranslationsNL : IShopTranslations
            {
                public virtual string Cart => "Winkelwagen";
            }

            public class ShopTranslationsNlBE : ShopTranslationsNL
            {
                public override string Cart => "Winkelkar";
            }

            public static class Texts
            {
                public static LocalizableText Greeting { get; } = LocalizableText.Inline(en: "Hello, {0}!", nl: "Hallo, {0}!", nlBE: "Dag, {0}!");
            }
            """);

        // Assert
        errors.ShouldBeEmpty();
    }

    [Test]
    public void DefaultLanguageServedByOtherLanguage_ShouldShowItsOwnWording()
    {
        // Arrange: en-GB is the default, and en, which en-GB resolves through, is a translation.
        var assembly = Load(GeneratorTestHelper.EmitLibraryWithGenerators("RegionalDefault", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en-GB", "en")]

            namespace RegionalDefault;

            public static class Texts
            {
                public static LocalizableText Colour { get; } = LocalizableText.Inline(enGB: "Colour", en: "Color");
            }
            """));

        // Act
        var text = (LocalizableText)assembly.GetType("RegionalDefault.Texts")!.GetProperty("Colour")!.GetValue(null)!;

        // Assert: a culture neither serves gets the default wording, in an app without a default language of its own,
        // and the app's default language otherwise (this test project declares en).
        text.ToString(CultureInfo.GetCultureInfo("en-GB")).ShouldBe("Colour");
        text.ToString(CultureInfo.GetCultureInfo("en-US")).ShouldBe("Color");
        text.ToString(CultureInfo.GetCultureInfo("fr-FR"), registry: null, appLanguages: []).ShouldBe("Colour");
        text.ToString(CultureInfo.GetCultureInfo("fr-FR")).ShouldBe("Color");
    }

    [Test]
    public void NoAttribute_ShouldEmitNoFactory()
    {
        // Arrange & Act
        var text = Factory(Run("namespace TestApp; public static class Texts;"));

        // Assert
        text.ShouldBeNull();
    }

    [Test]
    public void EveryLanguage_ShouldCompile()
    {
        // Arrange & Act
        var errors = GeneratorTestHelper.CompileWithGenerators(Source("\"en\", \"nl\"", """public static LocalizableText Orders { get; } = LocalizableText.Inline(en: "Orders", nl: "Bestellingen");"""));

        // Assert
        errors.ShouldBeEmpty();
    }

    [Test]
    public void LanguageLeftOut_ShouldNotCompile()
    {
        // Arrange & Act
        var errors = GeneratorTestHelper.CompileWithGenerators(Source("\"en\", \"nl\"", """public static LocalizableText Orders { get; } = LocalizableText.Inline(en: "Orders");"""));

        // Assert: the error names the language.
        var error = errors.ShouldHaveSingleItem();
        error.Id.ShouldBe("CS7036");
        error.GetMessage().ShouldContain("'nl'");
    }

    [Test]
    public void Factory_ShouldBeOnLocalizableTextAlone()
    {
        // Arrange & Act: ResourceText derives from LocalizableText, so a factory written for LocalizableText as such would
        // show on ResourceText too, and give a LocalizableText under the name of another type.
        var errors = GeneratorTestHelper.CompileWithGenerators(Source("\"en\", \"nl\"", """public static LocalizableText Orders { get; } = ResourceText.Inline(en: "Orders", nl: "Bestellingen");"""));

        // Assert: ResourceText is not its own IInlineText, which the factory is written for.
        var error = errors.ShouldHaveSingleItem();
        error.Id.ShouldBe("CS0311");
        error.GetMessage().ShouldContain("ResourceText");
    }

    [Test]
    public void DocReferences_ShouldResolve()
    {
        // Arrange & Act: a build that writes a documentation file checks each cref, so an app treating warnings as
        // errors would fail on one the factory gets wrong; zh-Hans and is need a parameter name unlike their code.
        var unresolved = GeneratorTestHelper.GetUnresolvedDocReferences(Source("\"en\", \"zh-Hans\", \"is\""));

        // Assert
        unresolved.ShouldBeEmpty();
    }

    [Test]
    public void NoAttribute_ShouldLeaveNoWayToWriteTranslations()
    {
        // Arrange & Act
        var errors = GeneratorTestHelper.CompileWithGenerators("""
            using CodeChops.Idioma;

            namespace TestApp;

            public static class Texts
            {
                public static LocalizableText Orders { get; } = LocalizableText.Inline(en: "Orders", nl: "Bestellingen");
            }
            """);

        // Assert
        errors.ShouldHaveSingleItem().Id.ShouldBe("CS0117");
    }

    [Test]
    public void LibraryWithInternalsVisibleToApp_ShouldNotMakeFactoryAmbiguous()
    {
        // Arrange: both assemblies generate the factory with the same languages, and the app sees the library's internals.
        var library = GeneratorTestHelper.CompileLibraryWithGenerators("ShopLibrary", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]
            [assembly: System.Runtime.CompilerServices.InternalsVisibleTo("TestAssembly")]

            namespace ShopLibrary;

            public static class LibraryTexts
            {
                public static LocalizableText Orders { get; } = LocalizableText.Inline(en: "Orders", nl: "Bestellingen");
            }
            """);

        // Act
        var errors = GeneratorTestHelper.CompileWithGenerators(
            Source("\"en\", \"nl\"", """public static LocalizableText Orders { get; } = LocalizableText.Inline(en: "Orders", nl: "Bestellingen");"""),
            library);

        // Assert
        errors.ShouldBeEmpty();
    }

    [Test]
    [Arguments("\"en\", \"nl\", \"\"", "'' is no culture name")]
    [Arguments("\"en\", \"n\"", "'n' is no culture name")]
    [Arguments("\"en\", \"nl_BE\"", "'nl_BE' is no culture name")]
    [Arguments("\"en\", \"nl-\"", "'nl-' is no culture name")]
    [Arguments("\"en\", \"nl\", \"NL\"", "'NL' is listed twice")]
    [Arguments("\"en\", \"sr-Latn-RS\", \"sr-LatnRS\"", "'sr-LatnRS' and 'sr-Latn-RS' differ only in their hyphens")]
    [Arguments("\"en\", \"zh-Hans\", \"zhhans\"", "'zhhans' and 'zh-Hans' differ only in their hyphens")]
    [Arguments("\"en\", \"nl\", null", "null is no culture name")]
    [Arguments("\"en\", null", "null is no culture name")]
    public void InvalidLanguage_ShouldReportLOC010AndKeepValidOnes(string languages, string expected)
    {
        // Arrange & Act
        var result = Run(Source(languages));

        // Assert: the factory still comes, so this stays the only error.
        var diagnostic = result.Results.ShouldHaveSingleItem().Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(LocalizableTextFactoryGenerator.InvalidLanguagesDiagnosticId);
        diagnostic.Id.ShouldBe("LOC010");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain(expected);
        Factory(result).ShouldNotBeNull().ShouldContain("string en");
    }

    [Test]
    public void NullListOfLanguages_ShouldReportLOC010WithoutStoppingAnyGenerator()
    {
        // Arrange: null for the other languages passes no array at all, which every generator reads.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", null)]

            namespace TestApp;

            public partial class ShopTranslationsEN : ITranslationSet
            {
                public string Checkout => "Checkout";
            }

            public static class Texts
            {
                public static LocalizableText Orders { get; } = LocalizableText.Inline(en: "Orders");
                public static LocalizableText Checkout => ShopTranslations.Checkout;
            }
            """;

        // Act
        var errors = GeneratorTestHelper.CompileWithGenerators(source);

        // Assert: a generator that throws leaves its output out, and Inline or ShopTranslations missing.
        errors.Select(static error => error.Id).ShouldBe(["LOC010"]);
    }

    [Test]
    [Arguments("public static LocalizableText Orders { get; } = LocalizableText.Inline(en: \"Orders\", nl: \"Bestellingen\");")]
    [Arguments("public static LocalizableText Orders { get; } = CodeChops.Idioma.LocalizableText.Inline(en: \"Orders\", nl: \"Bestellingen\");")]
    [Arguments("public static LocalizableText Orders { get; } = CodeChops.Idioma.LocalizableText.Inline(en: \"Orders\", nl: \"Bestellingen\");")]
    public void FactoryCalledInCode_ShouldMarkAssemblyAsWritingInlineTexts(string body)
    {
        // Arrange & Act
        var marker = InlineTextsMarker(Run(Source("\"en\", \"nl\"", body)));

        // Assert
        marker.ShouldNotBeNull().ShouldContain("[assembly: CodeChops.Idioma.Internals.InlineTextsAttribute]");
    }

    [Test]
    [Arguments("Pages/Orders.razor")]
    [Arguments("Views/Orders.cshtml")]
    public void FactoryCalledInMarkup_ShouldMarkAssemblyAsWritingInlineTexts(string path)
    {
        // Arrange & Act: Razor's own generator writes the C# for markup, which no other generator sees.
        var result = GeneratorTestHelper.RunWithAdditionalFiles(
            new LocalizableTextFactoryGenerator(),
            Source("\"en\", \"nl\""),
            (path, """<h1>@(LocalizableText.Inline(en: "Orders", nl: "Bestellingen"))</h1>"""));

        // Assert
        InlineTextsMarker(result).ShouldNotBeNull();
    }

    [Test]
    public void FactoryNotCalled_ShouldLeaveAssemblyUnmarked()
    {
        // Arrange & Act: languages alone, a method of another name, and markup without a call write no inline text.
        var result = GeneratorTestHelper.RunWithAdditionalFiles(
            new LocalizableTextFactoryGenerator(),
            Source("\"en\", \"nl\"", "public static string Orders { get; } = Other.Inline(); private static class Other { public static string Inline() => \"\"; }"),
            ("Pages/Orders.razor", "<h1>@ShopTranslations.Checkout</h1>"));

        // Assert
        InlineTextsMarker(result).ShouldBeNull();
    }

    [Test]
    public void FactoryCalledWithoutLanguages_ShouldLeaveAssemblyUnmarked()
    {
        // Arrange & Act: without languages the factory does not exist, so the call is an error of its own.
        var result = Run("""
            using CodeChops.Idioma;

            namespace TestApp;

            public static class Texts
            {
                public static LocalizableText Orders { get; } = LocalizableText.Inline(en: "Orders", nl: "Bestellingen");
            }
            """);

        // Assert
        InlineTextsMarker(result).ShouldBeNull();
    }

    [Test]
    public void UnrelatedEdit_ShouldLeaveGeneratorFullyCached()
    {
        // Arrange & Act
        var reasons = GeneratorTestHelper.GetOutputReasonsAfterUnrelatedEdit(new LocalizableTextFactoryGenerator(), Source("\"en\", \"nl\""));

        // Assert
        reasons.ShouldNotBeEmpty();
        reasons.ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }

    [Test]
    public void EditInFileOfAttribute_ShouldReportInItsNewTreeAndRewriteNoSource()
    {
        // Arrange & Act: a language listed twice, reported at the attribute, an inline text, and an edit in their file
        // which moves nothing. The attribute may well sit in a file edited all day, such as Program.cs.
        var (run, edited) = GeneratorTestHelper.RunAcrossEditInSameFile(
            new LocalizableTextFactoryGenerator(),
            Source("\"en\", \"nl\", \"en\"", "public static LocalizableText Orders { get; } = LocalizableText.Inline(en: \"Orders\", nl: \"Bestellingen\");"));

        // Assert: the diagnostic lies in the tree the file has now, where a pragma of that file applies to it, and
        // neither the factory nor the marker for inline texts is written again.
        var diagnostic = run.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC010");
        diagnostic.Location.SourceTree.ShouldBeSameAs(edited);
        var reasons = GeneratorTestHelper.GetSourceOutputReasons(run);
        reasons.Length.ShouldBe(2);
        reasons.ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }
}
