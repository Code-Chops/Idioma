using System.Diagnostics;

namespace CodeChops.Idioma.Internals;

/// <summary>
/// <para>
/// <b>Don't use this attribute directly! It's used by the source generator.</b>
/// </para>
/// <para>
/// Marks that an assembly holds a set of a public contract, the language the set serves, and what it derives from. The
/// build of an app that references the assembly reads these attributes instead of searching the assembly's types. It
/// reports each language of the app that a contract has no set for (<c>LOC700</c>), and rewordings that leave out a
/// set (<c>LOC208</c>, <c>LOC209</c>).
/// </para>
/// </summary>
// Matched by its full name and read by constructor position.
[Obsolete("Don't use this attribute directly. Only generated code applies this attribute. The source generator writes it for every set of a public contract.")]
[EditorBrowsable(EditorBrowsableState.Never)]
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class LocalizationSetAttribute : Attribute
{
    /// <summary>
    /// The full name of the contract.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string Contract { get; }

    /// <summary>
    /// The language the set serves, such as <c>nl</c>.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string Language { get; }

    /// <summary>
    /// The full name of the set.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string Set { get; }

    /// <summary>
    /// The full name of the set of another assembly it rewords, or <see langword="null"/> where it rewords none or is
    /// registered by hand.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string? Reworded { get; }

    /// <summary>
    /// The full names of the concrete sets it derives from, nearest first.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public IReadOnlyList<string> Bases { get; }

    /// <summary>
    /// Marks a set.
    /// </summary>
    /// <param name="contract">The full name of the contract. It's a string, so nothing keeps the type when trimming.</param>
    /// <param name="language">The language the set serves, such as <c>nl</c>.</param>
    /// <param name="set">The full name of the set.</param>
    /// <param name="reworded">The full name of the set of another assembly it rewords, or <see langword="null"/>.</param>
    /// <param name="bases">The full names of the concrete sets it derives from, nearest first.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public LocalizationSetAttribute(string contract, string language, string set, string? reworded, params string[] bases)
    {
        Debug.Assert(!String.IsNullOrWhiteSpace(contract));
        Debug.Assert(!String.IsNullOrWhiteSpace(language));
        Debug.Assert(!String.IsNullOrWhiteSpace(set));

        this.Contract = contract;
        this.Language = language;
        this.Set = set;
        this.Reworded = reworded;
        this.Bases = bases ?? [];
    }
}
