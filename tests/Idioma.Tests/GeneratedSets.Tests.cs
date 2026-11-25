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

    public class GeneratedLocalizationEn : ILocalizationSet, IGeneratedLocalization
    {
        public virtual string Checkout => "Checkout";
        public virtual string Greeting => "Hello!";

        // A text whose wording shows another text, which it reads itself.
        public virtual string Hint => $"Press {GeneratedLocalization.Checkout} to pay";

        // Texts that take values: one value, a wording chosen by a number next to a value, a wording chosen by an
        // enum of this namespace, a value of a type of another namespace, a value with a format, and a value that is
        // another text.
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
        public virtual string Total(decimal amount) => $"Total: {amount:N2}";
        public virtual string Removed(string what) => $"{what} removed";
    }

    public sealed class GeneratedLocalizationNl : ILocalizationSet, IGeneratedLocalization
    {
        public string Checkout => "Afrekenen";
        public string Greeting => "Hallo!";
        public string Hint => $"Druk op {GeneratedLocalization.Checkout} om te betalen";
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
        public string Total(decimal amount) => $"Totaal: {amount:N2}";
        public string Removed(string what) => $"{what} verwijderd";
    }

    /// <summary>
    /// Rewords one text of a set of this assembly, so it registers after that set: in English it must win over it.
    /// So with one text that takes a value.
    /// </summary>
    public sealed class RewordedGeneratedLocalizationEn : GeneratedLocalizationEn
    {
        public override string Checkout => "Pay";
        public override string Welcome(string name) => $"Good to see you, {name}!";
    }

    /// <summary>
    /// Opted out, so it stays unregistered. Registered, it would win over <see cref="GeneratedLocalizationNl"/>, which
    /// comes before it in name order.
    /// </summary>
    [ExcludeFromLocalizationDiscovery]
    public sealed class UnregisteredGeneratedLocalizationNl : ILocalizationSet, IGeneratedLocalization
    {
        public string Checkout => "Betalen";
        public string Greeting => "Goedendag!";
        public string Hint => "Betaal";
        public string Welcome(string name) => $"Goedendag, {name}!";
        public string ItemsInCart(int count, string shop) => $"{count} in de winkelwagen bij {shop}";
        public string Dear(Salutation salutation) => "Beste klant";
        public string DeliveredOn(DateOnly date) => $"Geleverd op {date:yyyy-MM-dd}";
        public string Total(decimal amount) => $"Totaal {amount:N2}";
        public string Removed(string what) => $"{what} weg";
    }

    /// <summary>
    /// The state of an order: an enum whose members each have a text. Two of its members share a value.
    /// </summary>
    [Flags]
    public enum OrderStatus
    {
        Pending = 1,
        Shipped = 2,
        Delivered = 4,
        Arrived = Delivered,
    }

    /// <summary>
    /// The texts of an enum: one per member, and one that is no member, which the build warns about (LOC305) and which
    /// still works.
    /// </summary>
    public class OrderStatusLocalizationEn : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
    {
        public virtual string Pending => "Pending";
        public virtual string Shipped => "Shipped";
        public virtual string Delivered => "Delivered";
        public virtual string Arrived => "Arrived";
#pragma warning disable LOC305
        public virtual string Caption => "Order status";
#pragma warning restore LOC305
    }

    public sealed class OrderStatusLocalizationNl : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
    {
        public string Pending => "In behandeling";
        public string Shipped => "Verzonden";
        public string Delivered => "Bezorgd";
        public string Arrived => "Aangekomen";
#pragma warning disable LOC305
        public string Caption => "Bestelstatus";
#pragma warning restore LOC305
    }

    /// <summary>
    /// How an order is delivered: an enum with a member marked [Obsolete] as an error, which generated code can't name,
    /// and one marked [Experimental], whose id generated code turns off.
    /// </summary>
    public enum Delivery
    {
        Standard,

        [Obsolete("Use Express.", error: true)]
        Rush,

        [System.Diagnostics.CodeAnalysis.Experimental("IDIOMA_TEST")]
        Express,
    }

    public sealed class DeliveryLocalizationEn : ILocalizationSet<Delivery>, IDeliveryLocalization
    {
        public string Standard => "Standard";
        public string Rush => "Rush";
        public string Express => "Express";
    }

    public sealed class DeliveryLocalizationNl : ILocalizationSet<Delivery>, IDeliveryLocalization
    {
        public string Standard => "Standaard";
        public string Rush => "Spoed";
        public string Express => "Snel";
    }

    /// <summary>
    /// A helper next to the generated texts: the generated class is partial.
    /// </summary>
    public static partial class GeneratedLocalization
    {
        public static string ShoutCheckout() => Checkout.ToUpperInvariant();
    }

    /// <summary>
    /// Texts through the factory the generator writes for the languages of this assembly.
    /// </summary>
    public static class InlineTexts
    {
        public static string Orders => LocalizedText.Inline(en: "Orders", nl: "Bestellingen");
    }
}
#endregion

namespace CodeChops.Idioma.Tests
{
    using GeneratedSets;
    using TestHelpers;

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

            // Act
            string checkout = GeneratedLocalization.Checkout;

            // Assert: a text is a string.
            checkout.ShouldBe("Afrekenen");
        }

        [Test]
        public void GeneratedText_ShouldHoldWordingOfTheMomentItIsRead()
        {
            // Arrange: read for an English user, as a text held in a field is.
            string checkout;
            string items;

            using (new UICultureScope("en-US"))
            {
                checkout = GeneratedLocalization.Checkout;
                items = GeneratedLocalization.ItemsInCart(3, "Idioma");
            }

            // Act & Assert: the strings stay English for a Dutch user, and reading the texts again gives Dutch.
            using var _ = new UICultureScope("nl-NL");

            checkout.ShouldBe("Pay");
            items.ShouldBe("3 items in your cart at Idioma");
            GeneratedLocalization.Checkout.ShouldBe("Afrekenen");
            GeneratedLocalization.ItemsInCart(3, "Idioma").ShouldBe("3 artikelen in je winkelwagen bij Idioma");
        }

        [Test]
        public void OverrideSet_ShouldWinOverSetItDerivesFrom()
        {
            // Arrange
            using var _ = new UICultureScope("en-US");

            // Act & Assert: the reworded text, and the inherited one.
            GeneratedLocalization.Checkout.ShouldBe("Pay");
            GeneratedLocalization.Greeting.ShouldBe("Hello!");
        }

        [Test]
        public void GeneratedTextThatTakesValues_ShouldBeWordedBySetOfCultureWithThoseValues()
        {
            // Arrange
            using var _ = new UICultureScope("nl-NL");

            // Act & Assert: one value, a wording chosen by a number, a wording chosen by an enum, and a value of a
            // type of another namespace than the set's.
            GeneratedLocalization.Welcome("Max").ShouldBe("Welkom, Max!");
            GeneratedLocalization.ItemsInCart(1, "Idioma").ShouldBe("1 artikel in je winkelwagen bij Idioma");
            GeneratedLocalization.ItemsInCart(count: 3, shop: "Idioma").ShouldBe("3 artikelen in je winkelwagen bij Idioma");
            GeneratedLocalization.Dear(Salutation.Madam).ShouldBe("Geachte mevrouw");
            GeneratedLocalization.Dear(Salutation.Unknown).ShouldBe("Geachte klant");
            GeneratedLocalization.DeliveredOn(new DateOnly(2026, 11, 15)).ShouldBe("Bezorgd op 2026-11-15");
        }

        [Test]
        public void GeneratedTextThatTakesValues_ShouldTakeRewordingAndFallBackLikeAnyText()
        {
            // Arrange & Act & Assert: the set rewording Welcome wins in English, the text it leaves alone keeps the
            // wording of the set it derives from, and a culture without a set gets the default language.
            using (new UICultureScope("en-US"))
            {
                GeneratedLocalization.Welcome("Max").ShouldBe("Good to see you, Max!");
                GeneratedLocalization.Dear(Salutation.Sir).ShouldBe("Dear sir");
            }

            using (new UICultureScope("fr-FR"))
                GeneratedLocalization.Welcome("Max").ShouldBe("Good to see you, Max!");
        }

        [Test]
        public void GeneratedTextThatTakesValues_ShouldFormatValuesAsTheThreadDoes()
        {
            // Arrange & Act & Assert: an American user, a Dutch one, and one who reads Dutch and writes numbers as an
            // American does. The wording follows the UI culture, and the number the culture.
            using (new CulturesScope(uiCulture: "en-US", culture: "en-US"))
                GeneratedLocalization.Total(1234.5m).ShouldBe("Total: 1,234.50");

            using (new CulturesScope(uiCulture: "nl-NL", culture: "nl-NL"))
                GeneratedLocalization.Total(1234.5m).ShouldBe("Totaal: 1.234,50");

            using (new CulturesScope(uiCulture: "nl-NL", culture: "en-US"))
                GeneratedLocalization.Total(1234.5m).ShouldBe("Totaal: 1,234.50");
        }

        [Test]
        [NotInParallel(nameof(LocalizationRegistry))]
        public void GeneratedTextThatTakesValues_WithCurrentUICultureProvider_ShouldBeWordedAndFormattedForItsCulture()
        {
            // Arrange: both cultures of the thread are American, as on a server, and the app keeps a Dutch user's
            // culture somewhere else.
            using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-US");

            // Act & Assert: in wording and in number, for a text read before the provider was set too.
            GeneratedLocalization.Total(1234.5m).ShouldBe("Total: 1,234.50");

            using (new ProvidedCultureScope("nl-NL"))
            {
                GeneratedLocalization.Total(1234.5m).ShouldBe("Totaal: 1.234,50");
                GeneratedLocalization.Checkout.ShouldBe("Afrekenen");
                InlineTexts.Orders.ShouldBe("Bestellingen");
                CultureInfo.CurrentCulture.Name.ShouldBe("en-US");
                CultureInfo.CurrentUICulture.Name.ShouldBe("en-US");
            }

            GeneratedLocalization.Total(1234.5m).ShouldBe("Total: 1,234.50");
        }

        [Test]
        [NotInParallel(nameof(LocalizationRegistry))]
        public void GeneratedTextThatTakesValues_WithCurrentUICultureProviderGivingCultureThreadDoesNotHave_ShouldAskItOncePerText()
        {
            // Arrange: a Dutch user on an American thread. Such a text is worded in a scope on every read, so it
            // never reads a kept set.
            using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-US");
            using var provider = new ProvidedCultureScope("nl-NL");

            // Act: the first read, and one after it.
            var first = GeneratedLocalization.Total(1234.5m);
            var callsOfFirst = provider.CallCount;
            var second = GeneratedLocalization.Total(1234.5m);

            // Assert
            first.ShouldBe("Totaal: 1.234,50");
            second.ShouldBe("Totaal: 1.234,50");
            callsOfFirst.ShouldBe(1);
            provider.CallCount.ShouldBe(2);
        }

        [Test]
        public void GeneratedClass_ShouldGiveDisplayAndValidationAttributesTheirWording()
        {
            // Arrange: .NET's own attributes, which read a public static string property of a public class by its name.
            var display = new System.ComponentModel.DataAnnotations.DisplayAttribute
            {
                Name = nameof(GeneratedLocalization.Checkout),
                ResourceType = typeof(GeneratedLocalization),
            };

            var required = new System.ComponentModel.DataAnnotations.RequiredAttribute
            {
                ErrorMessageResourceName = nameof(GeneratedLocalization.Checkout),
                ErrorMessageResourceType = typeof(GeneratedLocalization),
            };

            // Act: each reads the text when it's asked, so it follows the user's language.
            string dutchName, dutchMessage, englishName, englishMessage, dutch, english;

            using (new CulturesScope("nl-NL"))
            {
                dutch = GeneratedLocalization.Checkout;
                dutchName = display.GetName()!;
                dutchMessage = required.FormatErrorMessage("Name");
            }

            using (new CulturesScope("en-US"))
            {
                english = GeneratedLocalization.Checkout;
                englishName = display.GetName()!;
                englishMessage = required.FormatErrorMessage("Name");
            }

            // Assert
            String.Equals(dutch, english, StringComparison.Ordinal).ShouldBeFalse();
            dutchName.ShouldBe(dutch);
            dutchMessage.ShouldBe(dutch);
            englishName.ShouldBe(english);
            englishMessage.ShouldBe(english);
        }

        [Test]
        public void TextAmongValuesOfGeneratedText_ShouldBeWordedWhereItIsPassed()
        {
            // Arrange & Act & Assert: a text among the values is a string, worded for the reader before the call.
            using (new UICultureScope("en-US"))
                GeneratedLocalization.Removed(InlineTexts.Orders).ShouldBe("Orders removed");

            using (new UICultureScope("nl-NL"))
                GeneratedLocalization.Removed(InlineTexts.Orders).ShouldBe("Bestellingen verwijderd");
        }

        [Test]
        public void TextThatASetReadsItself_ShouldFollowTheLanguageOfTheTextAroundIt()
        {
            // Arrange & Act & Assert: the Dutch set reads the Dutch wording, and the English one the rewording.
            using (new UICultureScope("nl-NL"))
                GeneratedLocalization.Hint.ShouldBe("Druk op Afrekenen om te betalen");

            using (new UICultureScope("en-GB"))
                GeneratedLocalization.Hint.ShouldBe("Press Pay to pay");

            // A culture the app doesn't show gets the default language for both.
            using (new UICultureScope("fr-FR"))
                GeneratedLocalization.Hint.ShouldBe("Press Pay to pay");
        }

        [Test]
        public void CultureWithoutSet_ShouldGetSetOfDefaultLanguage()
        {
            // Arrange: no French set, so the English one serves it, reworded text included.
            using var _ = new UICultureScope("fr-FR");

            // Act & Assert
            GeneratedLocalization.Checkout.ShouldBe("Pay");
        }

        [Test]
        public void ExcludedSet_ShouldNotRegisterItself()
        {
            // Arrange
            var sets = LocalizationRegistry.Sets;

            // Act & Assert: the other sets did, and Dutch keeps their wording.
            sets.ShouldContain(set => set is GeneratedLocalizationNl);
            sets.ShouldNotContain(set => set is UnregisteredGeneratedLocalizationNl);

            using var _ = new UICultureScope("nl-NL");
            GeneratedLocalization.Checkout.ShouldBe("Afrekenen");
        }

        [Test]
        public void SetDerivedFromSetOfItsAssembly_ShouldRegisterAfterIt()
        {
            // Arrange & Act
            var sets = LocalizationRegistry.Sets;
            var english = sets.IndexOf(sets.Single(set => set.GetType() == typeof(GeneratedLocalizationEn)));
            var reworded = sets.IndexOf(sets.Single(set => set is RewordedGeneratedLocalizationEn));

            // Assert
            english.ShouldBeLessThan(reworded);
        }

        [Test]
        public void GeneratedClass_ShouldLetHelperSitNextToTexts()
        {
            // Arrange
            using var _ = new UICultureScope("nl-NL");

            // Act & Assert
            GeneratedLocalization.ShoutCheckout().ShouldBe("AFREKENEN");
        }

        [Test]
        public void GeneratedFactory_ShouldFollowCultureAndFallBackToDefaultLanguage()
        {
            // Arrange & Act & Assert
            using (new UICultureScope("nl-BE"))
                InlineTexts.Orders.ShouldBe("Bestellingen");

            using (new UICultureScope("en-GB"))
                InlineTexts.Orders.ShouldBe("Orders");

            using (new UICultureScope("fr-FR"))
                InlineTexts.Orders.ShouldBe("Orders");
        }

        [Test]
        public void GeneratedFactory_ShouldRefuseNullOnlyForTheWordingItGives()
        {
            // Arrange & Act & Assert: the wording of the user's language is checked, and the others aren't read.
            using (new UICultureScope("nl-NL"))
            {
                Should.Throw<ArgumentNullException>(() => LocalizedText.Inline(en: "Orders", nl: null!)).ParamName.ShouldBe("nl");
                LocalizedText.Inline(en: null!, nl: "Bestellingen").ShouldBe("Bestellingen");
            }

            using (new UICultureScope("en-US"))
            {
                Should.Throw<ArgumentNullException>(() => LocalizedText.Inline(en: null!, nl: "Bestellingen")).ParamName.ShouldBe("en");
                LocalizedText.Inline(en: "Orders", nl: null!).ShouldBe("Orders");
            }
        }

        [Test]
        public void GeneratedFactory_ShouldGiveWordingWithItsValue()
        {
            // Arrange
            const string name = "Max";

            // Act & Assert
            using (new UICultureScope("nl-NL"))
                LocalizedText.Inline(en: $"Hello, {name}!", nl: $"Hallo, {name}!").ShouldBe("Hallo, Max!");

            using (new UICultureScope("en-US"))
                LocalizedText.Inline(en: $"Hello, {name}!", nl: $"Hallo, {name}!").ShouldBe("Hello, Max!");
        }

        [Test]
        public async Task TextsReadByManyUsersAtOnce_ShouldShowEachUserTheirOwnLanguage()
        {
            // Arrange: as on a server, where the work of each user carries its own UI culture, and goes on wherever a
            // thread is free.
            (string Culture, string Checkout, string Welcome, string Orders)[] users =
            [
                ("nl-NL", "Afrekenen", "Welkom, Max!", "Bestellingen"),
                ("en-US", "Pay", "Good to see you, Max!", "Orders"),
                ("nl-BE", "Afrekenen", "Welkom, Max!", "Bestellingen"),
                ("en-GB", "Pay", "Good to see you, Max!", "Orders"),
                ("fr-FR", "Pay", "Good to see you, Max!", "Orders"),
            ];

            // Act
            var wrongReads = await Task.WhenAll(Enumerable.Range(start: 0, count: 64).Select(index => Task.Run(async () =>
            {
                var user = users[index % users.Length];
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(user.Culture);
                var wrong = 0;

                for (var i = 0; i < 2_000; i++)
                {
                    if (!String.Equals(GeneratedLocalization.Checkout, user.Checkout, StringComparison.Ordinal)
                        || !String.Equals(GeneratedLocalization.Welcome("Max"), user.Welcome, StringComparison.Ordinal)
                        || !String.Equals(InlineTexts.Orders, user.Orders, StringComparison.Ordinal))
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
        [NotInParallel(nameof(LocalizationRegistry))]
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

            // Act & Assert: a text of a set, an inline one, a text by its key, and the text of an enum value.
            Allocated(static () => GeneratedLocalization.Checkout).ShouldBe(0, "text of a set");
            Allocated(static () => GeneratedLocalization.Greeting).ShouldBe(0, "text of a set that a rewording leaves as it was");
            Allocated(static () => InlineTexts.Orders).ShouldBe(0, "inline text");
            Allocated(static () => GeneratedLocalization.GetTextByKey("Checkout")).ShouldBe(0, "text of a set, by its key");
            Allocated(static () => OrderStatus.Shipped.GetText()).ShouldBe(0, "text of an enum value");
        }

        [Test]
        [NotInParallel(nameof(LocalizationRegistry))]
        [Arguments("nl-NL")]
        [Arguments("en-US")]
        public void ShowingATextThatTakesValues_ShouldAllocateOnlyItsWording(string culture)
        {
            // Arrange: what the docs promise a text that takes values costs. A reader whose language has a set, so
            // that the text is a call of that set's method.
            using var _ = new CulturesScope(culture);
            const string name = "Max";
            var set = LocalizationRegistry.Find<IGeneratedLocalization>(CultureInfo.CurrentUICulture).ShouldNotBeNull();

            // Act: per call, the least of a few rounds of ten thousand. The wording itself is measured, not written
            // down, because the runtime decides what an interpolated string allocates.
            var shown = Allocated(() => GeneratedLocalization.Welcome(name)) / 10_000;
            var worded = Allocated(() => set.Welcome(name)) / 10_000;
            var counted = Allocated(() => GeneratedLocalization.ItemsInCart(3, name)) / 10_000;
            var countedWorded = Allocated(() => set.ItemsInCart(3, name)) / 10_000;

            // Assert: the string, and nothing that holds the values.
            shown.ShouldBe(worded, "text that takes a value");
            counted.ShouldBe(countedWorded, "text that takes a number and a string");
        }

        [Test]
        [NotInParallel(nameof(LocalizationRegistry))]
        [Arguments("fr-FR", null)]
        [Arguments("en-US", "nl-NL")]
        public void ShowingATextThatTakesValues_InACultureTheThreadDoesNotHave_ShouldAllocateOnlyItsWording(string culture, string? provided)
        {
            // Arrange: a French reader of an app in English and Dutch gets English, and a provider's Dutch reader on an
            // American thread gets Dutch. Either way the set's method runs in a culture the thread doesn't have, which
            // takes switching the culture. The first switch in a context copies it, and the next ones restore that copy.
            using var _ = new CulturesScope(culture);
            using var provider = provided is null ? null : new ProvidedCultureScope(provided);
            const string name = "Max";
            var wording = GeneratedLocalization.Welcome(name);

            // Act: per call, the least of a few rounds of ten thousand.
            var shown = Allocated(() => GeneratedLocalization.Welcome(name)) / 10_000;
            var ofWording = Allocated(() => new string(wording.AsSpan())) / 10_000;

            // Assert: the string alone. The set is kept for the text, so the values are handed to it without a function
            // that holds them, and the context isn't copied.
            shown.ShouldBe(ofWording);
        }

        /// <summary>
        /// The bytes this thread allocates in an action, the least of a few rounds after a warm-up: a round that meets the
        /// JIT or a lazily built table is not what a page that shows texts pays for each time.
        /// </summary>
        internal static long Allocated<T>(Func<T> action)
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
        public void GeneratedGetTextByKey_ShouldGiveTextWithThatKey()
        {
            // Arrange & Act & Assert: the key is the name of the text. English is reworded by a set of its own.
            using (new UICultureScope("nl-NL"))
            {
                GeneratedLocalization.GetTextByKey("Greeting").ShouldBe("Hallo!");
                GeneratedLocalization.GetTextByKey(nameof(GeneratedLocalization.Greeting)).ShouldBe(GeneratedLocalization.Greeting);
            }

            using (new UICultureScope("en-GB"))
            {
                GeneratedLocalization.GetTextByKey("Greeting").ShouldBe("Hello!");
                GeneratedLocalization.GetTextByKey("Checkout").ShouldBe("Pay");
            }

            using (new UICultureScope("fr-FR"))
                GeneratedLocalization.GetTextByKey("Checkout").ShouldBe("Pay");
        }

        [Test]
        public void GeneratedGetTextByKey_ShouldWordTextThatTakesValuesWithTheValuesPassed()
        {
            // Arrange
            object?[] values = [3, "Idioma"];

            // Act & Assert: one value, two values, values as an array, an enum, and a value that is another text.
            using (new UICultureScope("nl-NL"))
            {
                GeneratedLocalization.GetTextByKey("Welcome", "Max").ShouldBe("Welkom, Max!");
                GeneratedLocalization.GetTextByKey("ItemsInCart", values).ShouldBe("3 artikelen in je winkelwagen bij Idioma");
                GeneratedLocalization.GetTextByKey("Dear", Salutation.Madam).ShouldBe("Geachte mevrouw");
                GeneratedLocalization.GetTextByKey("Removed", GeneratedLocalization.Greeting).ShouldBe("Hallo! verwijderd");
            }

            using (new UICultureScope("en-GB"))
                GeneratedLocalization.GetTextByKey("ItemsInCart", 1, "Idioma").ShouldBe("1 item in your cart at Idioma");
        }

        [Test]
        [Arguments("Chekout")]
        [Arguments("checkout")]
        [Arguments("CodeChops.Idioma.Tests.GeneratedSets.GeneratedLocalization.Checkout")]
        [Arguments("")]
        public void GeneratedGetTextByKey_ShouldThrowForKeyNoTextHas(string key)
        {
            // Arrange & Act: a mistyped key, another letter case, and the full name.
            var exception = Should.Throw<KeyNotFoundException>(() => GeneratedLocalization.GetTextByKey(key));

            // Assert
            exception.Message.ShouldStartWith($"'GeneratedLocalization' has no text with the key '{key}'.");
            Should.Throw<ArgumentNullException>(() => GeneratedLocalization.GetTextByKey(null!));
        }

        [Test]
        public void GeneratedGetTextByKey_ShouldThrowForValuesTheTextDoesNotTake()
        {
            // Arrange & Act & Assert: a value for a text that takes none, too few, too many, and another type.
            Should.Throw<ArgumentException>(() => GeneratedLocalization.GetTextByKey("Checkout", "Max")).Message.ShouldContain("takes no values, but 1 was passed");
            Should.Throw<ArgumentException>(() => GeneratedLocalization.GetTextByKey("Welcome")).Message.ShouldContain("takes 1 value, but 0 were passed");
            Should.Throw<ArgumentException>(() => GeneratedLocalization.GetTextByKey("ItemsInCart", 1, "Idioma", 2)).Message.ShouldContain("takes 2 values, but 3 were passed");
            Should.Throw<ArgumentException>(() => GeneratedLocalization.GetTextByKey("ItemsInCart", 1L, "Idioma")).Message.ShouldContain("takes a value of type 'System.Int32' for 'count', but a value of type 'System.Int64' was passed");
        }

        [Test]
        public void GeneratedTryGetTextByKey_ShouldGiveTextWithThatKeyAndFalseForKeyNoTextHas()
        {
            // Arrange
            using var _ = new UICultureScope("nl-NL");

            // Act & Assert: a text, a text that takes a value, and a key no text has, as one from a database may be.
            GeneratedLocalization.TryGetTextByKey("Greeting", out var greeting).ShouldBeTrue();
            greeting.ShouldBe("Hallo!");
            GeneratedLocalization.TryGetTextByKey("Welcome", out var welcome, "Max").ShouldBeTrue();
            welcome.ShouldBe("Welkom, Max!");
            GeneratedLocalization.TryGetTextByKey("Chekout", out var unknown).ShouldBeFalse();
            unknown.ShouldBeNull();

            // A key that is null and values the text doesn't take are mistakes of the caller, so they throw as before.
            Should.Throw<ArgumentNullException>(() => GeneratedLocalization.TryGetTextByKey(null!, out var ignored));
            Should.Throw<ArgumentException>(() => GeneratedLocalization.TryGetTextByKey("Welcome", out var ignored)).Message.ShouldContain("takes 1 value, but 0 were passed");
        }

        [Test]
        public void GeneratedTextKeys_ShouldListKeyOfEveryText()
        {
            // Arrange & Act
            var keys = GeneratedLocalization.TextKeys;

            // Assert: every key gives a text or asks for its values, and the keys of a set of an enum are its members.
            keys.Contains("Greeting", StringComparer.Ordinal).ShouldBeTrue();
            keys.Contains("ItemsInCart", StringComparer.Ordinal).ShouldBeTrue();
            keys.ShouldAllBe(static key => typeof(IGeneratedLocalization).GetMember(key).Length == 1);
            keys.Length.ShouldBe(typeof(IGeneratedLocalization).GetMembers().Count(static member => member is not System.Reflection.MethodInfo { IsSpecialName: true }));
            OrderStatusLocalization.TextKeys.Contains(nameof(OrderStatus.Shipped), StringComparer.Ordinal).ShouldBeTrue();
        }

        [Test]
        public void GeneratedGetTextByKey_ShouldKnowTextOfEveryEnumMember()
        {
            // Arrange
            using var _ = new UICultureScope("nl-NL");

            // Act & Assert: by the name of the member, as ToString writes it.
            OrderStatusLocalization.GetTextByKey(nameof(OrderStatus.Shipped)).ShouldBe("Verzonden");
            OrderStatusLocalization.GetTextByKey(OrderStatus.Pending.ToString()).ShouldBe("In behandeling");
            OrderStatusLocalization.GetTextByKey("Caption").ShouldBe("Bestelstatus");
        }

        [Test]
        public void GeneratedTextOfEnumValue_ShouldBeTheTextNamedAfterItsMember()
        {
            // Arrange & Act & Assert: the text the class holds for the member, in each user's language.
            using (new UICultureScope("nl-NL"))
            {
                OrderStatusLocalization.GetTextByValue(OrderStatus.Shipped).ShouldBe("Verzonden");
                OrderStatusLocalization.GetTextByValue(OrderStatus.Shipped).ShouldBe(OrderStatusLocalization.Shipped);
            }

            using (new UICultureScope("en-GB"))
                OrderStatusLocalization.GetTextByValue(OrderStatus.Shipped).ShouldBe("Shipped");
        }

        [Test]
        public void GeneratedTextOfEnumValue_ShouldCoverEveryMemberAndKeepOtherTexts()
        {
            // Arrange
            using var _ = new UICultureScope("nl-NL");

            // Act & Assert: every value, and the text that is no member of the enum.
            OrderStatusLocalization.GetTextByValue(OrderStatus.Pending).ShouldBe("In behandeling");
            OrderStatusLocalization.GetTextByValue(OrderStatus.Shipped).ShouldBe("Verzonden");
            OrderStatusLocalization.GetTextByValue(OrderStatus.Delivered).ShouldBe("Bezorgd");
            OrderStatusLocalization.Caption.ShouldBe("Bestelstatus");
        }

        [Test]
        public void GeneratedTextOfEnumValue_OfMembersWithOneValue_ShouldBeTheTextOfTheFirst()
        {
            // Arrange
            using var _ = new UICultureScope("en-US");

            // Act & Assert: both members have a text, and the value gives the first one. The build warns about naming
            // the second (LOC600).
            OrderStatusLocalization.Arrived.ShouldBe("Arrived");
#pragma warning disable LOC600
            OrderStatusLocalization.GetTextByValue(OrderStatus.Arrived).ShouldBe("Delivered");
#pragma warning restore LOC600
        }

        [Test]
        public void GeneratedTextOfEnumValue_ThatIsNoMember_ShouldShowAsToStringWritesIt()
        {
            // Arrange
            using var _ = new UICultureScope("nl-NL");

            // Act & Assert: a combination of flags, and a number no member has.
            OrderStatusLocalization.GetTextByValue(OrderStatus.Pending | OrderStatus.Shipped).ShouldBe("Pending, Shipped");
            OrderStatusLocalization.GetTextByValue((OrderStatus)64).ShouldBe("64");
        }

        [Test]
        [NotInParallel(nameof(LocalizationRegistry))]
        public void GeneratedTextOfEnumValue_ShouldAllocateNothing()
        {
            // Arrange
            using var _ = new UICultureScope("nl-NL");

            // Act: per 10,000 calls.
            var allocated = Allocated(static () => OrderStatusLocalization.GetTextByValue(OrderStatus.Delivered)) / 10_000;

            // Assert
            allocated.ShouldBe(0);
        }

        [Test]
        public void GeneratedExtensionOfEnumValue_ShouldGiveTheTextOfTheValue()
        {
            // Arrange & Act & Assert: the text the class holds for the member, in each user's language.
            using (new UICultureScope("nl-NL"))
            {
                OrderStatus.Shipped.GetText().ShouldBe("Verzonden");
                OrderStatus.Shipped.GetText().ShouldBe(OrderStatusLocalization.Shipped);
            }

            using (new UICultureScope("en-GB"))
                OrderStatus.Shipped.GetText().ShouldBe("Shipped");
        }

        [Test]
        public void GeneratedExtensionOfEnumValue_OfMemberMarkedObsoleteAsError_ShouldGiveTheTextOfTheValue()
        {
            // Arrange
            using var _ = new UICultureScope("nl-NL");

            // Act & Assert: code can't name the member, but stored data may hold its value.
            ((Delivery)1).GetText().ShouldBe("Spoed");
        }

        [Test]
        public void GeneratedExtensionOfEnumValue_ThatIsNoMember_ShouldShowAsToStringWritesIt()
        {
            // Arrange
            using var _ = new UICultureScope("nl-NL");

            // Act & Assert: a combination of flags, and a number no member has.
            (OrderStatus.Pending | OrderStatus.Shipped).GetText().ShouldBe("Pending, Shipped");
            ((OrderStatus)64).GetText().ShouldBe("64");
        }

        [Test]
        [NotInParallel(nameof(LocalizationRegistry))]
        public void GeneratedExtensionOfEnumValue_ShouldAllocateNothing()
        {
            // Arrange
            using var _ = new UICultureScope("nl-NL");

            // Act: per 10,000 calls.
            var allocated = Allocated(static () => OrderStatus.Delivered.GetText()) / 10_000;

            // Assert
            allocated.ShouldBe(0);
        }

        [Test]
        public void ApplicationDeclaringLanguages_ShouldRegisterItsLanguagesAtStart()
        {
            // Arrange & Act: this test project is an application declaring ("en", "nl").
            var languages = LocalizationRegistry.SupportedLanguages;

            // Assert
            languages.ShouldBe(["en", "nl"]);
            LocalizationRegistry.DefaultLanguage.ShouldBe("en");
        }

        [Test]
        [Arguments("en", true)]
        [Arguments("en-GB", true)]
        [Arguments("nl-BE", true)]
        [Arguments("de-DE", false)]
        [Arguments("", false)]
        public void IsServed_ShouldTellWhetherOneOfTheAppLanguagesServesTheCulture(string name, bool expected)
        {
            // Arrange: this test project is an application declaring ("en", "nl"). A region falls back to its language,
            // and the invariant culture falls back to none.
            var culture = CultureInfo.GetCultureInfo(name);

            // Act
            var isServed = LocalizationRegistry.IsServed(culture);

            // Assert
            isServed.ShouldBe(expected);
        }

        [Test]
        public void AssemblyAttributes_ShouldBeInTheAssembly()
        {
            // Arrange
            var assembly = typeof(GeneratedSetsTests).Assembly;

            // Act
            var languages = (SupportedLanguagesAttribute)Attribute.GetCustomAttribute(assembly, typeof(SupportedLanguagesAttribute))!;
            var sets = Attribute.GetCustomAttributes(assembly, typeof(LocalizationSetAttribute)).Cast<LocalizationSetAttribute>().ToArray();
            var inlineTexts = Attribute.GetCustomAttribute(assembly, typeof(InlineTextsAttribute));

            // Assert: what the build of an assembly referencing this one reads.
            languages.Languages.ShouldBe(["en", "nl"]);
            inlineTexts.ShouldNotBeNull();
            sets.ShouldContain(set => set.Contract == "CodeChops.Idioma.Tests.GeneratedSets.IGeneratedLocalization" && set.Language == "en");
            sets.ShouldContain(set => set.Contract == "CodeChops.Idioma.Tests.GeneratedSets.IGeneratedLocalization" && set.Language == "nl");
        }
    }
}
