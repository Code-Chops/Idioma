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
        members.Distinct(StringComparer.Ordinal).ShouldBe(["CreateText", "CreateSetText", "RegisterLanguages", "RecordSet"], ignoreOrder: true);
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
        text.Key.ShouldBe("Shop.Greeting");
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
        text.GetTranslation(new CultureInfo("nl-NL")).ShouldBe("Bestellingen");
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
        text.GetTranslation(new CultureInfo("nl-NL")).ShouldBe(dutch);
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
        deliveries.GetTranslation(dutch).ShouldBe("Leveringen");
        other.GetTranslation(dutch).ShouldBe("Bezorgingen");
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
        ada.GetTranslation(dutch).ShouldBe("Hallo, Ada!");
        bob.DefaultText.ShouldBe("Hello, Bob!");
        bob.GetTranslation(dutch).ShouldBe("Hallo, Bob!");
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
