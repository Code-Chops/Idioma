using System.Diagnostics;

namespace CodeChops.Idioma.Internals;

/// <summary>
/// <para>
/// <b>Don't use this attribute directly! It's used by the source generator.</b>
/// </para>
/// <para>
/// Marks that an assembly holds a set of a public contract, and the language the set serves. The build of an app
/// that references the assembly reads these attributes instead of searching the assembly's types. It reports each
/// language of the app that a contract has no set for (<c>LOC700</c>).
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
    /// The language the set serves, or <c>*</c> when the build couldn't read it. Such a set may serve any language.
    /// The build can't read the set's <see cref="ILocalizationSet.LocalizationLanguageCode"/> in these cases:
    /// <list type="bullet">
    /// <item>It isn't a constant.</item>
    /// <item>It comes from another assembly.</item>
    /// <item>It holds a constant that isn't a culture name.</item>
    /// </list>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string Language { get; }

    /// <summary>
    /// Marks a set.
    /// </summary>
    /// <param name="contract">The full name of the contract. It's a string, so nothing keeps the type when trimming.</param>
    /// <param name="language">The language the set serves, or <c>*</c> when the build couldn't read it.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public LocalizationSetAttribute(string contract, string language)
    {
        Debug.Assert(!String.IsNullOrWhiteSpace(contract));
        Debug.Assert(!String.IsNullOrWhiteSpace(language));

        this.Contract = contract;
        this.Language = language;
    }
}
