namespace CodeChops.Idioma;

/// <summary>
/// Records that an assembly writes texts inline with <c>LocalizableText.Translated</c>, which no assembly referencing
/// it can add a language to. The build of an assembly supporting a language this one lacks warns about it
/// (<c>LOC009</c>). The source generator writes this; you never need to.
/// </summary>
// Located by its metadata-name string.
[EditorBrowsable(EditorBrowsableState.Never)]
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class InlineTextsAttribute : Attribute;
