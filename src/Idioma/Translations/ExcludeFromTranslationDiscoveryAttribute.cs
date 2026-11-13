namespace CodeChops.Idioma;

/// <summary>
/// Keeps an <see cref="ITranslations"/> set from registering itself, so that it applies only once you register it
/// with <see cref="TranslationsRegistry.Register(ReadOnlySpan{ITranslations})">TranslationsRegistry.Register</see>.
/// </summary>
/// <remarks>
/// <para>
/// Use it for a set that cannot register itself, such as one that needs constructor arguments, which build warning
/// <c>LOC003</c> points out. Or use it for a set meant only for a registry of its own, such as one a test creates
/// with <see cref="TranslationsRegistry.Create(IEnumerable{ITranslations})">TranslationsRegistry.Create</see>.
/// </para>
/// <para>
/// The build still counts an excluded set as the set of its language, for the apps that use your library as well, so
/// that language is not reported as missing. Until you register the set, users of that language see the default
/// language. The set's language must still be one your assembly lists (<c>LOC005</c>). Never exclude the set of your
/// default language: the build then writes neither its contract nor the class of its texts.
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
/// </code>
/// </example>
// The source generator locates this type by its metadata-name string, so renaming or moving it silently disables the
// exclusion.
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ExcludeFromTranslationDiscoveryAttribute : Attribute;
