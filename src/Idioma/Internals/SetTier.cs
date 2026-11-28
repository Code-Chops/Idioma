namespace CodeChops.Idioma.Internals;

/// <summary>
/// <para>
/// <b>Don't use this type directly! This code is called by source-generated code.</b>
/// </para>
/// <para>
/// Where the sets of <see cref="GeneratedCode.RegisterSets"/> come from. A later tier wins over an earlier one for the
/// same contract and language, and the sets of <see cref="LocalizationRegistry.Register(ReadOnlySpan{ILocalizationSet})"/>
/// win over every tier.
/// </para>
/// </summary>
[Obsolete("Don't use this type directly. Only generated code should call this.")]
[EditorBrowsable(EditorBrowsableState.Never)]
public enum SetTier
{
    /// <summary>
    /// A library's own sets, which give the wording of a contract in its own assembly.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    LibrarySets,

    /// <summary>
    /// A library's rewordings of another assembly's sets.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    Rewordings,

    /// <summary>
    /// The app's rewordings of another assembly's sets.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    ApplicationRewordings,
}
