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
    // The scope the code that runs now is in. It flows with the code, as the cultures of the thread do, so the scope
    // still holds after an await. Read only where a provider is set: without one, the thread's UI culture is the
    // scope's already.
    private static AsyncLocal<CultureScope?> Entered { get; } = new();

    /// <summary>
    /// The culture of the scope the code that runs now is in, or <see langword="null"/> outside one. A text follows
    /// it rather than <see cref="LocalizationRegistry.CurrentUICultureProvider"/>.
    /// </summary>
    internal static CultureInfo? Current => FindOpen(Entered.Value)?.Culture;

    /// <summary>
    /// A scope, or else the nearest scope around it that isn't disposed yet. So a scope disposed before one inside it
    /// doesn't come back when the inner one ends.
    /// </summary>
    private static CultureScope? FindOpen(CultureScope? scope)
    {
        while (scope is { _isDisposed: true })
            scope = scope.PreviousScope;

        return scope;
    }

    private CultureInfo Culture { get; }

    private CultureScope? PreviousScope { get; }

    // ReSharper disable once InconsistentNaming - Same casing as the property of CultureInfo.
    private CultureInfo PreviousUICulture { get; }
    private CultureInfo PreviousCulture { get; }

    private bool _isDisposed;

    internal CultureScope(CultureInfo culture)
    {
        this.Culture = culture;
        this.PreviousScope = Entered.Value;
        this.PreviousUICulture = CultureInfo.CurrentUICulture;
        this.PreviousCulture = CultureInfo.CurrentCulture;

        // The UI culture first, because only its setter can throw, on the name of a culture of a derived class.
        // Nothing has changed yet then.
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        Entered.Value = this;
    }

    /// <summary>
    /// Ends the scope. Texts follow the culture from before it again. Disposing a scope twice does nothing.
    /// </summary>
    public void Dispose()
    {
        if (this._isDisposed)
            return;

        this._isDisposed = true;

        // Disposed before a scope inside it: that scope still holds, and puts back what it found when it ends.
        if (!ReferenceEquals(Entered.Value, this))
            return;

        // The usual order: back to what this scope found.
        if (this.PreviousScope is not { _isDisposed: true })
        {
            Entered.Value = this.PreviousScope;
            CultureInfo.CurrentUICulture = this.PreviousUICulture;
            CultureInfo.CurrentCulture = this.PreviousCulture;

            return;
        }

        // The scope around this one ended first. Back to the nearest scope that still holds, and its culture. Outside
        // every scope, back to the cultures from before the outermost one.
        var outer = FindOpen(this.PreviousScope);
        var outermost = this;

        while (outer is null && outermost.PreviousScope is { } previous)
            outermost = previous;

        Entered.Value = outer;
        CultureInfo.CurrentUICulture = outer?.Culture ?? outermost.PreviousUICulture;
        CultureInfo.CurrentCulture = outer?.Culture ?? outermost.PreviousCulture;
    }
}
