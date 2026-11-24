namespace CodeChops.Idioma.Generators;

/// <summary>
/// That an assembly holds a set of a public contract for a language, or for <see cref="UnreadableLanguage"/>.
/// </summary>
internal readonly record struct ProvidedLocalizationSet(
    string Contract,
    string Language)
{
    /// <summary>
    /// What stands for the language of a set whose language its build could not read, as its
    /// <c>ILocalizationSet.LocalizationLanguageCode</c> is no constant or comes from another assembly. It is no culture name, so it
    /// serves no language, and a build that knows it takes the contract to miss none.
    /// </summary>
    public const string UnreadableLanguage = "*";
}
