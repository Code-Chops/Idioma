using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="LocalizableTextFactoryGenerator"/>: <c>LocalizableText.Inline</c> gets one required parameter
/// per language of <c>[assembly: SupportedLanguages]</c>, so leaving one out does not compile, and an invalid language
/// gets <c>LOC101</c>; an assembly calling it is marked <c>[assembly: InlineTexts]</c>. The attribute
/// <c>[Localized]</c> gets the same parameters.
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

    /// <summary>
    /// The attribute that puts a text on a member, or <see langword="null"/> when none is emitted.
    /// </summary>
    private static string? LocalizedAttribute(GeneratorDriverRunResult result)
    {
        var run = result.Results.ShouldHaveSingleItem();
        run.Exception.ShouldBeNull();

        return run.GeneratedSources.SingleOrDefault(source => source.HintName is "LocalizedAttribute.g.cs").SourceText?.ToString();
    }

    /// <summary>
    /// A class with a member marked by the given attribute.
    /// </summary>
    private static string Member(string languages, string attribute) => $$"""
        using CodeChops.Idioma;

        [assembly: SupportedLanguages({{languages}})]

        namespace TestApp;

        public class Address
        {
            {{attribute}}
            public string City { get; } = "";
        }
        """;

    /// <summary>
    /// Runs every generator over a source parsed as a version of C#, and gives the generators' diagnostics, the errors
    /// in generated files, and the names of the generated files.
    /// </summary>
    private static (ImmutableArray<Diagnostic> Diagnostics, Diagnostic[] GeneratedErrors, string[] HintNames) RunAsVersion(
        LanguageVersion version, string source)
    {
        var options = new CSharpParseOptions(version);
        var tree = CSharpSyntaxTree.ParseText(source, options);
        var compilation = CSharpCompilation.Create(
            assemblyName: "VersionApp",
            syntaxTrees: [tree],
            references: GeneratorTestHelper.References,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = GeneratorTestHelper
            .CreateFullDriver(options)
            .RunGeneratorsAndUpdateCompilation(compilation, out var updated, out var diagnostics);

        var generatedErrors = updated.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error && diagnostic.Location.SourceTree is { } at && at != tree)
            .ToArray();

        var hintNames = driver.GetRunResult().Results
            .SelectMany(static result => result.GeneratedSources).Select(static source => source.HintName).ToArray();

        return (diagnostics, generatedErrors, hintNames);
    }
    #endregion

    [Test]
    public void ProjectBelowCSharp14_ShouldReportLOC102AndWriteNoCodeForItsLanguages()
    {
        // Arrange & Act: a set, an inline text and a text on a member, each of which needs code written with C# 14.
        var (diagnostics, generatedErrors, hintNames) = RunAsVersion(LanguageVersion.CSharp13, """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }

            public class Address
            {
                [Localized(en: "City", nl: "Plaats")]
                public string City { get; } = "";

                public string Title => LocalizableText.Inline(en: "Address", nl: "Adres");
            }
            """);

        // Assert: one error that says why at the attribute, and none in generated files, as only the attribute that
        // marks embedded types is written.
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC102");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldBe("Idioma needs C# 14 or later, and this project uses C# 13.0. Remove <LangVersion> from the project file, or set it to 14 or later.");
        diagnostic.Location.GetLineSpan().StartLinePosition.Line.ShouldBe(2);
        generatedErrors.ShouldBeEmpty();
        hintNames.ShouldBe(["Microsoft.CodeAnalysis.EmbeddedAttribute.cs"]);
    }

    [Test]
    public void ProjectBelowCSharp14WithoutLanguages_ShouldBuild()
    {
        // Arrange & Act: a library that only references a library using the package declares no languages, and its
        // registrar needs no C# 14. An app declares its languages (LOC103), so it needs C# 14.
        var library = GeneratorTestHelper.CompileLibraryWithGenerators("VersionLibrary", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace VersionLibrary;

            public class GridLocalizationEn : ILocalizationSet, IGridLocalization
            {
                public virtual string First => "First";
            }

            public class GridLocalizationNl : ILocalizationSet, IGridLocalization
            {
                public string First => "Eerste";
            }
            """);

        var options = new CSharpParseOptions(LanguageVersion.CSharp12);
        var tree = CSharpSyntaxTree.ParseText("""
            public static class Reader
            {
                public static string Read() { return new VersionLibrary.GridLocalizationEn().First; }
            }
            """, options);
        var compilation = CSharpCompilation.Create("OldLibrary", [tree], [.. GeneratorTestHelper.References, library], new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorTestHelper.CreateFullDriver(options).RunGeneratorsAndUpdateCompilation(compilation, out var updated, out var diagnostics);

        // Assert
        diagnostics.ShouldBeEmpty();
        updated.GetDiagnostics().Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Error).ShouldBeEmpty();
        updated.SyntaxTrees.Count().ShouldBeGreaterThan(1);
    }

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
        text.ShouldContain("public static string Inline(string en)");
        text.ShouldContain("private static readonly string[] Languages = [\"en\"];");
        text.ShouldContain("return CodeChops.Idioma.Internals.GeneratedCode.FindLanguageIndex(Languages) switch");
        text.ShouldContain("_ => en ?? throw new System.ArgumentNullException(nameof(en)),");
        text.ShouldNotContain("1 =>");
    }

    [Test]
    public void LanguageThatIsNoIdentifier_ShouldKeepItsCodeAsLanguage()
    {
        // Arrange & Act: a hyphen can't be in a parameter name, and "is" (Icelandic) is a keyword.
        var text = Factory(Run(Source("\"en\", \"zh-Hans\", \"is\"")))!;

        // Assert
        text.ShouldContain("Inline(string en, string zhHans, string @is)");
        text.ShouldContain("private static readonly string[] Languages = [\"en\", \"zh-Hans\", \"is\"];");
        text.ShouldContain("1 => zhHans ?? throw new System.ArgumentNullException(nameof(zhHans)),");
        text.ShouldContain("2 => @is ?? throw new System.ArgumentNullException(nameof(@is)),");
        text.ShouldContain("_ => en ?? throw new System.ArgumentNullException(nameof(en)),");
        text.ShouldContain("<param name=\"is\">");
    }

    [Test]
    [Arguments("nl", "nl", "Nl")]
    [Arguments("NL", "nl", "Nl")]
    [Arguments("fil", "fil", "Fil")]
    [Arguments("FIL", "fil", "Fil")]
    [Arguments("nl-BE", "nlBe", "NlBe")]
    [Arguments("NL-be", "nlBe", "NlBe")]
    [Arguments("zh-Hans", "zhHans", "ZhHans")]
    [Arguments("sr-Latn-RS", "srLatnRs", "SrLatnRs")]
    [Arguments("es-419", "es419", "Es419")]
    public void Language_ShouldBecomeParameterNamedAsItsSetEnds(string code, string parameter, string suffix)
    {
        // Arrange & Act
        var text = Factory(Run(Source($"\"en\", \"{code}\"")))!;

        // Assert: a language reads alike in both places, however the attribute writes it. The parameter is what the
        // name of a set of the language ends in, in camelCase: nlBe next to ShopLocalizationNlBe.
        text.ShouldContain($"Inline(string en, string {parameter})");
        text.ShouldContain($"private static readonly string[] Languages = [\"en\", \"{code}\"];");
        text.ShouldContain($"1 => {parameter} ?? throw new System.ArgumentNullException(nameof({parameter})),");
        text.ShouldContain($"<param name=\"{parameter}\">The wording in <c>{code}</c>.</param>");
        SupportedLanguages.ToTypeSuffix(code).ShouldBe(suffix);
        parameter.ShouldBe(Char.ToLowerInvariant(suffix[0]) + suffix[1..]);
    }

    [Test]
    public void RegionalLanguage_ShouldCompileInSetAndInlineText()
    {
        // Arrange & Act: nl-BE as the end of a set's name, and as an argument.
        var errors = GeneratorTestHelper.CompileWithGenerators("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "nl-BE")]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Cart => "Cart";
            }

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public virtual string Cart => "Winkelwagen";
            }

            public class ShopLocalizationNlBe : ShopLocalizationNl
            {
                public override string Cart => "Winkelkar";
            }

            public static class Texts
            {
                public static string Greeting => LocalizableText.Inline(en: "Hello, {0}!", nl: "Hallo, {0}!", nlBe: "Dag, {0}!");
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
                public static string Colour => LocalizableText.Inline(enGb: "Colour", en: "Color");
            }
            """));

        var colour = assembly.GetType("RegionalDefault.Texts")!.GetProperty("Colour")!;

        // Act
        string Read(string culture)
        {
            using var _ = new CulturesScope(culture);

            var wording = (string)colour.GetValue(null)!;
            return wording;
        }

        // Assert: a culture neither serves gets the app's default language (this test project declares en).
        Read("en-GB").ShouldBe("Colour");
        Read("en-US").ShouldBe("Color");
        Read("fr-FR").ShouldBe("Color");

        // In an app without a default language of its own, it gets the default wording, which is the first.
        var french = LocalizationRegistry.Create([]).Resolve(CultureInfo.GetCultureInfo("fr-FR"));
        InlineLanguage.IndexOfShownLanguage(["en-GB", "en"], french).ShouldBe(0);
    }

    [Test]
    public void FactoryCalledByFullNameWithoutUsing_ShouldCompile()
    {
        // Arrange & Act: the factory is an extension, found only where its class is in reach. In the namespace of the
        // text it would take a using for that namespace, which a call by the text's full name does not write.
        var errors = GeneratorTestHelper.CompileWithGenerators("""
            [assembly: CodeChops.Idioma.SupportedLanguages("en", "nl")]

            namespace TestApp.Deep.Down;

            public static class Texts
            {
                public static string Orders => CodeChops.Idioma.LocalizableText.Inline(en: "Orders", nl: "Bestellingen");
            }
            """);

        // Assert
        errors.ShouldBeEmpty();
    }

    [Test]
    public void Factory_ShouldLieInTheGlobalNamespace()
    {
        // Arrange & Act
        var text = Factory(Run(Source("\"en\", \"nl\"")));

        // Assert
        text.ShouldNotBeNull();
        text.ShouldNotContain("namespace ");
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
        var errors = GeneratorTestHelper.CompileWithGenerators(Source("\"en\", \"nl\"", """public static string Orders => LocalizableText.Inline(en: "Orders", nl: "Bestellingen");"""));

        // Assert
        errors.ShouldBeEmpty();
    }

    [Test]
    public void LanguageLeftOut_ShouldNotCompile()
    {
        // Arrange & Act
        var errors = GeneratorTestHelper.CompileWithGenerators(Source("\"en\", \"nl\"", """public static string Orders => LocalizableText.Inline(en: "Orders");"""));

        // Assert: the error names the language.
        var error = errors.ShouldHaveSingleItem();
        error.Id.ShouldBe("CS7036");
        error.GetMessage().ShouldContain("'nl'");
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
                public static string Orders => LocalizableText.Inline(en: "Orders", nl: "Bestellingen");
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
                public static string Orders => LocalizableText.Inline(en: "Orders", nl: "Bestellingen");
            }
            """);

        // Act
        var errors = GeneratorTestHelper.CompileWithGenerators(
            Source("\"en\", \"nl\"", """public static string Orders => LocalizableText.Inline(en: "Orders", nl: "Bestellingen");"""),
            library);

        // Assert
        errors.ShouldBeEmpty();
    }

    [Test]
    public void LocalizedMember_InAssemblyWithoutLanguages_ShouldReportLOC100()
    {
        // Arrange & Act: without [assembly: SupportedLanguages] the generator writes no attribute. The compiler only says
        // that it isn't found (CS0246), and stops there before any analyzer runs, so the generator reports it.
        var result = Run("""
            namespace TestApp;

            public class Address
            {
                [Localized(en: "City", nl: "Plaats")]
                public string City { get; } = "";
            }
            """);

        // Assert: at the name of the attribute.
        var diagnostic = result.Results.ShouldHaveSingleItem().Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC100");
        diagnostic.GetMessage().ShouldBe("[Localized] takes a wording per language, but this assembly declares no languages. Add [assembly: SupportedLanguages(\"en\", \"nl\")] with your languages, the default first.");
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).ShouldBe("Localized");
    }

    [Test]
    public void LocalizedOfAnotherType_InAssemblyWithoutLanguages_ShouldNotBeReported()
    {
        // Arrange & Act: an attribute of that name the code brings itself.
        var result = Run("""
            namespace TestApp;

            public sealed class LocalizedAttribute(string en) : System.Attribute
            {
                public string En { get; } = en;
            }

            public class Address
            {
                [Localized(en: "City")]
                public string City { get; } = "";
            }
            """);

        // Assert
        result.Results.ShouldHaveSingleItem().Diagnostics.ShouldBeEmpty();
    }

    [Test]
    public void LocalizedMember_InAssemblyWithLanguages_ShouldNotReportLOC100()
    {
        // Arrange & Act: the attribute doesn't exist in the compilation the generator sees, but the generator writes it.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public class Address
            {
                [Localized(en: "City", nl: "Plaats")]
                public string City { get; } = "";
            }
            """);

        // Assert
        result.Results.ShouldHaveSingleItem().Diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("\"en\", \"nl\", \"\"", "'' isn't a culture name")]
    [Arguments("\"en\", \"n\"", "'n' isn't a culture name")]
    [Arguments("\"en\", \"nl_BE\"", "'nl_BE' isn't a culture name")]
    [Arguments("\"en\", \"nl-\"", "'nl-' isn't a culture name")]
    [Arguments("\"en\", \"nl\", \"NL\"", "'NL' is listed twice")]
    [Arguments("\"en\", \"sr-Latn-RS\", \"sr-LatnRS\"", "'sr-LatnRS' and 'sr-Latn-RS' differ only in their hyphens")]
    [Arguments("\"en\", \"zh-Hans\", \"zhhans\"", "'zhhans' and 'zh-Hans' differ only in their hyphens")]
    [Arguments("\"en\", \"nl\", null", "null isn't a culture name")]
    [Arguments("\"en\", null", "null isn't a culture name")]
    public void InvalidLanguage_ShouldReportLOC101AndKeepValidOnes(string languages, string expected)
    {
        // Arrange & Act
        var result = Run(Source(languages));

        // Assert: the factory still comes, so this stays the only error.
        var diagnostic = result.Results.ShouldHaveSingleItem().Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(LocalizableTextFactoryGenerator.InvalidLanguagesDiagnosticId);
        diagnostic.Id.ShouldBe("LOC101");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain(expected);
        Factory(result).ShouldNotBeNull().ShouldContain("string en");
    }

    [Test]
    [Arguments("dk", "'dk' won't match any user's culture, because 'dk' isn't an ISO 639-1 language code. Danish is 'da', so write 'da'.")]
    [Arguments("jp", "'jp' won't match any user's culture, because 'jp' isn't an ISO 639-1 language code. Japanese is 'ja', so write 'ja'.")]
    [Arguments("cz", "'cz' won't match any user's culture, because 'cz' isn't an ISO 639-1 language code. Czech is 'cs', so write 'cs'.")]
    [Arguments("gr", "'gr' won't match any user's culture, because 'gr' isn't an ISO 639-1 language code. Greek is 'el', so write 'el'.")]
    [Arguments("ua", "'ua' won't match any user's culture, because 'ua' isn't an ISO 639-1 language code. Ukrainian is 'uk', so write 'uk'.")]
    [Arguments("DK-dk", "'DK-dk' won't match any user's culture, because 'DK' isn't an ISO 639-1 language code. Danish is 'da', so write 'da-dk'.")]
    [Arguments("en-UK", "'en-UK' won't match any user's culture, because 'UK' isn't an ISO 3166-1 region code. The United Kingdom is 'GB', so write 'en-GB'.")]
    [Arguments("uk-UK", "'uk-UK' won't match any user's culture, because 'UK' isn't an ISO 3166-1 region code. Ukraine is 'UA', so write 'uk-UA'.")]
    [Arguments("sr-Latn-YU", "'sr-Latn-YU' won't match any user's culture, because 'YU' isn't an ISO 3166-1 region code. Check the code. If it's right, turn LOC104 off where it's reported.")]
    [Arguments("xx", "'xx' won't match any user's culture, because 'xx' isn't an ISO 639-1 language code. Check the code. If it's right, turn LOC104 off where it's reported.")]
    public void LanguageNoCultureHas_ShouldReportLOC104AndKeepTheLanguage(string language, string expected)
    {
        // Arrange & Act: the code has the form of a culture name, so the build uses it as written.
        var result = Run(Source($"\"en\", \"{language}\""));

        // Assert: a warning at the attribute, which a pragma there turns off.
        var diagnostic = result.Results.ShouldHaveSingleItem().Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(LocalizableTextFactoryGenerator.UnknownLanguageCodeDiagnosticId);
        diagnostic.Id.ShouldBe("LOC104");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().ShouldBe(expected);
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).ShouldStartWith("SupportedLanguages(");
        Factory(result).ShouldNotBeNull().ShouldContain($"string {SupportedLanguages.ToParameterName(language)}");
    }

    [Test]
    [Arguments("en")]
    [Arguments("nl")]
    [Arguments("en-GB")]
    [Arguments("zh-Hans")]
    [Arguments("zh-Hant-TW")]
    [Arguments("es-419")]
    [Arguments("en-001")]
    [Arguments("sr-Latn-RS")]
    [Arguments("fil")]
    [Arguments("sq-XK")]
    [Arguments("uk")]
    [Arguments("se")]
    [Arguments("EN-gb")]
    [Arguments("ca-ES-valencia")]
    [Arguments("en-US-u-ca-gregory")]
    public void LanguageACultureHas_ShouldReportNothing(string language)
    {
        // Arrange & Act: also a longer language, a script, and a region of three digits, which the build doesn't
        // check. 'uk' is Ukrainian and 'se' is Northern Sami.
        var result = Run(Source($"\"de\", \"{language}\""));

        // Assert
        result.Results.ShouldHaveSingleItem().Diagnostics.ShouldBeEmpty();
    }

    [Test]
    public void LanguageNoCultureHasAsDefault_ShouldReportLOC104ForEachCode()
    {
        // Arrange & Act
        var result = Run(Source("\"jp\", \"en-UK\""));

        // Assert
        var diagnostics = result.Results.ShouldHaveSingleItem().Diagnostics;
        diagnostics.Select(static diagnostic => diagnostic.Id).ShouldBe(["LOC104", "LOC104"]);
        diagnostics[0].GetMessage().ShouldStartWith("'jp' won't match");
        diagnostics[1].GetMessage().ShouldStartWith("'en-UK' won't match");
    }

    [Test]
    public void NullListOfLanguages_ShouldReportLOC101WithoutStoppingAnyGenerator()
    {
        // Arrange: null for the other languages passes no array at all, which every generator reads.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", null)]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            public static class Texts
            {
                public static string Orders => LocalizableText.Inline(en: "Orders");
                public static string Checkout => ShopLocalization.Checkout;
            }
            """;

        // Act
        var errors = GeneratorTestHelper.CompileWithGenerators(source);

        // Assert: a generator that throws leaves its output out, and Inline or ShopLocalization missing.
        errors.Select(static error => error.Id).ShouldBe(["LOC101"]);
    }

    [Test]
    [Arguments("public static string Orders => LocalizableText.Inline(en: \"Orders\", nl: \"Bestellingen\");")]
    [Arguments("public static string Orders => CodeChops.Idioma.LocalizableText.Inline(en: \"Orders\", nl: \"Bestellingen\");")]
    [Arguments("public static string Orders => CodeChops.Idioma.LocalizableText.Inline(en: \"Orders\", nl: \"Bestellingen\");")]
    public void FactoryCalledInCode_ShouldMarkAssemblyAsWritingInlineTexts(string body)
    {
        // Arrange & Act
        var marker = InlineTextsMarker(Run(Source("\"en\", \"nl\"", body)));

        // Assert
        marker.ShouldNotBeNull().ShouldContain("[assembly: CodeChops.Idioma.Internals.InlineTextsAttribute]");
    }

    [Test]
    public void MarkerOfInlineTexts_ShouldLeaveNoWarning()
    {
        // Arrange: InlineTextsAttribute is marked [Obsolete] for consumers. A warning about it in generated code is one
        // the author can't act on, and it stops a build that treats warnings as errors.
        var source = Source("\"en\", \"nl\"", "public static string Orders => LocalizableText.Inline(en: \"Orders\", nl: \"Bestellingen\");");

        // Act
        var marker = InlineTextsMarker(Run(source));
        var warnings = GeneratorTestHelper.GetWarningsWithGenerators(source);

        // Assert
        marker.ShouldNotBeNull();
        warnings.ShouldBeEmpty();
    }

    [Test]
    [Arguments("using Text = CodeChops.Idioma.LocalizableText;", "Text.Inline(en: \"Orders\", nl: \"Bestellingen\")")]
    [Arguments("using Text = global::CodeChops.Idioma.LocalizableText;", "Text.Inline(en: \"Orders\", nl: \"Bestellingen\")")]
    public void FactoryCalledThroughAlias_ShouldMarkAssemblyAsWritingInlineTexts(string alias, string call)
    {
        // Arrange: the name at the call says nothing, so the receiver has to be bound to be known for the text type.
        var source = $$"""
            {{alias}}

            [assembly: CodeChops.Idioma.SupportedLanguages("en", "nl")]

            namespace TestApp;

            public static class Texts
            {
                public static string Orders => {{call}};
            }
            """;

        // Act
        var marker = InlineTextsMarker(Run(source));

        // Assert
        marker.ShouldNotBeNull().ShouldContain("[assembly: CodeChops.Idioma.Internals.InlineTextsAttribute]");
    }

    [Test]
    public void InlineCalledOnAnotherTypeThroughAlias_ShouldLeaveAssemblyUnmarked()
    {
        // Arrange & Act: an alias of a type that has a method of that name, which is no inline text.
        var result = Run("""
            using Helper = TestApp.Other;

            [assembly: CodeChops.Idioma.SupportedLanguages("en", "nl")]

            namespace TestApp;

            public static class Other
            {
                public static string Inline() => "";
            }

            public static class Texts
            {
                public static string Orders { get; } = Helper.Inline();
            }
            """);

        // Assert
        InlineTextsMarker(result).ShouldBeNull();
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
            ("Pages/Orders.razor", "<h1>@ShopLocalization.Checkout</h1>"));

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
                public static string Orders => LocalizableText.Inline(en: "Orders", nl: "Bestellingen");
            }
            """);

        // Assert
        InlineTextsMarker(result).ShouldBeNull();
    }

    [Test]
    public Task Languages_ShouldBecomeRequiredParametersOfLocalizedInOrder()
    {
        // Arrange & Act
        var text = LocalizedAttribute(Run(Source("\"en\", \"nl\", \"en-GB\"")))!;

        // Assert: one parameter per language, as the factory has.
        return Verify(text);
    }

    [Test]
    public void LocalizedOfLanguageThatIsNoIdentifier_ShouldTakeTheParametersOfTheFactory()
    {
        // Arrange & Act: a hyphen can't be in a parameter name, and "is" (Icelandic) is a keyword.
        var text = LocalizedAttribute(Run(Source("\"en\", \"zh-Hans\", \"is\"")))!;

        // Assert
        text.ShouldContain("public LocalizedAttribute(string en, string zhHans, string @is)");
        text.ShouldContain("<param name=\"is\">The wording in <c>is</c>.</param>");
        text.ShouldContain("[Microsoft.CodeAnalysis.EmbeddedAttribute]");
    }

    [Test]
    public void NoAttribute_ShouldEmitNoLocalized()
    {
        // Arrange & Act
        var result = Run("namespace TestApp;");

        // Assert
        LocalizedAttribute(result).ShouldBeNull();
    }

    [Test]
    public void LocalizedOnEveryKindOfMember_ShouldCompileWithoutWarningsAndWithResolvedCrefs()
    {
        // Arrange: a property, a field, a member of an enum, and each kind of type.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "zh-Hans", "is")]

            namespace TestApp;

            [Localized(en: "Address", zhHans: "地址", @is: "Heimilisfang")]
            public class Address
            {
                [Localized(en: "City", zhHans: "城市", @is: "Borg")]
                public string City { get; } = "";

                [Localized(en: "Note", zhHans: "备注", @is: "Athugasemd")]
                public string Note = "";
            }

            [Localized(en: "Status", zhHans: "状态", @is: "Staða")]
            public enum Status
            {
                [Localized(en: "Open", zhHans: "打开", @is: "Opið")]
                Open,
            }

            [Localized(en: "Money", zhHans: "金额", @is: "Fé")]
            public struct Money;

            [Localized(en: "Named", zhHans: "命名", @is: "Nefnt")]
            public interface INamed;

            [Localized(en: "Line", zhHans: "行", @is: "Lína")]
            public record Line([property: Localized(en: "Quantity", zhHans: "数量", @is: "Magn")] int Quantity);
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
    public void LanguageLeftOutOfLocalized_ShouldNotCompile()
    {
        // Arrange & Act
        var errors = GeneratorTestHelper.CompileWithGenerators(Member("\"en\", \"nl\"", """[Localized(en: "City")]"""));

        // Assert: the error names the language.
        var error = errors.ShouldHaveSingleItem();
        error.Id.ShouldBe("CS7036");
        error.GetMessage().ShouldContain("'nl'");
    }

    [Test]
    public void LocalizedOnAMethod_ShouldNotCompile()
    {
        // Arrange & Act: a text is on a property, a field, a member of an enum, or a type.
        var errors = GeneratorTestHelper.CompileWithGenerators(Source("\"en\", \"nl\"", """[Localized(en: "Save", nl: "Opslaan")] public static void Save() { }"""));

        // Assert
        errors.ShouldHaveSingleItem().Id.ShouldBe("CS0592");
    }

    [Test]
    public void Localized_ShouldStayOutOfTheCompiledAssembly()
    {
        // Arrange & Act
        var assembly = Load(GeneratorTestHelper.EmitLibraryWithGenerators("ConditionalLocalized", Member("\"en\", \"nl\"", """[Localized(en: "City", nl: "Plaats")]""")));
        var property = assembly.GetType("TestApp.Address")!.GetProperty("City")!;

        // Assert: the build reads the wording and writes it as code, so the attribute on the member would only store
        // the wording a second time.
        property.GetCustomAttributesData().ShouldBeEmpty();
    }

    [Test]
    public void LibraryWithInternalsVisibleToApp_ShouldNotMakeLocalizedAmbiguous()
    {
        // Arrange: both assemblies generate the attribute and the class that records their members' texts, and the app
        // sees the library's internals.
        var library = GeneratorTestHelper.CompileLibraryWithGenerators("ShopLibrary", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]
            [assembly: System.Runtime.CompilerServices.InternalsVisibleTo("TestAssembly")]

            namespace ShopLibrary;

            public class Product
            {
                [Localized(en: "Name", nl: "Naam")]
                public string Name { get; } = "";
            }
            """);
        var source = Member("\"en\", \"nl\"", """[Localized(en: "City", nl: "Plaats")]""");

        // Act
        var errors = GeneratorTestHelper.CompileWithGenerators(source, library);
        var warnings = GeneratorTestHelper.GetWarningsWithGenerators(source, library);

        // Assert
        errors.ShouldBeEmpty();
        warnings.ShouldBeEmpty();
    }

    [Test]
    public void MemberMarkedLocalized_ShouldLeaveTheMarkToTheFileOfItsText()
    {
        // Arrange & Act: this generator marks an assembly for LocalizableText.Inline alone. The file that holds the
        // texts of members marks it for those, so that this generator need not look for them.
        var marker = InlineTextsMarker(Run(Member("\"en\", \"nl\"", """[Localized(en: "City", nl: "Plaats")]""")));

        // Assert
        marker.ShouldBeNull();
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
            Source("\"en\", \"nl\", \"en\"", "public static string Orders => LocalizableText.Inline(en: \"Orders\", nl: \"Bestellingen\");"));

        // Assert: the diagnostic lies in the tree the file has now, where a pragma of that file applies to it, and
        // neither the factory nor the marker for inline texts is written again.
        var diagnostic = run.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC101");
        diagnostic.Location.SourceTree.ShouldBeSameAs(edited);
        var reasons = GeneratorTestHelper.GetSourceOutputReasons(run);
        reasons.Length.ShouldBe(2);
        reasons.ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }
}
