# Language codes

A set's language is the end of its name. A two-letter code is the last two letters: `ShopTranslationsEN` serves `en`, and every culture under it.

A longer code, such as `en-GB`, `zh-Hans`, or `fil`, comes from the name as well, written in PascalCase (`ShopTranslationsEnGB`, `ShopTranslationsZhHans`, `ShopTranslationsFil`), but only when `[assembly: SupportedLanguages]` lists it. A set deriving from such a set serves the same language.

An inline text takes the same codes as the names of its arguments, in camelCase: `nl:` where a set's name ends in `NL`, and `enGB:`, `zhHans:`, and `fil:` for the sets above.

A name alone cannot tell `ShopTranslationsEnGB` (British English) from `AdminUiNL` (Dutch). So when the code is not listed, only the last two letters count: `ShopTranslationsEnGB` serves `gb`, which is not a language (build error `LOC005`).

Other subjects have a page of their own: [how Idioma works](how-it-works.md), [libraries that use Idioma](libraries-and-apps.md), [Blazor](blazor.md), [what the build rejects](build-errors.md), and [coming from resx](coming-from-resx.md).

## Regional variants

For English in the United States and Britain, you need no `en-US` set: `ShopTranslationsEN` serves `en-US`, `en-GB`, and every other English culture, so a British set holds only the texts that differ. There are two ways to add one:

- List `en-GB` in `[assembly: SupportedLanguages("en", "nl", "en-GB")]`. The name is enough. British English is then a language of its own to the build: every contract needs a British set (`LOC004`), which can derive from the English one and add nothing, and every inline text takes a British wording (`enGB:`).

  ```csharp
  public class ShopTranslationsEnGB : ShopTranslationsEN
  {
      public override string Cart => "Basket";
  }
  ```

- Leave it out, `("en", "nl")`, so that your other sets and your inline texts show their English in Britain, and implement `LanguageCode` in the set:

  ```csharp
  public class ShopTranslationsEnGB : ShopTranslationsEN, ITranslationSet
  {
      string ITranslationSet.LanguageCode => "en-GB";

      public override string Cart => "Basket";
  }
  ```

A set deriving from another set lists `ITranslationSet` again to implement `LanguageCode`, as in the second example; without it, its `LanguageCode` implements nothing (`LOC015`). The code it returns is a culture name: a constant that is not one, such as `"en_GB"`, is build error `LOC018`.

Separate `en-US` and `en-GB` sets without an `en` set work as well, with two drawbacks. Each set then has every text, as one region cannot derive from another (`LOC006`: texts it does not override would stay American). And a user whose culture is plain `en`, or `en-AU`, gets your default language.

A regional default language works the same way: with `[assembly: SupportedLanguages("en-GB", "nl")]`, `public partial class ShopTranslationsEnGB : ITranslationSet` gives the contract `IShopTranslations`.