namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="ResourceText"/>: the key, the translations contract a text is declared against, the fallback to
/// the default language, what a text shows through the sets of the whole app, and the equality that includes key and
/// contract.
/// </summary>
public sealed class ResourceTextTests
{
    #region Fixture
    private interface IResourceTranslations : ITranslations
    {
        string Greeting { get; }
        string Farewell { get; }
    }

    private class ResourceTranslationsEN : IResourceTranslations
    {
        public virtual string Greeting => "Hello";
        public virtual string Farewell => "Goodbye";
    }

    private sealed class ResourceTranslationsNL : IResourceTranslations
    {
        public string Greeting => "Hallo";
        public string Farewell => "Tot ziens";
    }

    /// <summary>
    /// An English set rewording one text of the default language.
    /// </summary>
    private sealed class ResourceRewordedTranslationsEN : ResourceTranslationsEN
    {
        public override string Greeting => "Hi";
    }

    /// <summary>
    /// A second English set rewording another text, as a shared library's rewording next to an app's.
    /// </summary>
    private sealed class ResourceSecondRewordedTranslationsEN : ResourceTranslationsEN
    {
        public override string Farewell => "Bye";
    }

    /// <summary>
    /// An English set with every text of its own, deriving from no English set, though one text matches the default
    /// wording.
    /// </summary>
    private sealed class ResourceStandaloneEnglishTranslations : IResourceTranslations
    {
        public string LanguageCode => "en";
        public string Greeting => "Hello";
        public string Farewell => "So long";
    }

    /// <summary>
    /// A set of another contract.
    /// </summary>
    private sealed class ResourceOtherTranslationsNL : ITranslations;

    private interface IResourceRegisteredTranslations : ITranslations
    {
        string Greeting { get; }
    }

    private sealed class ResourceRegisteredTranslationsEN : IResourceRegisteredTranslations
    {
        public string Greeting => "Hello";
    }

    private sealed class ResourceRegisteredTranslationsNL : IResourceRegisteredTranslations
    {
        public string Greeting => "Hallo";
    }

    /// <summary>
    /// A library's British set, deriving from its English set and wording one text its own way.
    /// </summary>
    private sealed class ResourceBritishTranslations : ResourceTranslationsEN, ITranslations
    {
        string ITranslations.LanguageCode => "en-GB";

        public override string Farewell => "Cheerio";
    }

    /// <summary>
    /// A British set with every text of its own, deriving from no English set, though one text matches the English
    /// wording.
    /// </summary>
    private sealed class ResourceStandaloneBritishTranslations : IResourceTranslations
    {
        public string LanguageCode => "en-GB";
        public string Greeting => "Hello";
        public string Farewell => "Cheerio";
    }

    /// <summary>
    /// Three levels of one language, each deriving from the one above (zh, zh-Hant, zh-Hant-TW); the middle one words
    /// one text its own way.
    /// </summary>
    private class ResourceChineseTranslations : IResourceTranslations
    {
        public virtual string LanguageCode => "zh";
        public virtual string Greeting => "Ni hao";
        public virtual string Farewell => "Zaijian";
    }

    private class ResourceTraditionalChineseTranslations : ResourceChineseTranslations
    {
        public override string LanguageCode => "zh-Hant";
        public override string Farewell => "Zaijian (Hant)";
    }

    private sealed class ResourceTaiwaneseTranslations : ResourceTraditionalChineseTranslations
    {
        public override string LanguageCode => "zh-Hant-TW";
    }

    /// <summary>
    /// An app's rewording of the top level.
    /// </summary>
    private sealed class ResourceRewordedChineseTranslations : ResourceChineseTranslations
    {
        public override string Greeting => "Nin hao";
    }

    private static ResourceText Greeting { get; } = ResourceText.Create<IResourceTranslations>("Resource.Greeting", t => t.Greeting, new ResourceTranslationsEN());

    private static ResourceText Farewell { get; } = ResourceText.Create<IResourceTranslations>("Resource.Farewell", t => t.Farewell, new ResourceTranslationsEN());
    #endregion

    [Test]
    public void GetTranslation_ShouldPhraseTextInGivenSet()
    {
        // Arrange & Act & Assert: a set of the contract selects its member; a set of another contract, and a key-only
        // text, are outside the contract.
        Greeting.GetTranslation(new ResourceTranslationsNL()).ShouldBe("Hallo");
        Greeting.GetTranslation(new ResourceTranslationsEN()).ShouldBe("Hello");
        Greeting.GetTranslation(new ResourceOtherTranslationsNL()).ShouldBeNull();
        new ResourceText("MyApp.Greeting", "Hello").GetTranslation(new ResourceTranslationsNL()).ShouldBeNull();
        Should.Throw<ArgumentNullException>(() => Greeting.GetTranslation((ITranslations)null!));
    }

    [Test]
    public void Create_WithDefaults_ShouldTakeDefaultTextAndLanguageFromSet()
    {
        // Arrange & Act
        var text = Greeting;

        // Assert
        text.Key.ShouldBe("Resource.Greeting");
        text.DefaultText.ShouldBe("Hello");
        text.Contract.ShouldBe(typeof(IResourceTranslations));
        text.DefaultLanguage.ShouldBe("en");
    }

    [Test]
    public void ToString_FromRegistryWithoutLanguages_ShouldShowEveryLanguageWithASet()
    {
        // Arrange: no languages given, so nothing of what the app of this process declares applies.
        var registry = TranslationsRegistry.Create([new ResourceTranslationsNL()]);

        // Act & Assert: the Dutch set shows, and a culture without a set gets the text's own default wording.
        Greeting.ToString(CultureInfo.GetCultureInfo("nl-BE"), registry).ShouldBe("Hallo");
        Greeting.ToString(CultureInfo.GetCultureInfo("fr-FR"), registry).ShouldBe("Hello");
    }

    [Test]
    public void ToString_FromRegistryWithLanguages_ShouldShowOnlyThoseAndFallBackToItsDefault()
    {
        // Arrange: a registry that has Dutch, with and without Dutch among its languages.
        ITranslations[] sets = [new ResourceTranslationsEN(), new ResourceTranslationsNL()];
        var dutchFirst = TranslationsRegistry.Create(sets, "nl", "en");
        var withoutDutch = TranslationsRegistry.Create(sets, "en", "fr");

        // Act & Assert: a culture no language serves gets the registry's default language, even where it has a set.
        Greeting.ToString(CultureInfo.GetCultureInfo("fr-FR"), dutchFirst).ShouldBe("Hallo");
        Greeting.ToString(CultureInfo.GetCultureInfo("en-GB"), dutchFirst).ShouldBe("Hello");
        Greeting.ToString(CultureInfo.GetCultureInfo("nl-NL"), withoutDutch).ShouldBe("Hello");
    }

    [Test]
    public void Create_WithExplicitDefault_ShouldRecordNoDefaultLanguage()
    {
        // Arrange & Act
        var text = ResourceText.Create<IResourceTranslations>("Resource.Farewell", "Goodbye", t => t.Farewell);

        // Assert: no set supplied the default, so no language is recorded as the fallback.
        text.DefaultText.ShouldBe("Goodbye");
        text.Contract.ShouldBe(typeof(IResourceTranslations));
        text.DefaultLanguage.ShouldBeNull();
        text.GetTranslation(new ResourceTranslationsNL()).ShouldBe("Tot ziens");
    }

    [Test]
    public void Create_ShouldThrowForInvalidArguments()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentException>(() => ResourceText.Create<IResourceTranslations>("", "x", t => t.Greeting));
        Should.Throw<ArgumentException>(() => ResourceText.Create<IResourceTranslations>(" ", t => t.Greeting, new ResourceTranslationsEN()));
        Should.Throw<ArgumentNullException>(() => ResourceText.Create<IResourceTranslations>("K", null!, t => t.Greeting));
        Should.Throw<ArgumentNullException>(() => ResourceText.Create<IResourceTranslations>("K", "x", selector: null!));
        Should.Throw<ArgumentNullException>(() => ResourceText.Create<IResourceTranslations>(null!, "x", t => t.Greeting));
        Should.Throw<ArgumentNullException>(() => ResourceText.Create("K", t => t.Greeting, defaults: (IResourceTranslations)null!));
    }

    [Test]
    public void KeyOnlyText_ShouldShowDefaultTextWhateverIsRegistered()
    {
        // Arrange
        var text = new ResourceText("MyApp.Greeting", "Hello");
        var registry = TranslationsRegistry.Create([new ResourceTranslationsNL()]);

        // Act & Assert: this package never reads the key, and a text without a contract matches no set.
        text.Key.ShouldBe("MyApp.Greeting");
        text.Contract.ShouldBeNull();
        text.Translations.ShouldBeEmpty();
        text.ToString(CultureInfo.GetCultureInfo("nl-NL"), registry).ShouldBe("Hello");
    }

    [Test]
    public void Constructor_ShouldThrowForInvalidArguments()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentNullException>(() => new ResourceText(key: null!, "Hello"));
        Should.Throw<ArgumentException>(() => new ResourceText("", "Hello"));
        Should.Throw<ArgumentException>(() => new ResourceText(" ", "Hello"));
        Should.Throw<ArgumentNullException>(() => new ResourceText("MyApp.Greeting", defaultText: null!));
    }

    [Test]
    public void WithTranslation_ShouldKeepKeyAndContract()
    {
        // Arrange & Act
        var text = Greeting.WithTranslation("fr", "Bonjour");

        // Assert: still a resource text of the same contract, now with an inline translation on top.
        text.Key.ShouldBe(Greeting.Key);
        text.Contract.ShouldBe(typeof(IResourceTranslations));
        text.GetTranslation(new ResourceTranslationsNL()).ShouldBe("Hallo");
        text.GetTranslation(CultureInfo.GetCultureInfo("fr-FR")).ShouldBe("Bonjour");
        Greeting.Translations.ShouldBeEmpty();
    }

    [Test]
    public void Equality_ShouldIncludeKeyAndContract()
    {
        // Arrange & Act & Assert: same key and default; the contract, the key, or the inline translations tell them
        // apart.
        new ResourceText("Key", "Name").ShouldBe(new ResourceText("Key", "Name"));
        new ResourceText("Key", "Name").GetHashCode().ShouldBe(new ResourceText("Key", "Name").GetHashCode());
        new ResourceText("Key", "Name").ShouldNotBe(new ResourceText("Other", "Name"));
        new ResourceText("Key", "Name").ShouldNotBe(new ResourceText("Key", "Name").WithTranslation("nl", "Naam"));
        Greeting.ShouldBe(Greeting);
        Greeting.ShouldNotBe(new ResourceText(Greeting.Key, Greeting.DefaultText));
        Greeting.ShouldBe(ResourceText.Create<IResourceTranslations>("Resource.Greeting", t => t.Greeting, new ResourceTranslationsEN()));
        Greeting.ShouldNotBe(ResourceText.Create<IResourceTranslations>("Resource.Greeting", "Hello", t => t.Greeting));
    }

    [Test]
    public void EqualityOperator_WithString_ShouldCompareWordingOfDerivedRecordToo()
    {
        // Arrange: ResourceText has a == of its own, generated for the record, which takes no string.
        var text = new ResourceText("Key", "Name").WithTranslation("nl", "Naam");
        var uiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("nl-NL");

        try
        {
            // Act & Assert
            (text == "Naam").ShouldBeTrue();
            ("Naam" == text).ShouldBeTrue();
            (text != "Naam").ShouldBeFalse();
            (text == null).ShouldBeFalse();
            (text == new ResourceText("Key", "Name").WithTranslation("nl", "Naam")).ShouldBeTrue();
        }
        finally
        {
            CultureInfo.CurrentUICulture = uiCulture;
        }
    }

    [Test]
    public void ToString_ShouldFallBackToDefaultLanguageSetWithoutCultureWalk()
    {
        // Arrange: an English set rewording a text. The invariant culture (the UI culture of an app under invariant
        // globalization, where no other culture can be constructed) has no chain to walk.
        var registry = TranslationsRegistry.Create([new ResourceRewordedTranslationsEN(), new ResourceTranslationsNL()]);
        var withoutDefaultLanguage = ResourceText.Create<IResourceTranslations>("Resource.Greeting", "Hello", t => t.Greeting);

        // Act & Assert: in an app without a default language of its own, the default-language set serves the invariant
        // culture and an uncovered one, a covered culture keeps its own set, and a text declared without a default
        // language keeps its default.
        Greeting.ToString(CultureInfo.InvariantCulture, registry, appLanguages: []).ShouldBe("Hi");
        Greeting.ToString(CultureInfo.GetCultureInfo("fr-FR"), registry, appLanguages: []).ShouldBe("Hi");
        Greeting.ToString(CultureInfo.GetCultureInfo("nl-NL"), registry, appLanguages: []).ShouldBe("Hallo");
        withoutDefaultLanguage.ToString(CultureInfo.InvariantCulture, registry, appLanguages: []).ShouldBe("Hello");
    }

    [Test]
    public void ToString_ShouldFallBackToSetOfAppDefaultLanguageBeforeItsOwn()
    {
        // Arrange: a contract whose default language is English, with an English set rewording a text, in an app whose
        // default language is Dutch.
        var registry = TranslationsRegistry.Create([new ResourceRewordedTranslationsEN(), new ResourceTranslationsNL()]);
        var withoutDefaultLanguage = ResourceText.Create<IResourceTranslations>("Resource.Greeting", "Hello", t => t.Greeting);
        var french = CultureInfo.GetCultureInfo("fr-FR");

        // Act & Assert: an uncovered culture gets the app's language, also through a regional app language (nl-BE
        // through nl) and for a text without a default language; a covered culture keeps its own set; and an app
        // language without a set leaves the text's own default language.
        Greeting.ToString(french, registry, appLanguages: ["nl"]).ShouldBe("Hallo");
        Greeting.ToString(CultureInfo.InvariantCulture, registry, appLanguages: ["nl"]).ShouldBe("Hallo");
        Greeting.ToString(french, registry, appLanguages: ["nl-BE"]).ShouldBe("Hallo");
        withoutDefaultLanguage.ToString(french, registry, appLanguages: ["nl"]).ShouldBe("Hallo");
        Greeting.ToString(CultureInfo.GetCultureInfo("en-GB"), registry, appLanguages: ["nl", "en"]).ShouldBe("Hi");
        Greeting.ToString(french, registry, appLanguages: ["de"]).ShouldBe("Hi");
    }

    [Test]
    public void ToString_ForCultureAppDoesNotDeclare_ShouldShowSetOfAppDefaultLanguageThoughSetExists()
    {
        // Arrange: a library's contract with English and Dutch sets, in an app declaring ("en", "fr").
        var registry = TranslationsRegistry.Create([new ResourceRewordedTranslationsEN(), new ResourceTranslationsNL()]);

        // Act & Assert: a Dutch user sees the English set, as the app's own texts are English for them too; an app
        // without languages shows the Dutch set.
        Greeting.ToString(CultureInfo.GetCultureInfo("nl-NL"), registry, appLanguages: ["en", "fr"]).ShouldBe("Hi");
        Greeting.ToString(CultureInfo.GetCultureInfo("nl-NL"), registry, appLanguages: []).ShouldBe("Hallo");
    }

    [Test]
    public void ToString_ForRegion_ShouldTakeRewordingOfItsLanguageWhereRegionalSetKeepsItsWording()
    {
        // Arrange: a library's English and British sets, and an app's rewording of the English set registered after
        // them, as an override is in the sets of the whole app; then the same with the rewording before the British set.
        var registry = TranslationsRegistry.Create([new ResourceTranslationsEN(), new ResourceBritishTranslations(), new ResourceRewordedTranslationsEN()]);
        var rewordingFirst = TranslationsRegistry.Create([new ResourceTranslationsEN(), new ResourceRewordedTranslationsEN(), new ResourceBritishTranslations()]);
        var british = CultureInfo.GetCultureInfo("en-GB");
        var french = CultureInfo.GetCultureInfo("fr-FR");

        // Act & Assert: a British user gets the rewording of the text the British set inherits, whichever registered
        // first, and the British wording of the text it words itself; so does an uncovered culture in an app whose
        // default language is British English. Other English cultures get the English sets.
        Greeting.ToString(british, registry, appLanguages: ["en", "nl"]).ShouldBe("Hi");
        Greeting.ToString(british, rewordingFirst, appLanguages: ["en", "nl"]).ShouldBe("Hi");
        Farewell.ToString(british, registry, appLanguages: ["en", "nl"]).ShouldBe("Cheerio");
        Greeting.ToString(french, registry, appLanguages: ["en-GB", "nl"]).ShouldBe("Hi");
        Farewell.ToString(french, registry, appLanguages: ["en-GB", "nl"]).ShouldBe("Cheerio");
        Greeting.ToString(CultureInfo.GetCultureInfo("en-US"), registry, appLanguages: ["en", "nl"]).ShouldBe("Hi");
        Farewell.ToString(CultureInfo.GetCultureInfo("en-US"), registry, appLanguages: ["en", "nl"]).ShouldBe("Goodbye");
    }

    [Test]
    public void ToString_ShouldShowEveryRewordingOfOneSetForTextsItChanges()
    {
        // Arrange: two rewordings of the English set, each changing another text, as a shared library's and an app's;
        // the same next to the British set; and a set implementing the contract in full, registered after a rewording.
        var registry = TranslationsRegistry.Create([new ResourceTranslationsEN(), new ResourceRewordedTranslationsEN(), new ResourceSecondRewordedTranslationsEN()]);
        var withBritish = TranslationsRegistry.Create([
            new ResourceTranslationsEN(),
            new ResourceBritishTranslations(),
            new ResourceRewordedTranslationsEN(),
            new ResourceSecondRewordedTranslationsEN(),
        ]);
        var standaloneLast = TranslationsRegistry.Create([new ResourceTranslationsEN(), new ResourceRewordedTranslationsEN(), new ResourceStandaloneEnglishTranslations()]);
        var english = CultureInfo.GetCultureInfo("en-US");
        var british = CultureInfo.GetCultureInfo("en-GB");
        var french = CultureInfo.GetCultureInfo("fr-FR");

        // Act & Assert: each text shows the last rewording that changes it, also as the default-language fallback of an
        // app without languages, and through the British set for a text it inherits; the British set's own wording
        // stays; a set deriving from no other set words every text itself, even one matching the default wording.
        Greeting.ToString(english, registry, appLanguages: ["en"]).ShouldBe("Hi");
        Farewell.ToString(english, registry, appLanguages: ["en"]).ShouldBe("Bye");
        Greeting.ToString(french, registry, appLanguages: []).ShouldBe("Hi");
        Farewell.ToString(french, registry, appLanguages: []).ShouldBe("Bye");
        Greeting.ToString(british, withBritish, appLanguages: ["en"]).ShouldBe("Hi");
        Farewell.ToString(british, withBritish, appLanguages: ["en"]).ShouldBe("Cheerio");
        Greeting.ToString(english, standaloneLast, appLanguages: ["en"]).ShouldBe("Hello");
        Farewell.ToString(english, standaloneLast, appLanguages: ["en"]).ShouldBe("So long");
    }

    [Test]
    public void ToString_ForRegion_ShouldKeepRegionalSetDerivingFromNoSetOfItsLanguage()
    {
        // Arrange
        var registry = TranslationsRegistry.Create([new ResourceTranslationsEN(), new ResourceStandaloneBritishTranslations(), new ResourceRewordedTranslationsEN()]);

        // Act & Assert: the British set inherits nothing from English, so the English rewording does not apply to it,
        // even where its wording matches the English.
        Greeting.ToString(CultureInfo.GetCultureInfo("en-GB"), registry, appLanguages: ["en"]).ShouldBe("Hello");
    }

    [Test]
    public void ToString_ForRegion_ShouldPassRewordingThroughEveryLevel()
    {
        // Arrange: walked by name, through the app's default language, so the levels do not depend on the parent
        // cultures of this machine.
        var registry = TranslationsRegistry.Create([
            new ResourceChineseTranslations(),
            new ResourceTraditionalChineseTranslations(),
            new ResourceTaiwaneseTranslations(),
            new ResourceRewordedChineseTranslations(),
        ]);
        var french = CultureInfo.GetCultureInfo("fr-FR");

        // Act & Assert: the Taiwanese set keeps the traditional wording, which keeps the Chinese one, so the rewording
        // of Chinese reaches Taiwan; the traditional set's own wording stays.
        Greeting.ToString(french, registry, appLanguages: ["zh-Hant-TW"]).ShouldBe("Nin hao");
        Farewell.ToString(french, registry, appLanguages: ["zh-Hant-TW"]).ShouldBe("Zaijian (Hant)");
    }

    [Test]
    [NotInParallel(nameof(TranslationsRegistry))]
    public void ToString_ShouldFindWordingInSetsOfWholeApp()
    {
        // Arrange: a contract no other test uses, so the sets of the whole app hold only this test's set for it.
        var text = ResourceText.Create<IResourceRegisteredTranslations>("Resource.Greeting", t => t.Greeting, new ResourceRegisteredTranslationsEN());
        var uiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("nl-NL");

        try
        {
            // Act: unregistered, then registered.
            var before = text.ToString();
            TranslationsRegistry.Register(new ResourceRegisteredTranslationsNL());
            var after = text.ToString();

            // Assert
            before.ShouldBe("Hello");
            after.ShouldBe("Hallo");
        }
        finally
        {
            CultureInfo.CurrentUICulture = uiCulture;
        }
    }

    [Test]
    public void MetadataName_ShouldMatchSourceGeneratorReferences()
    {
        // Assert: the class the source generator writes declares its texts as this type by name, so a rename or
        // namespace change must update the generator's emission in the same commit.
        typeof(ResourceText).FullName.ShouldBe("CodeChops.Idioma.ResourceText");
    }
}
