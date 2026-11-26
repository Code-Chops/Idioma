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
    /// for it. It's also <see langword="null"/> inside a <see cref="CultureScope"/> that the member of the set entered,
    /// because that scope chooses the culture of the texts the member shows, as it does everywhere else.
    /// </summary>
    // It's read only where a provider is set, and it reads the culture scope only inside a scope. So a text outside one
    // reads no AsyncLocal for it.
    public static CultureInfo? Current => EnteredCulture is { } entered && ReferenceEquals(CultureScope.Innermost, EnteredCultureScope)
        ? entered
        : null;

    // Per thread, which is enough, because a set words a text in one call and the scope ends before that call returns.
    // So a scope never spans an await, and AsyncLocal would only cost more. Code the thread runs while the member of a
    // set blocks does see the scope, such as a task that Wait runs inline, or a nested message loop. A text there
    // follows the scope's culture rather than the provider's. That's accepted, because a member that blocks is rare.
    [field: ThreadStatic]
    private static CultureInfo? EnteredCulture { get; set; }

    // The culture scope the code was in when the scope was entered. Another one is a culture scope that the member of
    // the set entered, which ends the scope's say over the culture of a text.
    [field: ThreadStatic]
    private static CultureScope? EnteredCultureScope { get; set; }

    private bool IsEntered { get; }

    private CultureInfo? PreviousEnteredCulture { get; }
    private CultureScope? PreviousEnteredCultureScope { get; }

    // The execution context from before a culture changed, which puts both cultures back in one step. A culture is an
    // AsyncLocal, so each time one is set, the runtime copies the context with every AsyncLocal in it. That's a few
    // hundred bytes in a web request, and setting both cultures back would copy it twice more. Restoring the context
    // copies nothing. It's null where no culture changed. It's also null where the flow of the context is suppressed,
    // because Capture gives none then, so the cultures are set back one by one.
    private ExecutionContext? PreviousContext { get; }

    /// <summary>
    /// Per execution context, the contexts scopes switched to from it, so that the next scope of the same cultures
    /// switches by restoring one. Kept as long as the context they were switched from lives.
    /// </summary>
    // Setting a culture copies the context. Without this table, that would cost every text that falls back, or runs in a
    // provider's culture, a few hundred bytes and most of its time. The texts of one render or request run in one
    // context, and switch to a few cultures, so all but the first of each restore the context the first one made, which
    // copies nothing. A context dies with its flow, and its switched contexts with it, so nothing here keeps the values
    // of a request alive.
    private static ConditionalWeakTable<ExecutionContext, SwitchedContexts> SwitchedFrom { get; } = new();

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

            // Where the flow is suppressed, there's no context to switch from.
            var switches = previous is null ? null : SwitchedFrom.GetValue(previous, static _ => new SwitchedContexts());

            if (switches?.Find(switchedUiCulture, switchedCulture) is { } switched)
            {
                ExecutionContext.Restore(switched);
            }
            else
            {
                if (changesUiCulture)
                    CultureInfo.CurrentUICulture = culture;

                if (changesCulture)
                    CultureInfo.CurrentCulture = valuesCulture!;

                if (switches is not null && ExecutionContext.Capture() is { } context)
                    switches.Add(new SwitchedContext(switchedUiCulture, switchedCulture, context));
            }
        }

        this.PreviousEnteredCulture = EnteredCulture;
        this.PreviousEnteredCultureScope = EnteredCultureScope;
        EnteredCulture = culture;

        // Only Current reads it, which is read only where a provider is set.
        EnteredCultureScope = LocalizationRegistry.CurrentUICultureProvider is null ? null : CultureScope.Innermost;
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
    public static CultureInfo GetReaderCulture(out CultureInfo? providedCulture)
    {
        providedCulture = null;

        // Without a provider, a scope's culture is the thread's UI culture, so the scope isn't read.
        if (LocalizationRegistry.CurrentUICultureProvider is not { } provider)
            return CultureInfo.CurrentUICulture;

        if (Current is { } entered)
            return entered;

        providedCulture = LocalizationRegistry.GetCultureFromScopeOrProvider(provider);

        var culture = providedCulture ?? CultureInfo.CurrentUICulture;
        return culture;
    }

    /// <summary>
    /// The culture a text follows on this thread, as <see cref="GetReaderCulture(out CultureInfo)"/> gives it, where
    /// that was asked already and gave <paramref name="providedCulture"/>. The provider isn't asked again, because it
    /// may read a request or a user's profile.
    /// </summary>
    /// <param name="providedCulture">What <see cref="GetReaderCulture(out CultureInfo)"/> gave as the provider's answer.</param>
    public static CultureInfo GetReaderCulture(CultureInfo? providedCulture)
    {
        if (LocalizationRegistry.CurrentUICultureProvider is null)
            return CultureInfo.CurrentUICulture;

        if (Current is { } entered)
            return entered;

        var culture = providedCulture ?? CultureInfo.CurrentUICulture;
        return culture;
    }

    public void Dispose()
    {
        if (!this.IsEntered)
            return;

        EnteredCulture = this.PreviousEnteredCulture;
        EnteredCultureScope = this.PreviousEnteredCultureScope;

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
    /// The contexts scopes switched to from one execution context, the newest first. A few are kept, because the texts
    /// of one context may take turns between scopes of a few cultures. An example is a text that falls back to English
    /// next to one worded in the culture a provider gives.
    /// </summary>
    // A context flows to every task started in it, so threads may look up and add at once. An addition replaces the
    // array, which nothing changes after, so a reader sees a whole one. Two additions at once may lose one of them, which
    // a later scope adds again.
    private sealed class SwitchedContexts
    {
        private const int Kept = 4;

        private volatile SwitchedContext[] _contexts = [];

        /// <summary>
        /// The context a scope switched to by setting these cultures, or <see langword="null"/> where none is kept.
        /// </summary>
        public ExecutionContext? Find(CultureInfo? uiCulture, CultureInfo? culture)
        {
            foreach (var switched in this._contexts)
                if (switched.Holds(uiCulture, culture))
                    return switched.Context;

            return null;
        }

        /// <summary>
        /// Keeps a context a scope switched to, in place of the oldest where <see cref="Kept"/> are kept already.
        /// </summary>
        public void Add(SwitchedContext switched)
        {
            var contexts = this._contexts;
            var added = new SwitchedContext[Math.Min(contexts.Length + 1, Kept)];

            added[0] = switched;
            Array.Copy(contexts, 0, added, 1, added.Length - 1);

            this._contexts = added;
        }
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
