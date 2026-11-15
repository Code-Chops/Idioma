# Blazor

In a Blazor or Razor project, add the namespace to `_Imports.razor`, together with the namespace of your sets, and `System.Globalization` for the `CultureInfo` that the examples below use:

```razor
@using System.Globalization
@using CodeChops.Idioma
@using MyApp.Translations
```

Other subjects have a page of their own: [how Idioma works](how-it-works.md), [language codes](language-codes.md), [libraries that use Idioma](libraries-and-apps.md), [what the build rejects](build-errors.md), and [coming from resx](coming-from-resx.md).

## Switching language without a reload

With Idioma, every language is compiled into your assemblies, so switching needs no download and no reload. Switch by setting the culture, as this language picker does. It shows a button for each language your app declares in its own project ([details](libraries-and-apps.md#the-apps-languages)):

```razor
@* LanguagePicker.razor *@
@foreach (var language in TranslationsRegistry.SupportedLanguages)
{
    <button @onclick="() => SelectLanguage(language)">
      @language.ToUpperInvariant()
    </button>
}

@code {
    [Parameter] public EventCallback LanguageChanged { get; set; }

    private async Task SelectLanguage(string name)
    {
        var culture = CultureInfo.GetCultureInfo(name);
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.CurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.CurrentUICulture = culture;

        await LanguageChanged.InvokeAsync();
    }
}
```

Set the `DefaultThread` cultures too: a culture set only on `CultureInfo.CurrentUICulture` lasts until Blazor has handled the event, and the next event shows the old language again. Do this in WebAssembly only, where the app runs for one user: on a server it would change the default culture for every user.

After the click, Blazor re-renders only the picker, and other components show the new language when they next render. To show it everywhere at once, key the layout's content on the culture, and let the picker re-render the layout through `LanguageChanged`:

```razor
@* MainLayout.razor *@
@inherits LayoutComponentBase

<LanguagePicker LanguageChanged="@StateHasChanged" />

<div @key="CultureInfo.CurrentUICulture.Name">
    <NavMenu />

    @Body
</div>
```

When the key changes, Blazor builds everything inside it anew, so every text reads the new culture. What is built anew loses its state, such as what a user typed in a form. The layout handles the event, so it must be interactive, as it is when the whole app is: `<Routes @rendermode="InteractiveWebAssembly" />`.

### Remembering the choice

The choice lasts until the page reloads: the app then starts in the browser's language again. To keep it, store the language when the user picks one, in the browser's local storage for example. Give the picker `@inject IJSRuntime JS`, and add this line to `SelectLanguage`:

```csharp
await JS.InvokeVoidAsync("localStorage.setItem", "language", name);
```

Then start the app in the stored language. In `Program.cs` of the WebAssembly project, add usings for `System.Globalization` and `Microsoft.JSInterop`, and replace `await builder.Build().RunAsync();` with:

```csharp
var host = builder.Build();

var js = host.Services.GetRequiredService<IJSRuntime>();

if (await js.InvokeAsync<string?>("localStorage.getItem", "language") is { } name)
    CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo(name);

await host.RunAsync();
```

This needs all globalization data (see [Dates and numbers](#dates-and-numbers)): without it, Blazor refuses to start in another language than the browser's.

## On a server

On a server, every user has their own language. A text follows the culture ASP.NET Core gives the request or the Blazor Server circuit it is shown in, so set that up with request localization (`app.UseRequestLocalization`). A circuit keeps the culture of the request that opened it: to switch, store the new culture in the culture cookie and reload the page, as [Microsoft's culture selector](https://learn.microsoft.com/aspnet/core/blazor/globalization-localization#dynamically-set-the-server-side-culture-by-user-preference) does.

## Dates and numbers

Dates and numbers follow the culture too, but they need its globalization data, and Blazor loads only the part that covers the culture the app starts with: an app starting in English shows Dutch dates in English. To load all of it, set `<BlazorWebAssemblyLoadAllGlobalizationData>true</BlazorWebAssemblyLoadAllGlobalizationData>` in the app's project file.

## A text as a component parameter

A text can be a parameter of a component with another render mode, such as an `InteractiveWebAssembly` component on a static page: Blazor passes it on as JSON, and the text still shows the user's language on the other side, even after a switch ([details](how-it-works.md#texts-in-json)).