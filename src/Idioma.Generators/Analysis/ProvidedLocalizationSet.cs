namespace CodeChops.Idioma.Generators;

/// <summary>
/// That an assembly holds a set of a public contract for a language. <see cref="Reworded"/> is the set of another
/// assembly it rewords, <see langword="null"/> where it rewords none or is registered by hand, and
/// <see cref="Bases"/> are the concrete sets it derives from, nearest first.
/// </summary>
internal readonly record struct ProvidedLocalizationSet(
    string Contract,
    string Language,
    string Set,
    string? Reworded,
    EquatableArray<string> Bases)
{
    /// <summary>
    /// A set of this assembly, for each public contract it implements.
    /// </summary>
    public static ProvidedLocalizationSet Of(string contract, DiscoveredLocalization set)
    {
        var provided = new ProvidedLocalizationSet(
            contract,
            set.Language!,
            set.MetadataName,
            set.IsExcluded ? null : set.Reworded?.MetadataName,
            set.BaseSetNames);

        return provided;
    }
}
