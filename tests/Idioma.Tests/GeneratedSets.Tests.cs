using System.Text.Json;
using CodeChops.Idioma.Internals;

#region Fixture
// Top-level, because that is what the source generators discover. Nothing in the tests below registers these sets.
[assembly: CodeChops.Idioma.SupportedLanguages("en", "nl")]

namespace CodeChops.Idioma.Tests.GeneratedSets
{
    /// <summary>
    /// How a customer is addressed: a value a text chooses its wording by.
    /// </summary>
    public enum Salutation
    {
        Unknown,
        Madam,
        Sir,
    }

    public partial class GeneratedTranslationsEN : ITranslationSet
    {
        public virtual string Checkout => "Checkout";
        public virtual string Greeting => "Hello, {0}!";

        // Texts that take values: one value, a wording chosen by a number next to a value, a wording chosen by an
        // enum of this namespace, and a value of a type of another namespace.
        public virtual string Welcome(string name) => $"Welcome, {name}!";

        public virtual string ItemsInCart(int count, string shop) => count is 1
            ? $"1 item in your cart at {shop}"
            : $"{count} items in your cart at {shop}";

        public virtual string Dear(Salutation salutation) => salutation switch
        {
            Salutation.Madam => "Dear madam",
            Salutation.Sir => "Dear sir",
            _ => "Dear customer",
        };

        public virtual string DeliveredOn(DateOnly date) => $"Delivered on {date:yyyy-MM-dd}";
    }

    public sealed class GeneratedTranslationsNL : IGeneratedTranslations
    {
        public string Checkout => "Afrekenen";
        public string Greeting => "Hallo, {0}!";
        public string Welcome(string name) => $"Welkom, {name}!";

        public string ItemsInCart(int count, string shop) => count is 1
            ? $"1 artikel in je winkelwagen bij {shop}"
            : $"{count} artikelen in je winkelwagen bij {shop}";

        public string Dear(Salutation salutation) => salutation switch
        {
            Salutation.Madam => "Geachte mevrouw",
            Salutation.Sir => "Geachte heer",
            _ => "Geachte klant",
        };

        public string DeliveredOn(DateOnly date) => $"Bezorgd op {date:yyyy-MM-dd}";
    }

    /// <summary>
    /// Rewords one text of a set of this assembly, so it registers after that set: in English it must win over it.
    /// So with one text that takes a value.
    /// </summary>
    public sealed class RewordedGeneratedTranslationsEN : GeneratedTranslationsEN
    {
        public override string Checkout => "Pay";
        public override string Welcome(string name) => $"Good to see you, {name}!";
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
        public string Welcome(string name) => $"Goedendag, {name}!";
        public string ItemsInCart(int count, string shop) => $"{count} in de winkelwagen bij {shop}";
        public string Dear(Salutation salutation) => "Beste klant";
        public string DeliveredOn(DateOnly date) => $"Geleverd op {date:yyyy-MM-dd}";
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
        public static LocalizableText Orders { get; } = LocalizableText.Inline(en: "Orders", nl: "Bestellingen");
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
        public void GeneratedTextThatTakesValues_ShouldBeWordedBySetOfCultureWithThoseValues()
        {
            // Arrange
            using var _ = new UICultureScope("nl-NL");

            // Act & Assert: one value, a wording chosen by a number, a wording chosen by an enum, and a value of a
            // type of another namespace than the set's.
            GeneratedTranslations.Welcome("Max").ToString().ShouldBe("Welkom, Max!");
            GeneratedTranslations.ItemsInCart(1, "Idioma").ToString().ShouldBe("1 artikel in je winkelwagen bij Idioma");
            GeneratedTranslations.ItemsInCart(count: 3, shop: "Idioma").ToString().ShouldBe("3 artikelen in je winkelwagen bij Idioma");
            GeneratedTranslations.Dear(Salutation.Madam).ToString().ShouldBe("Geachte mevrouw");
            GeneratedTranslations.Dear(Salutation.Unknown).ToString().ShouldBe("Geachte klant");
            GeneratedTranslations.DeliveredOn(new DateOnly(2026, 11, 15)).ToString().ShouldBe("Bezorgd op 2026-11-15");
        }

        [Test]
        public void GeneratedTextThatTakesValues_ShouldFollowLanguageAfterItIsCreated()
        {
            // Arrange: created for an English user, as a text held in a field is.
            ResourceText text;
            using (new UICultureScope("en-US"))
                text = GeneratedTranslations.ItemsInCart(3, "Idioma");

            // Act & Assert: shown to a Dutch user, in markup, as a string, compared, and for a culture that is passed.
            using var _ = new UICultureScope("nl-NL");
            string assigned = text;

            text.ToString().ShouldBe("3 artikelen in je winkelwagen bij Idioma");
            assigned.ShouldBe("3 artikelen in je winkelwagen bij Idioma");
            (text == "3 artikelen in je winkelwagen bij Idioma").ShouldBeTrue();
            text.ToString(CultureInfo.GetCultureInfo("en-GB")).ShouldBe("3 items in your cart at Idioma");
        }

        [Test]
        public void GeneratedTextThatTakesValues_ShouldTakeRewordingAndFallBackLikeAnyText()
        {
            // Arrange & Act & Assert: the set rewording Welcome wins in English, the text it leaves alone keeps the
            // wording of the set it derives from, and a culture without a set gets the default language.
            using (new UICultureScope("en-US"))
            {
                GeneratedTranslations.Welcome("Max").ToString().ShouldBe("Good to see you, Max!");
                GeneratedTranslations.Dear(Salutation.Sir).ToString().ShouldBe("Dear sir");
            }

            using (new UICultureScope("fr-FR"))
                GeneratedTranslations.Welcome("Max").ToString().ShouldBe("Good to see you, Max!");
        }

        [Test]
        public void GeneratedTextThatTakesValues_ShouldCarryKeyContractAndArguments()
        {
            // Arrange & Act
            var text = GeneratedTranslations.ItemsInCart(3, "Idioma");

            // Assert: the key is the full name of the method, without its parameters.
            text.Key.ShouldBe("CodeChops.Idioma.Tests.GeneratedSets.GeneratedTranslations.ItemsInCart");
            text.Contract.ShouldBe(typeof(IGeneratedTranslations));
            text.DefaultLanguage.ShouldBe("en");
            text.DefaultText.ShouldBe("3 items in your cart at Idioma");
            text.Arguments.ShouldBe([3, "Idioma"]);
            GeneratedTranslations.Checkout.Arguments.ShouldBeEmpty();

            // As an app fills in wording it looked up by the key (docs/coming-from-resx.md).
            String.Format(CultureInfo.InvariantCulture, "{0} at {1}", text.Arguments.AsSpan()).ShouldBe("3 at Idioma");
        }

        [Test]
        public void GeneratedTextsThatTakeValues_ShouldBeEqualWhenTheirValuesAre()
        {
            // Arrange & Act & Assert: also where two values read the same in the default language. The English set
            // words both an unknown salutation and one it has no case for as "Dear customer".
            GeneratedTranslations.ItemsInCart(3, "Idioma").ShouldBe(GeneratedTranslations.ItemsInCart(3, "Idioma"));
            GeneratedTranslations.ItemsInCart(3, "Idioma").GetHashCode().ShouldBe(GeneratedTranslations.ItemsInCart(3, "Idioma").GetHashCode());
            GeneratedTranslations.ItemsInCart(3, "Idioma").ShouldNotBe(GeneratedTranslations.ItemsInCart(4, "Idioma"));
            GeneratedTranslations.Dear(Salutation.Unknown).ShouldNotBe(GeneratedTranslations.Dear((Salutation)99));
            GeneratedTranslations.Dear(Salutation.Unknown).DefaultText.ShouldBe(GeneratedTranslations.Dear((Salutation)99).DefaultText);
        }

        [Test]
        public void GeneratedTextThatTakesValues_ShouldTravelThroughJsonWithItsWordingInEveryLanguage()
        {
            // Arrange
            var text = GeneratedTranslations.ItemsInCart(3, "Idioma");

            // Act
            var json = JsonSerializer.Serialize(text);
            var read = JsonSerializer.Deserialize<ResourceText>(json)!;

            // Assert: worded with its values when it is written, so that it needs neither the sets nor the values to
            // show each language once it is read back.
            using var document = JsonDocument.Parse(json);
            var translations = document.RootElement.GetProperty("translations");
            translations.GetProperty("en").GetString().ShouldBe("3 items in your cart at Idioma");
            translations.GetProperty("nl").GetString().ShouldBe("3 artikelen in je winkelwagen bij Idioma");

            read.Key.ShouldBe(text.Key);
            read.Arguments.ShouldBeEmpty();
            read.ToString(CultureInfo.GetCultureInfo("nl-NL")).ShouldBe("3 artikelen in je winkelwagen bij Idioma");
            read.ToString(CultureInfo.GetCultureInfo("en-US")).ShouldBe("3 items in your cart at Idioma");
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
        [NotInParallel(nameof(TranslationsRegistry))]
        [Arguments("nl-NL")]
        [Arguments("nl-BE")]
        [Arguments("en-US")]
        [Arguments("en-GB")]
        [Arguments("fr-FR")]
        public void ShowingAText_ShouldAllocateNothing(string culture)
        {
            // Arrange: what the docs promise a page full of texts costs. Apart from the other tests that register sets,
            // which make the next read build the registry anew: that read is this thread's, and would count.
            using var _ = new UICultureScope(culture);
            var uiCulture = CultureInfo.CurrentUICulture;
            var checkout = GeneratedTranslations.Checkout;
            var orders = InlineTexts.Orders;

            // Act & Assert: a text of a set and an inline one, the way each is shown, and compared with a string.
            Allocated(() => checkout.ToString()).ShouldBe(0, "text of a set, ToString()");
            Allocated(() => (string)checkout).ShouldBe(0, "text of a set, as a string");
            Allocated(() => checkout.ToString(uiCulture)).ShouldBe(0, "text of a set, ToString(culture)");
            Allocated(() => checkout == "Afrekenen").ShouldBe(0, "text of a set, == a string");
            Allocated(() => "Afrekenen" != checkout).ShouldBe(0, "a string != text of a set");
            Allocated(() => orders.ToString()).ShouldBe(0, "inline text, ToString()");
            Allocated(() => orders.ToString(uiCulture)).ShouldBe(0, "inline text, ToString(culture)");
            Allocated(() => orders == "Bestellingen").ShouldBe(0, "inline text, == a string");
        }

        /// <summary>
        /// The bytes this thread allocates in an action, the least of a few rounds after a warm-up: a round that meets the
        /// JIT or a lazily built table is not what a page that shows texts pays for each time.
        /// </summary>
        private static long Allocated<T>(Func<T> action)
        {
            T? sink = default;

            for (var i = 0; i < 20_000; i++)
                sink = action();

            var least = long.MaxValue;

            for (var round = 0; round < 3; round++)
            {
                var before = GC.GetAllocatedBytesForCurrentThread();

                for (var i = 0; i < 10_000; i++)
                    sink = action();

                least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
            }

            GC.KeepAlive(sink);

            return least;
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
