using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="SupportedLanguages"/> against the cultures of the runtime the tests run on: the parents the
/// build writes out must be the parents a text follows at run time (<see cref="CultureInfo.Parent"/>), and the
/// languages and regions it knows must hold those of every culture (<c>LOC103</c>).
/// </summary>
public sealed class SupportedLanguagesTests
{
    #region Fixture
    /// <summary>
    /// Every culture the runtime lists, and every name a listed culture with a script and a region has without its
    /// script (<c>zh-TW</c> for <c>zh-Hant-TW</c>), which the runtime does not list but does create.
    /// </summary>
    private static IEnumerable<CultureInfo> CulturesToCheck()
    {
        var listed = CultureInfo.GetCultures(CultureTypes.AllCultures);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var culture in listed)
        {
            if (culture.Name.Length > 0 && names.Add(culture.Name))
                yield return culture;
        }

        foreach (var culture in listed)
        {
            var parts = culture.Name.Split('-');
            if (parts.Length is not 3)
                continue;

            var withoutScript = $"{parts[0]}-{parts[2]}";
            if (!names.Add(withoutScript))
                continue;

            CultureInfo? alias;
            try
            {
                alias = CultureInfo.GetCultureInfo(withoutScript);
            }
            catch (CultureNotFoundException)
            {
                alias = null;
            }

            if (alias is not null)
                yield return alias;
        }
    }
    #endregion

    [Test]
    public void ScriptParentOfChineseRegion_ShouldBeTheParentTheRuntimeGives()
    {
        // Arrange: the five codes the table covers, which the runtime must still know, and must not have moved.
        string[] codes = ["zh-CN", "zh-SG", "zh-HK", "zh-MO", "zh-TW"];

        foreach (var code in codes)
        {
            // Act
            var parent = SupportedLanguages.FindScriptParentOfChineseRegion(code);

            // Assert
            parent.ShouldBe(CultureInfo.GetCultureInfo(code).Parent.Name, customMessage: code);
        }
    }

    [Test]
    public void EveryParentThatIsNoPrefix_ShouldBeInTheTable()
    {
        // Arrange: a parent the name does not start with, which only the table can give the build. The invariant
        // culture is left out, as a parent no set is written for (en-POSIX has it).
        var missed = new List<string>();

        foreach (var culture in CulturesToCheck())
        {
            var parent = culture.Parent.Name;
            if (parent.Length is 0 || culture.Name.StartsWith(parent + "-", StringComparison.OrdinalIgnoreCase))
                continue;

            // Act
            var written = SupportedLanguages.FindScriptParentOfChineseRegion(culture.Name);

            if (!String.Equals(written, parent, StringComparison.OrdinalIgnoreCase))
                missed.Add($"{culture.Name} -> {parent} (the table gives {written ?? "nothing"})");
        }

        // Assert
        missed.ShouldBeEmpty();
    }

    [Test]
    [Arguments("zh-TW", "zh-Hant-TW", true)]
    [Arguments("zh-CN", "zh-Hans-CN", true)]
    [Arguments("zh-HK", "ZH-HANT-HK", true)]
    [Arguments("zh-Hant", "zh-Hant-TW", true)]
    [Arguments("zh-CN", "zh-Hant-CN", false)] // Another script than zh-CN's
    [Arguments("zh-TW", "zh-Hans-TW", false)]
    [Arguments("zh-TW", "zh-Hant", false)]
    public void ChineseRegionWithScript_ShouldBeServedByTheRegionWithoutIt(string provided, string requested, bool expected)
    {
        // Arrange & Act: at run time a text tries zh-TW right after zh-Hant-TW, as an Apple platform names the culture.
        var serves = SupportedLanguages.Serves(provided, requested);

        // Assert
        serves.ShouldBe(expected);
    }

    [Test]
    public void EveryCultureOfTheRuntime_ShouldHaveNoUnknownPart()
    {
        // Arrange: the tables of languages and regions the build carries, against the cultures a user can have. A
        // culture the tables don't know would get LOC103 for a code that is right.
        var unknown = new List<string>();

        foreach (var culture in CulturesToCheck())
        {
            // Act
            if (SupportedLanguages.IsCultureName(culture.Name) && SupportedLanguages.FindUnknownPart(culture.Name) is { } part)
                unknown.Add($"{culture.Name} ({part.Part})");
        }

        // Assert
        unknown.ShouldBeEmpty();
    }

    [Test]
    [Arguments("dk", "dk", "ISO 639-1 language")]
    [Arguments("JP", "JP", "ISO 639-1 language")]
    [Arguments("iw", "iw", "ISO 639-1 language")]
    [Arguments("dk-UK", "dk", "ISO 639-1 language")]
    [Arguments("en-UK", "UK", "ISO 3166-1 region")]
    [Arguments("en-uk", "uk", "ISO 3166-1 region")]
    [Arguments("zh-Hant-ZZ", "ZZ", "ISO 3166-1 region")]
    [Arguments("fil-UK", "UK", "ISO 3166-1 region")]
    public void CodeWithPartNoCultureHas_ShouldGiveThatPart(string code, string expectedPart, string expectedKind)
    {
        // Act: the language before the region, in any letter case.
        var unknown = SupportedLanguages.FindUnknownPart(code);

        // Assert
        unknown.ShouldNotBeNull();
        unknown.Value.Code.ShouldBe(code);
        unknown.Value.Part.ShouldBe(expectedPart);
        unknown.Value.Kind.ShouldBe(expectedKind);
    }

    [Test]
    [Arguments("en")]
    [Arguments("uk")]
    [Arguments("se")]
    [Arguments("en-GB")]
    [Arguments("sq-XK")]
    [Arguments("es-419")]
    [Arguments("en-001")]
    [Arguments("zh-Hans")]
    [Arguments("zh-Hant-TW")]
    [Arguments("sr-Latn-RS")]
    [Arguments("fil")]
    [Arguments("xyz-Abcd-123")]
    [Arguments("en-US-u-ca-gregory")]
    [Arguments("ca-ES-valencia")]
    public void CodeOfKnownOrUncheckedParts_ShouldGiveNothing(string code)
    {
        // Act: a language of three letters, a script, a region of three digits, and what follows the region aren't
        // checked.
        var unknown = SupportedLanguages.FindUnknownPart(code);

        // Assert
        unknown.ShouldBeNull();
    }

    [Test]
    [Arguments("[assembly: SupportedLanguages(\"en\", \"nl\", \"de\")]", "en,nl,de", "en")]
    [Arguments("[assembly: SupportedLanguages(otherLanguages: [\"nl\"], defaultLanguage: \"en\")]", "en,nl", "en")]
    [Arguments("[assembly: SupportedLanguages(\"en\", new[] { \"nl\" })]", "en,nl", "en")]
    [Arguments("[assembly: CodeChops.Idioma.SupportedLanguagesAttribute(\"en-GB\")]", "en-GB", "en-GB")]
    [Arguments("[assembly: SupportedLanguages(\"en\", null)]", "en", "en")]
    [Arguments("[assembly: SupportedLanguages(\"en_GB\", \"nl\")]", "en_GB,nl", null)]
    [Arguments("[assembly: Languages(\"nl\", \"en\")]", "nl,en", "nl")]
    [Arguments("[assembly: SupportedLanguages(\"en\")]\n[assembly: SupportedLanguages(\"nl\")]", "en", null)]
    [Arguments("[assembly: System.Reflection.AssemblyTitle(\"Shop\")]", "", null)]
    public void LanguagesOfCompilation_ShouldBeReadFromTheAttributeHoweverItIsWritten(string attributes, string codes, string? defaultLanguage)
    {
        // Arrange: by name or by position, an array written out or as a collection expression, with its full name, and
        // through an alias, which the syntax doesn't show. Two attributes, which don't compile, give no default language.
        var source = $"""
            using CodeChops.Idioma;
            using Languages = CodeChops.Idioma.SupportedLanguagesAttribute;

            {attributes}
            """;

        var compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create(
            "Languages",
            [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(source, GeneratorTestHelper.ParseOptions)],
            GeneratorTestHelper.References,
            new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary));

        // Act
        var read = SupportedLanguages.ReadCodes(compilation.Assembly);
        var readDefault = SupportedLanguages.ReadDefaultLanguage(compilation.Assembly);

        // Assert
        String.Join(",", read).ShouldBe(codes);
        readDefault.ShouldBe(defaultLanguage);
    }

    [Test]
    public void EveryCulture_ShouldBeServedByEachOfItsParents()
    {
        // Arrange: what the build accepts (Serves) against the parents a text walks through at run time.
        var missed = new List<string>();

        foreach (var culture in CulturesToCheck())
        {
            for (var parent = culture.Parent; parent.Name.Length > 0; parent = parent.Parent)
            {
                // Act
                if (!SupportedLanguages.Serves(parent.Name, culture.Name))
                    missed.Add($"{parent.Name} does not serve {culture.Name}");
            }
        }

        // Assert
        missed.ShouldBeEmpty();
    }

    [Test]
    [Arguments("nl", "nl")]
    [Arguments("NL", "nl")]
    [Arguments("NL-be", "nl-BE")]
    [Arguments("ZH-hans-tw", "zh-Hans-TW")]
    [Arguments("ES-419", "es-419")]
    [Arguments("ca-es-VALENCIA", "ca-ES-valencia")]
    [Arguments("en-us-posix", "en-US-posix")]
    [Arguments("sgn-be-fr", "sgn-BE-FR")]
    [Arguments("EN-ca-X-CA", "en-CA-x-ca")]
    [Arguments("az-latn-x-LATN", "az-Latn-x-latn")]
    [Arguments("en-us-U-CA-Gregory", "en-US-u-ca-gregory")]
    public void Normalize_ShouldWriteCodeAsCultureNamesAreWritten(string code, string expected)
    {
        // Arrange & Act & Assert: the letter case RFC 5646 recommends, in which the generated code passes every code.
        SupportedLanguages.Normalize(code).ShouldBe(expected);
    }

    [Test]
    [Arguments("ShopLocalizationEnGb", "en", "en-GB")]
    [Arguments("ShopLocalizationEnGb", "en,en-GB", "en-GB")]
    [Arguments("ShopLocalizationDeDe", "de", "de-DE")]
    [Arguments("ShopLocalizationFrCa", "fr", "fr-CA")]
    [Arguments("ShopLocalizationFrCa", "fr,ca,fr-CA", "fr-CA")]
    [Arguments("ShopLocalizationFrCa", "fr,ca", null)] // Catalan, by its last two letters
    [Arguments("YesNoDe", "en,no,de", null)] // German, not no-DE
    [Arguments("UserIdNl", "id,nl", null)] // Dutch, not id-NL
    [Arguments("AdminUiNl", "en,nl", null)]
    public void FindNamedLanguage_ShouldLetDeclaredLanguageAtTheEndWinOverRegion(string typeName, string codes, string? expected)
    {
        // Arrange & Act: null leaves the name to its last two letters.
        var language = SupportedLanguages.FindNamedLanguage(typeName, codes.Split(','));

        // Assert
        language.ShouldBe(expected);
    }

    [Test]
    public void Normalize_ShouldAgreeWithCultureNamesOfRuntime()
    {
        // Arrange
        var differing = new List<string>();

        foreach (var culture in CultureInfo.GetCultures(CultureTypes.AllCultures))
        {
            // Act: a culture name the build accepts is written as the runtime writes it. Not a variant, which ICU writes
            // in upper case (en-US-POSIX), where RFC 5646 recommends lower case.
            if (culture.Name.Length > 0 && SupportedLanguages.IsCultureName(culture.Name) && culture.Name.Split('-').All(static part => part.Length <= 4)
                && !String.Equals(SupportedLanguages.Normalize(culture.Name.ToUpperInvariant()), culture.Name, StringComparison.Ordinal))
            {
                differing.Add(culture.Name);
            }
        }

        // Assert
        differing.ShouldBeEmpty();
    }
}
