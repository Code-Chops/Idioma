using System.Diagnostics;

namespace CodeChops.Idioma;

// The inline-translation half of LocalizableText: translations carried by the text itself, per language. A plain
// comment: the compiler merges the XML docs of every partial declaration, and the type's summary lives in
// LocalizableText.cs.
public sealed partial class LocalizableText
{
    /// <summary>
    /// The empty, case-insensitive translations map every text starts from.
    /// </summary>
    internal static ImmutableDictionary<string, string> NoTranslations { get; } = ImmutableDictionary.Create<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The wording of this text per language, the default language included, as passed to
    /// <c>LocalizableText.Inline</c> or read back from JSON. It's empty for a text without translations of its own, such
    /// as one converted from a string or one from a <see cref="ILocalizationSet">set</see>.
    /// </summary>
    /// <remarks>
    /// Each key is a culture name in any letter case. <c>en</c> serves <c>en-US</c> and <c>en-GB</c>, and <c>en-GB</c>
    /// serves only British English. This wording wins over a registered <see cref="ILocalizationSet">set</see>.
    /// </remarks>
    // Internal: empty for every text of a set, it would read as "no wording" for most texts an app shows. A caller
    // outside asks ToString(culture) for what a user sees, or GetWording(set) for the wording of one set.
    internal ImmutableDictionary<string, string> TranslationsByLanguage
    {
        // Built when first asked for. Two threads asking at once each build one with the same content, and one stays.
        get => this._translationsByLanguage ??= ToDictionary(this._translationPairs);
        private init
        {
            this._translationsByLanguage = value;
            this._translationPairs = [.. value.Select(static pair => (pair.Key, pair.Value))];
        }
    }
    private ImmutableDictionary<string, string>? _translationsByLanguage;

    // The translations as a lookup reads them, each language once, and the map above only for who asks for it. A text
    // has a handful of languages, and scanning those is several times faster than a lookup in the map on WebAssembly
    // (measured on the interpreters of .NET 10 and .NET 11); building the map costs more than every lookup of a
    // render together, and markup creates an inline text with a value on every render. Never changed once set.
    private (string Language, string Text)[] TranslationPairs
    {
        get => this._translationPairs;
        init
        {
            this._translationPairs = value;
            this._translationsByLanguage = null;
        }
    }
    private readonly (string Language, string Text)[] _translationPairs = [];

    /// <summary>
    /// The inline texts created last, in pairs of slots. See <see cref="FindOrCreateInline"/>.
    /// </summary>
    // Matched by the identity of the strings, not by their contents. A string literal is the same object on every call,
    // so a call with the same wording finds its text by comparing references, where comparing the wording would cost
    // about as much as creating the text. Wording with a value is a new string on every call, so it never matches, and
    // its text isn't kept (see FindOrCreateInline). A fixed array keeps the cache bounded, and a miss allocates nothing
    // besides the text. A slot is replaced in one write, so a thread reads the old text or the new one, and either is
    // whole. Two slots per pair, so that two texts of a page that pick the same pair don't keep replacing each other.
    private static LocalizableText?[] InlineTexts { get; } = new LocalizableText?[1024];

    // A string of 85,000 bytes or more lies on the large object heap, where the runtime reports every string as old,
    // also one created a moment ago. That's about 42,500 characters. Wording of this length or more is never kept, so
    // that a large wording with a value, such as the body of an email, doesn't stay in memory after its text is gone.
    private const int MaxKeptWordingLength = 40_000;

    /// <summary>
    /// A text with one wording for every culture, so a member that takes a text also takes a value that has no
    /// translations: <c>Title = customer.Name</c>. A <see langword="null"/> string gives <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// In an assembly that declares two or more languages with
    /// <see cref="SupportedLanguagesAttribute">[SupportedLanguages]</see>, converting wording you write in the source
    /// is a build error (<c>LOC500</c>). <c>"Orders"</c>, <c>$"Order {number}"</c>, and
    /// <c>"Order " + number</c> all need <c>LocalizableText.Inline</c>. A string without letters, such as
    /// <c>"-"</c> or <c>"1234"</c>, still converts, and so does a value read at run time.
    /// </remarks>
    [return: NotNullIfNotNull(nameof(text))]
    public static implicit operator LocalizableText?(string? text) => text is null ? null : new LocalizableText(text);

    /// <summary>
    /// The text's own wording for a culture, or else for its parent culture (<c>en-GB</c> falls back to <c>en</c>), or
    /// <see langword="null"/> when it has none. A text of a set has none of its own.
    /// </summary>
    /// <remarks>
    /// A registered <see cref="ILocalizationSet">set</see> and the <see cref="DefaultText"/> are left out. What a user
    /// sees is <see cref="ToString(CultureInfo)"/>.
    /// </remarks>
    /// <param name="culture">The culture to find a translation for.</param>
    /// <exception cref="ArgumentNullException">When <paramref name="culture"/> is <see langword="null"/>.</exception>
    // Internal for the reason TranslationsByLanguage is.
    internal string? GetWording(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        if (this._translationPairs.Length is 0)
            return null;

        foreach (var name in new CultureFallback(culture))
            if (this.FindOwnTranslation(name) is { } translation)
                return translation;

        return null;
    }

    /// <summary>
    /// Creates a text with its translations. <see cref="Internals.GeneratedCode.CreateText"/> calls this for the
    /// generated <c>LocalizableText.Inline</c>.
    /// </summary>
    internal static LocalizableText FromGeneratedCode(string defaultText, params ReadOnlySpan<(string Language, string Text)> translations)
    {
        // Built in one pass rather than through WithTranslation: markup creates an inline text with a value on every
        // render, and each WithTranslation would copy the text and its translations once more.
        var pairs = new (string Language, string Text)[translations.Length];

        // A language is checked for null alone. A blank or repeated language is checked in a Debug build only, because
        // markup creates an inline text with a value on every render. The generator passes each language of
        // [SupportedLanguages] once, and the build checks those.
        for (var i = 0; i < translations.Length; i++)
        {
            var (language, translation) = translations[i];
            ArgumentNullException.ThrowIfNull(language, nameof(translations));
            ArgumentNullException.ThrowIfNull(translation, nameof(translations));
            Debug.Assert(!String.IsNullOrWhiteSpace(language) && IndexOf(pairs.AsSpan(0, i), language) < 0,
                $"The language '{language}' is blank or listed twice.");

            pairs[i] = (language, translation);
        }

        var text = new LocalizableText(defaultText) { TranslationPairs = pairs };
        return text;
    }

    /// <summary>
    /// The text <see cref="FromGeneratedCode"/> creates for this wording, created once and kept where it can be. Markup
    /// that writes <c>LocalizableText.Inline</c> calls this on every render, with the same wording each time.
    /// </summary>
    internal static LocalizableText FindOrCreateInline(string defaultText, params ReadOnlySpan<(string Language, string Text)> translations)
    {
        ArgumentNullException.ThrowIfNull(defaultText);

        // The pair of slots comes from the length and three characters of the default wording. Not a hash of the whole
        // wording, which would cost about as much as creating the text. Not RuntimeHelpers.GetHashCode either, because a
        // string with a value is new on every call, and giving it an identity hash cost about 10 ns more on .NET. Written
        // out here, as is the comparison of the default wording, because a call costs more than either in WebAssembly's
        // interpreter. With both in methods, a text with a value took about 10% longer there (measured 2026-11-19).
        var length = defaultText.Length;
        var hash = length is 0 ? 0 : (length * 31) + (defaultText[0] * 7) + (defaultText[length / 2] * 3) + defaultText[length - 1];
        var first = (hash & ((InlineTexts.Length / 2) - 1)) * 2;
        var kept = InlineTexts[first];

        if (kept is not null && ReferenceEquals(kept._defaultText, defaultText) && kept.HasTranslations(translations))
            return kept;

        var other = InlineTexts[first + 1];

        if (other is not null && ReferenceEquals(other._defaultText, defaultText) && other.HasTranslations(translations))
            return other;

        var text = FromGeneratedCode(defaultText, translations);

        // Kept only when none of its wording is a new string, which is one created since the last garbage collection
        // (generation 0). A string literal never is. Wording with a value is a new string on every call, so no later
        // call could find its text. Keeping it would hold the value in memory after the text is gone, in the slot of a
        // text that can be found again. Two kinds of wording can't be told from a literal this way:
        // - A string of MaxKeptWordingLength characters or more. The runtime never counts a string that large as new,
        //   so it isn't kept at all, and a literal that long is created again on every call.
        // - A string that outlived a collection before this call, such as wording an app builds once and holds on to.
        //   It's kept like a literal, also when it holds a value.
        // Only a miss asks this, because a kept text is found before it. Asking costs a text with a value about 1 ns more
        // on .NET than storing it did, and nothing measurable in WebAssembly, with the interpreter or with AOT (measured
        // 2026-11-19).
        if (defaultText.Length >= MaxKeptWordingLength || GC.GetGeneration(defaultText) is 0)
            return text;

        foreach (var (_, translation) in translations)
            if (translation.Length >= MaxKeptWordingLength || GC.GetGeneration(translation) is 0)
                return text;

        // An empty slot of the pair first. Else the one the wording picks, so that of three texts that share a pair, two
        // take turns and the third stays.
        var slot = kept is null ? first : other is null ? first + 1 : first + ((hash >> 9) & 1);
        InlineTexts[slot] = text;
        return text;
    }

    /// <summary>
    /// Whether this text was created from these very translations, the same strings and not only the same characters.
    /// </summary>
    private bool HasTranslations(ReadOnlySpan<(string Language, string Text)> translations)
    {
        var pairs = this._translationPairs;

        if (pairs.Length != translations.Length)
            return false;

        for (var i = 0; i < pairs.Length; i++)
            if (!ReferenceEquals(pairs[i].Language, translations[i].Language) || !ReferenceEquals(pairs[i].Text, translations[i].Text))
                return false;

        return true;
    }

    /// <summary>
    /// A copy of this text with a translation for a language, replacing an existing one for that language.
    /// </summary>
    /// <param name="language">The language of the translation, as a culture name like the keys of <see cref="TranslationsByLanguage"/>.</param>
    /// <param name="text">The wording to show for the language.</param>
    /// <exception cref="ArgumentException">
    /// When <paramref name="language"/> is empty or white-space. Pass a culture name such as <c>en</c> or <c>en-GB</c>.
    /// </exception>
    // Internal: a text gets its translations from LocalizableText.Inline, which demands every language.
    internal LocalizableText WithTranslation(string language, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        ArgumentNullException.ThrowIfNull(text);

        var index = IndexOf(this._translationPairs, language);
        (string Language, string Text)[] pairs = index < 0 ? [.. this._translationPairs, (language, text)] : [.. this._translationPairs];

        if (index >= 0)
            pairs[index] = (language, text);

        var translation = new LocalizableText(this) { TranslationPairs = pairs };
        return translation;
    }

    /// <summary>
    /// A copy of this text with these translations instead of its own, for a text read back from JSON.
    /// </summary>
    internal LocalizableText WithTranslations(ImmutableDictionary<string, string> translations)
    {
        var localizableText = new LocalizableText(this) { TranslationsByLanguage = translations };
        return localizableText;
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
    /// The text's own translation for exactly this culture name, in any letter case.
    /// </summary>
    private string? FindOwnTranslation(string language)
    {
        var index = IndexOf(this._translationPairs, language);

        var translation = index < 0
            ? null
            : this._translationPairs[index].Text;

        return translation;
    }
}
