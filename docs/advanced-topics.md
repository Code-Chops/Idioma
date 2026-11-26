# Advanced topics

The [getting started guide](../README.md#getting-started) covers what most apps need. This page explains what happens behind the texts you write, and covers the tasks the guide leaves out:

- [Fallback order](#fallback-order): which wording a user sees, and [where the user's culture comes from](#the-current-culture)
- [Texts with parameters](#texts-with-parameters): what a call does, and keeping a public method of a set from becoming a text
- [Getting a text by its key](#getting-a-text-by-its-key): for a key your code only has at run time, such as one from a database
- [Registration](#registration): how sets register, and registering a set by hand
- [Reading sets yourself](#reading-sets-yourself): the wording of one set, whatever the user's culture
- [Native AOT](#native-aot): the languages of an app published without globalization data

Other subjects have a page of their own, listed in the [README](../README.md#documentation).

## Fallback order

A text is a `string`. Each time you read it, it gives the wording for `CultureInfo.CurrentUICulture`, the first it finds:

1. The wording for that culture, or else for its parent culture. An `en` wording serves `en-US` and `en-GB` alike, and an `en-GB` wording wins over it in Britain. A culture whose name holds its script, as `zh-Hant-TW` does on Apple platforms, tries the name without it right after itself, so a `zh-TW` wording serves it.
2. The wording for the app's default language, which is the first language it declares. Library texts fall back to it too, so no screen ends up in two languages. A culture that none of the app's languages serves starts here, even when a library has its language. For example, an app declaring `("en", "fr")` shows a Dutch user a library's texts in English, like its own.
3. The wording for the default language of the project that wrote the text.

In an app declaring `("en", "nl", "en-GB")`, whose British set calls the cart "Basket":

| The user's culture | `ShopLocalization.Cart` shows | Because                                            |
|--------------------|-------------------------------|----------------------------------------------------|
| `en-GB`            | Basket                        | It has its own wording                             |
| `en-AU`            | Cart                          | Its parent culture, `en`, has wording              |
| `nl-NL`            | Winkelwagen                   | Its parent culture, `nl`, has wording              |
| `de-DE`            | Cart                          | No app language serves it, so the default language |

### The current culture

A text takes the user's culture from `CultureInfo.CurrentUICulture`. To take it from somewhere else, such as a session or a user profile, set a function once at startup:

```csharp
LocalizationRegistry.CurrentUICultureProvider = () => UserProfile.Current?.Culture;
```

What to know:

- It applies to every text of the app, including the texts of the libraries it uses.
- It runs each time a text is read, so keep it fast.
- When it returns `null`, the text follows `CultureInfo.CurrentUICulture` for that call.
- A [text with parameters](#texts-with-parameters) formats their values for the same culture.

### Another language

A text gives the wording of the moment you read it. To get the wording in another language, such as that of an email's recipient, wrap the block that reads the texts in `LocalizationRegistry.UseCulture`:

```csharp
using (LocalizationRegistry.UseCulture(CultureInfo.GetCultureInfo("nl-NL")))
{
    var order = await orders.FindAsync(id);

    // "Totaal: 1.234,50", whatever the culture of the current user
    string line = $"{ShopLocalization.Total}: {order.Amount:N2}";
}
```

What to know:

- The culture chooses the wording and formats dates and numbers, so the line is Dutch throughout.
- It wins over `CurrentUICultureProvider`, which isn't asked inside the scope.
- The scope follows your code across an `await`. A task started inside it stays in it until the task ends, also when the scope ends first.
- A scope inside another wins until it's disposed.
- Entering a scope copies the execution context. So wrap a block of texts, not each text.

#### With an ambient scope of your own

`UseCulture` needs no other package. An app that already keeps its ambient values in scopes of another library, such as [Architect.AmbientContexts](https://github.com/TheArchitectDev/Architect.AmbientContexts), can keep the culture of its texts there too. Write the scope with that library, and give it to the provider once at startup:

```csharp
public sealed class TextCultureScope : AmbientScope<TextCultureScope>
{
    public static TextCultureScope? Current => GetAmbientScope();

    public CultureInfo Culture { get; }

    public TextCultureScope(CultureInfo culture)
        : base(AmbientScopeOption.ForceCreateNew)
    {
        this.Culture = culture;
        this.Activate();
    }

    protected override void DisposeImplementation()
    {
    }
}

// At startup
LocalizationRegistry.CurrentUICultureProvider = () => TextCultureScope.Current?.Culture;
```

Inside `using (new TextCultureScope(dutch))`, every text is Dutch, and the values of a text of a set are formatted for Dutch. Outside one, the provider returns `null`, so a text follows `CultureInfo.CurrentUICulture`. Such a scope doesn't change the thread's cultures. So a date you format yourself inside it keeps the format of the thread, and so does a value you write into an inline text.

For the wording of one set, whatever the user's culture, [read the set](#reading-sets-yourself).

## Texts with parameters

A public method of the default-language set that returns a `string` and takes parameters is a text with parameters ([an example](../README.md#texts-with-parameters)). The generated class gets a method with the same name and parameters. The contract repeats the method too, so every language implements it:

```csharp
public static partial class ShopLocalization
{
    public static string ItemsInCart(int count, string shop) => ...;
}

public interface IShopLocalization
{
    string ItemsInCart(int count, string shop);
}
```

Each call of `ShopLocalization.ItemsInCart` hands the values to the method of the set for the user's language, and returns its wording. Nothing is kept. A call allocates only the string the set's method makes.

Write a text without values as a property, because a method without parameters isn't a text. The build rejects such a method in the default-language set (`LOC400`), unless it's an override such as `ToString()` or implements an interface member.

A public method of the default-language set that returns a `string` and takes parameters becomes a text, also when it's a helper. Every language then has to implement it. To keep a helper out:

- Make it `private` or `internal`.
- Make it `static`. A static method is never a text, so it can stay public for other assemblies.
- Mark it `[ExcludeFromLocalizationDiscovery]` where it has to stay public on the instance, such as an override of a public member of a base class. The attribute also keeps a public `string` property from being a text.

The generated class repeats the parameters as the set declares them. So the build rejects a method it can't repeat, such as a generic one or one with a `ref` parameter (`LOC400`). [LOC400](build-errors/LOC4XX-texts-with-parameters.md#loc400) lists every case.

Every language names the parameters as the default-language set does (`LOC401`). The compiler checks their types and order, but not their names. Without this check, a set could swap two parameters of the same type and word each value as the other.

Every language also uses the values the default wording shows (`LOC402`). A Dutch `Greeting(string name) => "Hallo!"` next to an English `$"Hello, {name}!"` is a build error, because Dutch users wouldn't see the name. What counts as showing a value:

- A value the default wording only chooses by, such as `salutation` in a `switch`, doesn't count. So it may go unused in a language that doesn't make that distinction.
- A variable that a pattern declares for the whole value counts as the value. So `count switch { 1 => "1 item", var n => $"{n} items" }` shows `count`.

The contract records which values the default wording shows. So the check also holds for a set that an app adds to a library's contract. For a contract you wrote by hand, the build reads them from the default-language set in the same assembly.

What else to know:

- **Plurals.** Idioma has no plural rules. Each language chooses its wording for a number in its own set, in C#. `count is 1 ? ... : ...` is right for English and Dutch. A language with more forms writes a condition for each. Polish has three forms for a count:

  ```csharp
  public string ItemsInCart(int count, string shop) => count switch
  {
      1 => $"1 produkt w koszyku w sklepie {shop}",
      _ when count % 10 is >= 2 and <= 4 && count % 100 is not (>= 12 and <= 14) => $"{count} produkty w koszyku w sklepie {shop}",
      _ => $"{count} produktów w koszyku w sklepie {shop}",
  };
  ```

- **Formatting values.** A value in an interpolated string is formatted for `CultureInfo.CurrentCulture`, as anywhere in C#, and can carry a format, as in `{amount:N2}`. With `CurrentUICultureProvider` set, a text of a set formats its values for the culture it returns. A value in an inline text, or in a property of a set, keeps the format of the thread, because only a method of a set runs in the provider's culture. Inside `UseCulture`, every value follows the scope.
- **A text as a value.** A text is a `string`, so pass it like any other value: `ShopLocalization.Removed(ShopLocalization.Cart)`. Both are worded at the call, for the same user, so they read in one language.
- **A text inside a wording.** A set can read another text in its wording, as in `$"Press {ShopLocalization.Checkout} to pay"`. That text follows the language of the set. So when a text falls back to English for a Dutch user, the text inside it is English too.
- **Inline texts.** An inline text takes a value as you write it: `LocalizedText.Inline(en: $"Hello, {name}!", nl: $"Hallo, {name}!")`. Every language uses the values that the wording of the default language shows in its interpolated strings (`LOC402`). C# builds the wording of every language before `Inline` picks one. So write a text with a costly value as a method of a set, which builds one wording.
- **Placeholders.** A wording that holds a format-string placeholder, such as `{0}` or `{0:N2}`, gets build warning `LOC403`, in a set and in an inline text. A text shows its wording as written, so nothing would fill the placeholder in. Braces around anything but a number are text. A string passed to a method, such as the format of `string.Format`, gets no warning, because the method may fill the placeholder in. Neither does a text that a validation attribute names as its error message, which it fills in ([details](coming-from-resx.md#validation-and-display-attributes)).

## Getting a text by its key

`ShopLocalization.GetTextByKey` gives the text with a key. It's for a key your code only has at run time, such as one from a database. Where you know the text, write `ShopLocalization.Checkout`, which the build checks.

```csharp
string? cart = ShopLocalization.GetTextByKey("Cart"); // What ShopLocalization.Cart gives
```

- **The key.** It's the name of the text, in the same letter case.
- **An unknown key.** It gives `null`, such as for a key from a database whose text was renamed since it was stored. Show something else in its place: `ShopLocalization.GetTextByKey(key) ?? key`.
- **Every key.** `ShopLocalization.TextKeys` lists them, sorted by name in ordinal order, as `StringComparer.Ordinal` sorts them. An app can check its stored keys against it at startup.
- **The values.** A text with parameters takes its values after the key, in the order of its parameters: `ShopLocalization.GetTextByKey("ItemsInCart", cart.Count, shop.Name)`. Each value has to be of the type of its parameter, because a value isn't converted. An `int` for a `long` throws `ArgumentException`, and so does a missing value or one too many. So does `null` for a parameter that isn't nullable, such as `string name`. Wrong values throw, where an unknown key doesn't, because they're a mistake in the call and not in the key. So does a key that is `null`.
- **The result.** It's the wording for the user's language, as the text itself gives it.

## Registration

Every set is registered automatically. When an app starts, it registers the sets of every assembly it references, whether it uses that assembly or not. An assembly the app loads later, such as one a Blazor WebAssembly app loads lazily, registers its sets when its code first runs. Loading it alone doesn't.

The source generator writes the code that does this. It's a class named after your assembly, `LocalizationRegistrar_MyApp` for `MyApp`, in the namespace `CodeChops.Idioma.Generated`. The class is public in an assembly that has sets or texts written as attributes, so an app that references the assembly can register them at startup. In any other assembly it's internal. It's hidden from autocomplete, but it's still public API. So a library that tracks its public API, such as with Microsoft's public API analyzers, lists the registrar next to the contract and the generated class of every public set.

The texts of members marked `[Localized]` are recorded the same way, by the same generated code.

A set is registered by its type, and created when a text of its contract is first shown. So a set whose constructor throws doesn't stop your app from starting, and doesn't stop the texts of other contracts, or inline texts and attributes:

- A text that needs the set throws an `InvalidOperationException` that names the set, with the set's own exception inside.
- The next text of that contract creates the set again. So a constructor that failed once, such as on a setting that wasn't loaded yet, doesn't fail its texts for good.

A set that can't be registered automatically gets build warning `LOC208`, such as a set without a parameterless constructor. [LOC208](build-errors/LOC2XX-sets.md#loc208) lists every case and its fix. A member marked `[Localized]` whose text can't be registered, such as a member of a private nested type, gets warning [LOC502](build-errors/LOC5XX-inline-texts-and-attributes.md#loc502).

A nested, generic, or file-local class with your own texts isn't recognized, so its language is reported as missing (`LOC201`). Make it a top-level class that isn't generic.

To register a set by hand, such as a set that needs a constructor argument, mark it `[ExcludeFromLocalizationDiscovery]` and call `LocalizationRegistry.Register`:

```csharp
[ExcludeFromLocalizationDiscovery]
public sealed class BrandedShopLocalizationEn(string shopName) : ShopLocalizationEn
{
    public override string Checkout => $"Pay at {shopName}"; // Checkout is virtual in ShopLocalizationEn
}

// At startup:
LocalizationRegistry.Register(new BrandedShopLocalizationEn(configuration["ShopName"]!));
```

The build still counts it as a set of its language, and still requires that language to be listed (`LOC202`).

### Dependency injection

Idioma doesn't register or inject anything itself. A set you register by hand can still take services from your app's container, because you create it yourself:

```csharp
[ExcludeFromLocalizationDiscovery]
public sealed class BrandedShopLocalizationEn(IOptions<ShopOptions> options) : ShopLocalizationEn
{
    public override string Checkout => $"Pay at {options.Value.Name}";
}

// At startup, after builder.Build():
LocalizationRegistry.Register(ActivatorUtilities.CreateInstance<BrandedShopLocalizationEn>(app.Services));
```

What to know:

- **Take only singletons.** The registry keeps one instance of the set for the whole process. So a scoped service, such as a `DbContext`, would be shared by every request. In Development, ASP.NET Core throws when you resolve one this way.
- **Read a value per request in the text.** Take a singleton such as `IHttpContextAccessor`, and read from it in the text's getter. The set reads it each time a text is shown, so each request gets its own value.
- **Keep the set thread-safe.** Requests read its texts in parallel.

## Reading sets yourself

Besides registering, `LocalizationRegistry` reads a set directly: `LocalizationRegistry.GetLocalizationSet<IShopLocalization>(culture)`.

```csharp
var dutch = CultureInfo.GetCultureInfo("nl");

// The Dutch set itself, whatever the current UI culture
string? checkout = LocalizationRegistry.GetLocalizationSet<IShopLocalization>(dutch)?.Checkout;
```

`GetLocalizationSet` gives the set of the culture, or else of its parent culture, as the `nl` set for `nl-NL`. It doesn't fall back to a default language, as a text does ([fallback order](#fallback-order)). So a culture without a set gives `null`. The texts of a generated class, such as `ShopLocalization.Checkout`, read the same sets.

## Native AOT

Idioma is safe to trim and to publish with Native AOT. It finds no member by reflection, because the source generator writes every lookup as code.

Check one setting of your project. The templates of a Native AOT app, such as `dotnet new console --aot` and `dotnet new webapiaot`, set `<InvariantGlobalization>true</InvariantGlobalization>`. Such an app has no cultures but the invariant one:

- Every user sees your default language, because the user's culture is the invariant one.
- `CultureInfo.GetCultureInfo("nl")` throws a `CultureNotFoundException`, and so does `app.UseRequestLocalization("en", "nl")`.

Choose one of two settings in the project file:

- `<InvariantGlobalization>false</InvariantGlobalization>` gives the app every culture, with its dates and numbers. On Linux, the app then needs the ICU libraries on the machine.
- `<PredefinedCulturesOnly>false</PredefinedCulturesOnly>`, next to invariant globalization, lets the app create a culture by its name. Texts then follow the user's language, but dates and numbers keep the invariant format, as in `1,234.50`. That suits an app whose texts show no dates or numbers.

## Testing

A test reads a text as your app does, so it gets the wording your sets give:

```csharp
using (LocalizationRegistry.UseCulture(CultureInfo.GetCultureInfo("nl-NL")))
{
    Assert.Equal("Afrekenen", ShopLocalization.Checkout);
}
```

What to know:

- **A test project declares its languages.** It's an app, so it needs `[assembly: SupportedLanguages]` ([LOC103](build-errors/LOC1XX-languages.md#loc103)). List the languages your tests read, because the languages of the app it tests don't count in the test process.
- **Choose the culture with `UseCulture`.** It flows with the test, so tests that run in parallel each keep their own culture. Setting `CultureInfo.CurrentUICulture` works too, unless your app sets `CurrentUICultureProvider`, which then wins.
- **A text always reads the sets of the whole app.** A test can't give `ShopLocalization.Checkout` a registry of its own. To read the set a culture gets, use `LocalizationRegistry.GetLocalizationSet<IShopLocalization>(culture)`.
- **`LocalizationRegistry.Register` is for the whole process and has no undo.** A set a test registers stays for every test after it, also those that run in parallel. Register in a fixture that runs once, or keep such tests out of parallel runs.
- **To check one set, create it.** `new ShopLocalizationNl().Checkout` needs no registry and no culture.