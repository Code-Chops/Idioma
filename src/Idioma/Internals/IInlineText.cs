namespace CodeChops.Idioma.Internals;

/// <summary>
/// Marks <see cref="LocalizableText"/> as the type that <c>LocalizableText.Translated</c> belongs to. The source
/// generator writes <c>Translated</c> for the text type that implements this interface for itself; you never need to.
/// </summary>
/// <remarks>
/// <see cref="LocalizableText"/> implements the interface for itself. <see cref="ResourceText"/> derives from it, and
/// so implements the interface for <see cref="LocalizableText"/> as well, not for itself. That keeps
/// <c>Translated</c> off <see cref="ResourceText"/>, where it would give a <see cref="LocalizableText"/> under the
/// name of another type.
/// </remarks>
/// <typeparam name="TSelf">The text type that implements the interface.</typeparam>
// Located by its metadata-name string: the generated factory names it in the constraint of its extension.
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IInlineText<TSelf>
    where TSelf : LocalizableText;
