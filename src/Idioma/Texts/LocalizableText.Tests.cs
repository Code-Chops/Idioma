namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="LocalizableText"/>: the text's own translations, the order a wording is looked up in, and the
/// two conversions to and from a string. Texts of a set and texts with a key are tested in LocalizableTextSetsTests.
/// </summary>
public sealed class LocalizableTextTests
{
    #region Fixture
    /// <summary>
    /// Sets <see cref="CultureInfo.CurrentUICulture"/> for the duration of the scope (it is thread-bound, so tests on
    /// other threads are unaffected).
    /// </summary>
    private sealed class UICultureScope : IDisposable
    {
        private CultureInfo Previous { get; } = CultureInfo.CurrentUICulture;

        public UICultureScope(string name) => CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);

        public void Dispose() => CultureInfo.CurrentUICulture = this.Previous;
    }

    private interface ITextLocalization : ILocalizationSet
    {
        string Greeting { get; }
    }

    private sealed class TextLocalizationEn : ITextLocalization
    {
        public string Greeting => "Hello";
    }

    private sealed class TextLocalizationNl : ITextLocalization
    {
        public string Greeting => "Hallo";
    }

    /// <summary>
    /// A culture whose parent is the invariant culture, as every culture's is under invariant globalization.
    /// </summary>
    private sealed class TextInvariantParentCulture(string name) : CultureInfo(name)
    {
        public override CultureInfo Parent => InvariantCulture;
    }

    private static string TakeString(string value) => value;
    #endregion
    [Test]
    public void ToString_ShouldGiveWordingForCurrentUICulture()
    {
        // Arrange
        var text = LocalizableText.FromGeneratedCode("Name", ("nl", "Naam"));

        // Act & Assert: the translation walking up to the neutral culture, the default elsewhere, and what string
        // interpolation shows.
        using (new UICultureScope("nl-BE"))
        {
            text.ToString().ShouldBe("Naam");
            $"{text}".ShouldBe("Naam");
        }

        using (new UICultureScope("en-US"))
            text.ToString().ShouldBe("Name");
    }

    [Test]
    public void ImplicitConversion_ToString_ShouldGiveWordingForCurrentUICulture()
    {
        // Arrange
        using var _ = new UICultureScope("nl-NL");
        var text = LocalizableText.FromGeneratedCode("Name", ("nl", "Naam"));

        // Act
        string assigned = text;

        // Assert: an assignment, a parameter of a method, and the operand of a concatenation.
        assigned.ShouldBe("Naam");
        TakeString(text).ShouldBe("Naam");
        ("Veld: " + text).ShouldBe("Veld: Naam");
    }

    [Test]
    public void ImplicitConversion_ToString_ShouldConvertNoTextToNull()
    {
        // Arrange
        LocalizableText? nothing = null;

        // Act
        string? result = nothing;

        // Assert
        result.ShouldBeNull();
    }

    [Test]
    public void ImplicitConversion_ToString_ShouldReachTheTargetOfConditionalExpression()
    {
        // Arrange: a text and a string convert to each other, so such an expression has no type of its own and takes
        // its target's. This pins that it compiles.
        using var _ = new UICultureScope("nl-NL");
        var text = LocalizableText.FromGeneratedCode("Name", ("nl", "Naam"));
        const bool useText = true;

        // Act
        string result = useText ? text : "Other";

        // Assert
        result.ShouldBe("Naam");
    }

    [Test]
    public void ImplicitConversion_FromString_ShouldCreateTextWithoutTranslations()
    {
        // Arrange & Act
        LocalizableText text = "Name";
        LocalizableText? nothing = (string?)null;

        // Assert: a plain text, without a key.
        text.Key.ShouldBeNull();
        text.DefaultText.ShouldBe("Name");
        text.TranslationsByLanguage.ShouldBeEmpty();
        nothing.ShouldBeNull();
    }

    [Test]
    public void ToString_ShouldPreferOwnTranslationOverRegisteredSet()
    {
        // Arrange: the same text with and without an inline translation, and a registry serving Dutch.
        var culture = CultureInfo.GetCultureInfo("nl-NL");
        var registry = LocalizationRegistry.Create([new TextLocalizationNl()]);
        var declared = LocalizableText.Create<ITextLocalization>("Text.Greeting", t => t.Greeting, new TextLocalizationEn());

        // Act & Assert
        declared.ToString(culture, registry).ShouldBe("Hallo");
        declared.WithTranslation("nl", "Goedendag").ToString(culture, registry).ShouldBe("Goedendag");
    }

    [Test]
    public void ToString_ShouldFallBackToDefaultTextWhenNothingTranslates()
    {
        // Arrange
        var culture = CultureInfo.GetCultureInfo("de-DE");
        var registry = LocalizationRegistry.Create([new TextLocalizationNl()]);
        var declared = LocalizableText.Create<ITextLocalization>("Text.Greeting", t => t.Greeting, new TextLocalizationEn());

        // Act & Assert
        declared.ToString(culture, registry).ShouldBe("Hello");
        new LocalizableText("Name").ToString(culture, registry).ShouldBe("Name");
    }

    [Test]
    public void ToString_OfTextWithoutTranslationsOrContract_ShouldBeDefaultTextInEveryCulture()
    {
        // Arrange: a registry with sets and app languages, none of which can phrase a text of no contract.
        var registry = LocalizationRegistry.Create([new TextLocalizationEn(), new TextLocalizationNl()], "nl", "en");
        var plain = new LocalizableText("Name");
        var keyed = LocalizableText.Keyed("Customer.Name", "Jan");

        // Act & Assert: the default text itself, for a culture with a set, one without, and the invariant culture.
        foreach (var culture in new[] { CultureInfo.GetCultureInfo("nl-BE"), CultureInfo.GetCultureInfo("fr-FR"), CultureInfo.InvariantCulture })
        {
            plain.ToString(culture, registry).ShouldBeSameAs(plain.DefaultText);
            keyed.ToString(culture, registry).ShouldBeSameAs(keyed.DefaultText);
            plain.ToString(culture, registry: null).ShouldBeSameAs(plain.DefaultText);
        }

        plain.ToString().ShouldBeSameAs(plain.DefaultText);
        keyed.ToString().ShouldBeSameAs(keyed.DefaultText);
    }

    [Test]
    public void ToString_ForCultureNoLanguageServes_ShouldFallBackToAppDefaultLanguage()
    {
        // Arrange: an inline text of a library whose default language is English, in an app whose default is Dutch.
        var text = LocalizableText.FromGeneratedCode("Orders", ("en", "Orders"), ("nl", "Bestellingen"));
        var french = CultureInfo.GetCultureInfo("fr-FR");

        // Act & Assert: the app's default language, walked up to its language as the build checks it (nl-BE through
        // nl); a culture a language serves keeps that language; without the app's language the text's own default.
        text.ToString(french, registry: null, appLanguages: ["nl"]).ShouldBe("Bestellingen");
        text.ToString(french, registry: null, appLanguages: ["nl-BE"]).ShouldBe("Bestellingen");
        text.ToString(CultureInfo.InvariantCulture, registry: null, appLanguages: ["nl"]).ShouldBe("Bestellingen");
        text.ToString(CultureInfo.GetCultureInfo("en-GB"), registry: null, appLanguages: ["nl", "en"]).ShouldBe("Orders");
        text.ToString(french, registry: null, appLanguages: ["de"]).ShouldBe("Orders");
        text.ToString(french, registry: null, appLanguages: []).ShouldBe("Orders");
    }

    [Test]
    public void ToString_ForCultureAppDoesNotDeclare_ShouldShowAppDefaultLanguageThoughTextHasIt()
    {
        // Arrange: an inline text of a library with more languages than the app, which declares ("en", "fr").
        var text = LocalizableText.FromGeneratedCode("Orders", ("en", "Orders"), ("nl", "Bestellingen"), ("de", "Bestellungen"));
        string[] appLanguages = ["en", "fr"];

        // Act & Assert: a culture the app does not declare shows the app's default language, so the library's Dutch and
        // German stay unused next to the app's English; a culture the app declares keeps the walk (en-GB through en,
        // fr-FR to the default as the text lacks French); and an app without languages shows every language the text has.
        text.ToString(CultureInfo.GetCultureInfo("nl-NL"), registry: null, appLanguages: [.. appLanguages]).ShouldBe("Orders");
        text.ToString(CultureInfo.GetCultureInfo("de-DE"), registry: null, appLanguages: [.. appLanguages]).ShouldBe("Orders");
        text.ToString(CultureInfo.GetCultureInfo("en-GB"), registry: null, appLanguages: [.. appLanguages]).ShouldBe("Orders");
        text.ToString(CultureInfo.GetCultureInfo("fr-FR"), registry: null, appLanguages: [.. appLanguages]).ShouldBe("Orders");
        text.ToString(CultureInfo.GetCultureInfo("nl-NL"), registry: null, appLanguages: []).ShouldBe("Bestellingen");
        text.ToString(CultureInfo.GetCultureInfo("de-DE"), registry: null, appLanguages: []).ShouldBe("Bestellungen");
    }

    [Test]
    public void ToString_ForRegionOfAppLanguage_ShouldShowThatLanguage()
    {
        // Arrange: an app declaring Belgian Dutch next to English, and a text with Dutch and Belgian Dutch.
        var text = LocalizableText.FromGeneratedCode("Orders", ("en", "Orders"), ("nl", "Bestellingen"), ("nl-BE", "Bestellingen (BE)"));

        // Act & Assert: a culture under a declared language shows it; the neutral language above it is not declared,
        // and nor is another region of it.
        text.ToString(CultureInfo.GetCultureInfo("nl-BE"), registry: null, appLanguages: ["en", "nl-BE"]).ShouldBe("Bestellingen (BE)");
        text.ToString(CultureInfo.GetCultureInfo("nl"), registry: null, appLanguages: ["en", "nl-BE"]).ShouldBe("Orders");
        text.ToString(CultureInfo.GetCultureInfo("nl-NL"), registry: null, appLanguages: ["en", "nl-BE"]).ShouldBe("Orders");
        text.ToString(CultureInfo.GetCultureInfo("nl-NL"), registry: null, appLanguages: ["en", "NL"]).ShouldBe("Bestellingen");
    }

    [Test]
    public void ToString_WithoutAppDefaultLanguageArgument_ShouldUseLanguageAppRegistered()
    {
        // Arrange: this test project is an application declaring ("en", "nl"), so its generated registrar registered
        // English when it started.
        var text = LocalizableText.FromGeneratedCode("Bestellingen", ("nl", "Bestellingen"), ("en", "Orders"));
        var french = CultureInfo.GetCultureInfo("fr-FR");

        // Act & Assert: a Dutch-first text still falls back to the app's English.
        text.ToString(french, registry: null).ShouldBe("Orders");
        text.ToString(french).ShouldBe("Orders");
    }

    [Test]
    public void ToString_WithoutRegistry_ShouldConsiderOnlyOwnTranslations()
    {
        // Arrange
        var culture = CultureInfo.GetCultureInfo("nl-NL");
        var declared = LocalizableText.Create<ITextLocalization>("Text.Greeting", t => t.Greeting, new TextLocalizationEn());

        // Act & Assert
        declared.ToString(culture, registry: null).ShouldBe("Hello");
        declared.WithTranslation("nl", "Goedendag").ToString(culture, registry: null).ShouldBe("Goedendag");
    }

    [Test]
    public void ToString_ShouldThrowForNullCulture()
    {
        // Arrange
        var text = new LocalizableText("Name");

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => text.ToString(null!));
        Should.Throw<ArgumentNullException>(() => text.ToString(null!, registry: null));
    }

    [Test]
    public void Constructor_ShouldThrowForNull()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentNullException>(() => new LocalizableText(defaultText: null!));
    }

    [Test]
    public void FromGeneratedCode_ShouldCarryTranslationsByLanguageInAnyLetterCase()
    {
        // Arrange & Act
        var text = LocalizableText.FromGeneratedCode("Name", ("nl", "Naam"), ("DE", "Name (de)"));

        // Assert: key-less, default kept, languages looked up regardless of case.
        text.ShouldBeOfType<LocalizableText>();
        text.DefaultText.ShouldBe("Name");
        text.TranslationsByLanguage.Count.ShouldBe(2);
        text.TranslationsByLanguage["NL"].ShouldBe("Naam");
        text.TranslationsByLanguage["de"].ShouldBe("Name (de)");
    }

    [Test]
    public void WithTranslation_ShouldAddOrReplaceLanguageWithoutMutatingOriginal()
    {
        // Arrange
        var text = LocalizableText.FromGeneratedCode("Name", ("nl", "Naam"));

        // Act
        var extended = text.WithTranslation("de", "Name (de)").WithTranslation("nl", "Volledige naam");

        // Assert
        text.TranslationsByLanguage.Count.ShouldBe(1);
        text.TranslationsByLanguage["nl"].ShouldBe("Naam");
        extended.TranslationsByLanguage.Count.ShouldBe(2);
        extended.TranslationsByLanguage["nl"].ShouldBe("Volledige naam");
        extended.TranslationsByLanguage["de"].ShouldBe("Name (de)");
    }

    [Test]
    public void WithTranslation_AfterTranslationsWereRead_ShouldShowNewTranslationInMapAndLookup()
    {
        // Arrange: the map of the original is built before the copy is made.
        var text = LocalizableText.FromGeneratedCode("Name", ("nl", "Naam"));
        text.TranslationsByLanguage.Count.ShouldBe(1);

        // Act: a language in another letter case replaces, a new one is added.
        var extended = text.WithTranslation("NL", "Volledige naam").WithTranslation("de", "Name (de)");

        // Assert
        extended.TranslationsByLanguage.Count.ShouldBe(2);
        extended.TranslationsByLanguage["nl"].ShouldBe("Volledige naam");
        extended.GetTranslation(CultureInfo.GetCultureInfo("nl-BE")).ShouldBe("Volledige naam");
        extended.GetTranslation(CultureInfo.GetCultureInfo("de-DE")).ShouldBe("Name (de)");
        text.GetTranslation(CultureInfo.GetCultureInfo("nl-BE")).ShouldBe("Naam");
        text.GetTranslation(CultureInfo.GetCultureInfo("de-DE")).ShouldBeNull();
    }

    [Test]
    public void Translations_ShouldBeSameMapOnEveryRead()
    {
        // Arrange
        var text = LocalizableText.FromGeneratedCode("Name", ("nl", "Naam"), ("de", "Name (de)"));
        LocalizableText plain = "Name";

        // Act & Assert: built once, and one shared empty map for a text without translations.
        text.TranslationsByLanguage.ShouldBeSameAs(text.TranslationsByLanguage);
        text.TranslationsByLanguage.KeyComparer.ShouldBe(StringComparer.OrdinalIgnoreCase);
        plain.TranslationsByLanguage.ShouldBeSameAs(LocalizableText.NoTranslations);
    }

    [Test]
    public void Equals_BetweenTexts_ShouldCompareWordingForCurrentUICulture()
    {
        // Arrange: texts that read alike in Dutch, in another order of languages, with other English, or with one
        // wording for every culture.
        var text = LocalizableText.FromGeneratedCode("Name", ("en", "Name"), ("nl", "Naam"));
        var reordered = LocalizableText.FromGeneratedCode("Name", ("NL", "Naam"), ("EN", "Name"));
        var fullName = LocalizableText.FromGeneratedCode("Full name", ("en", "Full name"), ("nl", "Naam"));
        LocalizableText dutch = "Naam";

        // Act & Assert
        using (new UICultureScope("nl-NL"))
        {
            text.Equals(reordered).ShouldBeTrue();
            text.Equals(fullName).ShouldBeTrue();
            text.Equals(dutch).ShouldBeTrue();
            text.Equals(LocalizableText.FromGeneratedCode("Name", ("en", "Name"), ("nl", "Volledige naam"))).ShouldBeFalse();
            text.Equals((LocalizableText?)null).ShouldBeFalse();
        }

        using (new UICultureScope("en-US"))
        {
            text.Equals(reordered).ShouldBeTrue();
            text.Equals(fullName).ShouldBeFalse();
            text.Equals(dutch).ShouldBeFalse();
        }
    }

    [Test]
    public void FromGeneratedCode_AndWithTranslation_ShouldThrowForInvalidArguments()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentNullException>(() => LocalizableText.FromGeneratedCode(null!, ("nl", "Naam")));
        Should.Throw<ArgumentNullException>(() => LocalizableText.FromGeneratedCode("Name", ("nl", null!)));
        Should.Throw<ArgumentNullException>(() => LocalizableText.FromGeneratedCode("Name", (null!, "Naam")));
        Should.Throw<ArgumentException>(() => new LocalizableText("Name").WithTranslation("", "Naam"));
        Should.Throw<ArgumentException>(() => new LocalizableText("Name").WithTranslation(" ", "Naam"));
        Should.Throw<ArgumentNullException>(() => new LocalizableText("Name").WithTranslation("nl", null!));
    }

    [Test]
    public void GetTranslation_ForCulture_ShouldWalkUpToNeutralCulture()
    {
        // Arrange: a neutral entry and a specific one.
        var text = LocalizableText.FromGeneratedCode("Name", ("nl", "Naam"), ("nl-BE", "Naam (BE)"));

        // Act & Assert: the specific culture wins where present, a sibling culture falls back to the neutral entry, an
        // uncovered culture yields nothing, and a text without translations yields nothing.
        text.GetTranslation(CultureInfo.GetCultureInfo("nl-BE")).ShouldBe("Naam (BE)");
        text.GetTranslation(CultureInfo.GetCultureInfo("nl-NL")).ShouldBe("Naam");
        text.GetTranslation(CultureInfo.GetCultureInfo("nl")).ShouldBe("Naam");
        text.GetTranslation(CultureInfo.GetCultureInfo("de-DE")).ShouldBeNull();
        text.GetTranslation(CultureInfo.InvariantCulture).ShouldBeNull();
        new LocalizableText("Name").GetTranslation(CultureInfo.GetCultureInfo("nl")).ShouldBeNull();
        Should.Throw<ArgumentNullException>(() => text.GetTranslation((CultureInfo)null!));
    }

    [Test]
    public void ToString_ForRegionWithInvariantParent_ShouldShowItsLanguage()
    {
        // Arrange: nl-BE as invariant globalization creates it, whose parent is the invariant culture, in an app that
        // declares nl. It showed English before the lookups also walked by name.
        var belgian = new TextInvariantParentCulture("nl-BE");
        var registry = LocalizationRegistry.Create([new TextLocalizationNl()]);
        var inline = LocalizableText.FromGeneratedCode("Orders", ("en", "Orders"), ("nl", "Bestellingen"));
        var declared = LocalizableText.Create<ITextLocalization>("Text.Greeting", t => t.Greeting, new TextLocalizationEn());

        // Act & Assert
        inline.GetTranslation(belgian).ShouldBe("Bestellingen");
        inline.ToString(belgian, registry, appLanguages: ["en", "nl"]).ShouldBe("Bestellingen");
        declared.ToString(belgian, registry, appLanguages: ["en", "nl"]).ShouldBe("Hallo");
    }

    [Test]
    public void EqualityOperator_WithString_ShouldCompareWordingForCurrentUICulture()
    {
        // Arrange
        var text = LocalizableText.FromGeneratedCode("Name", ("nl", "Naam"));
        const string wording = "Naam";

        // Act & Assert: a literal and a variable, on either side, following the culture.
        using (new UICultureScope("nl-NL"))
        {
            (text == "Naam").ShouldBeTrue();
            ("Naam" == text).ShouldBeTrue();
            (text == wording).ShouldBeTrue();
            (text != "Naam").ShouldBeFalse();
            ("Naam" != text).ShouldBeFalse();
            (text == "Name").ShouldBeFalse();
        }

        using (new UICultureScope("en-US"))
        {
            (text == "Naam").ShouldBeFalse();
            (text == "Name").ShouldBeTrue();
            (text != "Naam").ShouldBeTrue();
        }
    }

    [Test]
    public void EqualityOperator_WithNull_ShouldStillCompile()
    {
        // Arrange: overloads taking a string would make these ambiguous, which is why the operators take a span.
        var text = new LocalizableText("Name");
        var empty = new LocalizableText("");
        LocalizableText? none = null;

        // Act & Assert
        (text == null).ShouldBeFalse();
        (null == text).ShouldBeFalse();
        (text != null).ShouldBeTrue();
        (empty == null).ShouldBeFalse();
        (none == null).ShouldBeTrue();
    }

    [Test]
    public void EqualityOperator_OfNullText_ShouldEqualNoWording()
    {
        // Arrange
        var empty = new LocalizableText("");
        LocalizableText? none = null;
        string? nothing = null;

        // Act & Assert: a null text is not an empty text.
        (none == "").ShouldBeFalse();
        ("" == none).ShouldBeFalse();
        (none != "").ShouldBeTrue();
        (none == nothing).ShouldBeFalse();
        (empty == "").ShouldBeTrue();
    }

    [Test]
    public void EqualityOperator_WithNullStringVariable_ShouldCompareAsEmptyString()
    {
        // Arrange: a span can't tell a null string from an empty one.
        var text = new LocalizableText("Name");
        var empty = new LocalizableText("");
        string? nothing = null;

        // Act & Assert
        (text == nothing).ShouldBeFalse();
        (empty == nothing).ShouldBeTrue();
    }

    [Test]
    public void EqualityOperator_BetweenTexts_ShouldCompareWordingForCurrentUICulture()
    {
        // Arrange: a translated text, and a text with one wording that only Dutch users see alike.
        var translated = LocalizableText.FromGeneratedCode("Name", ("nl", "Naam"));
        LocalizableText plain = "Naam";

        // Act & Assert
        using (new UICultureScope("nl-NL"))
        {
            (translated == plain).ShouldBeTrue();
            (translated != plain).ShouldBeFalse();
        }

        using (new UICultureScope("en-US"))
        {
            (translated == plain).ShouldBeFalse();
            (translated != plain).ShouldBeTrue();
            (translated == LocalizableText.FromGeneratedCode("Name", ("nl", "Naam"))).ShouldBeTrue();
        }
    }

    [Test]
    public void Equals_WithString_ShouldCompareWordingForCurrentUICulture()
    {
        // Arrange
        var text = LocalizableText.FromGeneratedCode("Name", ("nl", "Naam"));
        const string wording = "Naam";
        string? nothing = null;

        // Act & Assert: a literal and a variable, following the culture, as == does.
        using (new UICultureScope("nl-NL"))
        {
            text.Equals("Naam").ShouldBeTrue();
            text.Equals(wording).ShouldBeTrue();
            text.Equals("naam").ShouldBeFalse();
            text.Equals("Name").ShouldBeFalse();
            text.Equals(nothing).ShouldBeFalse();
        }

        using (new UICultureScope("en-US"))
        {
            text.Equals("Naam").ShouldBeFalse();
            text.Equals("Name").ShouldBeTrue();
        }

        // A null string compares as the empty string, and null itself is no text.
        new LocalizableText("").Equals(nothing).ShouldBeTrue();
        text.Equals(null).ShouldBeFalse();
    }

    [Test]
    public void Equals_WithStringOnTextWithKey_ShouldCompareWording()
    {
        // Arrange
        var text = LocalizableText.Keyed("Customer.Name", "Anna");
        const string wording = "Anna";

        // Act & Assert: a string is compared with the wording, also for a text with a key.
        text.Equals("Anna").ShouldBeTrue();
        text.Equals(wording).ShouldBeTrue();
        text.Equals("Customer.Name").ShouldBeFalse();
        text.Equals(LocalizableText.Keyed("Customer.Name", "Anna")).ShouldBeTrue();
        text.Equals(null).ShouldBeFalse();
    }

    [Test]
    public void Contains_WithString_ShouldFindTextShowingThatWording()
    {
        // Arrange: a collection compares texts, and the string becomes a text with that one wording for every culture.
        var translated = LocalizableText.FromGeneratedCode("Name", ("nl", "Naam"));
        List<LocalizableText> texts = [new LocalizableText("Other"), translated];
        const string wording = "Naam";

        // Act & Assert: also through the lookups that take an object.
        using (new UICultureScope("nl-NL"))
        {
            texts.Contains(wording).ShouldBeTrue();
            texts.IndexOf(wording).ShouldBe(1);
            Array.IndexOf(texts.ToArray(), wording).ShouldBe(1);
        }

        using (new UICultureScope("en-US"))
        {
            texts.Contains(wording).ShouldBeFalse();
            texts.Contains("Name").ShouldBeTrue();
        }
    }

    [Test]
    public void EqualsObject_WithString_ShouldCompareWordingOneWayOnly()
    {
        // Arrange
        using var _ = new UICultureScope("nl-NL");
        var text = LocalizableText.FromGeneratedCode("Name", ("nl", "Naam"));
        object wording = "Naam";

        // Act & Assert: a string's own Equals only takes another string.
        text.Equals(wording).ShouldBeTrue();
        Object.Equals(text, wording).ShouldBeTrue();
        Object.Equals(wording, text).ShouldBeFalse();
        text.Equals((object)"Name").ShouldBeFalse();
        text.Equals((object)1).ShouldBeFalse();
    }

    [Test]
    public void GetHashCode_ShouldBeThatOfTheWordingForCurrentUICulture()
    {
        // Arrange
        var text = LocalizableText.FromGeneratedCode("Name", ("nl", "Naam"));

        // Act & Assert: texts that read alike hash alike, and like the wording itself.
        using (new UICultureScope("nl-NL"))
        {
            text.GetHashCode().ShouldBe(new LocalizableText("Naam").GetHashCode());
            text.GetHashCode().ShouldBe(StringComparer.Ordinal.GetHashCode("Naam"));
            LocalizableText[] texts = [text, "Naam", "Name"];
            texts.Distinct().Count().ShouldBe(2);
        }

        using (new UICultureScope("en-US"))
            text.GetHashCode().ShouldBe(StringComparer.Ordinal.GetHashCode("Name"));
    }

    [Test]
    public void Collection_WithReferenceComparer_ShouldFindTextsAfterLanguageSwitch()
    {
        // Arrange: as the docs advise for a collection that outlives a language switch.
        var text = LocalizableText.FromGeneratedCode("Name", ("nl", "Naam"));
        var counts = new Dictionary<LocalizableText, int>(ReferenceEqualityComparer.Instance);

        using (new UICultureScope("nl-NL"))
            counts[text] = 1;

        // Act & Assert
        using (new UICultureScope("en-US"))
            counts[text].ShouldBe(1);
    }

    [Test]
    public void ComparingWording_WithoutLetterCaseOrForSorting_ShouldWorkThroughStrings()
    {
        // Arrange: as the docs show it.
        using var _ = new UICultureScope("nl-NL");
        var text = LocalizableText.FromGeneratedCode("Name", ("nl", "Naam"));
        LocalizableText[] texts = [text, LocalizableText.FromGeneratedCode("Address", ("nl", "Adres"))];

        // Act & Assert
        String.Equals(text, "naam", StringComparison.CurrentCultureIgnoreCase).ShouldBeTrue();
        texts.OrderBy(static item => (string)item, StringComparer.CurrentCulture).Select(static item => item.ToString()).ShouldBe(["Adres", "Naam"]);
    }
}
