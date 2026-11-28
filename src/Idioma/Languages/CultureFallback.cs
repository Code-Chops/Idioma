namespace CodeChops.Idioma;

/// <summary>
/// The names to try when looking up a culture, most specific first:
/// <list type="number">
/// <item>The culture.</item>
/// <item>The same name without its script, where that's the same script: <c>zh-TW</c> after <c>zh-Hant-TW</c>.</item>
/// <item>Each parent culture.</item>
/// <item>The last of those names, with its subtags dropped one at a time.</item>
/// </list>
/// For <c>zh-Hant-TW</c> that's <c>zh-Hant-TW</c>, <c>zh-TW</c>, <c>zh-Hant</c>, then <c>zh</c>. A culture name
/// instead of a culture drops subtags by name alone. The invariant culture gives no names.
/// </summary>
// Through CultureInfo.Parent, because a parent need not be a prefix of the name (zh-TW falls back to zh-Hant). By name
// as well, because under invariant globalization a culture has no parents, while en still serves en-GB. The registry
// keeps the names of each walk (LocalizationRegistry.Resolve and ResolveLanguage), so nothing is kept here.
internal readonly struct CultureFallback
{
    // Every script subtag has four letters (ISO 15924), and no other subtag has four letters in second place.
    private const int ScriptLength = 4;

    private CultureInfo? Culture { get; }
    private string? Name { get; }

    /// <summary>
    /// The default script of the languages that the build knows one for, such as <c>Hant</c> for <c>zh-TW</c>. They're
    /// the app's languages and those of the described sets (<see cref="LocalizationRegistry.AppScripts"/>).
    /// </summary>
    private FrozenDictionary<string, string>? Scripts { get; }

    public CultureFallback(CultureInfo culture, FrozenDictionary<string, string>? scripts = null)
    {
        ArgumentNullException.ThrowIfNull(culture);

        this.Culture = culture;
        this.Scripts = scripts;
    }

    public CultureFallback(string name, FrozenDictionary<string, string>? scripts = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        this.Name = name;
        this.Scripts = scripts;
    }

    /// <summary>
    /// The names to try for a language code, as a user of that language gets them, so a text falling back to it shows
    /// what that user sees. By name alone where no culture has that name, such as where ICU calls <c>zh-yue</c>
    /// <c>yue</c>, because the walk of that culture would never try the code itself.
    /// </summary>
    public static CultureFallback OfLanguage(string language, FrozenDictionary<string, string>? scripts = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);

        var fallback = CultureName.GetCulture(language) is { } culture && String.Equals(culture.Name, language, StringComparison.OrdinalIgnoreCase)
            ? new CultureFallback(culture, scripts)
            : new CultureFallback(language, scripts);

        return fallback;
    }

    public Enumerator GetEnumerator()
    {
        return new Enumerator(this.Culture, this.Name, this.Scripts);
    }

    /// <summary>
    /// A culture name without its script, such as <c>zh-TW</c> for <c>zh-Hant-TW</c>. It's <see langword="null"/> where
    /// the script is missing or last. It's also <see langword="null"/> where the build gave the name without the script
    /// another default script. The build gives <c>sr-RS</c> <c>Cyrl</c>, so <c>sr-Latn-RS</c> doesn't try it. A name
    /// the build gave no script is tried.
    /// </summary>
    // A user's culture can carry its script, as zh-Hant-TW does on Apple platforms, where an app declares zh-TW. Its
    // parents are zh-Hant and zh, so the walk would never meet zh-TW.
    private static string? GetNameWithoutScript(string name, FrozenDictionary<string, string>? scripts)
    {
        // Told from the name first, because nearly every name has no script.
        var scriptStart = IndexOfScript(name);
        if (scriptStart < 0 || scriptStart + ScriptLength == name.Length)
            return null;

        var withoutScript = String.Concat(name.AsSpan(start: 0, scriptStart - 1), name.AsSpan(scriptStart + ScriptLength));

        var isOtherScript = scripts is not null
                            && scripts.TryGetValue(withoutScript, out var script)
                            && !script.AsSpan().Equals(name.AsSpan(scriptStart, ScriptLength), StringComparison.OrdinalIgnoreCase);

        return isOtherScript ? null : withoutScript;
    }

    /// <summary>
    /// Where the script starts, or -1. A script is a second subtag of four letters, such as <c>Hant</c> in
    /// <c>zh-Hant-TW</c>.
    /// </summary>
    private static int IndexOfScript(string name)
    {
        var first = name.IndexOf('-', StringComparison.Ordinal);

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

    internal struct Enumerator(CultureInfo? culture, string? name, FrozenDictionary<string, string>? scripts)
    {
        public string Current { get; private set; } = "";
        private string? First { get; set; } = name;
        private CultureInfo? Next { get; set; } = culture;

        /// <summary>
        /// The first name without its script, to try right after it.
        /// </summary>
        private string? WithoutScript { get; set; }

        /// <summary>
        /// So the walk doesn't try it twice.
        /// </summary>
        private string? TriedWithoutScript { get; set; }

        /// <summary>
        /// The last name of the walk itself, which dropping a subtag starts from. Never the name without its script,
        /// because dropping a subtag from <c>zh-TW</c> would skip <c>zh-Hant</c>.
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
                    this.WithoutScript = GetNameWithoutScript(this.Last, scripts);
                }

                return true;
            }

            return false;
        }

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
