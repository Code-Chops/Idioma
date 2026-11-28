# Build errors about sets

Each code has an example and the fix. The [list of every code](../build-errors.md#all-codes) shows which ones your IDE can fix.

## LOC200

The generator can't use the default-language set, so it writes no texts for it. A default-language set it can use:

- is named after its language, such as `ShopLocalizationEn`, and a regional default language in full, such as `ShopLocalizationEnUs` for `en-US`
- has a name that gives no other language, as `ShopLocalizationEnNl` gives `en-NL` where `nl` is the default language and `en-NL` is listed too
- is top-level, so not nested
- isn't file-local, generic, or abstract
- has a public parameterless constructor
- has no `required` members
- is the only default-language set of its contract
- is a set of no other contract, such as a library's
- has a name that gives its generated class a name no other type or namespace has
- has a name that gives its contract a name no other type has

The generator still writes the contract when the only problem is one of these:

- the constructor
- a `required` member
- the name of the generated class

Otherwise it writes no contract either. The contract your sets list then doesn't exist, so they don't compile (`CS0246`) until you fix this error. The message ends in what to change.

Two sets are the default-language set of one contract when their names differ only in the letter case of the language, as `ShopLocalizationEn` and `ShopLocalizationEN` do. The generator writes the contract from the first by name and reports the other. Remove one of them, or rename it.

The generated class is named after the set without its language, so `OrderStatusEn` next to an enum `OrderStatus` would give a second `OrderStatus`. Rename the set, as in `OrderStatusLocalizationEn`, or the other type. A static partial class of that name is fine, because it's a part of the generated class. A namespace of that name counts too, as the folder `Texts` gives `MyApp.Texts` next to `TextsEn`.

An enum in another namespace counts when the set names it by its name alone, as `ILocalizationSet<OrderStatus>` does behind `using Domain;`. Inside the set's namespace, the generated class would stand before the enum, so that name would mean the generated class.

The contract is named after the set too, so an interface `IShopLocalization` of your own next to `ShopLocalizationEn` would give a second `IShopLocalization`. Rename the set or your interface.

With a regional default language, the set is named after that region, not after its language. With `[assembly: SupportedLanguages("en-US", "nl")]`, `ShopLocalizationEn` serves `en-US`, but it isn't the default-language set, so it starts no contract:

```text
error LOC200: 'ShopLocalizationEn' is the default-language set of a contract, but its name gives 'en', and the default language is 'en-US'. The generator can't use it, so it writes no texts for it. Rename it to ShopLocalizationEnUs, or make 'en' the default language in [assembly: SupportedLanguages].
```

A set that implements another contract, such as `Shop.IShopLocalization` of a library, translates or rewords that contract, and starts none of its own:

```csharp
public class AppLocalizationEn : ILocalizationSet, IAppLocalization, Shop.IShopLocalization // LOC200
```

Write the texts of `IAppLocalization` in a set of their own, and implement `Shop.IShopLocalization` in another class.

Your sets can share an abstract class that lists `ILocalizationSet` and the contract, as in `abstract class ShopBase : ILocalizationSet, IShopLocalization` and `class ShopLocalizationEn : ShopBase`. The default-language set that derives from it gives the contract, from the texts it declares itself ([LOC303](LOC3XX-texts-of-a-set.md#loc303)).

The IDE fix renames the class after its language, or removes `abstract` or `file`. An abstract set with an abstract member gets no fix, because it wouldn't compile without `abstract`.

## LOC201

A contract has no set for one of your languages. The message names the class to add:

```text
error LOC201: 'ShopLocalization' has no set for 'de', which [assembly: SupportedLanguages] requires.
Add 'class ShopLocalizationDe : ILocalizationSet, IShopLocalization'. A class without ILocalizationSet doesn't count as a set, and neither does a struct, or a nested, generic, or file-local class.
```

The IDE fix adds that class in a file of its own, without texts. The compiler then names each text the set lacks (`CS0535`), and your IDE can add them for you to translate. "Fix all" adds the set of every missing language at once.

This also applies to a listed region, such as `en-GB` next to `en`, and to a contract you wrote by hand. A regional set derives from the set of its language and holds only the texts that differ.

A class that implements the contract without `ILocalizationSet` doesn't count as a set, whatever its name. To make it the set of its language, add `ILocalizationSet` next to the contract.

## LOC202

A set is for a language that `[assembly: SupportedLanguages]` doesn't list. Add the language to the attribute, or remove the set.

This also applies to a set you register yourself.

A set whose name ends in a region of a listed language, such as `ShopLocalizationEnGb` where `en` is listed, serves that region, so it isn't LOC202. A region that doesn't exist is a near miss. `ShopLocalizationEnUk` serves `uk`, and the message asks whether you meant `en-GB`.

The IDE fix adds the language at the end of the attribute. A set whose name ends in a longer code of an unlisted language, such as `ShopLocalizationFrBE`, adds that code (`fr-BE`), which its name then gives it. Neither the message nor the fix advises a code that is no language, such as `gb`, because that would only give [LOC103](LOC1XX-languages.md#loc103). Every other contract then lacks a set for that language, which is [LOC201](#loc201), and its fix adds the sets.

A name in PascalCase can mean either of two languages. `ShopLocalizationFrBe` may be Belgian French, or Belarusian (`be`) after a word. Where `fr` isn't listed, the message names both, and the IDE offers a fix for each, `fr-BE` first.

## LOC203

A contract has two sets for one language, and neither derives from the other. Users see only one of them, so the wording of the other is lost:

```csharp
public class ShopLocalizationNl : ILocalizationSet, IShopLocalization { ... }
public class BasketNl : ILocalizationSet, IShopLocalization { ... } // LOC203
```

```text
error LOC203: 'BasketNl' is a second set of 'MyApp.IShopLocalization' for 'nl', next to 'ShopLocalizationNl'. Users of 'nl' see only one of them. Remove one of the two, or derive 'BasketNl' from 'ShopLocalizationNl' to reword some of its texts.
```

Keep one set per language. Two sets aren't reported in these cases:

- A set that derives from another set of its language rewords that set, and always wins over it.
- A set marked `[ExcludeFromLocalizationDiscovery]` is one you register yourself, and wins because you register it.
- A set of another assembly, such as a library's, is one an app may reword ([details](../libraries-and-apps.md#changing-a-text-of-a-library)). Two rewordings of it are [LOC209](#loc209) instead.

A test double that lists `ILocalizationSet` is a set too, so it's registered next to the real one. Let it list only the contract.

## LOC204

A set derives from a set of another language, so every text it doesn't override stays in that other language:

```csharp
public class ShopLocalizationNl : ShopLocalizationEn // Every text it doesn't override stays English
```

To translate, list `ILocalizationSet` and the contract instead (`: ILocalizationSet, IShopLocalization`), which requires every text. Derive from a set only to reword some of its texts in the same language.

A regional set may derive from the set of its language:

```csharp
public class ShopLocalizationEnGb : ShopLocalizationEn // Texts it doesn't override fall back to English
```

One region can't derive from another, because a region doesn't fall back to another region. `ShopLocalizationEnUs : ShopLocalizationEnGb` is LOC204.

The IDE fix lists `ILocalizationSet` and the contract in place of the set it derives from. A set of an enum gets `ILocalizationSet<TEnum>` instead. Its texts become `virtual` in place of `override`, so a set that derives from it still compiles. The compiler then names each text the set lacks (`CS0535`). A region that derives from another region also gets a fix that derives from the set of its language instead, as in `ShopLocalizationEnUs : ShopLocalizationEn`.

## LOC205

A set lists `ILocalizationSet` without its contract, so no text reads it. This also applies to the default-language set. Add the contract next to `ILocalizationSet`, as in `class ShopLocalizationNl : ILocalizationSet, IShopLocalization`. The IDE fix does this for you.

A contract comes from a default-language set. When the build finds none for the set, as for `ShopLocalizationNl` without a `ShopLocalizationEn`, the message names the set to write. That also goes for a set that lists the contract already, as in `ShopLocalizationNl : ILocalizationSet, IShopLocalization`, where the contract doesn't exist without that set. Remove `ILocalizationSet` from a class that isn't a set.

## LOC206

A set's `ILocalizationSet<TEnum>` doesn't match the enums of its contract. This is a warning, with these causes:

- A set of the contract of an enum lists `ILocalizationSet` instead of `ILocalizationSet<TEnum>`.
- A set lists `ILocalizationSet<TEnum>` for an enum its contract has no texts of.

```csharp
public class OrderStatusLocalizationNl : ILocalizationSet, IOrderStatusLocalization // LOC206
{
    public string Pending => "In behandeling";
    public string Shipped => "Verzonden";
}
```

```text
warning LOC206: 'OrderStatusLocalizationNl' doesn't list 'ILocalizationSet<OrderStatus>', which every set of 'IOrderStatusLocalization' lists. List it in place of 'ILocalizationSet'.
warning LOC206: 'ShopLocalizationNl' lists 'ILocalizationSet<PaymentStatus>', but 'IShopLocalization' has no texts of 'PaymentStatus'. Replace it with 'ILocalizationSet', or list it in the default-language set too.
```

List the enums the default-language set lists, in every language, and no other. Each set then shows which enum it words. The IDE fix does this for you: it lists a missing enum in place of `ILocalizationSet`, and it removes an enum of no texts. It compiles either way, because `ILocalizationSet<TEnum>` has no members.

- An enum listed in place of the right one is reported once, as the right one missing.
- To add an enum to the contract, list it in the default-language set. The contract takes its enums from that set.
- A regional set that derives from the set of its language lists it through that set.
- A set of a library's enum lists it too. An internal enum of a library is the exception, because no set outside the library can name it.
- A contract you wrote by hand names no enum, so its sets aren't checked.

## LOC207

A set can't be registered automatically, so its texts won't be found. This is a warning. The generated code that registers sets needs:

- a parameterless constructor
- no `required` members
- a language code: the end of its name, starting with a capital, such as `En` (see [language codes](../language-codes.md) for a longer code such as `EnGb`)
- a top-level class that isn't file-local (checked only for a set that rewords another assembly's texts)

Fix the cause, or mark the set `[ExcludeFromLocalizationDiscovery]` and register an instance yourself:

- In an app, pass it to `LocalizationRegistry.Register`.
- In a library, pass it to `LocalizationRegistry.RegisterLibrarySets`, so an app's set still rewords its texts.

A set marked `[ExcludeFromLocalizationDiscovery]` still needs the language code in its name, because `LocalizationRegistry.Register` throws for a set without a language. Such a set gets this warning too, and the message says it can't be registered by hand. Rename it, or derive a class from it whose name ends in the code.

A set marked `[Obsolete]` registers as before. The generated code turns off the warning that naming it reports, so a build that treats warnings as errors doesn't stop there.

A member marked `[Localized]` whose text can't be registered gets [LOC502](LOC5XX-inline-texts-and-attributes.md#loc502).

## LOC208

A set rewords a library's set for a language, and the library also has a set of that contract for a region of the language. No set of your project rewords that regional set, or otherwise serves the region. This is a warning.

For each contract and language, one set wins and words every text. A British user sees the library's British set, so your rewording of English doesn't reach them:

```csharp
// The library Shop has ShopLocalizationEn and ShopLocalizationEnGb : ShopLocalizationEn.
public sealed class MyShopLocalizationEn : ShopLocalizationEn // LOC208
{
    public override string Checkout => "Pay now";
}
```

```text
warning LOC208: 'MyShopLocalizationEn' rewords 'ShopLocalizationEn', but users of 'en-GB' see 'ShopLocalizationEnGb', which it doesn't reach. Add 'class MyShopLocalizationEnGb : ShopLocalizationEnGb' to reword it too.
```

Reword the regional set too, with the same changes:

```csharp
public sealed class MyShopLocalizationEnGb : ShopLocalizationEnGb
{
    public override string Checkout => "Pay now";
}
```

A set of your own for the region settles it too, and so does one you register yourself. A set marked `[ExcludeFromLocalizationDiscovery]` isn't reported, because you register it yourself and it wins over every other set. See [changing a text of a library](../libraries-and-apps.md#regional-sets).

## LOC209

Two sets reword the same set for one language, and neither derives from the other. Only one of them shows, so the texts of the other are lost. This is a warning.

For each contract and language, one set wins and words every text. Your app's rewording wins over a library's, and else the one registered last wins:

```csharp
// The shared library Shared has SharedShopLocalizationEn : ShopLocalizationEn.
public sealed class AppShopLocalizationEn : ShopLocalizationEn // LOC209
{
    public override string Cart => "Bag";
}
```

```text
warning LOC209: 'AppShopLocalizationEn' and 'SharedShopLocalizationEn' both reword 'ShopLocalizationEn', so only 'AppShopLocalizationEn' shows. Derive 'AppShopLocalizationEn' from 'SharedShopLocalizationEn', so both show.
```

Derive one from the other. The one that derives then wins, with the texts of both:

```csharp
public sealed class AppShopLocalizationEn : SharedShopLocalizationEn
{
    public override string Cart => "Bag";
}
```

It's reported where the build sees both rewordings:

- At the rewording of your project, when the other is in a library it references.
- At the one that doesn't show, when both are in your project. Merge them into one set, or derive one from the other.
- Without a location, when both are in libraries your project references. Unless one library references the other, the build can't tell which one shows.

A set marked `[ExcludeFromLocalizationDiscovery]` isn't reported, because you register it yourself and it wins over every other set. See [changing a text of a library](../libraries-and-apps.md#several-rewordings-of-one-set).