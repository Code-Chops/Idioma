using System.Runtime.CompilerServices;

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

/// <summary>
/// The culture <see cref="LocalizationRegistry.CurrentUICultureProvider"/> gives inside a scope, and none outside it.
/// The provider is one for the whole process, and other tests run next to the one that sets it. So it gives a culture
/// only to the test that asked for one. The tests that use it don't run next to each other
/// (<c>[NotInParallel(nameof(LocalizationRegistry))]</c>), so the scope puts back the provider it found.
/// </summary>
internal sealed class ProvidedCultureScope : IDisposable
{
    private static AsyncLocal<CultureInfo?> Provided { get; } = new();
    private static AsyncLocal<StrongBox<int>?> Calls { get; } = new();

    private Func<CultureInfo?>? PreviousProvider { get; } = LocalizationRegistry.CurrentUICultureProvider;
    private StrongBox<int> CallsInScope { get; } = new();

    /// <param name="name">The culture the provider gives, or <see langword="null"/> for a provider that gives none.</param>
    public ProvidedCultureScope(string? name)
        : this(name is null ? null : CultureInfo.GetCultureInfo(name))
    {
    }

    /// <param name="culture">The culture the provider gives, or <see langword="null"/> for a provider that gives none.</param>
    public ProvidedCultureScope(CultureInfo? culture)
    {
        LocalizationRegistry.CurrentUICultureProvider = static () =>
        {
            if (Calls.Value is { } calls)
                calls.Value++;

            return Provided.Value;
        };

        Provided.Value = culture;
        Calls.Value = this.CallsInScope;
    }

    /// <summary>
    /// How often the provider was asked for a culture inside the scope.
    /// </summary>
    public int CallCount => this.CallsInScope.Value;

    public void Dispose()
    {
        Provided.Value = null;
        Calls.Value = null;
        LocalizationRegistry.CurrentUICultureProvider = this.PreviousProvider;
    }
}
