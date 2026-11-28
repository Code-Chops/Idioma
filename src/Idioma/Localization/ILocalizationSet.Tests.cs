namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="ILocalizationSet"/>, the base of every localization contract: the language the registry reads
/// from a set's type (its description or its name), a clear error for
/// a type without a language, and the metadata name the generator pins.
/// </summary>
public sealed class ILocalizationSetTests
{
    #region Fixture
    // ReSharper disable ClassNeverInstantiated.Local - only their names matter.
    private sealed class Localization1;

    private sealed class LocalizationÉÈ;

    private sealed class Localization<T>;
    // ReSharper restore ClassNeverInstantiated.Local

    internal sealed class ContractLocalizationNl : ILocalizationSet;

    internal sealed class ContractLocalizationDE : ILocalizationSet;

    internal sealed class ContractLocalizationSet : ILocalizationSet;

    /// <summary>
    /// A set serving one script of a language: a code the two-letter default can't carry, so the build describes it.
    /// </summary>
    private sealed class SimplifiedChineseLocalization : ILocalizationSet;

    /// <summary>
    /// A rewording of the Chinese set, which inherits its description.
    /// </summary>
    private sealed class SimplifiedChineseRewording : SimplifiedChineseLocalizationBase;

    private class SimplifiedChineseLocalizationBase : ILocalizationSet;

    /// <summary>
    /// A set the build describes with Belgian Dutch, as the registrar of an assembly declaring nl-BE does.
    /// </summary>
    private class DescribedLocalizationNlBe : ILocalizationSet;

    /// <summary>
    /// A set the build describes with Serbian as written in Serbia, whose default script is Cyrillic.
    /// </summary>
    private sealed class ScriptDescribedLocalizationSrRs : ILocalizationSet;

    /// <summary>
    /// A rewording of the Belgian set, under a name that gives no language of its own.
    /// </summary>
    private sealed class DescribedRewording : DescribedLocalizationNlBe;

    /// <summary>
    /// A nested rewording of the Belgian set that the build didn't describe, whose name ends in another language.
    /// </summary>
    private sealed class DescribedRewordingFr : DescribedLocalizationNlBe;

    private sealed class FirstDescribedLocalizationZhHans : ILocalizationSet;

    /// <summary>
    /// A set whose code is described twice, as when the generated registration runs again.
    /// </summary>
    private sealed class RepeatedDescribedLocalizationEnGb : ILocalizationSet;
    #endregion

    [Test]
    public void LanguageCode_OfNestedSet_ShouldBeWhatTheBuildDescribedFromItsName()
    {
        // Act & Assert: the registrar of this assembly describes a nested set it can name, with the suffix in lower case.
        LocalizationRegistry.GetLanguageCode(typeof(ContractLocalizationNl)).ShouldBe("nl");
        LocalizationRegistry.GetLanguageCode(typeof(ContractLocalizationDE)).ShouldBe("de");
    }

    [Test]
    public void LanguageCode_ShouldThrowForSetWhoseNameTheBuildReadsNoLanguageFrom()
    {
        // Act: the capital marks where a code starts, so the build reads none from "Set", where "et" would be Estonian.
        var exception = Should.Throw<InvalidOperationException>(() => LocalizationRegistry.GetLanguageCode(typeof(ContractLocalizationSet)));

        // Assert
        exception.Message.ShouldContain("has no language. The build reads a set's language from its name");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void LanguageCode_ShouldComeFromDescriptionOfClassOrBaseClass()
    {
        // Arrange: what the registrar of an assembly that declares zh-Hans does.
        LocalizationRegistry.DescribeSet<SimplifiedChineseLocalization>("zh-Hans");
        LocalizationRegistry.DescribeSet<SimplifiedChineseLocalizationBase>("zh-Hans");
        var set = new SimplifiedChineseLocalization();
        var registry = LocalizationRegistry.Create([set]);

        // Act & Assert: zh-Hans-CN resolves through zh-Hans; zh-Hant-TW walks zh-Hant and zh, neither registered.
        LocalizationRegistry.GetLanguageCode(typeof(SimplifiedChineseLocalization)).ShouldBe("zh-Hans");
        LocalizationRegistry.GetLanguageCode(typeof(SimplifiedChineseRewording)).ShouldBe("zh-Hans");
        registry.GetByCulture<ILocalizationSet>(CultureInfo.GetCultureInfo("zh-Hans-CN")).ShouldBeSameAs(set);
        registry.GetByCulture<ILocalizationSet>(CultureInfo.GetCultureInfo("zh-Hant-TW")).ShouldBeNull();
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void LanguageCode_ShouldPreferCodeDescribedForSetOrSetItDerivesFrom()
    {
        // Arrange: what the generated registrar does for a set named after a declared code.
        LocalizationRegistry.DescribeSet<DescribedLocalizationNlBe>("nl-BE");

        // Act & Assert: the description wins over the last two letters (be), and a rewording inherits it, also where its
        // own name ends in another language.
        LocalizationRegistry.GetLanguageCode(typeof(DescribedLocalizationNlBe)).ShouldBe("nl-BE");
        LocalizationRegistry.GetLanguageCode(typeof(DescribedRewording)).ShouldBe("nl-BE");
        LocalizationRegistry.GetLanguageCode(typeof(DescribedRewordingFr)).ShouldBe("nl-BE");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void DescribeSet_WithScript_ShouldKeepCultureOfAnotherScriptFromThatLanguage()
    {
        // Arrange: what the generated registrar does for a regional set of a language written in two scripts.
        LocalizationRegistry.DescribeSet<ScriptDescribedLocalizationSrRs>("sr-RS", "Cyrl");
        var scripts = LocalizationRegistry.Current.AppScripts;
        var latin = new List<string>();
        var cyrillic = new List<string>();

        // Act: by name, so the names don't depend on the cultures of the machine.
        foreach (var name in new CultureFallback("sr-Latn-RS", scripts))
            latin.Add(name);

        foreach (var name in new CultureFallback("sr-Cyrl-RS", scripts))
            cyrillic.Add(name);

        // Assert: a user who reads Latin script doesn't get the Cyrillic set of their region.
        scripts["sr-RS"].ShouldBe("Cyrl");
        latin.ShouldBe(["sr-Latn-RS", "sr-Latn", "sr"]);
        cyrillic.ShouldBe(["sr-Cyrl-RS", "sr-RS", "sr-Cyrl", "sr"]);
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void DescribeSet_ShouldThrowForSecondCodeAndKeepFirst()
    {
        // Arrange & Act: a set's language must not change, so a second code is refused.
        LocalizationRegistry.DescribeSet<FirstDescribedLocalizationZhHans>("zh-Hans");
        Should.Throw<ArgumentException>(() => LocalizationRegistry.DescribeSet<FirstDescribedLocalizationZhHans>("zh-Hant"));

        // Assert
        LocalizationRegistry.GetLanguageCode(typeof(FirstDescribedLocalizationZhHans)).ShouldBe("zh-Hans");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void DescribeSet_ShouldIgnoreSameCodeDescribedAgain()
    {
        // Arrange & Act: the generated code passes a code cased as culture names are, every time it runs.
        LocalizationRegistry.DescribeSet<RepeatedDescribedLocalizationEnGb>("en-GB");
        LocalizationRegistry.DescribeSet<RepeatedDescribedLocalizationEnGb>("en-GB");

        // Assert
        LocalizationRegistry.GetLanguageCode(typeof(RepeatedDescribedLocalizationEnGb)).ShouldBe("en-GB");
    }

    [Test]
    public void GetLanguageCode_ShouldThrowForNameWithoutTwoAsciiLetterSuffix()
    {
        // Arrange: a digit, letters outside ASCII (no culture name carries them), and a generic type's arity.
        Type[] setTypes = [typeof(Localization1), typeof(LocalizationÉÈ), typeof(Localization<int>)];

        foreach (var setType in setTypes)
        {
            // Act
            var exception = Should.Throw<InvalidOperationException>(() => LocalizationRegistry.GetLanguageCode(setType));

            // Assert: the message names the type, and points at the naming rule.
            exception.Message.ShouldContain(setType.Name);
            exception.Message.ShouldContain("ShopLocalizationDe");
        }
    }

    [Test]
    public void MetadataName_ShouldMatchSourceGeneratorReferences()
    {
        // Assert: the source generators reference this name as a string constant, so a rename or namespace change must
        // update the generator constant in the same commit.
        typeof(ILocalizationSet).FullName.ShouldBe("CodeChops.Idioma.ILocalizationSet");
    }

    [Test]
    public void Interface_ShouldHaveNoMembers()
    {
        // Assert: a set's language comes from its type, so no code of the set runs to learn it.
        typeof(ILocalizationSet).GetMembers().ShouldBeEmpty();
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
