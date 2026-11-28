using CodeChops.Idioma.Internals;
using CodeChops.Idioma.Tests.TestHelpers;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for the wording of a text of a set: the order a text falls back in (<c>LocalizationRegistry.Word</c>), the
/// cultures a set's member runs in, the sets a user's culture keeps per contract (<see cref="ResolvedSets{TContract}"/>),
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
    // The languages that names of these nested sets can't give, described as a registrar describes a set.
    static LocalizationRegistryWordingTests()
    {
        LocalizationRegistry.DescribeSet<SetWithMemberBritishLocalization>("en-GB");
        LocalizationRegistry.DescribeSet<SetStandaloneEnglishLocalization>("en");
        LocalizationRegistry.DescribeSet<SetBritishLocalization>("en-GB");
        LocalizationRegistry.DescribeSet<SetStandaloneBritishLocalization>("en-GB");
        LocalizationRegistry.DescribeSet<SetChineseLocalization>("zh");
        LocalizationRegistry.DescribeSet<SetTraditionalChineseLocalization>("zh-Hant");
        LocalizationRegistry.DescribeSet<SetTaiwaneseLocalization>("zh-Hant-TW");
    }

    private interface ISetLocalization : ILocalizationSet
    {
        string Greeting { get; }
        string Farewell { get; }
    }

    internal class SetLocalizationEn : ISetLocalization
    {
        public virtual string Greeting => "Hello";
        public virtual string Farewell => "Goodbye";
    }

    internal sealed class SetLocalizationNl : ISetLocalization
    {
        public string Greeting => "Hallo";
        public string Farewell => "Tot ziens";
    }

    /// <summary>
    /// A set a text is declared with whose type gives no language, which the build doesn't describe.
    /// </summary>
    private sealed class SetWithoutLanguage : ISetLocalization
    {
        public string Greeting => "Hello";
        public string Farewell => "Goodbye";
    }

    /// <summary>
    /// A Dutch set that gives no greeting, as a member that hands on a value that's null at run time.
    /// </summary>
    internal sealed class SetWithoutGreetingLocalizationNl : ISetLocalization
    {
        public string Greeting => null!;
        public string Farewell => "Tot ziens";
    }

    /// <summary>
    /// An English set whose texts read a member that isn't a text, such as a shop's name or a currency, which a set
    /// deriving from it may override.
    /// </summary>
    internal class SetWithMemberLocalizationEn : ISetLocalization
    {
        protected virtual string Name => "Anna";

        public virtual string Greeting => $"Hello, {this.Name}";
        public virtual string Farewell => $"Goodbye, {this.Name}";
    }

    /// <summary>
    /// A British set that overrides only the member the texts read.
    /// </summary>
    private class SetWithMemberBritishLocalization : SetWithMemberLocalizationEn, ILocalizationSet
    {
        protected override string Name => "Annie";
    }

    /// <summary>
    /// An app's rewording of the British set that overrides only the member the texts read, as the app's rewording of
    /// English does.
    /// </summary>
    private sealed class SetWithMemberRewordedBritishLocalization : SetWithMemberBritishLocalization
    {
        protected override string Name => "Ann";
    }

    /// <summary>
    /// An app's rewording that overrides only the member the texts read.
    /// </summary>
    internal sealed class SetWithMemberRewordedLocalizationEn : SetWithMemberLocalizationEn
    {
        protected override string Name => "Ann";
    }

    /// <summary>
    /// A shared library's rewording of the greeting alone.
    /// </summary>
    internal sealed class SetWithMemberGreetingRewordedLocalizationEn : SetWithMemberLocalizationEn
    {
        public override string Greeting => "Hi";
    }

    /// <summary>
    /// An English set rewording one text of the default language.
    /// </summary>
    internal class SetRewordedLocalizationEn : SetLocalizationEn
    {
        public override string Greeting => "Hi";
    }

    /// <summary>
    /// A rewording that derives from the one above and rewords another text.
    /// </summary>
    internal sealed class SetFurtherRewordedLocalizationEn : SetRewordedLocalizationEn
    {
        public override string Farewell => "Bye";
    }

    /// <summary>
    /// A second English set rewording another text, as a shared library's rewording next to an app's.
    /// </summary>
    internal sealed class SetSecondRewordedLocalizationEn : SetLocalizationEn
    {
        public override string Farewell => "Bye";
    }

    /// <summary>
    /// An English set with every text of its own, deriving from no English set, though one text matches the default
    /// wording.
    /// </summary>
    private sealed class SetStandaloneEnglishLocalization : ISetLocalization
    {
        public string Greeting => "Hello";
        public string Farewell => "So long";
    }

    /// <summary>
    /// A library's British set, deriving from its English set and wording one text its own way.
    /// </summary>
    private sealed class SetBritishLocalization : SetLocalizationEn, ILocalizationSet
    {
        public override string Farewell => "Cheerio";
    }

    /// <summary>
    /// A British set with every text of its own, deriving from no English set, though one text matches the English
    /// wording.
    /// </summary>
    private sealed class SetStandaloneBritishLocalization : ISetLocalization
    {
        public string Greeting => "Hello";
        public string Farewell => "Cheerio";
    }

    /// <summary>
    /// Three levels of one language, each deriving from the one above (zh, zh-Hant, zh-Hant-TW); the middle one words
    /// one text its own way.
    /// </summary>
    private class SetChineseLocalization : ISetLocalization
    {
        public virtual string Greeting => "Ni hao";
        public virtual string Farewell => "Zaijian";
    }

    private class SetTraditionalChineseLocalization : SetChineseLocalization
    {
        public override string Farewell => "Zaijian (Hant)";
    }

    private sealed class SetTaiwaneseLocalization : SetTraditionalChineseLocalization
    {
    }

    /// <summary>
    /// An app's rewording of the top level.
    /// </summary>
    private sealed class SetRewordedChineseLocalization : SetChineseLocalization
    {
        public override string Greeting => "Nin hao";
    }

    /// <summary>
    /// An English set that counts how often its farewell is read, per instance.
    /// </summary>
    internal class DescribedLocalizationEn : ISetLocalization
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
    internal sealed class CountedLocalizationEn : ISetLocalization
    {
        private static int _created;

        public CountedLocalizationEn() => Interlocked.Increment(ref _created);

        public static int Created => Volatile.Read(ref _created);

        public string Greeting => "Hello";
        public string Farewell => "Goodbye";
    }

    /// <summary>
    /// An app's rewording of the greeting.
    /// </summary>
    internal sealed class DescribedRewordedLocalizationEn : DescribedLocalizationEn
    {
        public override string Greeting => "Hi";
    }

    /// <summary>
    /// A contract no other test uses, for the test that rewords its default language.
    /// </summary>
    private interface ISetDefaultRewordedLocalization : ILocalizationSet
    {
        string Checkout { get; }
    }

    internal class SetDefaultRewordedLocalizationEn : ISetDefaultRewordedLocalization
    {
        public virtual string Checkout => "Checkout";
    }

    /// <summary>
    /// An app's rewording of the default language, as the docs' BrandedShopLocalizationEn.
    /// </summary>
    internal sealed class SetBrandedDefaultRewordedLocalizationEn : SetDefaultRewordedLocalizationEn
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

    internal sealed class SetCountedLocalizationEn : ISetCountedLocalization
    {
        public string Items(int count) => count is 1 ? "1 item" : $"{count} items";
        public string Total(decimal amount) => $"Total: {amount:N2}";
    }

    internal sealed class SetCountedLocalizationNl : ISetCountedLocalization
    {
        public string Items(int count) => count is 1 ? "1 artikel" : $"{count} artikelen";
        public string Total(decimal amount) => $"Totaal: {amount:N2}";
    }

    /// <summary>
    /// A French set whose texts can't be worded.
    /// </summary>
#pragma warning disable LOC402 // It words no value on purpose, because it throws.
    internal sealed class SetCountedLocalizationFr : ISetCountedLocalization
    {
        public string Items(int count) => throw new InvalidOperationException("No wording.");
        public string Total(decimal amount) => throw new InvalidOperationException("No wording.");
    }
#pragma warning restore LOC402

    /// <summary>
    /// A contract whose texts show the cultures their member runs in: the UI culture, and the culture values are
    /// formatted for.
    /// </summary>
    private interface ISetThreadCultureLocalization : ILocalizationSet
    {
        string Cultures { get; }
        string CulturesOf(int number);
    }

    internal sealed class SetThreadCultureLocalizationEn : ISetThreadCultureLocalization
    {
        public string Cultures => $"{CultureInfo.CurrentUICulture.Name}/{CultureInfo.CurrentCulture.Name}";
        public string CulturesOf(int number) => $"{number}: {this.Cultures}";
    }

    /// <summary>
    /// A contract no other test uses, for the tests that register its Dutch set with the sets of the whole app.
    /// </summary>
    private interface ISetRegisteredLocalization : ILocalizationSet
    {
        string Greeting { get; }
    }

    internal sealed class SetRegisteredLocalizationEn : ISetRegisteredLocalization
    {
        public string Greeting => "Hello";
    }

    internal sealed class SetRegisteredLocalizationNl : ISetRegisteredLocalization
    {
        public string Greeting => "Hallo";
    }

    /// <summary>
    /// The text of <see cref="ISetRegisteredLocalization"/>, read as a property of a generated class reads it.
    /// </summary>
    private static string RegisteredGreeting => GeneratedCode.GetLocalizationSet<ISetRegisteredLocalization>()?.Greeting
        ?? GeneratedCode.ResolveWording<ISetRegisteredLocalization, SetRegisteredLocalizationEn>("Greeting", static set => set.Greeting);

    /// <summary>
    /// A contract no other test uses, for the test that registers a rewording of its default language with the sets of
    /// the whole app.
    /// </summary>
    private interface ISetStaleLocalization : ILocalizationSet
    {
        string Greeting { get; }
    }

    internal class SetStaleLocalizationEn : ISetStaleLocalization
    {
        public virtual string Greeting => "Hello";
    }

    /// <summary>
    /// A rewording of the default language, which registers alone, so it derives from no registered set.
    /// </summary>
    internal sealed class SetStaleRewordedLocalizationEn : SetStaleLocalizationEn
    {
        public override string Greeting => "Hi";
    }

    /// <summary>
    /// The text of <see cref="ISetStaleLocalization"/>, read as a property of a generated class reads it.
    /// </summary>
    private static string StaleGreeting => GeneratedCode.GetLocalizationSet<ISetStaleLocalization>()?.Greeting
        ?? GeneratedCode.ResolveWording<ISetStaleLocalization, SetStaleLocalizationEn>("Greeting", static set => set.Greeting);

    /// <summary>
    /// A contract no other test uses, for the tests of the sets a user's culture keeps in the sets of the whole app.
    /// Its English set is never registered, so an English user's text keeps the set it was declared with.
    /// </summary>
    private interface ISetKeptLocalization : ILocalizationSet
    {
        string Checkout { get; }
        string Total(decimal amount);
    }

    internal sealed class SetKeptLocalizationEn : ISetKeptLocalization
    {
        public string Checkout => "Checkout";
        public string Total(decimal amount) => $"Total: {amount:N2}";
    }

    internal sealed class SetKeptLocalizationNl : ISetKeptLocalization
    {
        public string Checkout => "Afrekenen";
        public string Total(decimal amount) => $"Totaal: {amount:N2}";
    }

    /// <summary>
    /// The texts of <see cref="ISetKeptLocalization"/>, read as the members of a generated class read them.
    /// </summary>
    private static class KeptTexts
    {
        public static string Checkout => GeneratedCode.GetLocalizationSet<ISetKeptLocalization>()?.Checkout
            ?? WordCheckout();

        public static string Total(decimal amount) => GeneratedCode.GetLocalizationSetForValues<ISetKeptLocalization>()?.Total(amount)
            ?? WordTotal(amount);

        // Without the kept set.
        public static string WordCheckout() => GeneratedCode.ResolveWording<ISetKeptLocalization, SetKeptLocalizationEn>("Checkout", static set => set.Checkout);

        public static string WordTotal(decimal amount) => GeneratedCode.ResolveWording<ISetKeptLocalization, SetKeptLocalizationEn, decimal>("Total", static (set, amount) => set.Total(amount), amount);
    }

    /// <summary>
    /// A contract no other test uses, whose texts take a value. One of its texts shows another, which its member reads
    /// itself.
    /// </summary>
    private interface ISetTotalLocalization : ILocalizationSet
    {
        string Total(decimal amount);
        string Summary(decimal amount);
    }

    internal sealed class SetTotalLocalizationEn : ISetTotalLocalization
    {
        public string Total(decimal amount) => $"Total: {amount:N2}";
        public string Summary(decimal amount) => $"Summary: {TotalTexts.Total(amount)}";
    }

    internal sealed class SetTotalLocalizationNl : ISetTotalLocalization
    {
        public string Total(decimal amount) => $"Totaal: {amount:N2}";
        public string Summary(decimal amount) => $"Overzicht: {TotalTexts.Total(amount)}";
    }

    /// <summary>
    /// The texts of <see cref="ISetTotalLocalization"/>, read as the methods of a generated class read them.
    /// </summary>
    private static class TotalTexts
    {
        public static string Total(decimal amount) => GeneratedCode.GetLocalizationSetForValues<ISetTotalLocalization>()?.Total(amount)
            ?? GeneratedCode.ResolveWording<ISetTotalLocalization, SetTotalLocalizationEn, decimal>("Total", static (set, amount) => set.Total(amount), amount);

        public static string Summary(decimal amount) => GeneratedCode.GetLocalizationSetForValues<ISetTotalLocalization>()?.Summary(amount)
            ?? GeneratedCode.ResolveWording<ISetTotalLocalization, SetTotalLocalizationEn, decimal>("Summary", static (set, amount) => set.Summary(amount), amount);
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

    internal sealed class SetShowingLocalizationEn : ISetShowingLocalization
    {
        public string Hint => $"Open the {Cart} to pay";
        public string Summary => $"Summary: {TotalTexts.Total(1234.5m)}";
    }

    internal sealed class SetShowingLocalizationNl : ISetShowingLocalization
    {
        public string Hint => $"Open de {Cart} om te betalen";
        public string Summary => $"Overzicht: {TotalTexts.Total(1234.5m)}";
    }

    /// <summary>
    /// The texts of <see cref="ISetShowingLocalization"/>, read as the properties of a generated class read them.
    /// </summary>
    private static class ShowingTexts
    {
        public static string Hint => GeneratedCode.GetLocalizationSet<ISetShowingLocalization>()?.Hint
            ?? GeneratedCode.ResolveWording<ISetShowingLocalization, SetShowingLocalizationEn>("Hint", static set => set.Hint);

        public static string Summary => GeneratedCode.GetLocalizationSet<ISetShowingLocalization>()?.Summary
            ?? GeneratedCode.ResolveWording<ISetShowingLocalization, SetShowingLocalizationEn>("Summary", static set => set.Summary);
    }

    /// <summary>
    /// A contract no other test uses, whose text shows two other texts in Dutch, which its method reads inside
    /// <see cref="LocalizationRegistry.UseCulture"/>, and then the same two after it. It takes a value, so its method
    /// runs in a scope of the language it's worded in, also for a culture the app doesn't show.
    /// </summary>
    private interface ISetUsingCultureLocalization : ILocalizationSet
    {
        string Checkouts(int count);
    }

    internal sealed class SetUsingCultureLocalizationEn : ISetUsingCultureLocalization
    {
        public string Checkouts(int count)
        {
            string dutch;

            using (LocalizationRegistry.UseCulture(Culture("nl-NL")))
                dutch = $"{KeptTexts.Checkout} {Cart}";

            return $"{count}: {dutch} / {KeptTexts.Checkout} {Cart}";
        }
    }

    /// <summary>
    /// The text of <see cref="ISetUsingCultureLocalization"/>, read as a method of a generated class reads it.
    /// </summary>
    private static string UsingCultureCheckouts(int count) => GeneratedCode.GetLocalizationSetForValues<ISetUsingCultureLocalization>()?.Checkouts(count)
        ?? GeneratedCode.ResolveWording<ISetUsingCultureLocalization, SetUsingCultureLocalizationEn, int>("Checkouts", static (set, count) => set.Checkouts(count), count);

    private interface ISetFlagLocalization : ILocalizationSet
    {
        string Flagged(int value);
    }

    internal sealed class SetFlagLocalizationEn : ISetFlagLocalization
    {
        public static AsyncLocal<string?> Flag { get; } = new();

        public string Flagged(int value)
        {
            Flag.Value = "inner";
            return $"Flagged {value}";
        }
    }

    private static CultureInfo Culture(string name) => CultureInfo.GetCultureInfo(name);

    /// <summary>
    /// The wording of a text for a user's culture in a registry, as <c>LocalizationRegistry.Word</c> gives it. A test
    /// whose set reads the cultures of the thread makes that culture the thread's UI culture first, as it is for every
    /// text an app shows.
    /// </summary>
    private static string Word<TContract, TDefaultSet>(LocalizationRegistry registry, CultureInfo culture, string name, Func<TContract, string?> select,
        bool takesValues = false, ResolvedSets<TContract>? resolved = null)
        where TContract : class
        where TDefaultSet : class, ILocalizationSet, TContract, new()
    {
        var wording = registry.Word(registry.Resolve(culture), name, select, typeof(TDefaultSet), static () => GeneratedCode.GetDefaultSet<TDefaultSet>(), takesValues, resolved);
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
    /// The total of a text that takes a value, for a user's culture in a registry.
    /// </summary>
    private static string Total(LocalizationRegistry registry, CultureInfo culture)
    {
        return Word<ISetCountedLocalization, SetCountedLocalizationEn>(registry, culture, "Total", static set => set.Total(1234.5m), takesValues: true);
    }

    /// <summary>
    /// The sets a culture keeps for the two texts of <see cref="ISetLocalization"/> in a registry, after each text was
    /// worded once, as a property and as a text that takes values.
    /// </summary>
    private static ResolvedSets<ISetLocalization> ResolveGreetingAndFarewell(LocalizationRegistry registry, string cultureName)
    {
        var culture = Culture(cultureName);
        var resolved = new ResolvedSets<ISetLocalization>(registry.Resolve(culture));

        Word<ISetLocalization, SetLocalizationEn>(registry, culture, "Greeting", static set => set.Greeting, resolved: resolved);
        Word<ISetLocalization, SetLocalizationEn>(registry, culture, "Farewell", static set => set.Farewell, resolved: resolved);

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
    [NotInParallel(nameof(LocalizationRegistry))]
    public void Word_ForCultureWithoutSet_ShouldCreateNoSecondDefaultSetWhereTheRegisteredOneWordsIt()
    {
        // Arrange: a registry without languages, as in a host that declares none, with the English set registered and
        // described, as the generated registrar describes it.
        LocalizationRegistry.DescribeSet<CountedLocalizationEn>("en");
        var registry = LocalizationRegistry.Create([new CountedLocalizationEn()]);
        var created = CountedLocalizationEn.Created;

        // Act
        var greeting = Word<ISetLocalization, CountedLocalizationEn>(registry, Culture("fr-FR"), "Greeting", static set => set.Greeting);

        // Assert: the registered set words it, and no instance is created only to tell its language.
        greeting.ShouldBe("Hello");
        CountedLocalizationEn.Created.ShouldBe(created);
    }

    [Test]
    public void Word_WhereSetGivesNull_ShouldThrowNamingTheSet()
    {
        // Arrange: a Dutch set that gives null for one text, as a value read at run time can be, next to an English
        // set that words it.
        ILocalizationSet[] sets = [new SetRewordedLocalizationEn(), new SetWithoutGreetingLocalizationNl()];
        var registry = LocalizationRegistry.Create(sets, "en", "nl");
        var resolved = new ResolvedSets<ISetLocalization>(registry.Resolve(Culture("nl-NL")));

        // Act
        var exception = Should.Throw<InvalidOperationException>(() =>
            Word<ISetLocalization, SetLocalizationEn>(registry, Culture("nl-NL"), "Greeting", static set => set.Greeting, resolved: resolved));

        // Assert: the text names the set that gave null, and doesn't fall back to English. No set is kept for it, and
        // the text the set does give still has its Dutch wording.
        exception.Message.ShouldContain(nameof(SetWithoutGreetingLocalizationNl));
        exception.Message.ShouldContain("'Greeting' of 'ISetLocalization'");
        resolved.Set.ShouldBeNull();
        Farewell(registry, "nl-NL").ShouldBe("Tot ziens");
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

        // Assert: the text names the set that gave null rather than show null.
        property.Message.ShouldContain("'Missing'");
        property.Message.ShouldContain(nameof(SetLocalizationEn));
        method.Message.ShouldContain(nameof(SetCountedLocalizationEn));
        method.Message.ShouldContain("'MissingItems' of 'ISetCountedLocalization'");
    }

    [Test]
    public void Word_WhereDeclaredSetHasNoLanguage_ShouldThrowNamingTheSet()
    {
        // Arrange: a registry without a set of the contract, so the text falls back to the set it was declared with.
        var registry = LocalizationRegistry.Create([]);

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => Word<ISetLocalization, SetWithoutLanguage>(registry, Culture("fr-FR"), "Greeting", static set => set.Greeting));

        // Assert: the set is named, rather than a null key deep in the lookup.
        exception.Message.ShouldContain(nameof(SetWithoutLanguage));
        exception.Message.ShouldContain("has no language");
    }

    [Test]
    public void Word_ForRegion_ShouldShowRegionalSetWholeAndNotTheRewordingOfItsLanguage()
    {
        // Arrange: a library's English and British sets, and an app's rewording of the English set, registered before
        // or after the British set. Then the same in an app whose default language is British English.
        ILocalizationSet[] sets = [new SetLocalizationEn(), new SetBritishLocalization(), new SetRewordedLocalizationEn()];
        var registry = LocalizationRegistry.Create(sets, "en", "nl");
        var rewordingFirst = LocalizationRegistry.Create([new SetLocalizationEn(), new SetRewordedLocalizationEn(), new SetBritishLocalization()], "en", "nl");
        var britishApp = LocalizationRegistry.Create(sets, "en-GB", "nl");

        // Act & Assert: a British user sees the British set, also for the text it inherits, because the rewording of
        // English doesn't reach it. So does an uncovered culture in the British app. Other English cultures see the
        // rewording.
        Greeting(registry, "en-GB").ShouldBe("Hello");
        Greeting(rewordingFirst, "en-GB").ShouldBe("Hello");
        Farewell(registry, "en-GB").ShouldBe("Cheerio");
        Greeting(britishApp, "fr-FR").ShouldBe("Hello");
        Farewell(britishApp, "fr-FR").ShouldBe("Cheerio");
        Greeting(registry, "en-US").ShouldBe("Hi");
        Farewell(registry, "en-US").ShouldBe("Goodbye");
    }

    [Test]
    public void Word_ShouldShowOnlyTheLastOfTwoRewordingsOfOneSet()
    {
        // Arrange: two rewordings of the English set, each changing another text, as a shared library's and an app's.
        // And a set implementing the contract in full, registered after a rewording.
        ILocalizationSet[] rewordings = [new SetLocalizationEn(), new SetRewordedLocalizationEn(), new SetSecondRewordedLocalizationEn()];
        var registry = LocalizationRegistry.Create(rewordings, "en");
        var withoutLanguages = LocalizationRegistry.Create(rewordings);
        var standaloneLast = LocalizationRegistry.Create([new SetLocalizationEn(), new SetRewordedLocalizationEn(), new SetStandaloneEnglishLocalization()], "en");

        // Act & Assert: the last rewording wins whole, so the first one's greeting doesn't show, also as the
        // default-language fallback of an app without languages. A set deriving from no other set words every text.
        Greeting(registry, "en-US").ShouldBe("Hello");
        Farewell(registry, "en-US").ShouldBe("Bye");
        Greeting(withoutLanguages, "fr-FR").ShouldBe("Hello");
        Farewell(withoutLanguages, "fr-FR").ShouldBe("Bye");
        Greeting(standaloneLast, "en-US").ShouldBe("Hello");
        Farewell(standaloneLast, "en-US").ShouldBe("So long");
    }

    [Test]
    public void Word_ForRewordingDerivedFromAnother_ShouldShowTheTextsOfBoth()
    {
        // Arrange: a rewording that derives from another, registered after or before it.
        var registry = LocalizationRegistry.Create([new SetLocalizationEn(), new SetRewordedLocalizationEn(), new SetFurtherRewordedLocalizationEn()], "en");
        var derivedFirst = LocalizationRegistry.Create([new SetLocalizationEn(), new SetFurtherRewordedLocalizationEn(), new SetRewordedLocalizationEn()], "en");

        // Act & Assert: the derived rewording shows its own farewell and the greeting it inherits. Registered first, it
        // gives way to the rewording it derives from, as any set registered before another does.
        Greeting(registry, "en-US").ShouldBe("Hi");
        Farewell(registry, "en-US").ShouldBe("Bye");
        Greeting(derivedFirst, "en-US").ShouldBe("Hi");
        Farewell(derivedFirst, "en-US").ShouldBe("Goodbye");
    }

    [Test]
    public void Word_ForRewording_ShouldReadEveryTextFromTheRewording()
    {
        // Arrange
        var english = new DescribedLocalizationEn();
        var rewording = new DescribedRewordedLocalizationEn();
        var registry = LocalizationRegistry.Create([english, rewording], "en");

        // Act
        var farewell = Farewell(registry, "en-US");

        // Assert: the rewording leaves the farewell as it was, so the English wording shows. It's read once, from the
        // rewording, which inherits it and may override a member it reads.
        farewell.ShouldBe("Goodbye");
        rewording.FarewellReads.ShouldBe(1);
        english.FarewellReads.ShouldBe(0);
    }

    [Test]
    public void Word_ForRegionThatOverridesOnlyAMemberItsTextsRead_ShouldShowItsTextsWithThatMember()
    {
        // Arrange: a library's English set, and its British set, which overrides only the name the texts read.
        var registry = LocalizationRegistry.Create([new SetWithMemberLocalizationEn(), new SetWithMemberBritishLocalization()], "en", "nl");

        // Act & Assert: British users see the British name in the texts the British set inherits. Other English
        // cultures keep the English name.
        Greeting(registry, "en-GB").ShouldBe("Hello, Annie");
        Farewell(registry, "en-GB").ShouldBe("Goodbye, Annie");
        Greeting(registry, "en-US").ShouldBe("Hello, Anna");
    }

    [Test]
    public void Word_ForRewordingThatOverridesOnlyAMemberItsTextsRead_ShouldShowItsTextsWithThatMember()
    {
        // Arrange: an app's rewording that overrides only the name the texts read. Then the same after a shared
        // library's rewording of the greeting, and next to the British set and a rewording of it.
        var english = new SetWithMemberLocalizationEn();
        var rewording = new SetWithMemberRewordedLocalizationEn();
        var registry = LocalizationRegistry.Create([english, rewording], "en", "nl");
        var withGreeting = LocalizationRegistry.Create([english, new SetWithMemberGreetingRewordedLocalizationEn(), rewording], "en", "nl");
        var withBritish = LocalizationRegistry.Create([english, new SetWithMemberBritishLocalization(), rewording], "en", "nl");
        var withBritishRewording = LocalizationRegistry.Create(
            [english, new SetWithMemberBritishLocalization(), rewording, new SetWithMemberRewordedBritishLocalization()],
            "en",
            "nl");

        // Act & Assert: the rewording's name shows in every text, also where English is the fallback. It wins whole
        // over the other rewording, whose greeting doesn't show. British users see the British set with its own name,
        // until a rewording of the British set gives them the rewording's name.
        Greeting(registry, "en-US").ShouldBe("Hello, Ann");
        Farewell(registry, "fr-FR").ShouldBe("Goodbye, Ann");
        Greeting(withGreeting, "en-US").ShouldBe("Hello, Ann");
        Farewell(withGreeting, "en-US").ShouldBe("Goodbye, Ann");
        Greeting(withBritish, "en-GB").ShouldBe("Hello, Annie");
        Greeting(withBritish, "en-US").ShouldBe("Hello, Ann");
        Greeting(withBritishRewording, "en-GB").ShouldBe("Hello, Ann");
    }

    [Test]
    public void Word_ForRegion_ShouldKeepRegionalSetDerivingFromNoSetOfItsLanguage()
    {
        // Arrange
        var registry = LocalizationRegistry.Create([new SetLocalizationEn(), new SetStandaloneBritishLocalization(), new SetRewordedLocalizationEn()], "en");

        // Act & Assert: the British set wins for British users, so the English rewording doesn't apply to it.
        Greeting(registry, "en-GB").ShouldBe("Hello");
    }

    [Test]
    public void Word_ForRegion_ShouldShowTheSetOfTheMostSpecificNameWhole()
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

        // Act & Assert: the Taiwanese set wins, with what it inherits from the traditional and the Chinese sets. The
        // rewording of Chinese doesn't reach it.
        Greeting(registry, "fr-FR").ShouldBe("Ni hao");
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
    public void Word_OfTextThatTakesValues_ShouldFormatValuesAsTheThreadDoes()
    {
        // Arrange: a user who reads Dutch and writes numbers as an American does.
        using var _ = new CulturesScope(uiCulture: "nl-NL", culture: "en-US");
        var registry = LocalizationRegistry.Create([new SetCountedLocalizationEn(), new SetCountedLocalizationNl()]);

        // Act & Assert
        Total(registry, CultureInfo.CurrentUICulture).ShouldBe("Totaal: 1,234.50");
        CultureInfo.CurrentCulture.Name.ShouldBe("en-US");
    }

    [Test]
    public void Word_ForFallbackLanguage_ShouldPutUICultureBackWhenSetThrows()
    {
        // Arrange: a German user of an app in French, whose French set throws.
        using var _ = new CulturesScope(uiCulture: "de-DE", culture: "en-GB");
        var registry = LocalizationRegistry.Create([new SetCountedLocalizationFr()], "fr");

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => Total(registry, CultureInfo.CurrentUICulture)).Message.ShouldBe("No wording.");
        CultureInfo.CurrentCulture.Name.ShouldBe("en-GB");
        CultureInfo.CurrentUICulture.Name.ShouldBe("de-DE");
    }

    [Test]
    public void Word_ForCultureTheAppDoesNotShow_ShouldRunMethodOfSetInTheCulturesOfTheUser()
    {
        // Arrange: a German user of an app in English and Dutch, so the text shows English. Every text its wording
        // shows ends in English too, so it needs no scope of English, as a property doesn't.
        using var _ = new CulturesScope(uiCulture: "de-DE", culture: "de-DE");
        var german = CultureInfo.CurrentUICulture;
        var registry = LocalizationRegistry.Create([new SetThreadCultureLocalizationEn(), new SetCountedLocalizationEn()], "en", "nl");

        // Act
        var cultures = Word<ISetThreadCultureLocalization, SetThreadCultureLocalizationEn>(registry, german, "CulturesOf", static set => set.CulturesOf(1), takesValues: true);
        var total = Total(registry, german);

        // Assert: the values keep the user's format.
        cultures.ShouldBe("1: de-DE/de-DE");
        total.ShouldBe("Total: 1.234,50");
        CultureInfo.CurrentUICulture.Name.ShouldBe("de-DE");
        CultureInfo.CurrentCulture.Name.ShouldBe("de-DE");
    }

    [Test]
    public void Word_OfMethodThatFallsBackAlone_ShouldRunMethodOfSetInUICultureOfThatLanguage()
    {
        // Arrange: a Dutch user of an app that has Dutch, and a contract without a Dutch set, so this text alone falls
        // back to English.
        using var _ = new CulturesScope(uiCulture: "nl-NL", culture: "nl-NL");
        var dutch = CultureInfo.CurrentUICulture;
        var registry = LocalizationRegistry.Create([new SetThreadCultureLocalizationEn()], "en", "nl");

        // Act
        var cultures = Word<ISetThreadCultureLocalization, SetThreadCultureLocalizationEn>(registry, dutch, "CulturesOf", static set => set.CulturesOf(1), takesValues: true);

        // Assert: the method of the set runs in English, the language it words, so a text it shows is English too.
        // The values keep the user's format, and the Dutch UI culture of the thread is back after.
        cultures.ShouldBe("1: en/nl-NL");
        CultureInfo.CurrentUICulture.Name.ShouldBe("nl-NL");
    }

    [Test]
    public void Word_OfMethodThatFallsBackAlone_ShouldCopyExecutionContextOnlyToChangeUICulture()
    {
        // Arrange: the same text for an English user and for a Dutch one of an app in English and Dutch, whose contract
        // lacks a Dutch set. The Dutch user's text falls back to English alone, so both show "3 items". It runs the
        // method of the set in English, and the UI culture is an AsyncLocal, so setting it copies the execution context.
        var registry = LocalizationRegistry.Create([new SetCountedLocalizationEn()], "en", "nl");
        var englishCulture = Culture("en");

        string Show()
        {
            return Word<ISetCountedLocalization, SetCountedLocalizationEn>(registry, CultureInfo.CurrentUICulture, "Items", static set => set.Items(3), takesValues: true);
        }

        // Act: per 10,000 texts, and per 10,000 times the UI culture changes and the context is restored, in the Dutch
        // user's context, so with as many AsyncLocals to copy.
        long english;
        using (new CulturesScope(uiCulture: "en-US", culture: "en-US"))
            english = GeneratedSetsTests.Allocated(Show) / 10_000;

        long dutch;
        long changed;
        using (new CulturesScope(uiCulture: "nl-NL", culture: "nl-NL"))
        {
            Show().ShouldBe("3 items");
            dutch = GeneratedSetsTests.Allocated(Show) / 10_000;
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
        dutch.ShouldBeLessThanOrEqualTo(english + changed);
    }

    [Test]
    [Arguments("en", "inner")] // A culture with wording: no scope is entered, so no context is captured.
    [Arguments("de", "outer")] // Another culture, which falls back to English: the scope changes the UI culture.
    public void Word_WhenMethodOfSetChangesAnAsyncLocal_ShouldEndThatChangeOnlyWithAScopeThatChangedACulture(string cultureName, string expectedFlag)
    {
        // Arrange
        using var _ = new CulturesScope(Culture(cultureName), CultureInfo.CurrentCulture);
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
        // the call. The values keep the user's format.
        Cultures(englishFirst, dutch).ShouldBe("en/nl-NL");
        Cultures(frenchFirst, dutch).ShouldBe("en/nl-NL");

        // And the user's culture again once the wording is done.
        Cart.ShouldBe("Winkelwagen");
        CultureInfo.CurrentUICulture.ShouldBeSameAs(dutch);
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
        Word<ISetThreadCultureLocalization, SetThreadCultureLocalizationEn>(registry, german, "Cultures", static set => set.Cultures).ShouldBe("de-DE/de-DE");
    }

    [Test]
    public void Word_OfPropertyWithWordingForTheCulture_ShouldEnterNoScope()
    {
        // Arrange: a British user, whom the English set serves.
        using var _ = new CulturesScope(uiCulture: "en-GB", culture: "en-GB");
        var registry = LocalizationRegistry.Create([new SetThreadCultureLocalizationEn()], "en", "nl");

        // Act & Assert: the thread's cultures are the user's already.
        Word<ISetThreadCultureLocalization, SetThreadCultureLocalizationEn>(registry, CultureInfo.CurrentUICulture, "Cultures", static set => set.Cultures).ShouldBe("en-GB/en-GB");
    }

    [Test]
    public void ResolvedSets_ShouldKeepSetOfTheCulture()
    {
        // Arrange: a set for each language.
        var dutch = new SetLocalizationNl();
        var registry = LocalizationRegistry.Create([new SetLocalizationEn(), dutch], "en", "nl");

        // Act
        var resolved = ResolveGreetingAndFarewell(registry, "nl-NL");

        // Assert: for a property and for a text that takes values, because the thread of a Dutch user needs no scope.
        resolved.Set.ShouldBe(dutch);
        resolved.SetForValues.ShouldBe(dutch);
        resolved.Lookup.ShouldBeSameAs(registry.Resolve(Culture("nl-NL")));
    }

    [Test]
    public void ResolvedSets_ShouldKeepTheRewordingThatWinsForEveryText()
    {
        // Arrange: a rewording of the greeting alone.
        var english = new DescribedLocalizationEn();
        var rewording = new DescribedRewordedLocalizationEn();
        var registry = LocalizationRegistry.Create([english, rewording], "en");

        // Act
        var resolved = ResolveGreetingAndFarewell(registry, "en-US");

        // Assert: the rewording wins whole, so it's kept for the farewell it inherits too, and the English set is never
        // read.
        Greeting(registry, "en-US").ShouldBe("Hi");
        Farewell(registry, "en-US").ShouldBe("Goodbye");
        resolved.Set.ShouldBe(rewording);
        resolved.SetForValues.ShouldBe(rewording);
        english.FarewellReads.ShouldBe(0);
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
        resolved.Set.ShouldBeNull();
        resolved.SetForValues.ShouldBeNull();
        resolved.Fallback.ShouldBeNull();
    }

    [Test]
    public void ResolvedSets_ForCultureTheAppDoesNotShow_ShouldKeepSetForPropertyAndMethod()
    {
        // Arrange: a German user of an app in English and Dutch. Neither a property nor a method of the set enters a
        // scope there, because every text it shows ends in English anyway.
        var english = new SetLocalizationEn();
        var registry = LocalizationRegistry.Create([english, new SetLocalizationNl()], "en", "nl");

        // Act
        var resolved = ResolveGreetingAndFarewell(registry, "de-DE");

        // Assert
        resolved.Set.ShouldBe(english);
        resolved.SetForValues.ShouldBe(english);
        resolved.Fallback.ShouldBeNull();
    }

    [Test]
    public void ResolvedSets_OfTextThatTakesValues_ThatFallsBackAlone_ShouldKeepSetAsFallback()
    {
        // Arrange: a Dutch user of an app in English and Dutch, whose contract lacks a Dutch set, so the text with a
        // parameter alone falls back to English.
        var english = new SetCountedLocalizationEn();
        var registry = LocalizationRegistry.Create([english], "en", "nl");
        var dutch = Culture("nl-NL");
        var resolved = new ResolvedSets<ISetCountedLocalization>(registry.Resolve(dutch));

        // Act
        var wording = Word<ISetCountedLocalization, SetCountedLocalizationEn>(registry, dutch, "Items", static set => set.Items(3),
            takesValues: true, resolved: resolved);

        // Assert: kept with the culture of English, and not for the generated class to call without its scope.
        wording.ShouldBe("3 items");
        resolved.SetForValues.ShouldBeNull();
        resolved.Fallback.ShouldNotBeNull().Set.ShouldBeSameAs(english);
        resolved.Fallback.Culture.ShouldNotBeNull().Name.ShouldBe("en");
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

        // Assert: a French user's text is worded in a scope of English, so it isn't kept.
        british.Set.ShouldBe(defaultSet);
        british.SetForValues.ShouldBe(defaultSet);
        french.Set.ShouldBeNull();
        french.SetForValues.ShouldBeNull();
        french.Fallback.ShouldBeNull();
    }

    [Test]
    public void ResolvedSets_Keep_ForPropertyAlone_ShouldLeaveTheOthersEmpty()
    {
        // Arrange
        var registry = LocalizationRegistry.Create([]);
        var resolved = new ResolvedSets<ISetLocalization>(registry.Resolve(Culture("en-US")));
        var set = new SetLocalizationEn();

        // Act
        resolved.Keep(set, forProperty: true, forValues: false);

        // Assert
        resolved.Set.ShouldBe(set);
        resolved.SetForValues.ShouldBeNull();
        resolved.Fallback.ShouldBeNull();
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void GetLocalizationSet_ShouldGiveKeptSetAfterTextWasWordedOnceAndNoneBefore()
    {
        // Arrange: a contract no other test uses, with its Dutch set in the sets of the whole app.
        using var _ = new CulturesScope("nl-NL");
        var dutch = new SetKeptLocalizationNl();
        LocalizationRegistry.Register(dutch);

        // Act & Assert: none before the text is worded, for a property and for a text that takes values.
        GeneratedCode.GetLocalizationSet<ISetKeptLocalization>().ShouldBeNull();
        GeneratedCode.GetLocalizationSetForValues<ISetKeptLocalization>().ShouldBeNull();

        KeptTexts.WordCheckout().ShouldBe("Afrekenen");
        GeneratedCode.GetLocalizationSet<ISetKeptLocalization>().ShouldBeSameAs(dutch);

        KeptTexts.WordTotal(1.5m).ShouldBe("Totaal: 1,50");
        GeneratedCode.GetLocalizationSetForValues<ISetKeptLocalization>().ShouldBeSameAs(dutch);

        // Kept per culture: an English user has none yet, and gets the set the text was declared with.
        using (new CulturesScope("en-US"))
        {
            GeneratedCode.GetLocalizationSet<ISetKeptLocalization>().ShouldBeNull();
            KeptTexts.Checkout.ShouldBe("Checkout");
            GeneratedCode.GetLocalizationSet<ISetKeptLocalization>().ShouldBeSameAs(GeneratedCode.GetDefaultSet<SetKeptLocalizationEn>());
        }

        GeneratedCode.GetLocalizationSet<ISetKeptLocalization>().ShouldBeSameAs(dutch);
        KeptTexts.Checkout.ShouldBe("Afrekenen");
        KeptTexts.Total(1.5m).ShouldBe("Totaal: 1,50");
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
        // user whose language has a registered set, and one whom the set the text was declared with serves.
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
        var keptBefore = GeneratedCode.GetLocalizationSet<ISetRegisteredLocalization>();

        var dutch = new SetRegisteredLocalizationNl();
        LocalizationRegistry.Register(dutch);

        var keptAfterRegistration = GeneratedCode.GetLocalizationSet<ISetRegisteredLocalization>();
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
        GeneratedCode.GetLocalizationSet<ISetRegisteredLocalization>().ShouldBeSameAs(dutch);
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void GeneratedText_AfterNewRegistration_ShouldNotBeServedSetKeptBefore()
    {
        // Arrange: an English user, whose text keeps the set it was declared with.
        using var _ = new CulturesScope("en-US");
        StaleGreeting.ShouldBe("Hello");
        var keptBefore = GeneratedCode.GetLocalizationSet<ISetStaleLocalization>().ShouldNotBeNull();

        // Act: a set registers that rewords the text.
        var rewording = new SetStaleRewordedLocalizationEn();
        LocalizationRegistry.Register(rewording);

        // Assert
        GeneratedCode.GetLocalizationSet<ISetStaleLocalization>().ShouldBeNull();
        StaleGreeting.ShouldBe("Hi");
        StaleGreeting.ShouldBe("Hi");
        GeneratedCode.GetLocalizationSet<ISetStaleLocalization>().ShouldBeSameAs(rewording);
        keptBefore.ShouldBeSameAs(GeneratedCode.GetDefaultSet<SetStaleLocalizationEn>());
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void GeneratedText_ThatTakesValues_ShouldBeWordedAndFormattedForTheCulturesOfTheThread()
    {
        // Arrange: a contract no other test uses, so the sets of the whole app hold only this test's set for it.
        LocalizationRegistry.Register(new SetTotalLocalizationNl());
        const decimal amount = 1234.5m;

        // Act & Assert: the Dutch wording, with the number as a Dutch user writes it, also in the text the method of
        // the set reads itself.
        using (new CulturesScope("nl-NL"))
        {
            TotalTexts.Total(amount).ShouldBe("Totaal: 1.234,50");
            TotalTexts.Summary(amount).ShouldBe("Overzicht: Totaal: 1.234,50");
        }

        using (new CulturesScope("en-US"))
        {
            TotalTexts.Total(amount).ShouldBe("Total: 1,234.50");
            TotalTexts.Summary(amount).ShouldBe("Summary: Total: 1,234.50");
        }
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void GeneratedText_ShouldShowTextThatPropertyReadsForTheCultureOfTheUser()
    {
        // Arrange: texts whose property reads another text: an inline one, and one that takes a value.
        LocalizationRegistry.Register(new SetShowingLocalizationEn(), new SetShowingLocalizationNl(), new SetTotalLocalizationNl());

        // Act & Assert: the text the property reads follows the user by itself, with the number as a Dutch user writes
        // it.
        using (new CulturesScope("nl-NL"))
        {
            ShowingTexts.Hint.ShouldBe("Open de Winkelwagen om te betalen");
            ShowingTexts.Summary.ShouldBe("Overzicht: Totaal: 1.234,50");
            ShowingTexts.Hint.ShouldBe("Open de Winkelwagen om te betalen");
        }

        using (new CulturesScope("en-US"))
        {
            ShowingTexts.Hint.ShouldBe("Open the Cart to pay");
            ShowingTexts.Summary.ShouldBe("Summary: Total: 1,234.50");
        }
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void GeneratedText_ForCultureTheAppDoesNotShow_ShouldShowTextThatSetReadsInTheFallbackLanguage()
    {
        // Arrange: a German user. This app shows English and Dutch, so every text falls back to English for them.
        using var _ = new CulturesScope(uiCulture: "de-DE", culture: "de-DE");
        LocalizationRegistry.Register(new SetShowingLocalizationEn(), new SetShowingLocalizationNl(), new SetTotalLocalizationNl());

        // Act & Assert: English throughout, with the number as the German user writes it.
        ShowingTexts.Hint.ShouldBe("Open the Cart to pay");
        ShowingTexts.Summary.ShouldBe("Summary: Total: 1.234,50");
        TotalTexts.Summary(1234.5m).ShouldBe("Summary: Total: 1.234,50");

        CultureInfo.CurrentUICulture.Name.ShouldBe("de-DE");
        CultureInfo.CurrentCulture.Name.ShouldBe("de-DE");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void GeneratedText_ThatFallsBackWhereFlowIsSuppressed_ShouldPutUICultureBack()
    {
        // Arrange: a German thread that doesn't flow its execution context, so there's no context to restore. The app
        // doesn't support German, so the text falls back to English, in a scope of it.
        using var _ = new CulturesScope(uiCulture: "de-DE", culture: "de-DE");
        using var flow = ExecutionContext.SuppressFlow();

        // Act
        var text = TotalTexts.Total(1234.5m);

        // Assert: the English wording with the number as the German user writes it, and the cultures of this thread as
        // they were.
        text.ShouldBe("Total: 1.234,50");
        CultureInfo.CurrentUICulture.Name.ShouldBe("de-DE");
        CultureInfo.CurrentCulture.Name.ShouldBe("de-DE");
    }

    [Test]
    public void WordingScope_ShouldSetUICultureOfThreadAndPutItBack()
    {
        // Arrange
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-US");
        var dutch = Culture("nl-NL");
        var german = Culture("de-DE");

        // Act & Assert: the UI culture alone, so that values keep the user's format.
        using (new WordingScope(dutch))
        {
            CultureInfo.CurrentUICulture.ShouldBeSameAs(dutch);
            CultureInfo.CurrentCulture.Name.ShouldBe("en-US");

            // A scope inside another, as for a text the member of a set reads, which falls back itself.
            using (new WordingScope(german))
                CultureInfo.CurrentUICulture.ShouldBeSameAs(german);

            CultureInfo.CurrentUICulture.ShouldBeSameAs(dutch);
        }

        CultureInfo.CurrentUICulture.Name.ShouldBe("en-US");
        CultureInfo.CurrentCulture.Name.ShouldBe("en-US");

        // A scope without a culture changes nothing, as for a language that has no culture on this machine.
        using (new WordingScope(culture: null))
            CultureInfo.CurrentUICulture.Name.ShouldBe("en-US");

        // A scope that was never entered puts nothing back.
        default(WordingScope).Dispose();
        CultureInfo.CurrentUICulture.Name.ShouldBe("en-US");
    }

    [Test]
    [NotInParallel]
    public void WordingScope_WhereDefaultCultureOfProcessChanges_ShouldLeaveValuesOnTheNewDefault()
    {
        // Arrange: a thread that sets no culture itself, so it follows the default cultures of the process. Its context
        // is one no other code uses, so no other scope switched from it. The defaults hold for the whole process, so no
        // other test runs next to this one.
        var american = Culture("en-US");
        var dutch = Culture("nl-NL");
        var german = Culture("de-DE");
        var defaultUICulture = CultureInfo.DefaultThreadCurrentUICulture;
        var defaultCulture = CultureInfo.DefaultThreadCurrentCulture;
        var ownContext = new AsyncLocal<object?>();
        CultureInfo? uiCultureInScope = null;
        CultureInfo? cultureInScope = null;

        // Act
        try
        {
            CultureInfo.DefaultThreadCurrentUICulture = american;
            CultureInfo.DefaultThreadCurrentCulture = american;

            var thread = new Thread(() =>
            {
                ownContext.Value = new object();

                // A Dutch text, whose scope keeps the context it switched to. Then the default format becomes German,
                // and the same text restores that context.
                using (new WordingScope(dutch))
                {
                }

                CultureInfo.DefaultThreadCurrentCulture = german;

                using (new WordingScope(dutch))
                {
                    uiCultureInScope = CultureInfo.CurrentUICulture;
                    cultureInScope = CultureInfo.CurrentCulture;
                }
            });

            using (ExecutionContext.SuppressFlow())
                thread.Start();

            thread.Join();
        }
        finally
        {
            CultureInfo.DefaultThreadCurrentUICulture = defaultUICulture;
            CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
        }

        // Assert: the UI culture the scope was given, and the new default for values, which the kept context doesn't
        // hold.
        uiCultureInScope.ShouldBeSameAs(dutch);
        cultureInScope.ShouldBeSameAs(german);
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void GeneratedText_WhoseSetShowsTextsInsideUseCulture_ShouldShowThemInItsCulture()
    {
        // Arrange: a German user of an app that shows English and Dutch, so the text falls back to English. Its method
        // shows two texts inside UseCulture for Dutch, and the same two after it.
        LocalizationRegistry.Register(new SetUsingCultureLocalizationEn(), new SetKeptLocalizationNl());

        // Act: twice, so that the second text is worded by the set kept for it.
        string[] wordings;
        using (new CulturesScope(uiCulture: "de-DE"))
            wordings = [UsingCultureCheckouts(2), UsingCultureCheckouts(2)];

        // Assert: Dutch inside UseCulture, which wins over the language the text falls back to. Then English again.
        wordings.ShouldAllBe(wording => wording == "2: Afrekenen Winkelwagen / Checkout Cart");
    }

    [Test]
    public void WordingScope_TakingTurnsBetweenCulturesInOneContext_ShouldCopyTheContextOnlyTheFirstTimeForEach()
    {
        // Arrange: an American thread whose texts take turns between two scopes. An example is a text that falls back
        // to the app's default language next to a library's text that falls back to its own.
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-US");
        var british = Culture("en-GB");
        var dutch = Culture("nl-NL");

        // Act: per 10,000 turns, after the first ones, which switched to each.
        var allocated = GeneratedSetsTests.Allocated(() =>
        {
            using (new WordingScope(british))
            {
            }

            using (new WordingScope(dutch))
            {
            }

            return british;
        }) / 10_000;

        // Assert: nothing, because each turn restores a context kept from the first.
        allocated.ShouldBe(0);
    }
}
