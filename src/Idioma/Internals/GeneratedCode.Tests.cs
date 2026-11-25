using System.Reflection;
using CodeChops.Idioma.Internals;
using CodeChops.Idioma.Tests.TestHelpers;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="GeneratedCode"/>: the names generated code calls it by, and that it is all generated code needs.
/// The order a text of a set falls back in has its own tests, next to <c>LocalizationRegistry.Word</c>.
/// </summary>
public sealed class GeneratedCodeTests
{
    #region Fixture
    /// <summary>
    /// A contract no other test uses, with a text per number of values. No set of it is registered, so every text is
    /// worded by the set of the default language.
    /// </summary>
    private interface IGeneratedCodeLocalization : ILocalizationSet
    {
        string Checkout { get; }
        string Greeting(string name);
        string Joined(string[] parts);
    }

    private sealed class GeneratedCodeLocalizationEn : IGeneratedCodeLocalization
    {
        public string Checkout => "Checkout";
        public string Greeting(string name) => $"Hello, {name}!";
        public string Joined(string[] parts) => String.Join('+', parts);
    }

    private interface IThrowingLocalization : ILocalizationSet
    {
        string Checkout { get; }
    }

    /// <summary>
    /// A set of a default language whose constructor throws, as one that reads a setting that isn't there.
    /// </summary>
    private sealed class ThrowingLocalizationEn : IThrowingLocalization
    {
        public static int Created { get; private set; }

        public ThrowingLocalizationEn()
        {
            Created++;
            throw new FormatException("No shop name.");
        }

        public string Checkout => "Checkout";
    }

    /// <summary>
    /// A contract no other test uses, whose set of the default language counts how often it's created.
    /// </summary>
    private interface ILazyLocalization : ILocalizationSet
    {
        string Checkout { get; }
    }

    private sealed class LazyLocalizationEn : ILazyLocalization
    {
        public static int Created { get; private set; }

        public LazyLocalizationEn() => Created++;

        public string Checkout => "Checkout";
    }

    /// <summary>
    /// A contract no other test uses, whose texts show the cultures they're worded in. No set of it is registered, so
    /// the set of the default language words every text. For a reader whose culture English doesn't serve, it's the
    /// fallback.
    /// </summary>
    private interface ICulturesLocalization : ILocalizationSet
    {
        string Cultures { get; }
        string CulturesOf(string name);
    }

    private sealed class CulturesLocalizationEn : ICulturesLocalization
    {
        public string Cultures => $"{CultureInfo.CurrentUICulture.Name}/{CultureInfo.CurrentCulture.Name}";
        public string CulturesOf(string name) => $"{name}: {this.Cultures}";
    }

    private static string CulturesOf(string name) =>
        GeneratedCode.ResolveWording<ICulturesLocalization, CulturesLocalizationEn, string>(1, 2, "CulturesOf", static (set, name) => set.CulturesOf(name), name);

    /// <summary>
    /// A type a string converts to in code, which a value passed by a key doesn't.
    /// </summary>
    private sealed record Label(string Value)
    {
        public static implicit operator Label(string value) => new(value);
    }

    private static readonly string[] EnglishDutch = ["en", "nl"];
    private static readonly string[] DutchEnglish = ["nl", "en"];
    private static readonly string[] EnglishDutchBritish = ["en", "nl", "en-GB"];
    private static readonly string[] FrenchGerman = ["fr", "de"];
    private static readonly string[] UpperCase = ["EN", "NL", "EN-gb"];

    private static int FindLanguageIndexFor(string culture, string[] languages)
    {
        using var _ = new CulturesScope(culture);

        var index = GeneratedCode.FindLanguageIndex(languages);
        return index;
    }
    #endregion

    [Test]
    public void MetadataName_ShouldMatchSourceGeneratorReferences()
    {
        // Arrange & Act
        var members = typeof(GeneratedCode)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(static method => method.Name);

        // Assert: generated code calls these by name, so a rename or namespace change must update the generators'
        // emission in the same commit.
        typeof(GeneratedCode).FullName.ShouldBe("CodeChops.Idioma.Internals.GeneratedCode");
        members.Distinct(StringComparer.Ordinal).ShouldBe(
            ["FindLanguageIndex", "FindSet", "FindSetForValues", "ResolveWording", "RecordMemberTexts", "ResolveKeyedWording", "GetKeyedValue", "CreateUnknownKeyException", "RegisterLanguages", "RecordSet", "RegisterSets"],
            ignoreOrder: true);
    }

    [Test]
    public void GetWording_ShouldHaveAnOverloadPerNumberOfValuesUpToEight()
    {
        // Arrange & Act
        var typeParameters = typeof(GeneratedCode)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(static method => method.Name is nameof(GeneratedCode.ResolveWording))
            .Select(static method => method.GetGenericArguments().Length);

        // Assert: the contract and the set of the default language, and then a type per value (LOC400 allows 8).
        typeParameters.ShouldBe([2, 3, 4, 5, 6, 7, 8, 9, 10], ignoreOrder: true);
    }

    [Test]
    [Arguments("nl-NL", 1)]
    [Arguments("nl-BE", 1)]
    [Arguments("nl", 1)]
    [Arguments("en-US", 0)]
    [Arguments("en-GB", 0)]
    [Arguments("en", 0)]
    public void FindLanguageIndex_ShouldGiveThePositionOfTheLanguageOfTheCurrentUICulture(string culture, int expected)
    {
        // Arrange & Act
        var index = FindLanguageIndexFor(culture, EnglishDutch);

        // Assert: the culture's own language, or else that of a culture above it.
        index.ShouldBe(expected);
    }

    [Test]
    public void FindLanguageIndex_ShouldGiveARegionItsOwnWordingAndOtherRegionsThatOfTheLanguage()
    {
        // Arrange & Act & Assert: British English next to English. A language is matched in any letter case.
        FindLanguageIndexFor("en-GB", EnglishDutchBritish).ShouldBe(2);
        FindLanguageIndexFor("en-US", EnglishDutchBritish).ShouldBe(0);
        FindLanguageIndexFor("en", EnglishDutchBritish).ShouldBe(0);
        FindLanguageIndexFor("nl-BE", EnglishDutchBritish).ShouldBe(1);
        FindLanguageIndexFor("en-GB", UpperCase).ShouldBe(2);
        FindLanguageIndexFor("nl-NL", UpperCase).ShouldBe(1);
    }

    [Test]
    public void FindLanguageIndex_ForCultureTheAppDoesNotShow_ShouldGiveTheAppDefaultLanguageElseTheFirst()
    {
        // Arrange & Act & Assert: this app shows English and Dutch, and its default language is English. A German
        // user gets English wherever it stands, also where the assembly has German, because the app's own texts are
        // English for them too. An assembly without English gives its first language.
        FindLanguageIndexFor("de-DE", EnglishDutch).ShouldBe(0);
        FindLanguageIndexFor("de-DE", DutchEnglish).ShouldBe(1);
        FindLanguageIndexFor("de-DE", FrenchGerman).ShouldBe(0);
        FindLanguageIndexFor("", DutchEnglish).ShouldBe(1);

        // A culture the app shows falls back the same way where the assembly lacks its language.
        FindLanguageIndexFor("nl-NL", FrenchGerman).ShouldBe(0);
    }

    [Test]
    public void FindLanguageIndex_WithTheSameArrayForAnotherCulture_ShouldPickAgain()
    {
        // Arrange & Act & Assert: the last pick is kept per thread, for the same languages and the same culture alone.
        for (var round = 0; round < 3; round++)
        {
            FindLanguageIndexFor("nl-NL", EnglishDutch).ShouldBe(1);
            FindLanguageIndexFor("nl-NL", DutchEnglish).ShouldBe(0);
            FindLanguageIndexFor("en-US", EnglishDutch).ShouldBe(0);
            FindLanguageIndexFor("en-US", DutchEnglish).ShouldBe(1);
        }
    }

    [Test]
    public void FindLanguageIndex_InRegistryWithLanguagesOfItsOwn_ShouldFollowThoseLanguages()
    {
        // Arrange: registries apart from the app's, as a culture resolves in each.
        var french = CultureInfo.GetCultureInfo("fr-FR");
        var dutchFirst = LocalizationRegistry.Create([], "nl", "en").Resolve(french);
        var britishFirst = LocalizationRegistry.Create([], "en-GB", "nl").Resolve(french);
        var withoutLanguages = LocalizationRegistry.Create([]).Resolve(french);
        var withFrench = LocalizationRegistry.Create([], "nl", "fr").Resolve(french);

        // Act & Assert: the default language of the registry, or else a language above it, and the first language
        // where the registry has no languages. A culture the registry shows gets its own.
        InlineLanguage.IndexOfShownLanguage(EnglishDutch, dutchFirst).ShouldBe(1);
        InlineLanguage.IndexOfShownLanguage(EnglishDutchBritish, britishFirst).ShouldBe(2);
        InlineLanguage.IndexOfShownLanguage(EnglishDutch, britishFirst).ShouldBe(0);
        InlineLanguage.IndexOfShownLanguage(DutchEnglish, britishFirst).ShouldBe(1);
        InlineLanguage.IndexOfShownLanguage(DutchEnglish, withoutLanguages).ShouldBe(0);
        InlineLanguage.IndexOfShownLanguage(FrenchGerman, withoutLanguages).ShouldBe(0);
        InlineLanguage.IndexOfShownLanguage(FrenchGerman, dutchFirst).ShouldBe(0);
        InlineLanguage.IndexOfShownLanguage(["de", "fr"], withFrench).ShouldBe(1);
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void FindLanguageIndex_WithCurrentUICultureProvider_ShouldFollowItsCultureInPlaceOfCurrentUICulture()
    {
        // Arrange: the thread is American, and the app keeps a Dutch user's culture somewhere else.
        using var _ = new CulturesScope("en-US");

        // Act & Assert
        using (new ProvidedCultureScope("nl-NL"))
            GeneratedCode.FindLanguageIndex(EnglishDutch).ShouldBe(1);

        // A provider that gives no culture leaves the text on the current UI culture.
        using (new ProvidedCultureScope(name: null))
            GeneratedCode.FindLanguageIndex(EnglishDutch).ShouldBe(0);
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void FindLanguageIndex_ShouldAllocateNothing()
    {
        // Arrange: apart from the tests that register sets, which make the next read build the registry anew.
        using var _ = new CulturesScope("nl-NL");

        // Act: per 10,000 calls.
        var allocated = GeneratedSetsTests.Allocated(static () => GeneratedCode.FindLanguageIndex(EnglishDutch)) / 10_000;

        // Assert
        allocated.ShouldBe(0);
    }

    [Test]
    public void GetKeyedWording_ShouldGiveTheTextUnlessValuesWerePassed()
    {
        // Arrange
        const string text = "Checkout";

        // Act
        var given = GeneratedCode.ResolveKeyedWording<IGeneratedCodeLocalization>(text, "Checkout", []);
        var exception = Should.Throw<ArgumentException>(() => GeneratedCode.ResolveKeyedWording<IGeneratedCodeLocalization>(text, "Checkout", ["Anna"]));
        var two = Should.Throw<ArgumentException>(() => GeneratedCode.ResolveKeyedWording<IGeneratedCodeLocalization>(text, "Checkout", ["Anna", 3]));

        // Assert
        given.ShouldBeSameAs(text);
        exception.ParamName.ShouldBe("values");
        exception.Message.ShouldStartWith("The text 'Checkout' of 'IGeneratedCodeLocalization' takes no values, but 1 was passed.");
        two.Message.ShouldStartWith("The text 'Checkout' of 'IGeneratedCodeLocalization' takes no values, but 2 were passed.");
    }

    [Test]
    public void GetKeyedValue_ShouldGiveTheValueOfTheParameter()
    {
        // Arrange & Act & Assert: by its position, as its own type, and null where the type allows it.
        GeneratedCode.GetKeyedValue<int>("Items", [3, "Idioma"], index: 0, count: 2, "count").ShouldBe(3);
        GeneratedCode.GetKeyedValue<string>("Items", [3, "Idioma"], index: 1, count: 2, "shop").ShouldBe("Idioma");
        GeneratedCode.GetKeyedValue<string?>("Greeting", [null], index: 0, count: 1, "name").ShouldBeNull();
        GeneratedCode.GetKeyedValue<int?>("Items", [null], index: 0, count: 1, "count").ShouldBeNull();
        GeneratedCode.GetKeyedValue<object>("Items", [3], index: 0, count: 1, "count").ShouldBe(3);
    }

    [Test]
    public void GetKeyedValue_ShouldRefuseAnotherNumberOfValuesThanTheTextTakes()
    {
        // Arrange & Act
        var tooFew = Should.Throw<ArgumentException>(() => GeneratedCode.GetKeyedValue<int>("Items", [], index: 0, count: 2, "count"));
        var tooMany = Should.Throw<ArgumentException>(() => GeneratedCode.GetKeyedValue<string>("Greeting", ["Anna", "Max"], index: 0, count: 1, "name"));

        // Assert
        tooFew.ParamName.ShouldBe("values");
        tooFew.Message.ShouldStartWith("The text 'Items' takes 2 values, but 0 were passed.");
        tooMany.Message.ShouldStartWith("The text 'Greeting' takes 1 value, but 2 were passed.");
    }

    [Test]
    public void GetKeyedValue_ShouldRefuseValueOfAnotherTypeWithoutConvertingIt()
    {
        // Arrange & Act: an int for a long, and null for a type that has none.
        var otherType = Should.Throw<ArgumentException>(() => GeneratedCode.GetKeyedValue<long>("Items", [3], index: 0, count: 1, "count"));
        var noValue = Should.Throw<ArgumentException>(() => GeneratedCode.GetKeyedValue<int>("Items", [null], index: 0, count: 1, "count"));

        // Assert
        otherType.ParamName.ShouldBe("values");
        otherType.Message.ShouldStartWith("The text 'Items' takes a value of type 'System.Int64' for 'count', but a value of type 'System.Int32' was passed.");
        otherType.Message.ShouldContain("Pass a value of that type, because a value isn't converted.");
        noValue.Message.ShouldStartWith("The text 'Items' takes a value of type 'System.Int32' for 'count', but null was passed.");
    }

    [Test]
    public void GetKeyedValue_ForStringParameter_ShouldGiveTheStringItself()
    {
        // Arrange: a text among the values is a string, as every text is.
        const string cart = "Cart";

        // Act
        var value = GeneratedCode.GetKeyedValue<string>("Removed", [cart], index: 0, count: 1, "what");

        // Assert
        value.ShouldBeSameAs(cart);
    }

    [Test]
    public void GetKeyedValue_ForOtherParameter_ShouldRefuseStringWithoutConvertingIt()
    {
        // Arrange & Act: a string for a number isn't parsed, and a string for a type it converts to in code isn't
        // converted.
        var forNumber = Should.Throw<ArgumentException>(() => GeneratedCode.GetKeyedValue<int>("Items", ["3"], index: 0, count: 1, "count"));
        var forLabel = Should.Throw<ArgumentException>(() => GeneratedCode.GetKeyedValue<Label>("Removed", ["Cart"], index: 0, count: 1, "what"));

        // Assert
        forNumber.Message.ShouldStartWith("The text 'Items' takes a value of type 'System.Int32' for 'count', but a value of type 'System.String' was passed.");
        forLabel.Message.ShouldStartWith($"The text 'Removed' takes a value of type '{typeof(Label)}' for 'what', but a value of type 'System.String' was passed.");
    }

    [Test]
    public void CreateUnknownKeyException_ShouldNameTheKeyAndTheClass()
    {
        // Arrange & Act
        var unknown = GeneratedCode.CreateUnknownKeyException<IGeneratedCodeLocalization>("Crat", "ShopLocalization");
        var missing = GeneratedCode.CreateUnknownKeyException<IGeneratedCodeLocalization>(key: null, "ShopLocalization");

        // Assert
        unknown.ShouldBeOfType<KeyNotFoundException>().Message.ShouldStartWith("'ShopLocalization' has no text with the key 'Crat'.");
        missing.ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("key");
    }

    [Test]
    public void Class_ShouldBeObsoleteAsWarning()
    {
        // Arrange & Act
        var obsolete = typeof(GeneratedCode).GetCustomAttribute<ObsoleteAttribute>();

        // Assert: a call by hand warns (CS0618). An error (CS0619) would break the generated code, which can only
        // suppress a warning.
        obsolete.ShouldNotBeNull();
        obsolete.IsError.ShouldBeFalse();
    }

    [Test]
    public void GetWording_OfProperty_ShouldGiveWordingOfSetOfDefaultLanguageWhereNoSetIsRegistered()
    {
        // Arrange
        using var _ = new CulturesScope("en-US");

        // Act: as a property of the class the source generator writes calls it.
        var wording = GeneratedCode.ResolveWording<IGeneratedCodeLocalization, GeneratedCodeLocalizationEn>(0, 3, "Checkout", static set => set.Checkout);

        // Assert
        wording.ShouldBe("Checkout");
    }

    [Test]
    public void GetWording_WithOneValue_ShouldHandItToTheSet()
    {
        // Arrange
        using var _ = new CulturesScope("en-US");
        const string name = "Anna";

        // Act: as a method of the class the source generator writes calls it.
        var wording = GeneratedCode.ResolveWording<IGeneratedCodeLocalization, GeneratedCodeLocalizationEn, string>(1, 3, "Greeting", static (set, name) => set.Greeting(name), name);

        // Assert
        wording.ShouldBe("Hello, Anna!");
    }

    [Test]
    public void GetWording_WithTwoToEightValues_ShouldHandThemToTheSetInOrder()
    {
        // Arrange
        using var _ = new CulturesScope("en-US");

        // Act: an overload per number of values, up to the most a text takes.
        var two = GeneratedCode.ResolveWording<IGeneratedCodeLocalization, GeneratedCodeLocalizationEn, string, string>(
            2, 3, "Joined", static (set, a, b) => set.Joined([a, b]), "1", "2");
        var three = GeneratedCode.ResolveWording<IGeneratedCodeLocalization, GeneratedCodeLocalizationEn, string, string, string>(
            2, 3, "Joined", static (set, a, b, c) => set.Joined([a, b, c]), "1", "2", "3");
        var four = GeneratedCode.ResolveWording<IGeneratedCodeLocalization, GeneratedCodeLocalizationEn, string, string, string, string>(
            2, 3, "Joined", static (set, a, b, c, d) => set.Joined([a, b, c, d]), "1", "2", "3", "4");
        var five = GeneratedCode.ResolveWording<IGeneratedCodeLocalization, GeneratedCodeLocalizationEn, string, string, string, string, string>(
            2, 3, "Joined", static (set, a, b, c, d, e) => set.Joined([a, b, c, d, e]), "1", "2", "3", "4", "5");
        var six = GeneratedCode.ResolveWording<IGeneratedCodeLocalization, GeneratedCodeLocalizationEn, string, string, string, string, string, string>(
            2, 3, "Joined", static (set, a, b, c, d, e, f) => set.Joined([a, b, c, d, e, f]), "1", "2", "3", "4", "5", "6");
        var seven = GeneratedCode.ResolveWording<IGeneratedCodeLocalization, GeneratedCodeLocalizationEn, string, string, string, string, string, string, string>(
            2, 3, "Joined", static (set, a, b, c, d, e, f, g) => set.Joined([a, b, c, d, e, f, g]), "1", "2", "3", "4", "5", "6", "7");
        var eight = GeneratedCode.ResolveWording<IGeneratedCodeLocalization, GeneratedCodeLocalizationEn, string, string, string, string, string, string, string, int>(
            2, 3, "Joined", static (set, a, b, c, d, e, f, g, h) => set.Joined([a, b, c, d, e, f, g, h.ToString(CultureInfo.InvariantCulture)]), "1", "2", "3", "4", "5", "6", "7", 8);

        // Assert
        two.ShouldBe("1+2");
        three.ShouldBe("1+2+3");
        four.ShouldBe("1+2+3+4");
        five.ShouldBe("1+2+3+4+5");
        six.ShouldBe("1+2+3+4+5+6");
        seven.ShouldBe("1+2+3+4+5+6+7");
        eight.ShouldBe("1+2+3+4+5+6+7+8");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    [Arguments("fr-FR", null)]
    [Arguments("en-US", "fr-FR")]
    public void GetWording_OfTextThatTakesValues_InLanguageItFallsBackTo_ShouldRunItsSetInThatLanguageOnEveryCall(string thread, string? provided)
    {
        // Arrange: a French reader of an app in English and Dutch gets English, from the thread or from a provider. The
        // set is kept as the text's fallback after the first call, and the generated class never calls it without the
        // scope of English.
        using var _ = new CulturesScope(thread);
        using var provider = provided is null ? null : new ProvidedCultureScope(provided);

        // Act: the first call looks the set up, and the next one words the text with the set kept for it.
        var first = CulturesOf("Ada");
        var second = CulturesOf("Max");
        var resolved = ResolvedSets<ICulturesLocalization>.Find(CultureInfo.GetCultureInfo("fr-FR"), count: 2);

        // Assert: in the UI culture of English, with the values formatted for the reader.
        var valuesCulture = provided ?? thread;

        first.ShouldBe($"Ada: en/{valuesCulture}");
        second.ShouldBe($"Max: en/{valuesCulture}");
        resolved.FindFallback(1).ShouldNotBeNull().Culture.ShouldNotBeNull().Name.ShouldBe("en");
        resolved.SetsForValues[1].ShouldBeNull();
        CultureInfo.CurrentUICulture.Name.ShouldBe(thread);
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void GetWording_OfTextThatTakesValues_InProvidedCultureTheThreadDoesNotHave_ShouldRunTheKeptSetInThatCulture()
    {
        // Arrange: a provider's British reader on a Dutch thread, whom English serves. The set is kept for the text,
        // but the generated class can't call it, because its method has to run in the provider's culture.
        using var _ = new CulturesScope("nl-NL");
        using var provider = new ProvidedCultureScope("en-GB");

        // Act: the first call looks the set up, and the next one words the text with the set kept for it.
        var first = CulturesOf("Ada");
        var second = CulturesOf("Max");
        var kept = ResolvedSets<ICulturesLocalization>.Find(CultureInfo.GetCultureInfo("en-GB"), count: 2).SetsForValues[1];

        // Assert: both in the provider's culture, and the thread's cultures back afterwards.
        first.ShouldBe("Ada: en-GB/en-GB");
        second.ShouldBe("Max: en-GB/en-GB");
        kept.ShouldBeSameAs(GeneratedCode.GetDefaultSet<CulturesLocalizationEn>());
        CultureInfo.CurrentUICulture.Name.ShouldBe("nl-NL");
        CultureInfo.CurrentCulture.Name.ShouldBe("nl-NL");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void GetWording_OfTextThatTakesValues_WithFallbackOfLanguageWithoutCulture_ShouldRunInScopeOfTheReader()
    {
        // Arrange: a provider's Belgian reader on an American thread, and a fallback whose language has no culture on
        // this machine, as under invariant globalization. No other test reads for fr-BE, so none finds this fallback.
        using var _ = new CulturesScope("en-US");
        using var provider = new ProvidedCultureScope("fr-BE");
        var resolved = ResolvedSets<ICulturesLocalization>.Find(CultureInfo.GetCultureInfo("fr-BE"), count: 2);
        resolved.KeepFallback(1, GeneratedCode.GetDefaultSet<CulturesLocalizationEn>(), culture: null);

        // Act
        var wording = CulturesOf("Max");

        // Assert: no scope of the language, but the values are formatted for the reader, as the lookup formats them.
        wording.ShouldBe("Max: fr-BE/fr-BE");
        CultureInfo.CurrentCulture.Name.ShouldBe("en-US");
    }

    [Test]
    public void GetWording_ForPositionOutsideTheTexts_ShouldStillWordTheText()
    {
        // Arrange: a position no generated class passes, as code written by hand could.
        using var _ = new CulturesScope("en-US");

        // Act
        var wording = GeneratedCode.ResolveWording<IGeneratedCodeLocalization, GeneratedCodeLocalizationEn>(7, 3, "Checkout", static set => set.Checkout);
        var negative = GeneratedCode.ResolveWording<IGeneratedCodeLocalization, GeneratedCodeLocalizationEn>(-1, 3, "Checkout", static set => set.Checkout);

        // Assert: worded, and no set is kept or read outside the texts.
        wording.ShouldBe("Checkout");
        negative.ShouldBe("Checkout");
        GeneratedCode.FindSet<IGeneratedCodeLocalization>(7, 3).ShouldBeNull();
        GeneratedCode.FindSet<IGeneratedCodeLocalization>(-1, 3).ShouldBeNull();
        GeneratedCode.FindSetForValues<IGeneratedCodeLocalization>(7, 3).ShouldBeNull();
    }

    [Test]
    public void GetDefaultSet_ShouldGiveOneInstanceOfTheSet()
    {
        // Arrange & Act
        var first = GeneratedCode.GetDefaultSet<GeneratedCodeLocalizationEn>();
        var second = GeneratedCode.GetDefaultSet<GeneratedCodeLocalizationEn>();

        // Assert
        second.ShouldBeSameAs(first);
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void SetOfDefaultLanguage_ShouldBeCreatedOnceWhenATextFirstNeedsIt()
    {
        // Arrange
        using var _ = new CulturesScope("en-US");

        // Act & Assert: asking for a kept set creates none.
        GeneratedCode.FindSet<ILazyLocalization>(0, 1).ShouldBeNull();
        LazyLocalizationEn.Created.ShouldBe(0);

        // The first text that needs the set creates it, and every text after it reads the same one.
        GeneratedCode.ResolveWording<ILazyLocalization, LazyLocalizationEn>(0, 1, "Checkout", static set => set.Checkout).ShouldBe("Checkout");
        GeneratedCode.ResolveWording<ILazyLocalization, LazyLocalizationEn>(0, 1, "Checkout", static set => set.Checkout).ShouldBe("Checkout");
        LazyLocalizationEn.Created.ShouldBe(1);
        GeneratedCode.FindSet<ILazyLocalization>(0, 1).ShouldBeSameAs(GeneratedCode.GetDefaultSet<LazyLocalizationEn>());
    }

    [Test]
    public void SetOfDefaultLanguageWhoseConstructorThrows_ShouldNotFailUntilATextNeedsIt()
    {
        // Arrange
        using var _ = new CulturesScope("en-US");

        // Act & Assert: asking for a kept set creates none, so the generated class is there for every text.
        GeneratedCode.FindSet<IThrowingLocalization>(0, 1).ShouldBeNull();
        ThrowingLocalizationEn.Created.ShouldBe(0);

        // What needs the set says which set threw, with the set's own exception inside, and so does every read after
        // it, which tries again.
        var first = Should.Throw<InvalidOperationException>(() => GeneratedCode.ResolveWording<IThrowingLocalization, ThrowingLocalizationEn>(0, 1, "Checkout", static set => set.Checkout));
        var second = Should.Throw<InvalidOperationException>(() => GeneratedCode.ResolveWording<IThrowingLocalization, ThrowingLocalizationEn>(0, 1, "Checkout", static set => set.Checkout));

        first.Message.ShouldContain(nameof(ThrowingLocalizationEn));
        first.InnerException.ShouldBeOfType<FormatException>().Message.ShouldBe("No shop name.");
        second.Message.ShouldBe(first.Message);
        ThrowingLocalizationEn.Created.ShouldBe(2);
        GeneratedCode.FindSet<IThrowingLocalization>(0, 1).ShouldBeNull();
    }
}
