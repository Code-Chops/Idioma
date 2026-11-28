namespace CodeChops.Idioma;

/// <summary>
/// A set that words a text on every call in a language the text falls back to, with that language's culture. One
/// object, so a thread reads the set and its culture together.
/// </summary>
internal sealed class FallbackSet<TContract>(TContract set, CultureInfo? culture)
    where TContract : class
{
    public TContract Set { get; } = set;

    /// <summary>
    /// <see langword="null"/> where this machine has no culture for the language, and then no scope is entered.
    /// </summary>
    public CultureInfo? Culture { get; } = culture;
}
