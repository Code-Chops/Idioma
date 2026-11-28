# Language codes

A language code, such as `en` or `en-GB`, is written in three ways: as a culture name in `[assembly: SupportedLanguages]`, in PascalCase at the end of a set's name, and in camelCase as a parameter name of an inline text or an attribute. This page covers the names, and the two ways to add a regional variant.

Other subjects have a page of their own, listed in the [README](../README.md#documentation).

## Codes in names

A set's language code is the end of its name, written in PascalCase. The build reads it and stores it. So a class renamed after the build keeps its language, and no code of the set runs to learn it. A two-letter code is the last two letters: `ShopLocalizationEn` serves `en` and every culture under it.

A longer code, such as `en-GB`, `zh-Hans`, or `fil`, comes from the name when `[assembly: SupportedLanguages]` lists it. It's written in PascalCase too: `ShopLocalizationEnGb`, `ShopLocalizationZhHans`, `ShopLocalizationFil`. A set deriving from such a set serves the same language.

Only the capital that starts the code counts. After it, the code matches in any letter case, so `ShopLocalizationEN` and `ShopLocalizationEnGB` work too. Without that capital, a name has no code. So `LocalizationSet` doesn't serve Estonian (`et`), and gets build warning `LOC207`. A set without a code serves no language, so the build still asks for a set of each language its contract lacks (`LOC201`).

An inline text and an attribute take the same codes as parameter names, in camelCase: `nl:` where a set's name ends in `Nl`, and `enGb:`, `zhHans:`, and `fil:` for the sets above.

A name can also end in a listed language followed by a region. `ShopLocalizationEnGb` serves `en-GB` when `en` is listed and `en-GB` isn't. `ShopLocalizationZhHansTw` serves `zh-Hans-TW` when `zh-Hans` is listed. The region is two letters that start with a capital, and it must exist.

A name alone can't tell `ShopLocalizationEnGb` (British English) from `AdminUiNl` (Dutch), so the build checks what comes before the region. `ui` isn't a listed language, so `AdminUiNl` stays Dutch. `UK` isn't a region, so `ShopLocalizationEnUk` serves `uk`, which is Ukrainian. Unless you list `uk`, that's build error `LOC202`, which suggests `ShopLocalizationEnGb`. Otherwise only the last two letters count.

A listed language at the end of a name wins over a region. With `no` and `de` both listed, `YesNoDe` stays German and doesn't become `no-DE`. So where `fr` and `ca` are both listed, `ShopLocalizationFrCa` is Catalan. List `fr-CA` to make it Canadian French.

Write each code as .NET names the culture. Danish is `da` and British English is `en-GB`, so `dk` and `en-UK` match no user's culture. The build warns about a language or a region that doesn't exist (`LOC103`).

A class you can't rename gets a language by deriving from it with a name that gives one: `public class ShopLocalizationDe : ThirdPartyGermanTexts;`.

A set nested in a public or internal class gets its language from its name too, so you can register it by hand. A private nested set has no language, because the build can't name it. Registering it throws.

## Regional variants

You don't need an `en-US` set. `ShopLocalizationEn` serves `en-US`, `en-GB`, and every other English culture, so a British set holds only the texts that differ. It derives from the English set and overrides those texts. So make them `virtual` in the English set, as in `public virtual string Cart => "Cart";`. A British set can also override a member that the English texts read, such as a `protected virtual` currency symbol. Every English text it inherits then shows with it. There are two ways to add a British set:

- List `en-GB` in `[assembly: SupportedLanguages("en", "nl", "en-GB")]`, and the name is enough:

  ```csharp
  public class ShopLocalizationEnGb : ShopLocalizationEn
  {
      public override string Cart => "Basket";
  }
  ```

  British users then see "Basket" and the rest of your English. The build treats British English as a language of its own, so no British wording is forgotten:

  - Every contract needs a British set (`LOC201`). One with nothing British about it is a single line: `public class GridLocalizationEnGb : GridLocalizationEn;`.
  - Every inline text and every attribute takes a British wording, as in `LocalizedText.Inline(en: "Color", nl: "Kleur", enGb: "Colour")`.

- Leave it out of `[assembly: SupportedLanguages("en", "nl")]` and name the set `ShopLocalizationEnGb`. Your other sets, inline texts and attributes then show their English in Britain.

  ```csharp
  public class ShopLocalizationEnGb : ShopLocalizationEn
  {
      public override string Cart => "Basket";
  }
  ```

A British user sees the British set alone, with what it inherits from the English set. So a rewording of the English set, such as an app's rewording of a library's `ShopLocalizationEn`, doesn't reach British users. Reword the British set too ([LOC208](build-errors/LOC2XX-sets.md#loc208), [details](libraries-and-apps.md#regional-sets)).

A set that derives from a British set serves `en-GB` too, such as `MyShopLocalizationEnGb` or `GiftLocalizationUk`. Its own name decides where it gives a longer code, or ends in another language you list. So `ShopLocalizationDe : ShopLocalizationEnGb` serves `de` where `de` is listed, and gets `LOC204` for deriving from British English.

Separate `en-US` and `en-GB` sets without an `en` set work too, with two drawbacks:

- Each set has every text, because one region can't derive from another (`LOC204`). Texts it doesn't override would stay American.
- A user whose culture is plain `en`, or `en-AU`, gets your default language.

A regional default language works the same way. With `[assembly: SupportedLanguages("en-GB", "nl")]`, `public class ShopLocalizationEnGb : ILocalizationSet, IShopLocalization` gives the contract `IShopLocalization`. A set named `ShopLocalizationEn` gives none, because its language isn't the default language itself ([LOC200](build-errors/LOC2XX-sets.md#loc200)).