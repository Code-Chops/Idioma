namespace CodeChops.Idioma.Internals;

/// <summary>
/// Records the placeholders the default wording of a text holds, such as <c>{0}</c> in <c>"Hello, {0}!"</c>, so that
/// the build of every set implementing the contract checks that its wording holds the same (<c>LOC016</c>), also in
/// another assembly. The source generator writes this on each text of a contract it generates; you never need to.
/// </summary>
// Located by its metadata-name string, and read by constructor position.
[EditorBrowsable(EditorBrowsableState.Never)]
[AttributeUsage(AttributeTargets.Property)]
public sealed class FormatPlaceholdersAttribute : Attribute
{
    /// <summary>
    /// The indexes of the placeholders, in ascending order: <c>0</c> and <c>1</c> for <c>"{1} of {0}"</c>. Empty for
    /// wording without placeholders.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public IReadOnlyList<int> Placeholders { get; }

    /// <summary>
    /// Records the placeholders of a default wording.
    /// </summary>
    /// <param name="placeholders">The indexes of the placeholders, in ascending order.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public FormatPlaceholdersAttribute(params int[] placeholders)
    {
        ArgumentNullException.ThrowIfNull(placeholders);

        this.Placeholders = [.. placeholders];
    }
}
