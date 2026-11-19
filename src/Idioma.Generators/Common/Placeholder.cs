using System.Globalization;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Finds a placeholder of a format string in a wording: <c>{0}</c>, with or without an alignment or a format, as in
/// <c>{1,8}</c> and <c>{0:N2}</c>. Nothing fills one in, so in a wording it is a value that was meant to be there and
/// is not (<c>LOC502</c>).
/// </summary>
/// <remarks>
/// A placeholder is written as <see cref="string.Format(string, object?)"/> reads it: a number, then an alignment, then
/// a format. The alignment is a comma and a whole number, as in <c>,8</c> or <c>, -8</c>. The format is a colon and
/// anything but a brace, as in <c>:N2</c>. So <c>{1, 2, 3}</c> is text, because <c> 2, 3</c> is no alignment.
/// </remarks>
internal static class Placeholder
{
    /// <summary>
    /// The first placeholder a wording holds, as it is written, or <see langword="null"/> when it holds none. Braces
    /// around anything but a number are text: <c>{name}</c>, <c>{}</c>, and <c>{ 0 }</c> are no placeholders.
    /// </summary>
    public static string? Find(string wording)
    {
        for (var start = wording.IndexOf('{'); start >= 0; start = wording.IndexOf('{', start + 1))
        {
            if (FindEnd(wording, start, braceWidth: 1) is var (_, end))
                return wording.Substring(start, end - start);
        }

        return null;
    }

    /// <summary>
    /// The placeholder that starts at <paramref name="start"/>: its number, its alignment and format as written (as in
    /// <c>,8</c> or <c>:N2</c>), and the index after it. <see langword="null"/> when none starts there, or when its
    /// number is too large to be one.
    /// </summary>
    /// <param name="written">The text that holds the placeholder.</param>
    /// <param name="start">The index of its opening brace.</param>
    /// <param name="braceWidth">
    /// How often a brace of the placeholder is written: once, or twice in the text of an interpolated string as the
    /// source writes it.
    /// </param>
    public static (int Number, string AlignmentAndFormat, int End)? Read(string written, int start, int braceWidth)
    {
        if (FindEnd(written, start, braceWidth) is not var (digitsEnd, end))
            return null;

        var digits = start + braceWidth;
        if (!Int32.TryParse(written.Substring(digits, digitsEnd - digits), NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            return null;

        var close = end - braceWidth;
        return (number, written.Substring(digitsEnd, close - digitsEnd), end);
    }

    /// <summary>
    /// Where the number of the placeholder that starts at <paramref name="start"/> ends, and the index after the
    /// placeholder. <see langword="null"/> when none starts there.
    /// </summary>
    private static (int DigitsEnd, int End)? FindEnd(string written, int start, int braceWidth)
    {
        if (!IsRun(written, start, '{', braceWidth))
            return null;

        var digits = start + braceWidth;
        var digitsEnd = SkipDigits(written, digits);

        // Without a number, no placeholder.
        if (digitsEnd == digits)
            return null;

        var end = digitsEnd;

        // An alignment: a comma, then a whole number, with spaces around it.
        if (end < written.Length && written[end] is ',')
        {
            end = SkipSpaces(written, end + 1);

            if (end < written.Length && written[end] is '-')
                end++;

            var alignmentEnd = SkipDigits(written, end);
            if (alignmentEnd == end)
                return null;

            end = SkipSpaces(written, alignmentEnd);
        }

        // A format: a colon, then anything that holds no brace itself.
        if (end < written.Length && written[end] is ':')
        {
            while (end < written.Length && written[end] is not ('{' or '}'))
                end++;
        }

        return IsRun(written, end, '}', braceWidth) ? (digitsEnd, end + braceWidth) : null;
    }

    private static int SkipDigits(string written, int index)
    {
        while (index < written.Length && written[index] is >= '0' and <= '9')
            index++;

        return index;
    }

    private static int SkipSpaces(string written, int index)
    {
        while (index < written.Length && written[index] is ' ')
            index++;

        return index;
    }

    private static bool IsRun(string written, int start, char character, int width)
    {
        for (var i = start; i < start + width; i++)
        {
            if (i >= written.Length || written[i] != character)
                return false;
        }

        return true;
    }
}
