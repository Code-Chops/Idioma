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

    private static int FindLanguageFor(string culture, string[] languages)
    {
        using var _ = new CulturesScope(culture);

        var index = GeneratedCode.FindLanguage(languages);
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
            ["FindLanguage", "FindSet", "FindSetForValues", "WordSetText", "RecordMemberTexts", "GetKeyedText", "GetKeyedValue", "CreateUnknownKeyException", "RegisterLanguages", "RecordSet", "RegisterSets"],
            ignoreOrder: true);
    }

    [Test]
    public void WordSetText_ShouldHaveAnOverloadPerNumberOfValuesUpToEight()
    {
        // Arrange & Act
        var typeParameters = typeof(GeneratedCode)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(static method => method.Name is nameof(GeneratedCode.WordSetText))
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
    public void FindLanguage_ShouldGiveThePositionOfTheLanguageOfTheCurrentUICulture(string culture, int expected)
    {
        // Arrange & Act
        var index = FindLanguageFor(culture, EnglishDutch);

        // Assert: the culture's own language, or else that of a culture above it.
        index.ShouldBe(expected);
    }

    [Test]
    public void FindLanguage_ShouldGiveARegionItsOwnWordingAndOtherRegionsThatOfTheLanguage()
    {
        // Arrange & Act & Assert: British English next to English. A language is matched in any letter case.
        FindLanguageFor("en-GB", EnglishDutchBritish).ShouldBe(2);
        FindLanguageFor("en-US", EnglishDutchBritish).ShouldBe(0);
        FindLanguageFor("en", EnglishDutchBritish).ShouldBe(0);
        FindLanguageFor("nl-BE", EnglishDutchBritish).ShouldBe(1);
        FindLanguageFor("en-GB", UpperCase).ShouldBe(2);
        FindLanguageFor("nl-NL", UpperCase).ShouldBe(1);
    }

    [Test]
    public void FindLanguage_ForCultureTheAppDoesNotShow_ShouldGiveTheAppDefaultLanguageElseTheFirst()
    {
        // Arrange & Act & Assert: this app shows English and Dutch, and its default language is English. A German
        // user gets English wherever it stands, also where the assembly has German, because the app's own texts are
        // English for them too. An assembly without English gives its first language.
        FindLanguageFor("de-DE", EnglishDutch).ShouldBe(0);
        FindLanguageFor("de-DE", DutchEnglish).ShouldBe(1);
        FindLanguageFor("de-DE", FrenchGerman).ShouldBe(0);
        FindLanguageFor("", DutchEnglish).ShouldBe(1);

        // A culture the app shows falls back the same way where the assembly lacks its language.
        FindLanguageFor("nl-NL", FrenchGerman).ShouldBe(0);
    }

    [Test]
    public void FindLanguage_WithTheSameArrayForAnotherCulture_ShouldPickAgain()
    {
        // Arrange & Act & Assert: the last pick is kept per thread, for the same languages and the same culture alone.
        for (var round = 0; round < 3; round++)
        {
            FindLanguageFor("nl-NL", EnglishDutch).ShouldBe(1);
            FindLanguageFor("nl-NL", DutchEnglish).ShouldBe(0);
            FindLanguageFor("en-US", EnglishDutch).ShouldBe(0);
            FindLanguageFor("en-US", DutchEnglish).ShouldBe(1);
        }
    }

    [Test]
    public void FindLanguage_InRegistryWithLanguagesOfItsOwn_ShouldFollowThoseLanguages()
    {
        // Arrange: registries apart from the app's, as a culture resolves in each.
        var french = CultureInfo.GetCultureInfo("fr-FR");
        var dutchFirst = LocalizationRegistry.Create([], "nl", "en").Resolve(french);
        var britishFirst = LocalizationRegistry.Create([], "en-GB", "nl").Resolve(french);
        var withoutLanguages = LocalizationRegistry.Create([]).Resolve(french);
        var withFrench = LocalizationRegistry.Create([], "nl", "fr").Resolve(french);

        // Act & Assert: the default language of the registry, or else a language above it, and the first language
        // where the registry has no languages. A culture the registry shows gets its own.
        InlineLanguage.Find(EnglishDutch, dutchFirst).ShouldBe(1);
        InlineLanguage.Find(EnglishDutchBritish, britishFirst).ShouldBe(2);
        InlineLanguage.Find(EnglishDutch, britishFirst).ShouldBe(0);
        InlineLanguage.Find(DutchEnglish, britishFirst).ShouldBe(1);
        InlineLanguage.Find(DutchEnglish, withoutLanguages).ShouldBe(0);
        InlineLanguage.Find(FrenchGerman, withoutLanguages).ShouldBe(0);
        InlineLanguage.Find(FrenchGerman, dutchFirst).ShouldBe(0);
        InlineLanguage.Find(["de", "fr"], withFrench).ShouldBe(1);
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void FindLanguage_WithCurrentUICultureProvider_ShouldFollowItsCultureInPlaceOfCurrentUICulture()
    {
        // Arrange: the thread is American, and the app keeps a Dutch user's culture somewhere else.
        using var _ = new CulturesScope("en-US");

        // Act & Assert
        using (new ProvidedCultureScope("nl-NL"))
            GeneratedCode.FindLanguage(EnglishDutch).ShouldBe(1);

        // A provider that gives no culture leaves the text on the current UI culture.
        using (new ProvidedCultureScope(name: null))
            GeneratedCode.FindLanguage(EnglishDutch).ShouldBe(0);
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void FindLanguage_ShouldAllocateNothing()
    {
        // Arrange: apart from the tests that register sets, which make the next read build the registry anew.
        using var _ = new CulturesScope("nl-NL");

        // Act: per 10,000 calls.
        var allocated = GeneratedSetsTests.Allocated(static () => GeneratedCode.FindLanguage(EnglishDutch)) / 10_000;

        // Assert
        allocated.ShouldBe(0);
    }

    [Test]
    public void FindLanguage_WithNull_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentNullException>(() => GeneratedCode.FindLanguage(null!)).ParamName.ShouldBe("languages");
    }

    [Test]
    public void GetKeyedText_ShouldGiveTheTextUnlessValuesWerePassed()
    {
        // Arrange
        const string text = "Checkout";

        // Act
        var given = GeneratedCode.GetKeyedText<IGeneratedCodeLocalization>(text, "Checkout", []);
        var exception = Should.Throw<ArgumentException>(() => GeneratedCode.GetKeyedText<IGeneratedCodeLocalization>(text, "Checkout", ["Anna"]));
        var two = Should.Throw<ArgumentException>(() => GeneratedCode.GetKeyedText<IGeneratedCodeLocalization>(text, "Checkout", ["Anna", 3]));

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
    public void WordSetText_OfProperty_ShouldGiveWordingOfSetOfDefaultLanguageWhereNoSetIsRegistered()
    {
        // Arrange
        using var _ = new CulturesScope("en-US");

        // Act: as a property of the class the source generator writes calls it.
        var wording = GeneratedCode.WordSetText<IGeneratedCodeLocalization, GeneratedCodeLocalizationEn>(0, 3, "Checkout", static set => set.Checkout);

        // Assert
        wording.ShouldBe("Checkout");
    }

    [Test]
    public void WordSetText_WithOneValue_ShouldHandItToTheSet()
    {
        // Arrange
        using var _ = new CulturesScope("en-US");
        const string name = "Anna";

        // Act: as a method of the class the source generator writes calls it.
        var wording = GeneratedCode.WordSetText<IGeneratedCodeLocalization, GeneratedCodeLocalizationEn, string>(1, 3, "Greeting", static (set, name) => set.Greeting(name), name);

        // Assert
        wording.ShouldBe("Hello, Anna!");
    }

    [Test]
    public void WordSetText_WithTwoToEightValues_ShouldHandThemToTheSetInOrder()
    {
        // Arrange
        using var _ = new CulturesScope("en-US");

        // Act: an overload per number of values, up to the most a text takes.
        var two = GeneratedCode.WordSetText<IGeneratedCodeLocalization, GeneratedCodeLocalizationEn, string, string>(
            2, 3, "Joined", static (set, a, b) => set.Joined([a, b]), "1", "2");
        var three = GeneratedCode.WordSetText<IGeneratedCodeLocalization, GeneratedCodeLocalizationEn, string, string, string>(
            2, 3, "Joined", static (set, a, b, c) => set.Joined([a, b, c]), "1", "2", "3");
        var four = GeneratedCode.WordSetText<IGeneratedCodeLocalization, GeneratedCodeLocalizationEn, string, string, string, string>(
            2, 3, "Joined", static (set, a, b, c, d) => set.Joined([a, b, c, d]), "1", "2", "3", "4");
        var five = GeneratedCode.WordSetText<IGeneratedCodeLocalization, GeneratedCodeLocalizationEn, string, string, string, string, string>(
            2, 3, "Joined", static (set, a, b, c, d, e) => set.Joined([a, b, c, d, e]), "1", "2", "3", "4", "5");
        var six = GeneratedCode.WordSetText<IGeneratedCodeLocalization, GeneratedCodeLocalizationEn, string, string, string, string, string, string>(
            2, 3, "Joined", static (set, a, b, c, d, e, f) => set.Joined([a, b, c, d, e, f]), "1", "2", "3", "4", "5", "6");
        var seven = GeneratedCode.WordSetText<IGeneratedCodeLocalization, GeneratedCodeLocalizationEn, string, string, string, string, string, string, string>(
            2, 3, "Joined", static (set, a, b, c, d, e, f, g) => set.Joined([a, b, c, d, e, f, g]), "1", "2", "3", "4", "5", "6", "7");
        var eight = GeneratedCode.WordSetText<IGeneratedCodeLocalization, GeneratedCodeLocalizationEn, string, string, string, string, string, string, string, int>(
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
    public void WordSetText_ForPositionOutsideTheTexts_ShouldStillWordTheText()
    {
        // Arrange: a position no generated class passes, as code written by hand could.
        using var _ = new CulturesScope("en-US");

        // Act
        var wording = GeneratedCode.WordSetText<IGeneratedCodeLocalization, GeneratedCodeLocalizationEn>(7, 3, "Checkout", static set => set.Checkout);
        var negative = GeneratedCode.WordSetText<IGeneratedCodeLocalization, GeneratedCodeLocalizationEn>(-1, 3, "Checkout", static set => set.Checkout);

        // Assert: worded, and no set is kept or read outside the texts.
        wording.ShouldBe("Checkout");
        negative.ShouldBe("Checkout");
        GeneratedCode.FindSet<IGeneratedCodeLocalization>(7, 3).ShouldBeNull();
        GeneratedCode.FindSet<IGeneratedCodeLocalization>(-1, 3).ShouldBeNull();
        GeneratedCode.FindSetForValues<IGeneratedCodeLocalization>(7, 3).ShouldBeNull();
    }

    [Test]
    public void WordSetText_WithNull_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert: for a text without values and for one with.
        Should.Throw<ArgumentNullException>(() => GeneratedCode.WordSetText<IGeneratedCodeLocalization, GeneratedCodeLocalizationEn>(0, 3, null!, static set => set.Checkout))
            .ParamName.ShouldBe("name");
        Should.Throw<ArgumentNullException>(() => GeneratedCode.WordSetText<IGeneratedCodeLocalization, GeneratedCodeLocalizationEn>(0, 3, "Checkout", null!));
        Should.Throw<ArgumentNullException>(() => GeneratedCode.WordSetText<IGeneratedCodeLocalization, GeneratedCodeLocalizationEn, string>(1, 3, null!, static (set, name) => set.Greeting(name), "Anna"))
            .ParamName.ShouldBe("name");
        Should.Throw<ArgumentNullException>(() => GeneratedCode.WordSetText<IGeneratedCodeLocalization, GeneratedCodeLocalizationEn, string>(1, 3, "Greeting", null!, "Anna"))
            .ParamName.ShouldBe("selector");
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
        GeneratedCode.WordSetText<ILazyLocalization, LazyLocalizationEn>(0, 1, "Checkout", static set => set.Checkout).ShouldBe("Checkout");
        GeneratedCode.WordSetText<ILazyLocalization, LazyLocalizationEn>(0, 1, "Checkout", static set => set.Checkout).ShouldBe("Checkout");
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
        var first = Should.Throw<InvalidOperationException>(() => GeneratedCode.WordSetText<IThrowingLocalization, ThrowingLocalizationEn>(0, 1, "Checkout", static set => set.Checkout));
        var second = Should.Throw<InvalidOperationException>(() => GeneratedCode.WordSetText<IThrowingLocalization, ThrowingLocalizationEn>(0, 1, "Checkout", static set => set.Checkout));

        first.Message.ShouldContain(nameof(ThrowingLocalizationEn));
        first.InnerException.ShouldBeOfType<FormatException>().Message.ShouldBe("No shop name.");
        second.Message.ShouldBe(first.Message);
        ThrowingLocalizationEn.Created.ShouldBe(2);
        GeneratedCode.FindSet<IThrowingLocalization>(0, 1).ShouldBeNull();
    }
}
