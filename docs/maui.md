# .NET MAUI

This page covers a .NET MAUI app: showing a text in XAML, the device's language, and switching languages in the app.

Other subjects have a page of their own, listed in the [README](../README.md#documentation).

## Declaring your languages

The project of a MAUI app is an app, so it declares its languages, also when its texts are in a library ([details](libraries-and-apps.md#the-apps-languages)). Put the attribute in `MauiProgram.cs`, after its `using` lines:

```csharp
using CodeChops.Idioma;

[assembly: SupportedLanguages(defaultLanguage: "en", "nl")]
```

Declare the same languages for each platform too, as Microsoft's [MAUI localization docs](https://learn.microsoft.com/dotnet/maui/fundamentals/localization#perform-platform-setup) describe:

- On iOS and Mac Catalyst, list them under `CFBundleLocalizations` in `Platforms/iOS/Info.plist` and `Platforms/MacCatalyst/Info.plist`, and set your default language as `CFBundleDevelopmentRegion`.
- On Windows, replace `<Resource Language="x-generate"/>` in `Platforms/Windows/Package.appxmanifest` with a `<Resource Language="..."/>` per language.
- Android needs nothing.

The app then starts in the language of the device. A device language you don't support gets your default language ([Fallback order](advanced-topics.md#fallback-order)).

## Showing a text in XAML

Show a text of a set with `x:Static`, after an `xmlns` of the namespace of your sets:

```xml
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:loc="clr-namespace:MyApp.Localization"
             x:Class="MyApp.CartPage">

    <Button Text="{x:Static loc:ShopLocalization.Checkout}" />

</ContentPage>
```

`x:Static` reads the text once, when the page is created. So does code-behind that sets a label's text. That's the wording for the user's language at that moment.

A text with parameters, the text of an enum value, and a text written as an attribute need code. Give the page or its view model a property that reads the text:

```csharp
public string Greeting => ShopLocalization.Greeting(customer.Name);
public string Status => order.Status.GetText();
```

Bind to it, as in `<Label Text="{Binding Greeting}" />`.

## Switching languages in the app

A language picker in the app does two things:

1. It makes every text follow the language the user chose.
2. It creates the pages again, so that each text is read again.

For the first, set `LocalizationRegistry.CurrentUICultureProvider` once at startup ([details](advanced-topics.md#the-current-culture)). Every text then follows the culture it returns, or the device's language where it returns `null`. The provider leaves the cultures of the app's threads alone, because Microsoft's docs note that setting `CultureInfo.CurrentUICulture` in code behaves differently per platform.

For the second, give the window a new page. The user loses what the page held, such as what they typed in a form.

```csharp
// LanguageChoice.cs
using System.Globalization;
using CodeChops.Idioma;

public static class LanguageChoice
{
    private static CultureInfo? chosen;

    // Once at startup, in the constructor of App, which runs before it creates its first page.
    public static void Restore()
    {
        var name = Preferences.Default.Get("language", "");

        if (name.Length > 0)
            chosen = CultureInfo.GetCultureInfo(name);

        LocalizationRegistry.CurrentUICultureProvider = static () => chosen;
    }

    // From the picker.
    public static void Select(string name)
    {
        chosen = CultureInfo.GetCultureInfo(name);
        Preferences.Default.Set("language", name);

        // x:Static and code-behind read each text once, so create the pages again.
        Application.Current!.Windows[0].Page = new AppShell();
    }
}
```

The picker shows a button for each language your app declares, here in the code-behind of a page with a `HorizontalStackLayout` named `Languages`:

```csharp
foreach (var language in LocalizationRegistry.SupportedLanguages)
    Languages.Add(new Button { Text = language.ToUpperInvariant(), Command = new Command(() => LanguageChoice.Select(language)) });
```

An app without Shell creates its first page in its own way. Give the window that page instead of `AppShell`.

Dates and numbers keep the format of the device, because the provider changes only the language of the texts. A value inside a text with parameters is formatted that way too.
