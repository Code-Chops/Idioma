namespace CodeChops.Idioma.Generators;

/// <summary>
/// Checks on ASCII characters, which netstandard2.0 does not have on <see cref="char"/>.
/// </summary>
internal static class Ascii
{
    /// <summary>
    /// Whether a character is one of the letters A to Z, in either letter case.
    /// </summary>
    public static bool IsLetter(char character) => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
}
