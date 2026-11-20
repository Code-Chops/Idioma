# Language codes

A set's language code is the end of its name, written in PascalCase. A two-letter code is the last two letters: `ShopLocalizationEn` serves `en` and every culture under it.

A longer code, such as `en-GB`, `zh-Hans`, or `fil`, comes from the name only when `[assembly: SupportedLanguages]` lists it. It's written in PascalCase too: `ShopLocalizationEnGb`, `ShopLocalizationZhHans`, `ShopLocalizationFil`. A set deriving from such a set serves the same language.

Only the capital that starts the code counts. After it, the code matches in any letter case, so `ShopLocalizationEN` and `ShopLocalizationEnGB` work too. Without that capital, a name has no code. So `LocalizationSet` doesn't serve Estonian (`et`), and gets build warning `LOC400`.

An inline text and an attribute take the same codes as parameter names, in camelCase: `nl:` where a set's name ends in `Nl`, and `enGb:`, `zhHans:`, and `fil:` for the sets above.

When a longer code isn't listed, only the last two letters count. A name alone can't tell `ShopLocalizationEnGb` (British English) from `AdminUiNl` (Dutch). So an unlisted `ShopLocalizationEnGb` serves `gb`, which isn't a language (build error `LOC202`).

Other subjects have a page of their own: [libraries that use Idioma](libraries-and-apps.md), [Blazor](blazor.md), [what the build rejects](build-errors.md), [coming from resx](coming-from-resx.md), [performance](performance.md), and [how Idioma works](how-it-works.md).

## Regional variants

You don't need an `en-US` set. `ShopLocalizationEn` serves `en-US`, `en-GB`, and every other English culture, so a British set holds only the texts that differ. There are two ways to add one:

- List `en-GB` in `[assembly: SupportedLanguages("en", "nl", "en-GB")]`, and the name is enough. The build then treats British English as a language of its own. Every contract needs a British set (`LOC201`), which can derive from the English one and add nothing, and every inline text and attribute takes a British wording (`enGb:`).

  ```csharp
  public class ShopLocalizationEnGb : ShopLocalizationEn
  {
      public override string Cart => "Basket";
  }
  ```

- Leave it out, `("en", "nl")`, and implement `LocalizationLanguageCode` in the set. Your other sets, your inline texts and your attributes then show their English in Britain.

  ```csharp
  public class ShopLocalizationEnGb : ShopLocalizationEn, ILocalizationSet
  {
      string ILocalizationSet.LocalizationLanguageCode => "en-GB";

      public override string Cart => "Basket";
  }
  ```

A set that derives from another set lists `ILocalizationSet` again to implement `LocalizationLanguageCode`, as in the second example. Without it, its `LocalizationLanguageCode` implements nothing (`LOC401`). The code it returns must be a culture name. A constant that isn't one, such as `"en_GB"`, is build error `LOC402`.

Separate `en-US` and `en-GB` sets without an `en` set work too, with two drawbacks:

- Each set has every text, because one region can't derive from another (`LOC203`). Texts it doesn't override would stay American.
- A user whose culture is plain `en`, or `en-AU`, gets your default language.

A regional default language works the same way. With `[assembly: SupportedLanguages("en-GB", "nl")]`, `public class ShopLocalizationEnGb : ILocalizationSet, IShopLocalization` gives the contract `IShopLocalization`.