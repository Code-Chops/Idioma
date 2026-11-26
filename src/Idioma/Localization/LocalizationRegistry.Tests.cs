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

    private interface IRegistryLazyLocalization : ILocalizationSet;

    /// <summary>
    /// A set that counts how often it's created, to show when the registry creates a set it registered by its type.
    /// </summary>
    private sealed class RegistryLazyLocalizationNl : IRegistryLazyLocalization
    {
        public static int Created;

        public RegistryLazyLocalizationNl() => Interlocked.Increment(ref Created);
    }

    private interface IRegistrySteadyLocalization : ILocalizationSet;

    private sealed class RegistrySteadyLocalizationNl : IRegistrySteadyLocalization;

    private interface IRegistryFlakyLocalization : ILocalizationSet;

    /// <summary>
    /// A set whose constructor throws as often as <see cref="FailuresLeft"/> says, as one that reads a setting that isn't
    /// loaded yet at the start of an app.
    /// </summary>
    private sealed class RegistryFlakyLocalizationNl : IRegistryFlakyLocalization
    {
        public static int FailuresLeft;

        public RegistryFlakyLocalizationNl()
        {
            if (Interlocked.Decrement(ref FailuresLeft) >= 0)
                throw new TimeoutException("The settings aren't loaded yet.");
        }
    }

    private interface IRegistryFlakyCodedLocalization : ILocalizationSet;

    /// <summary>
    /// The same, for a set that gives its language itself, which the registry creates to file it under its language.
    /// </summary>
    private sealed class RegistryFlakyCodedLocalization : IRegistryFlakyCodedLocalization
    {
        public static int FailuresLeft;

        public RegistryFlakyCodedLocalization()
        {
            if (Interlocked.Decrement(ref FailuresLeft) >= 0)
                throw new TimeoutException("The settings aren't loaded yet.");
        }

        string ILocalizationSet.LocalizationLanguageCode => "nl";
    }

    /// <summary>
    /// The one text of the contracts below, which a set reads while it's created.
    /// </summary>
    private interface IRegistryBrandTexts
    {
        string Brand { get; }
    }

    /// <summary>
    /// Reads the text as a property of a generated class reads it.
    /// </summary>
    private static string ReadBrand<TContract, TDefaultSet>()
        where TContract : class, IRegistryBrandTexts
        where TDefaultSet : class, ILocalizationSet, TContract, new()
    {
        var brand = Internals.GeneratedCode.GetSet<TContract>(0, 1)?.Brand
            ?? Internals.GeneratedCode.ResolveWording<TContract, TDefaultSet>(0, 1, "Brand", static set => set.Brand);

        return brand;
    }

    private interface IRegistrySelfReadingLocalization : ILocalizationSet, IRegistryBrandTexts;

    /// <summary>
    /// The set the text takes its default wording from. Never registered.
    /// </summary>
    private sealed class RegistrySelfReadingLocalizationEn : IRegistrySelfReadingLocalization
    {
        public string Brand => "Shop";
    }

    /// <summary>
    /// A set with a field initializer that reads a text of its own contract, on the thread that sets
    /// <see cref="ReadsOwnText"/>. Per thread, so that a test running beside it creates the set without failing.
    /// </summary>
    private sealed class RegistrySelfReadingLocalizationNl : IRegistrySelfReadingLocalization
    {
        [ThreadStatic]
        public static bool ReadsOwnText;

        private readonly string _brand = ReadsOwnText ? ReadBrand<IRegistrySelfReadingLocalization, RegistrySelfReadingLocalizationEn>() : "Winkel";

        public string Brand => this._brand;
    }

    private interface IRegistrySelfReadingDefaultLocalization : ILocalizationSet, IRegistryBrandTexts;

    /// <summary>
    /// The same, for the set a text takes its default wording from, which no registry holds.
    /// </summary>
    private sealed class RegistrySelfReadingDefaultLocalizationEn : IRegistrySelfReadingDefaultLocalization
    {
        [ThreadStatic]
        public static bool ReadsOwnText;

        private readonly string _brand = ReadsOwnText ? ReadBrand<IRegistrySelfReadingDefaultLocalization, RegistrySelfReadingDefaultLocalizationEn>() : "Shop";

        public string Brand => this._brand;
    }

    private interface IRegistrySelfReadingCodedLocalization : ILocalizationSet, IRegistryBrandTexts;

    private sealed class RegistrySelfReadingCodedLocalizationEn : IRegistrySelfReadingCodedLocalization
    {
        public string Brand => "Shop";
    }

    /// <summary>
    /// The same, for a set that gives its language itself, which the registry creates when it's built.
    /// </summary>
    private sealed class RegistrySelfReadingCodedLocalization : IRegistrySelfReadingCodedLocalization
    {
        [ThreadStatic]
        public static bool ReadsOwnText;

        private readonly string _brand = ReadsOwnText ? ReadBrand<IRegistrySelfReadingCodedLocalization, RegistrySelfReadingCodedLocalizationEn>() : "Winkel";

        public string Brand => this._brand;

        string ILocalizationSet.LocalizationLanguageCode => "nl";
    }

    private interface IRegistryReadLocalization : ILocalizationSet, IRegistryBrandTexts;

    private sealed class RegistryReadLocalizationEn : IRegistryReadLocalization
    {
        public string Brand => "Shop";
    }

    private sealed class RegistryReadLocalizationNl : IRegistryReadLocalization
    {
        public string Brand => "Winkel";
    }

    private interface IRegistryReaderLocalization : ILocalizationSet, IRegistryBrandTexts;

    /// <summary>
    /// A set that gives its language itself, with a field initializer that reads a text of another contract.
    /// </summary>
    private sealed class RegistryReaderCodedLocalization : IRegistryReaderLocalization
    {
        public string Brand { get; } = ReadBrand<IRegistryReadLocalization, RegistryReadLocalizationEn>();

        string ILocalizationSet.LocalizationLanguageCode => "nl";
    }

    /// <summary>
    /// A culture that counts how often its name is read, which a lookup by name does and a comparison by reference
    /// doesn't.
    /// </summary>
    private sealed class RegistryCountingCulture(string name) : CultureInfo(name)
    {
        public int NameReads { get; private set; }

        public override string Name
        {
            get
            {
                this.NameReads++;
                return base.Name;
            }
        }
    }

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
        registry.AllSets.ShouldBe([english, dutch]);
        registry.SetLanguages.ShouldBe(["en", "nl"]);
        LocalizationRegistry.Create([]).AllSets.ShouldBeEmpty();
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
    public void GetByCulture_AskedTwiceForOneContract_ShouldGiveSameSetAmongSetsOfOtherContracts()
    {
        // Arrange: the sets of a contract are found once per language and kept.
        var dutch = new RegistryLocalizationNl();
        var replacement = new RegistryReplacementLocalizationNl();
        var registry = LocalizationRegistry.Create([dutch, new RegistryTierDefaultLocalizationNl(), replacement, new RegistryOrderFirstLocalizationNl()]);
        var culture = CultureInfo.GetCultureInfo("nl-NL");

        // Act & Assert
        registry.GetByCulture<IRegistryLocalization>(culture).ShouldBeSameAs(replacement);
        registry.GetByCulture<IRegistryLocalization>(culture).ShouldBeSameAs(replacement);
        registry.GetByCulture<IRegistryTierLocalization>(culture).ShouldBeOfType<RegistryTierDefaultLocalizationNl>();
        registry.GetByCulture<IRegistryOtherLocalization>(culture).ShouldBeNull();
    }

    [Test]
    public void Create_ShouldWriteLanguagesAsCultureNamesWhateverCaseSetsGive()
    {
        // Arrange
        var belgian = new RegistryConfiguredLocalization("NL-be");
        var chinese = new RegistryConfiguredLocalization("ZH-hans");

        // Act
        var registry = LocalizationRegistry.Create([chinese, belgian]);

        // Assert: found in any letter case, listed in one, in ordinal order whatever order the sets registered in.
        registry.SetLanguages.ShouldBe(["nl-BE", "zh-Hans"]);
        registry.GetByCulture<ILocalizationSet>(CultureInfo.GetCultureInfo("nl-BE")).ShouldBeSameAs(belgian);
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
        Should.Throw<InvalidOperationException>(() => LocalizationRegistry.Register(invalid)).Message.ShouldContain($"'{languageCode}'");
        Should.Throw<InvalidOperationException>(() => LocalizationRegistry.RegisterDefaults(invalid));
        Should.Throw<InvalidOperationException>(() => LocalizationRegistry.Create([invalid]));
    }

    [Test]
    public void GetByCulture_ShouldWalkUpToNeutralCulture()
    {
        // Arrange
        var english = new RegistryLocalizationEn();
        var dutch = new RegistryLocalizationNl();
        var registry = LocalizationRegistry.Create([english, dutch]);

        // Act & Assert: nl-BE resolves through nl, en-GB through en; the invariant culture and an unknown language
        // yield null.
        registry.GetByCulture<IRegistryLocalization>(Culture("nl-BE")).ShouldBeSameAs(dutch);
        registry.GetByCulture<IRegistryLocalization>(Culture("en-GB")).ShouldBeSameAs(english);
        registry.GetByCulture<IRegistryLocalization>(Culture("de-DE")).ShouldBeNull();
        registry.GetByCulture<IRegistryLocalization>(CultureInfo.InvariantCulture).ShouldBeNull();
    }

    [Test]
    public void GetByCulture_ForRegionWithInvariantParent_ShouldFallBackByName()
    {
        // Arrange
        var dutch = new RegistryLocalizationNl();
        var registry = LocalizationRegistry.Create([new RegistryLocalizationEn(), dutch]);

        // Act & Assert: under invariant globalization nl-BE has no parent to walk to, and nl still serves it.
        registry.GetByCulture<IRegistryLocalization>(new RegistryInvariantParentCulture("nl-BE")).ShouldBeSameAs(dutch);
    }

    [Test]
    public void GetByCulture_ShouldLetLastSetWinPerContractAndLanguage()
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
        registry.GetByCulture<IRegistryLocalization>(Culture("nl")).ShouldBeSameAs(last);
        withOther.GetByCulture<IRegistryLocalization>(Culture("nl")).ShouldBeSameAs(first);
        withOther.GetByCulture<IRegistryOtherLocalization>(Culture("nl")).ShouldBeSameAs(other);
    }

    [Test]
    public void GetByCulture_ShouldServeEveryContractSetImplements()
    {
        // Arrange
        var combined = new RegistryCombinedLocalizationNl();
        var registry = LocalizationRegistry.Create([combined]);

        // Act & Assert
        registry.GetByCulture<IRegistryLocalization>(Culture("nl")).ShouldBeSameAs(combined);
        registry.GetByCulture<IRegistryOtherLocalization>(Culture("nl")).ShouldBeSameAs(combined);
        registry.GetByCulture(typeof(IRegistryOtherLocalization), Culture("nl")).ShouldBeSameAs(combined);
    }

    [Test]
    public void GetByCulture_AndCreate_ShouldThrowForNullArguments()
    {
        // Arrange
        var registry = LocalizationRegistry.Create([]);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => registry.GetByCulture(null!, CultureInfo.InvariantCulture));
        Should.Throw<ArgumentNullException>(() => registry.GetByCulture(typeof(IRegistryLocalization), null!));
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
        LocalizationRegistry.Sets.ShouldNotContain(withoutLanguage);
        LocalizationRegistry.Sets.ShouldNotContain(withBlankLanguage);
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
    }
}
