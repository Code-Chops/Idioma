namespace CodeChops.Idioma.Internals;

/// <summary>
/// <para>
/// You never need to use this type directly, because <see cref="LocalizableText"/> names it in its
/// <see cref="JsonConverterAttribute"/>.
/// </para>
/// <para>
/// Writes a <see cref="LocalizableText"/> as JSON and reads it back. This lets a text survive JSON, such as a Blazor
/// component parameter that crosses a render mode.
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// A text is written with every wording it can show:
/// </para>
/// <list type="bullet">
/// <item>Its default wording.</item>
/// <item>Its own translations.</item>
/// <item>For a text of an <see cref="ILocalizationSet"/> contract, the wording of each language your app declares. Also
/// that of every language a set is registered for that starts with the same language as one of them, such as
/// <c>en-GB</c> or <c>zh-TW</c> for an app that declares <c>en</c> or <c>zh-Hant</c>. In an app that declares no
/// languages, that's every language a set is registered for. Each is the wording a user of that language sees when
/// the text is written.</item>
/// </list>
/// <para>
/// A text with a key keeps its <see cref="LocalizableText.Key"/>:
/// <c>{"key":"MyApp.Localization.ShopLocalization.Checkout","defaultText":"Checkout",
/// "translations":{"en":"Checkout","nl":"Afrekenen"}}</c>.
/// A text that takes values leaves its key out. It's read back without its <see cref="LocalizableText.Arguments"/>,
/// so a resource file entry found by the key would have no values to fill in.
/// A text with one wording for every culture and no key, such as a customer's name, is written as that wording:
/// <c>"Anna"</c>. A text without translations leaves <c>translations</c> out.
/// </para>
/// <para>
/// Read back, the text shows that wording for every culture, even after a switch of language, without the sets. It
/// belongs to no contract anymore, so a set registered later doesn't reword it.
/// </para>
/// <para>
/// A JSON string reads as a text with that one wording for every culture, as a <see cref="string"/> converts to a
/// text. So JSON written by hand can hold <c>"title": "Orders"</c>. A <c>key</c> or <c>translations</c> that is
/// <see langword="null"/> reads as left out.
/// </para>
/// </remarks>
// Public, because the System.Text.Json source generator creates the converter a type names from the code it writes
// into the consumer's assembly.
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class LocalizableTextJsonConverter : JsonConverter<LocalizableText>
{
    private const string KeyName = "key";
    private const string DefaultTextName = "defaultText";
    private const string TranslationsName = "translations";
    private const string EndsEarlyMessage = "The JSON ends before the text does.";

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override LocalizableText Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return Read(ref reader);
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override void Write(Utf8JsonWriter writer, LocalizableText value, JsonSerializerOptions options)
    {
        Write(writer, value, LocalizationRegistry.Current);
    }

    /// <summary>
    /// Writes a text, with the wording the sets of a registry give it.
    /// </summary>
    internal static void Write(Utf8JsonWriter writer, LocalizableText text, LocalizationRegistry registry)
    {
        var wordingPerLanguage = text.GetWordingPerLanguage(registry);

        // A text that takes values is written with them filled in, and reads back without them. A resource file entry
        // found by its key would then show its placeholders as written, so the text travels without a key.
        var key = text.TakesValues ? null : text.Key;

        // A text with one wording and no key is that wording, as it reads back from a string: a customer's name
        // travels as the name alone. The wording the registry gives it, as in the object below: a set it registers may
        // reword the default language of a text that takes values.
        if (key is null && wordingPerLanguage.IsEmpty)
        {
            writer.WriteStringValue(text.GetDefaultWording(registry));
            return;
        }

        writer.WriteStartObject();

        if (key is not null)
            writer.WriteString(KeyName, key);

        writer.WriteString(DefaultTextName, text.GetDefaultWording(registry));

        if (!wordingPerLanguage.IsEmpty)
        {
            // In a fixed order and letter case, so that the same text always gives the same JSON, whichever set
            // registered first and however it spells its language. A key that is no culture name (a text can carry
            // any) stays as is. Sorted in an array of its own rather than through LINQ: a page writes many texts.
            var wordings = new KeyValuePair<string, string>[wordingPerLanguage.Count];
            var count = 0;

            foreach (var (language, wording) in wordingPerLanguage)
                wordings[count++] = new(CultureName.IsValid(language) ? CultureName.Normalize(language) : language, wording);

            Array.Sort(wordings, static (left, right) => String.CompareOrdinal(left.Key, right.Key));

            writer.WriteStartObject(TranslationsName);

            foreach (var (language, wording) in wordings)
                writer.WriteString(language, wording);

            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// Reads a text with its key when the JSON holds one.
    /// </summary>
    private static LocalizableText Read(ref Utf8JsonReader reader)
    {
        // A string is a text without translations, as it is everywhere else.
        if (reader.TokenType is JsonTokenType.String)
            return new LocalizableText(reader.GetString()!);

        if (reader.TokenType is not JsonTokenType.StartObject)
            throw new JsonException($"A text is a JSON string, or a JSON object with a \"{DefaultTextName}\", not {reader.TokenType}.");

        string? key = null;
        string? defaultText = null;
        var translations = LocalizableText.NoTranslations;

        while (ReadToken(ref reader) is not JsonTokenType.EndObject)
        {
            var name = reader.GetString();
            ReadToken(ref reader);

            // A key or translations written as null are the same as left out, as a writer that is not this converter
            // writes a text without them. The default text is what makes it a text, so that one must be there.
            if (String.Equals(name, KeyName, StringComparison.OrdinalIgnoreCase))
                key = reader.TokenType is JsonTokenType.Null ? null : ReadString(ref reader, KeyName);
            else if (String.Equals(name, DefaultTextName, StringComparison.OrdinalIgnoreCase))
                defaultText = ReadString(ref reader, DefaultTextName);
            else if (String.Equals(name, TranslationsName, StringComparison.OrdinalIgnoreCase))
                translations = reader.TokenType is JsonTokenType.Null ? LocalizableText.NoTranslations : ReadTranslations(ref reader);
            else if (!reader.TrySkip()) // Not Skip: it throws while more JSON is still to come, as from a stream; this text is all read.
                throw new JsonException(EndsEarlyMessage);
        }

        if (defaultText is null)
            throw new JsonException($"A text needs its \"{DefaultTextName}\".");

        if (key is not null && String.IsNullOrWhiteSpace(key))
            throw new JsonException($"The \"{KeyName}\" of a text can't be empty or white-space.");

        var text = key is null
            ? new LocalizableText(defaultText)
            : LocalizableText.Keyed(key, defaultText);

        text = translations.IsEmpty ? text : text.WithTranslations(translations);
        return text;
    }

    private static ImmutableDictionary<string, string> ReadTranslations(ref Utf8JsonReader reader)
    {
        if (reader.TokenType is not JsonTokenType.StartObject)
            throw new JsonException($"The \"{TranslationsName}\" of a text are a JSON object of languages and their wording, not {reader.TokenType}.");

        var builder = LocalizableText.NoTranslations.ToBuilder();

        while (ReadToken(ref reader) is not JsonTokenType.EndObject)
        {
            var language = reader.GetString();
            ReadToken(ref reader);

            if (String.IsNullOrWhiteSpace(language))
                throw new JsonException("A language of a text's translations can't be empty or white-space.");

            builder[language] = ReadString(ref reader, language);
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// The next token of a text, or an exception when the JSON ends before the text does.
    /// </summary>
    // The serializer hands a converter all of its value, so this only meets JSON that ends early when something else
    // calls the converter on part of a text; half a text must not read as a whole one.
    private static JsonTokenType ReadToken(ref Utf8JsonReader reader)
    {
        var token = reader.Read() ? reader.TokenType : throw new JsonException(EndsEarlyMessage);
        return token;
    }

    private static string ReadString(ref Utf8JsonReader reader, string name)
    {
        var readString = reader.TokenType is JsonTokenType.String
            ? reader.GetString()!
            : throw new JsonException($"\"{name}\" of a text is a string, not {reader.TokenType}.");

        return readString;
    }
}
