namespace CodeChops.Idioma.Internals;

/// <summary>
/// Records that an assembly holds a set of a translations contract for a language, so that the build of an assembly
/// referencing it knows which languages exist without searching its types. The source generator writes this; you never
/// need to.
/// </summary>
// Located by its metadata-name string, and read by constructor position.
[EditorBrowsable(EditorBrowsableState.Never)]
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class TranslationSetAttribute : Attribute
{
    /// <summary>
    /// The full name of the contract.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string Contract { get; }

    /// <summary>
    /// The language the set serves.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string Language { get; }

    /// <summary>
    /// Records a set.
    /// </summary>
    /// <param name="contract">The full name of the contract. A string, so that nothing keeps the type when trimming.</param>
    /// <param name="language">The language the set serves.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public TranslationSetAttribute(string contract, string language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contract);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);

        this.Contract = contract;
        this.Language = language;
    }
}
