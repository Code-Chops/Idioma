using System.Globalization;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Finds a format placeholder in a wording, such as <c>{0}</c>, <c>{1,8}</c> or <c>{0:N2}</c>. Nothing fills one in
/// (<c>LOC403</c>).
/// </summary>
/// <remarks>
/// It's read as <see cref="string.Format(string, object?)"/> reads it, so <c>{1, 2, 3}</c> is text.
/// </remarks>
internal static class Placeholder
{
    /// <summary>
    /// The first placeholder as written, or <see langword="null"/>. <c>{name}</c>, <c>{}</c> and <c>{ 0 }</c> are text.
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
    /// The placeholder at <paramref name="start"/>: its number, its alignment and format as written, and the index
    /// after it. <see langword="null"/> when none starts there, or its number overflows.
    /// </summary>
    /// <param name="braceWidth">2 for the source text of an interpolated string, which doubles braces, else 1.</param>
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
    /// Where the placeholder's number ends, and the index after it. <see langword="null"/> when none starts there.
    /// </summary>
    private static (int DigitsEnd, int End)? FindEnd(string written, int start, int braceWidth)
    {
        if (!IsRun(written, start, '{', braceWidth))
            return null;

        var digits = start + braceWidth;
        var digitsEnd = SkipDigits(written, digits);

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
