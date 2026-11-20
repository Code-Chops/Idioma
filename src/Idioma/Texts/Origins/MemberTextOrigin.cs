namespace CodeChops.Idioma;

/// <summary>
/// The text of a member marked <c>[Localized]</c>: a property, a field, an enum member, or a type. It has wording of
/// its own in every language, as an inline text has, and a key, because its member gives it a name. Created once per
/// text.
/// </summary>
internal sealed class MemberTextOrigin : TextOrigin
{
    /// <summary>
    /// The name of the member, such as <c>City</c>, or of the type for a type's own text. The source generator writes
    /// it, so that reading it builds no string.
    /// </summary>
    public override string Key { get; }

    public MemberTextOrigin(string fullKey, string key)
        : base(fullKey)
    {
        ArgumentNullException.ThrowIfNull(key);

        this.Key = key;
    }
}
