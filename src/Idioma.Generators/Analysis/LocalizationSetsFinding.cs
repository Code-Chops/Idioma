namespace CodeChops.Idioma.Generators;

/// <summary>
/// A diagnostic to report. <see cref="SuggestedName"/> is the name a code fix applies: the class name (<c>LOC200</c>),
/// the language to list (<c>LOC202</c>) or the contract to implement (<c>LOC205</c>). <see cref="AlternativeName"/> is
/// a second language a fix of <c>LOC202</c> offers, where a name can mean either. <see cref="NewSet"/> is the set it
/// adds (<c>LOC201</c>, <c>LOC700</c>).
/// </summary>
internal readonly record struct LocalizationSetsFinding(
    LocalizationSetsRule Rule,
    DiagnosticLocation Location,
    EquatableArray<string> Arguments,
    string? SuggestedName = null,
    NewLocalizationSet? NewSet = null,
    string? AlternativeName = null);

/// <summary>
/// A set the code fix of <c>LOC201</c> or <c>LOC700</c> adds in its own file, with its declaration without a body. The
/// namespace is empty for the global one, and <see langword="null"/> for a set of a library's contract, which gets the
/// app's namespace.
/// </summary>
internal readonly record struct NewLocalizationSet(
    string? Namespace,
    string Declaration);
