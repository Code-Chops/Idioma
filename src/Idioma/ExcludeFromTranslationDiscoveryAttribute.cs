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
/// The build checks only that an excluded set's language is one your assembly lists (<c>LOC005</c>), as a user whose
/// culture the app does not declare sees the default language, whatever is registered. It does count the set as a set
/// of its language, also for the apps that use your library: a language served only by an excluded set is not reported
/// as missing. Registering it is up to you, and until you do, users of that language see the default language. Never
/// exclude the set of your default language: the build then writes neither its contract nor the class of its texts.
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
