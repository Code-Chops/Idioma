namespace CodeChops.Idioma;

// The inline-translation half of LocalizableText: translations carried by the text itself, per language. A plain
// comment: the compiler merges the XML docs of every partial declaration, and the type's summary lives in
// LocalizableText.cs.
public partial record LocalizableText
{
    /// <summary>
    /// The empty, case-insensitive translations map every text starts from.
    /// </summary>
    internal static ImmutableDictionary<string, string> NoTranslations { get; } = ImmutableDictionary.Create<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The wording of this text per language, the default language included, as passed to
    /// <c>LocalizableText.Translated</c>. Empty for a text without translations of its own, such as one converted from
    /// a string or one from an <see cref="ITranslations"/> set.
    /// </summary>
    /// <remarks>
    /// Each key is a culture name in any letter case: <c>nl</c> serves <c>nl-NL</c> and <c>nl-BE</c>, and <c>nl-BE</c>
    /// serves only Belgian Dutch. This wording wins over a registered <see cref="ITranslations"/> set.
    /// </remarks>
    public ImmutableDictionary<string, string> Translations
    {
        // Built when first asked for. Two threads asking at once each build one with the same content, and one stays.
        get => this._translations ??= ToDictionary(this._translationPairs);
        private init
        {
            this._translations = value;
            this._translationPairs = [.. value.Select(static pair => (pair.Key, pair.Value))];
        }
    }

    // The translations as a lookup reads them, each language once, and the map above only for who asks for it. A text
    // has a handful of languages, and scanning those is several times faster than a lookup in the map on WebAssembly
    // (measured on the interpreters of .NET 10 and .NET 11); building the map costs more than every lookup of a
    // render together, and a text written inline in markup is created on every render. Never changed once set.
    private (string Language, string Text)[] TranslationPairs
    {
        get => this._translationPairs;
        init
        {
            this._translationPairs = value;
            this._translations = null;
        }
    }

    private readonly (string Language, string Text)[] _translationPairs = [];
    private ImmutableDictionary<string, string>? _translations;

    /// <summary>
    /// Creates a text with its translations, for the generated <c>LocalizableText.Translated</c>: write that
    /// instead, as it demands every language of <see cref="SupportedLanguagesAttribute">[SupportedLanguages]</see>. In
    /// an assembly that declares languages, calling this by hand is a build error (<c>LOC012</c>).
    /// </summary>
    /// <param name="defaultText">The wording shown for every culture without a translation.</param>
    /// <param name="translations">
    /// The translations, each a language (a culture name, see <see cref="Translations"/>) and its wording; a language
    /// listed twice keeps its last wording.
    /// </param>
    /// <exception cref="ArgumentException">
    /// When a language is empty or white-space; pass a culture name such as <c>nl</c> or <c>nl-BE</c>.
    /// </exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static LocalizableText FromGeneratedCode(string defaultText, params ReadOnlySpan<(string Language, string Text)> translations)
    {
        // Built in one pass rather than through WithTranslation: a text written inline in markup is created on every
        // render, and each WithTranslation would copy the text and its translations once more.
        var pairs = new (string Language, string Text)[translations.Length];
        var count = 0;

        foreach (var (language, translation) in translations)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(language, nameof(translations));
            ArgumentNullException.ThrowIfNull(translation, nameof(translations));

            var index = IndexOf(pairs.AsSpan(0, count), language);

            pairs[index < 0 ? count++ : index] = (language, translation);
        }

        return new LocalizableText(defaultText) { TranslationPairs = count == pairs.Length ? pairs : pairs[..count] };
    }

    /// <summary>
    /// A text with one wording for every culture, so a member that takes a text also takes a value that has no
    /// translations: <c>Title = customer.Name</c>. A <see langword="null"/> string gives <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// In an assembly that declares <see cref="SupportedLanguagesAttribute">[SupportedLanguages]</see>, converting
    /// wording you write in the source is a build error (<c>LOC011</c>): <c>"Orders"</c>, <c>$"Order {number}"</c>, and
    /// <c>"Order " + number</c> all need <c>LocalizableText.Translated</c>. A string without letters, such as
    /// <c>"-"</c> or <c>"1234"</c>, still converts, and so does a value read at run time.
    /// </remarks>
    [return: NotNullIfNotNull(nameof(text))]
    public static implicit operator LocalizableText?(string? text) => text is null ? null : new LocalizableText(text);

    /// <summary>
    /// A copy of this text with a translation for a language, replacing an existing one for that language. In an
    /// assembly that declares <see cref="SupportedLanguagesAttribute">[SupportedLanguages]</see>, calling this by hand
    /// is a build error (<c>LOC012</c>): write the text as <c>LocalizableText.Translated</c>.
    /// </summary>
    /// <param name="language">The language of the translation: a culture name, see <see cref="Translations"/>.</param>
    /// <param name="text">The wording to show for the language.</param>
    /// <exception cref="ArgumentException">
    /// When <paramref name="language"/> is empty or white-space; pass a culture name such as <c>nl</c> or <c>nl-BE</c>.
    /// </exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public LocalizableText WithTranslation(string language, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        ArgumentNullException.ThrowIfNull(text);

        var index = IndexOf(this._translationPairs, language);
        (string Language, string Text)[] pairs = index < 0 ? [.. this._translationPairs, (language, text)] : [.. this._translationPairs];

        if (index >= 0)
            pairs[index] = (language, text);

        return this with { TranslationPairs = pairs };
    }

    /// <summary>
    /// A copy of this text with these translations instead of its own, for a text read back from JSON.
    /// </summary>
    internal LocalizableText WithTranslations(ImmutableDictionary<string, string> translations)
    {
        return this with { Translations = translations };
    }

    /// <summary>
    /// The text's own wording for a culture, or else for its parent culture (<c>nl-BE</c> falls back to <c>nl</c>), or
    /// <see langword="null"/> when it has none. A registered <see cref="ITranslations"/> set and the
    /// <see cref="DefaultText"/> are left out: to get what a user sees, call <see cref="GetText(CultureInfo)"/>.
    /// </summary>
    /// <param name="culture">The culture to find a translation for.</param>
    public string? GetTranslation(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        if (this._translationPairs.Length is 0)
            return null;

        foreach (var name in new CultureFallback(culture))
            if (this.FindOwnTranslation(name) is { } translation)
                return translation;

        return null;
    }

    // The text's own translation for exactly this culture name, in any letter case.
    private string? FindOwnTranslation(string name)
    {
        var index = IndexOf(this._translationPairs, name);

        return index < 0 ? null : this._translationPairs[index].Text;
    }

    private static int IndexOf(ReadOnlySpan<(string Language, string Text)> pairs, string language)
    {
        for (var i = 0; i < pairs.Length; i++)
            if (String.Equals(pairs[i].Language, language, StringComparison.OrdinalIgnoreCase))
                return i;

        return -1;
    }

    private static ImmutableDictionary<string, string> ToDictionary((string Language, string Text)[] pairs)
    {
        if (pairs.Length is 0)
            return NoTranslations;

        var builder = NoTranslations.ToBuilder();
        foreach (var (language, text) in pairs)
            builder[language] = text;

        return builder.ToImmutable();
    }

    /// <summary>
    /// Whether two texts are the same text: the same type, the same default wording, and the same translations,
    /// whatever wording they show right now. Comparing two texts with <c>==</c> means the same.
    /// </summary>
    /// <remarks>
    /// A string becomes a text without translations here, so <c>text.Equals("Naam")</c> and
    /// <c>list.Contains("Naam")</c> are <see langword="false"/> for a translated text, even while it shows that
    /// wording. To compare with the wording a user sees, write <c>text == "Naam"</c>. A <see cref="ResourceText"/> also
    /// compares its key, contract, and default language.
    /// </remarks>
    public virtual bool Equals(LocalizableText? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        return this.EqualityContract == other.EqualityContract
               && String.Equals(this.DefaultText, other.DefaultText, StringComparison.Ordinal)
               && this._translationPairs.Length == other._translationPairs.Length
               && HasEveryTranslationOf(other);

        // Each language is in a text once, so with as many languages on both sides, one way round is enough.
        bool HasEveryTranslationOf(LocalizableText text)
        {
            foreach (var (language, translation) in text._translationPairs)
                if (!String.Equals(this.FindOwnTranslation(language), translation, StringComparison.Ordinal))
                    return false;

            return true;
        }
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        // The translations are left out on purpose: texts that differ only in their translations share a hash, which
        // is allowed (equal texts still hash alike), and an order-independent hash of a dictionary would cost more than
        // the collisions it avoids.
        return HashCode.Combine(this.EqualityContract, this.DefaultText);
    }
}
