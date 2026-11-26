namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="ILocalizationSet"/>, the base of every localization contract: the language code derived from the
/// type's name or recorded for it by the build, the implementation that replaces it with any neutral culture name, a
/// clear error for a name the code can't be derived from, and the metadata name the generator pins.
/// </summary>
public sealed class ILocalizationSetTests
{
    #region Fixture
    // ReSharper disable ClassNeverInstantiated.Local - only their names matter.
    private sealed class Localization1;

    private sealed class LocalizationÉÈ;

    private sealed class Localization<T>;
    // ReSharper restore ClassNeverInstantiated.Local

    private sealed class ContractLocalizationNl : ILocalizationSet;

    private sealed class ContractLocalizationDE : ILocalizationSet;

    private sealed class ContractLocalizationSet : ILocalizationSet;

    /// <summary>
    /// A set serving one script of a language: a code the two-letter default can't carry.
    /// </summary>
    private sealed class SimplifiedChineseLocalization : ILocalizationSet
    {
        string ILocalizationSet.LocalizationLanguageCode => "zh-Hans";
    }

    /// <summary>
    /// A set the build records Belgian Dutch for, as the registrar of an assembly declaring nl-BE does.
    /// </summary>
    private class RecordedLocalizationNlBe : ILocalizationSet;

    /// <summary>
    /// A rewording of the Belgian set, under a name that gives no language of its own.
    /// </summary>
    private sealed class RecordedRewording : RecordedLocalizationNlBe;

    /// <summary>
    /// A set implementing its code, which a record does not replace.
    /// </summary>
    private sealed class ImplementedLocalizationNlBe : ILocalizationSet
    {
        string ILocalizationSet.LocalizationLanguageCode => "nl-NL";
    }

    private sealed class FirstRecordedLocalizationZhHans : ILocalizationSet;

    /// <summary>
    /// A set whose code is recorded in another letter case than culture names have.
    /// </summary>
    private sealed class CasedRecordedLocalization : ILocalizationSet;

    /// <summary>
    /// A set whose code is recorded twice, as when the generated registration runs again.
    /// </summary>
    private sealed class RepeatedRecordedLocalizationEnGb : ILocalizationSet;
    #endregion

    [Test]
    public void LanguageCode_ShouldDefaultToTypeNameSuffixInLowerCase()
    {
        // Arrange & Act & Assert: the suffix in any letter case after its capital.
        ((ILocalizationSet)new ContractLocalizationNl()).LocalizationLanguageCode.ShouldBe("nl");
        ((ILocalizationSet)new ContractLocalizationDE()).LocalizationLanguageCode.ShouldBe("de");
    }

    [Test]
    public void LanguageCode_ShouldThrowForNameEndingInLettersWithoutCapital()
    {
        // Arrange & Act: the capital marks where a code starts, so "Set" holds none, where "et" would be Estonian.
        var exception = Should.Throw<InvalidOperationException>(() => ((ILocalizationSet)new ContractLocalizationSet()).LocalizationLanguageCode);

        // Assert
        exception.Message.ShouldContain("its name doesn't end in a language code that starts with a capital");
    }

    [Test]
    public void LanguageCode_ShouldBeImplementableWithAnyNeutralCultureName()
    {
        // Arrange
        var set = new SimplifiedChineseLocalization();
        var registry = LocalizationRegistry.Create([set]);

        // Act & Assert: zh-Hans-CN resolves through zh-Hans; zh-Hant-TW walks zh-Hant and zh, neither registered.
        ((ILocalizationSet)set).LocalizationLanguageCode.ShouldBe("zh-Hans");
        registry.GetByCulture<ILocalizationSet>(CultureInfo.GetCultureInfo("zh-Hans-CN")).ShouldBeSameAs(set);
        registry.GetByCulture<ILocalizationSet>(CultureInfo.GetCultureInfo("zh-Hant-TW")).ShouldBeNull();
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void LanguageCode_ShouldPreferCodeRecordedForSetOrSetItDerivesFrom()
    {
        // Arrange: what the generated registrar does for a set named after a declared code.
        LocalizationRegistry.RecordSet<RecordedLocalizationNlBe>("nl-BE", []);
        LocalizationRegistry.RecordSet<ImplementedLocalizationNlBe>("nl-BE", []);

        // Act & Assert: the record wins over the last two letters (be), a rewording inherits it, and an implementation
        // wins over a record.
        ((ILocalizationSet)new RecordedLocalizationNlBe()).LocalizationLanguageCode.ShouldBe("nl-BE");
        ((ILocalizationSet)new RecordedRewording()).LocalizationLanguageCode.ShouldBe("nl-BE");
        ILocalizationSet.GetLanguageCode(typeof(RecordedRewording)).ShouldBe("nl-BE");
        ((ILocalizationSet)new ImplementedLocalizationNlBe()).LocalizationLanguageCode.ShouldBe("nl-NL");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void RecordSet_ShouldThrowForSecondCodeAndKeepFirst()
    {
        // Arrange & Act: a set's language must not change, so a second code is refused.
        LocalizationRegistry.RecordSet<FirstRecordedLocalizationZhHans>("zh-Hans", []);
        Should.Throw<ArgumentException>(() => LocalizationRegistry.RecordSet<FirstRecordedLocalizationZhHans>("zh-Hant", []));

        // Assert
        ((ILocalizationSet)new FirstRecordedLocalizationZhHans()).LocalizationLanguageCode.ShouldBe("zh-Hans");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void RecordSet_ShouldIgnoreSameCodeRecordedAgain()
    {
        // Arrange & Act: in another letter case too, which gives the same culture name.
        LocalizationRegistry.RecordSet<RepeatedRecordedLocalizationEnGb>("en-GB", []);
        LocalizationRegistry.RecordSet<RepeatedRecordedLocalizationEnGb>("en-GB", []);
        LocalizationRegistry.RecordSet<RepeatedRecordedLocalizationEnGb>("EN-gb", []);

        // Assert
        ILocalizationSet.GetLanguageCode(typeof(RepeatedRecordedLocalizationEnGb)).ShouldBe("en-GB");
    }

    [Test]
    public void RecordSet_ShouldThrowForCodeThatIsNoCultureName()
    {
        // Arrange & Act & Assert: a code no culture reaches would fail every text once the sets are read again.
        Should.Throw<ArgumentException>(() => LocalizationRegistry.RecordSet<ContractLocalizationNl>(" ", []));
        Should.Throw<ArgumentException>(() => LocalizationRegistry.RecordSet<ContractLocalizationNl>("nl_BE", []));
        ILocalizationSet.GetLanguageCode(typeof(ContractLocalizationNl)).ShouldBe("nl");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void RecordSet_ShouldWriteCodeAsCultureName()
    {
        // Arrange & Act
        LocalizationRegistry.RecordSet<CasedRecordedLocalization>("NL-be", []);

        // Assert
        ILocalizationSet.GetLanguageCode(typeof(CasedRecordedLocalization)).ShouldBe("nl-BE");
    }

    [Test]
    public void GetLanguageCode_ShouldThrowForNameWithoutTwoAsciiLetterSuffix()
    {
        // Arrange: a digit, letters outside ASCII (no culture name carries them), and a generic type's arity.
        Type[] setTypes = [typeof(Localization1), typeof(LocalizationÉÈ), typeof(Localization<int>)];

        foreach (var setType in setTypes)
        {
            // Act
            var exception = Should.Throw<InvalidOperationException>(() => ILocalizationSet.GetLanguageCode(setType));

            // Assert: the message names the type and points at the naming rule and the property to implement.
            exception.Message.ShouldContain(setType.Name);
            exception.Message.ShouldContain(nameof(ILocalizationSet.LocalizationLanguageCode));
        }
    }

    [Test]
    public void GetLanguageCode_ShouldThrowForNullType()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentNullException>(() => ILocalizationSet.GetLanguageCode(null!));
    }

    [Test]
    public void MetadataName_ShouldMatchSourceGeneratorReferences()
    {
        // Assert: the source generators reference this name as a string constant, so a rename or namespace change must
        // update the generator constant in the same commit.
        typeof(ILocalizationSet).FullName.ShouldBe("CodeChops.Idioma.ILocalizationSet");
    }

    [Test]
    public void SetOfEnum_ShouldBeASetUnderTheNameTheGeneratorsRead()
    {
        // Assert: the generators tell this interface by its name and its one type parameter, and a set that lists it
        // is a set like any other.
        typeof(ILocalizationSet<>).FullName.ShouldBe("CodeChops.Idioma.ILocalizationSet`1");
        typeof(ILocalizationSet).IsAssignableFrom(typeof(ILocalizationSet<DayOfWeek>)).ShouldBeTrue();
        typeof(ILocalizationSet<>).GetMembers().ShouldBeEmpty();
    }
}
