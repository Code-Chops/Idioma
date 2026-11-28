namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="CultureFallback"/>: the culture names a lookup tries, through the parent cultures and by name.
/// </summary>
public sealed class CultureFallbackTests
{
    #region Fixture
    /// <summary>
    /// A culture with the parent given. With the invariant culture as its parent, it's what every culture is under
    /// invariant globalization with <c>PredefinedCulturesOnly</c> off (with it on, the default, no such culture can be
    /// created).
    /// </summary>
    private sealed class CultureWithParent(string name, CultureInfo parent) : CultureInfo(name)
    {
        public override CultureInfo Parent => parent;
    }

    /// <summary>
    /// The scripts the generated code of an app that declares <c>zh-TW</c>, <c>zh-CN</c> and <c>sr-RS</c> passes.
    /// </summary>
    private static readonly FrozenDictionary<string, string> AppScripts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["zh-TW"] = "Hant",
        ["zh-CN"] = "Hans",
        ["sr-RS"] = "Cyrl",
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static string[] Names(CultureInfo culture) => Names(new CultureFallback(culture, AppScripts));

    private static string[] Names(string name) => Names(new CultureFallback(name, AppScripts));

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
        Names(CultureInfo.GetCultureInfo("en-GB")).ShouldBe(["en-GB", "en"]);
        Names(CultureInfo.GetCultureInfo("zh-TW")).ShouldBe(["zh-TW", "zh-Hant", "zh"]);
        Names(CultureInfo.GetCultureInfo("nl")).ShouldBe(["nl"]);
        Names(CultureInfo.InvariantCulture).ShouldBeEmpty();
    }

    [Test]
    public void Names_WithInvariantParent_ShouldDropSubtagsByName()
    {
        // Arrange & Act & Assert
        Names(new CultureWithParent("en-GB", CultureInfo.InvariantCulture)).ShouldBe(["en-GB", "en"]);
        Names(new CultureWithParent("zh-Hant-TW", CultureInfo.InvariantCulture)).ShouldBe(["zh-Hant-TW", "zh-TW", "zh-Hant", "zh"]);
        Names(new CultureWithParent("nl", CultureInfo.InvariantCulture)).ShouldBe(["nl"]);
    }

    [Test]
    public void Names_WithParentWithoutScript_ShouldTryItOnce()
    {
        // Arrange
        var culture = new CultureWithParent("zh-Hant-TW", CultureInfo.GetCultureInfo("zh-TW"));

        // Act & Assert: the parent is the name without its script, which is tried right after the name already.
        Names(culture).ShouldBe(["zh-Hant-TW", "zh-TW", "zh-Hant", "zh"]);
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
    public void Names_WithScriptOfAppLanguage_ShouldTryNameWithoutScriptRightAfterIt()
    {
        // Arrange & Act & Assert: a platform can give a culture with its script, where an app declares the name without
        // it. The build gives zh-TW Hant and sr-RS Cyrillic, so each is the same script.
        Names(CultureInfo.GetCultureInfo("zh-Hant-TW")).ShouldBe(["zh-Hant-TW", "zh-TW", "zh-Hant", "zh"]);
        Names(CultureInfo.GetCultureInfo("sr-Cyrl-RS")).ShouldBe(["sr-Cyrl-RS", "sr-RS", "sr-Cyrl", "sr"]);
    }

    [Test]
    public void Names_WithOtherScriptThanAppLanguage_ShouldNotTryNameWithoutScript()
    {
        // Arrange & Act & Assert: zh-TW is written in Hant, not Hans, zh-CN in Hans, not Hant, and sr-RS in Cyrillic, not
        // Latin. Tried after the full name, they would give a user text in a script they didn't choose.
        Names(CultureInfo.GetCultureInfo("zh-Hans-TW"))[..2].ShouldBe(["zh-Hans-TW", "zh-Hans"]);
        Names(CultureInfo.GetCultureInfo("zh-Hant-CN")).ShouldBe(["zh-Hant-CN", "zh-Hant", "zh"]);
        Names(CultureInfo.GetCultureInfo("sr-Latn-RS")).ShouldBe(["sr-Latn-RS", "sr-Latn", "sr"]);
    }

    [Test]
    public void Names_WithScriptOfLanguageWithoutScriptGiven_ShouldTryNameWithoutScript()
    {
        // Arrange & Act & Assert: the build gives pt-BR no script, and a registry without app languages has no scripts at
        // all, so the name without the script is tried.
        Names(new CultureInfo("pt-Latn-BR"))[..2].ShouldBe(["pt-Latn-BR", "pt-BR"]);
        Names("qq-Latn-QQ").ShouldBe(["qq-Latn-QQ", "qq-QQ", "qq-Latn", "qq"]);
        Names(new CultureFallback(new CultureWithParent("sr-Latn-RS", CultureInfo.InvariantCulture)))
            .ShouldBe(["sr-Latn-RS", "sr-RS", "sr-Latn", "sr"]);
    }

    [Test]
    public void Names_WithScriptAndNothingAfterIt_ShouldNotDropScript()
    {
        // Arrange & Act & Assert: without a script, zh-Hant would be zh, which is its parent anyway.
        Names(CultureInfo.GetCultureInfo("zh-Hant")).ShouldBe(["zh-Hant", "zh"]);
        Names("zh-Hant").ShouldBe(["zh-Hant", "zh"]);
    }
}
