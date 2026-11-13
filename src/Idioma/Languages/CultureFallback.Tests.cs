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
        Names(new FallbackInvariantParentCulture("zh-Hant-TW")).ShouldBe(["zh-Hant-TW", "zh-Hant", "zh"]);
        Names(new FallbackInvariantParentCulture("nl")).ShouldBe(["nl"]);
    }

    [Test]
    public void Names_OfCultureName_ShouldDropSubtagsWithoutCreatingCulture()
    {
        // Arrange & Act & Assert: a language code, which under invariant globalization cannot become a culture.
        Names("zh-Hant-TW").ShouldBe(["zh-Hant-TW", "zh-Hant", "zh"]);
        Names("nl").ShouldBe(["nl"]);
        Names("").ShouldBeEmpty();
    }

    [Test]
    public void Names_OfCultureName_ShouldBeSameStringsOnNextWalk()
    {
        // Arrange
        var first = Names("zh-Hant-TW");

        // Act
        var second = Names(new string("zh-Hant-TW"));

        // Assert: the names that dropping a subtag gives are kept, so that a walk by name allocates nothing.
        second[1].ShouldBeSameAs(first[1]);
        second[2].ShouldBeSameAs(first[2]);
    }
}
