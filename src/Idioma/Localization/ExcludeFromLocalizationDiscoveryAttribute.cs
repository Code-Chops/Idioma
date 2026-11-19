namespace CodeChops.Idioma;

/// <summary>
/// Keeps the build from picking up a set or a member. A marked <see cref="ILocalizationSet">set</see> doesn't register
/// itself, so it takes effect only once you pass it to
/// <see cref="LocalizationRegistry.Register(ReadOnlySpan{ILocalizationSet})">LocalizationRegistry.Register</see>. A
/// marked public property or method of the set of your default language isn't a text. The initializer of a marked
/// property that holds a <see cref="LocalizableText"/> may hold wording that reads the same in every language,
/// such as a name.
/// </summary>
/// <remarks>
/// <para>
/// <b>On a set.</b> Mark a set that can't be registered automatically, such as one that needs constructor arguments.
/// Build warning <c>LOC400</c> points these out. Also mark a set meant only for a registry of its own, such as one a
/// test creates with
/// <see cref="LocalizationRegistry.Create(IEnumerable{ILocalizationSet})">LocalizationRegistry.Create</see>.
/// </para>
/// <para>
/// <b>On a member.</b> Every public string property of the set of your default language is a text. So is every public
/// method that returns a string and takes parameters. Mark a member that isn't a text but has to stay public, such as
/// an override of a public member of a base class. It then stays out of the contract and the generated class, so no
/// other language has to implement it. Other members that aren't texts need no attribute:
/// </para>
/// <list type="bullet">
/// <item>Make a helper <see langword="private"/> or <see langword="internal"/>.</item>
/// <item>Make a helper that other assemblies call <see langword="static"/>. A static member isn't a text.</item>
/// <item>Implement a member of another interface explicitly. An explicit implementation isn't a text.</item>
/// </list>
/// <para>
/// The build still counts an excluded set as the set of its language, also for the apps that use your library, so it
/// doesn't report that language as missing. Until you register the set, users of that language see the default
/// language. The set's language must still be one your assembly lists (<c>LOC202</c>).
/// </para>
/// <para>
/// Never exclude the set of your default language. The build then writes neither its contract nor the generated
/// class, so the first error you see is that the contract, such as <c>IShopLocalization</c>, doesn't exist. Idioma
/// reports nothing about the set, because it takes the set for one you register and read yourself.
/// </para>
/// <para>
/// The attribute isn't inherited. A set that derives from an excluded set is registered automatically, like any other.
/// </para>
/// <para>
/// <b>On a property that holds a text.</b> Mark a property whose wording reads the same in every language, such as a
/// name. Its initializer may then hold that wording as a string:
/// <c>[ExcludeFromLocalizationDiscovery] public static LocalizableText Brand { get; } = "Idioma";</c>. Without the
/// attribute, that's build error <c>LOC500</c>. The build still checks what other code assigns to the property, so a
/// marked component parameter still holds its callers to every language.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // A set that takes the shop's name from configuration, so it needs a constructor argument.
/// [ExcludeFromLocalizationDiscovery]
/// public sealed class BrandedShopLocalizationEn(string shopName) : ShopLocalizationEn
/// {
///     public override string Checkout => $"Pay at {shopName}";
/// }
///
/// // At startup:
/// LocalizationRegistry.Register(new BrandedShopLocalizationEn(configuration["ShopName"]!));
///
/// // Money overrides a public member of PriceFormatter, so it has to stay public. It isn't a text.
/// public class ShopLocalizationEn : PriceFormatter, ILocalizationSet, IShopLocalization
/// {
///     public virtual string Checkout => "Checkout";
///
///     public string Total(decimal amount) => $"Total: {Money(amount)}";
///
///     [ExcludeFromLocalizationDiscovery]
///     public override string Money(decimal amount) => amount.ToString("C");
/// }
/// </code>
/// </example>
// The source generator and the check for LOC500 locate this type by its metadata-name string, so renaming it or
// changing its namespace silently disables the exclusion.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Method, Inherited = false)]
public sealed class ExcludeFromLocalizationDiscoveryAttribute : Attribute;
