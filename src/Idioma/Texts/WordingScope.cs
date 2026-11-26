using System.Runtime.CompilerServices;

namespace CodeChops.Idioma;

/// <summary>
/// Makes a culture the one texts follow on this thread while a set words a text for it, and puts back what it changed
/// afterwards. It sets up to three things:
/// <list type="bullet">
/// <item><see cref="Current"/>: the culture of the text being worded.</item>
/// <item><see cref="CultureInfo.CurrentUICulture"/>, for what the member of the set reads from it, and for a text that
/// member shows.</item>
/// <item><see cref="CultureInfo.CurrentCulture"/>, where the values are formatted for a culture the thread doesn't
/// have.</item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// A text is worded at the call, by a member of the set the reader's culture gets. Nearly every text needs no scope,
/// because the thread's cultures are the reader's already. Two situations enter one:
/// </para>
/// <list type="bullet">
/// <item>A text is worded in a language its reader may not read, such as a fallback language. A text its wording shows,
/// as in <c>$"Press {ShopLocalization.Checkout} to pay"</c>, then shows in that language too.</item>
/// <item>A text with parameters is worded for a culture that
/// <see cref="LocalizationRegistry.CurrentUICultureProvider"/> gives and the thread doesn't have. The set's method
/// formats the values as it runs, so it runs in that culture.</item>
/// </list>
/// </remarks>
// A scope is disposed in any case, by a using, because the member of a set is the consumer's and may throw.
internal readonly ref struct WordingScope
{
    /// <summary>
    /// The culture of the scope this thread is in, or <see langword="null"/> outside one. Inside one, a text follows
    /// it rather than <see cref="LocalizationRegistry.CurrentUICultureProvider"/>, and the thread's cultures are set
    /// for it.
    /// </summary>
    // Per thread, which is enough, because a set words a text in one call and the scope ends before that call returns.
    // So a scope never spans an await, and AsyncLocal would only cost more.
    [field: ThreadStatic]
    public static CultureInfo? Current { get; private set; }

    private bool IsEntered { get; }

    private CultureInfo? PreviousEntered { get; }

    // The execution context from before a culture changed, which puts both cultures back in one step. A culture is an
    // AsyncLocal, so each time one is set, the runtime copies the context with every AsyncLocal in it. That's a few
    // hundred bytes in a web request, and setting both cultures back would copy it twice more. Restoring the context
    // copies nothing. It's null where no culture changed. It's also null where the flow of the context is suppressed,
    // because Capture gives none then, so the cultures are set back one by one.
    private ExecutionContext? PreviousContext { get; }

    /// <summary>
    /// Per execution context, that context with the cultures a scope switched to, so that the next scope of those
    /// cultures switches to it by restoring it. Kept as long as the context it was switched from lives.
    /// </summary>
    // Setting a culture copies the context, which cost every text that falls back, or runs in a provider's culture, a
    // few hundred bytes and most of its time. The texts of one render or request run in one context, and fall back to
    // one language, so all but the first restore the context the first one made, which copies nothing. A context dies
    // with its flow, and its switched context with it, so nothing here keeps the values of a request alive.
    private static ConditionalWeakTable<ExecutionContext, SwitchedContext> SwitchedContexts { get; } = new();

    // ReSharper disable once InconsistentNaming - Same casing as the property of CultureInfo.
    private CultureInfo? PreviousUICulture { get; }
    private CultureInfo? PreviousCulture { get; }

    /// <param name="culture">The culture texts follow in the scope.</param>
    /// <param name="valuesCulture">
    /// The culture to format the values for, or <see langword="null"/> to format them as the thread does.
    /// </param>
    public WordingScope(CultureInfo culture, CultureInfo? valuesCulture)
    {
        // The thread's own cultures, for what the member of a set reads from them: the format of a value, a text it
        // shows, and anything else that follows the UI culture, such as a resource file. The UI culture goes first,
        // because only its setter can throw, on the name of a culture of a derived class. Nothing has changed yet then.
        var uiCulture = CultureInfo.CurrentUICulture;
        var currentCulture = CultureInfo.CurrentCulture;
        var changesUiCulture = !culture.Equals(uiCulture);
        var changesCulture = valuesCulture is not null && !CultureFormats.AreAlike(valuesCulture, currentCulture);

        if (changesUiCulture || changesCulture)
        {
            var previous = ExecutionContext.Capture();

            // What the scope sets, with null for a culture it leaves alone, because that's all the context holds. A
            // culture left alone may come from a default of the process, such as
            // CultureInfo.DefaultThreadCurrentCulture, which can change while the context lives.
            var switchedUiCulture = changesUiCulture ? culture : null;
            var switchedCulture = changesCulture ? valuesCulture : null;

            this.PreviousContext = previous;
            this.PreviousUICulture = changesUiCulture ? uiCulture : null;
            this.PreviousCulture = changesCulture ? currentCulture : null;

            if (previous is not null && SwitchedContexts.TryGetValue(previous, out var switched) && switched.Holds(switchedUiCulture, switchedCulture))
            {
                ExecutionContext.Restore(switched.Context);
            }
            else
            {
                if (changesUiCulture)
                    CultureInfo.CurrentUICulture = culture;

                if (changesCulture)
                    CultureInfo.CurrentCulture = valuesCulture!;

                if (previous is not null && ExecutionContext.Capture() is { } context)
                    SwitchedContexts.AddOrUpdate(previous, new SwitchedContext(switchedUiCulture, switchedCulture, context));
            }
        }

        this.PreviousEntered = Current;
        Current = culture;
        this.IsEntered = true;
    }

    /// <summary>
    /// The culture a text follows on this thread. That's the culture of the text being worded, else that of
    /// <see cref="LocalizationRegistry.CurrentUICultureProvider"/>, else <see cref="CultureInfo.CurrentUICulture"/>.
    /// </summary>
    /// <param name="providedCulture">
    /// The provider's culture where the text follows it, else <see langword="null"/>. A provider gives the user's
    /// culture where the thread's cultures aren't the user's, as on a server. So values are formatted for the provided
    /// culture, not as the thread formats them. Inside a scope it's <see langword="null"/>, because the scope set the
    /// thread's format already.
    /// </param>
    /// <param name="takesKept">
    /// Whether to take the provider's answer that <see cref="KeepProvidedCulture"/> kept, in place of asking again.
    /// </param>
    public static CultureInfo GetReaderCulture(out CultureInfo? providedCulture, bool takesKept = false)
    {
        providedCulture = null;

        // Taken once, also where it isn't used, so that no later text finds it.
        var hasKept = takesKept && HasKeptProvided;
        var kept = hasKept ? KeptProvided : null;

        if (hasKept)
        {
            HasKeptProvided = false;
            KeptProvided = null;
        }

        // Without a provider, a scope's culture is the thread's UI culture, so the scope isn't read.
        if (LocalizationRegistry.CurrentUICultureProvider is not { } provider)
            return CultureInfo.CurrentUICulture;

        if (Current is { } entered)
            return entered;

        providedCulture = hasKept ? kept : LocalizationRegistry.GetCultureFromScopeOrProvider(provider);

        var culture = providedCulture ?? CultureInfo.CurrentUICulture;
        return culture;
    }

    // What the provider answered when a text looked for its kept set and found none. The text is worded right after,
    // on this thread, and takes the answer from here. So the provider, which may read a request or a user's profile,
    // is asked once per text.
    [ThreadStatic]
    private static bool HasKeptProvided;

    [ThreadStatic]
    private static CultureInfo? KeptProvided;

    /// <summary>
    /// Keeps the provider's answer for the text that is worded next on this thread
    /// (<see cref="GetReaderCulture"/>).
    /// </summary>
    /// <param name="providedCulture">What the provider gave, which may be <see langword="null"/>.</param>
    public static void KeepProvidedCulture(CultureInfo? providedCulture)
    {
        KeptProvided = providedCulture;
        HasKeptProvided = true;
    }

    public void Dispose()
    {
        if (!this.IsEntered)
            return;

        Current = this.PreviousEntered;

        // Also undoes an AsyncLocal the member of the set changed, which ends with the scope like the cultures. That
        // holds only for a scope that changed a culture. A scope that changed none captured no context, so such a
        // change stays.
        if (this.PreviousContext is not null)
        {
            ExecutionContext.Restore(this.PreviousContext);
            return;
        }

        if (this.PreviousUICulture is not null)
            CultureInfo.CurrentUICulture = this.PreviousUICulture;

        if (this.PreviousCulture is not null)
            CultureInfo.CurrentCulture = this.PreviousCulture;
    }

    /// <summary>
    /// An execution context with the cultures a scope set in it: the UI culture, the culture that formats values, or
    /// both.
    /// </summary>
    private sealed class SwitchedContext(CultureInfo? uiCulture, CultureInfo? culture, ExecutionContext context)
    {
        public ExecutionContext Context { get; } = context;

        // ReSharper disable once InconsistentNaming - Same casing as the property of CultureInfo.
        private CultureInfo? UICulture { get; } = uiCulture;
        private CultureInfo? Culture { get; } = culture;

        /// <summary>
        /// Whether a scope set these cultures in the context, where <see langword="null"/> is one it left alone.
        /// They're compared as objects, because a scope switches to the culture object its language or its provider
        /// gives, which is the same one text after text.
        /// </summary>
        public bool Holds(CultureInfo? uiCulture, CultureInfo? culture)
        {
            var holds = ReferenceEquals(this.UICulture, uiCulture) && ReferenceEquals(this.Culture, culture);
            return holds;
        }
    }
}
