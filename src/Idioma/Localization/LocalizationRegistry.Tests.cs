namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="LocalizationRegistry"/>: a registry holding exactly the given sets, lookup by contract with the
/// culture walk, and the sets of the whole app with their two tiers. The sets of the whole app are process-wide, so
/// every test registering there uses a contract of its own, and runs apart from the test that compares
/// <see cref="LocalizationRegistry.Current"/> by reference.
/// </summary>
public sealed partial class LocalizationRegistryTests
{
    #region Fixture
    private interface IRegistryLocalization : ILocalizationSet;

    private sealed class RegistryLocalizationEn : IRegistryLocalization;

    private sealed class RegistryLocalizationNl : IRegistryLocalization;

    private sealed class RegistryReplacementLocalizationNl : IRegistryLocalization;

    /// <summary>
    /// A contract no set in <see cref="RegistryLocalizationNl"/>'s family implements.
    /// </summary>
    private interface IRegistryOtherLocalization : ILocalizationSet;

    /// <summary>
    /// A set serving two contracts at once.
    /// </summary>
    private sealed class RegistryCombinedLocalizationNl : IRegistryLocalization, IRegistryOtherLocalization;

    private interface IRegistryTierLocalization : ILocalizationSet;

    private sealed class RegistryTierDefaultLocalizationNl : IRegistryTierLocalization;

    private sealed class RegistryTierOverrideLocalizationNl : IRegistryTierLocalization;

    private interface IRegistryConcurrentLocalization : ILocalizationSet
    {
        string Name { get; }
    }

    /// <summary>
    /// The set the text takes its default wording from. Never registered, so a Dutch reader never sees it.
    /// </summary>
    private sealed class RegistryConcurrentLocalizationEn : IRegistryConcurrentLocalization
    {
        public string Name => "Default";
    }

    private sealed class RegistryConcurrentFirstLocalizationNl : IRegistryConcurrentLocalization
    {
        public string Name => "Eerste";
    }

    private sealed class RegistryConcurrentSecondLocalizationNl : IRegistryConcurrentLocalization
    {
        public string Name => "Tweede";
    }

    /// <summary>
    /// Registered in the other tier, which the sets above come after.
    /// </summary>
    private sealed class RegistryConcurrentDefaultLocalizationNl : IRegistryConcurrentLocalization
    {
        public string Name => "Standaard";
    }

    private interface IRegistryOrderLocalization : ILocalizationSet;

    private sealed class RegistryOrderFirstLocalizationNl : IRegistryOrderLocalization;

    private sealed class RegistryOrderSecondLocalizationNl : IRegistryOrderLocalization;

    private interface IRegistryGenericLocalization : ILocalizationSet;

    private sealed class RegistryGenericLocalizationNl : IRegistryGenericLocalization;

    /// <summary>
    /// A set whose language is a constructor argument, so two instances of the type can serve two languages.
    /// </summary>
    private sealed class RegistryConfiguredLocalization(string language) : ILocalizationSet
    {
        public string LocalizationLanguageCode { get; } = language;
    }

    /// <summary>
    /// A set whose language code can change after it registered, which a set's shouldn't.
    /// </summary>
    private sealed class RegistryChangingLocalization : ILocalizationSet
    {
        public string LocalizationLanguageCode { get; set; } = "fr";
    }

    /// <summary>
    /// A second type whose language is a constructor argument, which no other test registers.
    /// </summary>
    private sealed class RegistryRepeatedLocalization(string language) : ILocalizationSet
    {
        public string LocalizationLanguageCode { get; } = language;
    }

    /// <summary>
    /// A name no language code can be derived from.
    /// </summary>
    private sealed class RegistryInvalidLocalization1 : ILocalizationSet;

    /// <summary>
    /// A culture whose parent is the invariant culture, as every culture's is under invariant globalization.
    /// </summary>
    private sealed class RegistryInvariantParentCulture(string name) : CultureInfo(name)
    {
        public override CultureInfo Parent => InvariantCulture;
    }

    private static CultureInfo Culture(string name) => CultureInfo.GetCultureInfo(name);
    #endregion

    [Test]
    public void Create_ShouldHoldExactlyGivenSets()
    {
        // Arrange
        var english = new RegistryLocalizationEn();
        var dutch = new RegistryLocalizationNl();

        // Act
        var registry = LocalizationRegistry.Create([english, dutch]);

        // Assert: nothing of the sets of the whole app, in the given order.
        registry.Sets.ShouldBe([english, dutch]);
        registry.Languages.ShouldBe(["en", "nl"], ignoreOrder: true);
        LocalizationRegistry.Create([]).Sets.ShouldBeEmpty();
    }

    [Test]
    public void Create_WithoutLanguages_ShouldHaveNoneWhateverAppDeclares()
    {
        // Arrange & Act
        var registry = LocalizationRegistry.Create([new RegistryLocalizationEn()]);

        // Assert: a registry of one's own does not take the languages of the process.
        registry.AppLanguages.ShouldBeEmpty();
    }

    [Test]
    public void Create_WithLanguages_ShouldKeepThemDefaultFirst()
    {
        // Arrange & Act
        var registry = LocalizationRegistry.Create([new RegistryLocalizationEn()], "nl", "en", "en-GB");

        // Assert
        registry.AppLanguages.ShouldBe(["nl", "en", "en-GB"]);
    }

    [Test]
    public void Create_WithLanguageThatIsNoCultureName_ShouldThrow()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentException>(() => LocalizationRegistry.Create([], "")).ParamName.ShouldBe("defaultLanguage");
        Should.Throw<ArgumentException>(() => LocalizationRegistry.Create([], "en", "nl_BE")).ParamName.ShouldBe("otherLanguages");
        Should.Throw<ArgumentNullException>(() => LocalizationRegistry.Create([], null!));
        Should.Throw<ArgumentNullException>(() => LocalizationRegistry.Create([], "en", null!, "nl"));
    }

    [Test]
    public void Create_WithLanguageListedTwice_ShouldThrow()
    {
        // Arrange & Act & Assert: the same language twice, in two spellings.
        Should.Throw<ArgumentException>(() => LocalizationRegistry.Create([], "en", "nl", "NL")).ParamName.ShouldBe("otherLanguages");
        Should.Throw<ArgumentException>(() => LocalizationRegistry.Create([], "en", "EN")).ParamName.ShouldBe("otherLanguages");
    }

    [Test]
    public void Find_AskedTwiceForOneContract_ShouldGiveSameSetAmongSetsOfOtherContracts()
    {
        // Arrange: the sets of a contract are found once per language and kept.
        var dutch = new RegistryLocalizationNl();
        var replacement = new RegistryReplacementLocalizationNl();
        var registry = LocalizationRegistry.Create([dutch, new RegistryTierDefaultLocalizationNl(), replacement, new RegistryOrderFirstLocalizationNl()]);
        var culture = CultureInfo.GetCultureInfo("nl-NL");

        // Act & Assert
        registry.Find<IRegistryLocalization>(culture).ShouldBeSameAs(replacement);
        registry.Find<IRegistryLocalization>(culture).ShouldBeSameAs(replacement);
        registry.Find<IRegistryTierLocalization>(culture).ShouldBeOfType<RegistryTierDefaultLocalizationNl>();
        registry.Find<IRegistryOtherLocalization>(culture).ShouldBeNull();
    }

    [Test]
    public void Create_ShouldWriteLanguagesAsCultureNamesWhateverCaseSetsGive()
    {
        // Arrange
        var belgian = new RegistryConfiguredLocalization("NL-be");
        var chinese = new RegistryConfiguredLocalization("ZH-hans");

        // Act
        var registry = LocalizationRegistry.Create([belgian, chinese]);

        // Assert: found in any letter case, listed in one.
        registry.Languages.ShouldBe(["nl-BE", "zh-Hans"], ignoreOrder: true);
        registry.Find<ILocalizationSet>(CultureInfo.GetCultureInfo("nl-BE")).ShouldBeSameAs(belgian);
    }

    [Test]
    [Arguments("nl_BE")]
    [Arguments("nl ")]
    [Arguments("n")]
    [Arguments("nl--BE")]
    [Arguments("1nl")]
    public void Register_AndCreate_ShouldThrowForLanguageCodeThatIsNoCultureName(string languageCode)
    {
        // Arrange
        var invalid = new RegistryConfiguredLocalization(languageCode);

        // Act & Assert: such a set would be filed under a language no culture reaches.
        Should.Throw<InvalidOperationException>(() => LocalizationRegistry.Register(invalid)).Message.ShouldContain($"\"{languageCode}\"");
        Should.Throw<InvalidOperationException>(() => LocalizationRegistry.RegisterDefaults(invalid));
        Should.Throw<InvalidOperationException>(() => LocalizationRegistry.Create([invalid]));
    }

    [Test]
    public void Find_ShouldWalkUpToNeutralCulture()
    {
        // Arrange
        var english = new RegistryLocalizationEn();
        var dutch = new RegistryLocalizationNl();
        var registry = LocalizationRegistry.Create([english, dutch]);

        // Act & Assert: nl-BE resolves through nl, en-GB through en; the invariant culture and an unknown language
        // yield null.
        registry.Find<IRegistryLocalization>(Culture("nl-BE")).ShouldBeSameAs(dutch);
        registry.Find<IRegistryLocalization>(Culture("en-GB")).ShouldBeSameAs(english);
        registry.Find<IRegistryLocalization>(Culture("de-DE")).ShouldBeNull();
        registry.Find<IRegistryLocalization>(CultureInfo.InvariantCulture).ShouldBeNull();
    }

    [Test]
    public void Find_ForRegionWithInvariantParent_ShouldFallBackByName()
    {
        // Arrange
        var dutch = new RegistryLocalizationNl();
        var registry = LocalizationRegistry.Create([new RegistryLocalizationEn(), dutch]);

        // Act & Assert: under invariant globalization nl-BE has no parent to walk to, and nl still serves it.
        registry.Find<IRegistryLocalization>(new RegistryInvariantParentCulture("nl-BE")).ShouldBeSameAs(dutch);
    }

    [Test]
    public void FindByLanguage_ShouldMatchLanguageCodeWithoutCultureWalk()
    {
        // Arrange
        var dutch = new RegistryLocalizationNl();
        var registry = LocalizationRegistry.Create([dutch]);

        // Act & Assert: the code in any letter case; a specific culture name is not walked up; a contract no set of the
        // language implements yields nothing.
        registry.FindByLanguage(typeof(IRegistryLocalization), "NL").ShouldBeSameAs(dutch);
        registry.FindByLanguage(typeof(IRegistryLocalization), "nl-BE").ShouldBeNull();
        registry.FindByLanguage(typeof(IRegistryOtherLocalization), "nl").ShouldBeNull();
    }

    [Test]
    public void Find_ShouldLetLastSetWinPerContractAndLanguage()
    {
        // Arrange
        var first = new RegistryLocalizationNl();
        var last = new RegistryReplacementLocalizationNl();
        var other = new RegistryCombinedLocalizationNl();

        // Act
        var registry = LocalizationRegistry.Create([first, last]);
        var withOther = LocalizationRegistry.Create([other, first]);

        // Assert: the last set wins for its contract, and a contract only an earlier set implements still finds that
        // one.
        registry.Find<IRegistryLocalization>(Culture("nl")).ShouldBeSameAs(last);
        withOther.Find<IRegistryLocalization>(Culture("nl")).ShouldBeSameAs(first);
        withOther.Find<IRegistryOtherLocalization>(Culture("nl")).ShouldBeSameAs(other);
    }

    [Test]
    public void Find_ShouldServeEveryContractSetImplements()
    {
        // Arrange
        var combined = new RegistryCombinedLocalizationNl();
        var registry = LocalizationRegistry.Create([combined]);

        // Act & Assert
        registry.Find<IRegistryLocalization>(Culture("nl")).ShouldBeSameAs(combined);
        registry.Find<IRegistryOtherLocalization>(Culture("nl")).ShouldBeSameAs(combined);
        registry.Find(typeof(IRegistryOtherLocalization), Culture("nl")).ShouldBeSameAs(combined);
    }

    [Test]
    public void Find_AndCreate_ShouldThrowForNullArguments()
    {
        // Arrange
        var registry = LocalizationRegistry.Create([]);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => registry.Find(null!, CultureInfo.InvariantCulture));
        Should.Throw<ArgumentNullException>(() => registry.Find(typeof(IRegistryLocalization), null!));
        Should.Throw<ArgumentNullException>(() => LocalizationRegistry.Create(null!));
        Should.Throw<ArgumentNullException>(() => LocalizationRegistry.Create([null!])).ParamName.ShouldBe("sets");
    }

    [Test]
    public void Register_AndCreate_ShouldThrowForNullOrWhiteSpaceLanguageCode()
    {
        // Arrange: a set taking its language from configuration that is missing.
        var withoutLanguage = new RegistryConfiguredLocalization(null!);
        var withBlankLanguage = new RegistryConfiguredLocalization(" ");

        // Act & Assert: at the registration, naming the set, rather than in every text read afterwards; neither tier
        // keeps the set.
        Should.Throw<InvalidOperationException>(() => LocalizationRegistry.Register(withoutLanguage)).Message.ShouldContain(nameof(RegistryConfiguredLocalization));
        Should.Throw<InvalidOperationException>(() => LocalizationRegistry.RegisterDefaults(withBlankLanguage));
        Should.Throw<InvalidOperationException>(() => LocalizationRegistry.Create([withoutLanguage]));
        LocalizationRegistry.Current.Sets.ShouldNotContain(withoutLanguage);
        LocalizationRegistry.Current.Sets.ShouldNotContain(withBlankLanguage);
    }

    [Test]
    public void Create_WithLanguages_ShouldWriteThemAsCultureNames()
    {
        // Arrange & Act
        var registry = LocalizationRegistry.Create([], "EN-gb", "zh-hans");

        // Assert
        registry.AppLanguages.ShouldBe(["en-GB", "zh-Hans"]);
    }

    [Test]
    public void MetadataName_ShouldMatchSourceGeneratorReferences()
    {
        // Assert: the generated module initializers call these by name, so a rename or namespace change must update the
        // generator's emission in the same commit.
        typeof(LocalizationRegistry).FullName.ShouldBe("CodeChops.Idioma.LocalizationRegistry");
        typeof(LocalizationRegistry).GetMember(nameof(LocalizationRegistry.Register)).ShouldNotBeEmpty();
        typeof(LocalizationRegistry).GetMember(nameof(LocalizationRegistry.RegisterDefaults)).ShouldNotBeEmpty();
    }
}
