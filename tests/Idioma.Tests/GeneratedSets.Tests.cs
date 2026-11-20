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

    public class GeneratedLocalizationEn : ILocalizationSet, IGeneratedLocalization
    {
        public virtual string Checkout => "Checkout";
        public virtual string Greeting => "Hello!";

        // Texts that take values: one value, a wording chosen by a number next to a value, a wording chosen by an
        // enum of this namespace, a value of a type of another namespace, a value with a format, and a value that is
        // a text itself.
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
        public virtual string Removed(LocalizableText what) => $"{what} removed";
    }

    public sealed class GeneratedLocalizationNl : ILocalizationSet, IGeneratedLocalization
    {
        public string Checkout => "Afrekenen";
        public string Greeting => "Hallo!";
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
        public string Removed(LocalizableText what) => $"{what} verwijderd";
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
        public string Welcome(string name) => $"Goedendag, {name}!";
        public string ItemsInCart(int count, string shop) => $"{count} in de winkelwagen bij {shop}";
        public string Dear(Salutation salutation) => "Beste klant";
        public string DeliveredOn(DateOnly date) => $"Geleverd op {date:yyyy-MM-dd}";
        public string Total(decimal amount) => $"Totaal {amount:N2}";
        public string Removed(LocalizableText what) => $"{what} weg";
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
    /// The texts of an enum: one per member, and one that is no member.
    /// </summary>
    public class OrderStatusLocalizationEn : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
    {
        public virtual string Pending => "Pending";
        public virtual string Shipped => "Shipped";
        public virtual string Delivered => "Delivered";
        public virtual string Arrived => "Arrived";
        public virtual string Caption => "Order status";
    }

    public sealed class OrderStatusLocalizationNl : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
    {
        public string Pending => "In behandeling";
        public string Shipped => "Verzonden";
        public string Delivered => "Bezorgd";
        public string Arrived => "Aangekomen";
        public string Caption => "Bestelstatus";
    }

    /// <summary>
    /// A helper next to the generated texts: the generated class is partial.
    /// </summary>
    public static partial class GeneratedLocalization
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
            GeneratedLocalization.Checkout.ToString().ShouldBe("Afrekenen");
        }

        [Test]
        public void GeneratedText_ShouldConvertToStringAndCompare()
        {
            // Arrange
            using var _ = new UICultureScope("nl-NL");

            // Act
            string assigned = GeneratedLocalization.Checkout;

            // Assert
            assigned.ShouldBe("Afrekenen");
            (GeneratedLocalization.Checkout == "Afrekenen").ShouldBeTrue();
        }

        [Test]
        public void OverrideSet_ShouldWinOverSetItDerivesFrom()
        {
            // Arrange
            using var _ = new UICultureScope("en-US");

            // Act & Assert: the reworded text, and the inherited one.
            GeneratedLocalization.Checkout.ToString().ShouldBe("Pay");
            GeneratedLocalization.Greeting.ToString().ShouldBe("Hello!");
        }

        [Test]
        public void GeneratedTextThatTakesValues_ShouldBeWordedBySetOfCultureWithThoseValues()
        {
            // Arrange
            using var _ = new UICultureScope("nl-NL");

            // Act & Assert: one value, a wording chosen by a number, a wording chosen by an enum, and a value of a
            // type of another namespace than the set's.
            GeneratedLocalization.Welcome("Max").ToString().ShouldBe("Welkom, Max!");
            GeneratedLocalization.ItemsInCart(1, "Idioma").ToString().ShouldBe("1 artikel in je winkelwagen bij Idioma");
            GeneratedLocalization.ItemsInCart(count: 3, shop: "Idioma").ToString().ShouldBe("3 artikelen in je winkelwagen bij Idioma");
            GeneratedLocalization.Dear(Salutation.Madam).ToString().ShouldBe("Geachte mevrouw");
            GeneratedLocalization.Dear(Salutation.Unknown).ToString().ShouldBe("Geachte klant");
            GeneratedLocalization.DeliveredOn(new DateOnly(2026, 11, 15)).ToString().ShouldBe("Bezorgd op 2026-11-15");
        }

        [Test]
        public void GeneratedTextThatTakesValues_ShouldFollowLanguageAfterItIsCreated()
        {
            // Arrange: created for an English user, as a text held in a field is.
            LocalizableText text;
            using (new UICultureScope("en-US"))
                text = GeneratedLocalization.ItemsInCart(3, "Idioma");

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
                GeneratedLocalization.Welcome("Max").ToString().ShouldBe("Good to see you, Max!");
                GeneratedLocalization.Dear(Salutation.Sir).ToString().ShouldBe("Dear sir");
            }

            using (new UICultureScope("fr-FR"))
                GeneratedLocalization.Welcome("Max").ToString().ShouldBe("Good to see you, Max!");
        }

        [Test]
        public void GeneratedTextThatTakesValues_ShouldFormatValuesForCultureItIsShownFor()
        {
            // Arrange: both current cultures are American, as on a server that writes to a Dutch customer.
            var (culture, uiCulture) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");

            try
            {
                var total = GeneratedLocalization.Total(1234.5m);

                // Act & Assert: for the reader of this thread, and for the customer, in wording and in number.
                total.ToString().ShouldBe("Total: 1,234.50");
                total.ToString(CultureInfo.GetCultureInfo("nl-NL")).ShouldBe("Totaal: 1.234,50");
                CultureInfo.CurrentCulture.Name.ShouldBe("en-US");
                CultureInfo.CurrentUICulture.Name.ShouldBe("en-US");
            }
            finally
            {
                (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (culture, uiCulture);
            }
        }

        [Test]
        public void TextAmongValuesOfGeneratedText_ShouldShowInLanguageTheTextIsWordedIn()
        {
            // Arrange: an English reader, and a text whose value is a text itself.
            using var _ = new UICultureScope("en-US");
            var removed = GeneratedLocalization.Removed(InlineTexts.Orders);

            // Act
            var json = JsonSerializer.Serialize(removed);

            // Assert: for the reader, for a culture that is passed, and in the copy of every language that travels
            // through JSON, the value is in the language of the wording around it.
            removed.ToString().ShouldBe("Orders removed");
            removed.ToString(CultureInfo.GetCultureInfo("nl-NL")).ShouldBe("Bestellingen verwijderd");

            using var document = JsonDocument.Parse(json);
            document.RootElement.GetProperty("defaultText").GetString().ShouldBe("Orders removed");
            document.RootElement.GetProperty("translations").GetProperty("en").GetString().ShouldBe("Orders removed");
            document.RootElement.GetProperty("translations").GetProperty("nl").GetString().ShouldBe("Bestellingen verwijderd");
            CultureInfo.CurrentUICulture.Name.ShouldBe("en-US");
        }

        [Test]
        public void GeneratedTextThatTakesValues_ShouldCarryKeyContractAndArguments()
        {
            // Arrange & Act
            var text = GeneratedLocalization.ItemsInCart(3, "Idioma");

            // Assert: the key is the name of the method, and the full key its full name, without its parameters.
            text.Key.ShouldBe("ItemsInCart");
            text.FullKey.ShouldBe("CodeChops.Idioma.Tests.GeneratedSets.GeneratedLocalization.ItemsInCart");
            text.Contract.ShouldBe(typeof(IGeneratedLocalization));
            text.DefaultLanguage.ShouldBe("en");
            text.DefaultText.ShouldBe("3 items in your cart at Idioma");
            text.Arguments.ShouldBe([3, "Idioma"]);
            GeneratedLocalization.Checkout.Arguments.ShouldBeEmpty();

            // As an app fills in wording it looked up by the key (docs/coming-from-resx.md).
            String.Format(CultureInfo.InvariantCulture, "{0} at {1}", text.Arguments.AsSpan()).ShouldBe("3 at Idioma");
        }

        [Test]
        public void GeneratedTextsThatTakeValues_ShouldBeEqualWhenTheyReadTheSame()
        {
            // Arrange: an English user. The English set words both an unknown salutation and one it has no case for as
            // "Dear customer".
            using var _ = new UICultureScope("en-US");

            // Act & Assert: also where the values differ.
            GeneratedLocalization.ItemsInCart(3, "Idioma").ShouldBe(GeneratedLocalization.ItemsInCart(3, "Idioma"));
            GeneratedLocalization.ItemsInCart(3, "Idioma").GetHashCode().ShouldBe(GeneratedLocalization.ItemsInCart(3, "Idioma").GetHashCode());
            GeneratedLocalization.ItemsInCart(3, "Idioma").ShouldNotBe(GeneratedLocalization.ItemsInCart(4, "Idioma"));
            GeneratedLocalization.Dear(Salutation.Unknown).ShouldBe(GeneratedLocalization.Dear((Salutation)99));
            GeneratedLocalization.Dear(Salutation.Madam).ShouldNotBe(GeneratedLocalization.Dear(Salutation.Sir));
        }

        [Test]
        public void GeneratedTextThatTakesValues_ShouldTravelThroughJsonWithItsWordingInEveryLanguage()
        {
            // Arrange
            var text = GeneratedLocalization.ItemsInCart(3, "Idioma");

            // Act
            var json = JsonSerializer.Serialize(text);
            var read = JsonSerializer.Deserialize<LocalizableText>(json)!;

            // Assert: worded with its values when it is written, so that it needs neither the sets nor the values to
            // show each language once it is read back.
            using var document = JsonDocument.Parse(json);
            var translations = document.RootElement.GetProperty("translations");
            translations.GetProperty("en").GetString().ShouldBe("3 items in your cart at Idioma");
            translations.GetProperty("nl").GetString().ShouldBe("3 artikelen in je winkelwagen bij Idioma");

            // Without its key, because it's read back without its values: a resource file entry found by the key
            // would show its placeholders as written.
            document.RootElement.TryGetProperty("key", out _).ShouldBeFalse();
            text.Key.ShouldNotBeNull();
            read.Key.ShouldBeNull();
            read.Arguments.ShouldBeEmpty();
            read.ToString(CultureInfo.GetCultureInfo("nl-NL")).ShouldBe("3 artikelen in je winkelwagen bij Idioma");
            read.ToString(CultureInfo.GetCultureInfo("en-US")).ShouldBe("3 items in your cart at Idioma");

            // Equal to the text it was written from, because both show the same wording.
            using (new UICultureScope("nl-NL"))
                read.ShouldBe(text);

            using (new UICultureScope("en-US"))
                read.ShouldBe(text);
        }

        [Test]
        public void CultureWithoutSet_ShouldGetSetOfDefaultLanguage()
        {
            // Arrange: no French set, so the English one serves it, reworded text included.
            using var _ = new UICultureScope("fr-FR");

            // Act & Assert
            GeneratedLocalization.Checkout.ToString().ShouldBe("Pay");
        }

        [Test]
        public void ExcludedSet_ShouldNotRegisterItself()
        {
            // Arrange
            var sets = LocalizationRegistry.Current.Sets;

            // Act & Assert: the other sets did, and Dutch keeps their wording.
            sets.ShouldContain(set => set is GeneratedLocalizationNl);
            sets.ShouldNotContain(set => set is UnregisteredGeneratedLocalizationNl);

            using var _ = new UICultureScope("nl-NL");
            GeneratedLocalization.Checkout.ToString().ShouldBe("Afrekenen");
        }

        [Test]
        public void SetDerivedFromSetOfItsAssembly_ShouldRegisterAfterIt()
        {
            // Arrange & Act
            var sets = LocalizationRegistry.Current.Sets;
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
                    if (!String.Equals(GeneratedLocalization.Checkout.ToString(), user.Checkout, StringComparison.Ordinal)
                        || !String.Equals(GeneratedLocalization.Welcome("Max").ToString(), user.Welcome, StringComparison.Ordinal)
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
            var uiCulture = CultureInfo.CurrentUICulture;
            var checkout = GeneratedLocalization.Checkout;
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

        [Test]
        [NotInParallel(nameof(LocalizationRegistry))]
        public void CreatingATextThatTakesValues_ShouldAllocateOnlyTheTextAndItsValues()
        {
            // Arrange: what the docs promise a text that takes values costs. A Dutch reader, whose language has one
            // set, so that showing the text words it once.
            using var _ = new UICultureScope("nl-NL");
            const string name = "Max";

            // Act: per call, the least of a few rounds of ten thousand.
            var created = Allocated(() => GeneratedLocalization.Welcome(name)) / 10_000;
            var shown = Allocated(() => GeneratedLocalization.Welcome(name).ToString()) / 10_000;
            var counted = Allocated(() => GeneratedLocalization.ItemsInCart(3, name)) / 10_000;

            // Assert: the text itself (48 bytes) and the object that holds its key, contract, and values (72 for one
            // string), and no function that holds the values, no default wording, and no default language. Shown, the
            // wording besides ("Welkom, Max!", 48). A number is kept as an int, not boxed: 8 bytes more for it, where a
            // box is 24.
            created.ShouldBeLessThanOrEqualTo(120, "text that takes a value, created");
            shown.ShouldBeLessThanOrEqualTo(168, "text that takes a value, created and shown");
            counted.ShouldBeLessThanOrEqualTo(128, "text that takes a number and a string, created");
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
            // Arrange
            using var _ = new UICultureScope("nl-NL");

            // Act & Assert: the key is what the text itself gives as its key. English is reworded by a set of its own.
            GeneratedLocalization.GetTextByKey("Greeting").ToString().ShouldBe("Hallo!");
            GeneratedLocalization.GetTextByKey(GeneratedLocalization.Greeting.Key!).ToString().ShouldBe("Hallo!");
            GeneratedLocalization.GetTextByKey("Greeting").ShouldBeSameAs(GeneratedLocalization.Greeting);
            GeneratedLocalization.GetTextByKey("Greeting").ToString(CultureInfo.GetCultureInfo("en-GB")).ShouldBe("Hello!");
            GeneratedLocalization.GetTextByKey("Checkout").ToString(CultureInfo.GetCultureInfo("en-GB")).ShouldBe("Pay");
            GeneratedLocalization.GetTextByKey("Checkout").ToString(CultureInfo.GetCultureInfo("fr-FR")).ShouldBe("Pay");
        }

        [Test]
        public void GeneratedGetTextByKey_ShouldWordTextThatTakesValuesWithTheValuesPassed()
        {
            // Arrange
            using var _ = new UICultureScope("nl-NL");
            object?[] values = [3, "Idioma"];

            // Act & Assert: one value, two values, values as an array, an enum, and a value that is a text itself.
            GeneratedLocalization.GetTextByKey("Welcome", "Max").ToString().ShouldBe("Welkom, Max!");
            GeneratedLocalization.GetTextByKey("ItemsInCart", 1, "Idioma").ToString(CultureInfo.GetCultureInfo("en-GB")).ShouldBe("1 item in your cart at Idioma");
            GeneratedLocalization.GetTextByKey("ItemsInCart", values).ToString().ShouldBe("3 artikelen in je winkelwagen bij Idioma");
            GeneratedLocalization.GetTextByKey("Dear", Salutation.Madam).ToString().ShouldBe("Geachte mevrouw");
            GeneratedLocalization.GetTextByKey("Removed", GeneratedLocalization.Greeting).ToString().ShouldBe("Hallo! verwijderd");
        }

        [Test]
        [Arguments("Chekout")]
        [Arguments("checkout")]
        [Arguments("CodeChops.Idioma.Tests.GeneratedSets.GeneratedLocalization.Checkout")]
        [Arguments("")]
        public void GeneratedGetTextByKey_ShouldThrowForKeyNoTextHas(string key)
        {
            // Arrange & Act: a mistyped key, another letter case, and the full key.
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
        public void GeneratedGetTextByKey_ShouldKnowTextOfEveryEnumMember()
        {
            // Arrange
            using var _ = new UICultureScope("nl-NL");

            // Act & Assert: by the name of the member, as ToString writes it.
            OrderStatusLocalization.GetTextByKey(nameof(OrderStatus.Shipped)).ToString().ShouldBe("Verzonden");
            OrderStatusLocalization.GetTextByKey(OrderStatus.Pending.ToString()).ToString().ShouldBe("In behandeling");
            OrderStatusLocalization.GetTextByKey("Caption").ToString().ShouldBe("Bestelstatus");
        }

        [Test]
        public void InlineText_ShouldHaveNoKey()
        {
            // Arrange & Act
            var text = InlineTexts.Orders;

            // Assert
            text.Key.ShouldBeNull();
            text.FullKey.ShouldBeNull();
        }

        [Test]
        public void GeneratedTextOfEnumValue_ShouldBeTheTextNamedAfterItsMember()
        {
            // Arrange
            using var _ = new UICultureScope("nl-NL");

            // Act
            var text = OrderStatusLocalization.GetTextByValue(OrderStatus.Shipped);

            // Assert: the text the class holds for the member, with the member's name as its key.
            text.ShouldBeSameAs(OrderStatusLocalization.Shipped);
            text.Key.ShouldBe("Shipped");
            text.FullKey.ShouldBe("CodeChops.Idioma.Tests.GeneratedSets.OrderStatusLocalization.Shipped");
            text.ToString().ShouldBe("Verzonden");
            text.ToString(CultureInfo.GetCultureInfo("en-GB")).ShouldBe("Shipped");
        }

        [Test]
        public void GeneratedTextOfEnumValue_ShouldCoverEveryMemberAndKeepOtherTexts()
        {
            // Arrange
            using var _ = new UICultureScope("nl-NL");

            // Act & Assert: every value, and the text that is no member of the enum.
            OrderStatusLocalization.GetTextByValue(OrderStatus.Pending).ToString().ShouldBe("In behandeling");
            OrderStatusLocalization.GetTextByValue(OrderStatus.Shipped).ToString().ShouldBe("Verzonden");
            OrderStatusLocalization.GetTextByValue(OrderStatus.Delivered).ToString().ShouldBe("Bezorgd");
            OrderStatusLocalization.Caption.ToString().ShouldBe("Bestelstatus");
        }

        [Test]
        public void GeneratedTextOfEnumValue_OfMembersWithOneValue_ShouldBeTheTextOfTheFirst()
        {
            // Arrange
            using var _ = new UICultureScope("en-US");

            // Act & Assert: both members have a text, and the value gives the first one.
            OrderStatusLocalization.Arrived.ToString().ShouldBe("Arrived");
            OrderStatusLocalization.GetTextByValue(OrderStatus.Arrived).ShouldBeSameAs(OrderStatusLocalization.Delivered);
        }

        [Test]
        public void GeneratedTextOfEnumValue_ThatIsNoMember_ShouldShowAsToStringWritesIt()
        {
            // Arrange
            using var _ = new UICultureScope("nl-NL");

            // Act & Assert: a combination of flags, and a number no member has.
            OrderStatusLocalization.GetTextByValue(OrderStatus.Pending | OrderStatus.Shipped).ToString().ShouldBe("Pending, Shipped");
            OrderStatusLocalization.GetTextByValue((OrderStatus)64).ToString().ShouldBe("64");
        }

        [Test]
        public void GeneratedTextOfEnumValue_ShouldAllocateNothing()
        {
            // Arrange & Act: per 10,000 calls.
            var allocated = Allocated(static () => OrderStatusLocalization.GetTextByValue(OrderStatus.Delivered)) / 10_000;

            // Assert
            allocated.ShouldBe(0);
        }

        [Test]
        public void GeneratedExtensionOfEnumValue_ShouldGiveTheTextOfTheValue()
        {
            // Arrange
            using var _ = new UICultureScope("nl-NL");

            // Act
            var text = OrderStatus.Shipped.GetText();

            // Assert: the text the class holds for the member, in the user's language and in another.
            text.ShouldBeSameAs(OrderStatusLocalization.Shipped);
            text.ToString().ShouldBe("Verzonden");
            text.ToString(CultureInfo.GetCultureInfo("en-GB")).ShouldBe("Shipped");
        }

        [Test]
        public void GeneratedExtensionOfEnumValue_ThatIsNoMember_ShouldShowAsToStringWritesIt()
        {
            // Arrange
            using var _ = new UICultureScope("nl-NL");

            // Act & Assert: a combination of flags, and a number no member has.
            (OrderStatus.Pending | OrderStatus.Shipped).GetText().ToString().ShouldBe("Pending, Shipped");
            ((OrderStatus)64).GetText().ToString().ShouldBe("64");
        }

        [Test]
        public void GeneratedExtensionOfEnumValue_ShouldAllocateNothing()
        {
            // Arrange & Act: per 10,000 calls.
            var allocated = Allocated(static () => OrderStatus.Delivered.GetText()) / 10_000;

            // Assert
            allocated.ShouldBe(0);
        }

        [Test]
        public void GeneratedFactory_ShouldHoldEveryLanguage()
        {
            // Arrange & Act
            var text = InlineTexts.Orders;

            // Assert: the first is the default wording, and every language, the default included, a translation.
            text.DefaultText.ShouldBe("Orders");
            text.TranslationsByLanguage.Keys.ShouldBe(["en", "nl"], ignoreOrder: true);
            text.TranslationsByLanguage["en"].ShouldBe("Orders");
            text.TranslationsByLanguage["nl"].ShouldBe("Bestellingen");
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
