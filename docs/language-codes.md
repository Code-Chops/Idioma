# Language codes

A language code, such as `en` or `en-GB`, is written in three ways: as a culture name in `[assembly: SupportedLanguages]`, in PascalCase at the end of a set's name, and in camelCase as a parameter name of an inline text or an attribute. This page covers the names, and the two ways to add a regional variant.

Other subjects have a page of their own, listed in the [README](../README.md#documentation).

## Codes in names

A set's language code is the end of its name, written in PascalCase. A two-letter code is the last two letters: `ShopLocalizationEn` serves `en` and every culture under it.

A longer code, such as `en-GB`, `zh-Hans`, or `fil`, comes from the name only when `[assembly: SupportedLanguages]` lists it. It's written in PascalCase too: `ShopLocalizationEnGb`, `ShopLocalizationZhHans`, `ShopLocalizationFil`. A set deriving from such a set serves the same language.

Only the capital that starts the code counts. After it, the code matches in any letter case, so `ShopLocalizationEN` and `ShopLocalizationEnGB` work too. Without that capital, a name has no code. So `LocalizationSet` doesn't serve Estonian (`et`), and gets build warning `LOC208`. A set without a code serves no language, so the build still asks for a set of each language its contract lacks (`LOC201`).

An inline text and an attribute take the same codes as parameter names, in camelCase: `nl:` where a set's name ends in `Nl`, and `enGb:`, `zhHans:`, and `fil:` for the sets above.

When a longer code isn't listed, only the last two letters count. A name alone can't tell `ShopLocalizationEnGb` (British English) from `AdminUiNl` (Dutch). So an unlisted `ShopLocalizationEnGb` serves `gb`, which isn't a language (build error `LOC202`).

## Regional variants

You don't need an `en-US` set. `ShopLocalizationEn` serves `en-US`, `en-GB`, and every other English culture, so a British set holds only the texts that differ. It derives from the English set and overrides those texts. So make them `virtual` in the English set, as in `public virtual string Cart => "Cart";`. There are two ways to add a British set:

- List `en-GB` in `[assembly: SupportedLanguages("en", "nl", "en-GB")]`, and the name is enough:

  ```csharp
  public class ShopLocalizationEnGb : ShopLocalizationEn
  {
      public override string Cart => "Basket";
  }
  ```

  British users then see "Basket" and the rest of your English. The build treats British English as a language of its own, so no British wording is forgotten:

  - Every contract needs a British set (`LOC201`). One with nothing British about it is a single line: `public class GridLocalizationEnGb : GridLocalizationEn;`.
  - Every inline text and every attribute takes a British wording, as in `LocalizableText.Inline(en: "Color", nl: "Kleur", enGb: "Colour")`.

- Leave it out, `("en", "nl")`, and implement `LocalizationLanguageCode` in the set. Your other sets, your inline texts and your attributes then show their English in Britain.

  ```csharp
  public class ShopLocalizationEnGb : ShopLocalizationEn, ILocalizationSet
  {
      string ILocalizationSet.LocalizationLanguageCode => "en-GB";

      public override string Cart => "Basket";
  }
  ```

A set that derives from another set lists `ILocalizationSet` again to implement `LocalizationLanguageCode`, as in the second example. Without it, its `LocalizationLanguageCode` implements nothing (`LOC206`). The code it returns must be a culture name. A constant that isn't one, such as `"en_GB"`, is build error `LOC207`.

Separate `en-US` and `en-GB` sets without an `en` set work too, with two drawbacks:

- Each set has every text, because one region can't derive from another (`LOC203`). Texts it doesn't override would stay American.
- A user whose culture is plain `en`, or `en-AU`, gets your default language.

A regional default language works the same way. With `[assembly: SupportedLanguages("en-GB", "nl")]`, `public class ShopLocalizationEnGb : ILocalizationSet, IShopLocalization` gives the contract `IShopLocalization`.