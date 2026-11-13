namespace CodeChops.Idioma;

/// <summary>
/// A <see cref="LocalizableText"/> with a name, its <see cref="Key"/>, such as <c>Shop.Checkout</c>. The texts of the
/// class the source generator writes are resource texts: <c>ShopTranslations.Checkout</c> is the <c>Checkout</c> text
/// of <c>IShopTranslations</c>, under the key <c>Shop.Checkout</c>.
/// </summary>
/// <remarks>
/// <para>
/// You rarely create one yourself. Declare your texts in one class per language (see <see cref="ITranslations"/>) and
/// use the texts of the generated class.
/// </para>
/// <para>
/// The key is for an app or library that looks texts up in resource files itself, such as through an
/// <c>IStringLocalizer</c>: a resource file can then provide or override a text's wording under its key, without a
/// rebuild. This package never reads the key.
/// </para>
/// <para>
/// To leave a text's wording to such a resource file, declare it by key, with a default for when nothing resolves the
/// key: <c>new ResourceText("Customer.Name", customer.Name)</c>. Outside such a host, it shows that default.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// ResourceText checkout = ShopTranslations.Checkout;
///
/// string key = checkout.Key;            // "Shop.Checkout"
/// Type? contract = checkout.Contract;   // typeof(IShopTranslations)
/// string wording = checkout;            // "Afrekenen" for a user whose UI culture is nl-NL
/// </code>
/// </example>
// Named here as well: the serializer reads the converter from the type it writes, and does not look at the base type.
[JsonConverter(typeof(Internals.LocalizableTextJsonConverter))]
public sealed record ResourceText : LocalizableText
{
    /// <summary>
    /// The name of this text in a resource file, such as <c>Shop.Checkout</c>.
    /// </summary>
    /// <remarks>
    /// A generated text's key is the name of its set without the language and a trailing <c>Translations</c>, then a
    /// dot and the text's name: <c>Checkout</c> of <c>ShopTranslationsEN</c> gives <c>Shop.Checkout</c>. The prefix
    /// <c>Shop.</c> is also the constant <c>ShopTranslations.ResourceKeyPrefix</c>.
    /// </remarks>
    public string Key { get; }

    /// <summary>
    /// The <see cref="ITranslations"/> contract this text belongs to, such as <c>IShopTranslations</c>: every
    /// registered set implementing it can give the text its wording. <see langword="null"/> for a text declared by key
    /// alone.
    /// </summary>
    public Type? Contract { get; private init; }

    /// <summary>
    /// The language of the set this text's <see cref="LocalizableText.DefaultText"/> comes from, such as <c>en</c> for
    /// a text of <c>ShopTranslationsEN</c>. <see langword="null"/> for a text declared by key alone, or with its
    /// default wording written out.
    /// </summary>
    /// <remarks>
    /// The language a text falls back to last, after your app's default language: see <see cref="LocalizableText"/>.
    /// </remarks>
    public string? DefaultLanguage { get; private init; }

    /// <summary>
    /// Selects this text from a set implementing <see cref="Contract"/>.
    /// </summary>
    private Func<ITranslations, string>? Selector { get; init; }

    /// <summary>
    /// A text that a resource file can provide under <paramref name="key"/>, for an app or library that looks texts up
    /// in resource files itself. Until something resolves the key, it shows <paramref name="defaultText"/>.
    /// </summary>
    /// <remarks>
    /// In an assembly that declares <see cref="SupportedLanguagesAttribute">[SupportedLanguages]</see>, a default you
    /// write as wording is a build error (<c>LOC011</c>), as the build cannot see which languages a resource file
    /// holds. Pass a value instead, such as a customer's name, or declare the text in a set per language (see
    /// <see cref="ITranslations"/>).
    /// </remarks>
    /// <param name="key">The name of the text in a resource file, such as <c>Customer.Name</c>.</param>
    /// <param name="defaultText">The wording shown when nothing resolves the key.</param>
    /// <exception cref="ArgumentException">
    /// When <paramref name="key"/> is empty or white-space; pass the name the text has in the resource file.
    /// </exception>
    public ResourceText(string key, string defaultText)
        : base(defaultText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        this.Key = key;
    }

    /// <summary>
    /// Declares a text of a translations contract, with its default wording taken from a set: what
    /// <see cref="Internals.GeneratedCode.CreateResourceText"/> gives the class the source generator writes.
    /// </summary>
    internal static ResourceText Create<TTranslations>(string key, Func<TTranslations, string> selector, TTranslations defaults)
        where TTranslations : class, ITranslations
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(defaults);

        return Create(key, selector(defaults), selector) with { DefaultLanguage = defaults.LanguageCode };
    }

    /// <summary>
    /// Declares a text of a translations contract with its default wording written out.
    /// </summary>
    internal static ResourceText Create<TTranslations>(string key, string defaultText, Func<TTranslations, string> selector)
        where TTranslations : class, ITranslations
    {
        ArgumentNullException.ThrowIfNull(selector);

        return new ResourceText(key, defaultText)
        {
            Contract = typeof(TTranslations),
            Selector = translations => selector((TTranslations)translations),
        };
    }

    /// <summary>
    /// This text in the wording of a given set, whatever the current UI culture:
    /// <c>ShopTranslations.Checkout.GetTranslation(new ShopTranslationsNL())</c> gives <c>"Afrekenen"</c>.
    /// </summary>
    /// <param name="translations">The set to read the text from.</param>
    /// <returns>
    /// The set's wording, or <see langword="null"/> when the set does not implement this text's <see cref="Contract"/>.
    /// </returns>
    public string? GetTranslation(ITranslations translations)
    {
        ArgumentNullException.ThrowIfNull(translations);

        return this.Contract is not null && this.Contract.IsInstanceOfType(translations)
            ? this.Selector!(translations)
            : null;
    }

    /// <summary>
    /// A copy of this text with a translation for a language, keeping its key and contract.
    /// </summary>
    /// <param name="language">
    /// The language of the translation: a culture name, see <see cref="LocalizableText.Translations"/>.
    /// </param>
    /// <param name="text">The wording to show for the language.</param>
    /// <exception cref="ArgumentException">
    /// When <paramref name="language"/> is empty or white-space; pass a culture name such as <c>nl</c> or <c>nl-BE</c>.
    /// </exception>
    internal new ResourceText WithTranslation(string language, string text)
    {
        return (ResourceText)base.WithTranslation(language, text);
    }

    /// <summary>
    /// Whether two texts are the same text: the same key, contract, default language, default wording, and
    /// translations, whatever wording they show right now.
    /// </summary>
    public bool Equals(ResourceText? other)
    {
        // The selector is not compared: the key names the member it selects.
        return other is not null
               && base.Equals(other)
               && String.Equals(this.Key, other.Key, StringComparison.Ordinal)
               && this.Contract == other.Contract
               && String.Equals(this.DefaultLanguage, other.DefaultLanguage, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), this.Key, this.Contract, this.DefaultLanguage);
    }

    /// <summary>
    /// This text as the sets registered for the first of the names that has one phrase it, or <see langword="null"/>
    /// when none is registered or the text has no <see cref="Contract"/>.
    /// </summary>
    private protected override string? FindTranslation(TranslationsRegistry registry, CultureFallback names)
    {
        return this.Contract is not null
            ? registry.FindWording(this.Contract, names, this.Selector!)
            : null;
    }

    /// <summary>
    /// This text as the sets registered for <see cref="DefaultLanguage"/> phrase it, as for any language (see
    /// <see cref="FindTranslation"/>), or <see langword="null"/> when the text has no default language or no such set
    /// is registered.
    /// </summary>
    private protected override string? FindDefaultTranslation(TranslationsRegistry registry)
    {
        // Walked by language code, not through a CultureInfo: under invariant globalization (a NativeAOT console, a
        // trimmed WebAssembly app) constructing any culture but the invariant one throws by default, and this fallback
        // runs for exactly that culture.
        return this.Contract is not null && this.DefaultLanguage is not null
            ? registry.FindWording(this.Contract, new CultureFallback(this.DefaultLanguage), this.Selector!)
            : null;
    }
}
