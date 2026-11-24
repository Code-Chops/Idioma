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

    internal CultureScope(CultureInfo culture)
    {
        this.Culture = culture;
        this.PreviousUICulture = CultureInfo.CurrentUICulture;
        this.PreviousCulture = CultureInfo.CurrentCulture;

        // The UI culture first, because only its setter can throw, on the name of a culture of a derived class.
        // Nothing has changed yet then.
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;

        var around = Entered.Value;

        Entered.Value = new OpenScope(
            this,
            around,
            uiCultureBefore: around?.UICultureBefore ?? this.PreviousUICulture,
            cultureBefore: around?.CultureBefore ?? this.PreviousCulture,
            isOnWhatItFound: true);
    }

    /// <summary>
    /// Ends the scope. Texts follow the culture from before it again. Disposing a scope twice does nothing.
    /// </summary>
    /// <remarks>
    /// Dispose a scope in the code that created it, as <see langword="using"/> does. The scope ends there, and in the
    /// code that runs from there on. Code that was started inside it and still runs, such as a task, stays in it until
    /// it ends itself.
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

        var around = innermost.Around;
        Entered.Value = around;

        // The usual order: back to what this scope found.
        if (innermost.IsOnWhatItFound)
        {
            CultureInfo.CurrentUICulture = this.PreviousUICulture;
            CultureInfo.CurrentCulture = this.PreviousCulture;

            return;
        }

        // The scope around this one ended first. Back to the nearest scope that still holds, and its culture. Outside
        // every scope, back to the cultures from before the outermost one.
        CultureInfo.CurrentUICulture = around?.Scope.Culture ?? innermost.UICultureBefore;
        CultureInfo.CurrentCulture = around?.Scope.Culture ?? innermost.CultureBefore;
    }

    /// <summary>
    /// A scope that holds in a flow, with the scopes around it that hold there too.
    /// </summary>
    // Never changed, because a flow started inside a scope shares what it found. A scope disposed out of order gives
    // the scopes inside it a new list without it. A class rather than a record, so that the package carries no
    // equality and printing it never uses.
    private sealed class OpenScope(CultureScope scope, OpenScope? around, CultureInfo uiCultureBefore, CultureInfo cultureBefore, bool isOnWhatItFound)
    {
        /// <summary>
        /// The scope.
        /// </summary>
        public CultureScope Scope { get; } = scope;

        /// <summary>
        /// The scope around it that holds, or <see langword="null"/> for the outermost.
        /// </summary>
        public OpenScope? Around { get; } = around;

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
        /// Whether the scope around it is still the one it was entered in, so that the cultures it found are those to
        /// put back.
        /// </summary>
        public bool IsOnWhatItFound { get; } = isOnWhatItFound;

        /// <summary>
        /// Whether a scope holds here: it's this one, or one around it.
        /// </summary>
        public bool Holds(CultureScope scope)
        {
            for (var current = this; current is not null; current = current.Around)
                if (ReferenceEquals(current.Scope, scope))
                    return true;

            return false;
        }

        /// <summary>
        /// The scopes that hold here without one around this one. The scope that stood directly on it then no longer
        /// stands on what it found.
        /// </summary>
        public OpenScope? Without(CultureScope scope)
        {
            if (ReferenceEquals(this.Scope, scope))
                return this.Around;

            var around = this.Around!.Without(scope);
            var stoodOnIt = ReferenceEquals(this.Around.Scope, scope);

            return new OpenScope(this.Scope, around, this.UICultureBefore, this.CultureBefore, this.IsOnWhatItFound && !stoodOnIt);
        }
    }
}
