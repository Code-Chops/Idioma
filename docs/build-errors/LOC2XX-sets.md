# Build errors about sets

Each code has an example and the fix. The [list of every code](../build-errors.md#all-codes) shows which ones your IDE can fix.

## LOC200

The generator can't use the default-language set, so it writes no texts for it. A default-language set it can use:

- is named after its language, such as `ShopLocalizationEn`
- has no `LocalizationLanguageCode` that gives it another language, or one the build can't read because it isn't a constant
- is top-level, so not nested
- isn't file-local, generic, or abstract
- has a public parameterless constructor
- has no `required` members
- isn't marked `[Obsolete]` as an error
- is the only default-language set of its contract
- is a set of no other contract, such as a library's
- has a name that gives its generated class a name no other type or namespace has
- has a name that gives its contract a name no other type has

The generator still writes the contract when the only problem is one of these:

- the constructor
- a `required` member
- `[Obsolete]`
- the name of the generated class

Otherwise it writes no contract either. The contract your sets list then doesn't exist, so they don't compile (`CS0246`) until you fix this error. The message ends in what to change.

Two sets are the default-language set of one contract when their names differ only in the letter case of the language, as `ShopLocalizationEn` and `ShopLocalizationEN` do. The generator writes the contract from the first by name and reports the other. Remove one of them, or rename it.

The generated class is named after the set without its language, so `OrderStatusEn` next to an enum `OrderStatus` would give a second `OrderStatus`. Rename the set, as in `OrderStatusLocalizationEn`, or the other type. A static partial class of that name is fine, because it's a part of the generated class. A namespace of that name counts too, as the folder `Texts` gives `MyApp.Texts` next to `TextsEn`.

An enum in another namespace counts when the set names it by its name alone, as `ILocalizationSet<OrderStatus>` does behind `using Domain;`. Inside the set's namespace, the generated class would stand before the enum, so that name would mean the generated class.

The contract is named after the set too, so an interface `IShopLocalization` of your own next to `ShopLocalizationEn` would give a second `IShopLocalization`. Rename the set or your interface.

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

The IDE fix adds the language at the end of the attribute. A set whose name ends in a longer code, such as `ShopLocalizationEnGb`, adds that code (`en-GB`), which its name then gives it. Every other contract then lacks a set for that language, which is [LOC201](#loc201), and its fix adds the sets.

## LOC203

A set derives from a set of another language, so every text it doesn't override stays in that other language:

```csharp
public class ShopLocalizationNl : ShopLocalizationEn // Every text it doesn't override stays English
```

To translate, list `ILocalizationSet` and the contract instead (`: ILocalizationSet, IShopLocalization`), which requires every text. Derive from a set only to reword some of its texts in the same language.

A regional set may derive from the set of its language:

```csharp
public class ShopLocalizationEnGb : ShopLocalizationEn // Texts it doesn't override fall back to English
```

One region can't derive from another, because a region doesn't fall back to another region. `ShopLocalizationEnUs : ShopLocalizationEnGb` is LOC203.

The IDE fix lists `ILocalizationSet` and the contract in place of the set it derives from. A set of an enum gets `ILocalizationSet<TEnum>` instead. Its texts become `virtual` in place of `override`, so a set that derives from it still compiles. The compiler then names each text the set lacks (`CS0535`). A region that derives from another region also gets a fix that derives from the set of its language instead, as in `ShopLocalizationEnUs : ShopLocalizationEn`.

## LOC204

A set lists `ILocalizationSet` without its contract, so no text reads it. This also applies to the default-language set. Add the contract next to `ILocalizationSet`, as in `class ShopLocalizationNl : ILocalizationSet, IShopLocalization`. The IDE fix does this for you.

## LOC205

A set's `ILocalizationSet<TEnum>` doesn't match the enums of its contract. This is a warning, with these causes:

- A set of the contract of an enum lists `ILocalizationSet` instead of `ILocalizationSet<TEnum>`.
- A set lists `ILocalizationSet<TEnum>` for an enum its contract has no texts of.

```csharp
public class OrderStatusLocalizationNl : ILocalizationSet, IOrderStatusLocalization // LOC205
{
    public string Pending => "In behandeling";
    public string Shipped => "Verzonden";
}
```

```text
warning LOC205: 'OrderStatusLocalizationNl' doesn't list 'ILocalizationSet<OrderStatus>', which every set of 'IOrderStatusLocalization' lists. List it in place of 'ILocalizationSet'.
warning LOC205: 'ShopLocalizationNl' lists 'ILocalizationSet<PaymentStatus>', but 'IShopLocalization' has no texts of 'PaymentStatus'. Replace it with 'ILocalizationSet', or list it in the default-language set too.
```

List the enums the default-language set lists, in every language, and no other. Each set then shows which enum it words. The IDE fix does this for you: it lists a missing enum in place of `ILocalizationSet`, and it removes an enum of no texts. It compiles either way, because `ILocalizationSet<TEnum>` has no members.

- An enum listed in place of the right one is reported once, as the right one missing.
- To add an enum to the contract, list it in the default-language set. The contract takes its enums from that set.
- A regional set that derives from the set of its language lists it through that set.
- A set of a library's enum lists it too. An internal enum of a library is the exception, because no set outside the library can name it.
- A contract generated by Idioma 0.9.0 or earlier doesn't name its enum, so its sets aren't checked. Neither is a library's contract of no enum, because the build can't tell it from such a contract.

## LOC206

A set derives from another set and declares a `LocalizationLanguageCode`, but doesn't list `ILocalizationSet` itself. Its `LocalizationLanguageCode` then implements nothing, so the set doesn't serve the language it returns:

```csharp
public class ShopLocalizationBritish : ShopLocalizationEn // LOC206: LocalizationLanguageCode is never read
{
    public string LocalizationLanguageCode => "en-GB";
}
```

List the interface again: `class ShopLocalizationBritish : ShopLocalizationEn, ILocalizationSet`. The IDE fix does this for you.

Without the fix, the set serves the language of its name. A name that gives none, as `ShopLocalizationBritish` doesn't, leaves the set without a language, so it isn't registered. LOC206 is then the only code reported, because its fix gives the set its language.

## LOC207

A set's `LocalizationLanguageCode` isn't a culture name, such as `"en_GB"`. Registering the set would throw when the app starts. Return a culture name such as `en`, `en-GB` or `zh-Hans`.

This also applies to a set marked `[ExcludeFromLocalizationDiscovery]`, which would throw where you register it.

A code that has the form of a culture name but names no language or region, such as `"en-UK"`, is [LOC104](LOC1XX-languages.md#loc104).

## LOC208

A set can't be registered automatically, so its texts won't be found. This is a warning. The generated code that registers sets needs:

- a parameterless constructor
- no `required` members
- a language code: the end of its name, starting with a capital, such as `En`, or an implementation of `ILocalizationSet.LocalizationLanguageCode` (see [language codes](../language-codes.md) for a longer code such as `EnGb`)
- a top-level class that isn't file-local (checked only for a set that rewords another assembly's texts)
- no `[Obsolete]` marked as an error, because generated code can't name such a class

Fix the cause, or mark the set `[ExcludeFromLocalizationDiscovery]` and register an instance yourself:

- In an app, pass it to `LocalizationRegistry.Register`.
- In a library, pass it to `LocalizationRegistry.RegisterDefaults`, so an app's set still rewords its texts.

A set marked `[Obsolete]` as a warning registers as before, and so does one marked `[Experimental]`. The generated code turns off what naming it reports, so a build that treats warnings as errors doesn't stop there.

A member marked `[Localized]` whose text can't be registered gets [LOC502](LOC5XX-inline-texts-and-attributes.md#loc502).

## LOC209

A contract has two sets for one language, and neither derives from the other. Users see only one of them, so the wording of the other is lost:

```csharp
public class ShopLocalizationNl : ILocalizationSet, IShopLocalization { ... }
public class BasketNl : ILocalizationSet, IShopLocalization { ... } // LOC209
```

```text
error LOC209: 'BasketNl' is a second set of 'MyApp.IShopLocalization' for 'nl', next to 'ShopLocalizationNl'. Users of 'nl' see only one of them. Remove one of the two, or derive 'BasketNl' from 'ShopLocalizationNl' to reword some of its texts.
```

Keep one set per language. Two sets aren't reported in these cases:

- A set that derives from another set of its language rewords that set, and always wins over it.
- A set marked `[ExcludeFromLocalizationDiscovery]` is one you register yourself, and wins because you register it.
- A set of another assembly, such as a library's, is one an app may reword ([details](../libraries-and-apps.md#changing-a-text-of-a-library)).

A test double that lists `ILocalizationSet` is a set too, so it's registered next to the real one. Let it list only the contract.