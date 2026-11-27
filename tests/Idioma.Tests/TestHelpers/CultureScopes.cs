namespace CodeChops.Idioma.Tests.TestHelpers;

/// <summary>
/// Sets both current cultures for the duration of the scope: the one a text takes its wording for, and the one values
/// are formatted for. They flow with the test, so tests that run next to this one keep their own.
/// </summary>
internal sealed class CulturesScope : IDisposable
{
    private CultureInfo PreviousCulture { get; } = CultureInfo.CurrentCulture;
    // ReSharper disable once InconsistentNaming - Same casing as the property of CultureInfo.
    private CultureInfo PreviousUICulture { get; } = CultureInfo.CurrentUICulture;

    /// <param name="uiCulture">The name of the culture a text takes its wording for.</param>
    /// <param name="culture">
    /// The name of the culture values are formatted for, or <see langword="null"/> for the same culture.
    /// </param>
    public CulturesScope(string uiCulture, string? culture = null)
        : this(CultureInfo.GetCultureInfo(uiCulture), CultureInfo.GetCultureInfo(culture ?? uiCulture))
    {
    }

    public CulturesScope(CultureInfo uiCulture, CultureInfo culture)
    {
        CultureInfo.CurrentUICulture = uiCulture;
        CultureInfo.CurrentCulture = culture;
    }

    public void Dispose()
    {
        CultureInfo.CurrentUICulture = this.PreviousUICulture;
        CultureInfo.CurrentCulture = this.PreviousCulture;
    }
}
