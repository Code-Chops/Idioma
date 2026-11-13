namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="TranslationsRegistry"/>: a registry holding exactly the given sets, lookup by contract with the
/// culture walk, and the sets of the whole app with their two tiers. The sets of the whole app are process-wide, so
/// every test registering there uses a contract of its own, and runs apart from the test that compares
/// <see cref="TranslationsRegistry.Current"/> by reference.
/// </summary>
public sealed class TranslationsRegistryTests
{
    #region Fixture
    private interface IRegistryTranslations : ITranslations;

    private sealed class RegistryTranslationsEN : IRegistryTranslations;

    private sealed class RegistryTranslationsNL : IRegistryTranslations;

    private sealed class RegistryReplacementTranslationsNL : IRegistryTranslations;

    /// <summary>
    /// A contract no set in <see cref="RegistryTranslationsNL"/>'s family implements.
    /// </summary>
    private interface IRegistryOtherTranslations : ITranslations;

    /// <summary>
    /// A set serving two contracts at once.
    /// </summary>
    private sealed class RegistryCombinedTranslationsNL : IRegistryTranslations, IRegistryOtherTranslations;

    private interface IRegistryTierTranslations : ITranslations;

    private sealed class RegistryTierDefaultTranslationsNL : IRegistryTierTranslations;

    private sealed class RegistryTierOverrideTranslationsNL : IRegistryTierTranslations;

    private interface IRegistryOrderTranslations : ITranslations;

    private sealed class RegistryOrderFirstTranslationsNL : IRegistryOrderTranslations;

    private sealed class RegistryOrderSecondTranslationsNL : IRegistryOrderTranslations;

    private interface IRegistryGenericTranslations : ITranslations;

    private sealed class RegistryGenericTranslationsNL : IRegistryGenericTranslations;

    /// <summary>
    /// A set whose language is a constructor argument, so two instances of the type can serve two languages.
    /// </summary>
    private sealed class RegistryConfiguredTranslations(string language) : ITranslations
    {
        public string LanguageCode { get; } = language;
    }

    /// <summary>
    /// A name no language code can be derived from.
    /// </summary>
    private sealed class RegistryInvalidTranslations1 : ITranslations;

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
        var english = new RegistryTranslationsEN();
        var dutch = new RegistryTranslationsNL();

        // Act
        var registry = TranslationsRegistry.Create([english, dutch]);

        // Assert: nothing of the sets of the whole app, in the given order.
        registry.Sets.ShouldBe([english, dutch]);
        registry.Languages.ShouldBe(["en", "nl"], ignoreOrder: true);
        TranslationsRegistry.Create([]).Sets.ShouldBeEmpty();
    }

    [Test]
    public void Create_WithoutLanguages_ShouldHaveNoneWhateverAppDeclares()
    {
        // Arrange & Act
        var registry = TranslationsRegistry.Create([new RegistryTranslationsEN()]);

        // Assert: a registry of one's own does not take the languages of the process.
        registry.AppLanguages.ShouldBeEmpty();
    }

    [Test]
    public void Create_WithLanguages_ShouldKeepThemDefaultFirst()
    {
        // Arrange & Act
        var registry = TranslationsRegistry.Create([new RegistryTranslationsEN()], "nl", "en", "en-GB");

        // Assert
        registry.AppLanguages.ShouldBe(["nl", "en", "en-GB"]);
    }

    [Test]
    public void Create_WithLanguageThatIsNoCultureName_ShouldThrow()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentException>(() => TranslationsRegistry.Create([], "")).ParamName.ShouldBe("defaultLanguage");
        Should.Throw<ArgumentException>(() => TranslationsRegistry.Create([], "en", "nl_BE")).ParamName.ShouldBe("otherLanguages");
        Should.Throw<ArgumentNullException>(() => TranslationsRegistry.Create([], null!));
        Should.Throw<ArgumentNullException>(() => TranslationsRegistry.Create([], "en", null!, "nl"));
    }

    [Test]
    public void Find_AskedTwiceForOneContract_ShouldGiveSameSetAmongSetsOfOtherContracts()
    {
        // Arrange: the sets of a contract are found once per language and kept.
        var dutch = new RegistryTranslationsNL();
        var replacement = new RegistryReplacementTranslationsNL();
        var registry = TranslationsRegistry.Create([dutch, new RegistryTierDefaultTranslationsNL(), replacement, new RegistryOrderFirstTranslationsNL()]);
        var culture = CultureInfo.GetCultureInfo("nl-NL");

        // Act & Assert
        registry.Find<IRegistryTranslations>(culture).ShouldBeSameAs(replacement);
        registry.Find<IRegistryTranslations>(culture).ShouldBeSameAs(replacement);
        registry.Find<IRegistryTierTranslations>(culture).ShouldBeOfType<RegistryTierDefaultTranslationsNL>();
        registry.Find<IRegistryOtherTranslations>(culture).ShouldBeNull();
    }

    [Test]
    public void Create_ShouldWriteLanguagesAsCultureNamesWhateverCaseSetsGive()
    {
        // Arrange
        var belgian = new RegistryConfiguredTranslations("NL-be");
        var chinese = new RegistryConfiguredTranslations("ZH-hans");

        // Act
        var registry = TranslationsRegistry.Create([belgian, chinese]);

        // Assert: found in any letter case, listed in one.
        registry.Languages.ShouldBe(["nl-BE", "zh-Hans"], ignoreOrder: true);
        registry.Find<ITranslations>(CultureInfo.GetCultureInfo("nl-BE")).ShouldBeSameAs(belgian);
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
        var invalid = new RegistryConfiguredTranslations(languageCode);

        // Act & Assert: such a set would be filed under a language no culture reaches.
        Should.Throw<InvalidOperationException>(() => TranslationsRegistry.Register(invalid)).Message.ShouldContain($"\"{languageCode}\"");
        Should.Throw<InvalidOperationException>(() => TranslationsRegistry.RegisterDefaults(invalid));
        Should.Throw<InvalidOperationException>(() => TranslationsRegistry.Create([invalid]));
    }

    [Test]
    public void Find_ShouldWalkUpToNeutralCulture()
    {
        // Arrange
        var english = new RegistryTranslationsEN();
        var dutch = new RegistryTranslationsNL();
        var registry = TranslationsRegistry.Create([english, dutch]);

        // Act & Assert: nl-BE resolves through nl, en-GB through en; the invariant culture and an unknown language
        // yield null.
        registry.Find<IRegistryTranslations>(Culture("nl-BE")).ShouldBeSameAs(dutch);
        registry.Find<IRegistryTranslations>(Culture("en-GB")).ShouldBeSameAs(english);
        registry.Find<IRegistryTranslations>(Culture("de-DE")).ShouldBeNull();
        registry.Find<IRegistryTranslations>(CultureInfo.InvariantCulture).ShouldBeNull();
    }

    [Test]
    public void Find_ForRegionWithInvariantParent_ShouldFallBackByName()
    {
        // Arrange
        var dutch = new RegistryTranslationsNL();
        var registry = TranslationsRegistry.Create([new RegistryTranslationsEN(), dutch]);

        // Act & Assert: under invariant globalization nl-BE has no parent to walk to, and nl still serves it.
        registry.Find<IRegistryTranslations>(new RegistryInvariantParentCulture("nl-BE")).ShouldBeSameAs(dutch);
    }

    [Test]
    public void FindByLanguage_ShouldMatchLanguageCodeWithoutCultureWalk()
    {
        // Arrange
        var dutch = new RegistryTranslationsNL();
        var registry = TranslationsRegistry.Create([dutch]);

        // Act & Assert: the code in any letter case; a specific culture name is not walked up; a contract no set of the
        // language implements yields nothing.
        registry.FindByLanguage(typeof(IRegistryTranslations), "NL").ShouldBeSameAs(dutch);
        registry.FindByLanguage(typeof(IRegistryTranslations), "nl-BE").ShouldBeNull();
        registry.FindByLanguage(typeof(IRegistryOtherTranslations), "nl").ShouldBeNull();
    }

    [Test]
    public void Find_ShouldLetLastSetWinPerContractAndLanguage()
    {
        // Arrange
        var first = new RegistryTranslationsNL();
        var last = new RegistryReplacementTranslationsNL();
        var other = new RegistryCombinedTranslationsNL();

        // Act
        var registry = TranslationsRegistry.Create([first, last]);
        var withOther = TranslationsRegistry.Create([other, first]);

        // Assert: the last set wins for its contract, and a contract only an earlier set implements still finds that
        // one.
        registry.Find<IRegistryTranslations>(Culture("nl")).ShouldBeSameAs(last);
        withOther.Find<IRegistryTranslations>(Culture("nl")).ShouldBeSameAs(first);
        withOther.Find<IRegistryOtherTranslations>(Culture("nl")).ShouldBeSameAs(other);
    }

    [Test]
    public void Find_ShouldServeEveryContractSetImplements()
    {
        // Arrange
        var combined = new RegistryCombinedTranslationsNL();
        var registry = TranslationsRegistry.Create([combined]);

        // Act & Assert
        registry.Find<IRegistryTranslations>(Culture("nl")).ShouldBeSameAs(combined);
        registry.Find<IRegistryOtherTranslations>(Culture("nl")).ShouldBeSameAs(combined);
        registry.Find(typeof(IRegistryOtherTranslations), Culture("nl")).ShouldBeSameAs(combined);
    }

    [Test]
    public void Find_AndCreate_ShouldThrowForNullArguments()
    {
        // Arrange
        var registry = TranslationsRegistry.Create([]);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => registry.Find(null!, CultureInfo.InvariantCulture));
        Should.Throw<ArgumentNullException>(() => registry.Find(typeof(IRegistryTranslations), null!));
        Should.Throw<ArgumentNullException>(() => TranslationsRegistry.Create(null!));
    }

    [Test]
    [NotInParallel(nameof(TranslationsRegistry))]
    public void Register_ShouldWinOverRegisterDefaultsWhicheverComesFirst()
    {
        // Arrange: an app's override registered before the library it rewords has loaded, which is the order module
        // initializers can run in.
        var appOverride = new RegistryTierOverrideTranslationsNL();
        var libraryDefault = new RegistryTierDefaultTranslationsNL();

        // Act
        TranslationsRegistry.Register(appOverride);
        TranslationsRegistry.RegisterDefaults(libraryDefault);

        // Assert
        var current = TranslationsRegistry.Current;
        current.Find<IRegistryTierTranslations>(Culture("nl-NL")).ShouldBeSameAs(appOverride);
        current.Sets.IndexOf(libraryDefault).ShouldBeLessThan(current.Sets.IndexOf(appOverride));
    }

    [Test]
    [NotInParallel(nameof(TranslationsRegistry))]
    public void Register_ShouldLetLastRegistrationWinAndReplaceSameType()
    {
        // Arrange
        var first = new RegistryOrderFirstTranslationsNL();
        var second = new RegistryOrderSecondTranslationsNL();
        var firstAgain = new RegistryOrderFirstTranslationsNL();

        // Act & Assert: the later set wins; registering a type again replaces its instance and makes it the last.
        TranslationsRegistry.Register(first, second);
        TranslationsRegistry.Current.Find<IRegistryOrderTranslations>(Culture("nl")).ShouldBeSameAs(second);

        TranslationsRegistry.Register(firstAgain);
        var current = TranslationsRegistry.Current;
        current.Find<IRegistryOrderTranslations>(Culture("nl")).ShouldBeSameAs(firstAgain);
        current.Sets.Count(set => set is RegistryOrderFirstTranslationsNL).ShouldBe(1);
    }

    [Test]
    [NotInParallel(nameof(TranslationsRegistry))]
    public void Register_ShouldKeepInstancesOfOneTypeForDifferentLanguages()
    {
        // Arrange
        var german = new RegistryConfiguredTranslations("de");
        var french = new RegistryConfiguredTranslations("fr");

        // Act
        TranslationsRegistry.Register(german, french);

        // Assert
        var current = TranslationsRegistry.Current;
        current.Sets.ShouldContain(german);
        current.Sets.ShouldContain(french);
    }

    [Test]
    [NotInParallel(nameof(TranslationsRegistry))]
    public void Register_WithTypeArgument_ShouldCreateAndRegisterSet()
    {
        // Arrange & Act
        TranslationsRegistry.Register<RegistryGenericTranslationsNL>();

        // Assert
        TranslationsRegistry.Current.Find<IRegistryGenericTranslations>(Culture("nl")).ShouldBeOfType<RegistryGenericTranslationsNL>();
    }

    [Test]
    public void Register_ShouldThrowAtRegistrationForSetWithoutLanguageCode()
    {
        // Arrange
        var invalid = new RegistryInvalidTranslations1();

        // Act & Assert: both tiers fail here, not at the first text rendered afterwards, and neither keeps the set.
        Should.Throw<InvalidOperationException>(() => TranslationsRegistry.Register(invalid));
        Should.Throw<InvalidOperationException>(() => TranslationsRegistry.RegisterDefaults(invalid));
        TranslationsRegistry.Current.Sets.ShouldNotContain(invalid);
    }

    [Test]
    public void Register_ShouldThrowForNullSet()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentNullException>(() => TranslationsRegistry.Register([null!]));
        Should.Throw<ArgumentNullException>(() => TranslationsRegistry.RegisterDefaults([null!]));
    }

    [Test]
    public void Register_AndCreate_ShouldThrowForNullOrWhiteSpaceLanguageCode()
    {
        // Arrange: a set taking its language from configuration that is missing.
        var withoutLanguage = new RegistryConfiguredTranslations(null!);
        var withBlankLanguage = new RegistryConfiguredTranslations(" ");

        // Act & Assert: at the registration, naming the set, rather than in every text read afterwards; neither tier
        // keeps the set.
        Should.Throw<InvalidOperationException>(() => TranslationsRegistry.Register(withoutLanguage)).Message.ShouldContain(nameof(RegistryConfiguredTranslations));
        Should.Throw<InvalidOperationException>(() => TranslationsRegistry.RegisterDefaults(withBlankLanguage));
        Should.Throw<InvalidOperationException>(() => TranslationsRegistry.Create([withoutLanguage]));
        TranslationsRegistry.Current.Sets.ShouldNotContain(withoutLanguage);
        TranslationsRegistry.Current.Sets.ShouldNotContain(withBlankLanguage);
    }

    [Test]
    [NotInParallel(nameof(TranslationsRegistry))]
    public void Current_ShouldStayTheSameInstanceUntilNextRegistration()
    {
        // Arrange
        var before = TranslationsRegistry.Current;

        // Act
        var unchanged = TranslationsRegistry.Current;
        TranslationsRegistry.Register(new RegistryConfiguredTranslations("it"));
        var changed = TranslationsRegistry.Current;

        // Assert: the lookup tables are built once per change, since every text reads this on every render.
        unchanged.ShouldBeSameAs(before);
        changed.ShouldNotBeSameAs(before);
    }

    [Test]
    [NotInParallel(nameof(TranslationsRegistry))]
    public void RegisterLanguages_ShouldKeepFirstRegistration()
    {
        // Arrange: this test project's own registrar registered its languages when it started.
        var registered = TranslationsRegistry.SupportedLanguages;
        registered.ShouldNotBeEmpty();

        // Act: what the registrar of an application this one references would do afterwards.
        TranslationsRegistry.RegisterLanguages("de", "fr");

        // Assert
        TranslationsRegistry.SupportedLanguages.ShouldBe(registered);
        TranslationsRegistry.DefaultLanguage.ShouldBe(registered[0]);
    }

    [Test]
    public void RegisterLanguages_ShouldThrowForLanguageThatIsNoCultureName()
    {
        // Arrange & Act & Assert: checked before the first-call rule, so also once the languages are registered.
        Should.Throw<ArgumentException>(() => TranslationsRegistry.RegisterLanguages(" "));
        Should.Throw<ArgumentException>(() => TranslationsRegistry.RegisterLanguages("en", "nl", " "));
        Should.Throw<ArgumentException>(() => TranslationsRegistry.RegisterLanguages("nl_BE")).ParamName.ShouldBe("defaultLanguage");
        Should.Throw<ArgumentException>(() => TranslationsRegistry.RegisterLanguages("en", "nl_BE")).ParamName.ShouldBe("otherLanguages");
    }

    [Test]
    public void Create_WithLanguages_ShouldWriteThemAsCultureNames()
    {
        // Arrange & Act
        var registry = TranslationsRegistry.Create([], "EN-gb", "zh-hans");

        // Assert
        registry.AppLanguages.ShouldBe(["en-GB", "zh-Hans"]);
    }

    [Test]
    public void MetadataName_ShouldMatchSourceGeneratorReferences()
    {
        // Assert: the generated module initializers call these by name, so a rename or namespace change must update the
        // generator's emission in the same commit.
        typeof(TranslationsRegistry).FullName.ShouldBe("CodeChops.Idioma.TranslationsRegistry");
        typeof(TranslationsRegistry).GetMember(nameof(TranslationsRegistry.Register)).ShouldNotBeEmpty();
        typeof(TranslationsRegistry).GetMember(nameof(TranslationsRegistry.RegisterDefaults)).ShouldNotBeEmpty();
    }
}
