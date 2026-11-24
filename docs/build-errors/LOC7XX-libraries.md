# Build errors about libraries

Each code has an example and the fix. The [list of every code](../build-errors.md#all-codes) shows which ones your IDE can fix.

## LOC700

A library you reference has no set for one of your languages. The message names the set to add to your app:

```text
error LOC700: No set serves 'Shop.IShopLocalization' in 'de', which this assembly supports. Add 'class ShopLocalizationDe : ILocalizationSet, Shop.IShopLocalization'.
```

The IDE fix adds that class in a file of its own, without texts, as the fix of [LOC201](LOC2XX-sets.md#loc201) does. The file goes next to the one that lists your languages, and the class gets that file's namespace. It names the contract after a `using` of its namespace, as in `using Shop;` and `IShopLocalization`, and writes its full name only where the short one would mean another type. "Fix all" adds a set for every library and language at once.

See [a language the library lacks](../libraries-and-apps.md#a-language-the-library-lacks).

## LOC701

A library you reference has inline texts that lack one of your languages. This is a warning. Nothing outside the library can add a language to them, so your users see those texts in your app's default language. Where the library lacks that language too, they see the library's default language. The message names the language they see:

```text
warning LOC701: 'Shop' has inline texts without 'de', which this assembly supports. No set added here can translate them, so users of 'de' will see them in 'en'.
```

The texts a library writes as attributes, with `[Localized]`, count among them, because they carry their wording themselves too.

To accept it, add `LOC701` to `<NoWarn>` in the project file, or put `#pragma warning disable LOC701` around `[assembly: SupportedLanguages]`, where it's reported.
