namespace CodeChops.Idioma;

/// <summary>
/// What a language code must look like, and how it is written: a culture name such as <c>nl</c>, <c>nl-BE</c>, or
/// <c>zh-Hans</c>.
/// </summary>
internal static class CultureName
{
    /// <summary>
    /// Whether a code looks like a culture name: a first part of two to eight ASCII letters, and further parts of one
    /// to eight ASCII letters and digits, separated by hyphens.
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
    /// A valid code in the letter case culture names are written in: the language in lower case, a script of four
    /// letters capitalized, a region of two letters in upper case, and every other part in lower case
    /// (<c>ZH-hans-cn</c> gives <c>zh-Hans-CN</c>).
    /// </summary>
    // Codes match in any letter case, so the case is only what a reader sees: the languages of a registry, and the
    // languages a text is written to JSON under, which must not depend on which set registered first.
    public static string Normalize(string code)
    {
        Span<char> buffer = stackalloc char[code.Length <= 64 ? code.Length : 0];
        if (buffer.Length != code.Length)
            buffer = new char[code.Length];

        var isFirst = true;

        foreach (var range in code.AsSpan().Split('-'))
        {
            var (start, length) = range.GetOffsetAndLength(code.Length);
            var part = code.AsSpan(start, length);
            var target = buffer.Slice(start, length);

            if (start > 0)
                buffer[start - 1] = '-';

            if (!isFirst && length is 2 && Char.IsAsciiLetter(part[0]) && Char.IsAsciiLetter(part[1]))
            {
                part.ToUpperInvariant(target);
            }
            else
            {
                part.ToLowerInvariant(target);

                if (!isFirst && length is 4 && !part.ContainsAnyInRange('0', '9'))
                    target[0] = Char.ToUpperInvariant(target[0]);
            }

            isFirst = false;
        }

        var result = buffer.SequenceEqual(code) ? code : new string(buffer);
        return result;
    }
}
