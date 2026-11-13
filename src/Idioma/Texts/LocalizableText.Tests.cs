namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="LocalizableText"/>: the text's own translations, the order a wording is looked up in, the two
/// conversions to and from a string, and <see cref="LocalizableText.Format(ReadOnlySpan{object})"/>. The contract half lives in the
/// <see cref="ResourceText"/> tests.
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

    /// <summary>
    /// Sets <see cref="CultureInfo.CurrentCulture"/> for the duration of the scope.
    /// </summary>
    private sealed class CultureScope : IDisposable
    {
        private CultureInfo Previous { get; } = CultureInfo.CurrentCulture;

        public CultureScope(string name) => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);

        public void Dispose() => CultureInfo.CurrentCulture = this.Previous;
    }

    private interface ITextTranslations : ITranslationSet
    {
        string Greeting { get; }
    }

    private sealed class TextTranslationsEN : ITextTranslations
    {
        public string Greeting => "Hello";
    }

    private sealed class TextTranslationsNL : ITextTranslations
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

    /// <summary>
    /// A text type of a consumer's own, which the package does not allow.
    /// </summary>
    private sealed record TextDerived(string Wording) : LocalizableText(Wording);

    /// <summary>
    /// A text type of a consumer's own that copies a text instead of creating one.
    /// </summary>
    private sealed record TextCopied : LocalizableText
    {
        public TextCopied(LocalizableText original)
            : base(original)
        {
        }
    }

    private static string TakeString(string value) => value;
    #endregion

    [Test]
    public void Constructor_OfDerivedType_ShouldThrow()
    {
        // Arrange & Act & Assert: a derived text would be written to JSON without its translations and fail to read
        // back, so it fails where it is created, naming the type.
        Should.Throw<NotSupportedException>(() => new TextDerived("Name")).Message.ShouldContain(nameof(TextDerived));
    }

    [Test]
    public void CopyConstructor_OfDerivedType_ShouldThrow()
    {
        // Arrange & Act & Assert: the copy constructor of a record is open to a derived record too.
        Should.Throw<NotSupportedException>(() => new TextCopied(new LocalizableText("Name"))).Message.ShouldContain(nameof(TextCopied));
    }

    [Test]
    public void With_ShouldCopyTextAndItsTranslations()
    {
        // Arrange
        var text = LocalizableText.FromGeneratedCode("Name", ("nl", "Naam"));
        var resourceText = new ResourceText("Customer.Name", "Name").WithTranslation("nl", "Naam");

        // Act
        var copy = text with { };
        var resourceCopy = resourceText with { };

        // Assert
        copy.ShouldNotBeSameAs(text);
        copy.ShouldBe(text);
        copy.Translations["nl"].ShouldBe("Naam");
        resourceCopy.ShouldBe(resourceText);
        resourceCopy.Key.ShouldBe("Customer.Name");
    }

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
        var useText = true;

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

        // Assert: a plain text, not a resource text.
        text.ShouldBeOfType<LocalizableText>();
        text.DefaultText.ShouldBe("Name");
        text.Translations.ShouldBeEmpty();
        nothing.ShouldBeNull();
    }

    [Test]
    public void Format_WithoutArguments_ShouldStillTreatWordingAsFormatString()
    {
        // Arrange
        var escaped = new LocalizableText("Price ({{net}})");
        var unescaped = new LocalizableText("Price {EUR}");

        // Act & Assert: the same wording gives the same braces with and without arguments.
        escaped.Format().ShouldBe("Price ({net})");
        escaped.Format("unused").ShouldBe("Price ({net})");
        Should.Throw<FormatException>(() => unescaped.Format());
    }

    [Test]
    public void Format_WithCulture_ShouldTakeWordingAndFormatValuesFromThatCulture()
    {
        // Arrange: the current cultures are both English, the recipient is Dutch.
        var text = LocalizableText.FromGeneratedCode("Total: {0}", ("en", "Total: {0}"), ("nl", "Totaal: {0}"));
        var dutch = CultureInfo.GetCultureInfo("nl-NL");

        // Act & Assert
        using (new UICultureScope("en-US"))
        using (new CultureScope("en-US"))
        {
            text.Format(dutch, 1.5).ShouldBe("Totaal: 1,5");
            Should.Throw<FormatException>(() => text.Format(dutch));
        }

        Should.Throw<ArgumentNullException>(() => text.Format(culture: null!, 1.5));
    }

    [Test]
    public void Format_WithArguments_ShouldFillInPlaceholders()
    {
        // Arrange
        var text = new LocalizableText("{0} ({{net}}) {1}");

        // Act & Assert: a composite format string, with an escaped brace.
        text.Format("Total", 3).ShouldBe("Total ({net}) 3");
    }

    [Test]
    public void Format_ShouldTakeWordingFromUICultureAndFormatValuesInCulture()
    {
        // Arrange: the two cultures on purpose differ, as they do for an English user in the Netherlands.
        var text = LocalizableText.FromGeneratedCode("Total: {0}", ("nl", "Totaal: {0}"));

        // Act & Assert
        using (new UICultureScope("nl-NL"))
        using (new CultureScope("en-US"))
            text.Format(1.5).ShouldBe("Totaal: 1.5");

        using (new UICultureScope("en-US"))
        using (new CultureScope("nl-NL"))
            text.Format(1.5).ShouldBe("Total: 1,5");
    }

    [Test]
    public void Format_WithPlaceholderBeyondArguments_ShouldThrow()
    {
        // Arrange
        var text = new LocalizableText("{0} and {1}");

        // Act & Assert
        Should.Throw<FormatException>(() => text.Format("one"));
    }

    [Test]
    public void ToString_ShouldPreferOwnTranslationOverRegisteredSet()
    {
        // Arrange: the same text with and without an inline translation, and a registry serving Dutch.
        var culture = CultureInfo.GetCultureInfo("nl-NL");
        var registry = TranslationsRegistry.Create([new TextTranslationsNL()]);
        var declared = ResourceText.Create<ITextTranslations>("Text.Greeting", t => t.Greeting, new TextTranslationsEN());

        // Act & Assert
        declared.ToString(culture, registry).ShouldBe("Hallo");
        declared.WithTranslation("nl", "Goedendag").ToString(culture, registry).ShouldBe("Goedendag");
    }

    [Test]
    public void ToString_ShouldFallBackToDefaultTextWhenNothingTranslates()
    {
        // Arrange
        var culture = CultureInfo.GetCultureInfo("de-DE");
        var registry = TranslationsRegistry.Create([new TextTranslationsNL()]);
        var declared = ResourceText.Create<ITextTranslations>("Text.Greeting", t => t.Greeting, new TextTranslationsEN());

        // Act & Assert
        declared.ToString(culture, registry).ShouldBe("Hello");
        new LocalizableText("Name").ToString(culture, registry).ShouldBe("Name");
    }

    [Test]
    public void ToString_OfTextWithoutTranslationsOrContract_ShouldBeDefaultTextInEveryCulture()
    {
        // Arrange: a registry with sets and app languages, none of which can phrase a text of no contract.
        var registry = TranslationsRegistry.Create([new TextTranslationsEN(), new TextTranslationsNL()], "nl", "en");
        var plain = new LocalizableText("Name");
        var keyed = new ResourceText("Customer.Name", "Jan");

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
        var declared = ResourceText.Create<ITextTranslations>("Text.Greeting", t => t.Greeting, new TextTranslationsEN());

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
        text.Translations.Count.ShouldBe(2);
        text.Translations["NL"].ShouldBe("Naam");
        text.Translations["de"].ShouldBe("Name (de)");
    }

    [Test]
    public void FromGeneratedCode_ShouldKeepLastTextForRepeatedLanguage()
    {
        // Arrange & Act: the same language twice, in two spellings.
        var text = LocalizableText.FromGeneratedCode("Name", ("nl", "Naam"), ("NL", "Volledige naam"));

        // Assert
        text.Translations.Count.ShouldBe(1);
        text.Translations["nl"].ShouldBe("Volledige naam");
    }

    [Test]
    public void WithTranslation_ShouldAddOrReplaceLanguageWithoutMutatingOriginal()
    {
        // Arrange
        var text = LocalizableText.FromGeneratedCode("Name", ("nl", "Naam"));

        // Act
        var extended = text.WithTranslation("de", "Name (de)").WithTranslation("nl", "Volledige naam");

        // Assert
        text.Translations.Count.ShouldBe(1);
        text.Translations["nl"].ShouldBe("Naam");
        extended.Translations.Count.ShouldBe(2);
        extended.Translations["nl"].ShouldBe("Volledige naam");
        extended.Translations["de"].ShouldBe("Name (de)");
    }

    [Test]
    public void WithTranslation_AfterTranslationsWereRead_ShouldShowNewTranslationInMapAndLookup()
    {
        // Arrange: the map of the original is built before the copy is made.
        var text = LocalizableText.FromGeneratedCode("Name", ("nl", "Naam"));
        text.Translations.Count.ShouldBe(1);

        // Act: a language in another letter case replaces, a new one is added.
        var extended = text.WithTranslation("NL", "Volledige naam").WithTranslation("de", "Name (de)");

        // Assert
        extended.Translations.Count.ShouldBe(2);
        extended.Translations["nl"].ShouldBe("Volledige naam");
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
        text.Translations.ShouldBeSameAs(text.Translations);
        text.Translations.KeyComparer.ShouldBe(StringComparer.OrdinalIgnoreCase);
        plain.Translations.ShouldBeSameAs(LocalizableText.NoTranslations);
    }

    [Test]
    public void Equals_ShouldIgnoreOrderAndLetterCaseOfLanguages()
    {
        // Arrange
        var text = LocalizableText.FromGeneratedCode("Name", ("nl", "Naam"), ("de", "Name (de)"));

        // Act & Assert
        text.Equals(LocalizableText.FromGeneratedCode("Name", ("DE", "Name (de)"), ("NL", "Naam"))).ShouldBeTrue();
        text.Equals(LocalizableText.FromGeneratedCode("Name", ("nl", "Naam"), ("de", "Anders"))).ShouldBeFalse();
        text.Equals(LocalizableText.FromGeneratedCode("Name", ("nl", "Naam"), ("fr", "Name (de)"))).ShouldBeFalse();
        text.Equals(LocalizableText.FromGeneratedCode("Name", ("nl", "Naam"))).ShouldBeFalse();
    }

    [Test]
    public void FromGeneratedCode_AndWithTranslation_ShouldThrowForInvalidArguments()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentNullException>(() => LocalizableText.FromGeneratedCode(null!, ("nl", "Naam")));
        Should.Throw<ArgumentNullException>(() => LocalizableText.FromGeneratedCode("Name", ("nl", null!)));
        Should.Throw<ArgumentNullException>(() => LocalizableText.FromGeneratedCode("Name", (null!, "Naam")));
        Should.Throw<ArgumentException>(() => LocalizableText.FromGeneratedCode("Name", ("", "Naam")));
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
        var registry = TranslationsRegistry.Create([new TextTranslationsNL()]);
        var inline = LocalizableText.FromGeneratedCode("Orders", ("en", "Orders"), ("nl", "Bestellingen"));
        var declared = ResourceText.Create<ITextTranslations>("Text.Greeting", t => t.Greeting, new TextTranslationsEN());

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
        var wording = "Naam";

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
        // Arrange: a span cannot tell a null string from an empty one.
        var text = new LocalizableText("Name");
        var empty = new LocalizableText("");
        string? nothing = null;

        // Act & Assert
        (text == nothing).ShouldBeFalse();
        (empty == nothing).ShouldBeTrue();
    }

    [Test]
    public void EqualityOperator_BetweenTexts_ShouldCompareTextsRatherThanWording()
    {
        // Arrange: two texts showing the same wording for this culture.
        using var _ = new UICultureScope("nl-NL");
        var translated = LocalizableText.FromGeneratedCode("Name", ("nl", "Naam"));
        LocalizableText plain = "Naam";

        // Act & Assert
        translated.ToString().ShouldBe(plain.ToString());
        (translated == plain).ShouldBeFalse();
        (translated == LocalizableText.FromGeneratedCode("Name", ("nl", "Naam"))).ShouldBeTrue();
    }

    [Test]
    public void Equals_WithString_ShouldCompareTextsRatherThanWording()
    {
        // Arrange: what a collection relies on, so it stays structural.
        using var _ = new UICultureScope("nl-NL");
        var translated = LocalizableText.FromGeneratedCode("Name", ("nl", "Naam"));
        List<LocalizableText> texts = [translated];

        // Act & Assert
        translated.Equals("Naam").ShouldBeFalse();
        texts.Contains("Naam").ShouldBeFalse();
        texts.Contains(translated).ShouldBeTrue();
    }

    [Test]
    public void Equality_ShouldBeStructuralIncludingTranslations()
    {
        // Arrange & Act & Assert
        new LocalizableText("Name").ShouldBe((LocalizableText)"Name");
        new LocalizableText("Name").GetHashCode().ShouldBe(((LocalizableText)"Name").GetHashCode());
        LocalizableText.FromGeneratedCode("Name", ("nl", "Naam")).ShouldBe(LocalizableText.FromGeneratedCode("Name", ("NL", "Naam")));
        LocalizableText.FromGeneratedCode("Name", ("nl", "Naam")).GetHashCode().ShouldBe(LocalizableText.FromGeneratedCode("Name", ("NL", "Naam")).GetHashCode());
        LocalizableText.FromGeneratedCode("Name", ("nl", "Naam")).ShouldNotBe(LocalizableText.FromGeneratedCode("Name", ("nl", "Volledige naam")));
        LocalizableText.FromGeneratedCode("Name", ("nl", "Naam")).ShouldNotBe(new LocalizableText("Name"));
        new LocalizableText("Name").ShouldNotBe(new LocalizableText("Other"));
        new ResourceText("Key", "Name").ShouldNotBe(new LocalizableText("Name"));
        new LocalizableText("Name").ShouldNotBe(new ResourceText("Key", "Name"));
    }
}
