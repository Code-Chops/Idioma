# Build errors about libraries

Each code has an example and the fix. The [list of every code](../build-errors.md#all-codes) shows which ones your IDE can fix.

## LOC700

A library you reference has no set for one of your languages. The message names the set to add to your app:

```text
error LOC700: No set serves 'Shop.IShopLocalization' in 'de', which this assembly supports. Add 'class ShopLocalizationDe : ILocalizationSet, Shop.IShopLocalization'.
```

The IDE fix adds that class in a file of its own, without texts, as the fix of [LOC201](LOC2XX-sets.md#loc201) does. The file goes next to the one that lists your languages, and the class gets that file's namespace. It names the contract after a `using` of its namespace, as in `using Shop;` and `IShopLocalization`, and writes its full name only where the short one would mean another type. The enum of a set of an enum, in `ILocalizationSet<OrderStatus>`, is named the same way. "Fix all" adds a set for every library and language at once.

See [a language the library lacks](../libraries-and-apps.md#a-language-the-library-lacks).

## LOC701

A library you reference has inline texts that lack one of your languages. This is a warning. Nothing outside the library can add a language to them, so your users see those texts in your app's default language. Where the library lacks that language too, they see the library's default language. The message names the language they see:

```text
warning LOC701: 'Shop' has inline texts without 'de', which this assembly supports. No set added here can translate them, so users of 'de' will see them in 'en'.
```

The texts a library writes as attributes, with `[Localized]`, count among them, because they carry their wording themselves too. That holds even where your app gives an enum of the library its languages with a set of the enum ([Enums](../enums.md#a-set-of-the-enum)). The library's build marks only that it has such texts, not which, so the warning can't tell they're covered.

To accept it, add `LOC701` to `<NoWarn>` in the project file, or put `#pragma warning disable LOC701` around `[assembly: SupportedLanguages]`, where it's reported.

## LOC702

A library you reference was built with a version of Idioma that your version may not run. Your app runs every library with the one version of the package it references. A new major version may change what a library built with an earlier one calls: the code the generator wrote into it, and Idioma's own API. So such a library could fail when your app starts, before `Main`:

```text
error LOC702: 'Shop' was built with CodeChops.Idioma 0.14.0, and this project uses 0.15.0. Code built with 0.14.0 may not run with 0.15.0, so update 'Shop' to a version built with 0.15.0, or rebuild it.
```

Below 1.0, a new minor version may break a library too, as semantic versioning lets anything change there. So these are reported:

- another major version, such as 1.x next to 2.x
- below 1.0, another minor version, such as 0.14 next to 0.15

A patch, such as 0.15.0 next to 0.15.2, isn't reported, and neither is a minor version from 1.0 on, such as 1.2 next to 1.9.

Update the library to a version built with your version of Idioma, or one it runs. If the library is yours, build it again with your version.
