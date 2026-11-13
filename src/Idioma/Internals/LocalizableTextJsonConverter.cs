namespace CodeChops.Idioma.Internals;

/// <summary>
/// Writes a <see cref="LocalizableText"/> or a <see cref="ResourceText"/> as JSON and reads it back, so that a text
/// survives JSON, such as a Blazor component parameter that crosses a render mode. You never need this type: both
/// texts name it in their <see cref="JsonConverterAttribute"/>.
/// </summary>
/// <remarks>
/// <para>
/// A text is written with every wording it can show: its default wording, its own translations, and for a text of an
/// <see cref="ITranslations"/> contract the wording of every language your app declares that a set is registered for,
/// their regions included, as a user of that language sees it when the text is written. A resource text keeps its
/// <see cref="ResourceText.Key"/>:
/// <c>{"key":"Shop.Checkout","defaultText":"Checkout","translations":{"en":"Checkout","nl":"Afrekenen"}}</c>.
/// A text with one wording for every culture and no key, such as a customer's name, is written as that wording:
/// <c>"Anna"</c>. A text without translations leaves <c>translations</c> out.
/// </para>
/// <para>
/// Read back, the text shows that wording for every culture, even after a switch of language, without the sets. It
/// belongs to no contract anymore, so a set registered later does not reword it.
/// </para>
/// <para>
/// A JSON string reads as a text with that one wording for every culture, as a <see cref="string"/> converts to a
/// text, so JSON written by hand can hold <c>"title": "Orders"</c>. A <c>key</c> or <c>translations</c> that is
/// <c>null</c> reads as left out. A <see cref="ResourceText"/> always needs its object, for its key.
/// </para>
/// </remarks>
// A factory, as each of the two types needs a converter of its own type. Public, because the System.Text.Json source
// generator creates the converter a type names from the code it writes into the consumer's assembly.
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class LocalizableTextJsonConverter : JsonConverterFactory
{
    private const string KeyName = "key";
    private const string DefaultTextName = "defaultText";
    private const string TranslationsName = "translations";
    private const string EndsEarlyMessage = "The JSON ends before the text does.";

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert == typeof(LocalizableText) || typeToConvert == typeof(ResourceText);
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        return typeToConvert == typeof(ResourceText) ? ResourceTextConverter.Instance : TextConverter.Instance;
    }

    /// <summary>
    /// Writes a text, with the wording the sets of a registry give it.
    /// </summary>
    internal static void Write(Utf8JsonWriter writer, LocalizableText text, TranslationsRegistry registry)
    {
        var wordingPerLanguage = text.GetWordingPerLanguage(registry);
        var resourceText = text as ResourceText;

        // A text with one wording and no key is that wording, as it reads back from a string: a customer's name
        // travels as the name alone.
        if (resourceText is null && wordingPerLanguage.IsEmpty)
        {
            writer.WriteStringValue(text.DefaultText);
            return;
        }

        writer.WriteStartObject();

        if (resourceText is not null)
            writer.WriteString(KeyName, resourceText.Key);

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
    /// Reads a text: a resource text when the JSON holds a key, or when <paramref name="requiresKey"/> demands one.
    /// </summary>
    private static LocalizableText Read(ref Utf8JsonReader reader, bool requiresKey)
    {
        // A string is a text without translations, as it is everywhere else; a resource text has a key to carry.
        if (reader.TokenType is JsonTokenType.String && !requiresKey)
            return new LocalizableText(reader.GetString()!);

        if (reader.TokenType is not JsonTokenType.StartObject)
            throw new JsonException(requiresKey
                ? $"A {nameof(ResourceText)} is a JSON object with a \"{KeyName}\" and a \"{DefaultTextName}\", not {reader.TokenType}."
                : $"A text is a JSON string, or a JSON object with a \"{DefaultTextName}\", not {reader.TokenType}.");

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
            throw new JsonException($"The \"{KeyName}\" of a text cannot be empty or white-space.");

        if (requiresKey && key is null)
            throw new JsonException($"A {nameof(ResourceText)} needs its \"{KeyName}\".");

        var text = key is null ? new LocalizableText(defaultText) : new ResourceText(key, defaultText);

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
                throw new JsonException("A language of a text's translations cannot be empty or white-space.");

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

    private sealed class TextConverter : JsonConverter<LocalizableText>
    {
        public static TextConverter Instance { get; } = new();

        public override LocalizableText Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return LocalizableTextJsonConverter.Read(ref reader, requiresKey: false);
        }

        public override void Write(Utf8JsonWriter writer, LocalizableText value, JsonSerializerOptions options)
        {
            LocalizableTextJsonConverter.Write(writer, value, TranslationsRegistry.Current);
        }
    }

    private sealed class ResourceTextConverter : JsonConverter<ResourceText>
    {
        public static ResourceTextConverter Instance { get; } = new();

        public override ResourceText Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return (ResourceText)LocalizableTextJsonConverter.Read(ref reader, requiresKey: true);
        }

        public override void Write(Utf8JsonWriter writer, ResourceText value, JsonSerializerOptions options)
        {
            LocalizableTextJsonConverter.Write(writer, value, TranslationsRegistry.Current);
        }
    }
}
