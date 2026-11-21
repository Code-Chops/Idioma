using System.Runtime.CompilerServices;
using CodeChops.Idioma.Internals;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for the texts of a set and the texts with a key of <see cref="LocalizableText"/>: the key, the localization contract a text is declared against, the fallback to
/// the default language, what a text shows through the sets of the whole app, and the equality that includes key and
/// contract.
/// </summary>
public sealed class LocalizableTextSetsTests
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

    /// <summary>
    /// A set whose name gives no language, so its LocalizationLanguageCode throws.
    /// </summary>
    private sealed class SetLocalization1 : ISetLocalization
    {
        public string Greeting => "Hello";
        public string Farewell => "Goodbye";
    }

    private sealed class SetLocalizationNl : ISetLocalization
    {
        public string Greeting => "Hallo";
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
    /// A set of another contract.
    /// </summary>
    private sealed class SetOtherLocalizationNl : ILocalizationSet;

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
    /// A contract no other test uses, for the test that registers a rewording of its default language with the sets of
    /// the whole app.
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
    /// A contract with texts that take a value: one that chooses its wording by it, one that formats it, and one
    /// whose value is a text itself.
    /// </summary>
    private interface ISetCountedLocalization : ILocalizationSet
    {
        string Items(int count);
        string Total(decimal amount);
        string Removed(LocalizableText what);
    }

    private sealed class SetCountedLocalizationEn : ISetCountedLocalization
    {
        public string Items(int count) => count is 1 ? "1 item" : $"{count} items";
        public string Total(decimal amount) => $"Total: {amount:N2}";
        public string Removed(LocalizableText what) => $"{what} removed";
    }

    private sealed class SetCountedLocalizationNl : ISetCountedLocalization
    {
        public string Items(int count) => count is 1 ? "1 artikel" : $"{count} artikelen";
        public string Total(decimal amount) => $"Totaal: {amount:N2}";
        public string Removed(LocalizableText what) => $"{what} verwijderd";
    }

    /// <summary>
    /// A French set whose texts can't be worded.
    /// </summary>
#pragma warning disable LOC302 // It words no value on purpose, because it throws.
    private sealed class SetCountedLocalizationFr : ISetCountedLocalization
    {
        public string Items(int count) => throw new InvalidOperationException("No wording.");
        public string Total(decimal amount) => throw new InvalidOperationException("No wording.");
        public string Removed(LocalizableText what) => throw new InvalidOperationException("No wording.");
    }
#pragma warning restore LOC302

    /// <summary>
    /// A contract whose text shows the UI culture its method runs in, after the text among its values.
    /// </summary>
    private interface ISetThreadCultureLocalization : ILocalizationSet
    {
        string Removed(LocalizableText what);
    }

    private sealed class SetThreadCultureLocalizationEn : ISetThreadCultureLocalization
    {
        public string Removed(LocalizableText what) => $"{what} removed in {CultureInfo.CurrentUICulture.Name}";
    }

    /// <summary>
    /// Sets both current cultures for the duration of the scope (they are thread-bound, so tests on other threads are
    /// unaffected): the one a text takes its wording for, and the one values are formatted for.
    /// </summary>
    private sealed class CulturesScope : IDisposable
    {
        private CultureInfo PreviousCulture { get; } = CultureInfo.CurrentCulture;
        // ReSharper disable once InconsistentNaming - Same casing as the property of CultureInfo.
        private CultureInfo PreviousUICulture { get; } = CultureInfo.CurrentUICulture;

        public CulturesScope(string uiCulture, string culture)
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(uiCulture);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        }

        public void Dispose()
        {
            CultureInfo.CurrentUICulture = this.PreviousUICulture;
            CultureInfo.CurrentCulture = this.PreviousCulture;
        }
    }

    /// <summary>
    /// A contract no other test uses, for the tests that register its Dutch set with the sets of the whole app.
    /// </summary>
    private interface ISetProvidedLocalization : ILocalizationSet
    {
        string Total(decimal amount);
        string Removed(LocalizableText what);
    }

    private sealed class SetProvidedLocalizationEn : ISetProvidedLocalization
    {
        public string Total(decimal amount) => $"Total: {amount:N2}";
        public string Removed(LocalizableText what) => $"{what} removed";
    }

    private sealed class SetProvidedLocalizationNl : ISetProvidedLocalization
    {
        public string Total(decimal amount) => $"Totaal: {amount:N2}";
        public string Removed(LocalizableText what) => $"{what} verwijderd";
    }

    /// <summary>
    /// The culture <see cref="LocalizationRegistry.CurrentUICultureProvider"/> gives inside a scope, and none outside
    /// it. The provider is one for the whole process, and other tests run next to these, so it gives a culture only to
    /// the test that asked for one. The tests that use it don't run next to each other, so the scope puts back the
    /// provider it found.
    /// </summary>
    private sealed class ProvidedCultureScope : IDisposable
    {
        private static AsyncLocal<CultureInfo?> Provided { get; } = new();
        private static AsyncLocal<StrongBox<int>?> Calls { get; } = new();

        private Func<CultureInfo?>? PreviousProvider { get; } = LocalizationRegistry.CurrentUICultureProvider;
        private StrongBox<int> CallsInScope { get; } = new();

        /// <param name="name">The culture the provider gives, or <see langword="null"/> for a provider that gives none.</param>
        public ProvidedCultureScope(string? name)
        {
            LocalizationRegistry.CurrentUICultureProvider = static () =>
            {
                if (Calls.Value is { } calls)
                    calls.Value++;

                return Provided.Value;
            };

            Provided.Value = name is null ? null : CultureInfo.GetCultureInfo(name);
            Calls.Value = this.CallsInScope;
        }

        /// <summary>
        /// How often the provider was asked for a culture inside the scope.
        /// </summary>
        public int CallCount => this.CallsInScope.Value;

        public void Dispose()
        {
            Provided.Value = null;
            Calls.Value = null;
            LocalizationRegistry.CurrentUICultureProvider = this.PreviousProvider;
        }
    }

    /// <summary>
    /// An American culture under a name the UI culture of a thread can't take, as a culture of a derived class can have.
    /// </summary>
    private sealed class UnnamableCulture() : CultureInfo("en-US")
    {
        public override string Name => "en US";
    }

    private static LocalizableText Greeting { get; } = LocalizableText.Create<ISetLocalization>("Set.Greeting", t => t.Greeting, new SetLocalizationEn());

    private static LocalizableText Farewell { get; } = LocalizableText.Create<ISetLocalization>("Set.Farewell", t => t.Farewell, new SetLocalizationEn());
    #endregion

    [Test]
    public void GetTranslation_ShouldPhraseTextInGivenSet()
    {
        // Arrange & Act & Assert: a set of the contract selects its member; a set of another contract, and a key-only
        // text, are outside the contract.
        Greeting.GetWording(new SetLocalizationNl()).ShouldBe("Hallo");
        Greeting.GetWording(new SetLocalizationEn()).ShouldBe("Hello");
        Greeting.GetWording(new SetOtherLocalizationNl()).ShouldBeNull();
        LocalizableText.Keyed("MyApp.Greeting", "Hello").GetWording(new SetLocalizationNl()).ShouldBeNull();
        Should.Throw<ArgumentNullException>(() => Greeting.GetWording((ILocalizationSet)null!));
    }

    [Test]
    public void Create_WithDefaults_ShouldTakeDefaultTextAndLanguageFromSet()
    {
        // Arrange & Act
        var text = Greeting;

        // Assert
        text.Key.ShouldBe("Greeting");
        text.FullKey.ShouldBe("Set.Greeting");
        text.DefaultText.ShouldBe("Hello");
        text.Contract.ShouldBe(typeof(ISetLocalization));
        text.DefaultLanguage.ShouldBe("en");
    }

    [Test]
    public void ToString_FromRegistryWithoutLanguages_ShouldShowEveryLanguageWithASet()
    {
        // Arrange: no languages given, so nothing of what the app of this process declares applies.
        var registry = LocalizationRegistry.Create([new SetLocalizationNl()]);

        // Act & Assert: the Dutch set shows, and a culture without a set gets the text's own default wording.
        Greeting.ToString(CultureInfo.GetCultureInfo("nl-BE"), registry).ShouldBe("Hallo");
        Greeting.ToString(CultureInfo.GetCultureInfo("fr-FR"), registry).ShouldBe("Hello");
    }

    [Test]
    public void ToString_FromRegistryWithLanguages_ShouldShowOnlyThoseAndFallBackToItsDefault()
    {
        // Arrange: a registry that has Dutch, with and without Dutch among its languages.
        ILocalizationSet[] sets = [new SetLocalizationEn(), new SetLocalizationNl()];
        var dutchFirst = LocalizationRegistry.Create(sets, "nl", "en");
        var withoutDutch = LocalizationRegistry.Create(sets, "en", "fr");

        // Act & Assert: a culture no language serves gets the registry's default language, even where it has a set.
        Greeting.ToString(CultureInfo.GetCultureInfo("fr-FR"), dutchFirst).ShouldBe("Hallo");
        Greeting.ToString(CultureInfo.GetCultureInfo("en-GB"), dutchFirst).ShouldBe("Hello");
        Greeting.ToString(CultureInfo.GetCultureInfo("nl-NL"), withoutDutch).ShouldBe("Hello");
    }

    [Test]
    public void Create_ShouldThrowForInvalidArguments()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentException>(() => LocalizableText.Create<ISetLocalization>("", t => t.Greeting, new SetLocalizationEn()));
        Should.Throw<ArgumentException>(() => LocalizableText.Create<ISetLocalization>(" ", t => t.Greeting, new SetLocalizationEn()));
        Should.Throw<ArgumentNullException>(() => LocalizableText.Create<ISetLocalization>(null!, t => t.Greeting, new SetLocalizationEn()));
        Should.Throw<ArgumentNullException>(() => LocalizableText.Create<ISetLocalization>("K", selector: null!, new SetLocalizationEn()));
        Should.Throw<ArgumentNullException>(() => LocalizableText.Create<ISetLocalization>("K", t => t.Greeting, defaultSet: null!)).ParamName.ShouldBe("defaultSet");

        // The key first, before the set's LocalizationLanguageCode runs, whose exception would hide the argument's.
        Should.Throw<ArgumentException>(() => LocalizableText.Create<ISetLocalization>("", t => t.Greeting, new SetLocalization1())).ParamName.ShouldBe("key");

        // A text that takes values names the key as its caller does too.
        Should.Throw<ArgumentNullException>(() => GeneratedCode.CreateSetText<ISetCountedLocalization, int>(null!, static (set, value) => set.Items(value), new SetCountedLocalizationEn(), 3)).ParamName.ShouldBe("key");

        // A set of another contract can't give the default wording. The set is named as the parameter of
        // GeneratedCode.CreateSetText, which passes it on.
        Should.Throw<ArgumentException>(() => LocalizableText.Create<ISetLocalization>("K", t => t.Greeting, new SetCountedLocalizationEn())).ParamName.ShouldBe("defaultSet");
    }

    [Test]
    public void KeyOnlyText_ShouldShowDefaultTextWhateverIsRegistered()
    {
        // Arrange
        var text = LocalizableText.Keyed("MyApp.Greeting", "Hello");
        var registry = LocalizationRegistry.Create([new SetLocalizationNl()]);

        // Act & Assert: this package never reads the key, and a text without a contract matches no set. Such a text has
        // one key, which is not cut at its dot.
        text.Key.ShouldBe("MyApp.Greeting");
        text.FullKey.ShouldBe("MyApp.Greeting");
        text.Contract.ShouldBeNull();
        text.TranslationsByLanguage.ShouldBeEmpty();
        text.ToString(CultureInfo.GetCultureInfo("nl-NL"), registry).ShouldBe("Hello");
    }

    [Test]
    public void Constructor_ShouldThrowForInvalidArguments()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentNullException>(() => LocalizableText.Keyed(key: null!, "Hello"));
        Should.Throw<ArgumentException>(() => LocalizableText.Keyed("", "Hello"));
        Should.Throw<ArgumentException>(() => LocalizableText.Keyed(" ", "Hello"));
        Should.Throw<ArgumentNullException>(() => LocalizableText.Keyed("MyApp.Greeting", defaultText: null!));
    }

    [Test]
    public void WithTranslation_ShouldKeepKeyAndContract()
    {
        // Arrange & Act
        var text = Greeting.WithTranslation("fr", "Bonjour");

        // Assert: still a text of the same contract, now with an inline translation on top.
        text.Key.ShouldBe(Greeting.Key);
        text.FullKey.ShouldBe(Greeting.FullKey);
        text.Contract.ShouldBe(typeof(ISetLocalization));
        text.GetWording(new SetLocalizationNl()).ShouldBe("Hallo");
        text.GetWording(CultureInfo.GetCultureInfo("fr-FR")).ShouldBe("Bonjour");
        Greeting.TranslationsByLanguage.ShouldBeEmpty();
    }

    [Test]
    public void Equality_ShouldIgnoreKeyContractAndDefaultLanguage()
    {
        // Arrange: an American user, for whom no set of the contract is registered, so Greeting shows its default
        // wording. The British set leaves Greeting as English.
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-US");

        // Act & Assert: texts that show the same wording are equal, whatever else they have.
        LocalizableText.Keyed("Key", "Name").ShouldBe(LocalizableText.Keyed("Other", "Name"));
        LocalizableText.Keyed("Key", "Name").ShouldBe(new LocalizableText("Name"));
        LocalizableText.Keyed("Key", "Name").GetHashCode().ShouldBe(new LocalizableText("Name").GetHashCode());
        LocalizableText.Keyed("Key", "Name").ShouldNotBe(LocalizableText.Keyed("Key", "Other"));
        Greeting.ShouldBe(LocalizableText.Keyed(Greeting.Key!, "Hello"));
        Greeting.ShouldBe(LocalizableText.Create<ISetLocalization>("Set.Other", t => t.Greeting, new SetBritishLocalization()));
        Greeting.ShouldNotBe(LocalizableText.Create<ISetLocalization>("Set.Farewell", t => t.Farewell, new SetLocalizationEn()));
    }

    [Test]
    public void Create_WithArguments_ShouldKeepThemAndWordTextWithThemInEverySet()
    {
        // Arrange
        const int count = 3;
        var registry = LocalizationRegistry.Create([new SetCountedLocalizationNl()]);

        // Act: as a method of the generated class creates a text that takes a value.
        var text = GeneratedCode.CreateSetText<ISetCountedLocalization, int>("Set.Items", static (set, value) => set.Items(value), new SetCountedLocalizationEn(), count);

        // Assert: the default wording is that of the set it is created with, and every other set words it when asked.
        text.Key.ShouldBe("Items");
        text.FullKey.ShouldBe("Set.Items");
        text.Contract.ShouldBe(typeof(ISetCountedLocalization));
        text.DefaultLanguage.ShouldBe("en");
        text.DefaultText.ShouldBe("3 items");
        text.Arguments.ShouldBe([3]);
        text.GetWording(new SetCountedLocalizationNl()).ShouldBe("3 artikelen");
        text.ToString(CultureInfo.GetCultureInfo("nl-NL"), registry).ShouldBe("3 artikelen");
        text.ToString(CultureInfo.GetCultureInfo("fr-FR"), registry).ShouldBe("3 items");
        text.WithTranslation("fr", "3 articles").Arguments.ShouldBe([3]);
    }

    [Test]
    public void ToString_ForOtherCulture_ShouldWordTextThatTakesValuesAsForUserOfThatCulture()
    {
        // Arrange: both current cultures are American, and the recipient is Dutch.
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-US");
        var registry = LocalizationRegistry.Create([new SetCountedLocalizationEn(), new SetCountedLocalizationNl()]);
        var dutch = CultureInfo.GetCultureInfo("nl-NL");
        const decimal amount = 1234.5m;
        var cart = LocalizableText.FromGeneratedCode("Cart", ("en", "Cart"), ("nl", "Winkelwagen"));

        var total = GeneratedCode.CreateSetText<ISetCountedLocalization, decimal>("Set.Total", static (set, value) => set.Total(value), new SetCountedLocalizationEn(), amount);
        var removed = GeneratedCode.CreateSetText<ISetCountedLocalization, LocalizableText>("Set.Removed", static (set, value) => set.Removed(value), new SetCountedLocalizationEn(), cart);

        // Act & Assert: the Dutch wording, with the number as a Dutch reader writes it and the text among the values
        // in Dutch, and the cultures of this thread as they were.
        total.ToString(dutch, registry).ShouldBe("Totaal: 1.234,50");
        removed.ToString(dutch, registry).ShouldBe("Winkelwagen verwijderd");
        total.ToString(CultureInfo.GetCultureInfo("en-GB"), registry).ShouldBe("Total: 1,234.50");
        CultureInfo.CurrentCulture.Name.ShouldBe("en-US");
        CultureInfo.CurrentUICulture.Name.ShouldBe("en-US");
    }

    [Test]
    public void ToString_ForCurrentUICulture_ShouldFormatValuesForCurrentCultureAsToStringDoes()
    {
        // Arrange: a user who reads Dutch and writes numbers as an American does.
        using var _ = new CulturesScope(uiCulture: "nl-NL", culture: "en-US");
        var registry = LocalizationRegistry.Create([new SetCountedLocalizationEn(), new SetCountedLocalizationNl()]);
        const decimal amount = 1234.5m;
        var total = GeneratedCode.CreateSetText<ISetCountedLocalization, decimal>("Set.Total", static (set, value) => set.Total(value), new SetCountedLocalizationEn(), amount);

        // Act & Assert: passing the culture the text would follow anyway changes nothing.
        total.ToString(CultureInfo.CurrentUICulture, registry).ShouldBe("Totaal: 1,234.50");
    }

    [Test]
    public void ToString_ForOtherCulture_ShouldPutCulturesBackWhenSetThrows()
    {
        // Arrange
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-GB");
        var registry = LocalizationRegistry.Create([new SetCountedLocalizationFr()]);
        const decimal amount = 1234.5m;
        var total = GeneratedCode.CreateSetText<ISetCountedLocalization, decimal>("Set.Total", static (set, value) => set.Total(value), new SetCountedLocalizationEn(), amount);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => total.ToString(CultureInfo.GetCultureInfo("fr-FR"), registry));
        CultureInfo.CurrentCulture.Name.ShouldBe("en-GB");
        CultureInfo.CurrentUICulture.Name.ShouldBe("en-US");
    }

    [Test]
    public void Create_WithArguments_ShouldThrowForNullArguments()
    {
        // Arrange & Act & Assert: null alone. A blank key or a set of another contract is checked in a Debug build only,
        // because a text that takes values is created on every call.
        Should.Throw<ArgumentNullException>(() => GeneratedCode.CreateSetText<ISetCountedLocalization, int>(null!, static (set, value) => set.Items(value), new SetCountedLocalizationEn(), 1));
        Should.Throw<ArgumentNullException>(() => GeneratedCode.CreateSetText<ISetCountedLocalization, int>("K", null!, new SetCountedLocalizationEn(), 1));
        Should.Throw<ArgumentNullException>(() => GeneratedCode.CreateSetText<ISetCountedLocalization, int>("K", static (set, value) => set.Items(value), null!, 1))
            .ParamName.ShouldBe("defaultSet");
    }

    [Test]
    public void Arguments_ShouldBeEmptyForTextThatTakesNone()
    {
        // Arrange & Act & Assert: empty, and not without a value, so that it can be read without a check.
        Greeting.Arguments.ShouldBeEmpty();
        LocalizableText.Keyed("MyApp.Greeting", "Hello").Arguments.ShouldBeEmpty();
        Greeting.WithTranslation("fr", "Bonjour").Arguments.ShouldBeEmpty();
    }

    [Test]
    public void Equality_OfTextsThatTakeValues_ShouldCompareTheWordingTheValuesGive()
    {
        // Arrange: an American user, for whom the values are formatted too.
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-US");

        static LocalizableText Items(int count)
        {
            return GeneratedCode.CreateSetText<ISetCountedLocalization, int>("Set.Items", static (set, value) => set.Items(value), new SetCountedLocalizationEn(), count);
        }

        static LocalizableText Total(decimal amount)
        {
            return GeneratedCode.CreateSetText<ISetCountedLocalization, decimal>("Set.Total", static (set, value) => set.Total(value), new SetCountedLocalizationEn(), amount);
        }

        // Act & Assert: equal values read alike, and so do other values that the wording shows alike.
        Items(3).ShouldBe(Items(3));
        Items(3).GetHashCode().ShouldBe(Items(3).GetHashCode());
        Items(3).ShouldNotBe(Items(4));
        Items(3).ShouldBe(LocalizableText.FromGeneratedCode("3 items", ("en", "3 items"), ("nl", "3 artikelen")));
        Total(1.5m).ShouldBe(Total(1.499m));
        Total(1.5m).ShouldNotBe(Total(1.6m));
    }

    [Test]
    public void Create_WithEightValues_ShouldKeepThemInOrderAndWordTextWithThem()
    {
        // Arrange & Act: the most values a text takes, which a tuple holds as seven and a tuple of the rest.
        static LocalizableText Create(int last)
        {
            return GeneratedCode.CreateSetText<ISetCountedLocalization, int, int, int, int, int, int, int, int>("Set.Items",
                static (set, a, b, c, d, e, f, g, h) => set.Items(a + b + c + d + e + f + g + h), new SetCountedLocalizationEn(), 1, 2, 3, 4, 5, 6, 7, last);
        }

        var text = Create(last: 8);

        // Assert
        text.DefaultText.ShouldBe("36 items");
        text.Arguments.ShouldBe([1, 2, 3, 4, 5, 6, 7, 8]);
        text.ShouldBe(Create(last: 8));
        text.GetHashCode().ShouldBe(Create(last: 8).GetHashCode());
        text.ShouldNotBe(Create(last: 9));
    }

    [Test]
    public void EqualityOperator_WithString_ShouldCompareWordingOfTextWithKeyToo()
    {
        // Arrange: a text has a == that takes two texts too, which must not catch a string.
        var text = LocalizableText.Keyed("Key", "Name").WithTranslation("nl", "Naam");
        var uiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("nl-NL");

        try
        {
            // Act & Assert
            (text == "Naam").ShouldBeTrue();
            ("Naam" == text).ShouldBeTrue();
            (text != "Naam").ShouldBeFalse();
            (text == null).ShouldBeFalse();
            (text == LocalizableText.Keyed("Key", "Name").WithTranslation("nl", "Naam")).ShouldBeTrue();
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
        var registry = LocalizationRegistry.Create([new SetRewordedLocalizationEn(), new SetLocalizationNl()]);

        // Act & Assert: in an app without a default language of its own, the default-language set serves the invariant
        // culture and an uncovered one, and a covered culture keeps its own set.
        Greeting.ToString(CultureInfo.InvariantCulture, registry, appLanguages: []).ShouldBe("Hi");
        Greeting.ToString(CultureInfo.GetCultureInfo("fr-FR"), registry, appLanguages: []).ShouldBe("Hi");
        Greeting.ToString(CultureInfo.GetCultureInfo("nl-NL"), registry, appLanguages: []).ShouldBe("Hallo");
    }

    [Test]
    public void ToString_ShouldFallBackToSetOfAppDefaultLanguageBeforeItsOwn()
    {
        // Arrange: a contract whose default language is English, with an English set rewording a text, in an app whose
        // default language is Dutch.
        var registry = LocalizationRegistry.Create([new SetRewordedLocalizationEn(), new SetLocalizationNl()]);
        var french = CultureInfo.GetCultureInfo("fr-FR");

        // Act & Assert: an uncovered culture gets the app's language, also through a regional app language (nl-BE
        // through nl); a covered culture keeps its own set; and an app language without a set leaves the text's own
        // default language.
        Greeting.ToString(french, registry, appLanguages: ["nl"]).ShouldBe("Hallo");
        Greeting.ToString(CultureInfo.InvariantCulture, registry, appLanguages: ["nl"]).ShouldBe("Hallo");
        Greeting.ToString(french, registry, appLanguages: ["nl-BE"]).ShouldBe("Hallo");
        Greeting.ToString(CultureInfo.GetCultureInfo("en-GB"), registry, appLanguages: ["nl", "en"]).ShouldBe("Hi");
        Greeting.ToString(french, registry, appLanguages: ["de"]).ShouldBe("Hi");
    }

    [Test]
    public void ToString_ForCultureAppDoesNotDeclare_ShouldShowSetOfAppDefaultLanguageThoughSetExists()
    {
        // Arrange: a library's contract with English and Dutch sets, in an app declaring ("en", "fr").
        var registry = LocalizationRegistry.Create([new SetRewordedLocalizationEn(), new SetLocalizationNl()]);

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
        var registry = LocalizationRegistry.Create([new SetLocalizationEn(), new SetBritishLocalization(), new SetRewordedLocalizationEn()]);
        var rewordingFirst = LocalizationRegistry.Create([new SetLocalizationEn(), new SetRewordedLocalizationEn(), new SetBritishLocalization()]);
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
        var registry = LocalizationRegistry.Create([new SetLocalizationEn(), new SetRewordedLocalizationEn(), new SetSecondRewordedLocalizationEn()]);
        var withBritish = LocalizationRegistry.Create([
            new SetLocalizationEn(),
            new SetBritishLocalization(),
            new SetRewordedLocalizationEn(),
            new SetSecondRewordedLocalizationEn(),
        ]);
        var standaloneLast = LocalizationRegistry.Create([new SetLocalizationEn(), new SetRewordedLocalizationEn(), new SetStandaloneEnglishLocalization()]);
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
    public void ToString_ForRecordedRegion_ShouldKeepWordingItsClassWordsThoughItMatchesItsLanguage()
    {
        // Arrange: recorded as the build records them, so the registry knows which texts each set words itself.
        LocalizationRegistry.RecordSet<RecordedRewordedLocalizationEn>(null, ["Greeting"]);
        LocalizationRegistry.RecordSet<RecordedBritishLocalization>(null, ["Farewell", "Greeting"]);
        var registry = LocalizationRegistry.Create([new RecordedLocalizationEn(), new RecordedBritishLocalization(), new RecordedRewordedLocalizationEn()]);

        // Act & Assert: the British set words the greeting itself, so the English rewording doesn't reach it, though
        // its wording matches the English one. Compared by wording, it would show "Hi".
        Greeting.ToString(CultureInfo.GetCultureInfo("en-GB"), registry, appLanguages: ["en", "nl"]).ShouldBe("Hello");
        Farewell.ToString(CultureInfo.GetCultureInfo("en-GB"), registry, appLanguages: ["en", "nl"]).ShouldBe("Cheerio");
        Greeting.ToString(CultureInfo.GetCultureInfo("en-US"), registry, appLanguages: ["en", "nl"]).ShouldBe("Hi");
    }

    [Test]
    public void ToString_ForRecordedRewording_ShouldNotReadTextItLeavesAsItWas()
    {
        // Arrange
        LocalizationRegistry.RecordSet<RecordedRewordedLocalizationEn>(null, ["Greeting"]);
        var english = new RecordedLocalizationEn();
        var rewording = new RecordedRewordedLocalizationEn();
        var registry = LocalizationRegistry.Create([english, rewording]);

        // Act
        var farewell = Farewell.ToString(CultureInfo.GetCultureInfo("en-US"), registry, appLanguages: ["en"]);

        // Assert: the record says the rewording leaves the farewell as it was, so only the English set is read.
        farewell.ShouldBe("Goodbye");
        rewording.FarewellReads.ShouldBe(0);
        english.FarewellReads.ShouldBe(1);
    }

    [Test]
    public void ToString_ForRegion_ShouldKeepRegionalSetDerivingFromNoSetOfItsLanguage()
    {
        // Arrange
        var registry = LocalizationRegistry.Create([new SetLocalizationEn(), new SetStandaloneBritishLocalization(), new SetRewordedLocalizationEn()]);

        // Act & Assert: the British set inherits nothing from English, so the English rewording does not apply to it,
        // even where its wording matches the English.
        Greeting.ToString(CultureInfo.GetCultureInfo("en-GB"), registry, appLanguages: ["en"]).ShouldBe("Hello");
    }

    [Test]
    public void ToString_ForRegion_ShouldPassRewordingThroughEveryLevel()
    {
        // Arrange: walked by name, through the app's default language, so the levels don't depend on the parent
        // cultures of this machine.
        var registry = LocalizationRegistry.Create([
            new SetChineseLocalization(),
            new SetTraditionalChineseLocalization(),
            new SetTaiwaneseLocalization(),
            new SetRewordedChineseLocalization(),
        ]);
        var french = CultureInfo.GetCultureInfo("fr-FR");

        // Act & Assert: the Taiwanese set keeps the traditional wording, which keeps the Chinese one, so the rewording
        // of Chinese reaches Taiwan; the traditional set's own wording stays.
        Greeting.ToString(french, registry, appLanguages: ["zh-Hant-TW"]).ShouldBe("Nin hao");
        Farewell.ToString(french, registry, appLanguages: ["zh-Hant-TW"]).ShouldBe("Zaijian (Hant)");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void ToString_ShouldFindWordingInSetsOfWholeApp()
    {
        // Arrange: a contract no other test uses, so the sets of the whole app hold only this test's set for it.
        var text = LocalizableText.Create<ISetRegisteredLocalization>("Set.Greeting", t => t.Greeting, new SetRegisteredLocalizationEn());
        var uiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("nl-NL");

        try
        {
            // Act: unregistered, then registered.
            var before = text.ToString();
            LocalizationRegistry.Register(new SetRegisteredLocalizationNl());
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
    [NotInParallel(nameof(LocalizationRegistry))]
    public void ToString_WithCurrentUICultureProvider_ShouldFollowItsCultureInPlaceOfCurrentUICulture()
    {
        // Arrange: the thread is American, and the app keeps a Dutch user's culture somewhere else.
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-US");
        var text = LocalizableText.FromGeneratedCode("Name", ("en", "Name"), ("nl", "Naam"));
        string wording;

        // Act
        using (new ProvidedCultureScope("nl-NL"))
        {
            wording = text;

            // Assert: every way a text shows its wording follows the provider.
            text.ToString().ShouldBe("Naam");
            (text == "Naam").ShouldBeTrue();
            text.Equals("Naam").ShouldBeTrue();
            $"{text}".ShouldBe("Naam");
        }

        wording.ShouldBe("Naam");

        // A provider that gives no culture leaves the text on the current UI culture.
        text.ToString().ShouldBe("Name");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void ToString_WithCurrentUICultureProvider_ShouldWordTextThatTakesValuesAsForUserOfItsCulture()
    {
        // Arrange: a contract no other test uses, so the sets of the whole app hold only this test's set for it.
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-US");
        LocalizationRegistry.Register(new SetProvidedLocalizationNl());
        const decimal amount = 1234.5m;
        var cart = LocalizableText.FromGeneratedCode("Cart", ("en", "Cart"), ("nl", "Winkelwagen"));

        var total = GeneratedCode.CreateSetText<ISetProvidedLocalization, decimal>("Set.ProvidedTotal", static (set, value) => set.Total(value), new SetProvidedLocalizationEn(), amount);
        var removed = GeneratedCode.CreateSetText<ISetProvidedLocalization, LocalizableText>("Set.ProvidedRemoved", static (set, value) => set.Removed(value), new SetProvidedLocalizationEn(), cart);

        // Act & Assert: the Dutch wording, with the number as a Dutch user writes it and the text among the values in
        // Dutch, and the cultures of this thread as they were.
        using (new ProvidedCultureScope("nl-NL"))
        {
            total.ToString().ShouldBe("Totaal: 1.234,50");
            removed.ToString().ShouldBe("Winkelwagen verwijderd");
            CultureInfo.CurrentCulture.Name.ShouldBe("en-US");
            CultureInfo.CurrentUICulture.Name.ShouldBe("en-US");
        }

        total.ToString().ShouldBe("Total: 1,234.50");
        removed.ToString().ShouldBe("Cart removed");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void ToString_ForGivenCulture_ShouldWinOverCurrentUICultureProvider()
    {
        // Arrange: the provider gives Dutch, and the wording is asked for other cultures.
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-US");
        var registry = LocalizationRegistry.Create([new SetProvidedLocalizationEn(), new SetProvidedLocalizationNl()]);
        var cart = LocalizableText.FromGeneratedCode("Cart", ("en", "Cart"), ("nl", "Winkelwagen"), ("de", "Warenkorb"));
        var removed = GeneratedCode.CreateSetText<ISetProvidedLocalization, LocalizableText>("Set.ProvidedRemoved", static (set, value) => set.Removed(value), new SetProvidedLocalizationEn(), cart);

        using var provided = new ProvidedCultureScope("nl-NL");

        // Act & Assert: the text among the values follows the culture asked for, also where that is the culture of
        // this thread, and not the provider.
        removed.ToString(CultureInfo.GetCultureInfo("en-GB"), registry).ShouldBe("Cart removed");
        removed.ToString(CultureInfo.GetCultureInfo("en-US"), registry).ShouldBe("Cart removed");
        removed.ToString(CultureInfo.GetCultureInfo("nl-BE"), registry).ShouldBe("Winkelwagen verwijderd");
        cart.ToString(CultureInfo.GetCultureInfo("de-DE"), registry).ShouldBe("Warenkorb");

        // The copy of a text for every language holds that text in each language too.
        var wordings = removed.GetWordingPerLanguage(registry);
        wordings["en"].ShouldBe("Cart removed");
        wordings["nl"].ShouldBe("Winkelwagen verwijderd");

        // And the provider gives its culture again once the wording is done.
        cart.ToString().ShouldBe("Winkelwagen");
    }

    [Test]
    public void GetTranslation_OfTextThatTakesValues_ShouldShowTextAmongThemInLanguageOfSet()
    {
        // Arrange
        var cart = LocalizableText.FromGeneratedCode("Cart", ("en", "Cart"), ("en-GB", "Basket"), ("nl", "Winkelwagen"));
        var removed = GeneratedCode.CreateSetText<ISetCountedLocalization, LocalizableText>("Set.Removed", static (set, value) => set.Removed(value), new SetCountedLocalizationEn(), cart);

        // Act & Assert: the Dutch set shows the text among the values in Dutch, also to a user who reads English.
        using (new CulturesScope(uiCulture: "en-US", culture: "en-US"))
            removed.GetWording(new SetCountedLocalizationNl()).ShouldBe("Winkelwagen verwijderd");

        // The English set keeps the region of a British user, so the text among the values is British.
        using (new CulturesScope(uiCulture: "en-GB", culture: "en-GB"))
            removed.GetWording(new SetCountedLocalizationEn()).ShouldBe("Basket removed");
    }

    [Test]
    public void ToString_ForFallbackLanguage_ShouldShowTextAmongValuesInThatLanguage()
    {
        // Arrange: a Dutch user, and no Dutch set of the contract.
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-US");
        var registry = LocalizationRegistry.Create([new SetCountedLocalizationEn()]);
        var dutch = CultureInfo.GetCultureInfo("nl-NL");
        var cart = LocalizableText.FromGeneratedCode("Cart", ("en", "Cart"), ("nl", "Winkelwagen"));
        var removed = GeneratedCode.CreateSetText<ISetCountedLocalization, LocalizableText>("Set.Removed", static (set, value) => set.Removed(value), new SetCountedLocalizationEn(), cart);

        // Act & Assert: English through the app's default language, and through the text's own default language where
        // the app's has no set, as the copy of the text holds it.
        removed.ToString(dutch, registry, appLanguages: ["en", "nl"]).ShouldBe("Cart removed");
        removed.ToString(dutch, registry, appLanguages: ["fr", "nl"]).ShouldBe("Cart removed");
        removed.GetDefaultWording(registry).ShouldBe("Cart removed");
    }

    [Test]
    public void ToString_ForFallbackLanguage_ShouldRunMethodOfSetInUICultureOfThatLanguage()
    {
        // Arrange: a German user of an app in English and Dutch, so the text falls back to English.
        using var _ = new CulturesScope(uiCulture: "de-DE", culture: "de-DE");
        var registry = LocalizationRegistry.Create([new SetThreadCultureLocalizationEn()]);
        var cart = LocalizableText.FromGeneratedCode("Cart", ("en", "Cart"), ("de", "Warenkorb"));
        var removed = GeneratedCode.CreateSetText<ISetThreadCultureLocalization, LocalizableText>("Set.ThreadCultureRemoved", static (set, value) => set.Removed(value), new SetThreadCultureLocalizationEn(), cart);

        // Act & Assert: the method of the set in English, the language it words, as the text among the values, and the
        // German UI culture of the thread back after.
        removed.ToString(CultureInfo.CurrentUICulture, registry, appLanguages: ["en", "nl"]).ShouldBe("Cart removed in en");
        CultureInfo.CurrentUICulture.Name.ShouldBe("de-DE");
    }

    [Test]
    public void ToString_ForFallbackLanguage_ShouldCopyExecutionContextOnlyToChangeUICulture()
    {
        // Arrange: the same text, with a text among its values, for an English user and for a German one of an app in
        // English and Dutch. The German user's text falls back to English, so both show "Cart removed". It runs the
        // method of the set in English, and the UI culture is an AsyncLocal, so setting it copies the execution context.
        var registry = LocalizationRegistry.Create([new SetCountedLocalizationEn(), new SetCountedLocalizationNl()]);
        var cart = LocalizableText.FromGeneratedCode("Cart", ("en", "Cart"), ("nl", "Winkelwagen"));
        var removed = GeneratedCode.CreateSetText<ISetCountedLocalization, LocalizableText>("Set.Removed", static (set, value) => set.Removed(value), new SetCountedLocalizationEn(), cart);
        var englishCulture = CultureInfo.GetCultureInfo("en");
        string Show() => removed.ToString(CultureInfo.CurrentUICulture, registry, appLanguages: ["en", "nl"]);

        // Act: per 10,000 texts, and per 10,000 times the UI culture changes and the context is restored, in the German
        // user's context, so with as many AsyncLocals to copy.
        long english;
        using (new CulturesScope(uiCulture: "en-US", culture: "en-US"))
            english = GeneratedSetsTests.Allocated(Show) / 10_000;

        long german;
        long changed;
        using (new CulturesScope(uiCulture: "de-DE", culture: "de-DE"))
        {
            Show().ShouldBe("Cart removed");
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
    public void DefaultText_OfTextThatTakesValues_ShouldShowTextAmongThemInDefaultLanguage()
    {
        // Arrange
        var cart = LocalizableText.FromGeneratedCode("Cart", ("en", "Cart"), ("en-GB", "Basket"), ("nl", "Winkelwagen"));
        var removed = GeneratedCode.CreateSetText<ISetCountedLocalization, LocalizableText>("Set.Removed", static (set, value) => set.Removed(value), new SetCountedLocalizationEn(), cart);

        // Act & Assert: English for a Dutch user, and British for a British one.
        using (new CulturesScope(uiCulture: "nl-NL", culture: "nl-NL"))
            removed.DefaultText.ShouldBe("Cart removed");

        using (new CulturesScope(uiCulture: "en-GB", culture: "en-GB"))
            removed.DefaultText.ShouldBe("Basket removed");
    }

    [Test]
    public void DefaultText_ShouldFollowCultureOfItsReaderNotOfTheFirst()
    {
        // Arrange: a text whose default wording shows another text, created while the user reads Dutch, as the
        // generated class creates its texts when it's first used.
        var cart = LocalizableText.FromGeneratedCode("Cart", ("en", "Cart"), ("nl", "Winkelwagen"));
        LocalizableText text;

        using (new CulturesScope(uiCulture: "nl-NL", culture: "nl-NL"))
            text = LocalizableText.Create<ISetLocalization>("Set.Cart", _ => $"{cart}", new SetLocalizationEn());

        // Act & Assert
        using (new CulturesScope(uiCulture: "en-US", culture: "en-US"))
            text.DefaultText.ShouldBe("Cart");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void DefaultText_ShouldFollowSetThatRewordsDefaultLanguage()
    {
        // Arrange: a contract no other test uses, so the sets of the whole app hold only this test's rewording of it.
        var text = LocalizableText.Create<ISetDefaultRewordedLocalization>("Set.Checkout", static set => set.Checkout, new SetDefaultRewordedLocalizationEn());
        var french = CultureInfo.GetCultureInfo("fr-FR");

        // Act: before and after the rewording registers.
        var before = text.DefaultText;
        LocalizationRegistry.Register(new SetBrandedDefaultRewordedLocalizationEn());
        var after = text.DefaultText;

        // Assert: the rewording, as a user whose language has no wording sees it and as the copy for JSON holds it.
        before.ShouldBe("Checkout");
        after.ShouldBe("Pay now");
        text.ToString(french, LocalizationRegistry.Current, appLanguages: []).ShouldBe("Pay now");
        text.GetDefaultWording(LocalizationRegistry.Current).ShouldBe("Pay now");

        // A registry of its own, without that rewording, still ends at the set the text was declared with.
        text.ToString(french, LocalizationRegistry.Create([])).ShouldBe("Checkout");
    }

    [Test]
    public void DefaultText_ShouldThrowWhereSetGivesNull()
    {
        // Arrange: sets that give null, as a value read at run time can be, for a text without values and one with.
        var text = LocalizableText.Create<ISetLocalization>("Set.Missing", static _ => null!, new SetLocalizationEn());
        var items = GeneratedCode.CreateSetText<ISetCountedLocalization, int>("Set.MissingItems", static (_, _) => null!, new SetCountedLocalizationEn(), 1);

        // Act & Assert: the default wording is the last fallback, so the text names the set that gave null rather
        // than show null.
        Should.Throw<InvalidOperationException>(() => text.DefaultText).Message.ShouldContain("'Set.Missing'");
        Should.Throw<InvalidOperationException>(() => items.DefaultText).Message.ShouldContain(nameof(SetCountedLocalizationEn));
        Should.Throw<InvalidOperationException>(() => items.ToString(CultureInfo.GetCultureInfo("fr-FR"), registry: null));
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void ToString_WithCurrentUICultureProviderGivingUICultureOfThread_ShouldFormatValuesForIt()
    {
        // Arrange: the provider gives the UI culture the thread has, and the thread formats numbers as a Dutch user
        // does.
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "nl-NL");
        const decimal amount = 1234.5m;
        var total = GeneratedCode.CreateSetText<ISetProvidedLocalization, decimal>("Set.ProvidedTotal", static (set, value) => set.Total(value), new SetProvidedLocalizationEn(), amount);

        // Act & Assert: the number as an American user writes it, as for a British user, whose culture the thread
        // doesn't have, and the cultures of this thread as they were.
        using (new ProvidedCultureScope("en-US"))
            total.ToString().ShouldBe("Total: 1,234.50");

        using (new ProvidedCultureScope("en-GB"))
            total.ToString().ShouldBe("Total: 1,234.50");

        CultureInfo.CurrentCulture.Name.ShouldBe("nl-NL");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void ToString_WithCurrentUICultureProvider_ShouldAskItOncePerText()
    {
        // Arrange: the provider gives the UI culture the thread has, where a text that takes values asked it twice.
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-US");
        var cart = LocalizableText.FromGeneratedCode("Cart", ("en", "Cart"), ("nl", "Winkelwagen"));
        var total = GeneratedCode.CreateSetText<ISetProvidedLocalization, decimal>("Set.ProvidedTotal", static (set, value) => set.Total(value), new SetProvidedLocalizationEn(), 1234.5m);
        var removed = GeneratedCode.CreateSetText<ISetProvidedLocalization, LocalizableText>("Set.ProvidedRemoved", static (set, value) => set.Removed(value), new SetProvidedLocalizationEn(), cart);

        using var provided = new ProvidedCultureScope("en-US");

        // Act & Assert: once for a text, and not again for a text among its values.
        total.ToString().ShouldBe("Total: 1,234.50");
        provided.CallCount.ShouldBe(1);
        removed.ToString().ShouldBe("Cart removed");
        provided.CallCount.ShouldBe(2);
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void ToString_WithCurrentUICultureProviderGivingNone_ShouldAskItOncePerText()
    {
        // Arrange: no set of the whole app words this text in English, so it falls back step by step to its default
        // wording, where each step asked the provider again.
        using var _ = new CulturesScope(uiCulture: "en-GB", culture: "en-GB");
        var total = GeneratedCode.CreateSetText<ISetProvidedLocalization, decimal>("Set.ProvidedTotal", static (set, value) => set.Total(value), new SetProvidedLocalizationEn(), 1234.5m);

        using var provided = new ProvidedCultureScope(name: null);

        // Act
        var text = total.ToString();

        // Assert: the default wording, with the value as the thread formats it, and the provider asked once.
        text.ShouldBe("Total: 1,234.50");
        provided.CallCount.ShouldBe(1);
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void GetWordingPerLanguage_WithCurrentUICultureProvider_ShouldFormatValuesForItsCulture()
    {
        // Arrange: an American thread, and a Dutch user whose culture the provider gives.
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-US");
        var registry = LocalizationRegistry.Create([new SetProvidedLocalizationEn(), new SetProvidedLocalizationNl()]);
        var total = GeneratedCode.CreateSetText<ISetProvidedLocalization, decimal>("Set.ProvidedTotal", static (set, value) => set.Total(value), new SetProvidedLocalizationEn(), 1234.5m);

        using var provided = new ProvidedCultureScope("nl-NL");

        // Act
        var wordings = total.GetWordingPerLanguage(registry);

        // Assert: the copy of every language has the number as the Dutch user writes it. So the text the user sees
        // reads the same after it's read back.
        total.ToString(CultureInfo.GetCultureInfo("nl-NL"), registry).ShouldBe("Totaal: 1.234,50");
        wordings["nl"].ShouldBe("Totaal: 1.234,50");
        wordings["en"].ShouldBe("Total: 1.234,50");
        total.GetDefaultWording(registry).ShouldBe("Total: 1.234,50");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void ToString_WithCurrentUICultureProvider_ShouldCopyExecutionContextOnlyToChangeCultures()
    {
        // Arrange: an American thread, and a British user whose culture the provider gives. A culture is an AsyncLocal,
        // so each time one is set, the runtime copies the execution context. Setting both cultures back copied it twice
        // more, where restoring the context copies nothing.
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-US");
        var british = CultureInfo.GetCultureInfo("en-GB");
        var total = GeneratedCode.CreateSetText<ISetProvidedLocalization, decimal>("Set.ProvidedTotal", static (set, value) => set.Total(value), new SetProvidedLocalizationEn(), 1234.5m);

        using var provided = new ProvidedCultureScope("en-GB");

        // Act: per 10,000 texts, and per 10,000 times both cultures change and the context is restored, in the same
        // context, so with as many AsyncLocals to copy.
        var shown = GeneratedSetsTests.Allocated(() => total.ToString()) / 10_000;
        var changed = GeneratedSetsTests.Allocated(() =>
        {
            var context = ExecutionContext.Capture()!;
            CultureInfo.CurrentUICulture = british;
            CultureInfo.CurrentCulture = british;
            ExecutionContext.Restore(context);
            return context;
        }) / 10_000;

        // The wording itself, as the method of the set allocates it for a British user. Measured, not written down,
        // because formatting the amount allocates besides the string (32 bytes for N2 on .NET 11), and the runtime
        // decides how much.
        var set = new SetProvidedLocalizationEn();
        long worded;
        using (new CulturesScope(uiCulture: "en-GB", culture: "en-GB"))
            worded = GeneratedSetsTests.Allocated(() => set.Total(1234.5m)) / 10_000;

        // Assert: the wording besides, and the cultures of this thread as they were.
        total.ToString().ShouldBe("Total: 1,234.50");
        shown.ShouldBeLessThanOrEqualTo(changed + worded);
        CultureInfo.CurrentUICulture.Name.ShouldBe("en-US");
        CultureInfo.CurrentCulture.Name.ShouldBe("en-US");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void ToString_WithCurrentUICultureProviderWhereFlowIsSuppressed_ShouldPutCulturesBack()
    {
        // Arrange: an American thread that doesn't flow its execution context, so there's no context to restore, and a
        // German user whose culture the provider gives. The app doesn't support German, so the text falls back to
        // English, in a scope of its own.
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-US");
        var total = GeneratedCode.CreateSetText<ISetProvidedLocalization, decimal>("Set.ProvidedTotal", static (set, value) => set.Total(value), new SetProvidedLocalizationEn(), 1234.5m);

        using var provided = new ProvidedCultureScope("de-DE");
        using var flow = ExecutionContext.SuppressFlow();

        // Act
        var text = total.ToString();

        // Assert: the English wording with the number as the German user writes it, and the cultures of this thread as
        // they were.
        text.ShouldBe("Total: 1.234,50");
        CultureInfo.CurrentUICulture.Name.ShouldBe("en-US");
        CultureInfo.CurrentCulture.Name.ShouldBe("en-US");
    }

    [Test]
    public void ToString_FromGivenRegistry_ShouldLookUpTextAmongValuesThereToo()
    {
        // Arrange: a registry with the Dutch sets of both texts, where the sets of the whole app have neither.
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-US");
        var registry = LocalizationRegistry.Create([new SetCountedLocalizationNl(), new SetLocalizationNl()]);
        var removed = GeneratedCode.CreateSetText<ISetCountedLocalization, LocalizableText>("Set.Removed", static (set, value) => set.Removed(value), new SetCountedLocalizationEn(), Greeting);

        // Act & Assert: the text among the values from that registry too, and from no sets where none is given.
        removed.ToString(CultureInfo.GetCultureInfo("nl-NL"), registry).ShouldBe("Hallo verwijderd");
        removed.ToString(CultureInfo.GetCultureInfo("nl-NL"), registry: null).ShouldBe("Hello removed");
    }

    [Test]
    public void ToString_ForCultureThreadCannotTake_ShouldLeaveTextsOnCultureOfUser()
    {
        // Arrange: a Dutch user, and a culture whose name the UI culture of a thread can't take.
        using var _ = new CulturesScope(uiCulture: "nl-NL", culture: "nl-NL");
        var registry = LocalizationRegistry.Create([new SetCountedLocalizationEn(), new SetCountedLocalizationNl()]);
        var cart = LocalizableText.FromGeneratedCode("Cart", ("en", "Cart"), ("nl", "Winkelwagen"));
        var total = GeneratedCode.CreateSetText<ISetCountedLocalization, decimal>("Set.Total", static (set, value) => set.Total(value), new SetCountedLocalizationEn(), 1234.5m);

        // Act & Assert: the thread refuses the culture, and a text after that still follows the user.
        Should.Throw<ArgumentException>(() => total.ToString(new UnnamableCulture(), registry));
        cart.ToString().ShouldBe("Winkelwagen");
        CultureInfo.CurrentUICulture.Name.ShouldBe("nl-NL");
        CultureInfo.CurrentCulture.Name.ShouldBe("nl-NL");
    }

    [Test]
    public void MetadataName_ShouldMatchSourceGeneratorReferences()
    {
        // Assert: the class the source generator writes declares its texts as this type by name, so a rename or
        // namespace change must update the generator's emission in the same commit.
        typeof(LocalizableText).FullName.ShouldBe("CodeChops.Idioma.LocalizableText");
    }

    [Test]
    [Arguments("en", "inner")] // The culture the thread has already: the scope changes no culture, and captures no context.
    [Arguments("de", "outer")] // Another culture, which falls back to English: the scope changes the UI culture.
    public void ToString_WhenMethodOfSetChangesAnAsyncLocal_ShouldEndThatChangeOnlyWithAScopeThatChangedACulture(string cultureName, string expectedFlag)
    {
        // Arrange
        var previousUICulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        SetFlagLocalizationEn.Flag.Value = "outer";

        try
        {
            var registry = LocalizationRegistry.Create([new SetFlagLocalizationEn()]);
            var text = GeneratedCode.CreateSetText<ISetFlagLocalization, int>("Set.Flagged", static (set, value) => set.Flagged(value), new SetFlagLocalizationEn(), 1);

            // Act
            var shown = text.ToString(CultureInfo.GetCultureInfo(cultureName), registry);

            // Assert: restoring the context that a change of culture captured undoes the change of the set's method too.
            shown.ShouldBe("Flagged 1");
            SetFlagLocalizationEn.Flag.Value.ShouldBe(expectedFlag);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousUICulture;
        }
    }

    private interface ISetFlagLocalization : ILocalizationSet
    {
        string Flagged(int value);
    }

    /// <summary>
    /// A contract whose texts are properties that show another text: an inline one, and one that takes a value.
    /// </summary>
    private interface ISetShowingLocalization : ILocalizationSet
    {
        string Hint { get; }
        string Summary { get; }
    }

    private static LocalizableText ShownCart { get; } = LocalizableText.FromGeneratedCode("Cart", ("en", "Cart"), ("en-GB", "Basket"), ("nl", "Winkelwagen"));

    private static LocalizableText ShownTotal { get; } = GeneratedCode.CreateSetText<ISetProvidedLocalization, decimal>("Set.ShownTotal", static (set, value) => set.Total(value), new SetProvidedLocalizationEn(), 1234.5m);

    private static LocalizableText ShownCulture { get; } = GeneratedCode.CreateSetText<ISetThreadCultureLocalization, LocalizableText>("Set.ShownCulture", static (set, value) => set.Removed(value), new SetThreadCultureLocalizationEn(), ShownCart);

    private sealed class SetShowingLocalizationEn : ISetShowingLocalization
    {
        public string Hint => $"Open the {ShownCart} to pay";
        public string Summary => $"Summary: {ShownTotal}";
    }

    private sealed class SetShowingLocalizationNl : ISetShowingLocalization
    {
        public string Hint => $"Open de {ShownCart} om te betalen";
        public string Summary => $"Overzicht: {ShownTotal}";
    }

    /// <summary>
    /// A set whose name gives no language, so its LocalizationLanguageCode throws.
    /// </summary>
    private sealed class SetShowingLocalization1 : ISetShowingLocalization
    {
        public string Hint => $"Open {ShownCart}";
        public string Summary => "Summary";
    }

    /// <summary>
    /// A set whose property shows a text that says which UI culture its method runs in.
    /// </summary>
    private sealed class SetShowingCultureLocalizationEn : ISetShowingLocalization
    {
        public string Hint => "Hint";
        public string Summary => $"{ShownCulture}";
    }

    private static LocalizableText CreateHint() => GeneratedCode.CreateSetText<ISetShowingLocalization>("Set.Hint", static set => set.Hint, new SetShowingLocalizationEn());

    private static LocalizableText CreateSummary() => GeneratedCode.CreateSetText<ISetShowingLocalization>("Set.Summary", static set => set.Summary, new SetShowingLocalizationEn());

    [Test]
    public void ToString_ForGivenCulture_ShouldShowTextOfPropertyForThatCulture()
    {
        // Arrange: an English user, and a text whose property shows another text.
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-US");
        var registry = LocalizationRegistry.Create([new SetShowingLocalizationEn(), new SetShowingLocalizationNl()]);
        var hint = CreateHint();

        // Act & Assert: the text the property shows follows the culture asked for, not the user's.
        hint.ToString(CultureInfo.GetCultureInfo("nl-NL"), registry).ShouldBe("Open de Winkelwagen om te betalen");
        hint.ToString(CultureInfo.GetCultureInfo("en-GB"), registry).ShouldBe("Open the Basket to pay");
        hint.ToString(CultureInfo.GetCultureInfo("en-US"), registry).ShouldBe("Open the Cart to pay");

        // And the user's culture again once the wording is done.
        ShownCart.ToString().ShouldBe("Cart");
        CultureInfo.CurrentUICulture.Name.ShouldBe("en-US");
    }

    [Test]
    public void GetWordingPerLanguage_OfTextOfProperty_ShouldShowItsTextInEachLanguage()
    {
        // Arrange: a Dutch user writes the copy of every language.
        using var _ = new CulturesScope(uiCulture: "nl-NL", culture: "nl-NL");
        var registry = LocalizationRegistry.Create([new SetShowingLocalizationEn(), new SetShowingLocalizationNl()]);
        var hint = CreateHint();

        // Act
        var wordings = hint.GetWordingPerLanguage(registry);

        // Assert: each language holds the text the property shows in that language, not in Dutch.
        wordings["en"].ShouldBe("Open the Cart to pay");
        wordings["nl"].ShouldBe("Open de Winkelwagen om te betalen");
        hint.GetDefaultWording(registry).ShouldBe("Open the Cart to pay");
    }

    [Test]
    public void DefaultText_OfTextOfProperty_ShouldShowItsTextInDefaultLanguage()
    {
        // Arrange
        var hint = CreateHint();

        // Act & Assert: English for a Dutch user, and the region of a British one, whom English serves.
        using (new CulturesScope(uiCulture: "nl-NL", culture: "nl-NL"))
            hint.DefaultText.ShouldBe("Open the Cart to pay");

        using (new CulturesScope(uiCulture: "en-GB", culture: "en-GB"))
            hint.DefaultText.ShouldBe("Open the Basket to pay");
    }

    [Test]
    public void GetTranslation_OfTextOfProperty_ShouldShowItsTextInLanguageOfSet()
    {
        // Arrange
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-US");
        var hint = CreateHint();

        // Act & Assert: the Dutch set shows the text of its property in Dutch, also to a user who reads English.
        hint.GetWording(new SetShowingLocalizationNl()).ShouldBe("Open de Winkelwagen om te betalen");

        // A set without a language shows it in the user's language, and doesn't throw as for a text that takes values.
        hint.GetWording(new SetShowingLocalization1()).ShouldBe("Open Cart");
    }

    [Test]
    public void ToString_ForGivenCulture_ShouldRunMethodOfTextThatPropertyShowsInThatCulture()
    {
        // Arrange: an English user, and a text whose property shows a text that takes a value.
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-US");
        var dutch = CultureInfo.GetCultureInfo("nl-NL");
        var registry = LocalizationRegistry.Create([new SetShowingLocalizationEn(), new SetShowingLocalizationNl(), new SetProvidedLocalizationEn(), new SetProvidedLocalizationNl()]);
        var summary = CreateSummary();

        // Act & Assert: the Dutch wording of both, with the number as a Dutch reader writes it, as the text that takes
        // the value shows for that culture by itself.
        summary.ToString(dutch, registry).ShouldBe("Overzicht: Totaal: 1.234,50");
        ShownTotal.ToString(dutch, registry).ShouldBe("Totaal: 1.234,50");

        // For the user's own culture, the values keep the thread's format.
        summary.ToString(CultureInfo.GetCultureInfo("en-US"), registry).ShouldBe("Summary: Total: 1,234.50");

        // The method of the set runs in the UI culture it words, also below a property.
        var showsCulture = GeneratedCode.CreateSetText<ISetShowingLocalization>("Set.Summary", static set => set.Summary, new SetShowingCultureLocalizationEn());
        var cultureRegistry = LocalizationRegistry.Create([new SetShowingCultureLocalizationEn(), new SetThreadCultureLocalizationEn()]);
        showsCulture.ToString(CultureInfo.GetCultureInfo("en-GB"), cultureRegistry).ShouldBe("Basket removed in en-GB");

        CultureInfo.CurrentUICulture.Name.ShouldBe("en-US");
        CultureInfo.CurrentCulture.Name.ShouldBe("en-US");
    }

    [Test]
    public void ToString_ForCultureOfSameNameAsOneShownBefore_ShouldFormatValuesOfTextThatPropertyShowsForGivenCulture()
    {
        // Arrange: an English user, a text whose property shows a text that takes a value, and two Dutch cultures of
        // one name, of which the second writes a number its own way.
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-US");
        var dutch = CultureInfo.GetCultureInfo("nl-NL");
        var ownDutch = (CultureInfo)dutch.Clone();
        ownDutch.NumberFormat.NumberGroupSeparator = " ";
        var registry = LocalizationRegistry.Create([new SetShowingLocalizationEn(), new SetShowingLocalizationNl(), new SetProvidedLocalizationEn(), new SetProvidedLocalizationNl()]);
        var summary = CreateSummary();

        // Act & Assert: the registry keeps what it worked out for the first culture of the name, and each culture still
        // formats the number itself, as it does for the text that takes the value.
        summary.ToString(dutch, registry).ShouldBe("Overzicht: Totaal: 1.234,50");
        summary.ToString(ownDutch, registry).ShouldBe("Overzicht: Totaal: 1 234,50");
        ShownTotal.ToString(ownDutch, registry).ShouldBe("Totaal: 1 234,50");
    }

    [Test]
    [Arguments("nl-NL", "nl-NL")]
    [Arguments("en-US", "nl-NL")]
    public void ToString_ForCultureOfSameNameAsCultureOfThread_ShouldFormatValuesForGivenCulture(string uiCulture, string culture)
    {
        // Arrange: a thread that formats as Dutch already, and a Dutch culture of the same name that writes a number its
        // own way. The two are equal as cultures, and still format differently.
        using var _ = new CulturesScope(uiCulture: uiCulture, culture: culture);
        var ownDutch = (CultureInfo)CultureInfo.GetCultureInfo("nl-NL").Clone();
        ownDutch.NumberFormat.NumberGroupSeparator = " ";
        var registry = LocalizationRegistry.Create([new SetShowingLocalizationEn(), new SetShowingLocalizationNl(), new SetProvidedLocalizationEn(), new SetProvidedLocalizationNl()]);
        var summary = CreateSummary();

        // Act & Assert: the text that takes the value, and the text whose property shows it.
        ShownTotal.ToString(ownDutch, registry).ShouldBe("Totaal: 1 234,50");
        summary.ToString(ownDutch, registry).ShouldBe("Overzicht: Totaal: 1 234,50");
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
}
