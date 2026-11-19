namespace CodeChops.Idioma;

/// <summary>
/// Where a text's wording comes from besides its own translations: a key for a resource file, and, for a text of a
/// set, its contract, the set of its default language, and how a set words it. A text without a key has none.
/// </summary>
// Kept apart from the text, so an inline text, which markup creates on every render when it has a value, holds one
// empty field for it and no more. A text declared by key alone is an origin of this class itself.
internal class TextOrigin
{
    /// <summary>
    /// The name of the text in a resource file. See <see cref="LocalizableText.Key"/>.
    /// </summary>
    public string Key { get; }

    // Checked for null alone, because a text that takes values is created on every call, with the key the generator
    // wrote. The callers that take a key from outside check it for blank too: Keyed and the declaration of a text of a
    // set.
    public TextOrigin(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        this.Key = key;
    }

    /// <summary>
    /// The name of the text, the last part of its key: <c>Checkout</c> of <c>MyApp.ShopLocalization.Checkout</c>. The
    /// registry asks for it to find whether a set rewords the text (<c>LocalizationRegistry.RecordSet</c>).
    /// </summary>
    // Worked out on every read here, so that a text that takes values, created on every call, holds no field for it.
    public virtual ReadOnlySpan<char> Name => this.Key.AsSpan(this.Key.LastIndexOf('.') + 1);

    /// <summary>
    /// The contract that sets word the text, or <see langword="null"/> for a text declared by key alone.
    /// </summary>
    public virtual Type? Contract => null;

    /// <summary>
    /// The language of the set the text's default wording comes from, or <see langword="null"/> for a text declared by
    /// key alone. See <see cref="LocalizableText.DefaultLanguage"/>.
    /// </summary>
    public virtual string? DefaultLanguage => null;

    /// <summary>
    /// The set the text has its default wording and its default language from, or <see langword="null"/> for a text
    /// declared by key alone.
    /// </summary>
    public virtual ILocalizationSet? DefaultSet => null;

    /// <summary>
    /// The text as a set of <see cref="Contract"/> words it.
    /// </summary>
    public virtual string Select(ILocalizationSet set)
    {
        throw new InvalidOperationException($"The text '{this.Key}' belongs to no contract, so no set words it.");
    }
}
