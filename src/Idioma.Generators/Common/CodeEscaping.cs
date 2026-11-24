namespace CodeChops.Idioma.Generators;

/// <summary>
/// Writes names and text as generated code and its doc comments can hold them.
/// </summary>
internal static class CodeEscaping
{
    /// <summary>
    /// Text as a doc comment can hold it: markup characters as entities, and a character XML does not allow, such as a
    /// control character or half of a surrogate pair, as the escape C# writes it with (<c>\u0001</c>). Such a character
    /// would make the comment invalid (<c>CS1570</c>), also when written as an entity.
    /// </summary>
    public static string EscapeXml(string text)
    {
        var escaped = new StringBuilder(text.Length);

        for (var i = 0; i < text.Length; i++)
        {
            var character = text[i];

            if (character is '&')
                escaped.Append("&amp;");
            else if (character is '<')
                escaped.Append("&lt;");
            else if (character is '>')
                escaped.Append("&gt;");
            else if (Char.IsHighSurrogate(character) && i + 1 < text.Length && Char.IsLowSurrogate(text[i + 1]))
                escaped.Append(character).Append(text[++i]);
            else if (character is '\t' or '\n' or '\r' or (>= ' ' and <= '\uD7FF') or (>= '\uE000' and <= '\uFFFD'))
                escaped.Append(character);
            else
                escaped.Append("\\u").Append(((int)character).ToString("X4", System.Globalization.CultureInfo.InvariantCulture));
        }

        return escaped.ToString();
    }

    /// <summary>
    /// A name as code can write it: a keyword gets an <c>@</c>, as for a text declared as <c>@event</c>.
    /// </summary>
    // So does scoped, which is a keyword only in front of a parameter: the function that picks the set of a text
    // lists its parameters by name alone, and (set, scoped) reads there as a modifier without a parameter (CS1001).
    public static string EscapeIdentifier(string name)
    {
        var escapeIdentifier = SyntaxFacts.GetKeywordKind(name) is SyntaxKind.None && name is not "scoped" ? name : $"@{name}";
        return escapeIdentifier;
    }

    /// <summary>
    /// A namespace as code can write it, each part escaped like a name (<c>@event.Shop</c>).
    /// </summary>
    public static string EscapeNamespace(string @namespace)
    {
        var escapeNamespace = @namespace.Length is 0 ? @namespace : String.Join(".", @namespace.Split('.').Select(EscapeIdentifier));
        return escapeNamespace;
    }

    /// <summary>
    /// Text as a part of a name: every character a name can't hold becomes an underscore, as in <c>My_App</c> for
    /// <c>My-App</c>. Nothing is put in front of it, so it's only the rest of a name that starts with something else.
    /// </summary>
    public static string ToIdentifierPart(string? text)
    {
        var part = new StringBuilder();

        foreach (var character in text ?? "")
            part.Append(SyntaxFacts.IsIdentifierPartCharacter(character) ? character : '_');

        return part.ToString();
    }
}
