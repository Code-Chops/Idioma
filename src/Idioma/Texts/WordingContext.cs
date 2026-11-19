namespace CodeChops.Idioma;

/// <summary>
/// What a text of a set is worded for: a culture, the sets to look in, and the app's languages. The default has no
/// culture, for a thread that words no such text.
/// </summary>
// Not readonly: a scope that only follows a culture writes Followed alone, in place.
internal struct WordingContext(CultureInfo culture, LocalizationRegistry? registry, ImmutableArray<string> appLanguages)
{
    public CultureInfo? Culture { get; } = culture;
    public LocalizationRegistry? Registry { get; } = registry;
    public ImmutableArray<string> AppLanguages { get; } = appLanguages;

    /// <summary>
    /// The culture texts follow while the thread's cultures are something else, as its registry resolves it, or
    /// <see langword="null"/> (see <see cref="WordingScope.Follow"/>). It comes before <see cref="Culture"/>, because
    /// a scope that sets the thread's cultures clears it for as long as it lasts. A text that takes values enters such
    /// a scope here, so that its set's method runs in the culture it words.
    /// </summary>
    public LocalizationRegistry.CultureLookup? Followed { get; set; }

    /// <summary>
    /// Which format a text that takes values gives them where a culture is only followed.
    /// </summary>
    public FollowedValues FollowedValues { get; set; }

    /// <summary>
    /// The culture to format the values for, of a text that takes values and is shown where
    /// <paramref name="followed"/> is only followed, or <see langword="null"/> to format them as the thread does.
    /// </summary>
    public readonly CultureInfo? FindValuesCulture(CultureInfo followed)
    {
        var provided = this.FollowedValues is FollowedValues.OfThread ? null : LocalizationRegistry.CurrentUICultureProvider?.Invoke();

        var valuesCulture = this.FollowedValues switch
        {
            // As for a text shown for a culture asked for: that culture, unless the thread follows it already and
            // formats the values as the user does.
            FollowedValues.OfCulture => provided is null && followed.Equals(CultureInfo.CurrentUICulture) ? null : followed,
            FollowedValues.OfReader => provided,
            _ => null,
        };

        return valuesCulture;
    }
}
