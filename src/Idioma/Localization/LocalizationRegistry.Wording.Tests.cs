using CodeChops.Idioma.Internals;
using CodeChops.Idioma.Tests.TestHelpers;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for the wording of a text of a set: the order a text falls back in (<c>LocalizationRegistry.Word</c>), the
/// cultures a set's member runs in, the sets a reader's culture keeps per text (<see cref="ResolvedSets{TContract}"/>),
/// and what the generated class reads through <see cref="GeneratedCode"/>.
/// </summary>
/// <remarks>
/// A test that asks for the wording of a culture in sets of its own calls <c>Word</c> on a registry from
/// <see cref="LocalizationRegistry.Create(IEnumerable{ILocalizationSet})"/>. A test of what the generated class reads
/// goes through the sets of the whole app, with a contract no other test uses.
/// </remarks>
public sealed class LocalizationRegistryWordingTests
{
    #region Fixture
    private interface ISetLocalization : ILocalizationSet
    {
        string Greeting { get; }
        string Farewell { get; }
    }

    private class SetLocalizationEn : ISetLocalization
    {
        public virtual string Greeting => "Hello";
        public virtual string Farewell => "Goodbye";
    }

    private sealed class SetLocalizationNl : ISetLocalization
    {
        public string Greeting => "Hallo";
        public string Farewell => "Tot ziens";
    }

    /// <summary>
    /// A Dutch set that gives no greeting, as a member that hands on a value that's null at run time.
    /// </summary>
    private sealed class SetWithoutGreetingLocalizationNl : ISetLocalization
    {
        public string Greeting => null!;
        public string Farewell => "Tot ziens";
    }

    /// <summary>
    /// An English set rewording one text of the default language.
    /// </summary>
    private sealed class SetRewordedLocalizationEn : SetLocalizationEn
    {
        public override string Greeting => "Hi";
    }

    /// <summary>
    /// A second English set rewording another text, as a shared library's rewording next to an app's.
    /// </summary>
    private sealed class SetSecondRewordedLocalizationEn : SetLocalizationEn
    {
        public override string Farewell => "Bye";
    }

    /// <summary>
    /// An English set with every text of its own, deriving from no English set, though one text matches the default
    /// wording.
    /// </summary>
    private sealed class SetStandaloneEnglishLocalization : ISetLocalization
    {
        public string LocalizationLanguageCode => "en";
        public string Greeting => "Hello";
        public string Farewell => "So long";
    }

    /// <summary>
    /// A library's British set, deriving from its English set and wording one text its own way.
    /// </summary>
    private sealed class SetBritishLocalization : SetLocalizationEn, ILocalizationSet
    {
        string ILocalizationSet.LocalizationLanguageCode => "en-GB";

        public override string Farewell => "Cheerio";
    }

    /// <summary>
    /// A British set with every text of its own, deriving from no English set, though one text matches the English
    /// wording.
    /// </summary>
    private sealed class SetStandaloneBritishLocalization : ISetLocalization
    {
        public string LocalizationLanguageCode => "en-GB";
        public string Greeting => "Hello";
        public string Farewell => "Cheerio";
    }

    /// <summary>
    /// Three levels of one language, each deriving from the one above (zh, zh-Hant, zh-Hant-TW); the middle one words
    /// one text its own way.
    /// </summary>
    private class SetChineseLocalization : ISetLocalization
    {
        public virtual string LocalizationLanguageCode => "zh";
        public virtual string Greeting => "Ni hao";
        public virtual string Farewell => "Zaijian";
    }

    private class SetTraditionalChineseLocalization : SetChineseLocalization
    {
        public override string LocalizationLanguageCode => "zh-Hant";
        public override string Farewell => "Zaijian (Hant)";
    }

    private sealed class SetTaiwaneseLocalization : SetTraditionalChineseLocalization
    {
        public override string LocalizationLanguageCode => "zh-Hant-TW";
    }

    /// <summary>
    /// An app's rewording of the top level.
    /// </summary>
    private sealed class SetRewordedChineseLocalization : SetChineseLocalization
    {
        public override string Greeting => "Nin hao";
    }

    /// <summary>
    /// An English set that counts how often its farewell is read, per instance. The tests that use it record the sets
    /// deriving from it, as the generated registrar records a set the build found (<c>RecordSet</c>).
    /// </summary>
    private class RecordedLocalizationEn : ISetLocalization
    {
        public int FarewellReads { get; private set; }

        public virtual string Greeting => "Hello";

        public virtual string Farewell
        {
            get
            {
                this.FarewellReads++;
                return "Goodbye";
            }
        }
    }

    /// <summary>
    /// An English set that counts how often it's created, of every instance. Only one test uses it.
    /// </summary>
    private sealed class CountedLocalizationEn : ISetLocalization
    {
        private static int _created;

        public CountedLocalizationEn() => Interlocked.Increment(ref _created);

        public static int Created => Volatile.Read(ref _created);

        public string Greeting => "Hello";
        public string Farewell => "Goodbye";
    }

    /// <summary>
    /// An app's rewording of the greeting, recorded as wording that text alone.
    /// </summary>
    private sealed class RecordedRewordedLocalizationEn : RecordedLocalizationEn
    {
        public override string Greeting => "Hi";
    }

    /// <summary>
    /// A British set that words the greeting itself, as English does, and the farewell its own way, recorded as wording
    /// both.
    /// </summary>
    private sealed class RecordedBritishLocalization : RecordedLocalizationEn, ILocalizationSet
    {
        string ILocalizationSet.LocalizationLanguageCode => "en-GB";

        public override string Greeting => "Hello";
        public override string Farewell => "Cheerio";
    }

    /// <summary>
    /// A British set that words the greeting itself and gives none, as a member that hands on a value that's null at
    /// run time. It's recorded as wording that text.
    /// </summary>
    private sealed class RecordedSilentBritishLocalization : RecordedLocalizationEn, ILocalizationSet
    {
        string ILocalizationSet.LocalizationLanguageCode => "en-GB";

        public override string Greeting => null!;
    }

    /// <summary>
    /// A contract no other test uses, for the test that rewords its default language.
    /// </summary>
    private interface ISetDefaultRewordedLocalization : ILocalizationSet
    {
        string Checkout { get; }
    }

    private class SetDefaultRewordedLocalizationEn : ISetDefaultRewordedLocalization
    {
        public virtual string Checkout => "Checkout";
    }

    /// <summary>
    /// An app's rewording of the default language, as the docs' BrandedShopLocalizationEn.
    /// </summary>
    private sealed class SetBrandedDefaultRewordedLocalizationEn : SetDefaultRewordedLocalizationEn
    {
        public override string Checkout => "Pay now";
    }

    /// <summary>
    /// A contract with texts that take a value: one that chooses its wording by it, and one that formats it.
    /// </summary>
    private interface ISetCountedLocalization : ILocalizationSet
    {
        string Items(int count);
        string Total(decimal amount);
    }

    private sealed class SetCountedLocalizationEn : ISetCountedLocalization
    {
        public string Items(int count) => count is 1 ? "1 item" : $"{count} items";
        public string Total(decimal amount) => $"Total: {amount:N2}";
    }

    private sealed class SetCountedLocalizationNl : ISetCountedLocalization
    {
        public string Items(int count) => count is 1 ? "1 artikel" : $"{count} artikelen";
        public string Total(decimal amount) => $"Totaal: {amount:N2}";
    }

    /// <summary>
    /// A French set whose texts can't be worded.
    /// </summary>
#pragma warning disable LOC402 // It words no value on purpose, because it throws.
    private sealed class SetCountedLocalizationFr : ISetCountedLocalization
    {
        public string Items(int count) => throw new InvalidOperationException("No wording.");
        public string Total(decimal amount) => throw new InvalidOperationException("No wording.");
    }
#pragma warning restore LOC402

    /// <summary>
    /// A contract whose texts show the cultures their member runs in: the UI culture, the culture values are formatted
    /// for, and the culture of the scope the text is worded in, if any.
    /// </summary>
    private interface ISetThreadCultureLocalization : ILocalizationSet
    {
        string Cultures { get; }
        string CulturesOf(int number);
    }

    private sealed class SetThreadCultureLocalizationEn : ISetThreadCultureLocalization
    {
        public string Cultures => $"{CultureInfo.CurrentUICulture.Name}/{CultureInfo.CurrentCulture.Name}/{WordingScope.Current?.Name ?? "none"}";
        public string CulturesOf(int number) => $"{number}: {this.Cultures}";
    }

    /// <summary>
    /// A contract no other test uses, for the tests that register its Dutch set with the sets of the whole app.
    /// </summary>
    private interface ISetRegisteredLocalization : ILocalizationSet
    {
        string Greeting { get; }
    }

    private sealed class SetRegisteredLocalizationEn : ISetRegisteredLocalization
    {
        public string Greeting => "Hello";
    }

    private sealed class SetRegisteredLocalizationNl : ISetRegisteredLocalization
    {
        public string Greeting => "Hallo";
    }

    /// <summary>
    /// The text of <see cref="ISetRegisteredLocalization"/>, read as a property of a generated class reads it.
    /// </summary>
    private static string RegisteredGreeting => GeneratedCode.FindSet<ISetRegisteredLocalization>(0, 1)?.Greeting
        ?? GeneratedCode.ResolveWording<ISetRegisteredLocalization, SetRegisteredLocalizationEn>(0, 1, "Greeting", static set => set.Greeting);

    /// <summary>
    /// A contract no other test uses, for the test that registers a rewording of its default language with the sets of
    /// the whole app.
    /// </summary>
    private interface ISetStaleLocalization : ILocalizationSet
    {
        string Greeting { get; }
    }

    private class SetStaleLocalizationEn : ISetStaleLocalization
    {
        public virtual string Greeting => "Hello";
    }

    /// <summary>
    /// A rewording of the default language, which registers alone, so it derives from no registered set.
    /// </summary>
    private sealed class SetStaleRewordedLocalizationEn : SetStaleLocalizationEn
    {
        public override string Greeting => "Hi";
    }

    /// <summary>
    /// The text of <see cref="ISetStaleLocalization"/>, read as a property of a generated class reads it.
    /// </summary>
    private static string StaleGreeting => GeneratedCode.FindSet<ISetStaleLocalization>(0, 1)?.Greeting
        ?? GeneratedCode.ResolveWording<ISetStaleLocalization, SetStaleLocalizationEn>(0, 1, "Greeting", static set => set.Greeting);

    /// <summary>
    /// A contract no other test uses, for the tests of the sets a reader's culture keeps in the sets of the whole app.
    /// Its English set is never registered, so an English reader's text keeps the set it was declared with.
    /// </summary>
    private interface ISetKeptLocalization : ILocalizationSet
    {
        string Checkout { get; }
        string Total(decimal amount);
    }

    private sealed class SetKeptLocalizationEn : ISetKeptLocalization
    {
        public string Checkout => "Checkout";
        public string Total(decimal amount) => $"Total: {amount:N2}";
    }

    private sealed class SetKeptLocalizationNl : ISetKeptLocalization
    {
        public string Checkout => "Afrekenen";
        public string Total(decimal amount) => $"Totaal: {amount:N2}";
    }

    /// <summary>
    /// The texts of <see cref="ISetKeptLocalization"/>, read as the members of a generated class read them.
    /// </summary>
    private static class KeptTexts
    {
        public static string Checkout => GeneratedCode.FindSet<ISetKeptLocalization>(0, 2)?.Checkout
            ?? WordCheckout();

        public static string Total(decimal amount) => GeneratedCode.FindSetForValues<ISetKeptLocalization>(1, 2)?.Total(amount)
            ?? WordTotal(amount);

        public static string WordCheckout() => GeneratedCode.ResolveWording<ISetKeptLocalization, SetKeptLocalizationEn>(0, 2, "Checkout", static set => set.Checkout);

        public static string WordTotal(decimal amount) => GeneratedCode.ResolveWording<ISetKeptLocalization, SetKeptLocalizationEn, decimal>(1, 2, "Total", static (set, amount) => set.Total(amount), amount);
    }

    /// <summary>
    /// A contract no other test uses, for the tests of <see cref="LocalizationRegistry.CurrentUICultureProvider"/>. One
    /// of its texts shows another, which its member reads itself.
    /// </summary>
    private interface ISetProvidedLocalization : ILocalizationSet
    {
        string Total(decimal amount);
        string Summary(decimal amount);
    }

    private sealed class SetProvidedLocalizationEn : ISetProvidedLocalization
    {
        public string Total(decimal amount) => $"Total: {amount:N2}";
        public string Summary(decimal amount) => $"Summary: {ProvidedTexts.Total(amount)}";
    }

    private sealed class SetProvidedLocalizationNl : ISetProvidedLocalization
    {
        public string Total(decimal amount) => $"Totaal: {amount:N2}";
        public string Summary(decimal amount) => $"Overzicht: {ProvidedTexts.Total(amount)}";
    }

    /// <summary>
    /// The texts of <see cref="ISetProvidedLocalization"/>, read as the methods of a generated class read them.
    /// </summary>
    private static class ProvidedTexts
    {
        public static string Total(decimal amount) => GeneratedCode.FindSetForValues<ISetProvidedLocalization>(0, 2)?.Total(amount)
            ?? WordTotal(amount);

        public static string Summary(decimal amount) => GeneratedCode.FindSetForValues<ISetProvidedLocalization>(1, 2)?.Summary(amount)
            ?? WordSummary(amount);

        public static string WordTotal(decimal amount) => GeneratedCode.ResolveWording<ISetProvidedLocalization, SetProvidedLocalizationEn, decimal>(0, 2, "Total", static (set, amount) => set.Total(amount), amount);

        public static string WordSummary(decimal amount) => GeneratedCode.ResolveWording<ISetProvidedLocalization, SetProvidedLocalizationEn, decimal>(1, 2, "Summary", static (set, amount) => set.Summary(amount), amount);
    }

    /// <summary>
    /// A contract whose texts are properties that show another text, which their member reads itself: an inline one,
    /// and one that takes a value.
    /// </summary>
    private interface ISetShowingLocalization : ILocalizationSet
    {
        string Hint { get; }
        string Summary { get; }
    }

    private static string Cart => LocalizedText.Inline(en: "Cart", nl: "Winkelwagen");

    private sealed class SetShowingLocalizationEn : ISetShowingLocalization
    {
        public string Hint => $"Open the {Cart} to pay";
        public string Summary => $"Summary: {ProvidedTexts.Total(1234.5m)}";
    }

    private sealed class SetShowingLocalizationNl : ISetShowingLocalization
    {
        public string Hint => $"Open de {Cart} om te betalen";
        public string Summary => $"Overzicht: {ProvidedTexts.Total(1234.5m)}";
    }

    /// <summary>
    /// The texts of <see cref="ISetShowingLocalization"/>, read as the properties of a generated class read them.
    /// </summary>
    private static class ShowingTexts
    {
        public static string Hint => GeneratedCode.FindSet<ISetShowingLocalization>(0, 2)?.Hint
            ?? GeneratedCode.ResolveWording<ISetShowingLocalization, SetShowingLocalizationEn>(0, 2, "Hint", static set => set.Hint);

        public static string Summary => GeneratedCode.FindSet<ISetShowingLocalization>(1, 2)?.Summary
            ?? GeneratedCode.ResolveWording<ISetShowingLocalization, SetShowingLocalizationEn>(1, 2, "Summary", static set => set.Summary);
    }

    private interface ISetFlagLocalization : ILocalizationSet
    {
        string Flagged(int value);
    }

    private sealed class SetFlagLocalizationEn : ISetFlagLocalization
    {
        public static AsyncLocal<string?> Flag { get; } = new();

        public string Flagged(int value)
        {
            Flag.Value = "inner";
            return $"Flagged {value}";
        }
    }

    /// <summary>
    /// An American culture under a name the UI culture of a thread can't take, as a culture of a derived class can have.
    /// </summary>
    private sealed class UnnamableCulture() : CultureInfo("en-US")
    {
        public override string Name => "en US";
    }

    private static CultureInfo Culture(string name) => CultureInfo.GetCultureInfo(name);

    /// <summary>
    /// The wording of a text for a reader's culture in a registry, as <c>LocalizationRegistry.Word</c> gives it.
    /// </summary>
    private static string Word<TContract, TDefaultSet>(LocalizationRegistry registry, CultureInfo culture, string name, Func<TContract, string?> select,
        bool takesValues = false, CultureInfo? valuesCulture = null, ResolvedSets<TContract>? resolved = null, int text = -1)
        where TContract : class
        where TDefaultSet : class, ILocalizationSet, TContract, new()
    {
        var wording = registry.Word(registry.Resolve(culture), culture, valuesCulture, name, select, typeof(TDefaultSet), static () => GeneratedCode.GetDefaultSet<TDefaultSet>(), takesValues, resolved, text);
        return wording;
    }

    private static string Greeting(LocalizationRegistry registry, string culture)
    {
        return Word<ISetLocalization, SetLocalizationEn>(registry, Culture(culture), "Greeting", static set => set.Greeting);
    }

    private static string Farewell(LocalizationRegistry registry, string culture)
    {
        return Word<ISetLocalization, SetLocalizationEn>(registry, Culture(culture), "Farewell", static set => set.Farewell);
    }

    /// <summary>
    /// The total of a text that takes a value, with the value formatted for the culture too, as for a user whose
    /// culture <see cref="LocalizationRegistry.CurrentUICultureProvider"/> gives.
    /// </summary>
    private static string Total(LocalizationRegistry registry, CultureInfo culture, bool formatsForCulture = true)
    {
        return Word<ISetCountedLocalization, SetCountedLocalizationEn>(registry, culture, "Total", static set => set.Total(1234.5m),
            takesValues: true, valuesCulture: formatsForCulture ? culture : null);
    }

    /// <summary>
    /// The sets a culture keeps for the two texts of <see cref="ISetLocalization"/> in a registry, after each text was
    /// worded once, as a property and as a text that takes values.
    /// </summary>
    private static ResolvedSets<ISetLocalization> ResolveGreetingAndFarewell(LocalizationRegistry registry, string cultureName)
    {
        var culture = Culture(cultureName);
        var resolved = new ResolvedSets<ISetLocalization>(registry.Resolve(culture), count: 2);

        Word<ISetLocalization, SetLocalizationEn>(registry, culture, "Greeting", static set => set.Greeting, resolved: resolved, text: 0);
        Word<ISetLocalization, SetLocalizationEn>(registry, culture, "Farewell", static set => set.Farewell, resolved: resolved, text: 1);

        return resolved;
    }
    #endregion

    [Test]
    public void Word_InRegistryWithoutLanguages_ShouldShowEveryLanguageWithASet()
    {
        // Arrange: no languages given, so nothing of what the app of this process declares applies.
        var registry = LocalizationRegistry.Create([new SetLocalizationNl()]);

        // Act & Assert: the Dutch set shows, and a culture without a set gets the text's own default wording.
        Greeting(registry, "nl-BE").ShouldBe("Hallo");
        Greeting(registry, "fr-FR").ShouldBe("Hello");
    }

    [Test]
    public void Word_InRegistryWithLanguages_ShouldShowOnlyThoseAndFallBackToItsDefault()
    {
        // Arrange: a registry that has Dutch, with and without Dutch among its languages.
        ILocalizationSet[] sets = [new SetLocalizationEn(), new SetLocalizationNl()];
        var dutchFirst = LocalizationRegistry.Create(sets, "nl", "en");
        var withoutDutch = LocalizationRegistry.Create(sets, "en", "fr");

        // Act & Assert: a culture no language serves gets the registry's default language, even where it has a set.
        Greeting(dutchFirst, "fr-FR").ShouldBe("Hallo");
        Greeting(dutchFirst, "en-GB").ShouldBe("Hello");
        Greeting(withoutDutch, "nl-NL").ShouldBe("Hello");
    }

    [Test]
    public void Word_ShouldFallBackToDefaultLanguageSetWithoutCultureWalk()
    {
        // Arrange: an English set rewording a text. The invariant culture (the UI culture of an app under invariant
        // globalization, where no other culture can be constructed) has no chain to walk.
        var registry = LocalizationRegistry.Create([new SetRewordedLocalizationEn(), new SetLocalizationNl()]);

        // Act & Assert: in an app without a default language of its own, the default-language set serves the invariant
        // culture and an uncovered one, and a covered culture keeps its own set.
        Greeting(registry, "").ShouldBe("Hi");
        Greeting(registry, "fr-FR").ShouldBe("Hi");
        Greeting(registry, "nl-NL").ShouldBe("Hallo");
    }

    [Test]
    public void Word_ShouldFallBackToSetOfAppDefaultLanguageBeforeItsOwn()
    {
        // Arrange: a contract whose default language is English, with an English set rewording a text, in apps whose
        // default language is Dutch.
        ILocalizationSet[] sets = [new SetRewordedLocalizationEn(), new SetLocalizationNl()];

        // Act & Assert: an uncovered culture gets the app's language, also through a regional app language (nl-BE
        // through nl). A covered culture keeps its own set. An app language without a set leaves the text's own
        // default language.
        Greeting(LocalizationRegistry.Create(sets, "nl"), "fr-FR").ShouldBe("Hallo");
        Greeting(LocalizationRegistry.Create(sets, "nl"), "").ShouldBe("Hallo");
        Greeting(LocalizationRegistry.Create(sets, "nl-BE"), "fr-FR").ShouldBe("Hallo");
        Greeting(LocalizationRegistry.Create(sets, "nl", "en"), "en-GB").ShouldBe("Hi");
        Greeting(LocalizationRegistry.Create(sets, "de"), "fr-FR").ShouldBe("Hi");
    }

    [Test]
    public void Word_ForCultureAppDoesNotDeclare_ShouldShowSetOfAppDefaultLanguageThoughSetExists()
    {
        // Arrange: a library's contract with English and Dutch sets, in an app declaring ("en", "fr").
        ILocalizationSet[] sets = [new SetRewordedLocalizationEn(), new SetLocalizationNl()];

        // Act & Assert: a Dutch user sees the English set, as the app's own texts are English for them too. An app
        // without languages shows the Dutch set.
        Greeting(LocalizationRegistry.Create(sets, "en", "fr"), "nl-NL").ShouldBe("Hi");
        Greeting(LocalizationRegistry.Create(sets), "nl-NL").ShouldBe("Hallo");
    }

    [Test]
    public void Word_ForCultureWithoutWording_ShouldTakeSetThatRewordsDefaultLanguageBeforeTheSetTheTextWasDeclaredWith()
    {
        // Arrange: a French user of an app without languages, with and without a rewording of the text's default
        // language.
        var french = Culture("fr-FR");
        var reworded = LocalizationRegistry.Create([new SetBrandedDefaultRewordedLocalizationEn()]);
        var empty = LocalizationRegistry.Create([]);

        static string Checkout(LocalizationRegistry registry, CultureInfo culture)
        {
            return Word<ISetDefaultRewordedLocalization, SetDefaultRewordedLocalizationEn>(registry, culture, "Checkout", static set => set.Checkout);
        }

        // Act & Assert: the rewording, as a user whose language has no wording sees it. A registry without that
        // rewording still ends at the set the text was declared with.
        Checkout(reworded, french).ShouldBe("Pay now");
        Checkout(empty, french).ShouldBe("Checkout");
    }

    [Test]
    public void Word_ForCultureWithoutSet_ShouldCreateNoSecondDefaultSetWhereTheRegisteredOneWordsIt()
    {
        // Arrange: a registry without languages, as in a host that declares none, with the English set registered and
        // recorded, as the generated registrar records it.
        LocalizationRegistry.RecordSet<CountedLocalizationEn>("en", []);
        var registry = LocalizationRegistry.Create([new CountedLocalizationEn()]);
        var created = CountedLocalizationEn.Created;

        // Act
        var greeting = Word<ISetLocalization, CountedLocalizationEn>(registry, Culture("fr-FR"), "Greeting", static set => set.Greeting);

        // Assert: the registered set words it, and no instance is created only to tell its language.
        greeting.ShouldBe("Hello");
        CountedLocalizationEn.Created.ShouldBe(created);
    }

    [Test]
    public void Word_WhereSetGivesNull_ShouldFallThroughToTheNextWording()
    {
        // Arrange: a Dutch set that gives null for one text, as a value read at run time can be.
        ILocalizationSet[] sets = [new SetRewordedLocalizationEn(), new SetWithoutGreetingLocalizationNl()];

        // Act & Assert: the app's default language, else the text's own default language, and the Dutch wording of
        // the text the set does give.
        Greeting(LocalizationRegistry.Create(sets, "en", "nl"), "nl-NL").ShouldBe("Hi");
        Greeting(LocalizationRegistry.Create(sets), "nl-NL").ShouldBe("Hi");
        Greeting(LocalizationRegistry.Create([new SetWithoutGreetingLocalizationNl()], "nl"), "nl-NL").ShouldBe("Hello");
        Farewell(LocalizationRegistry.Create(sets, "en", "nl"), "nl-NL").ShouldBe("Tot ziens");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void ResolvedSets_WhereSetOfTheCultureGivesNull_ShouldNotKeepTheSetFoundAfterIt()
    {
        // Arrange: a British set that gives null for the greeting, which it words itself, next to the English set.
        LocalizationRegistry.RecordSet<RecordedSilentBritishLocalization>(null, ["Greeting"]);
        var english = new RecordedLocalizationEn();
        var registry = LocalizationRegistry.Create([english, new RecordedSilentBritishLocalization()], "en", "nl");

        // Act
        var resolved = ResolveGreetingAndFarewell(registry, "en-GB");

        // Assert: the greeting falls through to English for this read, and no set is kept for it. Kept, the English
        // set would word it on every later read, also once the British set gives a wording. The farewell, which the
        // British set leaves to English, is kept.
        Greeting(registry, "en-GB").ShouldBe("Hello");
        resolved.Sets[0].ShouldBeNull();
        resolved.SetsForValues[0].ShouldBeNull();
        resolved.Sets[1].ShouldBeSameAs(english);
    }

    [Test]
    public void Word_WhereSetOfDefaultLanguageGivesNull_ShouldThrow()
    {
        // Arrange: sets that give null, as a value read at run time can be, for a text without values and one with.
        var registry = LocalizationRegistry.Create([]);
        var french = Culture("fr-FR");

        // Act
        var property = Should.Throw<InvalidOperationException>(() => Word<ISetLocalization, SetLocalizationEn>(registry, french, "Missing", static _ => null));
        var method = Should.Throw<InvalidOperationException>(() => Word<ISetCountedLocalization, SetCountedLocalizationEn>(registry, french, "MissingItems", static _ => null, takesValues: true));

        // Assert: the default wording is the last fallback, so the text names the set that gave null rather than show
        // null.
        property.Message.ShouldContain("'Missing'");
        property.Message.ShouldContain(nameof(SetLocalizationEn));
        property.Message.ShouldContain("'en'");
        method.Message.ShouldContain(nameof(SetCountedLocalizationEn));
        method.Message.ShouldContain("'MissingItems' of 'ISetCountedLocalization'");
    }

    [Test]
    public void Word_ForRegion_ShouldTakeRewordingOfItsLanguageWhereRegionalSetKeepsItsWording()
    {
        // Arrange: a library's English and British sets, and an app's rewording of the English set registered after
        // them, as an override is in the sets of the whole app. Then the same with the rewording before the British set.
        ILocalizationSet[] sets = [new SetLocalizationEn(), new SetBritishLocalization(), new SetRewordedLocalizationEn()];
        var registry = LocalizationRegistry.Create(sets, "en", "nl");
        var rewordingFirst = LocalizationRegistry.Create([new SetLocalizationEn(), new SetRewordedLocalizationEn(), new SetBritishLocalization()], "en", "nl");
        var britishApp = LocalizationRegistry.Create(sets, "en-GB", "nl");

        // Act & Assert: a British user gets the rewording of the text the British set inherits, whichever registered
        // first, and the British wording of the text it words itself. So does an uncovered culture in an app whose
        // default language is British English. Other English cultures get the English sets.
        Greeting(registry, "en-GB").ShouldBe("Hi");
        Greeting(rewordingFirst, "en-GB").ShouldBe("Hi");
        Farewell(registry, "en-GB").ShouldBe("Cheerio");
        Greeting(britishApp, "fr-FR").ShouldBe("Hi");
        Farewell(britishApp, "fr-FR").ShouldBe("Cheerio");
        Greeting(registry, "en-US").ShouldBe("Hi");
        Farewell(registry, "en-US").ShouldBe("Goodbye");
    }

    [Test]
    public void Word_ShouldShowEveryRewordingOfOneSetForTextsItChanges()
    {
        // Arrange: two rewordings of the English set, each changing another text, as a shared library's and an app's.
        // The same next to the British set. And a set implementing the contract in full, registered after a rewording.
        ILocalizationSet[] rewordings = [new SetLocalizationEn(), new SetRewordedLocalizationEn(), new SetSecondRewordedLocalizationEn()];
        var registry = LocalizationRegistry.Create(rewordings, "en");
        var withoutLanguages = LocalizationRegistry.Create(rewordings);
        var withBritish = LocalizationRegistry.Create(
            [
                new SetLocalizationEn(),
                new SetBritishLocalization(),
                new SetRewordedLocalizationEn(),
                new SetSecondRewordedLocalizationEn(),
            ],
            "en");
        var standaloneLast = LocalizationRegistry.Create([new SetLocalizationEn(), new SetRewordedLocalizationEn(), new SetStandaloneEnglishLocalization()], "en");

        // Act & Assert: each text shows the last rewording that changes it, also as the default-language fallback of an
        // app without languages, and through the British set for a text it inherits. The British set's own wording
        // stays. A set deriving from no other set words every text itself, even one matching the default wording.
        Greeting(registry, "en-US").ShouldBe("Hi");
        Farewell(registry, "en-US").ShouldBe("Bye");
        Greeting(withoutLanguages, "fr-FR").ShouldBe("Hi");
        Farewell(withoutLanguages, "fr-FR").ShouldBe("Bye");
        Greeting(withBritish, "en-GB").ShouldBe("Hi");
        Farewell(withBritish, "en-GB").ShouldBe("Cheerio");
        Greeting(standaloneLast, "en-US").ShouldBe("Hello");
        Farewell(standaloneLast, "en-US").ShouldBe("So long");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void Word_ForRecordedRegion_ShouldKeepWordingItsClassWordsThoughItMatchesItsLanguage()
    {
        // Arrange: recorded as the build records them, so the registry knows which texts each set words itself.
        LocalizationRegistry.RecordSet<RecordedRewordedLocalizationEn>(null, ["Greeting"]);
        LocalizationRegistry.RecordSet<RecordedBritishLocalization>(null, ["Farewell", "Greeting"]);
        var registry = LocalizationRegistry.Create([new RecordedLocalizationEn(), new RecordedBritishLocalization(), new RecordedRewordedLocalizationEn()], "en", "nl");

        // Act & Assert: the British set words the greeting itself, so the English rewording doesn't reach it, though
        // its wording matches the English one. Compared by wording, it would show "Hi".
        Greeting(registry, "en-GB").ShouldBe("Hello");
        Farewell(registry, "en-GB").ShouldBe("Cheerio");
        Greeting(registry, "en-US").ShouldBe("Hi");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void Word_ForRecordedRewording_ShouldNotReadTextItLeavesAsItWas()
    {
        // Arrange
        LocalizationRegistry.RecordSet<RecordedRewordedLocalizationEn>(null, ["Greeting"]);
        var english = new RecordedLocalizationEn();
        var rewording = new RecordedRewordedLocalizationEn();
        var registry = LocalizationRegistry.Create([english, rewording], "en");

        // Act
        var farewell = Farewell(registry, "en-US");

        // Assert: the record says the rewording leaves the farewell as it was, so only the English set is read.
        farewell.ShouldBe("Goodbye");
        rewording.FarewellReads.ShouldBe(0);
        english.FarewellReads.ShouldBe(1);
    }

    [Test]
    public void Word_ForRegion_ShouldKeepRegionalSetDerivingFromNoSetOfItsLanguage()
    {
        // Arrange
        var registry = LocalizationRegistry.Create([new SetLocalizationEn(), new SetStandaloneBritishLocalization(), new SetRewordedLocalizationEn()], "en");

        // Act & Assert: the British set inherits nothing from English, so the English rewording does not apply to it,
        // even where its wording matches the English.
        Greeting(registry, "en-GB").ShouldBe("Hello");
    }

    [Test]
    public void Word_ForRegion_ShouldPassRewordingThroughEveryLevel()
    {
        // Arrange: walked by name, through the app's default language, so the levels don't depend on the parent
        // cultures of this machine.
        var registry = LocalizationRegistry.Create(
            [
                new SetChineseLocalization(),
                new SetTraditionalChineseLocalization(),
                new SetTaiwaneseLocalization(),
                new SetRewordedChineseLocalization(),
            ],
            "zh-Hant-TW");

        // Act & Assert: the Taiwanese set keeps the traditional wording, which keeps the Chinese one, so the rewording
        // of Chinese reaches Taiwan. The traditional set's own wording stays.
        Greeting(registry, "fr-FR").ShouldBe("Nin hao");
        Farewell(registry, "fr-FR").ShouldBe("Zaijian (Hant)");
    }

    [Test]
    public void Word_OfTextThatTakesValues_ShouldBeWordedWithThemBySetOfCultureElseBySetOfDefaultLanguage()
    {
        // Arrange
        const int count = 3;
        var registry = LocalizationRegistry.Create([new SetCountedLocalizationNl()]);

        string Items(string culture)
        {
            return Word<ISetCountedLocalization, SetCountedLocalizationEn>(registry, Culture(culture), "Items", set => set.Items(count), takesValues: true);
        }

        // Act & Assert: every set words the text with the values, and a culture without a set gets the default wording.
        Items("nl-NL").ShouldBe("3 artikelen");
        Items("fr-FR").ShouldBe("3 items");
    }

    [Test]
    public void Word_ForCultureThreadDoesNotHave_ShouldRunMethodOfSetInThatCultureAndPutCulturesBack()
    {
        // Arrange: both current cultures are American, and the reader is Dutch, as a user whose culture a provider
        // gives.
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-US");
        var registry = LocalizationRegistry.Create([new SetCountedLocalizationEn(), new SetCountedLocalizationNl(), new SetThreadCultureLocalizationEn()]);

        // Act & Assert: the Dutch wording, with the number as a Dutch reader writes it, and the cultures of this
        // thread as they were.
        Total(registry, Culture("nl-NL")).ShouldBe("Totaal: 1.234,50");
        Total(registry, Culture("en-GB")).ShouldBe("Total: 1,234.50");
        CultureInfo.CurrentCulture.Name.ShouldBe("en-US");
        CultureInfo.CurrentUICulture.Name.ShouldBe("en-US");
        WordingScope.Current.ShouldBeNull();

        // The method of the set runs in the reader's culture, in a scope of it.
        Word<ISetThreadCultureLocalization, SetThreadCultureLocalizationEn>(registry, Culture("en-GB"), "CulturesOf", static set => set.CulturesOf(1), takesValues: true, valuesCulture: Culture("en-GB"))
            .ShouldBe("1: en-GB/en-GB/en-GB");
    }

    [Test]
    public void Word_WithoutCultureForValues_ShouldFormatValuesAsTheThreadDoes()
    {
        // Arrange: a user who reads Dutch and writes numbers as an American does.
        using var _ = new CulturesScope(uiCulture: "nl-NL", culture: "en-US");
        var registry = LocalizationRegistry.Create([new SetCountedLocalizationEn(), new SetCountedLocalizationNl()]);

        // Act & Assert
        Total(registry, CultureInfo.CurrentUICulture, formatsForCulture: false).ShouldBe("Totaal: 1,234.50");
        CultureInfo.CurrentCulture.Name.ShouldBe("en-US");
    }

    [Test]
    public void Word_ForCultureThreadDoesNotHave_ShouldPutCulturesBackWhenSetThrows()
    {
        // Arrange
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-GB");
        var registry = LocalizationRegistry.Create([new SetCountedLocalizationFr()]);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => Total(registry, Culture("fr-FR"))).Message.ShouldBe("No wording.");
        CultureInfo.CurrentCulture.Name.ShouldBe("en-GB");
        CultureInfo.CurrentUICulture.Name.ShouldBe("en-US");
        WordingScope.Current.ShouldBeNull();
    }

    [Test]
    public void Word_ForCultureThreadCannotTake_ShouldLeaveTextsOnCultureOfUser()
    {
        // Arrange: a Dutch user, and a culture whose name the UI culture of a thread can't take.
        using var _ = new CulturesScope(uiCulture: "nl-NL", culture: "nl-NL");
        var registry = LocalizationRegistry.Create([new SetCountedLocalizationEn(), new SetCountedLocalizationNl()]);

        // Act & Assert: the thread refuses the culture, and a text after that still follows the user.
        Should.Throw<ArgumentException>(() => Total(registry, new UnnamableCulture()));
        Cart.ShouldBe("Winkelwagen");
        WordingScope.Current.ShouldBeNull();
        CultureInfo.CurrentUICulture.Name.ShouldBe("nl-NL");
        CultureInfo.CurrentCulture.Name.ShouldBe("nl-NL");
    }

    [Test]
    public void Word_ForCultureOfSameNameAsOneWordedBefore_ShouldFormatValuesForGivenCulture()
    {
        // Arrange: an English user, and two Dutch cultures of one name, of which the second writes a number its own
        // way.
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-US");
        var dutch = Culture("nl-NL");
        var ownDutch = (CultureInfo)dutch.Clone();
        ownDutch.NumberFormat.NumberGroupSeparator = " ";
        var registry = LocalizationRegistry.Create([new SetCountedLocalizationEn(), new SetCountedLocalizationNl()]);

        // Act & Assert: the registry keeps what it worked out for the first culture of the name, and each culture still
        // formats the number itself.
        Total(registry, dutch).ShouldBe("Totaal: 1.234,50");
        Total(registry, ownDutch).ShouldBe("Totaal: 1 234,50");
        Total(registry, dutch).ShouldBe("Totaal: 1.234,50");
    }

    [Test]
    [Arguments("nl-NL", "nl-NL")]
    [Arguments("en-US", "nl-NL")]
    public void Word_ForCultureOfSameNameAsCultureOfThread_ShouldFormatValuesForGivenCulture(string uiCulture, string culture)
    {
        // Arrange: a thread that formats as Dutch already, and a Dutch culture of the same name that writes a number its
        // own way. The two are equal as cultures, and still format differently.
        using var _ = new CulturesScope(uiCulture: uiCulture, culture: culture);
        var ownDutch = (CultureInfo)Culture("nl-NL").Clone();
        ownDutch.NumberFormat.NumberGroupSeparator = " ";
        var registry = LocalizationRegistry.Create([new SetCountedLocalizationEn(), new SetCountedLocalizationNl()]);

        // Act & Assert
        Total(registry, ownDutch).ShouldBe("Totaal: 1 234,50");
        CultureInfo.CurrentCulture.ShouldBeSameAs(Culture(culture));
    }

    [Test]
    public void Word_ForFallbackLanguage_ShouldRunMethodOfSetInUICultureOfThatLanguage()
    {
        // Arrange: a German user of an app in English and Dutch, so the text falls back to English.
        using var _ = new CulturesScope(uiCulture: "de-DE", culture: "de-DE");
        var german = CultureInfo.CurrentUICulture;
        var registry = LocalizationRegistry.Create([new SetThreadCultureLocalizationEn(), new SetCountedLocalizationEn()], "en", "nl");

        // Act
        var cultures = Word<ISetThreadCultureLocalization, SetThreadCultureLocalizationEn>(registry, german, "CulturesOf", static set => set.CulturesOf(1), takesValues: true);
        var total = Total(registry, german, formatsForCulture: false);

        // Assert: the method of the set runs in English, the language it words, and the values keep the reader's
        // format. The German UI culture of the thread is back after.
        cultures.ShouldBe("1: en/de-DE/en");
        total.ShouldBe("Total: 1.234,50");
        CultureInfo.CurrentUICulture.Name.ShouldBe("de-DE");
        CultureInfo.CurrentCulture.Name.ShouldBe("de-DE");
        WordingScope.Current.ShouldBeNull();
    }

    [Test]
    public void Word_ForFallbackLanguage_ShouldCopyExecutionContextOnlyToChangeUICulture()
    {
        // Arrange: the same text for an English user and for a German one of an app in English and Dutch. The German
        // user's text falls back to English, so both show "3 items". It runs the method of the set in English, and
        // the UI culture is an AsyncLocal, so setting it copies the execution context.
        var registry = LocalizationRegistry.Create([new SetCountedLocalizationEn(), new SetCountedLocalizationNl()], "en", "nl");
        var englishCulture = Culture("en");

        string Show()
        {
            return Word<ISetCountedLocalization, SetCountedLocalizationEn>(registry, CultureInfo.CurrentUICulture, "Items", static set => set.Items(3), takesValues: true);
        }

        // Act: per 10,000 texts, and per 10,000 times the UI culture changes and the context is restored, in the German
        // user's context, so with as many AsyncLocals to copy.
        long english;
        using (new CulturesScope(uiCulture: "en-US", culture: "en-US"))
            english = GeneratedSetsTests.Allocated(Show) / 10_000;

        long german;
        long changed;
        using (new CulturesScope(uiCulture: "de-DE", culture: "de-DE"))
        {
            Show().ShouldBe("3 items");
            german = GeneratedSetsTests.Allocated(Show) / 10_000;
            changed = GeneratedSetsTests.Allocated(() =>
            {
                var context = ExecutionContext.Capture()!;
                CultureInfo.CurrentUICulture = englishCulture;
                ExecutionContext.Restore(context);
                return context;
            }) / 10_000;
        }

        // Assert: one copy of the context for the fallback besides what the English user's text allocates. Setting the
        // UI culture back would copy it once more.
        german.ShouldBeLessThanOrEqualTo(english + changed);
    }

    [Test]
    [Arguments("en", "inner")] // The culture the thread has already: the scope changes no culture, and captures no context.
    [Arguments("de", "outer")] // Another culture, which falls back to English: the scope changes the UI culture.
    public void Word_WhenMethodOfSetChangesAnAsyncLocal_ShouldEndThatChangeOnlyWithAScopeThatChangedACulture(string cultureName, string expectedFlag)
    {
        // Arrange
        using var _ = new CulturesScope(CultureInfo.GetCultureInfo("en"), CultureInfo.CurrentCulture);
        SetFlagLocalizationEn.Flag.Value = "outer";
        var registry = LocalizationRegistry.Create([new SetFlagLocalizationEn()]);

        // Act
        var shown = Word<ISetFlagLocalization, SetFlagLocalizationEn>(registry, Culture(cultureName), "Flagged", static set => set.Flagged(1), takesValues: true);

        // Assert: restoring the context that a change of culture captured undoes the change of the set's method too.
        shown.ShouldBe("Flagged 1");
        SetFlagLocalizationEn.Flag.Value.ShouldBe(expectedFlag);
    }

    [Test]
    public void Word_OfPropertyThatFallsBackAlone_ShouldShowTheTextItReadsInTheLanguageItFallsBackTo()
    {
        // Arrange: a Dutch user of an app that has Dutch, and a contract without a Dutch set. The text the property
        // reads has Dutch wording.
        using var _ = new CulturesScope(uiCulture: "nl-NL", culture: "nl-NL");
        var dutch = CultureInfo.CurrentUICulture;
        ILocalizationSet[] sets = [new SetShowingLocalizationEn(), new SetThreadCultureLocalizationEn()];
        var englishFirst = LocalizationRegistry.Create(sets, "en", "nl");
        var frenchFirst = LocalizationRegistry.Create(sets, "fr", "nl");

        static string Hint(LocalizationRegistry registry, CultureInfo culture)
        {
            return Word<ISetShowingLocalization, SetShowingLocalizationEn>(registry, culture, "Hint", static set => set.Hint);
        }

        static string Cultures(LocalizationRegistry registry, CultureInfo culture)
        {
            return Word<ISetThreadCultureLocalization, SetThreadCultureLocalizationEn>(registry, culture, "Cultures", static set => set.Cultures);
        }

        // Act & Assert: one language throughout, and not "Open the Winkelwagen to pay". English through the app's
        // default language, and through the text's own default language where the app's has no set.
        Cart.ShouldBe("Winkelwagen");
        Hint(englishFirst, dutch).ShouldBe("Open the Cart to pay");
        Hint(frenchFirst, dutch).ShouldBe("Open the Cart to pay");

        // The property runs in a scope of the language it falls back to, which is the UI culture of the thread for
        // the call. The values keep the reader's format.
        Cultures(englishFirst, dutch).ShouldBe("en/nl-NL/en");
        Cultures(frenchFirst, dutch).ShouldBe("en/nl-NL/en");

        // And the user's culture again once the wording is done.
        Cart.ShouldBe("Winkelwagen");
        CultureInfo.CurrentUICulture.ShouldBeSameAs(dutch);
        WordingScope.Current.ShouldBeNull();
    }

    [Test]
    public void Word_OfPropertyForCultureTheAppDoesNotShow_ShouldEnterNoScope()
    {
        // Arrange: a German user of an app in English and Dutch. Every text falls back to English for them, and so
        // does a text the property reads, so nothing is switched.
        using var _ = new CulturesScope(uiCulture: "de-DE", culture: "de-DE");
        var german = CultureInfo.CurrentUICulture;
        var registry = LocalizationRegistry.Create([new SetShowingLocalizationEn(), new SetThreadCultureLocalizationEn()], "en", "nl");

        // Act & Assert
        Word<ISetShowingLocalization, SetShowingLocalizationEn>(registry, german, "Hint", static set => set.Hint).ShouldBe("Open the Cart to pay");
        Word<ISetThreadCultureLocalization, SetThreadCultureLocalizationEn>(registry, german, "Cultures", static set => set.Cultures).ShouldBe("de-DE/de-DE/none");
    }

    [Test]
    public void Word_OfPropertyWithWordingForTheCulture_ShouldEnterNoScope()
    {
        // Arrange: a British user, whom the English set serves.
        using var _ = new CulturesScope(uiCulture: "en-GB", culture: "en-GB");
        var registry = LocalizationRegistry.Create([new SetThreadCultureLocalizationEn()], "en", "nl");

        // Act & Assert: the thread's cultures are the reader's already.
        Word<ISetThreadCultureLocalization, SetThreadCultureLocalizationEn>(registry, CultureInfo.CurrentUICulture, "Cultures", static set => set.Cultures).ShouldBe("en-GB/en-GB/none");
    }

    [Test]
    public void ResolvedSets_ShouldKeepSetTheRecordTells()
    {
        // Arrange: sets that derive from no other set, so each words every text itself.
        var dutch = new SetLocalizationNl();
        var registry = LocalizationRegistry.Create([new SetLocalizationEn(), dutch], "en", "nl");

        // Act
        var resolved = ResolveGreetingAndFarewell(registry, "nl-NL");

        // Assert: for a property and for a text that takes values, because the thread of a Dutch reader needs no scope.
        resolved.Sets.ShouldBe([dutch, dutch]);
        resolved.SetsForValues.ShouldBe([dutch, dutch]);
        resolved.Lookup.ShouldBeSameAs(registry.Resolve(Culture("nl-NL")));
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void ResolvedSets_ShouldKeepSetPerTextWhereRecordTellsWhichSetWordsIt()
    {
        // Arrange: a rewording the build recorded as wording the greeting alone.
        LocalizationRegistry.RecordSet<RecordedRewordedLocalizationEn>(null, ["Greeting"]);
        var english = new RecordedLocalizationEn();
        var rewording = new RecordedRewordedLocalizationEn();
        var registry = LocalizationRegistry.Create([english, rewording], "en");

        // Act
        var resolved = ResolveGreetingAndFarewell(registry, "en-US");

        // Assert: each text keeps the set that words it, without a wording compared.
        resolved.Sets.ShouldBe([rewording, english]);
        resolved.SetsForValues.ShouldBe([rewording, english]);
    }

    [Test]
    public void ResolvedSets_ShouldNotKeepSetOnlyWordingCouldTell()
    {
        // Arrange: a rewording the build has no record of, so the registry compares its wording with that of the set
        // it derives from, on every call.
        var registry = LocalizationRegistry.Create([new SetLocalizationEn(), new SetRewordedLocalizationEn()], "en");

        // Act
        var resolved = ResolveGreetingAndFarewell(registry, "en-US");

        // Assert: a wording may differ on the next call, so no set is kept, though the text is worded.
        Greeting(registry, "en-US").ShouldBe("Hi");
        Farewell(registry, "en-US").ShouldBe("Goodbye");
        resolved.Sets.ShouldBe([null, null]);
        resolved.SetsForValues.ShouldBe([null, null]);
    }

    [Test]
    public void ResolvedSets_ShouldNotKeepSetWhereAScopeIsNeeded()
    {
        // Arrange: a Dutch user of an app that has Dutch, and a contract without a Dutch set. The text falls back
        // alone, in a scope of English.
        var english = new SetLocalizationEn();
        var registry = LocalizationRegistry.Create([english], "en", "nl");

        // Act
        var resolved = ResolveGreetingAndFarewell(registry, "nl-NL");

        // Assert: also not as a fallback, which only a text with parameters keeps.
        Greeting(registry, "nl-NL").ShouldBe("Hello");
        resolved.Sets.ShouldBe([null, null]);
        resolved.SetsForValues.ShouldBe([null, null]);
        resolved.FallbackSets.ShouldBe([null, null]);
    }

    [Test]
    public void ResolvedSets_ForCultureTheAppDoesNotShow_ShouldKeepSetForPropertyAlone()
    {
        // Arrange: a German user of an app in English and Dutch. A property enters no scope there, and a method of
        // the set runs in a scope of English.
        var english = new SetLocalizationEn();
        var registry = LocalizationRegistry.Create([english, new SetLocalizationNl()], "en", "nl");

        // Act
        var resolved = ResolveGreetingAndFarewell(registry, "de-DE");

        // Assert
        resolved.Sets.ShouldBe([english, english]);
        resolved.SetsForValues.ShouldBe([null, null]);
        resolved.FallbackSets.ShouldBe([null, null]);
    }

    [Test]
    public void ResolvedSets_OfTextThatTakesValues_ForCultureTheAppDoesNotShow_ShouldKeepSetAsFallback()
    {
        // Arrange: a German user of an app in English and Dutch, whose text with a parameter falls back to English.
        var english = new SetCountedLocalizationEn();
        var registry = LocalizationRegistry.Create([english, new SetCountedLocalizationNl()], "en", "nl");
        var german = Culture("de-DE");
        var resolved = new ResolvedSets<ISetCountedLocalization>(registry.Resolve(german), count: 2);

        // Act
        var wording = Word<ISetCountedLocalization, SetCountedLocalizationEn>(registry, german, "Items", static set => set.Items(3),
            takesValues: true, resolved: resolved, text: 0);

        // Assert: kept with the culture of English, and not for the generated class to call without its scope.
        wording.ShouldBe("3 items");
        resolved.SetsForValues.ShouldBe([null, null]);
        resolved.FindFallback(0).ShouldNotBeNull().Set.ShouldBeSameAs(english);
        resolved.FindFallback(0)!.Culture.ShouldNotBeNull().Name.ShouldBe("en");
        resolved.FindFallback(1).ShouldBeNull();
    }

    [Test]
    public void ResolvedSets_WhereSetOfTheCultureGivesNull_ShouldNotKeepTheFallbackFoundAfterIt()
    {
        // Arrange: a Dutch set whose method gives null, as one that hands on a value read at run time can.
        var registry = LocalizationRegistry.Create([new SetCountedLocalizationEn(), new SetCountedLocalizationNl()], "en", "nl");
        var dutch = Culture("nl-NL");
        var resolved = new ResolvedSets<ISetCountedLocalization>(registry.Resolve(dutch), count: 2);

        // Act
        var wording = Word<ISetCountedLocalization, SetCountedLocalizationEn>(registry, dutch, "Items",
            static set => set is SetCountedLocalizationNl ? null : set.Items(3), takesValues: true, resolved: resolved, text: 0);

        // Assert: the text falls back to English for this read alone. Kept, English would word it on every later
        // read, also once the Dutch set gives a wording.
        wording.ShouldBe("3 items");
        resolved.FallbackSets.ShouldBe([null, null]);
    }

    [Test]
    public void ResolvedSets_ShouldKeepSetTheTextWasDeclaredWithOnlyWhereItsLanguageServesTheCulture()
    {
        // Arrange: a registry without a set of the contract, so the set the text was declared with words it.
        var registry = LocalizationRegistry.Create([]);
        var defaultSet = GeneratedCode.GetDefaultSet<SetLocalizationEn>();

        // Act
        var british = ResolveGreetingAndFarewell(registry, "en-GB");
        var french = ResolveGreetingAndFarewell(registry, "fr-FR");

        // Assert: a French reader's text is worded in a scope of English, so it isn't kept.
        british.Sets.ShouldBe([defaultSet, defaultSet]);
        british.SetsForValues.ShouldBe([defaultSet, defaultSet]);
        french.Sets.ShouldBe([null, null]);
        french.SetsForValues.ShouldBe([null, null]);
        french.FallbackSets.ShouldBe([null, null]);
    }

    [Test]
    public void ResolvedSets_Keep_ShouldLeaveOutPositionOutsideTheTexts()
    {
        // Arrange
        var registry = LocalizationRegistry.Create([]);
        var resolved = new ResolvedSets<ISetLocalization>(registry.Resolve(Culture("en-US")), count: 1);
        var set = new SetLocalizationEn();

        // Act: positions no generated class passes, and one kept for a property alone.
        resolved.Keep(text: -1, set, forProperty: true, forValues: true);
        resolved.Keep(text: 1, set, forProperty: true, forValues: true);
        resolved.Keep(text: 0, set, forProperty: true, forValues: false);
        resolved.KeepFallback(text: -1, set, Culture("en"));
        resolved.KeepFallback(text: 1, set, Culture("en"));

        // Assert
        resolved.Sets.ShouldBe([set]);
        resolved.SetsForValues.ShouldBe([null]);
        resolved.FallbackSets.ShouldBe([null]);
        resolved.FindFallback(-1).ShouldBeNull();
        resolved.FindFallback(1).ShouldBeNull();
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void FindSet_ShouldGiveKeptSetAfterTextWasWordedOnceAndNoneBefore()
    {
        // Arrange: a contract no other test uses, with its Dutch set in the sets of the whole app.
        using var _ = new CulturesScope("nl-NL");
        var dutch = new SetKeptLocalizationNl();
        LocalizationRegistry.Register(dutch);

        // Act & Assert: none before the text is worded, for a property and for a text that takes values.
        GeneratedCode.FindSet<ISetKeptLocalization>(0, 2).ShouldBeNull();
        GeneratedCode.FindSetForValues<ISetKeptLocalization>(1, 2).ShouldBeNull();

        KeptTexts.WordCheckout().ShouldBe("Afrekenen");
        GeneratedCode.FindSet<ISetKeptLocalization>(0, 2).ShouldBeSameAs(dutch);
        GeneratedCode.FindSet<ISetKeptLocalization>(1, 2).ShouldBeNull();

        KeptTexts.WordTotal(1.5m).ShouldBe("Totaal: 1,50");
        GeneratedCode.FindSetForValues<ISetKeptLocalization>(1, 2).ShouldBeSameAs(dutch);

        // Kept per culture: an English reader has none yet, and gets the set the text was declared with.
        using (new CulturesScope("en-US"))
        {
            GeneratedCode.FindSet<ISetKeptLocalization>(0, 2).ShouldBeNull();
            KeptTexts.Checkout.ShouldBe("Checkout");
            GeneratedCode.FindSet<ISetKeptLocalization>(0, 2).ShouldBeSameAs(GeneratedCode.GetDefaultSet<SetKeptLocalizationEn>());
        }

        GeneratedCode.FindSet<ISetKeptLocalization>(0, 2).ShouldBeSameAs(dutch);
        KeptTexts.Checkout.ShouldBe("Afrekenen");
        KeptTexts.Total(1.5m).ShouldBe("Totaal: 1,50");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void FindSetForValues_WithCurrentUICultureProviderGivingCultureThreadDoesNotHave_ShouldGiveNone()
    {
        // Arrange: an American thread, and a set kept for a Dutch reader and for an American one.
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-US");
        var dutch = new SetKeptLocalizationNl();
        LocalizationRegistry.Register(dutch);

        using (new ProvidedCultureScope("nl-NL"))
        {
            // Act
            var total = KeptTexts.Total(1234.5m);
            var totalAgain = KeptTexts.Total(1234.5m);
            KeptTexts.Checkout.ShouldBe("Afrekenen");

            // Assert: the method of the set must run in the provided culture, which takes a scope on this thread. So
            // no set is given, however often the text was worded. A property formats nothing, so it reads its set.
            total.ShouldBe("Totaal: 1.234,50");
            totalAgain.ShouldBe("Totaal: 1.234,50");
            GeneratedCode.FindSetForValues<ISetKeptLocalization>(1, 2).ShouldBeNull();
            GeneratedCode.FindSet<ISetKeptLocalization>(0, 2).ShouldBeSameAs(dutch);
        }

        // A provider that gives the cultures the thread has needs no scope, so the kept set is given.
        using (new ProvidedCultureScope(CultureInfo.CurrentCulture))
        {
            KeptTexts.Total(1234.5m).ShouldBe("Total: 1,234.50");
            GeneratedCode.FindSetForValues<ISetKeptLocalization>(1, 2).ShouldBeSameAs(GeneratedCode.GetDefaultSet<SetKeptLocalizationEn>());
        }

        // A culture of the same name that formats a number its own way is another culture to format for.
        var ownAmerican = (CultureInfo)Culture("en-US").Clone();
        ownAmerican.NumberFormat.NumberGroupSeparator = " ";

        using (new ProvidedCultureScope(ownAmerican))
        {
            GeneratedCode.FindSetForValues<ISetKeptLocalization>(1, 2).ShouldBeNull();
            KeptTexts.Total(1234.5m).ShouldBe("Total: 1 234.50");
        }

        // A thread whose UI culture is the provided one, and whose culture for values is another.
        using (new CulturesScope(uiCulture: "en-US", culture: "nl-NL"))
        using (new ProvidedCultureScope("en-US"))
        {
            GeneratedCode.FindSetForValues<ISetKeptLocalization>(1, 2).ShouldBeNull();
            KeptTexts.Total(1234.5m).ShouldBe("Total: 1,234.50");
        }

        CultureInfo.CurrentUICulture.Name.ShouldBe("en-US");
        CultureInfo.CurrentCulture.Name.ShouldBe("en-US");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    [Arguments("nl-NL", "Afrekenen")]
    [Arguments("nl-BE", "Afrekenen")]
    [Arguments("en-GB", "Checkout")]
    [Arguments("en-US", "Checkout")]
    public void ReadingTextOfProperty_AfterItWasWordedOnce_ShouldAllocateNothing(string culture, string expected)
    {
        // Arrange: apart from the tests that register sets, which make the next read build the registry anew. A
        // reader whose language has a registered set, and one whom the set the text was declared with serves.
        using var _ = new CulturesScope(culture);
        LocalizationRegistry.Register(new SetKeptLocalizationNl());

        for (var i = 0; i < 20_000; i++)
            KeptTexts.Checkout.ShouldBe(expected);

        // Act: 1,000 reads, the least of a few rounds.
        var least = long.MaxValue;
        string? last = null;

        for (var round = 0; round < 3; round++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();

            for (var i = 0; i < 1_000; i++)
                last = KeptTexts.Checkout;

            least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        // Assert
        last.ShouldBe(expected);
        least.ShouldBe(0);
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void GeneratedText_ShouldFindWordingInSetsOfWholeAppAndFollowANewRegistration()
    {
        // Arrange: a contract no other test uses, so the sets of the whole app hold only this test's sets for it.
        using var _ = new CulturesScope("nl-NL");

        // Act: unregistered, then registered, and read twice each time, so that the second read finds a kept set.
        var before = RegisteredGreeting;
        var beforeAgain = RegisteredGreeting;
        var keptBefore = GeneratedCode.FindSet<ISetRegisteredLocalization>(0, 1);

        var dutch = new SetRegisteredLocalizationNl();
        LocalizationRegistry.Register(dutch);

        var keptAfterRegistration = GeneratedCode.FindSet<ISetRegisteredLocalization>(0, 1);
        var after = RegisteredGreeting;
        var afterAgain = RegisteredGreeting;

        // Assert: a registration builds a new registry, which starts without kept sets. So the set kept before isn't
        // served again.
        before.ShouldBe("Hello");
        beforeAgain.ShouldBe("Hello");
        keptBefore.ShouldBeNull("the text fell back alone, in a scope of English");
        keptAfterRegistration.ShouldBeNull();
        after.ShouldBe("Hallo");
        afterAgain.ShouldBe("Hallo");
        GeneratedCode.FindSet<ISetRegisteredLocalization>(0, 1).ShouldBeSameAs(dutch);
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void GeneratedText_AfterNewRegistration_ShouldNotBeServedSetKeptBefore()
    {
        // Arrange: an English reader, whose text keeps the set it was declared with.
        using var _ = new CulturesScope("en-US");
        StaleGreeting.ShouldBe("Hello");
        var keptBefore = GeneratedCode.FindSet<ISetStaleLocalization>(0, 1).ShouldNotBeNull();

        // Act: a set registers that rewords the text.
        var rewording = new SetStaleRewordedLocalizationEn();
        LocalizationRegistry.Register(rewording);

        // Assert
        GeneratedCode.FindSet<ISetStaleLocalization>(0, 1).ShouldBeNull();
        StaleGreeting.ShouldBe("Hi");
        StaleGreeting.ShouldBe("Hi");
        GeneratedCode.FindSet<ISetStaleLocalization>(0, 1).ShouldBeSameAs(rewording);
        keptBefore.ShouldBeSameAs(GeneratedCode.GetDefaultSet<SetStaleLocalizationEn>());
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void GeneratedText_WithCurrentUICultureProvider_ShouldFollowItsCultureInPlaceOfCurrentUICulture()
    {
        // Arrange: the thread is American, and the app keeps a Dutch user's culture somewhere else.
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-US");
        LocalizationRegistry.Register(new SetKeptLocalizationNl());

        // Act & Assert: a text of a set and an inline one.
        using (new ProvidedCultureScope("nl-NL"))
        {
            KeptTexts.Checkout.ShouldBe("Afrekenen");
            KeptTexts.Checkout.ShouldBe("Afrekenen");
            Cart.ShouldBe("Winkelwagen");
            $"{KeptTexts.Checkout}".ShouldBe("Afrekenen");
        }

        // A provider that gives no culture leaves the text on the current UI culture, and so does no provider.
        using (new ProvidedCultureScope(name: null))
        {
            KeptTexts.Checkout.ShouldBe("Checkout");
            Cart.ShouldBe("Cart");
        }

        KeptTexts.Checkout.ShouldBe("Checkout");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void GeneratedText_WithCurrentUICultureProvider_ShouldWordTextThatTakesValuesAsForUserOfItsCulture()
    {
        // Arrange: a contract no other test uses, so the sets of the whole app hold only this test's set for it.
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-US");
        LocalizationRegistry.Register(new SetProvidedLocalizationNl());
        const decimal amount = 1234.5m;

        // Act & Assert: the Dutch wording, with the number as a Dutch user writes it, also in the text the method of
        // the set reads itself, and the cultures of this thread as they were.
        using (new ProvidedCultureScope("nl-NL"))
        {
            ProvidedTexts.Total(amount).ShouldBe("Totaal: 1.234,50");
            ProvidedTexts.Summary(amount).ShouldBe("Overzicht: Totaal: 1.234,50");
            CultureInfo.CurrentCulture.Name.ShouldBe("en-US");
            CultureInfo.CurrentUICulture.Name.ShouldBe("en-US");
            WordingScope.Current.ShouldBeNull();
        }

        ProvidedTexts.Total(amount).ShouldBe("Total: 1,234.50");
        ProvidedTexts.Summary(amount).ShouldBe("Summary: Total: 1,234.50");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void GeneratedText_WithCurrentUICultureProvider_ShouldShowTextThatPropertyReadsForItsCulture()
    {
        // Arrange: an American thread, a Dutch user whose culture the provider gives, and texts whose property reads
        // another text: an inline one, and one that takes a value.
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-US");
        LocalizationRegistry.Register(new SetShowingLocalizationEn(), new SetShowingLocalizationNl(), new SetProvidedLocalizationNl());

        // Act & Assert: the text the property reads follows the provider by itself, with the number as a Dutch user
        // writes it.
        using (new ProvidedCultureScope("nl-NL"))
        {
            ShowingTexts.Hint.ShouldBe("Open de Winkelwagen om te betalen");
            ShowingTexts.Summary.ShouldBe("Overzicht: Totaal: 1.234,50");
            ShowingTexts.Hint.ShouldBe("Open de Winkelwagen om te betalen");
        }

        // And the user's culture again once the provider gives none.
        ShowingTexts.Hint.ShouldBe("Open the Cart to pay");
        ShowingTexts.Summary.ShouldBe("Summary: Total: 1,234.50");
        CultureInfo.CurrentUICulture.Name.ShouldBe("en-US");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void GeneratedText_WithCurrentUICultureProviderGivingCultureTheAppDoesNotShow_ShouldShowTextThatSetReadsInTheFallbackLanguage()
    {
        // Arrange: an American thread, and a German user whose culture the provider gives. This app shows English and
        // Dutch, so every text falls back to English for them.
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-US");
        LocalizationRegistry.Register(new SetShowingLocalizationEn(), new SetShowingLocalizationNl(), new SetProvidedLocalizationNl());

        // Act & Assert: English throughout, with the number as the German user writes it.
        using (new ProvidedCultureScope("de-DE"))
        {
            ShowingTexts.Hint.ShouldBe("Open the Cart to pay");
            ShowingTexts.Summary.ShouldBe("Summary: Total: 1.234,50");
            ProvidedTexts.Summary(1234.5m).ShouldBe("Summary: Total: 1.234,50");
        }

        CultureInfo.CurrentUICulture.Name.ShouldBe("en-US");
        CultureInfo.CurrentCulture.Name.ShouldBe("en-US");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void GeneratedText_WithCurrentUICultureProviderGivingUICultureOfThread_ShouldFormatValuesForIt()
    {
        // Arrange: the provider gives the UI culture the thread has, and the thread formats numbers as a Dutch user
        // does.
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "nl-NL");
        const decimal amount = 1234.5m;

        // Act & Assert: the number as an American user writes it, as for a British user, whose culture the thread
        // doesn't have, and the cultures of this thread as they were.
        using (new ProvidedCultureScope("en-US"))
        {
            ProvidedTexts.Total(amount).ShouldBe("Total: 1,234.50");
            ProvidedTexts.Total(amount).ShouldBe("Total: 1,234.50");
        }

        using (new ProvidedCultureScope("en-GB"))
            ProvidedTexts.Total(amount).ShouldBe("Total: 1,234.50");

        CultureInfo.CurrentCulture.Name.ShouldBe("nl-NL");
        ProvidedTexts.Total(amount).ShouldBe("Total: 1.234,50");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void GetWording_WithCurrentUICultureProvider_ShouldAskItOncePerText()
    {
        // Arrange: the provider gives the UI culture the thread has.
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-US");
        using var provided = new ProvidedCultureScope("en-US");

        // Act & Assert: once for a text, and not again for a text the method of its set reads, which follows the
        // scope the method runs in.
        ProvidedTexts.WordTotal(1234.5m).ShouldBe("Total: 1,234.50");
        provided.CallCount.ShouldBe(1);
        ProvidedTexts.WordSummary(1234.5m).ShouldBe("Summary: Total: 1,234.50");
        provided.CallCount.ShouldBe(2);
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void GetWording_WithCurrentUICultureProviderGivingNone_ShouldAskItOncePerText()
    {
        // Arrange: no set of the whole app words this text in English, so it falls back step by step to its default
        // wording, where each step asked the provider again.
        using var _ = new CulturesScope(uiCulture: "en-GB", culture: "en-GB");
        using var provided = new ProvidedCultureScope(name: null);

        // Act
        var text = ProvidedTexts.WordTotal(1234.5m);

        // Assert: the default wording, with the value as the thread formats it, and the provider asked once.
        text.ShouldBe("Total: 1,234.50");
        provided.CallCount.ShouldBe(1);
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void GeneratedText_WithCurrentUICultureProvider_ShouldAskItOncePerTextOnceItsSetIsKept()
    {
        // Arrange: the provider gives the cultures the thread has, so that a text reads its kept set. Read once
        // first, which finds the sets.
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-US");
        using var provided = new ProvidedCultureScope("en-US");
        KeptTexts.Checkout.ShouldBe("Checkout");
        KeptTexts.Total(1234.5m).ShouldBe("Total: 1,234.50");
        var asked = provided.CallCount;

        // Act & Assert: a property, and a text that takes a value.
        KeptTexts.Checkout.ShouldBe("Checkout");
        provided.CallCount.ShouldBe(asked + 1);
        KeptTexts.Total(1234.5m).ShouldBe("Total: 1,234.50");
        provided.CallCount.ShouldBe(asked + 2);
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void GetWording_WithCurrentUICultureProvider_ShouldCopyExecutionContextOnlyToChangeCultures()
    {
        // Arrange: an American thread, and a British user whose culture the provider gives. A culture is an AsyncLocal,
        // so each time one is set, the runtime copies the execution context. Setting both cultures back copied it twice
        // more, where restoring the context copies nothing.
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-US");
        var british = Culture("en-GB");

        // Act: per 10,000 texts for a user whose cultures the thread has, which changes none. Then for the British
        // user, and per 10,000 times both cultures change and the context is restored, in the same context, so with
        // as many AsyncLocals to copy.
        long unchanged;
        using (new ProvidedCultureScope("en-US"))
            unchanged = GeneratedSetsTests.Allocated(static () => ProvidedTexts.WordTotal(1234.5m)) / 10_000;

        long shown;
        long changed;
        string wording;
        using (new ProvidedCultureScope("en-GB"))
        {
            wording = ProvidedTexts.WordTotal(1234.5m);
            shown = GeneratedSetsTests.Allocated(static () => ProvidedTexts.WordTotal(1234.5m)) / 10_000;
            changed = GeneratedSetsTests.Allocated(() =>
            {
                var context = ExecutionContext.Capture()!;
                CultureInfo.CurrentUICulture = british;
                CultureInfo.CurrentCulture = british;
                ExecutionContext.Restore(context);
                return context;
            }) / 10_000;
        }

        // Assert: the change of both cultures besides, and the cultures of this thread as they were.
        wording.ShouldBe("Total: 1,234.50");
        shown.ShouldBeLessThanOrEqualTo(unchanged + changed);
        CultureInfo.CurrentUICulture.Name.ShouldBe("en-US");
        CultureInfo.CurrentCulture.Name.ShouldBe("en-US");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void GeneratedText_WithCurrentUICultureProviderWhereFlowIsSuppressed_ShouldPutCulturesBack()
    {
        // Arrange: an American thread that doesn't flow its execution context, so there's no context to restore, and a
        // German user whose culture the provider gives. The app doesn't support German, so the text falls back to
        // English, in a scope of its own.
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-US");
        using var provided = new ProvidedCultureScope("de-DE");
        using var flow = ExecutionContext.SuppressFlow();

        // Act
        var text = ProvidedTexts.Total(1234.5m);

        // Assert: the English wording with the number as the German user writes it, and the cultures of this thread as
        // they were.
        text.ShouldBe("Total: 1.234,50");
        CultureInfo.CurrentUICulture.Name.ShouldBe("en-US");
        CultureInfo.CurrentCulture.Name.ShouldBe("en-US");
        WordingScope.Current.ShouldBeNull();
    }

    [Test]
    public void WordingScope_ShouldSetCulturesOfThreadAndPutThemBack()
    {
        // Arrange
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-US");
        var dutch = Culture("nl-NL");
        var german = Culture("de-DE");

        // Act & Assert: the UI culture, and the culture for values where one is given.
        using (new WordingScope(dutch, valuesCulture: null))
        {
            WordingScope.Current.ShouldBeSameAs(dutch);
            CultureInfo.CurrentUICulture.ShouldBeSameAs(dutch);
            CultureInfo.CurrentCulture.Name.ShouldBe("en-US");

            // A scope inside another, as for a text the member of a set reads, which falls back itself.
            using (new WordingScope(german, valuesCulture: german))
            {
                WordingScope.Current.ShouldBeSameAs(german);
                CultureInfo.CurrentUICulture.ShouldBeSameAs(german);
                CultureInfo.CurrentCulture.ShouldBeSameAs(german);
            }

            WordingScope.Current.ShouldBeSameAs(dutch);
            CultureInfo.CurrentUICulture.ShouldBeSameAs(dutch);
            CultureInfo.CurrentCulture.Name.ShouldBe("en-US");
        }

        WordingScope.Current.ShouldBeNull();
        CultureInfo.CurrentUICulture.Name.ShouldBe("en-US");
        CultureInfo.CurrentCulture.Name.ShouldBe("en-US");

        // A scope that was never entered puts nothing back.
        default(WordingScope).Dispose();
        CultureInfo.CurrentUICulture.Name.ShouldBe("en-US");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void FindReaderCulture_ShouldGiveCultureOfScopeElseOfProviderElseOfThread()
    {
        // Arrange
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-US");
        var american = CultureInfo.CurrentUICulture;
        var dutch = Culture("nl-NL");
        var german = Culture("de-DE");

        // Act & Assert: without a provider, the thread's UI culture, which a scope sets.
        WordingScope.FindReaderCulture(out var provided).ShouldBeSameAs(american);
        provided.ShouldBeNull();

        using (new ProvidedCultureScope(dutch))
        {
            // The provider's culture, which is the culture to format values for too.
            WordingScope.FindReaderCulture(out provided).ShouldBeSameAs(dutch);
            provided.ShouldBeSameAs(dutch);

            // Inside a scope, its culture, and the scope set the thread's format already.
            using (new WordingScope(german, valuesCulture: dutch))
            {
                WordingScope.FindReaderCulture(out provided).ShouldBeSameAs(german);
                provided.ShouldBeNull();
            }
        }

        using (new ProvidedCultureScope(name: null))
        {
            WordingScope.FindReaderCulture(out provided).ShouldBeSameAs(american);
            provided.ShouldBeNull();
        }
    }
}
