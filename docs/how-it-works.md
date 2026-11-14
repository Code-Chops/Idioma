# How Idioma works

What happens behind the [getting started guide](../README.md#getting-started): which wording a user sees, what the source generator writes, how sets register, and how a text travels through JSON.

Other subjects have a page of their own: [language codes](language-codes.md), [libraries and apps](libraries-and-apps.md), [Blazor](blazor.md), and [what the build rejects](build-errors.md).

## Which wording a user sees

A text follows `CultureInfo.CurrentUICulture`, and takes the first wording it finds:

1. The wording for that culture, or else for its parent culture: an `en` wording serves `en-US` and `en-GB` alike, and an `en-GB` wording wins over it in Britain.
2. The wording for the app's default language: the first one it declares. The texts of the libraries the app uses fall back to it as well, so no screen ends up in two languages. A culture none of the app's languages serves starts here, even where a library has its language: an app declaring `("en", "fr")` shows a Dutch user a library's texts in English, like its own.
3. The wording for the default language of the project that wrote the text.

`ToString` follows the same order for the culture you pass. `Format` formats its values for `CultureInfo.CurrentCulture`, or for the culture you pass, so a placeholder can carry a format, as in `{0:N2}` or `{0:d}`. It always reads the wording as a format string, even without values, so write a literal brace as `{{` or `}}`.

## What the source generator writes

`LocalizableText.Inline` gets one required parameter per declared language, named after it: `enGB` for `en-GB`.

From the set of the default language, `ShopTranslationsEN`, it writes the contract `IShopTranslations` and the class `ShopTranslations`. Each generated text shows its wording in every language and links to it, so hovering over `ShopTranslations.Checkout` shows `nl: Afrekenen`, and you can jump from there to `ShopTranslationsNL.Checkout`. The generated class is partial, so a helper can sit next to the texts. It holds nothing but the texts, so a text can take any name except that of the class itself.

Each generated text is a `ResourceText` with a key, `Shop.Checkout` here. The package never reads the key: it is for an app or library that also looks texts up in resource files, such as through `IStringLocalizer`, so a resource file can override a wording without a rebuild.

## Registration

Every set registers itself: an app registers the sets of every assembly it references when it starts, whether it uses that assembly or not. An assembly the app loads later, such as one a Blazor WebAssembly app loads lazily, registers its sets when its code first runs; loading it alone does not.

A set that cannot register itself gets build warning `LOC003`:

- It has no parameterless constructor, a `required` member, which generated code cannot set, or no language code.
- It rewords another assembly's texts from a nested or file-local class, where generated code cannot reach it.

A nested, generic, or file-local class with your own texts is no set to the build, so its language is reported as missing (`LOC004`): make it a top-level class that is not generic.

To register a set by hand, mark it `[ExcludeFromTranslationDiscovery]` and call `TranslationsRegistry.Register`. The build still counts it as a set of its language, and still requires that language to be listed (`LOC005`).

`TranslationsRegistry` does two more things:

- It reads a set directly: `TranslationsRegistry.Current.Find<IShopTranslations>(culture)`.
- It gives part of an app, or a test, sets of its own: `TranslationsRegistry.Create(sets)`. Such a registry stands apart from your app's languages as well: it shows every language it has a set for, unless you pass it languages of its own, `TranslationsRegistry.Create(sets, "en", "nl")`.

## Texts in JSON

A text survives JSON, such as a Blazor component parameter that crosses a render mode. It is written with every wording it can show: its own translations, and for a text of a set, the wording of every language your app declares that has a registered set, regions included. Read back, it shows that wording for every culture without needing the sets, and a resource text keeps its key. It belongs to no contract any more, so a set registered later does not reword it.

A JSON string reads as a text too, with that one wording for every culture, so JSON you write by hand, such as a settings file, can hold `"title": "Orders"`.