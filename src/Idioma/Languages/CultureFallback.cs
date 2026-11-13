namespace CodeChops.Idioma;

/// <summary>
/// The names to try when looking up a culture, most specific first: the culture, then each parent culture
/// (<c>zh-TW</c>, <c>zh-Hant</c>, then <c>zh</c>), then the last of those names with its subtags dropped one at a time.
/// For a culture name instead of a culture, only the subtags are dropped: <c>zh-TW</c>, then <c>zh</c>. The invariant
/// culture gives no names, and an empty name is refused.
/// </summary>
// Through CultureInfo.Parent, because a parent need not be a prefix of the name (zh-TW falls back to zh-Hant), and by
// name as well: under invariant globalization a culture either cannot be created at all (the default) or has the
// invariant culture as its parent (PredefinedCulturesOnly off), while en still serves en-GB, as the build checks it.
// By name alone for a language code, so that the walk never needs a CultureInfo. A plain struct enumerator, and a
// name that dropping a subtag gives is kept for the next walk, so a lookup allocates nothing once its names are kept:
// the parent cultures hold their names, and an app's default language (en-GB, then en) is walked by name for every
// text that falls back to it.
internal readonly struct CultureFallback
{
    // Far more than the languages of an app and the cultures of its users; a name beyond it is cut on every walk, so
    // that culture names from outside (a request header, where invariant globalization with PredefinedCulturesOnly
    // off lets any name become a culture) cannot grow this without end.
    private const int MaxNamesKept = 1024;

    /// <summary>
    /// Per culture name, the name above it: the same name without its last subtag (<c>zh-Hant-TW</c> gives
    /// <c>zh-Hant</c>, <c>en-GB</c> gives <c>en</c>). Kept so that a walk by name hands out the same strings each time
    /// instead of cutting new ones, up to <see cref="MaxNamesKept"/> names.
    /// </summary>
    // Keyed by the exact spelling, so en-GB and EN-gb are kept apart: each walk gives back the casing it was asked for.
    private static ConcurrentDictionary<string, string> NamesAbove { get; } = new(StringComparer.Ordinal);

    private CultureInfo? Culture { get; }
    private string? Name { get; }

    public CultureFallback(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        this.Culture = culture;
    }

    public CultureFallback(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        this.Name = name;
    }

    /// <summary>
    /// The names to try for a language code as a user of that language gets them: through its culture where this
    /// machine has one (<c>zh-TW</c>, <c>zh-Hant</c>, then <c>zh</c>), else by name alone.
    /// </summary>
    // So that a text falling back to a language shows what a user of that language sees. By name where the culture
    // goes by another name than the code (an alias), which its walk would never try.
    public static CultureFallback OfLanguage(string language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);

        var fallback = CultureName.FindCulture(language) is { } culture && String.Equals(culture.Name, language, StringComparison.OrdinalIgnoreCase)
            ? new CultureFallback(culture)
            : new CultureFallback(language);

        return fallback;
    }

    public Enumerator GetEnumerator()
    {
        return new Enumerator(this.Culture, this.Name);
    }

    internal struct Enumerator(CultureInfo? culture, string? name)
    {
        public string Current { get; private set; } = "";
        private CultureInfo? Next { get; set; } = culture;
        private string? First { get; set; } = name;

        public bool MoveNext()
        {
            if (this.First is { Length: > 0 } first)
            {
                this.Current = first;
                this.First = null;

                return true;
            }

            if (this.Next is { Name.Length: > 0 } next)
            {
                this.Current = next.Name;
                this.Next = next.Parent;

                return true;
            }

            this.Next = null;
            var separator = this.Current.LastIndexOf('-');

            if (separator < 0)
                return false;

            this.Current = GetNameAbove(this.Current, separator);

            return true;

            static string GetNameAbove(string name, int separator)
            {
                if (NamesAbove.TryGetValue(name, out var above))
                    return above;

                above = name[..separator];

                if (NamesAbove.Count < MaxNamesKept)
                    NamesAbove.TryAdd(name, above);

                return above;
            }
        }
    }
}
