namespace CodeChops.Idioma;

/// <summary>
/// What a language code must look like, and how it is written: a culture name such as <c>en</c>, <c>en-GB</c>, or
/// <c>zh-Hans</c>.
/// </summary>
internal static class CultureName
{
    // Far more than the languages of an app and its libraries; a code beyond it is looked up every time, so that codes
    // from outside (the languages of a text read from JSON) cannot grow this without end.
    private const int MaxCulturesKept = 1024;

    /// <summary>
    /// Per code, its culture, or <see langword="null"/> where this machine has none. Kept because a code without a
    /// culture costs an exception to find out, and under invariant globalization that is every code.
    /// </summary>
    private static ConcurrentDictionary<string, CultureInfo?> Cultures { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether a code looks like a culture name: a first part of two to eight ASCII letters, and further parts of one
    /// to eight ASCII letters or digits, separated by hyphens.
    /// </summary>
    // The rule the build applies to [assembly: SupportedLanguages] (LOC010), so that a code the build accepts is one
    // the registry accepts. Not checked against the cultures of this machine: under invariant globalization only the
    // invariant culture exists, and a set for nl must still register there.
    public static bool IsValid(ReadOnlySpan<char> code)
    {
        var isFirst = true;

        foreach (var range in code.Split('-'))
        {
            var part = code[range];

            if (part.Length is 0 or > 8 || (isFirst && part.Length < 2))
                return false;

            foreach (var character in part)
                if (!(isFirst ? Char.IsAsciiLetter(character) : Char.IsAsciiLetterOrDigit(character)))
                    return false;

            isFirst = false;
        }

        return true;
    }

    /// <summary>
    /// The culture a code names, or <see langword="null"/> when the code is no culture name or this machine has no
    /// such culture, as under invariant globalization.
    /// </summary>
    public static CultureInfo? FindCulture(string code)
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

        if (Cultures.Count < MaxCulturesKept)
            Cultures.TryAdd(code, culture);

        return culture;
    }

    /// <summary>
    /// A code in the letter case culture names are written in: the language in lower case, each later part of four
    /// letters capitalized, as a script is, each later part of two letters in upper case, as a region is, and every
    /// other part in lower case (<c>ZH-hans-cn</c> gives <c>zh-Hans-CN</c>). What follows a part of one character, an
    /// extension or private use, is all in lower case (<c>EN-ca-X-CA</c> gives <c>en-CA-x-ca</c>). A code that is not
    /// valid is not refused: its parts are cased the same way.
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

        foreach (var range in code.AsSpan().Split('-'))
        {
            var (start, length) = range.GetOffsetAndLength(code.Length);
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
        }

        var result = buffer.SequenceEqual(code) ? code : new string(buffer);
        return result;
    }
}
