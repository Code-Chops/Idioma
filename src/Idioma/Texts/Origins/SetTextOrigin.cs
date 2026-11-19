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

    public override ILocalizationSet DefaultSet { get; }
    public override string DefaultLanguage { get; }

    // Once per text, as such a text is created once and its name is read on every lookup in a set that derives from
    // another. Its name is its key among the texts of its class.
    public override string Key => this._name;
    public override ReadOnlySpan<char> Name => this._name;
    private readonly string _name;

    private Func<TContract, string> Selector { get; }

    // A constructor body, and not the initializers of a primary constructor: those run before the base constructor, so
    // the set's LocalizationLanguageCode would run before the key is checked, and its exception would hide the argument's.
    public SetTextOrigin(string key, Func<TContract, string> selector, ILocalizationSet defaultSet)
        : base(key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(defaultSet);

        this.Selector = selector;
        this.DefaultSet = defaultSet;
        this.DefaultLanguage = defaultSet.LocalizationLanguageCode;
        this._name = key[(key.LastIndexOf('.') + 1)..];
    }

    public override string Select(ILocalizationSet set)
    {
        ArgumentNullException.ThrowIfNull(set);

        var wording = this.Selector((TContract)set);
        return wording;
    }
}
