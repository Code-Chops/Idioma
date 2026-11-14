namespace CodeChops.Idioma.Generators;

/// <summary>
/// Reads the placeholders of a wording as <c>String.Format</c> reads a composite format string: <c>{0}</c>,
/// <c>{1,-10}</c>, and <c>{0:N2}</c> are placeholders, and <c>{{</c> and <c>}}</c> are a literal brace.
/// </summary>
internal static class FormatPlaceholders
{
    /// <summary>
    /// What a message says about a wording that is no valid format string.
    /// </summary>
    public const string InvalidDescription = "a brace that is no placeholder (write a literal brace as {{ or }})";

    /// <summary>
    /// The index from which a placeholder is read as invalid. Not a limit of <c>String.Format</c>, which parses any
    /// number, but no wording needs a million arguments: <c>Format</c> would throw for an index that no call can fill,
    /// and a bound keeps the number from overflowing.
    /// </summary>
    private const int IndexLimit = 1_000_000;

    /// <summary>
    /// The distinct indexes of the placeholders of a wording, in ascending order, or <see langword="false"/> when it
    /// is no valid format string: a lone brace, or a placeholder <c>String.Format</c> would reject.
    /// </summary>
    public static bool TryRead(string wording, out ImmutableArray<int> placeholders)
    {
        var indexes = new SortedSet<int>();
        placeholders = ImmutableArray<int>.Empty;

        for (var i = 0; i < wording.Length; i++)
        {
            var c = wording[i];

            if (c is '}')
            {
                if (i + 1 < wording.Length && wording[i + 1] is '}')
                {
                    i++;
                    continue;
                }

                return false;
            }

            if (c is not '{')
                continue;

            if (i + 1 < wording.Length && wording[i + 1] is '{')
            {
                i++;
                continue;
            }

            if (ReadPlaceholder(wording, ref i) is not { } index)
                return false;

            indexes.Add(index);
        }

        placeholders = [.. indexes];

        return true;
    }

    /// <summary>
    /// The placeholders as a message names them: <c>placeholders {0} and {1}</c>, or <c>no placeholders</c>.
    /// </summary>
    public static string Describe(ImmutableArray<int> placeholders)
    {
        if (placeholders.IsEmpty)
            return "no placeholders";

        var items = placeholders.Select(static index => $"{{{index}}}").ToArray();

        var description = items.Length is 1
            ? $"placeholder {items[0]}"
            : $"placeholders {String.Join(", ", items, startIndex: 0, items.Length - 1)}{(items.Length > 2 ? "," : "")} and {items[items.Length - 1]}";

        return description;
    }

    /// <summary>
    /// Reads the placeholder that starts at the brace at <paramref name="position"/>, and leaves the position at its
    /// closing brace; <see langword="null"/> when it is no valid placeholder.
    /// </summary>
    private static int? ReadPlaceholder(string wording, ref int position)
    {
        // The index: digits right after the brace.
        var i = position + 1;
        var index = 0;
        var start = i;

        while (i < wording.Length && wording[i] is >= '0' and <= '9')
        {
            index = (index * 10) + (wording[i] - '0');
            i++;

            if (index >= IndexLimit)
                return null;
        }

        if (i == start)
            return null;

        SkipSpaces(wording, ref i);

        // The alignment: a comma, then an optionally negative number.
        if (i < wording.Length && wording[i] is ',')
        {
            i++;
            SkipSpaces(wording, ref i);

            if (i < wording.Length && wording[i] is '-')
                i++;

            var alignment = i;
            while (i < wording.Length && wording[i] is >= '0' and <= '9')
                i++;

            if (i == alignment)
                return null;

            SkipSpaces(wording, ref i);
        }

        // The format: a colon, then anything up to the closing brace, which may hold no opening brace.
        if (i < wording.Length && wording[i] is ':')
        {
            while (i < wording.Length && wording[i] is not '}')
            {
                if (wording[i] is '{')
                    return null;

                i++;
            }
        }

        if (i >= wording.Length || wording[i] is not '}')
            return null;

        position = i;

        return index;

        static void SkipSpaces(string text, ref int i)
        {
            while (i < text.Length && text[i] is ' ')
                i++;
        }
    }
}
