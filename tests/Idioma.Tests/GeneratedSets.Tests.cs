#region Fixture
// Top-level, because that is what the source generators discover. Nothing in the tests below registers these sets.
[assembly: CodeChops.Idioma.SupportedLanguages("en", "nl")]

namespace CodeChops.Idioma.Tests.GeneratedSets
{
    public partial class GeneratedTranslationsEN : ITranslations
    {
        public virtual string Checkout => "Checkout";
        public virtual string Greeting => "Hello, {0}!";
    }

    public sealed class GeneratedTranslationsNL : IGeneratedTranslations
    {
        public string Checkout => "Afrekenen";
        public string Greeting => "Hallo, {0}!";
    }

    /// <summary>
    /// Rewords one text of a set of this assembly, so it registers after that set: in English it must win over it.
    /// </summary>
    public sealed class RewordedGeneratedTranslationsEN : GeneratedTranslationsEN
    {
        public override string Checkout => "Pay";
    }

    /// <summary>
    /// Opted out, so it stays unregistered. Registered, it would win over <see cref="GeneratedTranslationsNL"/>, which
    /// comes before it in name order.
    /// </summary>
    [ExcludeFromTranslationDiscovery]
    public sealed class UnregisteredGeneratedTranslationsNL : IGeneratedTranslations
    {
        public string Checkout => "Betalen";
        public string Greeting => "Goedendag, {0}!";
    }

    /// <summary>
    /// A helper next to the generated texts: the generated class is partial.
    /// </summary>
    public static partial class GeneratedTranslations
    {
        public static string ShoutCheckout() => Checkout.ToString().ToUpperInvariant();
    }

    /// <summary>
    /// Texts through the factory the generator writes for the languages of this assembly.
    /// </summary>
    public static class InlineTexts
    {
        public static LocalizableText Orders { get; } = LocalizableText.Translated(en: "Orders", nl: "Bestellingen");
    }
}
#endregion

namespace CodeChops.Idioma.Tests
{
    using GeneratedSets;

    /// <summary>
    /// The generators at work in a real build: this project runs them on the sets above, and the texts find their
    /// languages without any registration call, because the generated module initializer ran when this assembly loaded.
    /// </summary>
    public sealed class GeneratedSetsTests
    {
        #region Fixture
        private sealed class UICultureScope : IDisposable
        {
            private CultureInfo Previous { get; } = CultureInfo.CurrentUICulture;

            public UICultureScope(string name) => CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);

            public void Dispose() => CultureInfo.CurrentUICulture = this.Previous;
        }
        #endregion

        [Test]
        public void GeneratedText_ShouldFindSetOfCultureWithoutAnyRegistration()
        {
            // Arrange
            using var _ = new UICultureScope("nl-NL");

            // Act & Assert
            GeneratedTranslations.Checkout.ToString().ShouldBe("Afrekenen");
        }

        [Test]
        public void GeneratedText_ShouldConvertToStringAndCompareAndFormat()
        {
            // Arrange
            using var _ = new UICultureScope("nl-NL");

            // Act
            string assigned = GeneratedTranslations.Checkout;

            // Assert
            assigned.ShouldBe("Afrekenen");
            (GeneratedTranslations.Checkout == "Afrekenen").ShouldBeTrue();
            GeneratedTranslations.Greeting.Format("Max").ShouldBe("Hallo, Max!");
        }

        [Test]
        public void OverrideSet_ShouldWinOverSetItDerivesFrom()
        {
            // Arrange
            using var _ = new UICultureScope("en-US");

            // Act & Assert: the reworded text, and the inherited one.
            GeneratedTranslations.Checkout.ToString().ShouldBe("Pay");
            GeneratedTranslations.Greeting.Format("Max").ShouldBe("Hello, Max!");
        }

        [Test]
        public void CultureWithoutSet_ShouldGetSetOfDefaultLanguage()
        {
            // Arrange: no French set, so the English one serves it, reworded text included.
            using var _ = new UICultureScope("fr-FR");

            // Act & Assert
            GeneratedTranslations.Checkout.ToString().ShouldBe("Pay");
        }

        [Test]
        public void ExcludedSet_ShouldNotRegisterItself()
        {
            // Arrange
            var sets = TranslationsRegistry.Current.Sets;

            // Act & Assert: the other sets did, and Dutch keeps their wording.
            sets.ShouldContain(set => set is GeneratedTranslationsNL);
            sets.ShouldNotContain(set => set is UnregisteredGeneratedTranslationsNL);

            using var _ = new UICultureScope("nl-NL");
            GeneratedTranslations.Checkout.ToString().ShouldBe("Afrekenen");
        }

        [Test]
        public void SetDerivedFromSetOfItsAssembly_ShouldRegisterAfterIt()
        {
            // Arrange & Act
            var sets = TranslationsRegistry.Current.Sets;
            var english = sets.IndexOf(sets.Single(set => set.GetType() == typeof(GeneratedTranslationsEN)));
            var reworded = sets.IndexOf(sets.Single(set => set is RewordedGeneratedTranslationsEN));

            // Assert
            english.ShouldBeLessThan(reworded);
        }

        [Test]
        public void GeneratedClass_ShouldLetHelperSitNextToTexts()
        {
            // Arrange
            using var _ = new UICultureScope("nl-NL");

            // Act & Assert
            GeneratedTranslations.ShoutCheckout().ShouldBe("AFREKENEN");
        }

        [Test]
        public void GeneratedFactory_ShouldFollowCultureAndFallBackToDefaultLanguage()
        {
            // Arrange & Act & Assert
            using (new UICultureScope("nl-BE"))
                InlineTexts.Orders.ToString().ShouldBe("Bestellingen");

            using (new UICultureScope("en-GB"))
                InlineTexts.Orders.ToString().ShouldBe("Orders");

            using (new UICultureScope("fr-FR"))
                InlineTexts.Orders.ToString().ShouldBe("Orders");
        }

        [Test]
        public void GeneratedFactory_ShouldHoldEveryLanguage()
        {
            // Arrange & Act
            var text = InlineTexts.Orders;

            // Assert: the first is the default wording, and every language, the default included, a translation.
            text.DefaultText.ShouldBe("Orders");
            text.Translations.Keys.ShouldBe(["en", "nl"], ignoreOrder: true);
            text.Translations["en"].ShouldBe("Orders");
            text.Translations["nl"].ShouldBe("Bestellingen");
        }

        [Test]
        public void ApplicationDeclaringLanguages_ShouldRegisterItsLanguagesAtStart()
        {
            // Arrange & Act: this test project is an application declaring ("en", "nl").
            var languages = TranslationsRegistry.SupportedLanguages;

            // Assert
            languages.ShouldBe(["en", "nl"]);
            TranslationsRegistry.DefaultLanguage.ShouldBe("en");
        }

        [Test]
        public void AssemblyAttributes_ShouldBeInTheAssembly()
        {
            // Arrange
            var assembly = typeof(GeneratedSetsTests).Assembly;

            // Act
            var languages = (SupportedLanguagesAttribute)Attribute.GetCustomAttribute(assembly, typeof(SupportedLanguagesAttribute))!;
            var sets = Attribute.GetCustomAttributes(assembly, typeof(TranslationSetAttribute)).Cast<TranslationSetAttribute>().ToArray();
            var inlineTexts = Attribute.GetCustomAttribute(assembly, typeof(InlineTextsAttribute));

            // Assert: what the build of an assembly referencing this one reads.
            languages.Languages.ShouldBe(["en", "nl"]);
            inlineTexts.ShouldNotBeNull();
            sets.ShouldContain(set => set.Contract == "CodeChops.Idioma.Tests.GeneratedSets.IGeneratedTranslations" && set.Language == "en");
            sets.ShouldContain(set => set.Contract == "CodeChops.Idioma.Tests.GeneratedSets.IGeneratedTranslations" && set.Language == "nl");
        }
    }
}
