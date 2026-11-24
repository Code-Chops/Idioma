# ASP.NET Core

This page covers a server app: Razor Pages, MVC, and minimal APIs. A Blazor app has a [page of its own](blazor.md).

Other subjects have a page of their own, listed in the [README](../README.md#documentation).

## The language of a request

A text follows `CultureInfo.CurrentUICulture`. ASP.NET Core sets it for each request once you add request localization, with your app's languages:

```csharp
var app = builder.Build();

app.UseRequestLocalization([.. LocalizationRegistry.SupportedLanguages]);
```

The first language is the default one. ASP.NET Core then takes the language of a request from the query string, a cookie, or the `Accept-Language` header, in that order. Without this line, every user gets the language of the server.

A user whose language you don't support gets your default language, from ASP.NET Core and from Idioma alike ([Fallback order](advanced-topics.md#fallback-order)).

## Showing a text

Read a text where you show it. It gives the wording for the language of the request:

```cshtml
<button>@ShopLocalization.Checkout</button>
<p>@ShopLocalization.Greeting(Model.Customer.Name)</p>
```

```csharp
app.MapGet("/checkout/label", () => ShopLocalization.Checkout);
```

## Labels and validation messages

`[Display]` and the validation attributes read a text of a set by its name. Name the generated class:

```csharp
public class Customer
{
    [Display(Name = nameof(ShopLocalization.Name), ResourceType = typeof(ShopLocalization))]
    [Required(ErrorMessageResourceName = nameof(ShopLocalization.NameRequired), ErrorMessageResourceType = typeof(ShopLocalization))]
    public string? Name { get; set; }
}
```

The attribute reads the text for each request, so the label and the message follow the user's language. The set has to be public ([details](coming-from-resx.md#validation-and-display-attributes)).

## What to watch on a server

A server serves users of different languages at once, so a text that's kept shows one user the language of another:

- **A static field or property.** `static readonly string Label = ShopLocalization.Checkout;` keeps the language of the first request. The build warns about it ([LOC602](build-errors.md#loc602)). Write `static string Label => ShopLocalization.Checkout;`.
- **A singleton or a cache.** A text kept in a field of a singleton, or in a cached view model, stays in the language it was read in. The build doesn't see this. Keep what the text depends on, such as an id, and read the text per request.
- **Work outside a request.** A background job or an email has no request, so it runs in the language of the server. Wrap the block that reads the texts in `LocalizationRegistry.UseCulture`, with the culture of the recipient ([Another language](advanced-topics.md#another-language)).

## A language from the user's profile

An app that keeps each user's language in their profile doesn't need request localization for its texts. Set a provider once at startup:

```csharp
LocalizationRegistry.CurrentUICultureProvider = () => UserProfile.Current?.Culture;
```

Every text then follows the culture it returns ([details](advanced-topics.md#the-current-culture)). Return the same `CultureInfo` object for the same language, as `CultureInfo.GetCultureInfo` does, because Idioma finds its sets fastest for a culture object it has seen before.
