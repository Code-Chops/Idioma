namespace CodeChops.Idioma;

/// <summary>
/// The rules for a language code and how it's written. A language code is a culture name, such as <c>en</c>,
/// <c>en-GB</c>, or <c>zh-Hans</c>. This class checks a code and writes it in the letter case that culture names use.
/// </summary>
internal static class CultureName
{
    // Far more than the languages of an app and the cultures of its users. A code beyond it is looked up every time, so
    // codes from outside, such as the culture names of a request header, can't grow Cultures without end. That doesn't
    // cap memory with ICU, because CultureInfo.GetCultureInfo keeps every culture it creates.
    private const int MaxCulturesKept = 1024;

    /// <summary>
    /// The culture of each code, or <see langword="null"/> for a code without one. The cultures are kept, because
    /// finding out that a code has no culture throws an exception. Under invariant globalization, that's every code,
    /// unless <c>PredefinedCulturesOnly</c> is off.
    /// </summary>
    private static ConcurrentDictionary<string, CultureInfo?> Cultures { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether <see cref="Cultures"/> holds <see cref="MaxCulturesKept"/> cultures, so no more are kept.
    /// </summary>
    // Cultures are never removed, so this is set once. A code past the limit then skips Cultures.Count, which takes all
    // of the dictionary's locks. A thread that doesn't see it set yet reads Count once more, which is harmless.
    private static bool HasKeptMaxCultures { get; set; }

    /// <summary>
    /// Returns whether a code is a valid language code. The parts are separated by hyphens. The first part must be two
    /// to eight ASCII letters. Each later part must be one to eight ASCII letters or digits.
    /// </summary>
    // The rule the build applies to [assembly: SupportedLanguages] (LOC101), so that a code the build accepts is one
    // the registry accepts. Not checked against the cultures of this machine: under invariant globalization only the
    // invariant culture exists, and a set for nl must still register there.
    public static bool IsValid(ReadOnlySpan<char> code)
    {
        var isFirst = true;

        // Part by part through IndexOf, not Split. The framework holds no compiled Split over char, and ReadyToRun can't
        // compile it into this library, so it cost every app four JIT-compiled methods at its first registration.
        while (true)
        {
            var hyphen = code.IndexOf('-');
            var part = hyphen < 0 ? code : code[..hyphen];

            if (part.Length is 0 or > 8 || (isFirst && part.Length < 2))
                return false;

            foreach (var character in part)
                if (!(isFirst ? Char.IsAsciiLetter(character) : Char.IsAsciiLetterOrDigit(character)))
                    return false;

            if (hyphen < 0)
                return true;

            code = code[(hyphen + 1)..];
            isFirst = false;
        }
    }

    /// <summary>
    /// Returns the culture a code names, or <see langword="null"/> when there's none:
    /// <list type="bullet">
    /// <item>The code isn't valid.</item>
    /// <item>This machine has no culture for it. Under invariant globalization, that's every code, unless
    /// <c>PredefinedCulturesOnly</c> is off.</item>
    /// </list>
    /// </summary>
    /// <exception cref="ArgumentException">When <paramref name="code"/> is null, empty, or white space.</exception>
    // With ICU, nearly every valid code has a culture, even a made-up one such as qq or nl-US.
    public static CultureInfo? GetCulture(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        if (Cultures.TryGetValue(code, out var culture))
            return culture;

        try
        {
            culture = IsValid(code) ? CultureInfo.GetCultureInfo(code) : null;
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

    /// <summary>
    /// Returns a code in the letter case that culture names use, so <c>ZH-hans-cn</c> becomes <c>zh-Hans-CN</c>. Each
    /// part is cased as follows:
    /// <list type="bullet">
    /// <item>The language is lower case.</item>
    /// <item>Each later part of four letters is capitalized, as a script is.</item>
    /// <item>Each later part of two letters is upper case, as a region is.</item>
    /// <item>Every other part is lower case.</item>
    /// </list>
    /// Every part after a part of one character, such as an extension or private use, is lower case. So
    /// <c>EN-ca-X-CA</c> becomes <c>en-CA-x-ca</c>. A code that isn't valid is still cased, not refused.
    /// </summary>
    // The letter case RFC 5646 (section 2.1.1) recommends, and not always that of CultureInfo.Name, which depends on
    // the machine: with ICU a variant is in upper case (ca-ES-VALENCIA).
    // Codes match in any letter case, so the case is only what a reader sees: the languages of a registry, and the
    // languages a text is written to JSON under, which must not depend on which set registered first.
    public static string Normalize(string code)
    {
        Span<char> buffer = stackalloc char[code.Length <= 64 ? code.Length : 0];
        if (buffer.Length != code.Length)
            buffer = new char[code.Length];

        var isFirst = true;
        var isAfterSingleCharacter = false;
        var start = 0;

        // Part by part through IndexOf, for the reason IsValid gives. A code that ends in a hyphen ends in an empty part.
        while (start <= code.Length)
        {
            var hyphen = code.IndexOf('-', start);
            var length = (hyphen < 0 ? code.Length : hyphen) - start;
            var part = code.AsSpan(start, length);
            var target = buffer.Slice(start, length);

            if (start > 0)
                buffer[start - 1] = '-';

            var hasCaseOfItsOwn = !isFirst && !isAfterSingleCharacter;

            if (hasCaseOfItsOwn && length is 2 && Char.IsAsciiLetter(part[0]) && Char.IsAsciiLetter(part[1]))
            {
                part.ToUpperInvariant(target);
            }
            else
            {
                part.ToLowerInvariant(target);

                if (hasCaseOfItsOwn && length is 4 && !part.ContainsAnyInRange('0', '9'))
                    target[0] = Char.ToUpperInvariant(target[0]);
            }

            isFirst = false;
            isAfterSingleCharacter |= length is 1;
            start += length + 1;
        }

        var result = buffer.SequenceEqual(code)
            ? code
            : new string(buffer);

        return result;
    }
}
