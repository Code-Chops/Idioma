namespace CodeChops.Idioma;

/// <summary>
/// Keeps the build from picking something up. On a <see cref="ITranslationSet">set</see>, it keeps the set from
/// registering itself, so that it applies only once you register it with
/// <see cref="TranslationsRegistry.Register(ReadOnlySpan{ITranslationSet})">TranslationsRegistry.Register</see>. On a
/// public property or method of the set of your default language, it keeps that member from being a text.
/// </summary>
/// <remarks>
/// <para>
/// On a member: every public string property of the set of your default language is a text, and so is every public
/// method that returns a string and takes parameters. Mark one that is no text, such as a helper another class calls
/// or a member another interface asks for. It then stays out of the contract and out of the generated class, so no
/// other language has to implement it. A helper that only the set itself calls needs no attribute: make it
/// <see langword="private"/>.
/// </para>
/// <para>
/// On a set: use it for a set that cannot register itself, such as one that needs constructor arguments, which build warning
/// <c>LOC003</c> points out. Or use it for a set meant only for a registry of its own, such as one a test creates
/// with <see cref="TranslationsRegistry.Create(IEnumerable{ITranslationSet})">TranslationsRegistry.Create</see>.
/// </para>
/// <para>
/// The build still counts an excluded set as the set of its language, for the apps that use your library as well, so
/// that language is not reported as missing. Until you register the set, users of that language see the default
/// language. The set's language must still be one your assembly lists (<c>LOC005</c>). Never exclude the set of your
/// default language: the build then writes neither its contract nor the class of its texts, and reports nothing, as it
/// takes the set for one you register and read yourself.
/// </para>
/// <para>
/// The attribute is not inherited: a set deriving from an excluded set registers itself like any other.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // A set that takes the shop's name from configuration, so it needs a constructor argument.
/// [ExcludeFromTranslationDiscovery]
/// public sealed class BrandedShopTranslationsEN(string shopName) : ShopTranslationsEN
/// {
///     public override string Checkout => $"Pay at {shopName}";
/// }
///
/// // At startup:
/// TranslationsRegistry.Register(new BrandedShopTranslationsEN(configuration["ShopName"]!));
///
/// // A helper that other classes call too, and that is no text of the shop.
/// public partial class ShopTranslationsEN : ITranslationSet
/// {
///     public string Total(decimal amount) => $"Total: {Money(amount)}";
///
///     [ExcludeFromTranslationDiscovery]
///     public string Money(decimal amount) => amount.ToString("C");
/// }
/// </code>
/// </example>
// The source generator locates this type by its metadata-name string, so renaming or moving it silently disables the
// exclusion.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Method, Inherited = false)]
public sealed class ExcludeFromTranslationDiscoveryAttribute : Attribute;
