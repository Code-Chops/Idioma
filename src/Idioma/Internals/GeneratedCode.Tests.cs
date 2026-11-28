using System.Reflection;
using CodeChops.Idioma.Internals;
using CodeChops.Idioma.Tests.TestHelpers;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="GeneratedCode"/>: the names generated code calls it by, and that it is all generated code needs.
/// The order a text of a set falls back in has its own tests, next to <c>LocalizationRegistry.Word</c>.
/// </summary>
public sealed partial class GeneratedCodeTests
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

    internal sealed class GeneratedCodeLocalizationEn : IGeneratedCodeLocalization
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
    internal sealed class ThrowingLocalizationEn : IThrowingLocalization
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

    internal sealed class LazyLocalizationEn : ILazyLocalization
    {
        public static int Created { get; private set; }

        public LazyLocalizationEn() => Created++;

        public string Checkout => "Checkout";
    }

    /// <summary>
    /// A contract no other test uses, whose texts show the cultures they're worded in. No set of it is registered, so
    /// the set of the default language words every text. For a user whose culture English doesn't serve, it's the
    /// fallback.
    /// </summary>
    private interface ICulturesLocalization : ILocalizationSet
    {
        string Cultures { get; }
        string CulturesOf(string name);
    }

    internal sealed class CulturesLocalizationEn : ICulturesLocalization
    {
        public string Cultures => $"{CultureInfo.CurrentUICulture.Name}/{CultureInfo.CurrentCulture.Name}";
        public string CulturesOf(string name) => $"{name}: {this.Cultures}";
    }

    // As the source generator writes a text that takes values.
    private static string CulturesOf(string name) =>
        GeneratedCode.GetLocalizationSetForValues<ICulturesLocalization>()?.CulturesOf(name)
        ?? GeneratedCode.ResolveWording<ICulturesLocalization, CulturesLocalizationEn, string>("CulturesOf", static (set, name) => set.CulturesOf(name), name);

    /// <summary>
    /// A contract no other test uses, whose text shows the nickname it's given, which may be null at run time, as one
    /// read from a database. No set of it is registered, so for a French user the set of the default language words it
    /// as the fallback.
    /// </summary>
    private interface INicknameLocalization : ILocalizationSet
    {
        string Nickname(string? nickname);
    }

    internal sealed class NicknameLocalizationEn : INicknameLocalization
    {
        public static int Reads { get; private set; }

        public string Nickname(string? nickname)
        {
            Reads++;
            return nickname!;
        }
    }

    // As the source generator writes a text that takes values.
    private static string Nickname(string? nickname) =>
        GeneratedCode.GetLocalizationSetForValues<INicknameLocalization>()?.Nickname(nickname)
        ?? GeneratedCode.ResolveWording<INicknameLocalization, NicknameLocalizationEn, string?>("Nickname", static (set, nickname) => set.Nickname(nickname), nickname);

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

    private static int GetLanguageIndexIn(string culture, string[] languages)
    {
        using var _ = new CulturesScope(culture);

        var index = GeneratedCode.GetLanguageIndexForStandaloneText(languages);
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
            ["GetLanguageIndexForStandaloneText", "GetLocalizationSet", "GetLocalizationSetForValues", "ResolveWording", "ResolveKeyedWording", "GetKeyedValue", "CreateNullKeyException", "RegisterLanguages", "DescribeSet", "RegisterSets", "RegisterMemberTexts", "GetEnumText", "RegisterEnumTexts"],
            ignoreOrder: true);
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
    [Arguments("nl-NL", 1)]
    [Arguments("nl-BE", 1)]
    [Arguments("nl", 1)]
    [Arguments("en-US", 0)]
    [Arguments("en-GB", 0)]
    [Arguments("en", 0)]
    public void GetLanguageIndexForStandaloneText_ShouldGiveThePositionOfTheLanguageOfTheCurrentUICulture(string culture, int expected)
    {
        // Arrange & Act
        var index = GetLanguageIndexIn(culture, EnglishDutch);

        // Assert: the culture's own language, or else that of a culture it falls back to.
        index.ShouldBe(expected);
    }

    [Test]
    public void GetLanguageIndexForStandaloneText_ShouldGiveARegionItsOwnWordingAndOtherRegionsThatOfTheLanguage()
    {
        // Arrange & Act & Assert: British English next to English. A language is matched in any letter case.
        GetLanguageIndexIn("en-GB", EnglishDutchBritish).ShouldBe(2);
        GetLanguageIndexIn("en-US", EnglishDutchBritish).ShouldBe(0);
        GetLanguageIndexIn("en", EnglishDutchBritish).ShouldBe(0);
        GetLanguageIndexIn("nl-BE", EnglishDutchBritish).ShouldBe(1);
        GetLanguageIndexIn("en-GB", UpperCase).ShouldBe(2);
        GetLanguageIndexIn("nl-NL", UpperCase).ShouldBe(1);
    }

    [Test]
    public void GetLanguageIndexForStandaloneText_ForCultureTheAppDoesNotShow_ShouldGiveTheAppDefaultLanguageElseTheFirst()
    {
        // Arrange & Act & Assert: this app shows English and Dutch, and its default language is English. A German
        // user gets English wherever it stands, also where the assembly has German, because the app's own texts are
        // English for them too. An assembly without English gives its first language.
        GetLanguageIndexIn("de-DE", EnglishDutch).ShouldBe(0);
        GetLanguageIndexIn("de-DE", DutchEnglish).ShouldBe(1);
        GetLanguageIndexIn("de-DE", FrenchGerman).ShouldBe(0);
        GetLanguageIndexIn("", DutchEnglish).ShouldBe(1);

        // A culture the app shows falls back the same way where the assembly lacks its language.
        GetLanguageIndexIn("nl-NL", FrenchGerman).ShouldBe(0);
    }

    [Test]
    public void GetLanguageIndexForStandaloneText_WithTheSameArrayForAnotherCulture_ShouldPickAgain()
    {
        // Arrange & Act & Assert: the last pick is kept per thread, for the same languages and the same culture alone.
        for (var round = 0; round < 3; round++)
        {
            GetLanguageIndexIn("nl-NL", EnglishDutch).ShouldBe(1);
            GetLanguageIndexIn("nl-NL", DutchEnglish).ShouldBe(0);
            GetLanguageIndexIn("en-US", EnglishDutch).ShouldBe(0);
            GetLanguageIndexIn("en-US", DutchEnglish).ShouldBe(1);
        }
    }

    [Test]
    public void GetLanguageIndexForStandaloneText_InRegistryWithLanguagesOfItsOwn_ShouldFollowThoseLanguages()
    {
        // Arrange: registries apart from the app's, as a culture resolves in each.
        var french = CultureInfo.GetCultureInfo("fr-FR");
        var dutchFirst = LocalizationRegistry.Create([], "nl", "en").Resolve(french);
        var britishFirst = LocalizationRegistry.Create([], "en-GB", "nl").Resolve(french);
        var withoutLanguages = LocalizationRegistry.Create([]).Resolve(french);
        var withFrench = LocalizationRegistry.Create([], "nl", "fr").Resolve(french);

        // Act & Assert: the default language of the registry, or else a language it falls back to, and the first
        // language where the registry has no languages. A culture the registry shows gets its own.
        StandaloneLanguage.IndexOfShownLanguage(EnglishDutch, dutchFirst).ShouldBe(1);
        StandaloneLanguage.IndexOfShownLanguage(EnglishDutchBritish, britishFirst).ShouldBe(2);
        StandaloneLanguage.IndexOfShownLanguage(EnglishDutch, britishFirst).ShouldBe(0);
        StandaloneLanguage.IndexOfShownLanguage(DutchEnglish, britishFirst).ShouldBe(1);
        StandaloneLanguage.IndexOfShownLanguage(DutchEnglish, withoutLanguages).ShouldBe(0);
        StandaloneLanguage.IndexOfShownLanguage(FrenchGerman, withoutLanguages).ShouldBe(0);
        StandaloneLanguage.IndexOfShownLanguage(FrenchGerman, dutchFirst).ShouldBe(0);
        StandaloneLanguage.IndexOfShownLanguage(["de", "fr"], withFrench).ShouldBe(1);
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void GetLanguageIndexForStandaloneText_ShouldAllocateNothing()
    {
        // Arrange: apart from the tests that register sets, which make the next read build the registry anew.
        using var _ = new CulturesScope("nl-NL");

        // Act: per 10,000 calls.
        var allocated = GeneratedSetsTests.Allocated(static () => GeneratedCode.GetLanguageIndexForStandaloneText(EnglishDutch)) / 10_000;

        // Assert
        allocated.ShouldBe(0);
    }

    [Test]
    public void GetWording_ShouldHaveAnOverloadWithoutValuesAndOneWithThem()
    {
        // Arrange & Act
        var typeParameters = typeof(GeneratedCode)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(static method => method.Name is nameof(GeneratedCode.ResolveWording))
            .Select(static method => method.GetGenericArguments().Length);

        // Assert: the contract and the set of the default language, and then the type of the values, which is a tuple
        // for more than one. So a text takes any number of values.
        typeParameters.ShouldBe([2, 3], ignoreOrder: true);
    }

    [Test]
    public void GetWording_OfProperty_ShouldGiveWordingOfSetOfDefaultLanguageWhereNoSetIsRegistered()
    {
        // Arrange
        using var _ = new CulturesScope("en-US");

        // Act: as a property of the class the source generator writes calls it.
        var wording = GeneratedCode.ResolveWording<IGeneratedCodeLocalization, GeneratedCodeLocalizationEn>("Checkout", static set => set.Checkout);

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
        var wording = GeneratedCode.ResolveWording<IGeneratedCodeLocalization, GeneratedCodeLocalizationEn, string>("Greeting", static (set, name) => set.Greeting(name), name);

        // Assert
        wording.ShouldBe("Hello, Anna!");
    }

    [Test]
    public void GetWording_WithSeveralValues_ShouldHandThemToTheSetInOrder()
    {
        // Arrange
        using var _ = new CulturesScope("en-US");

        // Act: the values as one tuple, as the source generator writes them, also beyond the seven a tuple holds
        // before it nests the rest.
        var two = GeneratedCode.ResolveWording<IGeneratedCodeLocalization, GeneratedCodeLocalizationEn, (string, string)>(
            "Joined", static (set, values) => set.Joined([values.Item1, values.Item2]), ("1", "2"));
        var nine = GeneratedCode.ResolveWording<IGeneratedCodeLocalization, GeneratedCodeLocalizationEn, (string, string, string, string, string, string, string, string, int)>(
            "Joined", static (set, values) => set.Joined([values.Item1, values.Item2, values.Item3, values.Item4, values.Item5, values.Item6, values.Item7, values.Item8, values.Item9.ToString(CultureInfo.InvariantCulture)]),
            ("1", "2", "3", "4", "5", "6", "7", "8", 9));

        // Assert
        two.ShouldBe("1+2");
        nine.ShouldBe("1+2+3+4+5+6+7+8+9");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void GetWording_OfTextThatTakesValues_ForCultureTheAppDoesNotShow_ShouldKeepItsSetAndEnterNoScope()
    {
        // Arrange: a French user of an app in English and Dutch gets English. Every text that wording shows falls back
        // to English too, so the set's method needs no scope of English, as a property doesn't.
        using var _ = new CulturesScope("fr-FR");

        // Act: the first call looks the set up, and the next one words the text with the set kept for it.
        var first = CulturesOf("Ada");
        var second = CulturesOf("Max");
        var resolved = ResolvedSets<ICulturesLocalization>.Get(CultureInfo.GetCultureInfo("fr-FR"));

        // Assert: in the cultures of the user, and kept for the generated class to call itself.
        first.ShouldBe("Ada: fr-FR/fr-FR");
        second.ShouldBe("Max: fr-FR/fr-FR");
        resolved.SetForValues.ShouldBeOfType<CulturesLocalizationEn>();
        resolved.Fallback.ShouldBeNull();
        CultureInfo.CurrentUICulture.Name.ShouldBe("fr-FR");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void GetWording_OfTextThatTakesValues_WithFallbackOfLanguageWithoutCulture_ShouldEnterNoScope()
    {
        // Arrange: a Belgian user, and a fallback whose language has no culture on this machine, as under invariant
        // globalization. No other test reads for fr-BE, so none finds this fallback.
        using var _ = new CulturesScope("fr-BE");
        var resolved = ResolvedSets<ICulturesLocalization>.Get(CultureInfo.GetCultureInfo("fr-BE"));
        resolved.KeepFallback(GeneratedCode.GetDefaultSet<CulturesLocalizationEn>(), culture: null);

        // Act
        var wording = CulturesOf("Max");

        // Assert: no scope of the language, so the set's method runs in the cultures of the user.
        wording.ShouldBe("Max: fr-BE/fr-BE");
        CultureInfo.CurrentUICulture.Name.ShouldBe("fr-BE");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void GetWording_OfTextThatTakesValues_WhereTheKeptFallbackGivesNull_ShouldAskItOnce()
    {
        // Arrange: a fallback kept with the culture of its language, as for a Canadian user of an app that shows
        // fr-CA where the contract lacks a French set. No other test reads this contract for fr-CA.
        using var _ = new CulturesScope("fr-CA");
        var resolved = ResolvedSets<INicknameLocalization>.Get(CultureInfo.GetCultureInfo("fr-CA"));
        resolved.KeepFallback(GeneratedCode.GetDefaultSet<NicknameLocalizationEn>(), CultureInfo.GetCultureInfo("en"));
        var reads = NicknameLocalizationEn.Reads;

        // Act: a read whose value makes that set give null.
        var exception = Should.Throw<InvalidOperationException>(() => Nickname(null));

        // Assert: the set was asked once, and the exception names it.
        NicknameLocalizationEn.Reads.ShouldBe(reads + 1);
        exception.Message.ShouldContain(nameof(NicknameLocalizationEn));
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
        GeneratedCode.GetLocalizationSet<ILazyLocalization>().ShouldBeNull();
        LazyLocalizationEn.Created.ShouldBe(0);

        // The first text that needs the set creates it, and every text after it reads the same one.
        GeneratedCode.ResolveWording<ILazyLocalization, LazyLocalizationEn>("Checkout", static set => set.Checkout).ShouldBe("Checkout");
        GeneratedCode.ResolveWording<ILazyLocalization, LazyLocalizationEn>("Checkout", static set => set.Checkout).ShouldBe("Checkout");
        LazyLocalizationEn.Created.ShouldBe(1);
        GeneratedCode.GetLocalizationSet<ILazyLocalization>().ShouldBeSameAs(GeneratedCode.GetDefaultSet<LazyLocalizationEn>());
    }

    [Test]
    public void SetOfDefaultLanguageWhoseConstructorThrows_ShouldNotFailUntilATextNeedsIt()
    {
        // Arrange
        using var _ = new CulturesScope("en-US");

        // Act & Assert: asking for a kept set creates none, so the generated class is there for every text.
        GeneratedCode.GetLocalizationSet<IThrowingLocalization>().ShouldBeNull();
        ThrowingLocalizationEn.Created.ShouldBe(0);

        // What needs the set says which set threw, with the set's own exception inside, and so does every read after
        // it, which tries again.
        var first = Should.Throw<InvalidOperationException>(() => GeneratedCode.ResolveWording<IThrowingLocalization, ThrowingLocalizationEn>("Checkout", static set => set.Checkout));
        var second = Should.Throw<InvalidOperationException>(() => GeneratedCode.ResolveWording<IThrowingLocalization, ThrowingLocalizationEn>("Checkout", static set => set.Checkout));

        first.Message.ShouldContain(nameof(ThrowingLocalizationEn));
        first.InnerException.ShouldBeOfType<FormatException>().Message.ShouldBe("No shop name.");
        second.Message.ShouldBe(first.Message);
        ThrowingLocalizationEn.Created.ShouldBe(2);
        GeneratedCode.GetLocalizationSet<IThrowingLocalization>().ShouldBeNull();
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
        GeneratedCode.GetKeyedValue<int>("Items", [3, "Idioma"], parameterIndex: 0, parameterCount: 2, "count", acceptsNull: true).ShouldBe(3);
        GeneratedCode.GetKeyedValue<string>("Items", [3, "Idioma"], parameterIndex: 1, parameterCount: 2, "shop", acceptsNull: false).ShouldBe("Idioma");
        GeneratedCode.GetKeyedValue<string?>("Greeting", [null], parameterIndex: 0, parameterCount: 1, "name", acceptsNull: true).ShouldBeNull();
        GeneratedCode.GetKeyedValue<int?>("Items", [null], parameterIndex: 0, parameterCount: 1, "count", acceptsNull: true).ShouldBeNull();
        GeneratedCode.GetKeyedValue<object>("Items", [3], parameterIndex: 0, parameterCount: 1, "count", acceptsNull: false).ShouldBe(3);
    }

    [Test]
    public void GetKeyedValue_ShouldRefuseAnotherNumberOfValuesThanTheTextTakes()
    {
        // Arrange & Act
        var tooFew = Should.Throw<ArgumentException>(() => GeneratedCode.GetKeyedValue<int>("Items", [], parameterIndex: 0, parameterCount: 2, "count", acceptsNull: true));
        var tooMany = Should.Throw<ArgumentException>(() => GeneratedCode.GetKeyedValue<string>("Greeting", ["Anna", "Max"], parameterIndex: 0, parameterCount: 1, "name", acceptsNull: false));

        // Assert
        tooFew.ParamName.ShouldBe("values");
        tooFew.Message.ShouldStartWith("The text 'Items' takes 2 values, but 0 were passed.");
        tooMany.Message.ShouldStartWith("The text 'Greeting' takes 1 value, but 2 were passed.");
    }

    [Test]
    public void GetKeyedValue_ShouldRefuseValueOfAnotherTypeWithoutConvertingIt()
    {
        // Arrange & Act: an int for a long, and null for a type that has none.
        var otherType = Should.Throw<ArgumentException>(() => GeneratedCode.GetKeyedValue<long>("Items", [3], parameterIndex: 0, parameterCount: 1, "count", acceptsNull: true));
        var noValue = Should.Throw<ArgumentException>(() => GeneratedCode.GetKeyedValue<int>("Items", [null], parameterIndex: 0, parameterCount: 1, "count", acceptsNull: true));

        // Assert
        otherType.ParamName.ShouldBe("values");
        otherType.Message.ShouldStartWith("The text 'Items' takes a value of type 'System.Int64' for 'count', but a value of type 'System.Int32' was passed.");
        otherType.Message.ShouldContain("Pass a value of that type, because a value isn't converted.");
        noValue.Message.ShouldStartWith("The text 'Items' takes a value of type 'System.Int32' for 'count', which isn't nullable, but null was passed.");
    }

    [Test]
    public void GetKeyedValue_ForReferenceTypeThatIsNotNullable_ShouldRefuseNull()
    {
        // Arrange & Act: a string, which isn't nullable, where the method of the set would read the value without a check.
        var refused = Should.Throw<ArgumentException>(() => GeneratedCode.GetKeyedValue<string>("Greeting", [null], parameterIndex: 0, parameterCount: 1, "name", acceptsNull: false));

        // Assert
        refused.ParamName.ShouldBe("values");
        refused.Message.ShouldStartWith("The text 'Greeting' takes a value of type 'System.String' for 'name', which isn't nullable, but null was passed. Pass a value that isn't null.");
    }

    [Test]
    public void GetKeyedValue_ForStringParameter_ShouldGiveTheStringItself()
    {
        // Arrange: a text among the values is a string, as every text is.
        const string cart = "Cart";

        // Act
        var value = GeneratedCode.GetKeyedValue<string>("Removed", [cart], parameterIndex: 0, parameterCount: 1, "what", acceptsNull: false);

        // Assert
        value.ShouldBeSameAs(cart);
    }

    [Test]
    public void GetKeyedValue_ForOtherParameter_ShouldRefuseStringWithoutConvertingIt()
    {
        // Arrange & Act: a string for a number isn't parsed, and a string for a type it converts to in code isn't
        // converted.
        var forNumber = Should.Throw<ArgumentException>(() => GeneratedCode.GetKeyedValue<int>("Items", ["3"], parameterIndex: 0, parameterCount: 1, "count", acceptsNull: true));
        var forLabel = Should.Throw<ArgumentException>(() => GeneratedCode.GetKeyedValue<Label>("Removed", ["Cart"], parameterIndex: 0, parameterCount: 1, "what", acceptsNull: false));

        // Assert
        forNumber.Message.ShouldStartWith("The text 'Items' takes a value of type 'System.Int32' for 'count', but a value of type 'System.String' was passed.");
        forLabel.Message.ShouldStartWith($"The text 'Removed' takes a value of type '{typeof(Label)}' for 'what', but a value of type 'System.String' was passed.");
    }

    [Test]
    public void CreateNullKeyException_ShouldNameTheKeyParameter()
    {
        // Arrange & Act
        var exception = GeneratedCode.CreateNullKeyException<IGeneratedCodeLocalization>(key: null);

        // Assert
        exception.ParamName.ShouldBe("key");
    }
}
