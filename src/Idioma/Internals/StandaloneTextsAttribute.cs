namespace CodeChops.Idioma.Internals;

/// <summary>
/// <para>
/// <b>Don't use this attribute directly! It's used by the source generator.</b>
/// </para>
/// <para>
/// Marks that a library writes standalone texts, with <c>LocalizedText.Inline</c> or with <c>[Localized]</c> on a
/// member. Such a text carries its wording itself, so no app that references the library can add a language to it.
/// The app's build warns about each of its languages the library lacks (<c>LOC701</c>).
/// </para>
/// </summary>
// Located by its metadata-name string.
[Obsolete("Don't use this attribute directly. Only generated code applies this attribute. The source generator writes it for an assembly that uses LocalizedText.Inline or [Localized].")]
[EditorBrowsable(EditorBrowsableState.Never)]
// The generated factory's file and the file with the texts of members each apply it.
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class StandaloneTextsAttribute : Attribute;
