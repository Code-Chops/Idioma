namespace CodeChops.Idioma.Internals;

/// <summary>
/// <para>
/// Don't use this attribute directly! It's used by the source generator.
/// </para>
/// <para>
/// Records that a library writes texts inline with <c>LocalizableText.Inline</c>. No app that references the library
/// can add a language to those texts. So the app's build warns about each of its languages the library lacks
/// (<c>LOC601</c>).
/// </para>
/// </summary>
// Located by its metadata-name string.
[Obsolete("Don't use this attribute directly. Only generated code applies this attribute. The source generator writes it for an assembly that uses LocalizableText.Inline.")]
[EditorBrowsable(EditorBrowsableState.Never)]
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class InlineTextsAttribute : Attribute;
