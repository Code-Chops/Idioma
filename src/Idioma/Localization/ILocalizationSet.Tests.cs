namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="ILocalizationSet"/>, the base of every localization contract: the language code derived from the
/// type's name or given by the build's description of it, the implementation that replaces it with any neutral culture
/// name, a clear error for a name the code can't be derived from, and the metadata name the generator pins.
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
    /// A set the build describes with Belgian Dutch, as the registrar of an assembly declaring nl-BE does.
    /// </summary>
    private class DescribedLocalizationNlBe : ILocalizationSet;

    /// <summary>
    /// A rewording of the Belgian set, under a name that gives no language of its own.
    /// </summary>
    private sealed class DescribedRewording : DescribedLocalizationNlBe;

    /// <summary>
    /// A set implementing its code, which a description does not replace.
    /// </summary>
    private sealed class ImplementedLocalizationNlBe : ILocalizationSet
    {
        string ILocalizationSet.LocalizationLanguageCode => "nl-NL";
    }

    private sealed class FirstDescribedLocalizationZhHans : ILocalizationSet;

    /// <summary>
    /// A set whose code is described in another letter case than culture names have.
    /// </summary>
    private sealed class CasedDescribedLocalization : ILocalizationSet;

    /// <summary>
    /// A set whose code is described twice, as when the generated registration runs again.
    /// </summary>
    private sealed class RepeatedDescribedLocalizationEnGb : ILocalizationSet;
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
    public void LanguageCode_ShouldPreferCodeDescribedForSetOrSetItDerivesFrom()
    {
        // Arrange: what the generated registrar does for a set named after a declared code.
        LocalizationRegistry.DescribeSet<DescribedLocalizationNlBe>("nl-BE", []);
        LocalizationRegistry.DescribeSet<ImplementedLocalizationNlBe>("nl-BE", []);

        // Act & Assert: the description wins over the last two letters (be), a rewording inherits it, and an implementation
        // wins over a description.
        ((ILocalizationSet)new DescribedLocalizationNlBe()).LocalizationLanguageCode.ShouldBe("nl-BE");
        ((ILocalizationSet)new DescribedRewording()).LocalizationLanguageCode.ShouldBe("nl-BE");
        ILocalizationSet.GetLanguageCode(typeof(DescribedRewording)).ShouldBe("nl-BE");
        ((ILocalizationSet)new ImplementedLocalizationNlBe()).LocalizationLanguageCode.ShouldBe("nl-NL");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void DescribeSet_ShouldThrowForSecondCodeAndKeepFirst()
    {
        // Arrange & Act: a set's language must not change, so a second code is refused.
        LocalizationRegistry.DescribeSet<FirstDescribedLocalizationZhHans>("zh-Hans", []);
        Should.Throw<ArgumentException>(() => LocalizationRegistry.DescribeSet<FirstDescribedLocalizationZhHans>("zh-Hant", []));

        // Assert
        ((ILocalizationSet)new FirstDescribedLocalizationZhHans()).LocalizationLanguageCode.ShouldBe("zh-Hans");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void DescribeSet_ShouldIgnoreSameCodeDescribedAgain()
    {
        // Arrange & Act: in another letter case too, which gives the same culture name.
        LocalizationRegistry.DescribeSet<RepeatedDescribedLocalizationEnGb>("en-GB", []);
        LocalizationRegistry.DescribeSet<RepeatedDescribedLocalizationEnGb>("en-GB", []);
        LocalizationRegistry.DescribeSet<RepeatedDescribedLocalizationEnGb>("EN-gb", []);

        // Assert
        ILocalizationSet.GetLanguageCode(typeof(RepeatedDescribedLocalizationEnGb)).ShouldBe("en-GB");
    }

    [Test]
    public void DescribeSet_ShouldThrowForCodeThatIsNoCultureName()
    {
        // Arrange & Act & Assert: a code no culture reaches would fail every text once the sets are read again.
        Should.Throw<ArgumentException>(() => LocalizationRegistry.DescribeSet<ContractLocalizationNl>(" ", []));
        Should.Throw<ArgumentException>(() => LocalizationRegistry.DescribeSet<ContractLocalizationNl>("nl_BE", []));
        ILocalizationSet.GetLanguageCode(typeof(ContractLocalizationNl)).ShouldBe("nl");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void DescribeSet_ShouldWriteCodeAsCultureName()
    {
        // Arrange & Act
        LocalizationRegistry.DescribeSet<CasedDescribedLocalization>("NL-be", []);

        // Assert
        ILocalizationSet.GetLanguageCode(typeof(CasedDescribedLocalization)).ShouldBe("nl-BE");
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
