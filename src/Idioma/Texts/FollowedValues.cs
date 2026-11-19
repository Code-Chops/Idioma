namespace CodeChops.Idioma;

/// <summary>
/// Which format a text that takes values gives them where a culture is only followed.
/// </summary>
internal enum FollowedValues : byte
{
    /// <summary>The thread's format, inside a text being worded, which chose it.</summary>
    OfThread,

    /// <summary>The followed culture's format, for a text shown for a culture asked for.</summary>
    OfCulture,

    /// <summary>The reader's format, for a text worded in a language: the provided culture's, else the thread's.</summary>
    OfReader,
}
