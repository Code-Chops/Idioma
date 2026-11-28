using System.Diagnostics;

namespace CodeChops.Idioma.Internals;

/// <summary>
/// <para>
/// <b>Don't use this attribute directly! It's used by the source generator.</b>
/// </para>
/// <para>
/// Marks that the default wording of a text shows a value it takes, such as <c>name</c> in
/// <c>public string Greeting(string name) => $"Hello, {name}!";</c>. The build of every set implementing the contract
/// then checks that its method uses the value too (<c>LOC402</c>), also in another assembly. The source generator
/// writes this on a method of a contract it generates, once for each value the default wording shows.
/// </para>
/// </summary>
// Read by constructor position. On the method, not the parameter, because an IDE's "Implement interface" copies a
// parameter's attributes into the set, where this one would warn that it's obsolete.
[Obsolete("Don't use this attribute directly. Only generated code applies this attribute on a method of a contract, for each value the default wording shows.")]
[EditorBrowsable(EditorBrowsableState.Never)]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ShownValueAttribute : Attribute
{
    /// <summary>
    /// The name of the parameter whose value the default wording shows.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string Parameter { get; }

    /// <summary>
    /// Marks a value the default wording shows.
    /// </summary>
    /// <param name="parameter">The name of the parameter whose value the default wording shows.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ShownValueAttribute(string parameter)
    {
        Debug.Assert(!String.IsNullOrWhiteSpace(parameter));

        this.Parameter = parameter;
    }
}
