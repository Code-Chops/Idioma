namespace CodeChops.Idioma.Internals;

/// <summary>
/// <para>
/// Don't use this attribute directly! It's used by the source generator.
/// </para>
/// <para>
/// Records that a library writes texts inline, with <c>LocalizedText.Inline</c> or with <c>[Localized]</c> on a
/// member. No app that references the library can add a language to those texts. So the app's build warns about each
/// of its languages the library lacks (<c>LOC701</c>).
/// </para>
/// </summary>
// Located by its metadata-name string.
[Obsolete("Don't use this attribute directly. Only generated code applies this attribute. The source generator writes it for an assembly that uses LocalizedText.Inline or [Localized].")]
[EditorBrowsable(EditorBrowsableState.Never)]
// More than once: the generated factory's file applies it for LocalizedText.Inline, and the file with the texts of
// members applies it for [Localized].
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class InlineTextsAttribute : Attribute;
