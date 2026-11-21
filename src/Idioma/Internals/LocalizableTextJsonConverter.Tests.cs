using System.Buffers;
using System.Text;
using System.Text.Json;
using CodeChops.Idioma.Internals;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="LocalizableTextJsonConverter"/>: a text written as JSON carries every wording it can show, and
/// reads back to a text that shows the same, as for a Blazor component parameter that crosses a render mode.
/// </summary>
public sealed class LocalizableTextJsonConverterTests
{
    #region Fixture
    private interface IJsonLocalization : ILocalizationSet
    {
        string Greeting { get; }
    }

    private class JsonLocalizationEn : IJsonLocalization
    {
        public virtual string Greeting => "Hello";
    }

    private sealed class JsonLocalizationNl : IJsonLocalization
    {
        public string Greeting => "Hallo";
    }

    /// <summary>
    /// An app's rewording of the English set.
    /// </summary>
    private sealed class JsonRewordedLocalizationEn : JsonLocalizationEn
    {
        public override string Greeting => "Hi";
    }

    /// <summary>
    /// A British set that keeps the English wording.
    /// </summary>
    private sealed class JsonBritishLocalization : JsonLocalizationEn, ILocalizationSet
    {
        string ILocalizationSet.LocalizationLanguageCode => "en-GB";
    }

    /// <summary>
    /// A Belgian set that spells its language in another letter case than culture names have.
    /// </summary>
    private sealed class JsonBelgianLocalization : IJsonLocalization
    {
        string ILocalizationSet.LocalizationLanguageCode => "NL-be";

        public string Greeting => "Dag";
    }

    private sealed class JsonLocalizationDe : IJsonLocalization
    {
        public string Greeting => "Hallo zusammen";
    }

    private interface IJsonCountedLocalization : ILocalizationSet
    {
        string Items(int count);
    }

    private class JsonCountedLocalizationEn : IJsonCountedLocalization
    {
        public virtual string Items(int count) => $"{count} items";
    }

    /// <summary>
    /// An app's rewording of the English set of a text that takes values.
    /// </summary>
    private sealed class JsonRewordedCountedLocalizationEn : JsonCountedLocalizationEn
    {
        public override string Items(int count) => $"{count} things";
    }

    /// <summary>
    /// A set of another contract, whose language the texts of this contract have no wording for.
    /// </summary>
    private sealed class JsonOtherLocalizationDe : ILocalizationSet;

    /// <summary>
    /// A set for Traditional Chinese, the parent culture of <c>zh-TW</c>, which is no prefix of that name.
    /// </summary>
    private class JsonTraditionalChineseLocalization : IJsonLocalization, ILocalizationSet
    {
        string ILocalizationSet.LocalizationLanguageCode => "zh-Hant";

        public virtual string Greeting => "你好";
    }

    /// <summary>
    /// A Taiwanese set that keeps the Traditional Chinese wording.
    /// </summary>
    private sealed class JsonTaiwaneseLocalization : JsonTraditionalChineseLocalization, ILocalizationSet
    {
        string ILocalizationSet.LocalizationLanguageCode => "zh-TW";
    }

    /// <summary>
    /// An app's rewording of the Traditional Chinese set.
    /// </summary>
    private sealed class JsonRewordedTraditionalChineseLocalization : JsonTraditionalChineseLocalization
    {
        public override string Greeting => "哈囉";
    }

    /// <summary>
    /// A contract whose text takes a text as its value, so that texts can be nested.
    /// </summary>
    private interface IJsonNestedLocalization : ILocalizationSet
    {
        string Wrap(LocalizableText inner);
    }

    /// <summary>
    /// An English set that counts how often it words a text, per instance, so that tests running next to each other
    /// don't count each other's calls.
    /// </summary>
    private class JsonNestedLocalizationEn : IJsonNestedLocalization
    {
        private int _calls;

        public int Calls => this._calls;

        public string Wrap(LocalizableText inner)
        {
            Interlocked.Increment(ref this._calls);
            return $"[{inner}]";
        }
    }

    /// <summary>
    /// A British set that leaves the text to the English one, recorded as the build records it.
    /// </summary>
    private sealed class JsonNestedBritishLocalization : JsonNestedLocalizationEn, ILocalizationSet
    {
        string ILocalizationSet.LocalizationLanguageCode => "en-GB";
    }

    private sealed class JsonNestedLocalizationNl : IJsonNestedLocalization
    {
        private int _calls;

        public int Calls => this._calls;

        public string Wrap(LocalizableText inner)
        {
            Interlocked.Increment(ref this._calls);
            return $"<{inner}>";
        }
    }

    private static LocalizableText Greeting { get; } = LocalizableText.Create<IJsonLocalization>("Json.Greeting", t => t.Greeting, new JsonLocalizationEn());

    private static LocalizableText Inline { get; } = LocalizableText.FromGeneratedCode("Checkout", ("en", "Checkout"), ("nl", "Afrekenen"));

    private static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// What the converter writes for a text, given the sets of a registry instead of those of the whole app.
    /// </summary>
    private static string Write(LocalizableText text, LocalizationRegistry registry)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
            LocalizableTextJsonConverter.Write(writer, text, registry);

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>
    /// Asserts what a text holds, which equality no longer tells: a text compares by the wording it shows.
    /// </summary>
    private static void ShouldHold(LocalizableText? text, string? key, string defaultText, params (string Language, string Wording)[] translations)
    {
        text.ShouldNotBeNull();
        text.Key.ShouldBe(key);
        text.Contract.ShouldBeNull();
        text.DefaultText.ShouldBe(defaultText);
        text.TranslationsByLanguage.Select(static pair => (pair.Key, pair.Value)).ShouldBe(translations, ignoreOrder: true);
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
        ShouldHold(text, key: null, Inline.DefaultText, [.. Inline.TranslationsByLanguage.Select(static pair => (pair.Key, pair.Value))]);
        text.ToString(CultureInfo.GetCultureInfo("nl-NL"), registry: null, appLanguages: ["en", "nl"]).ShouldBe("Afrekenen");
        text.ToString(CultureInfo.GetCultureInfo("en-US"), registry: null, appLanguages: ["en", "nl"]).ShouldBe("Checkout");
    }

    [Test]
    public Task Write_TextOfASet_ShouldWriteKeyAndWordingOfEveryLanguageWithASet()
    {
        // Arrange: the English set reworded, a British set keeping the English wording, a Dutch set, and a German set of
        // another contract.
        var registry = LocalizationRegistry.Create([
            new JsonLocalizationEn(),
            new JsonBritishLocalization(),
            new JsonLocalizationNl(),
            new JsonRewordedLocalizationEn(),
            new JsonOtherLocalizationDe(),
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
        var registry = LocalizationRegistry.Create(
            [new JsonLocalizationEn(), new JsonBelgianLocalization(), new JsonLocalizationDe()],
            "en-GB", "nl");

        // Act
        var json = Write(Greeting, registry);
        var text = JsonSerializer.Deserialize<LocalizableText>(json, Options)!;

        // Assert: the default language through the English set, English itself, and the region of Dutch; no German,
        // which a German user of this app never sees, and no Dutch, which no set words.
        text.TranslationsByLanguage.Keys.Order(StringComparer.Ordinal).ShouldBe(["en", "en-GB", "nl-BE"]);
        text.ToString(CultureInfo.GetCultureInfo("de-DE"), registry: null, appLanguages: ["en-GB", "nl"]).ShouldBe("Hello");
        text.ToString(CultureInfo.GetCultureInfo("nl-BE"), registry: null, appLanguages: ["en-GB", "nl"]).ShouldBe("Dag");
    }

    [Test]
    public void Write_ShouldWriteLanguagesAsCultureNamesWhateverCaseTheyWereGivenIn()
    {
        // Arrange: a set and a translation of the text itself, each spelling its language in its own way.
        var registry = LocalizationRegistry.Create([new JsonLocalizationEn(), new JsonBelgianLocalization()]);
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
        var registry = LocalizationRegistry.Create(
            [new JsonLocalizationEn(), new JsonTraditionalChineseLocalization(), new JsonTaiwaneseLocalization(), new JsonRewordedTraditionalChineseLocalization()],
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
        var registry = LocalizationRegistry.Create([new JsonLocalizationEn(), new JsonTraditionalChineseLocalization()], "zh-TW");
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
        var registry = LocalizationRegistry.Create([]);
        var declaredByKey = LocalizableText.Keyed("Customer.Name", "Anna");

        // Act
        var ofString = Write(new LocalizableText("Anna"), registry);
        var ofKey = Write(declaredByKey, registry);

        // Assert: each reads back as it was.
        ofString.ShouldBe("\"Anna\"");
        ofKey.ShouldBe("""{"key":"Customer.Name","defaultText":"Anna"}""");
        ShouldHold(JsonSerializer.Deserialize<LocalizableText>(ofString, Options), key: null, "Anna");
        ShouldHold(JsonSerializer.Deserialize<LocalizableText>(ofKey, Options), key: "Customer.Name", "Anna");
    }

    [Test]
    public void Write_TextWithValuesWithoutWordingPerLanguage_ShouldWriteDefaultWordingOfTheRegistry()
    {
        // Arrange: the registry rewords English but doesn't list it, so the text has no wording per language, and it
        // takes values, so it has no key either.
        var registry = LocalizationRegistry.Create([new JsonRewordedCountedLocalizationEn()], "nl");
        var text = GeneratedCode.CreateSetText<IJsonCountedLocalization, int>("Json.Items", static (set, count) => set.Items(count), new JsonCountedLocalizationEn(), 3);

        // Act
        var json = Write(text, registry);

        // Assert: the wording of the registry it was written with, as the object a text with more wordings is written
        // as has, and not that of the default set.
        json.ShouldBe("\"3 things\"");
    }

    [Test]
    [Arguments("""{"defaultText":"Checkout","translations":{"nl":"Afrekenen"}""")]
    [Arguments("""{"defaultText":"Checkout","translations":{"nl":""")]
    [Arguments("""{"defaultText":"Checkout","other":[1,""")]
    [Arguments("""{"defaultText":""")]
    public void Read_JsonThatEndsBeforeTheText_ShouldThrow(string json)
    {
        // Arrange: part of a text with more JSON to come, which the serializer never hands a converter.
        var converter = new LocalizableTextJsonConverter();

        // Act & Assert
        Should.Throw<JsonException>(() =>
        {
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json), isFinalBlock: false, state: default);
            reader.Read();

            converter.Read(ref reader, typeof(LocalizableText), Options);
        });
    }

    [Test]
    public void Deserialize_TextOfASet_ShouldKeepKeyAndShowWordingOfSetsWithoutThem()
    {
        // Arrange
        var registry = LocalizationRegistry.Create([new JsonLocalizationEn(), new JsonLocalizationNl()]);
        var json = Write(Greeting, registry);

        // Act
        var text = JsonSerializer.Deserialize<LocalizableText>(json, Options)!;

        // Assert: no contract any more, so the wording comes from the text itself, with no sets at all. It travelled
        // with its full key, which is the one key it has now.
        Greeting.Key.ShouldBe("Greeting");
        text.Key.ShouldBe("Json.Greeting");
        text.FullKey.ShouldBe("Json.Greeting");
        text.Contract.ShouldBeNull();
        text.ToString(CultureInfo.GetCultureInfo("nl-BE"), registry: null, appLanguages: ["en", "nl"]).ShouldBe("Hallo");
        text.ToString(CultureInfo.GetCultureInfo("fr-FR"), registry: null, appLanguages: ["en", "nl"]).ShouldBe("Hello");
    }

    [Test]
    public void Serialize_TextOfASetAsObject_ShouldWriteTextAsBlazorDoesForParameters()
    {
        // Arrange: Blazor writes the parameter values of a component that crosses a render mode as a list of objects,
        // so each value is written as its own type.
        List<object> parameters = [Greeting, Inline];

        // Act
        var json = JsonSerializer.Serialize(parameters, Options);
        var greeting = JsonSerializer.Deserialize<LocalizableText>(JsonDocument.Parse(json).RootElement[0].GetRawText(), Options)!;

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
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<LocalizableText>("""{"defaultText":null}""", Options));
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<LocalizableText>("""{"translations":{"nl":"Afrekenen"}}""", Options));
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<LocalizableText>("""{"defaultText":1}""", Options));
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<LocalizableText>("""{"defaultText":"Checkout","translations":{" ":"Afrekenen"}}""", Options));
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<LocalizableText>("""{"defaultText":"Checkout","translations":{"nl":null}}""", Options));
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<LocalizableText>("""{"key":" ","defaultText":"Checkout"}""", Options));
    }

    [Test]
    public void Deserialize_String_ShouldGiveTextWithoutTranslations()
    {
        // Arrange & Act: as JSON written by hand holds a text.
        var text = JsonSerializer.Deserialize<LocalizableText>("\"Checkout\"", Options);

        // Assert
        ShouldHold(text, key: null, "Checkout");
    }

    [Test]
    public void Deserialize_NullKeyOrTranslations_ShouldReadAsLeftOut()
    {
        // Arrange & Act
        var text = JsonSerializer.Deserialize<LocalizableText>("""{"key":null,"defaultText":"Checkout","translations":null}""", Options);

        // Assert
        ShouldHold(text, key: null, "Checkout");
    }

    [Test]
    public void Serialize_TextNestedInTexts_ShouldWordEachLevelOncePerLanguage()
    {
        // Arrange: a text nested 8 deep in texts that take a text as their value, in an app with British English, whose
        // set leaves the text to the English one.
        LocalizationRegistry.RecordSet<JsonNestedBritishLocalization>(null, []);
        var english = new JsonNestedLocalizationEn();
        var british = new JsonNestedBritishLocalization();
        var dutch = new JsonNestedLocalizationNl();
        var registry = LocalizationRegistry.Create([english, british, dutch], "en", "nl", "en-GB");
        const int depth = 8;

        var text = LocalizableText.FromGeneratedCode("Cart", ("en", "Cart"), ("nl", "Winkelwagen"), ("en-GB", "Basket"));
        for (var i = 0; i < depth; i++)
            text = GeneratedCode.CreateSetText<IJsonNestedLocalization, LocalizableText>("Json.Wrap", static (set, inner) => set.Wrap(inner), english, text);

        // Act
        var json = Write(text, registry);

        // Assert: once per level for each of the three languages and the default wording, where every level once
        // worded its British text twice (510 calls instead of 8 for this text alone). The British set isn't called,
        // because the build's record says it leaves the text to English.
        (english.Calls + british.Calls + dutch.Calls).ShouldBe(4 * depth);
        british.Calls.ShouldBe(0);

        var read = JsonSerializer.Deserialize<LocalizableText>(json, Options)!;
        read.ToString(CultureInfo.GetCultureInfo("en-GB")).ShouldBe($"{new string('[', depth)}Basket{new string(']', depth)}");
        read.ToString(CultureInfo.GetCultureInfo("nl-NL")).ShouldBe($"{new string('<', depth)}Winkelwagen{new string('>', depth)}");
        read.DefaultText.ShouldBe($"{new string('[', depth)}Cart{new string(']', depth)}");
    }

    [Test]
    public void ToString_OfTextNestedInTextsForRegion_ShouldWordEachLevelOnce()
    {
        // Arrange: as above, shown to a British user.
        LocalizationRegistry.RecordSet<JsonNestedBritishLocalization>(null, []);
        var english = new JsonNestedLocalizationEn();
        var british = new JsonNestedBritishLocalization();
        var registry = LocalizationRegistry.Create([english, british, new JsonNestedLocalizationNl()], "en", "nl", "en-GB");
        const int depth = 8;

        var text = LocalizableText.FromGeneratedCode("Cart", ("en", "Cart"), ("nl", "Winkelwagen"), ("en-GB", "Basket"));
        for (var i = 0; i < depth; i++)
            text = GeneratedCode.CreateSetText<IJsonNestedLocalization, LocalizableText>("Json.Wrap", static (set, inner) => set.Wrap(inner), english, text);

        // Act
        var wording = text.ToString(CultureInfo.GetCultureInfo("en-GB"), registry);

        // Assert: a set a less specific name overrides isn't worded at all.
        wording.ShouldBe($"{new string('[', depth)}Basket{new string(']', depth)}");
        english.Calls.ShouldBe(depth);
        british.Calls.ShouldBe(0);
    }

    [Test]
    public void Deserialize_ShouldSkipUnknownMembersAndReadNull()
    {
        // Arrange & Act
        var text = JsonSerializer.Deserialize<LocalizableText>("""{"other":{"a":[1]},"defaultText":"Checkout"}""", Options);
        var none = JsonSerializer.Deserialize<LocalizableText>("null", Options);

        // Assert
        ShouldHold(text, key: null, "Checkout");
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
        read[^1].GetWording(CultureInfo.GetCultureInfo("nl")).ShouldBe("Bestellingen");
    }
}
