namespace CodeChops.Idioma.Internals;

/// <summary>
/// Records that the default wording of a text shows a value it takes, such as <c>name</c> in
/// <c>public string Greeting(string name) => $"Hello, {name}!";</c>, so that the build of every set implementing the
/// contract checks that its wording uses the value too (<c>LOC021</c>), also in another assembly. The source generator
/// writes this on the parameters of a contract it generates; you never need to.
/// </summary>
// Located by its metadata-name string.
[EditorBrowsable(EditorBrowsableState.Never)]
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class ShownValueAttribute : Attribute;
