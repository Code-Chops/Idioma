using CodeChops.Idioma.Generators;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="SupportedLanguages"/> against the cultures of the runtime the tests run on: the parents the
/// build writes out must be the parents a text follows at run time (<see cref="CultureInfo.Parent"/>), and the
/// languages and regions it knows must hold those of every culture (<c>LOC104</c>).
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
        // culture the tables don't know would get LOC104 for a code that is right.
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
}
