namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="ITranslationSet"/>, the base of every translations contract: the language code derived from the
/// type's name or recorded for it by the build, the implementation that replaces it with any neutral culture name, a
/// clear error for a name the code cannot be derived from, and the metadata name the generator pins.
/// </summary>
public sealed class ITranslationSetTests
{
    #region Fixture
    // ReSharper disable ClassNeverInstantiated.Local - only their names matter.
    private sealed class Translations1;

    private sealed class TranslationsÉÈ;

    private sealed class Translations<T>;
    // ReSharper restore ClassNeverInstantiated.Local

    private sealed class ContractTranslationsNL : ITranslationSet;

    private sealed class ContractTranslationsde : ITranslationSet;

    /// <summary>
    /// A set serving one script of a language: a code the two-letter default cannot carry.
    /// </summary>
    private sealed class SimplifiedChineseTranslations : ITranslationSet
    {
        string ITranslationSet.LanguageCode => "zh-Hans";
    }

    /// <summary>
    /// A set the build records Belgian Dutch for, as the registrar of an assembly declaring nl-BE does.
    /// </summary>
    private class RecordedTranslationsNlBE : ITranslationSet;

    /// <summary>
    /// A rewording of the Belgian set, under a name that gives no language of its own.
    /// </summary>
    private sealed class RecordedRewording : RecordedTranslationsNlBE;

    /// <summary>
    /// A set implementing its code, which a record does not replace.
    /// </summary>
    private sealed class ImplementedTranslationsNlBE : ITranslationSet
    {
        string ITranslationSet.LanguageCode => "nl-NL";
    }

    private sealed class FirstRecordedTranslationsZhHans : ITranslationSet;

    /// <summary>
    /// A set whose code is recorded in another letter case than culture names have.
    /// </summary>
    private sealed class CasedRecordedTranslations : ITranslationSet;
    #endregion

    [Test]
    public void LanguageCode_ShouldDefaultToTypeNameSuffixInLowerCase()
    {
        // Arrange & Act & Assert: the suffix in any letter case.
        ((ITranslationSet)new ContractTranslationsNL()).LanguageCode.ShouldBe("nl");
        ((ITranslationSet)new ContractTranslationsde()).LanguageCode.ShouldBe("de");
    }

    [Test]
    public void LanguageCode_ShouldBeImplementableWithAnyNeutralCultureName()
    {
        // Arrange
        var translations = new SimplifiedChineseTranslations();
        var registry = TranslationsRegistry.Create([translations]);

        // Act & Assert: zh-Hans-CN resolves through zh-Hans; zh-Hant-TW walks zh-Hant and zh, neither registered.
        ((ITranslationSet)translations).LanguageCode.ShouldBe("zh-Hans");
        registry.Find<ITranslationSet>(CultureInfo.GetCultureInfo("zh-Hans-CN")).ShouldBeSameAs(translations);
        registry.Find<ITranslationSet>(CultureInfo.GetCultureInfo("zh-Hant-TW")).ShouldBeNull();
    }

    [Test]
    public void LanguageCode_ShouldPreferCodeRecordedForSetOrSetItDerivesFrom()
    {
        // Arrange: what the generated registrar does for a set named after a declared code that is not two letters.
        TranslationsRegistry.RegisterLanguageCode<RecordedTranslationsNlBE>("nl-BE");
        TranslationsRegistry.RegisterLanguageCode<ImplementedTranslationsNlBE>("nl-BE");

        // Act & Assert: the record wins over the last two letters (be), a rewording inherits it, and an implementation
        // wins over a record.
        ((ITranslationSet)new RecordedTranslationsNlBE()).LanguageCode.ShouldBe("nl-BE");
        ((ITranslationSet)new RecordedRewording()).LanguageCode.ShouldBe("nl-BE");
        ITranslationSet.GetLanguageCode(typeof(RecordedRewording)).ShouldBe("nl-BE");
        ((ITranslationSet)new ImplementedTranslationsNlBE()).LanguageCode.ShouldBe("nl-NL");
    }

    [Test]
    public void RegisterLanguageCode_ShouldKeepFirstCode()
    {
        // Arrange & Act: a set's language must not change while it is registered.
        TranslationsRegistry.RegisterLanguageCode<FirstRecordedTranslationsZhHans>("zh-Hans");
        TranslationsRegistry.RegisterLanguageCode<FirstRecordedTranslationsZhHans>("zh-Hant");

        // Assert
        ((ITranslationSet)new FirstRecordedTranslationsZhHans()).LanguageCode.ShouldBe("zh-Hans");
    }

    [Test]
    public void RegisterLanguageCode_ShouldThrowForCodeThatIsNoCultureName()
    {
        // Arrange & Act & Assert: a code no culture reaches would fail every text once the sets are read again.
        Should.Throw<ArgumentException>(() => TranslationsRegistry.RegisterLanguageCode<ContractTranslationsNL>(" "));
        Should.Throw<ArgumentException>(() => TranslationsRegistry.RegisterLanguageCode<ContractTranslationsNL>("nl_BE"));
        ITranslationSet.GetLanguageCode(typeof(ContractTranslationsNL)).ShouldBe("nl");
    }

    [Test]
    public void RegisterLanguageCode_ShouldWriteCodeAsCultureName()
    {
        // Arrange & Act
        TranslationsRegistry.RegisterLanguageCode<CasedRecordedTranslations>("NL-be");

        // Assert
        ITranslationSet.GetLanguageCode(typeof(CasedRecordedTranslations)).ShouldBe("nl-BE");
    }

    [Test]
    public void GetLanguageCode_ShouldThrowForNameWithoutTwoAsciiLetterSuffix()
    {
        // Arrange: a digit, letters outside ASCII (no culture name carries them), and a generic type's arity.
        Type[] setTypes = [typeof(Translations1), typeof(TranslationsÉÈ), typeof(Translations<int>)];

        foreach (var setType in setTypes)
        {
            // Act
            var exception = Should.Throw<InvalidOperationException>(() => ITranslationSet.GetLanguageCode(setType));

            // Assert: the message names the type and points at the naming rule and the property to implement.
            exception.Message.ShouldContain(setType.Name);
            exception.Message.ShouldContain(nameof(ITranslationSet.LanguageCode));
        }
    }

    [Test]
    public void GetLanguageCode_ShouldThrowForNullType()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentNullException>(() => ITranslationSet.GetLanguageCode(null!));
    }

    [Test]
    public void MetadataName_ShouldMatchSourceGeneratorReferences()
    {
        // Assert: the source generators reference this name as a string constant, so a rename or namespace change must
        // update the generator constant in the same commit.
        typeof(ITranslationSet).FullName.ShouldBe("CodeChops.Idioma.ITranslationSet");
    }
}
