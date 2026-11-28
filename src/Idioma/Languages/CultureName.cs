namespace CodeChops.Idioma;

/// <summary>
/// Finds the culture of a language code, such as <c>en-GB</c>. Every code comes from the generated code, which the
/// build has checked (<c>LOC102</c>).
/// </summary>
internal static class CultureName
{
    // Far more than an app needs, so an app that loads many libraries can't grow Cultures without end.
    private const int MaxCulturesKept = 1024;

    /// <summary>
    /// Kept, because finding out that a code has no culture throws an exception. Under invariant globalization, that's
    /// every code.
    /// </summary>
    private static ConcurrentDictionary<string, CultureInfo?> Cultures { get; } = new(StringComparer.OrdinalIgnoreCase);

    // Set once, so a code past the limit skips Cultures.Count, which takes all of the dictionary's locks.
    private static bool HasKeptMaxCultures { get; set; }

    /// <summary>
    /// The culture a code names, or <see langword="null"/> for a code that this machine has no culture for. With ICU,
    /// nearly every code has one, even a made-up one such as <c>qq</c>.
    /// </summary>
    public static CultureInfo? GetCulture(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        if (Cultures.TryGetValue(code, out var culture))
            return culture;

        try
        {
            culture = CultureInfo.GetCultureInfo(code);
        }
        catch (CultureNotFoundException)
        {
            culture = null;
        }

        if (HasKeptMaxCultures)
            return culture;

        if (Cultures.Count < MaxCulturesKept)
            Cultures.TryAdd(code, culture);
        else
            HasKeptMaxCultures = true;

        return culture;
    }
}
