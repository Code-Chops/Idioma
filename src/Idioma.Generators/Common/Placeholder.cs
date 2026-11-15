namespace CodeChops.Idioma.Generators;

/// <summary>
/// Finds a placeholder of a format string in a wording: <c>{0}</c>, with or without an alignment or a format, as in
/// <c>{1,8}</c> and <c>{0:N2}</c>. Nothing fills one in, so in a wording it is a value that was meant to be there and
/// is not (<c>LOC016</c>).
/// </summary>
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
            var end = start + 1;

            while (end < wording.Length && wording[end] is >= '0' and <= '9')
                end++;

            // Without a number, no placeholder.
            if (end == start + 1)
                continue;

            // An alignment or a format follows the number, and holds no brace itself.
            if (end < wording.Length && wording[end] is ',' or ':')
                while (end < wording.Length && wording[end] is not ('{' or '}'))
                    end++;

            if (end < wording.Length && wording[end] is '}')
                return wording.Substring(start, end - start + 1);
        }

        return null;
    }
}
