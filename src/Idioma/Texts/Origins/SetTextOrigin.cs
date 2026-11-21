namespace CodeChops.Idioma;

/// <summary>
/// A text of a set that takes no values, such as <c>ShopLocalization.Checkout</c>. Created once per text.
/// </summary>
// Generic, so a set words the text through the selector of the generated class itself: one cast, and no delegate
// around the selector to make the cast.
internal sealed class SetTextOrigin<TContract> : TextOrigin
    where TContract : class
{
    public override Type Contract { get; } = typeof(TContract);

    public override bool IsOfSet => true;

    // The set itself, or what gives it. The generated class passes the latter, so that the set is created when a text
    // first needs it and not as the class is first used, where a constructor that throws would fail the class for good.
    public override ILocalizationSet DefaultSet => this._defaultSet ?? this.CreateDefaultSet();
    private ILocalizationSet? _defaultSet;

    public override string DefaultLanguage => this._defaultLanguage ??= this.DefaultSet.LocalizationLanguageCode;
    private string? _defaultLanguage;

    // Once per text, as such a text is created once and its name is read on every lookup in a set that derives from
    // another. Its name is its key among the texts of its class.
    public override string Key { get; }
    public override ReadOnlySpan<char> Name => this.Key;

    private Func<TContract, string> Selector { get; }
    private Func<ILocalizationSet>? CreateSet { get; }

    // A constructor body, and not the initializers of a primary constructor: those run before the base constructor, so
    // the set's LocalizationLanguageCode would run before the key is checked, and its exception would hide the argument's.
    public SetTextOrigin(string key, Func<TContract, string> selector, ILocalizationSet defaultSet)
        : this(key, selector)
    {
        ArgumentNullException.ThrowIfNull(defaultSet);

        this._defaultSet = defaultSet;
        this._defaultLanguage = defaultSet.LocalizationLanguageCode;
    }

    /// <summary>
    /// A text whose set of the default language is created when the text first needs it.
    /// </summary>
    public SetTextOrigin(string key, Func<TContract, string> selector, Func<ILocalizationSet> createDefaultSet)
        : this(key, selector)
    {
        ArgumentNullException.ThrowIfNull(createDefaultSet);

        this.CreateSet = createDefaultSet;
    }

    private SetTextOrigin(string key, Func<TContract, string> selector)
        : base(key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(selector);

        this.Selector = selector;
        this.Key = key[(key.LastIndexOf('.') + 1)..];
    }

    public override string Select(ILocalizationSet set)
    {
        ArgumentNullException.ThrowIfNull(set);

        var wording = this.Selector((TContract)set);
        return wording;
    }

    /// <summary>
    /// Creates the set of the default language, and keeps it.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// When what gives the set gives none, or one that doesn't implement <typeparamref name="TContract"/>.
    /// </exception>
    // Two threads may each ask for the set. What gives it hands both the same instance, so either may keep it.
    private ILocalizationSet CreateDefaultSet()
    {
        var set = this.CreateSet!();

        if (set is not TContract)
            throw new InvalidOperationException($"{set?.GetType().ToString() ?? "No set"} doesn't implement {typeof(TContract)}, so it can't give the default wording of the text '{this.FullKey}'.");

        this._defaultSet = set;
        return set;
    }
}
