namespace CodeChops.Idioma.Internals;

/// <summary>
/// <para>
/// Don't use this attribute directly! It's used by the source generator.
/// </para>
/// <para>
/// Marks an interface as the contract of a set, such as <c>IShopLocalization</c>. The source generator writes this on
/// every contract it generates. The contract doesn't derive from <see cref="ILocalizationSet"/>, so this is how the
/// build recognizes it, in its own assembly and in every assembly that references it. The build then holds each set
/// that implements the contract to its texts.
/// </para>
/// </summary>
// Matched by its name and namespaces, part by part.
[Obsolete("Don't use this attribute directly. Only generated code should apply this attribute. The source generator writes it on every contract.")]
[EditorBrowsable(EditorBrowsableState.Never)]
[AttributeUsage(AttributeTargets.Interface)]
public sealed class LocalizationContractAttribute : Attribute;
