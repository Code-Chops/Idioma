using Architect.AmbientContexts;
using CodeChops.Idioma.Tests.GeneratedSets;
using CodeChops.Idioma.Tests.TestHelpers;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// <see cref="LocalizationRegistry.UseCulture"/> through the generated texts of <see cref="GeneratedLocalization"/>,
/// and the same with an ambient scope of Architect.AmbientContexts, which an app may use in its place.
/// </summary>
public sealed class CultureScopeTests
{
    private static CultureInfo Dutch { get; } = CultureInfo.GetCultureInfo("nl-NL");

    [Test]
    public void UseCulture_ShouldGiveEveryKindOfTextItsWordingAndFormatValuesForTheCulture()
    {
        // Arrange
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-US");
        var before = GeneratedLocalization.Total(1234.5m);

        // Act
        string total, checkout, orders;

        using (LocalizationRegistry.UseCulture(Dutch))
        {
            total = GeneratedLocalization.Total(1234.5m);
            checkout = GeneratedLocalization.Checkout;
            orders = InlineTexts.Orders;
        }

        // Assert: Dutch inside the scope, and as before after it.
        before.ShouldBe("Total: 1,234.50");
        total.ShouldBe("Totaal: 1.234,50");
        checkout.ShouldBe("Afrekenen");
        orders.ShouldBe("Bestellingen");
        GeneratedLocalization.Total(1234.5m).ShouldBe("Total: 1,234.50");
        CultureInfo.CurrentUICulture.Name.ShouldBe("en-US");
        CultureInfo.CurrentCulture.Name.ShouldBe("en-US");
    }

    [Test]
    public async Task UseCulture_ShouldHoldAcrossAnAwait()
    {
        // Arrange
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-US");

        // Act: the code goes on at another moment, and maybe on another thread.
        string total;

        using (LocalizationRegistry.UseCulture(Dutch))
        {
            await Task.Delay(1).ConfigureAwait(false);
            await Task.Yield();

            total = GeneratedLocalization.Total(1234.5m);
        }

        // Assert
        total.ShouldBe("Totaal: 1.234,50");
    }

    [Test]
    public void UseCulture_InsideAnotherScope_ShouldWinUntilItIsDisposed()
    {
        // Arrange
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-US");

        // Act
        string inner, outerAfterInner;

        using (LocalizationRegistry.UseCulture(Dutch))
        {
            using (LocalizationRegistry.UseCulture(CultureInfo.GetCultureInfo("en-US")))
                inner = GeneratedLocalization.Total(1234.5m);

            outerAfterInner = GeneratedLocalization.Total(1234.5m);
        }

        // Assert
        inner.ShouldBe("Total: 1,234.50");
        outerAfterInner.ShouldBe("Totaal: 1.234,50");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void UseCulture_WithCurrentUICultureProvider_ShouldWinOverItWithoutAskingIt()
    {
        // Arrange: an app that keeps an American user's culture somewhere else, on a thread of another culture.
        using var _ = new CulturesScope(uiCulture: "en-GB", culture: "en-GB");
        using var provider = new ProvidedCultureScope("en-US");

        // Act
        string total, checkout, orders;
        int calls;

        using (LocalizationRegistry.UseCulture(Dutch))
        {
            var callsBefore = provider.CallCount;

            total = GeneratedLocalization.Total(1234.5m);
            checkout = GeneratedLocalization.Checkout;
            orders = InlineTexts.Orders;

            calls = provider.CallCount - callsBefore;
        }

        // Assert: the scope's culture, and the provider's again after it.
        total.ShouldBe("Totaal: 1.234,50");
        checkout.ShouldBe("Afrekenen");
        orders.ShouldBe("Bestellingen");
        calls.ShouldBe(0);
        GeneratedLocalization.Total(1234.5m).ShouldBe("Total: 1,234.50");
    }

    [Test]
    public void UseCulture_DisposedTwice_ShouldPutCulturesBackOnce()
    {
        // Arrange
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-US");
        var scope = LocalizationRegistry.UseCulture(Dutch);
        scope.Dispose();

        // Act: a second dispose, after the culture changed again.
        using var inner = new CulturesScope(uiCulture: "nl-NL", culture: "nl-NL");
        scope.Dispose();

        // Assert
        CultureInfo.CurrentUICulture.Name.ShouldBe("nl-NL");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void UseCulture_DisposedBeforeAScopeInsideIt_ShouldEndThatScopeToo()
    {
        // Arrange: a provider, so that a text reads the scope and not only the cultures of the thread.
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-US");
        using var provider = new ProvidedCultureScope("en-US");
        var outer = LocalizationRegistry.UseCulture(CultureInfo.GetCultureInfo("en-GB"));
        var inner = LocalizationRegistry.UseCulture(Dutch);

        // Act: the outer scope ends first, and the inner one ends after it.
        outer.Dispose();
        var uiCultureAfterOuter = CultureInfo.CurrentUICulture.Name;
        var cultureAfterOuter = CultureInfo.CurrentCulture.Name;
        var totalAfterOuter = GeneratedLocalization.Total(1234.5m);

        inner.Dispose();

        // Assert: nothing of either scope is left once the outer one ends, and ending the inner one changes nothing.
        uiCultureAfterOuter.ShouldBe("en-US");
        cultureAfterOuter.ShouldBe("en-US");
        totalAfterOuter.ShouldBe("Total: 1,234.50");
        CultureScope.Current.ShouldBeNull();
        CultureInfo.CurrentUICulture.Name.ShouldBe("en-US");
        GeneratedLocalization.Total(1234.5m).ShouldBe("Total: 1,234.50");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void UseCulture_MiddleOfThreeDisposedFirst_ShouldEndTheInnermostTooAndLeaveTheOutermost()
    {
        // Arrange: a provider, so that a text reads the scope and not only the cultures of the thread.
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-US");
        using var provider = new ProvidedCultureScope("en-US");
        var outermost = LocalizationRegistry.UseCulture(Dutch);
        var middle = LocalizationRegistry.UseCulture(CultureInfo.GetCultureInfo("en-GB"));
        var innermost = LocalizationRegistry.UseCulture(CultureInfo.GetCultureInfo("nl-BE"));

        // Act: the middle scope ends first, then the innermost one, and then the outermost one.
        middle.Dispose();
        var uiCultureInsideOutermost = CultureInfo.CurrentUICulture.Name;
        var cultureInsideOutermost = CultureInfo.CurrentCulture.Name;
        var totalInsideOutermost = GeneratedLocalization.Total(1234.5m);

        innermost.Dispose();
        var uiCultureAfterInnermost = CultureInfo.CurrentUICulture.Name;

        outermost.Dispose();

        // Assert: back in the outermost scope once the middle one ends, and outside every scope once that one ends.
        uiCultureInsideOutermost.ShouldBe("nl-NL");
        cultureInsideOutermost.ShouldBe("nl-NL");
        totalInsideOutermost.ShouldBe("Totaal: 1.234,50");
        uiCultureAfterInnermost.ShouldBe("nl-NL");
        CultureInfo.CurrentUICulture.Name.ShouldBe("en-US");
        CultureInfo.CurrentCulture.Name.ShouldBe("en-US");
        GeneratedLocalization.Total(1234.5m).ShouldBe("Total: 1,234.50");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public async Task UseCulture_DisposedWhileATaskStartedInsideItRuns_ShouldStillHoldInThatTask()
    {
        // Arrange: a provider, so that a text reads the scope and not only the cultures of the thread. An email is
        // started inside the scope, and the code that started it ends the scope before the email reads its texts.
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-US");
        using var provider = new ProvidedCultureScope("en-US");
        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<(string Checkout, string Total, string AfterScopeOfItsOwn)> email;

        using (LocalizationRegistry.UseCulture(Dutch))
        {
            email = Task.Run(async () =>
            {
                await ended.Task;

                var checkout = GeneratedLocalization.Checkout;
                var total = GeneratedLocalization.Total(1234.5m);

                // A scope of its own ends back in the scope the task was started in.
                using (LocalizationRegistry.UseCulture(CultureInfo.GetCultureInfo("en-GB")))
                {
                }

                return (checkout, total, GeneratedLocalization.Checkout);
            });
        }

        // Act
        ended.SetResult();
        var (checkout, total, afterScopeOfItsOwn) = await email;

        // Assert: the email is Dutch throughout, as the cultures of its thread are, and the code that ended the scope
        // follows the provider again.
        checkout.ShouldBe("Afrekenen");
        total.ShouldBe("Totaal: 1.234,50");
        afterScopeOfItsOwn.ShouldBe("Afrekenen");
        GeneratedLocalization.Total(1234.5m).ShouldBe("Total: 1,234.50");
    }

    [Test]
    [NotInParallel]
    public void UseCulture_OnThreadThatFollowsTheDefaultCultures_ShouldLeaveItFollowingThem()
    {
        // Arrange: a thread that sets no culture itself, as the UI thread of a desktop app, which follows the default
        // cultures of the process. The defaults hold for the whole process, so no other test runs next to this one.
        var american = CultureInfo.GetCultureInfo("en-US");
        var german = CultureInfo.GetCultureInfo("de-DE");
        var defaultUICulture = CultureInfo.DefaultThreadCurrentUICulture;
        var defaultCulture = CultureInfo.DefaultThreadCurrentCulture;
        var ownLocal = new AsyncLocal<object?>();
        CultureInfo? uiCultureAfterDefaultChanged = null;
        CultureInfo? cultureAfterDefaultChanged = null;
        CultureInfo? uiCultureAfterScopeThatSetALocal = null;

        // Act
        try
        {
            CultureInfo.DefaultThreadCurrentUICulture = american;
            CultureInfo.DefaultThreadCurrentCulture = american;

            var thread = new Thread(() =>
            {
                using (LocalizationRegistry.UseCulture(Dutch))
                {
                }

                // A language picker that changes the defaults, as a desktop app does.
                CultureInfo.DefaultThreadCurrentUICulture = german;
                CultureInfo.DefaultThreadCurrentCulture = german;

                uiCultureAfterDefaultChanged = CultureInfo.CurrentUICulture;
                cultureAfterDefaultChanged = CultureInfo.CurrentCulture;

                // Code inside the scope that sets an AsyncLocal of its own, which the scope must leave as it is. So it
                // sets the cultures from before it.
                using (LocalizationRegistry.UseCulture(Dutch))
                    ownLocal.Value = new object();

                uiCultureAfterScopeThatSetALocal = CultureInfo.CurrentUICulture;
            });

            using (ExecutionContext.SuppressFlow())
                thread.Start();

            thread.Join();
        }
        finally
        {
            CultureInfo.DefaultThreadCurrentUICulture = defaultUICulture;
            CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
        }

        // Assert
        uiCultureAfterDefaultChanged.ShouldBeSameAs(german);
        cultureAfterDefaultChanged.ShouldBeSameAs(german);
        uiCultureAfterScopeThatSetALocal.ShouldBeSameAs(german);
    }

    [Test]
    public void UseCulture_WithNull_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => LocalizationRegistry.UseCulture(null!)).ParamName.ShouldBe("culture");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public async Task AmbientScopeOfAnotherLibrary_GivenToTheProvider_ShouldGiveTextsItsCulture()
    {
        // Arrange: an app that uses Architect.AmbientContexts gives its own scope to the provider. The provider gives
        // no culture outside such a scope, so the tests that run next to this one keep theirs.
        using var _ = new CulturesScope(uiCulture: "en-US", culture: "en-US");
        var previous = LocalizationRegistry.CurrentUICultureProvider;
        LocalizationRegistry.CurrentUICultureProvider = static () => TextCultureScope.Current?.Culture;

        try
        {
            // Act
            string total, checkout, orders, nested;

            using (new TextCultureScope(Dutch))
            {
                await Task.Yield();

                total = GeneratedLocalization.Total(1234.5m);
                checkout = GeneratedLocalization.Checkout;
                orders = InlineTexts.Orders;

                // A scope of Idioma inside it wins, as it does over any provider.
                using (LocalizationRegistry.UseCulture(CultureInfo.GetCultureInfo("en-US")))
                    nested = GeneratedLocalization.Total(1234.5m);
            }

            // Assert
            total.ShouldBe("Totaal: 1.234,50");
            checkout.ShouldBe("Afrekenen");
            orders.ShouldBe("Bestellingen");
            nested.ShouldBe("Total: 1,234.50");
            GeneratedLocalization.Total(1234.5m).ShouldBe("Total: 1,234.50");
        }
        finally
        {
            LocalizationRegistry.CurrentUICultureProvider = previous;
        }
    }

    /// <summary>
    /// An ambient scope as an app writes it with Architect.AmbientContexts, which holds the culture of its texts.
    /// </summary>
    private sealed class TextCultureScope : AmbientScope<TextCultureScope>
    {
        public static TextCultureScope? Current => GetAmbientScope();

        public CultureInfo Culture { get; }

        public TextCultureScope(CultureInfo culture)
            : base(AmbientScopeOption.ForceCreateNew)
        {
            this.Culture = culture;
            this.Activate();
        }

        protected override void DisposeImplementation()
        {
        }
    }
}
