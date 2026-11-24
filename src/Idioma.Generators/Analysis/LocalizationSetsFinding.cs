namespace CodeChops.Idioma.Generators;

/// <summary>
/// A diagnostic to report: which rule, where, and the arguments of its message. A finding a name resolves carries it,
/// for the code fix to apply: the name the class should carry (<c>LOC200</c>), the language to list
/// (<c>LOC202</c>), the contract it should implement (<c>LOC204</c>), or the set to add (<c>LOC201</c> and
/// <c>LOC700</c>), which also carries how to write that set.
/// </summary>
internal readonly record struct LocalizationSetsFinding(
    LocalizationSetsRule Rule,
    DiagnosticLocation Location,
    EquatableArray<string> Arguments,
    string? SuggestedName = null,
    NewLocalizationSet? NewSet = null);

/// <summary>
/// A set the code fix of <c>LOC201</c> or <c>LOC700</c> adds in a file of its own: its namespace, empty for the global
/// one, and its declaration without a body, such as
/// <c>public class ShopLocalizationDe : ILocalizationSet, IShopLocalization</c>. The namespace is
/// <see langword="null"/> for a set of a library's contract (<c>LOC700</c>), which the fix gives the namespace of the
/// app.
/// </summary>
internal readonly record struct NewLocalizationSet(
    string? Namespace,
    string Declaration);
