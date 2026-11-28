namespace CodeChops.Idioma;

/// <summary>
/// A block of code in which every text follows a culture you chose, such as that of an email's recipient. Create one
/// with <see cref="LocalizationRegistry.UseCulture"/>, and dispose it to go back to the culture from before.
/// </summary>
/// <remarks>
/// <para>
/// Inside the scope, the culture is both <see cref="CultureInfo.CurrentUICulture"/>, which chooses the wording, and
/// <see cref="CultureInfo.CurrentCulture"/>, which formats dates and numbers.
/// </para>
/// <para>
/// The scope follows your code across an <see langword="await"/>. A scope inside another wins until it's disposed.
/// Dispose scopes in the reverse order you created them, as <see langword="using"/> does. Disposing a scope also ends
/// the scopes inside it.
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
    // The innermost scope. It flows with the code, as the thread's cultures do, so each flow keeps its own. It only tells
    // Dispose which scopes are still open, because texts read the UI culture.
    private static AsyncLocal<CultureScope?> Entered { get; } = new();

    private CultureScope? Surrounding { get; }

    // ReSharper disable once InconsistentNaming - Same casing as the property of CultureInfo.
    private CultureInfo PreviousUICulture { get; }
    private CultureInfo PreviousCulture { get; }

    // Restoring the context from before keeps a thread that never set a culture following the process's defaults, which
    // setting the culture back wouldn't. It's done only where the context is still the one the scope made, so an
    // AsyncLocal set inside isn't undone. Both are null where the flow is suppressed.
    private ExecutionContext? ContextBefore { get; }
    private ExecutionContext? ContextEntered { get; }

    internal CultureScope(CultureInfo culture)
    {
        this.PreviousUICulture = CultureInfo.CurrentUICulture;
        this.PreviousCulture = CultureInfo.CurrentCulture;

        var before = ExecutionContext.Capture();

        // The UI culture first, because only its setter can throw, and nothing has changed yet then.
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;

        this.Surrounding = Entered.Value;
        Entered.Value = this;

        this.ContextBefore = before;
        this.ContextEntered = before is null ? null : ExecutionContext.Capture();
    }

    /// <summary>
    /// Ends the scope, and every scope inside it. Texts follow the culture from before it again. Disposing a scope twice
    /// does nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Dispose a scope in the code that created it, as <see langword="using"/> does. The scope ends there, and in the
    /// code that runs from there on. Code that was started inside it and still runs, such as a task, stays in it until
    /// that code ends.
    /// </para>
    /// <para>
    /// A thread that set no culture itself follows the default cultures of the process, such as
    /// <see cref="CultureInfo.DefaultThreadCurrentUICulture"/>. It still follows them after the scope, unless something
    /// changed the execution context inside it. Then the scope sets the cultures from before it, and the thread keeps
    /// them when the defaults change. The context changes in these cases:
    /// </para>
    /// <list type="bullet">
    /// <item>The code inside the scope set an <see cref="AsyncLocal{T}"/>, even one it set back. A logging scope does
    /// that, such as <c>using (logger.BeginScope(...))</c>, and so does an <see cref="System.Diagnostics.Activity"/>
    /// that starts and stops.</item>
    /// <item>The code inside the scope set <see cref="CultureInfo.CurrentUICulture"/> or
    /// <see cref="CultureInfo.CurrentCulture"/> itself.</item>
    /// <item>A scope inside this one is still open, because it wasn't disposed first.</item>
    /// <item>The flow of the execution context is suppressed, as by <see cref="ExecutionContext.SuppressFlow"/>.</item>
    /// </list>
    /// <para>
    /// An <see cref="AsyncLocal{T}"/> set inside an awaited method doesn't count, because the change ends with that
    /// method.
    /// </para>
    /// </remarks>
    public void Dispose()
    {
        // Ended already in this flow, by itself or with a scope around it, or never entered in it.
        if (!this.IsOpen())
            return;

        // Nothing changed the context since the scope made it, so restoring it puts everything back as it was. Any
        // AsyncLocal set inside makes a new context, and then the cultures are set one by one.
        if (this.ContextBefore is not null && ReferenceEquals(ExecutionContext.Capture(), this.ContextEntered))
        {
            ExecutionContext.Restore(this.ContextBefore);
            return;
        }

        Entered.Value = this.Surrounding;
        CultureInfo.CurrentUICulture = this.PreviousUICulture;
        CultureInfo.CurrentCulture = this.PreviousCulture;
    }

    private bool IsOpen()
    {
        for (var scope = Entered.Value; scope is not null; scope = scope.Surrounding)
            if (ReferenceEquals(scope, this))
                return true;

        return false;
    }
}
