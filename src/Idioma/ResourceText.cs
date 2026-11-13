namespace CodeChops.Idioma;

/// <summary>
/// A <see cref="LocalizableText"/> with a name, its <see cref="Key"/>, such as <c>Shop.Checkout</c>. The texts of the
/// class the source generator writes are resource texts: <c>ShopTranslations.Checkout</c> is the <c>Checkout</c> text
/// of <c>IShopTranslations</c>, under the key <c>Shop.Checkout</c>.
/// </summary>
/// <remarks>
/// <para>
/// You rarely create one yourself. Declare your texts in one class per language (see <see cref="ITranslations"/>) and
/// use the texts of the generated class: each shows the wording of the set registered for the current UI culture, and
/// a culture without a set, or one your app does not declare, gets your app's default language, or else the contract's
/// default language.
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
[JsonConverter(typeof(LocalizableTextJsonConverter))]
public sealed record ResourceText : LocalizableText
{
    /// <summary>
    /// The name of this text in a resource file, such as <c>Shop.Checkout</c>. A generated text's key starts with the
    /// name of its set without the language suffix and a trailing <c>Translations</c> (<c>ShopTranslationsEN</c> gives
    /// <c>Shop.</c>, the constant <c>ShopTranslations.ResourceKeyPrefix</c>), followed by the text's name.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// The <see cref="ITranslations"/> contract this text belongs to, such as <c>IShopTranslations</c>: every
    /// registered set implementing it can phrase the text. <see langword="null"/> for a text declared by key alone.
    /// </summary>
    public Type? Contract { get; private init; }

    /// <summary>
    /// The language of the set this text's <see cref="LocalizableText.DefaultText"/> comes from, such as <c>en</c> for
    /// a text of <c>ShopTranslationsEN</c>. <see langword="null"/> for a text declared by key alone, or with its
    /// default wording written out.
    /// </summary>
    /// <remarks>
    /// A culture without a set of its own, or one your app does not declare, gets the set registered for your app's
    /// default language (<see cref="TranslationsRegistry.DefaultLanguage"/>), else the set registered for this language.
    /// So a set that rewords this language's texts also serves those cultures.
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
    /// Declares a text of a translations contract, with its default wording taken from a set:
    /// <c>ResourceText.Create&lt;IShopTranslations&gt;("Shop.Checkout", t => t.Checkout, new ShopTranslationsEN())</c>.
    /// The class the source generator writes declares its texts this way: use that class instead. In an assembly that
    /// declares languages, calling this by hand is a build error (<c>LOC012</c>).
    /// </summary>
    /// <typeparam name="TTranslations">
    /// The contract the text belongs to; every registered set implementing it provides the text. Name it: inferred from
    /// <paramref name="defaults"/> it would be that set's own type, and no other language's set would match.
    /// </typeparam>
    /// <param name="key">The name of this text in a resource file.</param>
    /// <param name="selector">Picks the text from a set.</param>
    /// <param name="defaults">The set that supplies the default wording, typically the contract's English set.</param>
    /// <exception cref="ArgumentException">
    /// When <paramref name="key"/> is empty or white-space; pass the name the text has in the resource file.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// When <paramref name="defaults"/> has no language code: its name does not end in one and it does not implement
    /// <see cref="ITranslations.LanguageCode"/>. Name the set after its language (<c>ShopTranslationsEN</c>), or
    /// implement that property.
    /// </exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static ResourceText Create<TTranslations>(string key, Func<TTranslations, string> selector, TTranslations defaults)
        where TTranslations : class, ITranslations
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(defaults);

        return Create(key, selector(defaults), selector) with { DefaultLanguage = defaults.LanguageCode };
    }

    /// <summary>
    /// Declares a text of a translations contract with its default wording written out, for a contract without a
    /// default-language set. Declare the texts in one class per language instead (see <see cref="ITranslations"/>). In
    /// an assembly that declares languages, calling this by hand is a build error (<c>LOC012</c>).
    /// </summary>
    /// <typeparam name="TTranslations">
    /// The contract the text belongs to; every registered set implementing it provides the text.
    /// </typeparam>
    /// <param name="key">The name of this text in a resource file.</param>
    /// <param name="defaultText">The wording shown when no set provides another.</param>
    /// <param name="selector">Picks the text from a set.</param>
    /// <exception cref="ArgumentException">
    /// When <paramref name="key"/> is empty or white-space; pass the name the text has in the resource file.
    /// </exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static ResourceText Create<TTranslations>(string key, string defaultText, Func<TTranslations, string> selector)
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
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new ResourceText WithTranslation(string language, string text)
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
        // trimmed WebAssembly app) constructing any culture but the invariant one throws, and this fallback runs for
        // exactly that culture.
        return this.Contract is not null && this.DefaultLanguage is not null
            ? registry.FindWording(this.Contract, new CultureFallback(this.DefaultLanguage), this.Selector!)
            : null;
    }
}
