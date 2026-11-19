using System.Reflection;
using System.Runtime.CompilerServices;
using CodeChops.Idioma.Internals;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="GeneratedCode"/>: the names generated code calls it by, and that it is all generated code needs.
/// </summary>
public sealed class GeneratedCodeTests
{
    #region Fixture
    private interface IGeneratedCodeLocalization : ILocalizationSet
    {
        string Greeting(string name);
    }

    private sealed class GeneratedCodeLocalizationEn : IGeneratedCodeLocalization
    {
        public string Greeting(string name) => $"Hello, {name}!";
    }
    #endregion

    [Test]
    public void MetadataName_ShouldMatchSourceGeneratorReferences()
    {
        // Arrange & Act
        var members = typeof(GeneratedCode)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(static method => method.Name);

        // Assert: generated code calls these by name, so a rename or namespace change must update the generators'
        // emission in the same commit.
        typeof(GeneratedCode).FullName.ShouldBe("CodeChops.Idioma.Internals.GeneratedCode");
        members.Distinct(StringComparer.Ordinal).ShouldBe(
            ["CreateText", "CreateSetText", "CreateUndefinedEnumText", "GetKeyedText", "GetKeyedValue", "CreateUnknownKeyException", "RegisterLanguages", "RecordSet"],
            ignoreOrder: true);
    }

    [Test]
    public void GetKeyedText_ShouldGiveTheTextUnlessValuesWerePassed()
    {
        // Arrange
        LocalizableText text = LocalizableText.Keyed("Checkout", "Checkout");

        // Act
        var given = GeneratedCode.GetKeyedText<IGeneratedCodeLocalization>(text, "Checkout", []);
        var exception = Should.Throw<ArgumentException>(() => GeneratedCode.GetKeyedText<IGeneratedCodeLocalization>(text, "Checkout", ["Anna"]));

        // Assert
        given.ShouldBeSameAs(text);
        exception.ParamName.ShouldBe("values");
        exception.Message.ShouldStartWith("The text 'Checkout' of 'IGeneratedCodeLocalization' takes no values, but 1 was passed.");
    }

    [Test]
    public void GetKeyedValue_ShouldGiveTheValueOfTheParameter()
    {
        // Arrange & Act & Assert: by its position, as its own type, and null where the type allows it.
        GeneratedCode.GetKeyedValue<int>("Items", [3, "Idioma"], index: 0, count: 2, "count").ShouldBe(3);
        GeneratedCode.GetKeyedValue<string>("Items", [3, "Idioma"], index: 1, count: 2, "shop").ShouldBe("Idioma");
        GeneratedCode.GetKeyedValue<string?>("Greeting", [null], index: 0, count: 1, "name").ShouldBeNull();
        GeneratedCode.GetKeyedValue<int?>("Items", [null], index: 0, count: 1, "count").ShouldBeNull();
        GeneratedCode.GetKeyedValue<object>("Items", [3], index: 0, count: 1, "count").ShouldBe(3);
    }

    [Test]
    public void GetKeyedValue_ShouldRefuseAnotherNumberOfValuesThanTheTextTakes()
    {
        // Arrange & Act
        var tooFew = Should.Throw<ArgumentException>(() => GeneratedCode.GetKeyedValue<int>("Items", [], index: 0, count: 2, "count"));
        var tooMany = Should.Throw<ArgumentException>(() => GeneratedCode.GetKeyedValue<string>("Greeting", ["Anna", "Max"], index: 0, count: 1, "name"));

        // Assert
        tooFew.ParamName.ShouldBe("values");
        tooFew.Message.ShouldStartWith("The text 'Items' takes 2 values, but 0 were passed.");
        tooMany.Message.ShouldStartWith("The text 'Greeting' takes 1 value, but 2 were passed.");
    }

    [Test]
    public void GetKeyedValue_ShouldRefuseValueOfAnotherTypeWithoutConvertingIt()
    {
        // Arrange & Act: an int for a long, and null for a type that has none.
        var otherType = Should.Throw<ArgumentException>(() => GeneratedCode.GetKeyedValue<long>("Items", [3], index: 0, count: 1, "count"));
        var noValue = Should.Throw<ArgumentException>(() => GeneratedCode.GetKeyedValue<int>("Items", [null], index: 0, count: 1, "count"));

        // Assert
        otherType.ParamName.ShouldBe("values");
        otherType.Message.ShouldStartWith("The text 'Items' takes a value of type 'System.Int64' for 'count', but a value of type 'System.Int32' was passed.");
        noValue.Message.ShouldStartWith("The text 'Items' takes a value of type 'System.Int32' for 'count', but null was passed.");
    }

    [Test]
    public void CreateUnknownKeyException_ShouldNameTheKeyAndTheClass()
    {
        // Arrange & Act
        var unknown = GeneratedCode.CreateUnknownKeyException<IGeneratedCodeLocalization>("Crat", "ShopLocalization");
        var missing = GeneratedCode.CreateUnknownKeyException<IGeneratedCodeLocalization>(key: null, "ShopLocalization");

        // Assert
        unknown.ShouldBeOfType<KeyNotFoundException>().Message.ShouldStartWith("'ShopLocalization' has no text with the key 'Crat'.");
        missing.ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("key");
    }

    [Test]
    [Arguments(DayOfWeek.Friday, "Friday")]
    [Arguments((DayOfWeek)42, "42")]
    public void CreateUndefinedEnumText_ShouldShowValueAsToStringWritesIt(DayOfWeek value, string expected)
    {
        // Arrange & Act: as the method GetTextByValue of a generated class calls it, for a value none of its texts is named after.
        var text = GeneratedCode.CreateUndefinedEnumText(value);

        // Assert: the same in every language, and no text of a set.
        text.ToString(CultureInfo.GetCultureInfo("nl-NL")).ShouldBe(expected);
        text.DefaultText.ShouldBe(expected);
        text.Key.ShouldBeNull();
        text.FullKey.ShouldBeNull();
    }

    [Test]
    public void Class_ShouldBeObsoleteAsWarning()
    {
        // Arrange & Act
        var obsolete = typeof(GeneratedCode).GetCustomAttribute<ObsoleteAttribute>();

        // Assert: a call by hand warns (CS0618). An error (CS0619) would break the generated code, which can only
        // suppress a warning.
        obsolete.ShouldNotBeNull();
        obsolete.IsError.ShouldBeFalse();
    }

    [Test]
    public void CreateSetText_WithArguments_ShouldGiveTextThatKeepsThem()
    {
        // Arrange
        const string name = "Anna";

        // Act: as a method of the class the source generator writes calls it.
        var text = GeneratedCode.CreateSetText<IGeneratedCodeLocalization, string>("Shop.Greeting", static (set, value) => set.Greeting(value), new GeneratedCodeLocalizationEn(), name);

        // Assert
        text.Key.ShouldBe("Greeting");
        text.FullKey.ShouldBe("Shop.Greeting");
        text.Contract.ShouldBe(typeof(IGeneratedCodeLocalization));
        text.DefaultText.ShouldBe("Hello, Anna!");
        text.Arguments.ShouldBe(["Anna"]);
    }

    [Test]
    public void CreateText_ShouldGiveTextWithItsTranslations()
    {
        // Arrange & Act
        var text = GeneratedCode.CreateText("Orders", ("nl", "Bestellingen"));

        // Assert
        text.DefaultText.ShouldBe("Orders");
        text.GetWording(new CultureInfo("nl-NL")).ShouldBe("Bestellingen");
    }

    [Test]
    [Arguments("", "en")]
    [Arguments("s", "")]
    public void CreateText_ShouldTakeEmptyWordingInAnyLanguage(string english, string dutch)
    {
        // Arrange & Act: a suffix one language has and another lacks. The default language is no exception.
        var text = GeneratedCode.CreateText(english, ("en", english), ("nl", dutch));

        // Assert
        text.DefaultText.ShouldBe(english);
        text.GetWording(new CultureInfo("nl-NL")).ShouldBe(dutch);
    }

    [Test]
    public void CreateText_ShouldRefuseNullWording()
    {
        // Arrange, Act & Assert
        Should.Throw<ArgumentNullException>(() => GeneratedCode.CreateText(null!, ("nl", "Bestellingen")));
        Should.Throw<ArgumentNullException>(() => GeneratedCode.CreateText("Orders", ("nl", null!)));
    }

    [Test]
    public void CreateText_WithSameStrings_ShouldGiveTextCreatedOnce()
    {
        // Arrange & Act: the same literals twice, as markup that writes LocalizableText.Inline does on every render.
        var first = GeneratedCode.CreateText("Shipments", ("en", "Shipments"), ("nl", "Zendingen"));
        var second = GeneratedCode.CreateText("Shipments", ("en", "Shipments"), ("nl", "Zendingen"));

        // Assert
        second.ShouldBeSameAs(first);
    }

    [Test]
    public void CreateText_WithOtherTranslation_ShouldGiveTextOfItsOwn()
    {
        // Arrange & Act: the same English wording, and another Dutch one.
        var dutch = new CultureInfo("nl-NL");
        var deliveries = GeneratedCode.CreateText("Deliveries", ("en", "Deliveries"), ("nl", "Leveringen"));
        var other = GeneratedCode.CreateText("Deliveries", ("en", "Deliveries"), ("nl", "Bezorgingen"));

        // Assert
        other.ShouldNotBeSameAs(deliveries);
        deliveries.GetWording(dutch).ShouldBe("Leveringen");
        other.GetWording(dutch).ShouldBe("Bezorgingen");
    }

    [Test]
    public void CreateText_WithWordingMadeAtRunTime_ShouldShowItsOwnValue()
    {
        // Arrange: wording with a value, which is a new string on every call.
        var dutch = new CultureInfo("nl-NL");
        static LocalizableText Greeting(string name) =>
            GeneratedCode.CreateText($"Hello, {name}!", ("en", $"Hello, {name}!"), ("nl", $"Hallo, {name}!"));

        // Act
        var ada = Greeting("Ada");
        var bob = Greeting("Bob");

        // Assert: each shows its own value, also where a later text took the slot of an earlier one.
        ada.DefaultText.ShouldBe("Hello, Ada!");
        ada.GetWording(dutch).ShouldBe("Hallo, Ada!");
        bob.DefaultText.ShouldBe("Hello, Bob!");
        bob.GetWording(dutch).ShouldBe("Hallo, Bob!");
    }

    [Test]
    public void CreateText_WithWordingMadeAtRunTime_ShouldNotKeepIt()
    {
        // Arrange & Act: wording with a value, which is a new string on every call, so no later call could find it.
        var wording = CreateAndForget("Ada");

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Assert: nothing holds the wording once its text is gone, so a user's name doesn't stay in memory.
        wording.IsAlive.ShouldBeFalse();

        // A method of its own, so that nothing of it holds the wording when the collector runs.
        [MethodImpl(MethodImplOptions.NoInlining)]
        static WeakReference CreateAndForget(string name)
        {
            var english = $"Hello, {name}!";
            _ = GeneratedCode.CreateText(english, ("en", english), ("nl", $"Hallo, {name}!"));

            return new WeakReference(english);
        }
    }

    [Test]
    public void CreateText_WithOneWordingMadeAtRunTime_ShouldNotKeepIt()
    {
        // Arrange: a default wording that is a literal, and Dutch wording made at run time.
        var dutch = String.Concat("Retour", "zendingen");

        // Act: the same strings twice.
        var first = GeneratedCode.CreateText("Returns", ("en", "Returns"), ("nl", dutch));
        var second = GeneratedCode.CreateText("Returns", ("en", "Returns"), ("nl", dutch));

        // Assert: the first wasn't kept, though its default wording is no new string.
        second.ShouldNotBeSameAs(first);
    }

    [Test]
    public void CreateText_WithSameStrings_ShouldAllocateNothingOnceKept()
    {
        // Arrange & Act: per 10,000 calls with the same literals.
        var allocated = GeneratedSetsTests.Allocated(static () => GeneratedCode.CreateText("Invoices", ("en", "Invoices"), ("nl", "Facturen"))) / 10_000;

        // Assert
        allocated.ShouldBe(0);
    }

    [Test]
    public void CreateText_WithWordingOf40000CharactersOrMore_ShouldNotKeepTheText()
    {
        // Arrange: the runtime reports a string this large as old from the start, so it can't be told from a literal. It
        // may hold a value, such as the body of an email, so its text isn't kept.
        var english = new string('a', 60_000);
        var dutch = new string('b', 60_000);

        // Act
        var first = GeneratedCode.CreateText(english, ("en", english), ("nl", dutch));
        var second = GeneratedCode.CreateText(english, ("en", english), ("nl", dutch));

        // Assert
        second.ShouldNotBeSameAs(first);
        second.ToString(CultureInfo.GetCultureInfo("nl")).ShouldBe(dutch);
    }
}
