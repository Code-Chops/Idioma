using System.Runtime.CompilerServices;

namespace CodeChops.Idioma;

/// <summary>
/// Makes the culture of a language the thread's <see cref="CultureInfo.CurrentUICulture"/> while a set words a text in
/// that language, and puts it back afterwards.
/// </summary>
/// <remarks>
/// <para>
/// A text is worded at the call, by a member of the set the user's culture gets. Nearly every text needs no scope,
/// because the thread's UI culture is the user's already. A scope is entered where a text is worded in a language its
/// user may not read, such as a fallback language. A text its wording shows, as in
/// <c>$"Press {ShopLocalization.Checkout} to pay"</c>, then shows in that language too. So does anything else the
/// member of the set reads from the UI culture, such as a resource file.
/// </para>
/// <para>
/// <see cref="CultureInfo.CurrentCulture"/> stays as it is. So the values keep the user's format, because a language
/// says nothing about the region its user writes numbers for.
/// </para>
/// </remarks>
// A scope is disposed in any case, by a using, because the member of a set is the consumer's and may throw.
internal readonly ref struct WordingScope
{
    // The execution context from before the culture changed, which puts the culture back. A culture is an AsyncLocal,
    // so each time one is set, the runtime copies the context with every AsyncLocal in it. That's a few hundred bytes
    // in a web request. Restoring the context copies nothing. It's null where the culture didn't change. It's also
    // null where the flow of the context is suppressed, because Capture gives none then, so the culture is set back.
    private ExecutionContext? PreviousContext { get; }

    // ReSharper disable once InconsistentNaming - Same casing as the property of CultureInfo.
    private CultureInfo? PreviousUICulture { get; }

    /// <summary>
    /// Per execution context, the contexts scopes switched to from it, so that the next scope of the same culture
    /// switches by restoring one. Kept as long as the context they were switched from lives.
    /// </summary>
    // Setting a culture copies the context. Without this table, that would cost every text that falls back a few
    // hundred bytes and most of its time. The texts of one render or request run in one context, and switch to a few
    // cultures, so all but the first of each restore the context the first one made, which copies nothing. A context
    // dies with its flow, and its switched contexts with it, so nothing here keeps the values of a request alive.
    private static ConditionalWeakTable<ExecutionContext, SwitchedContexts> SwitchedFrom { get; } = new();

    /// <param name="culture">
    /// The culture texts follow in the scope. <see langword="null"/> enters no scope, as for a language that has no
    /// culture on this machine, such as under invariant globalization.
    /// </param>
    public WordingScope(CultureInfo? culture)
    {
        var uiCulture = CultureInfo.CurrentUICulture;

        if (culture is null || culture.Equals(uiCulture))
            return;

        var previous = ExecutionContext.Capture();

        this.PreviousContext = previous;
        this.PreviousUICulture = uiCulture;

        // Where the flow is suppressed, there's no context to switch from.
        var switches = previous is null ? null : SwitchedFrom.GetValue(previous, static _ => new SwitchedContexts());

        if (switches?.Find(culture) is { } switched)
        {
            ExecutionContext.Restore(switched);
            return;
        }

        CultureInfo.CurrentUICulture = culture;

        if (switches is not null && ExecutionContext.Capture() is { } context)
            switches.Add(new SwitchedContext(culture, context));
    }

    public void Dispose()
    {
        // Also undoes an AsyncLocal the member of the set changed, which ends with the scope like the culture.
        if (this.PreviousContext is not null)
        {
            ExecutionContext.Restore(this.PreviousContext);
            return;
        }

        if (this.PreviousUICulture is not null)
            CultureInfo.CurrentUICulture = this.PreviousUICulture;
    }

    /// <summary>
    /// The contexts scopes switched to from one execution context, the newest first. A few are kept, because the texts
    /// of one context may take turns between scopes of a few cultures. An example is a text that falls back to the
    /// app's default language next to a library's text that falls back to its own.
    /// </summary>
    // A context flows to every task started in it, so threads may look up and add at once. An addition replaces the
    // array, which nothing changes after, so a thread sees a whole one. Two additions at once may lose one of them,
    // which a later scope adds again.
    private sealed class SwitchedContexts
    {
        private const int Kept = 4;

        private volatile SwitchedContext[] _contexts = [];

        /// <summary>
        /// The context a scope switched to by setting this culture, or <see langword="null"/> where none is kept.
        /// </summary>
        public ExecutionContext? Find(CultureInfo uiCulture)
        {
            foreach (var switched in this._contexts)
                if (ReferenceEquals(switched.UICulture, uiCulture))
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
    /// An execution context with the UI culture a scope set in it.
    /// </summary>
    private sealed class SwitchedContext(CultureInfo uiCulture, ExecutionContext context)
    {
        public ExecutionContext Context { get; } = context;

        /// <summary>
        /// The culture the scope set. It's compared as an object, because a scope switches to the culture object of its
        /// language, which is the same one text after text.
        /// </summary>
        // ReSharper disable once InconsistentNaming - Same casing as the property of CultureInfo.
        public CultureInfo UICulture { get; } = uiCulture;
    }
}
