namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="CultureFallback"/>: the culture names a lookup tries, through the parent cultures and by name.
/// </summary>
public sealed class CultureFallbackTests
{
    #region Fixture
    /// <summary>
    /// A culture whose parent is the invariant culture, as every culture's is under invariant globalization with
    /// <c>PredefinedCulturesOnly</c> off (with it on, the default, no such culture can be created).
    /// </summary>
    private sealed class FallbackInvariantParentCulture(string name) : CultureInfo(name)
    {
        public override CultureInfo Parent => InvariantCulture;
    }

    private static string[] Names(CultureInfo culture) => Names(new CultureFallback(culture));

    private static string[] Names(string name) => Names(new CultureFallback(name));

    private static string[] Names(CultureFallback fallback)
    {
        var names = new List<string>();
        foreach (var name in fallback)
            names.Add(name);

        return [.. names];
    }
    #endregion

    [Test]
    public void Names_ShouldFollowParentCultures()
    {
        // Arrange & Act & Assert: a parent that is no prefix of the name (zh-TW falls back to zh-Hant) is followed too.
        Names(CultureInfo.GetCultureInfo("nl-BE")).ShouldBe(["nl-BE", "nl"]);
        Names(CultureInfo.GetCultureInfo("zh-TW")).ShouldBe(["zh-TW", "zh-Hant", "zh"]);
        Names(CultureInfo.GetCultureInfo("nl")).ShouldBe(["nl"]);
        Names(CultureInfo.InvariantCulture).ShouldBeEmpty();
    }

    [Test]
    public void Names_WithInvariantParent_ShouldDropSubtagsByName()
    {
        // Arrange & Act & Assert
        Names(new FallbackInvariantParentCulture("nl-BE")).ShouldBe(["nl-BE", "nl"]);
        Names(new FallbackInvariantParentCulture("zh-Hant-TW")).ShouldBe(["zh-Hant-TW", "zh-TW", "zh-Hant", "zh"]);
        Names(new FallbackInvariantParentCulture("nl")).ShouldBe(["nl"]);
    }

    [Test]
    public void Names_OfCultureName_ShouldDropSubtagsWithoutCreatingCulture()
    {
        // Arrange & Act & Assert: a language code, which under invariant globalization can't become a culture.
        Names("zh-Hant-TW").ShouldBe(["zh-Hant-TW", "zh-TW", "zh-Hant", "zh"]);
        Names("nl").ShouldBe(["nl"]);
        Should.Throw<ArgumentException>(() => Names(""));
    }

    [Test]
    public void Names_OfLanguage_ShouldFollowParentCultures()
    {
        // Arrange & Act & Assert: by name alone, zh-TW would skip zh-Hant.
        Names(CultureFallback.OfLanguage("zh-TW")).ShouldBe(["zh-TW", "zh-Hant", "zh"]);
    }

    [Test]
    public void Names_OfLanguageWithAliasCulture_ShouldDropSubtagsByName()
    {
        // Arrange & Act & Assert: ICU gives zh-yue the culture yue, whose walk would never try zh-yue.
        Names(CultureFallback.OfLanguage("zh-yue")).ShouldBe(["zh-yue", "zh"]);
    }

    [Test]
    public void Names_WithScript_ShouldTryNameWithoutScriptRightAfterIt()
    {
        // Arrange & Act & Assert: a platform can give a culture with its script, where an app declares the name without
        // it. zh-TW writes in Hant (its parent culture), and pt-BR in Latin, so each is the same culture.
        Names(CultureInfo.GetCultureInfo("zh-Hant-TW")).ShouldBe(["zh-Hant-TW", "zh-TW", "zh-Hant", "zh"]);
        Names(new CultureInfo("pt-Latn-BR"))[..2].ShouldBe(["pt-Latn-BR", "pt-BR"]);
    }

    [Test]
    public void Names_WithScriptOfAnotherCulture_ShouldNotTryNameWithoutScript()
    {
        // Arrange & Act & Assert: zh-CN writes in Hans, not Hant, and sr-RS in Cyrillic, not Latin, which its month
        // names show. Tried after the full name, they would give a user text in a script they didn't choose.
        Names(CultureInfo.GetCultureInfo("zh-Hant-CN")).ShouldBe(["zh-Hant-CN", "zh-Hant", "zh"]);
        Names(CultureInfo.GetCultureInfo("sr-Latn-RS")).ShouldBe(["sr-Latn-RS", "sr-Latn", "sr"]);
        Names(CultureInfo.GetCultureInfo("sr-Cyrl-RS")).ShouldBe(["sr-Cyrl-RS", "sr-RS", "sr-Cyrl", "sr"]);
    }

    [Test]
    public void Names_WithScriptAndNothingAfterIt_ShouldNotDropScript()
    {
        // Arrange & Act & Assert: without a script, zh-Hant would be zh, which is its parent anyway.
        Names(CultureInfo.GetCultureInfo("zh-Hant")).ShouldBe(["zh-Hant", "zh"]);
        Names("zh-Hant").ShouldBe(["zh-Hant", "zh"]);
    }
}
