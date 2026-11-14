using CodeChops.Idioma.Generators;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="SupportedLanguages"/> against the cultures of the runtime the tests run on: the parents the
/// build writes out must be the parents a text follows at run time (<see cref="CultureInfo.Parent"/>).
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
