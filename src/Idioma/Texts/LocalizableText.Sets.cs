namespace CodeChops.Idioma;

// The half of LocalizableText for texts of a set and texts with a key: the key, the contract whose sets word the text,
// and the values a text of a set takes. A plain comment: the compiler merges the XML docs of every partial declaration,
// and the type's summary lives in LocalizableText.cs.
public sealed partial class LocalizableText
{
    /// <summary>
    /// The key of this text among the texts of its generated class, such as <c>Checkout</c> for
    /// <c>ShopLocalization.Checkout</c>, or <see langword="null"/> for a text without a key, such as one written with
    /// <c>LocalizableText.Inline</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The key of a text of a set is its name. It's what the generated class takes to look the text up at run time:
    /// <c>ShopLocalization.GetTextByKey("Checkout")</c>. It's also the name of the text's entry in a resource file of its own
    /// class, as an <c>IStringLocalizer&lt;Shop&gt;</c> reads it. Two classes can each have a text with this key. For
    /// a key that no other text has, read <see cref="FullKey"/>.
    /// </para>
    /// <para>
    /// The key of a text on a member, written with <c>[Localized]</c>, is the member's name, such as <c>City</c>, or
    /// the type's name for a type's own text.
    /// </para>
    /// <para>
    /// A text created with <see cref="Keyed"/> has one key, so this is the key it was given, the same as its
    /// <see cref="FullKey"/>. So is the key of a text of a set that was read from JSON, which travels with its full
    /// key.
    /// </para>
    /// <para>
    /// For a text with parameters, each read gives a new string, because the text doesn't keep it.
    /// </para>
    /// </remarks>
    public string? Key => this.Origin?.Key;

    /// <summary>
    /// The key that is this text's alone, such as <c>MyApp.Localization.ShopLocalization.Checkout</c>, or
    /// <see langword="null"/> for a text without a key, such as one written with <c>LocalizableText.Inline</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The full key of a text of a set is its full name. That's the namespace of its set, the generated class, and the
    /// text's <see cref="Key"/>. <c>Checkout</c> of <c>ShopLocalizationEn</c> in the namespace
    /// <c>MyApp.Localization</c> gives <c>MyApp.Localization.ShopLocalization.Checkout</c>. So sets of one name in two
    /// namespaces have full keys of their own. A set in the global namespace gives <c>ShopLocalization.Checkout</c>.
    /// </para>
    /// <para>
    /// The full key of a text on a member, written with <c>[Localized]</c>, is the member's full name, such as
    /// <c>Shop.Domain.Address.City</c>, or the type's full name for a type's own text. A type inside another is
    /// written with dots, and a type with type parameters keeps its arity, as in <c>Shop.Domain.Page`1.Items</c>. An
    /// inherited text keeps the key of the member it's written on.
    /// </para>
    /// <para>
    /// The full key is for an app or library that looks texts up in one place itself, such as a shared resource file
    /// or a database. That place can then provide or override a text's wording under its full key, without a
    /// rebuild. This package never reads it.
    /// </para>
    /// </remarks>
    public string? FullKey => this.Origin?.FullKey;

    /// <summary>
    /// The <see cref="ILocalizationSet"/> contract this text belongs to, such as <c>IShopLocalization</c>, or
    /// <see langword="null"/> for a text that isn't a text of a set. Every registered set implementing the contract can
    /// give the text its wording.
    /// </summary>
    public Type? Contract => this.Origin?.Contract;

    /// <summary>
    /// The language of the set this text's <see cref="DefaultText"/> comes from, such as <c>en</c> for a text of
    /// <c>ShopLocalizationEn</c>, or <see langword="null"/> for a text that isn't a text of a set.
    /// </summary>
    /// <remarks>
    /// A text falls back to this language last, after your app's default language (see <see cref="LocalizableText"/>).
    /// </remarks>
    public string? DefaultLanguage => this.Origin?.DefaultLanguage;

    /// <summary>
    /// The values this text was created with, in the order of its parameters. For
    /// <c>ShopLocalization.Greeting(customer.Name)</c> that's <c>customer.Name</c>. Empty for a text that takes none,
    /// such as <c>ShopLocalization.Checkout</c> or an inline text.
    /// </summary>
    /// <remarks>
    /// Every set words the text with these values. An app or library that looks the text up in a resource file by its
    /// <see cref="FullKey"/> can fill the wording it finds there with them. Each read gives a new array. A value such as a
    /// number is boxed then, because the text keeps each value as its own type.
    /// </remarks>
    public ImmutableArray<object?> Arguments => (this.Origin as SetTextWithValuesOrigin)?.ToArguments() ?? [];

    /// <summary>
    /// The key, the contract, and how a set words this text, or <see langword="null"/> for a text without a key.
    /// </summary>
    private TextOrigin? Origin { get; init; }

    /// <summary>
    /// Whether a set words this text with values the text keeps, as for <c>ShopLocalization.Greeting(customer.Name)</c>.
    /// </summary>
    internal bool TakesValues => this.Origin is SetTextWithValuesOrigin;

    /// <summary>
    /// Whether a set words this text, with or without values. A property or method of the set then runs each time the
    /// text is worded.
    /// </summary>
    private bool IsOfSet => this.Origin?.DefaultSet is not null;

    /// <summary>
    /// A text that a resource file can provide under <paramref name="key"/>, for an app or library that looks texts up
    /// in resource files itself. It shows <paramref name="defaultText"/> until something resolves the key.
    /// </summary>
    /// <remarks>
    /// In an assembly that declares two or more languages with
    /// <see cref="SupportedLanguagesAttribute">[SupportedLanguages]</see>, a default you write as wording is a build
    /// error (<c>LOC500</c>), because the build can't see which languages a resource file holds. Pass a value instead,
    /// such as a customer's name, or declare the text in a set per language (see <see cref="ILocalizationSet"/>).
    /// </remarks>
    /// <example>
    /// <code>
    /// LocalizableText name = LocalizableText.Keyed("Customer.Name", customer.Name);
    /// </code>
    /// </example>
    /// <param name="key">
    /// The name of the text in a resource file, such as <c>Customer.Name</c>. It's the text's <see cref="Key"/> and
    /// its <see cref="FullKey"/>.
    /// </param>
    /// <param name="defaultText">The wording shown when nothing resolves the key.</param>
    /// <exception cref="ArgumentException">
    /// When <paramref name="key"/> is empty or white-space. Pass the name the text has in the resource file.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// When <paramref name="key"/> or <paramref name="defaultText"/> is <see langword="null"/>.
    /// </exception>
    public static LocalizableText Keyed(string key, string defaultText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(defaultText);

        var text = new LocalizableText(defaultText) { Origin = new TextOrigin(key) };
        return text;
    }

    /// <summary>
    /// This text in the wording of a given set, whatever the current UI culture.
    /// <c>ShopLocalization.Checkout.GetWording(new ShopLocalizationNl())</c> gives <c>"Afrekenen"</c>.
    /// </summary>
    /// <remarks>
    /// For a text that takes values, a text among them shows in the set's language too. The other values stay
    /// formatted for the current culture.
    /// </remarks>
    /// <param name="set">The set to read the text from.</param>
    /// <returns>
    /// The set's wording, or <see langword="null"/> when the set doesn't implement this text's <see cref="Contract"/>,
    /// or the text has none.
    /// </returns>
    /// <exception cref="ArgumentNullException">When <paramref name="set"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// When this text takes values and <paramref name="set"/> has no language code, because its name doesn't end in
    /// one. The build warns about such a set with <c>LOC400</c>.
    /// </exception>
    public string? GetWording(ILocalizationSet set)
    {
        ArgumentNullException.ThrowIfNull(set);

        if (this.Contract is not { } contract || !contract.IsInstanceOfType(set))
            return null;

        // A text the wording shows follows the set's language: a text among the values, or a text a property shows. It's
        // looked up in the sets of the whole app, where no text being worded gives others. A text that takes values
        // needs the language, so its set must have one. A text without values does without it where the set has none.
        var language = this.TakesValues ? set.LocalizationLanguageCode : FindLanguageCode(set);
        using var scope = this.EnterLanguageUnlessServed(language, LocalizationRegistry.Current);

        var text = this.Select(set);
        return text;
    }

    /// <summary>
    /// Declares a text of a localization contract, with its default wording taken from a set each time it's read.
    /// <see cref="Internals.GeneratedCode.CreateSetText{TContract}(string, Func{TContract, string}, ILocalizationSet)"/>
    /// calls this for the generated class.
    /// </summary>
    // Not worded here: the generated class creates its texts as it's first used, under the culture of whoever uses it
    // first, and a wording that shows another text would keep that culture.
    internal static LocalizableText Create<TContract>(string key, Func<TContract, string> selector, ILocalizationSet defaultSet)
        where TContract : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(defaultSet);
        ThrowIfNotImplementing<TContract>(defaultSet);

        var origin = new SetTextOrigin<TContract>(key, selector, defaultSet);

        var text = new LocalizableText(origin);
        return text;
    }

    /// <summary>
    /// Declares a text of a localization contract that takes values, with its default wording taken from the set of
    /// the values. The overloads of <c>GeneratedCode.CreateSetText</c> that take values call this for a method of the
    /// generated class.
    /// </summary>
    // Created on every call, where a text without values is created once, so it does no more here than keep what it
    // is given: the text itself and its values are all a call allocates. Its default wording and its default language
    // are asked of the set when they are read, which a text that finds its wording in the user's language never does.
    // Not generic, so no type test runs in generic code on every call. The origin checks that the set implements the
    // contract, in a Debug build (see there).
    internal static LocalizableText Create(SetTextWithValuesOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(origin);

        var text = new LocalizableText(origin);
        return text;
    }

    /// <summary>
    /// The name of this text, the last part of its <see cref="FullKey"/>, or empty for a text without a key.
    /// </summary>
    internal ReadOnlySpan<char> Name => this.Origin is { } origin ? origin.Name : default;

    /// <summary>
    /// This text as a set implementing <see cref="Contract"/> words it, with its <see cref="Arguments"/> when it
    /// takes values.
    /// </summary>
    internal string Select(ILocalizationSet set)
    {
        var wording = this.Origin!.Select(set);
        return wording;
    }

    /// <summary>
    /// Throws when the set of the default language is missing or doesn't implement the contract.
    /// </summary>
    // Named as the parameter of GeneratedCode.CreateSetText, which passes the set on, so its exceptions name a
    // parameter the caller has.
    private static void ThrowIfNotImplementing<TContract>(ILocalizationSet defaultSet)
        where TContract : class
    {
        ArgumentNullException.ThrowIfNull(defaultSet);

        if (defaultSet is not TContract)
            throw new ArgumentException($"{defaultSet.GetType()} doesn't implement {typeof(TContract)}, so it can't give the default wording of its texts.", nameof(defaultSet));
    }

    /// <summary>
    /// A set's language code, or <see langword="null"/> for a set that has none, because its name doesn't end in one.
    /// </summary>
    private static string? FindLanguageCode(ILocalizationSet set)
    {
        try
        {
            return set.LocalizationLanguageCode;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// The wording the registered sets give this text for the first of the names that has a set, or
    /// <see langword="null"/> when none does or the text has no <see cref="Contract"/>.
    /// </summary>
    private string? FindTranslation(LocalizationRegistry registry, LocalizationRegistry.FallbackLookup lookup)
    {
        var text = this.Contract is { } contract
            ? registry.FindWording(contract, lookup, this)
            : null;

        return text;
    }

    /// <summary>
    /// The wording the set registered for <see cref="DefaultLanguage"/> gives this text, as for any language (see
    /// <see cref="FindTranslation"/>), or <see langword="null"/> when the text has no default language or no such set
    /// is registered.
    /// </summary>
    private string? FindDefaultTranslation(LocalizationRegistry registry)
    {
        if (this.Contract is not { } contract || this.DefaultLanguage is not { } defaultLanguage)
            return null;

        // A text among the values shows in the default language too, where the user reads another. A text without
        // values enters no scope on this path, for the reason GetWording gives.
        using var scope = this.TakesValues ? this.EnterLanguageUnlessServed(defaultLanguage, registry) : default;

        // From the language code, through its culture only where this machine has one: under invariant globalization (a
        // NativeAOT console, a trimmed WebAssembly app) constructing any culture but the invariant one throws by
        // default, and this fallback runs for exactly that culture.
        var text = registry.FindWording(contract, registry.ResolveLanguage(defaultLanguage), this);
        return text;
    }
}
