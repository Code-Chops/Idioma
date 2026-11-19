namespace CodeChops.Idioma;

/// <summary>
/// Makes a culture the one texts follow on this thread while a text of a set is worded for it, and puts back what it
/// changed afterwards. For a text that takes values, it sets up to three things:
/// <list type="bullet">
/// <item><see cref="Current"/>: the culture, the sets, and the app's languages of the text being worded.</item>
/// <item><see cref="CultureInfo.CurrentUICulture"/>, for what the method of the set reads from it.</item>
/// <item><see cref="CultureInfo.CurrentCulture"/>, where the values are formatted for a culture the thread doesn't
/// have.</item>
/// </list>
/// </summary>
/// <remarks>
/// A set's method words a text that takes values, and formats the values as it runs, as an interpolated string does.
/// So the method has to run in the culture the text is worded for. A text among the values reads <see cref="Current"/>
/// and shows in that culture too, from the same sets.
/// <para>
/// A text without values is worded by a property of its set, which may show another text, as in
/// <c>$"Press {ShopLocalization.Checkout} to pay"</c>. For such a text the scope sets <see cref="Current"/> alone
/// (<see cref="Follow"/>), so the text it shows follows the culture and the thread's cultures stay as they are.
/// </para>
/// </remarks>
// All of a text's culture switching lives here. LocalizableText decides when a text enters a scope, in
// LocalizableText.Values.cs, which also explains the model, and this type does the switching and the putting back. A
// scope is disposed in any case, by a using, because the method of a set is the consumer's and may throw.
internal readonly ref struct WordingScope
{
    /// <summary>
    /// What the text being worded on this thread is worded for. The default has no culture, for a thread that words no
    /// text that takes values.
    /// </summary>
    // Per thread, which is enough: a set words a text in one call, and the scope ends before that call returns.
    [ThreadStatic]
    private static WordingContext Entered;

    /// <summary>
    /// What the text that takes values being worded on this thread is worded for: its culture, its sets, and the app's
    /// languages. A text among its values reads them here and shows as that text does. The default has no culture.
    /// </summary>
    public static WordingContext Current => Entered;

    private bool IsEntered { get; }

    // For a scope in which a culture is only followed: what was followed before it.
    private bool IsFollowed { get; }
    private LocalizationRegistry.CultureLookup? PreviousFollowed { get; }
    private CultureInfo? PreviousFollowedCulture { get; }
    private FollowedValues PreviousFollowedValues { get; }
    private WordingContext PreviousWording { get; }

    // The execution context from before a culture changed, which puts both cultures back in one step. A culture is an
    // AsyncLocal, so each time one is set, the runtime copies the context with every AsyncLocal in it. That's a few
    // hundred bytes in a web request, and setting both cultures back would copy it twice more. Restoring the context
    // copies nothing. It's null where no culture changed. It's also null where the flow of the context is suppressed,
    // because Capture gives none then, so the cultures are set back one by one.
    private ExecutionContext? PreviousContext { get; }

    // ReSharper disable once InconsistentNaming - Same casing as the property of CultureInfo.
    private CultureInfo? PreviousUICulture { get; }
    private CultureInfo? PreviousCulture { get; }

    /// <param name="culture">The culture texts follow in the scope.</param>
    /// <param name="registry">The sets a text among the values is looked up in.</param>
    /// <param name="appLanguages">The app's languages a text among the values is shown for.</param>
    /// <param name="valuesCulture">
    /// The culture to format the values for, or <see langword="null"/> to format them as the thread does.
    /// </param>
    public WordingScope(CultureInfo culture, LocalizationRegistry? registry, ImmutableArray<string> appLanguages, CultureInfo? valuesCulture)
    {
        // The thread's own cultures too, for what the method of a set reads from them: the format of a value, and
        // anything else that follows the UI culture, such as a resource file. The UI culture goes first, because only
        // its setter can throw, on the name of a culture of a derived class. Nothing has changed yet then.
        var uiCulture = CultureInfo.CurrentUICulture;
        var currentCulture = CultureInfo.CurrentCulture;
        var changesUiCulture = !culture.Equals(uiCulture);
        var changesCulture = valuesCulture is not null && !ReferenceEquals(valuesCulture, currentCulture);

        if (changesUiCulture || changesCulture)
            this.PreviousContext = ExecutionContext.Capture();

        if (changesUiCulture)
        {
            CultureInfo.CurrentUICulture = culture;
            this.PreviousUICulture = uiCulture;
        }

        if (changesCulture)
        {
            CultureInfo.CurrentCulture = valuesCulture!;
            this.PreviousCulture = currentCulture;
        }

        this.PreviousWording = Entered;
        Entered = new WordingContext(culture, registry, appLanguages);
        this.IsEntered = true;
    }

    // A scope in which texts follow a culture, as a lookup resolves it, and the thread's cultures stay as they are. The
    // culture is the one given, not the lookup's, which is the first culture of that name the registry saw.
    private WordingScope(LocalizationRegistry.CultureLookup lookup, CultureInfo culture, FollowedValues values)
    {
        this.PreviousFollowed = Entered.Followed;
        this.PreviousFollowedCulture = Entered.FollowedCulture;
        this.PreviousFollowedValues = Entered.FollowedValues;
        this.IsFollowed = true;

        Entered.Followed = lookup;
        Entered.FollowedCulture = culture;
        Entered.FollowedValues = values;
    }

    /// <summary>
    /// A scope in which texts follow a culture and the thread's cultures stay as they are, for a text without values
    /// that is shown for a culture asked for. A text it shows is worded for that culture, from the sets of the lookup's
    /// registry.
    /// </summary>
    /// <param name="lookup">How the registry resolves <paramref name="culture"/>.</param>
    /// <param name="culture">The culture to follow. It also formats the values of a text that is shown.</param>
    // Two references are written, to a lookup the registry keeps and to the culture. Setting the UI culture copies the
    // execution context, and writing a whole context here made a text without values take 32 ns where it takes 12 ns
    // (measured 2026-11-19). Such a text is shown far more often than one that takes values.
    public static WordingScope Follow(LocalizationRegistry.CultureLookup lookup, CultureInfo culture)
    {
        return new WordingScope(lookup, culture, FollowedValues.OfCulture);
    }

    /// <summary>
    /// A scope in which texts follow the culture of a language and the thread's cultures stay as they are, for a text
    /// without values that is worded in a language its reader may not read. No scope where the language has no
    /// culture, as under invariant globalization, or where there's no registry to look in.
    /// </summary>
    /// <remarks>
    /// Inside a text being worded, the scope keeps that text's sets and the format it chose for values, as
    /// <see cref="Enter"/> does.
    /// </remarks>
    /// <param name="culture">The culture of the language, or <see langword="null"/> where it has none.</param>
    /// <param name="registry">The sets to look in, where no text is being worded.</param>
    public static WordingScope FollowLanguage(CultureInfo? culture, LocalizationRegistry? registry)
    {
        if (culture is null)
            return default;

        var wording = Entered;

        if (wording.Followed is { } followed)
            return new WordingScope(followed.Registry.Resolve(culture), culture, wording.FollowedValues);

        if (wording.Culture is not null)
            return wording.Registry is { } wordingRegistry ? new WordingScope(wordingRegistry.Resolve(culture), culture, FollowedValues.OfThread) : default;

        return registry is null ? default : new WordingScope(registry.Resolve(culture), culture, FollowedValues.OfReader);
    }

    /// <summary>
    /// The culture texts follow on this thread when they're given none. That's the culture of the text being worded,
    /// else that of <see cref="LocalizationRegistry.CurrentUICultureProvider"/>, else
    /// <see cref="CultureInfo.CurrentUICulture"/>.
    /// </summary>
    /// <param name="providedCulture">
    /// The provider's culture where texts follow it, else <see langword="null"/>. A provider gives the user's culture
    /// where the thread's cultures aren't the user's, as on a server. So values are formatted for the provided culture,
    /// not as the thread formats them.
    /// </param>
    public static CultureInfo GetFollowedCulture(out CultureInfo? providedCulture)
    {
        var wordingCulture = Entered.FollowedCulture ?? Entered.Culture;
        providedCulture = wordingCulture is null ? LocalizationRegistry.CurrentUICultureProvider?.Invoke() : null;

        var culture = wordingCulture ?? providedCulture ?? CultureInfo.CurrentUICulture;
        return culture;
    }

    /// <summary>
    /// A scope for the culture of a language, or no scope where the language has no culture, as under invariant
    /// globalization.
    /// </summary>
    /// <remarks>
    /// Inside a text being worded, the scope keeps that text's sets, its app languages, and the format it chose for
    /// values. Elsewhere it takes the given sets and their languages, and formats the values for the user of this
    /// thread. That's the provided culture where there is one, and else the thread's own format.
    /// </remarks>
    /// <param name="culture">The culture of the language, or <see langword="null"/> where it has none.</param>
    /// <param name="registry">The sets to look in, where no text is being worded.</param>
    /// <param name="providedCulture">The provider's culture (<see cref="GetFollowedCulture"/>).</param>
    public static WordingScope Enter(CultureInfo? culture, LocalizationRegistry? registry, CultureInfo? providedCulture)
    {
        if (culture is null)
            return default;

        var wording = Entered;

        var scope = wording.Culture is null
            ? new WordingScope(culture, registry, registry?.AppLanguages ?? LocalizationRegistry.SupportedLanguages, valuesCulture: providedCulture)
            : new WordingScope(culture, wording.Registry, wording.AppLanguages, valuesCulture: null);

        return scope;
    }

    public void Dispose()
    {
        if (this.IsFollowed)
        {
            Entered.Followed = this.PreviousFollowed;
            Entered.FollowedCulture = this.PreviousFollowedCulture;
            Entered.FollowedValues = this.PreviousFollowedValues;
            return;
        }

        if (!this.IsEntered)
            return;

        Entered = this.PreviousWording;

        // Also undoes an AsyncLocal the method of the set changed, which ends with the scope like the cultures. That
        // holds only for a scope that changed a culture. A scope that changed none captured no context, so such a change
        // stays. Capturing the context for every scope would end it there too, and cost every text with a parameter
        // about 8 ns on .NET, 47 to 55 ns (measured 2026-11-19).
        if (this.PreviousContext is not null)
        {
            ExecutionContext.Restore(this.PreviousContext);
            return;
        }

        if (this.PreviousUICulture is not null)
            CultureInfo.CurrentUICulture = this.PreviousUICulture;

        if (this.PreviousCulture is not null)
            CultureInfo.CurrentCulture = this.PreviousCulture;
    }
}
