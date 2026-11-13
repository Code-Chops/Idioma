using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeChops.Idioma.Internals;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="LocalizableTextJsonConverter"/>: a text written as JSON carries every wording it can show, and
/// reads back to a text that shows the same, as for a Blazor component parameter that crosses a render mode.
/// </summary>
public sealed class LocalizableTextJsonConverterTests
{
    #region Fixture
    private interface IJsonTranslations : ITranslations
    {
        string Greeting { get; }
    }

    private class JsonTranslationsEN : IJsonTranslations
    {
        public virtual string Greeting => "Hello";
    }

    private sealed class JsonTranslationsNL : IJsonTranslations
    {
        public string Greeting => "Hallo";
    }

    /// <summary>
    /// An app's rewording of the English set.
    /// </summary>
    private sealed class JsonRewordedTranslationsEN : JsonTranslationsEN
    {
        public override string Greeting => "Hi";
    }

    /// <summary>
    /// A British set that keeps the English wording.
    /// </summary>
    private sealed class JsonBritishTranslations : JsonTranslationsEN, ITranslations
    {
        string ITranslations.LanguageCode => "en-GB";
    }

    /// <summary>
    /// A Belgian set that spells its language in another letter case than culture names have.
    /// </summary>
    private sealed class JsonBelgianTranslations : IJsonTranslations
    {
        string ITranslations.LanguageCode => "NL-be";

        public string Greeting => "Dag";
    }

    private sealed class JsonTranslationsDE : IJsonTranslations
    {
        public string Greeting => "Hallo zusammen";
    }

    /// <summary>
    /// A set of another contract, whose language the texts of this contract have no wording for.
    /// </summary>
    private sealed class JsonOtherTranslationsDE : ITranslations;

    /// <summary>
    /// A set for Traditional Chinese, the parent culture of <c>zh-TW</c>, which is no prefix of that name.
    /// </summary>
    private class JsonTraditionalChineseTranslations : IJsonTranslations, ITranslations
    {
        string ITranslations.LanguageCode => "zh-Hant";

        public virtual string Greeting => "你好";
    }

    /// <summary>
    /// A Taiwanese set that keeps the Traditional Chinese wording.
    /// </summary>
    private sealed class JsonTaiwaneseTranslations : JsonTraditionalChineseTranslations, ITranslations
    {
        string ITranslations.LanguageCode => "zh-TW";
    }

    /// <summary>
    /// An app's rewording of the Traditional Chinese set.
    /// </summary>
    private sealed class JsonRewordedTraditionalChineseTranslations : JsonTraditionalChineseTranslations
    {
        public override string Greeting => "哈囉";
    }

    private static ResourceText Greeting { get; } = ResourceText.Create<IJsonTranslations>("Json.Greeting", t => t.Greeting, new JsonTranslationsEN());

    private static LocalizableText Inline { get; } = LocalizableText.FromGeneratedCode("Checkout", ("en", "Checkout"), ("nl", "Afrekenen"));

    private static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// What the converter writes for a text, given the sets of a registry instead of those of the whole app.
    /// </summary>
    private static string Write(LocalizableText text, TranslationsRegistry registry)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
            LocalizableTextJsonConverter.Write(writer, text, registry);

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
    #endregion

    [Test]
    public Task Serialize_InlineText_ShouldWriteDefaultTextAndTranslations()
    {
        // Arrange & Act
        var json = JsonSerializer.Serialize(Inline, Options);

        // Assert
        return Verify(json);
    }

    [Test]
    public void Deserialize_InlineText_ShouldEqualOriginalAndShowEveryLanguage()
    {
        // Arrange
        var json = JsonSerializer.Serialize(Inline, Options);

        // Act
        var text = JsonSerializer.Deserialize<LocalizableText>(json, Options)!;

        // Assert
        text.ShouldBe(Inline);
        text.ShouldNotBeOfType<ResourceText>();
        text.ToString(CultureInfo.GetCultureInfo("nl-NL"), registry: null, appLanguages: ["en", "nl"]).ShouldBe("Afrekenen");
        text.ToString(CultureInfo.GetCultureInfo("en-US"), registry: null, appLanguages: ["en", "nl"]).ShouldBe("Checkout");
    }

    [Test]
    public Task Write_ResourceText_ShouldWriteKeyAndWordingOfEveryLanguageWithASet()
    {
        // Arrange: the English set reworded, a British set keeping the English wording, a Dutch set, and a German set of
        // another contract.
        var registry = TranslationsRegistry.Create([
            new JsonTranslationsEN(),
            new JsonBritishTranslations(),
            new JsonTranslationsNL(),
            new JsonRewordedTranslationsEN(),
            new JsonOtherTranslationsDE(),
        ]);

        // Act
        var json = Write(Greeting, registry);

        // Assert: the default wording and each language as a user of it sees the text, the British one through the
        // rewording; no German, as nothing words this text in German.
        return Verify(json);
    }

    [Test]
    public void Write_ForRegistryWithLanguages_ShouldLeaveOutLanguagesTheyCannotShow()
    {
        // Arrange: an app declaring British English and Dutch, with sets for English, Belgian Dutch, and German.
        var registry = TranslationsRegistry.Create(
            [new JsonTranslationsEN(), new JsonBelgianTranslations(), new JsonTranslationsDE()],
            "en-GB", "nl");

        // Act
        var json = Write(Greeting, registry);
        var text = JsonSerializer.Deserialize<ResourceText>(json, Options)!;

        // Assert: the default language through the English set, English itself, and the region of Dutch; no German,
        // which a German user of this app never sees, and no Dutch, which no set words.
        text.Translations.Keys.Order(StringComparer.Ordinal).ShouldBe(["en", "en-GB", "nl-BE"]);
        text.ToString(CultureInfo.GetCultureInfo("de-DE"), registry: null, appLanguages: ["en-GB", "nl"]).ShouldBe("Hello");
        text.ToString(CultureInfo.GetCultureInfo("nl-BE"), registry: null, appLanguages: ["en-GB", "nl"]).ShouldBe("Dag");
    }

    [Test]
    public void Write_ShouldWriteLanguagesAsCultureNamesWhateverCaseTheyWereGivenIn()
    {
        // Arrange: a set and a translation of the text itself, each spelling its language in its own way.
        var registry = TranslationsRegistry.Create([new JsonTranslationsEN(), new JsonBelgianTranslations()]);
        var inline = LocalizableText.FromGeneratedCode("Checkout", ("EN", "Checkout"), ("zh-hans", "结账"));

        // Act
        var ofSet = Write(Greeting, registry);
        var ofText = Write(inline, registry);

        // Assert: the same JSON whichever set registered first and however it spells its language.
        ofSet.ShouldContain("\"nl-BE\":\"Dag\"");
        ofText.ShouldContain("\"en\":\"Checkout\"");
        ofText.ShouldContain("\"zh-Hans\":");
    }

    [Test]
    public void Write_RegionWhoseParentCultureIsNoPrefixOfItsName_ShouldWriteWordingAUserOfTheRegionSees()
    {
        // Arrange: a Taiwanese set that leaves the text as Traditional Chinese words it, and a rewording of that.
        var registry = TranslationsRegistry.Create(
            [new JsonTranslationsEN(), new JsonTraditionalChineseTranslations(), new JsonTaiwaneseTranslations(), new JsonRewordedTraditionalChineseTranslations()],
            "en", "zh-Hant");
        var taiwanese = CultureInfo.GetCultureInfo("zh-TW");

        // Act
        var text = JsonSerializer.Deserialize<LocalizableText>(Write(Greeting, registry), Options)!;

        // Assert: the rewording, which zh-TW reaches through its parent culture and not through its name.
        Greeting.ToString(taiwanese, registry).ShouldBe("哈囉");
        text.ToString(taiwanese, registry: null, appLanguages: registry.AppLanguages).ShouldBe("哈囉");
    }

    [Test]
    public void Write_DefaultLanguageWhoseParentCultureIsNoPrefixOfItsName_ShouldShowWhatAUserOfItSees()
    {
        // Arrange: an app whose default language is Taiwanese, with a set for Traditional Chinese alone.
        var registry = TranslationsRegistry.Create([new JsonTranslationsEN(), new JsonTraditionalChineseTranslations()], "zh-TW");
        var taiwanese = CultureInfo.GetCultureInfo("zh-TW");
        var french = CultureInfo.GetCultureInfo("fr-FR");

        // Act
        var text = JsonSerializer.Deserialize<LocalizableText>(Write(Greeting, registry), Options)!;

        // Assert: a culture the app does not declare gets the default language as its own users see it, live and
        // read back.
        Greeting.ToString(taiwanese, registry).ShouldBe("你好");
        Greeting.ToString(french, registry).ShouldBe("你好");
        text.ToString(taiwanese, registry: null, appLanguages: registry.AppLanguages).ShouldBe("你好");
        text.ToString(french, registry: null, appLanguages: registry.AppLanguages).ShouldBe("你好");
    }

    [Test]
    public void Write_TextWithOneWording_ShouldWriteStringAndLeaveOutEmptyTranslations()
    {
        // Arrange
        var registry = TranslationsRegistry.Create([]);
        var declaredByKey = new ResourceText("Customer.Name", "Anna");

        // Act
        var ofString = Write(new LocalizableText("Anna"), registry);
        var ofKey = Write(declaredByKey, registry);

        // Assert: each reads back as it was.
        ofString.ShouldBe("\"Anna\"");
        ofKey.ShouldBe("""{"key":"Customer.Name","defaultText":"Anna"}""");
        JsonSerializer.Deserialize<LocalizableText>(ofString, Options).ShouldBe(new LocalizableText("Anna"));
        JsonSerializer.Deserialize<LocalizableText>(ofKey, Options).ShouldBe(declaredByKey);
    }

    [Test]
    [Arguments("""{"defaultText":"Checkout","translations":{"nl":"Afrekenen"}""")]
    [Arguments("""{"defaultText":"Checkout","translations":{"nl":""")]
    [Arguments("""{"defaultText":"Checkout","other":[1,""")]
    [Arguments("""{"defaultText":""")]
    public void Read_JsonThatEndsBeforeTheText_ShouldThrow(string json)
    {
        // Arrange: part of a text with more JSON to come, which the serializer never hands a converter.
        var converter = (JsonConverter<LocalizableText>)new LocalizableTextJsonConverter().CreateConverter(typeof(LocalizableText), Options);

        // Act & Assert
        Should.Throw<JsonException>(() =>
        {
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json), isFinalBlock: false, state: default);
            reader.Read();

            converter.Read(ref reader, typeof(LocalizableText), Options);
        });
    }

    [Test]
    public void Deserialize_ResourceText_ShouldKeepKeyAndShowWordingOfSetsWithoutThem()
    {
        // Arrange
        var registry = TranslationsRegistry.Create([new JsonTranslationsEN(), new JsonTranslationsNL()]);
        var json = Write(Greeting, registry);

        // Act: read as the type written, and as the base type a component parameter declares.
        var text = JsonSerializer.Deserialize<ResourceText>(json, Options)!;
        var asBase = JsonSerializer.Deserialize<LocalizableText>(json, Options);

        // Assert: no contract any more, so the wording comes from the text itself, with no sets at all.
        text.Key.ShouldBe("Json.Greeting");
        text.Contract.ShouldBeNull();
        text.ToString(CultureInfo.GetCultureInfo("nl-BE"), registry: null, appLanguages: ["en", "nl"]).ShouldBe("Hallo");
        text.ToString(CultureInfo.GetCultureInfo("fr-FR"), registry: null, appLanguages: ["en", "nl"]).ShouldBe("Hello");
        asBase.ShouldBe(text);
    }

    [Test]
    public void Serialize_ResourceTextAsObject_ShouldWriteTextAsBlazorDoesForParameters()
    {
        // Arrange: Blazor writes the parameter values of a component that crosses a render mode as a list of objects,
        // so each value is written as its own type.
        List<object> parameters = [Greeting, Inline];

        // Act
        var json = JsonSerializer.Serialize(parameters, Options);
        var greeting = JsonSerializer.Deserialize<ResourceText>(JsonDocument.Parse(json).RootElement[0].GetRawText(), Options)!;

        // Assert
        json.ShouldContain("\"key\":\"Json.Greeting\"");
        greeting.Key.ShouldBe("Json.Greeting");
        greeting.DefaultText.ShouldBe("Hello");
    }

    [Test]
    public void Deserialize_ShouldRejectJsonThatIsNoText()
    {
        // Arrange & Act & Assert
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<LocalizableText>("1", Options));
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<ResourceText>("\"Checkout\"", Options));
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<LocalizableText>("""{"defaultText":null}""", Options));
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<LocalizableText>("""{"translations":{"nl":"Afrekenen"}}""", Options));
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<LocalizableText>("""{"defaultText":1}""", Options));
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<LocalizableText>("""{"defaultText":"Checkout","translations":{" ":"Afrekenen"}}""", Options));
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<LocalizableText>("""{"defaultText":"Checkout","translations":{"nl":null}}""", Options));
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<LocalizableText>("""{"key":" ","defaultText":"Checkout"}""", Options));
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<ResourceText>("""{"defaultText":"Checkout"}""", Options));
    }

    [Test]
    public void Deserialize_String_ShouldGiveTextWithoutTranslations()
    {
        // Arrange & Act: as JSON written by hand holds a text.
        var text = JsonSerializer.Deserialize<LocalizableText>("\"Checkout\"", Options);

        // Assert
        text.ShouldBe(new LocalizableText("Checkout"));
    }

    [Test]
    public void Deserialize_NullKeyOrTranslations_ShouldReadAsLeftOut()
    {
        // Arrange & Act
        var text = JsonSerializer.Deserialize<LocalizableText>("""{"key":null,"defaultText":"Checkout","translations":null}""", Options);

        // Assert
        text.ShouldBe(new LocalizableText("Checkout"));
        text.ShouldNotBeOfType<ResourceText>();
    }

    [Test]
    public void Deserialize_ShouldSkipUnknownMembersAndReadNull()
    {
        // Arrange & Act
        var text = JsonSerializer.Deserialize<LocalizableText>("""{"other":{"a":[1]},"defaultText":"Checkout"}""", Options);
        var none = JsonSerializer.Deserialize<LocalizableText>("null", Options);

        // Assert
        text.ShouldBe(new LocalizableText("Checkout"));
        none.ShouldBeNull();
    }

    [Test]
    public async Task Deserialize_FromStreamLongerThanSerializerReadsAtOnce_ShouldSkipUnknownMembers()
    {
        // Arrange: more JSON than the serializer reads from a stream at once, so that it hands texts to the converter
        // while more JSON is still to come. Utf8JsonReader.Skip throws there; a short text never gets that far, as the
        // serializer has all of it at once.
        var texts = String.Join(",", Enumerable.Repeat("""{"defaultText":"Orders","other":{"a":[1]},"translations":{"nl":"Bestellingen"}}""", 2000));
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes($"[{texts}]"));

        // Act
        var read = await JsonSerializer.DeserializeAsync<LocalizableText[]>(stream, Options);

        // Assert
        read.ShouldNotBeNull().Length.ShouldBe(2000);
        read[^1].GetTranslation(CultureInfo.GetCultureInfo("nl")).ShouldBe("Bestellingen");
    }
}
