using System.Runtime.CompilerServices;

namespace CodeChops.Idioma;

/// <summary>
/// Makes the culture of a language the thread's <see cref="CultureInfo.CurrentUICulture"/> while a set words a text in
/// that language, such as a fallback language, and puts it back afterwards. So a text the wording shows, or a resource
/// the set's member reads, follows that language too.
/// </summary>
/// <remarks>
/// <see cref="CultureInfo.CurrentCulture"/> stays as it is, so values keep the user's format.
/// </remarks>
// Always disposed by a using, because the set's member is the consumer's and may throw.
internal readonly ref struct WordingScope
{
    // Restoring the context from before puts the culture back without copying it, as setting an AsyncLocal does. It's
    // null where the culture didn't change, or where the flow is suppressed, and then the culture is set back.
    private ExecutionContext? PreviousContext { get; }

    // ReSharper disable once InconsistentNaming - Same casing as the property of CultureInfo.
    private CultureInfo? PreviousUICulture { get; }

    /// <summary>
    /// Per execution context, the contexts scopes switched to from it, so the next scope of the same culture switches
    /// by restoring one, which copies nothing.
    /// </summary>
    // Weak, so a context's switched contexts die with it, and nothing here keeps the values of a request alive.
    private static ConditionalWeakTable<ExecutionContext, SwitchedContexts> SwitchedFrom { get; } = new();

    /// <param name="culture">
    /// <see langword="null"/> enters no scope, as for a language that has no culture on this machine.
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
    /// The contexts scopes switched to from one execution context, the newest first. A few are kept, because a context's
    /// texts may take turns between a few cultures.
    /// </summary>
    // Threads may look up and add at once, because a context flows to every task started in it. An addition replaces
    // the array, so a thread sees a whole one. Two additions at once may lose one, which a later scope adds again.
    private sealed class SwitchedContexts
    {
        private const int Kept = 4;

        private volatile SwitchedContext[] _contexts = [];

        public ExecutionContext? Find(CultureInfo uiCulture)
        {
            foreach (var switched in this._contexts)
                if (ReferenceEquals(switched.UICulture, uiCulture))
                    return switched.Context;

            return null;
        }

        /// <summary>
        /// Replaces the oldest where <see cref="Kept"/> are kept already.
        /// </summary>
        public void Add(SwitchedContext switched)
        {
            var contexts = this._contexts;
            var added = new SwitchedContext[Math.Min(contexts.Length + 1, Kept)];

            added[0] = switched;
            Array.Copy(contexts, sourceIndex: 0, added, destinationIndex: 1, added.Length - 1);

            this._contexts = added;
        }
    }

    private sealed class SwitchedContext(CultureInfo uiCulture, ExecutionContext context)
    {
        public ExecutionContext Context { get; } = context;

        /// <summary>
        /// Compared as an object, because a scope switches to its language's culture object, the same text after text.
        /// </summary>
        // ReSharper disable once InconsistentNaming - Same casing as the property of CultureInfo.
        public CultureInfo UICulture { get; } = uiCulture;
    }
}
