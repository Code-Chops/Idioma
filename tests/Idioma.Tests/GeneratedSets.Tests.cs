using CodeChops.Idioma.Internals;

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
        public async Task TextsReadByManyUsersAtOnce_ShouldShowEachUserTheirOwnLanguage()
        {
            // Arrange: as on a server, where the work of each user carries its own UI culture, and goes on wherever a
            // thread is free.
            (string Culture, string Checkout, string Greeting, string Orders)[] users =
            [
                ("nl-NL", "Afrekenen", "Hallo, Max!", "Bestellingen"),
                ("en-US", "Pay", "Hello, Max!", "Orders"),
                ("nl-BE", "Afrekenen", "Hallo, Max!", "Bestellingen"),
                ("en-GB", "Pay", "Hello, Max!", "Orders"),
                ("fr-FR", "Pay", "Hello, Max!", "Orders"),
            ];

            // Act
            var wrongReads = await Task.WhenAll(Enumerable.Range(start: 0, count: 64).Select(index => Task.Run(async () =>
            {
                var user = users[index % users.Length];
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(user.Culture);
                var wrong = 0;

                for (var i = 0; i < 2_000; i++)
                {
                    if (!String.Equals(GeneratedTranslations.Checkout.ToString(), user.Checkout, StringComparison.Ordinal)
                        || !String.Equals(GeneratedTranslations.Greeting.Format("Max"), user.Greeting, StringComparison.Ordinal)
                        || !String.Equals(InlineTexts.Orders.ToString(), user.Orders, StringComparison.Ordinal))
                    {
                        wrong++;
                    }

                    // Hands the thread to the work of another user.
                    await Task.Yield();
                }

                return wrong;
            })));

            // Assert
            wrongReads.Sum().ShouldBe(0);
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
