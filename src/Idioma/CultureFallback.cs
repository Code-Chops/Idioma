namespace CodeChops.Idioma;

/// <summary>
/// The culture names a lookup for a culture tries, most specific first: the culture and each culture it falls back to
/// (<c>nl-BE</c>, then <c>nl</c>), then what dropping the last subtag of the last of them gives, one subtag at a time.
/// For a culture name instead of a culture, only the subtags are dropped: <c>zh-Hant-TW</c>, <c>zh-Hant</c>, then
/// <c>zh</c>.
/// </summary>
// Through CultureInfo.Parent, because a parent need not be a prefix of the name (zh-TW falls back to zh-Hant), and by
// name as well: under invariant globalization the parent of every culture is the invariant culture, while nl still
// serves nl-BE, as the build checks it. By name alone for a language code, which under invariant globalization cannot
// become a CultureInfo. A plain struct enumerator, and a name that dropping a subtag gives is kept for the next walk,
// so a lookup allocates nothing: the parent cultures hold their names, and an app's default language (en-GB, then en)
// is walked by name for every text that falls back to it.
internal readonly struct CultureFallback
{
    // Far more than the languages of an app and the cultures of its users; a name beyond it is cut on every walk, so
    // that culture names from outside (a request header, under invariant globalization) cannot grow this without end.
    private const int MaxNamesKept = 1024;

    private static ConcurrentDictionary<string, string> NamesAbove { get; } = new(StringComparer.Ordinal);

    private CultureInfo? Culture { get; }
    private string? Name { get; }

    public CultureFallback(CultureInfo culture)
    {
        this.Culture = culture;
    }

    public CultureFallback(string name)
    {
        this.Name = name;
    }

    public Enumerator GetEnumerator() => new(this.Culture, this.Name);

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
        }
    }

    private static string GetNameAbove(string name, int separator)
    {
        if (NamesAbove.TryGetValue(name, out var above))
            return above;

        above = name[..separator];

        if (NamesAbove.Count < MaxNamesKept)
            NamesAbove.TryAdd(name, above);

        return above;
    }
}
