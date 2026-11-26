namespace CodeChops.Idioma;

/// <summary>
/// The names to try when looking up a culture, most specific first:
/// <list type="number">
/// <item>The culture.</item>
/// <item>The same name without its script, where that's the same culture: <c>zh-TW</c> after <c>zh-Hant-TW</c>.</item>
/// <item>Each parent culture.</item>
/// <item>The last of those names, with its subtags dropped one at a time.</item>
/// </list>
/// For <c>zh-TW</c> that's <c>zh-TW</c>, <c>zh-Hant</c>, then <c>zh</c>, and for <c>zh-Hant-TW</c> it's
/// <c>zh-Hant-TW</c>, <c>zh-TW</c>, <c>zh-Hant</c>, then <c>zh</c>. For a culture name instead of a culture, the
/// subtags are dropped by name: <c>zh-TW</c>, then <c>zh</c>. The invariant culture gives no names. A name that's
/// empty or white space is refused.
/// </summary>
// Through CultureInfo.Parent, because a parent need not be a prefix of the name (zh-TW falls back to zh-Hant), and by
// name as well: under invariant globalization a culture either can't be created at all (the default) or has the
// invariant culture as its parent (PredefinedCulturesOnly off), while en still serves en-GB, as the build checks it.
// By name alone for a language code that has no culture of that name (see OfLanguage). The registry keeps the names
// of each walk in its lookup of the culture or the code (LocalizationRegistry.Resolve and ResolveLanguage), so a name
// is walked about once per registry. Nothing is kept here, because CultureName keeps the cultures that the check of a
// script reads.
internal readonly struct CultureFallback
{
    // Every script subtag has four letters (ISO 15924), and no other subtag has four letters in second place.
    private const int ScriptLength = 4;

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
    /// The names to try for a language code, as a user of that language gets them. They come through the culture of
    /// that name, so <c>zh-TW</c> gives <c>zh-TW</c>, <c>zh-Hant</c>, then <c>zh</c>. They come by name alone when
    /// there's no culture of that name:
    /// <list type="bullet">
    /// <item>The code isn't valid.</item>
    /// <item>This machine has no culture for it, as under invariant globalization.</item>
    /// <item>Its culture goes by another name, as ICU gives <c>zh-yue</c> the culture <c>yue</c>.</item>
    /// </list>
    /// </summary>
    // So that a text falling back to a language shows what a user of that language sees. By name for an alias, because
    // the walk of its culture would never try the code itself. With ICU, nearly every valid code has a culture of that
    // name, even a made-up one such as qq.
    public static CultureFallback OfLanguage(string language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);

        var fallback = CultureName.GetCulture(language) is { } culture && String.Equals(culture.Name, language, StringComparison.OrdinalIgnoreCase)
            ? new CultureFallback(culture)
            : new CultureFallback(language);

        return fallback;
    }

    public Enumerator GetEnumerator()
    {
        return new Enumerator(this.Culture, this.Name);
    }

    /// <summary>
    /// A culture name without its script, such as <c>zh-TW</c> for <c>zh-Hant-TW</c>, where that's the same culture.
    /// It's <see langword="null"/> for:
    /// <list type="bullet">
    /// <item>A name without a script.</item>
    /// <item>A name with nothing after its script, such as <c>zh-Hant</c>.</item>
    /// <item>A name whose culture without the script writes in another one. <c>sr-RS</c> is written in Cyrillic, so it
    /// isn't tried for <c>sr-Latn-RS</c>.</item>
    /// </list>
    /// Under invariant globalization, the name without its script is always tried, because the scripts can't be compared
    /// there. So <c>sr-Latn-RS</c> tries <c>sr-RS</c> too.
    /// </summary>
    // A user's culture can carry its script, as zh-Hant-TW does on Apple platforms, where an app declares zh-TW. The
    // parent cultures of zh-Hant-TW are zh-Hant and zh, so without this the walk never meets zh-TW.
    private static string? GetNameWithoutScript(string name)
    {
        // Told from the name first, because nearly every name has no script.
        var scriptStart = IndexOfScript(name);
        if (scriptStart < 0 || scriptStart + ScriptLength == name.Length)
            return null;

        var withoutScript = String.Concat(name.AsSpan(start: 0, scriptStart - 1), name.AsSpan(scriptStart + ScriptLength));

        return HasSameScript(name, name.AsSpan(scriptStart, ScriptLength), withoutScript) ? withoutScript : null;
    }

    /// <summary>
    /// Whether the culture without the script writes in that script, as <c>zh-TW</c> writes in <c>Hant</c>.
    /// </summary>
    // Through the cultures of this machine, as a name alone can't tell: zh-TW is Traditional Chinese, zh-CN Simplified,
    // and sr-RS Cyrillic. A parent culture with a script names it (zh-TW falls back to zh-Hant). Without one, the names
    // of a month and a day show the script: Latin "januar" for sr-Latn-RS, Cyrillic for sr-RS. Without the cultures, as
    // under invariant globalization, the name alone decides, as it does for the rest of the walk there. CultureName
    // keeps the cultures, so each is created once.
    private static bool HasSameScript(string name, ReadOnlySpan<char> script, string withoutScript)
    {
        if (CultureName.GetCulture(withoutScript) is not { } other || CultureName.GetCulture(name) is not { } culture)
            return true;

        // The culture itself too, for a name the cultures know under another one with a script.
        for (var current = other; current.Name.Length > 0; current = current.Parent)
            if (IndexOfScript(current.Name) is >= 0 and var start)
                return current.Name.AsSpan(start, ScriptLength).Equals(script, StringComparison.OrdinalIgnoreCase);

        var format = culture.DateTimeFormat;
        var otherFormat = other.DateTimeFormat;

        var isSame = String.Equals(format.GetMonthName(1), otherFormat.GetMonthName(1), StringComparison.Ordinal)
                     && String.Equals(format.GetDayName(DayOfWeek.Monday), otherFormat.GetDayName(DayOfWeek.Monday), StringComparison.Ordinal);

        return isSame;
    }

    /// <summary>
    /// Where the script of a culture name starts, or -1 for a name without one. A script is the second subtag, of
    /// <see cref="ScriptLength"/> letters, after a language of two letters or more: <c>Hant</c> in <c>zh-Hant-TW</c>.
    /// </summary>
    private static int IndexOfScript(string name)
    {
        var first = name.IndexOf('-');

        if (first < 2)
            return -1;

        var end = name.IndexOf('-', first + 1);

        if ((end < 0 ? name.Length : end) - first - 1 != ScriptLength)
            return -1;

        for (var i = first + 1; i <= first + ScriptLength; i++)
            if (!Char.IsAsciiLetter(name[i]))
                return -1;

        return first + 1;
    }

    internal struct Enumerator(CultureInfo? culture, string? name)
    {
        public string Current { get; private set; } = "";
        private string? First { get; set; } = name;
        private CultureInfo? Next { get; set; } = culture;

        /// <summary>
        /// The first name without its script, to try right after it, or <see langword="null"/>.
        /// </summary>
        private string? WithoutScript { get; set; }

        /// <summary>
        /// The name without its script that was tried, so that the walk doesn't try it a second time.
        /// </summary>
        private string? TriedWithoutScript { get; set; }

        /// <summary>
        /// The last name of the walk itself, which dropping a subtag starts from. The name without its script lies
        /// beside the walk, so it's this one only where a parent culture has that name. Dropping a subtag from
        /// <c>zh-TW</c> would skip <c>zh-Hant</c>.
        /// </summary>
        private string Last { get; set; } = "";

        private bool HasStarted { get; set; }

        public bool MoveNext()
        {
            if (this.WithoutScript is { } withoutScript)
            {
                this.Current = this.TriedWithoutScript = withoutScript;
                this.WithoutScript = null;

                return true;
            }

            while (this.MoveToNextName())
            {
                if (this.TriedWithoutScript is { } tried && String.Equals(this.Last, tried, StringComparison.OrdinalIgnoreCase))
                    continue;

                this.Current = this.Last;

                if (!this.HasStarted)
                {
                    this.HasStarted = true;
                    this.WithoutScript = GetNameWithoutScript(this.Last);
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Moves <see cref="Last"/> on to the next name of the walk itself.
        /// </summary>
        private bool MoveToNextName()
        {
            if (this.First is { Length: > 0 } first)
            {
                this.Last = first;
                this.First = null;

                return true;
            }

            if (this.Next is { Name.Length: > 0 } next)
            {
                this.Last = next.Name;
                this.Next = next.Parent;

                return true;
            }

            this.Next = null;
            var separator = this.Last.LastIndexOf('-');

            if (separator < 0)
                return false;

            this.Last = this.Last[..separator];

            return true;
        }
    }
}
