namespace CodeChops.Idioma;

/// <summary>
/// A block of code in which every text follows a culture you chose, such as that of an email's recipient. Create one
/// with <see cref="LocalizationRegistry.UseCulture"/>, and dispose it to go back to the culture from before.
/// </summary>
/// <remarks>
/// <para>
/// Inside the scope, the culture is both <see cref="CultureInfo.CurrentUICulture"/>, which chooses the wording, and
/// <see cref="CultureInfo.CurrentCulture"/>, which formats dates and numbers. It also wins over
/// <see cref="LocalizationRegistry.CurrentUICultureProvider"/>.
/// </para>
/// <para>
/// The scope follows your code across an <see langword="await"/>. A scope inside another wins until it's disposed.
/// Dispose scopes in the reverse order you created them, as <see langword="using"/> does.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// using (LocalizationRegistry.UseCulture(CultureInfo.GetCultureInfo("nl-NL")))
/// {
///     var order = await orders.FindAsync(id);
///
///     // "Totaal: 1.234,50", whatever the culture of the user who placed the order
///     string line = $"{ShopLocalization.Total}: {order.Amount:N2}";
/// }
/// </code>
/// </example>
public sealed class CultureScope : IDisposable
{
    // The scopes the code that runs now is in, the innermost first. They flow with the code, as the cultures of the
    // thread do, so a scope still holds after an await. Each flow keeps its own: a scope disposed in one, such as the
    // code that started a task, still holds in a task started inside it, whose thread cultures are still the scope's.
    // Read only where a provider is set: without one, the thread's UI culture is the scope's already.
    private static AsyncLocal<OpenScope?> Entered { get; } = new();

    /// <summary>
    /// The culture of the scope the code that runs now is in, or <see langword="null"/> outside one. A text follows
    /// it rather than <see cref="LocalizationRegistry.CurrentUICultureProvider"/>.
    /// </summary>
    internal static CultureInfo? Current => Entered.Value?.Scope.Culture;

    private CultureInfo Culture { get; }

    // ReSharper disable once InconsistentNaming - Same casing as the property of CultureInfo.
    private CultureInfo PreviousUICulture { get; }
    private CultureInfo PreviousCulture { get; }

    // The execution context from before the scope, and the one the scope made. A culture is an AsyncLocal, and a thread
    // that never set one follows the default cultures of the process, such as CultureInfo.DefaultThreadCurrentUICulture.
    // Setting the culture from before back would set it explicitly, so the thread would stop following those defaults.
    // Restoring the context from before leaves it as it was instead. That's done only where the context is still the
    // one the scope made, because restoring it would also undo an AsyncLocal that the code inside the scope set. Both are
    // null where the flow of the context is suppressed, because Capture gives none then.
    private ExecutionContext? ContextBefore { get; }
    private ExecutionContext? ContextEntered { get; }

    internal CultureScope(CultureInfo culture)
    {
        this.Culture = culture;
        this.PreviousUICulture = CultureInfo.CurrentUICulture;
        this.PreviousCulture = CultureInfo.CurrentCulture;

        var before = ExecutionContext.Capture();

        // The UI culture first, because only its setter can throw, on the name of a culture of a derived class.
        // Nothing has changed yet then.
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;

        var surrounding = Entered.Value;

        Entered.Value = new OpenScope(
            this,
            surrounding,
            uiCultureBefore: surrounding?.UICultureBefore ?? this.PreviousUICulture,
            cultureBefore: surrounding?.CultureBefore ?? this.PreviousCulture,
            isOnWhatItFound: true);

        this.ContextBefore = before;
        this.ContextEntered = before is null ? null : ExecutionContext.Capture();
    }

    /// <summary>
    /// Ends the scope. Texts follow the culture from before it again. Disposing a scope twice does nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Dispose a scope in the code that created it, as <see langword="using"/> does. The scope ends there, and in the
    /// code that runs from there on. Code that was started inside it and still runs, such as a task, stays in it until
    /// that code ends.
    /// </para>
    /// <para>
    /// A thread that set no culture itself follows the default cultures of the process, such as
    /// <see cref="CultureInfo.DefaultThreadCurrentUICulture"/>. It still follows them after the scope, unless the code
    /// inside the scope set an <see cref="AsyncLocal{T}"/> of its own. Then the scope sets the cultures from before it,
    /// and the thread keeps them when the defaults change.
    /// </para>
    /// </remarks>
    public void Dispose()
    {
        var innermost = Entered.Value;

        // Disposed already in this flow, or never entered in it.
        if (innermost is null || !innermost.Holds(this))
            return;

        // Disposed before a scope inside it: that scope still holds, and puts back what it found when it ends.
        if (!ReferenceEquals(innermost.Scope, this))
        {
            Entered.Value = innermost.Without(this);
            return;
        }

        // The usual case: nothing changed the context since the scope made it, so the context from before puts back the
        // cultures and the surrounding scope as they were, also a culture the thread didn't set itself.
        if (innermost.IsOnWhatItFound && this.ContextBefore is not null && ReferenceEquals(ExecutionContext.Capture(), this.ContextEntered))
        {
            ExecutionContext.Restore(this.ContextBefore);
            return;
        }

        var surrounding = innermost.Surrounding;
        Entered.Value = surrounding;

        // The usual order: back to what this scope found.
        if (innermost.IsOnWhatItFound)
        {
            CultureInfo.CurrentUICulture = this.PreviousUICulture;
            CultureInfo.CurrentCulture = this.PreviousCulture;

            return;
        }

        // The surrounding scope it was entered in ended first. Back to the nearest scope that still holds, and its
        // culture. Outside every scope, back to the cultures from before the outermost one.
        CultureInfo.CurrentUICulture = surrounding?.Scope.Culture ?? innermost.UICultureBefore;
        CultureInfo.CurrentCulture = surrounding?.Scope.Culture ?? innermost.CultureBefore;
    }

    /// <summary>
    /// A scope that holds in a flow, with the surrounding scopes that hold there too.
    /// </summary>
    // Never changed, because a flow started inside a scope shares what it found. A scope disposed out of order gives
    // the scopes inside it a new list without it. A class rather than a record, so that the package carries no
    // equality and printing it never uses.
    private sealed class OpenScope(CultureScope scope, OpenScope? surrounding, CultureInfo uiCultureBefore,
        CultureInfo cultureBefore, bool isOnWhatItFound)
    {
        /// <summary>
        /// The scope.
        /// </summary>
        public CultureScope Scope { get; } = scope;

        /// <summary>
        /// The surrounding scope that holds, or <see langword="null"/> for the outermost.
        /// </summary>
        public OpenScope? Surrounding { get; } = surrounding;

        /// <summary>
        /// The UI culture from before the outermost scope.
        /// </summary>
        // ReSharper disable once InconsistentNaming - Same casing as the property of CultureInfo.
        public CultureInfo UICultureBefore { get; } = uiCultureBefore;

        /// <summary>
        /// The culture from before the outermost scope.
        /// </summary>
        public CultureInfo CultureBefore { get; } = cultureBefore;

        /// <summary>
        /// Whether the scope this one was entered in still directly surrounds it, so that the cultures it found are
        /// those to put back.
        /// </summary>
        public bool IsOnWhatItFound { get; } = isOnWhatItFound;

        /// <summary>
        /// Whether a scope is this one or a surrounding one.
        /// </summary>
        public bool Holds(CultureScope scope)
        {
            for (var current = this; current is not null; current = current.Surrounding)
                if (ReferenceEquals(current.Scope, scope))
                    return true;

            return false;
        }

        /// <summary>
        /// The scopes that hold here without one of the surrounding scopes. The scope directly inside that one then no
        /// longer puts back the cultures it found.
        /// </summary>
        public OpenScope? Without(CultureScope scope)
        {
            if (ReferenceEquals(this.Scope, scope))
                return this.Surrounding;

            var surrounding = this.Surrounding!.Without(scope);
            var directlyInsideIt = ReferenceEquals(this.Surrounding.Scope, scope);

            var result = new OpenScope(
                this.Scope,
                surrounding,
                this.UICultureBefore,
                this.CultureBefore,
                isOnWhatItFound: this.IsOnWhatItFound && !directlyInsideIt);

            return result;
        }
    }
}
