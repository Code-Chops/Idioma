namespace CodeChops.Idioma;

/// <summary>
/// A set that words a text on every call in a language the text falls back to, with the culture of that language. The
/// set's member runs in a scope of that culture.
/// </summary>
/// <typeparam name="TContract">The contract.</typeparam>
// One object, so that a thread reads the set and its culture together.
internal sealed class FallbackSet<TContract>(TContract set, CultureInfo? culture)
    where TContract : class
{
    public TContract Set { get; } = set;

    /// <summary>
    /// The culture of the language, or <see langword="null"/> where this machine has none, as under invariant
    /// globalization. No scope of the language is entered then, as in <see cref="LocalizationRegistry.Word"/>.
    /// </summary>
    public CultureInfo? Culture { get; } = culture;
}
