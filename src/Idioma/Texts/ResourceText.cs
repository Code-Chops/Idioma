namespace CodeChops.Idioma;

/// <summary>
/// A <see cref="LocalizableText"/> with a name, its <see cref="Key"/>. The texts of the class the source generator
/// writes are resource texts: <c>ShopTranslations.Checkout</c> is the <c>Checkout</c> text of
/// <c>IShopTranslations</c>, under the key <c>MyApp.Translations.ShopTranslations.Checkout</c>, its full name.
/// </summary>
/// <remarks>
/// <para>
/// You rarely create one yourself. Declare your texts in one class per language (see <see cref="ITranslationSet"/>) and
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
/// <para>
/// A text that takes values is a method of the generated class: <c>ShopTranslations.Greeting(customer.Name)</c> is
/// the <c>Greeting</c> text with that name as its <see cref="Arguments"/>. It follows the user's language as any other
/// text does: each set words it with those values when the text is shown.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// ResourceText checkout = ShopTranslations.Checkout;
///
/// string key = checkout.Key;            // "MyApp.Translations.ShopTranslations.Checkout"
/// Type? contract = checkout.Contract;   // typeof(IShopTranslations)
/// string wording = checkout;            // "Afrekenen" for a user whose UI culture is nl-NL
///
/// ResourceText greeting = ShopTranslations.Greeting("Anna");
///
/// string name = (string)greeting.Arguments[0]!; // "Anna"
/// string hello = greeting;                      // "Hallo, Anna!" for that user
/// </code>
/// </example>
// Named here as well: the serializer reads the converter from the type it writes, and does not look at the base type.
[JsonConverter(typeof(Internals.LocalizableTextJsonConverter))]
public sealed record ResourceText : LocalizableText
{
    /// <summary>
    /// The name of this text in a resource file, such as <c>MyApp.Translations.ShopTranslations.Checkout</c>.
    /// </summary>
    /// <remarks>
    /// A generated text's key is its full name: the namespace of its set, the generated class, and the text's name.
    /// <c>Checkout</c> of <c>ShopTranslationsEN</c> in the namespace <c>MyApp.Translations</c> gives
    /// <c>MyApp.Translations.ShopTranslations.Checkout</c>, so sets of one name in two namespaces have keys of their
    /// own. A set in the global namespace gives <c>ShopTranslations.Checkout</c>.
    /// </remarks>
    public string Key { get; }

    /// <summary>
    /// The <see cref="ITranslationSet"/> contract this text belongs to, such as <c>IShopTranslations</c>: every
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
    /// The values this text was created with, in the order of its parameters: <c>customer.Name</c> for
    /// <c>ShopTranslations.Greeting(customer.Name)</c>. Empty for a text that takes none, such as
    /// <c>ShopTranslations.Checkout</c>.
    /// </summary>
    /// <remarks>
    /// Every set words the text with these values. An app or library that looks the text up in a resource file by its
    /// <see cref="Key"/> can fill the wording it finds there with them.
    /// </remarks>
    public ImmutableArray<object?> Arguments { get; private init; } = [];

    /// <summary>
    /// Selects this text from a set implementing <see cref="Contract"/>.
    /// </summary>
    private Func<ITranslationSet, string>? Selector { get; init; }

    private protected override bool TakesValues => !this.Arguments.IsEmpty;

    /// <summary>
    /// A text that a resource file can provide under <paramref name="key"/>, for an app or library that looks texts up
    /// in resource files itself. Until something resolves the key, it shows <paramref name="defaultText"/>.
    /// </summary>
    /// <remarks>
    /// In an assembly that declares two or more languages with
    /// <see cref="SupportedLanguagesAttribute">[SupportedLanguages]</see>, a default you write as wording is a build
    /// error (<c>LOC011</c>), as the build cannot see which languages a resource file holds. Pass a value instead, such as a customer's name, or declare the text in a set per language (see
    /// <see cref="ITranslationSet"/>).
    /// </remarks>
    /// <param name="key">The name of the text in a resource file, such as <c>Customer.Name</c>.</param>
    /// <param name="defaultText">The wording shown when nothing resolves the key.</param>
    /// <exception cref="ArgumentException">
    /// When <paramref name="key"/> is empty or white-space; pass the name the text has in the resource file.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// When <paramref name="key"/> or <paramref name="defaultText"/> is <see langword="null"/>.
    /// </exception>
    public ResourceText(string key, string defaultText)
        : base(defaultText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        this.Key = key;
    }

    /// <summary>
    /// Declares a text of a translations contract, with its default wording taken from a set: what
    /// <see cref="Internals.GeneratedCode.CreateResourceText{TContract}(string, Func{TContract, string}, TContract)"/>
    /// gives the class the source generator writes.
    /// </summary>
    internal static ResourceText Create<TContract>(string key, Func<TContract, string> selector, TContract defaults)
        where TContract : class, ITranslationSet
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(defaults);

        return Create(key, selector(defaults), selector) with { DefaultLanguage = defaults.LanguageCode };
    }

    /// <summary>
    /// Declares a text of a translations contract with its default wording written out.
    /// </summary>
    internal static ResourceText Create<TContract>(string key, string defaultText, Func<TContract, string> selector)
        where TContract : class, ITranslationSet
    {
        ArgumentNullException.ThrowIfNull(selector);

        var text = new ResourceText(key, defaultText)
        {
            Contract = typeof(TContract),
            Selector = set => selector((TContract)set),
        };
        return text;
    }

    /// <summary>
    /// Declares a text of a translations contract that takes values, with its default wording taken from a set: what
    /// <see cref="Internals.GeneratedCode.CreateResourceText{TContract}(string, Func{TContract, string}, TContract, ReadOnlySpan{object})"/>
    /// gives a method of the class the source generator writes.
    /// </summary>
    // Created on every call, where a text without arguments is created once, so in one step: the text is not copied
    // to give it its default language, as the Create above copies it.
    internal static ResourceText Create<TContract>(string key, Func<TContract, string> selector, TContract defaults, ReadOnlySpan<object?> arguments)
        where TContract : class, ITranslationSet
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(defaults);

        var text = new ResourceText(key, selector(defaults))
        {
            Contract = typeof(TContract),
            Selector = set => selector((TContract)set),
            DefaultLanguage = defaults.LanguageCode,
            Arguments = [.. arguments],
        };
        return text;
    }

    /// <summary>
    /// This text in the wording of a given set, whatever the current UI culture:
    /// <c>ShopTranslations.Checkout.GetTranslation(new ShopTranslationsNL())</c> gives <c>"Afrekenen"</c>.
    /// </summary>
    /// <param name="set">The set to read the text from.</param>
    /// <returns>
    /// The set's wording, or <see langword="null"/> when the set does not implement this text's <see cref="Contract"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">When <paramref name="set"/> is <see langword="null"/>.</exception>
    public string? GetTranslation(ITranslationSet set)
    {
        ArgumentNullException.ThrowIfNull(set);

        var text = this.Contract is not null && this.Contract.IsInstanceOfType(set)
            ? this.Selector!(set)
            : null;

        return text;
    }

    /// <summary>
    /// A copy of this text with a translation for a language, keeping its key and contract.
    /// </summary>
    /// <param name="language">
    /// The language of the translation: a culture name, see <see cref="LocalizableText.Translations"/>.
    /// </param>
    /// <param name="text">The wording to show for the language.</param>
    /// <exception cref="ArgumentException">
    /// When <paramref name="language"/> is empty or white-space; pass a culture name such as <c>en</c> or <c>en-GB</c>.
    /// </exception>
    internal new ResourceText WithTranslation(string language, string text)
    {
        var result = (ResourceText)base.WithTranslation(language, text);
        return result;
    }

    /// <summary>
    /// Whether two texts are the same text: the same key, contract, default language, default wording, translations,
    /// and <see cref="Arguments"/>, whatever wording they show right now.
    /// </summary>
    /// <remarks>
    /// Two arguments are the same when <see cref="object.Equals(object)"/> says so: <c>Greeting("Anna")</c> equals
    /// <c>Greeting("Anna")</c>, and a text created with an array equals only a text created with that same array.
    /// </remarks>
    public bool Equals(ResourceText? other)
    {
        // The selector is not compared: the key names the member it selects, and the arguments are what it passes.
        // The arguments are compared themselves, and not left to the default wording they give: two values can read
        // the same in the default language and differ in another.
        var isEqual = other is not null
               && base.Equals(other)
               && String.Equals(this.Key, other.Key, StringComparison.Ordinal)
               && this.Contract == other.Contract
               && String.Equals(this.DefaultLanguage, other.DefaultLanguage, StringComparison.Ordinal)
               && this.Arguments.AsSpan().SequenceEqual(other.Arguments.AsSpan());

        return isEqual;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        // Without the arguments: equal texts still hash alike, and the default wording, which is in the hash of the
        // base, differs with nearly every argument already.
        return HashCode.Combine(base.GetHashCode(), this.Key, this.Contract, this.DefaultLanguage);
    }

    /// <summary>
    /// This text as the sets registered for the first of the names that has one phrase it, or <see langword="null"/>
    /// when none is registered or the text has no <see cref="Contract"/>.
    /// </summary>
    private protected override string? FindTranslation(TranslationsRegistry registry, CultureFallback names)
    {
        var text = this.Contract is not null
            ? registry.FindWording(this.Contract, names, this.Selector!)
            : null;

        return text;
    }

    /// <summary>
    /// This text as the sets registered for <see cref="DefaultLanguage"/> phrase it, as for any language (see
    /// <see cref="FindTranslation"/>), or <see langword="null"/> when the text has no default language or no such set
    /// is registered.
    /// </summary>
    private protected override string? FindDefaultTranslation(TranslationsRegistry registry)
    {
        // From the language code, through its culture only where this machine has one: under invariant globalization (a
        // NativeAOT console, a trimmed WebAssembly app) constructing any culture but the invariant one throws by
        // default, and this fallback runs for exactly that culture.
        var text = this.Contract is not null && this.DefaultLanguage is not null
            ? registry.FindWording(this.Contract, CultureFallback.OfLanguage(this.DefaultLanguage), this.Selector!)
            : null;

        return text;
    }
}
