# Blazor

In a Blazor or Razor project, add three namespaces to `_Imports.razor`: Idioma's, the one of your sets, and `System.Globalization` for the `CultureInfo` in the examples below.

```razor
@using System.Globalization
@using CodeChops.Idioma
@using MyApp.Localization
```

Other subjects have a page of their own, listed in the [README](../README.md#documentation).

## A language picker

This picker works in Blazor WebAssembly, Blazor Server, and a Blazor Web App in any render mode, `InteractiveAuto` included. It shows a button for each language your app declares in its own project ([details](libraries-and-apps.md#the-apps-languages)):

```razor
@* LanguagePicker.razor *@
@inject NavigationManager Navigation
@inject IJSRuntime JS

@foreach (var language in LocalizationRegistry.SupportedLanguages)
{
    <button @onclick="() => SelectLanguage(language)">
        @language.ToUpperInvariant()
    </button>
}

@code {
    [Parameter] public EventCallback LanguageChanged { get; set; }

    private async Task SelectLanguage(string name)
    {
        // For the WebAssembly runtime to start in this language, also after a reload.
        await JS.InvokeVoidAsync("localStorage.setItem", "language", name);

        if (OperatingSystem.IsBrowser())
        {
            // For the pages a server renders. Leave this out in standalone Blazor WebAssembly, which has no server.
            await JS.InvokeVoidAsync("fetch", $"culture/set?culture={Uri.EscapeDataString(name)}");

            if (LanguageChanged.HasDelegate)
            {
                // In WebAssembly the app runs for one user, so switch in place. Set the DefaultThread cultures too,
                // because a culture set only on CurrentCulture and CurrentUICulture doesn't last beyond this event.
                var culture = CultureInfo.GetCultureInfo(name);
                CultureInfo.DefaultThreadCurrentCulture = CultureInfo.CurrentCulture = culture;
                CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.CurrentUICulture = culture;

                await LanguageChanged.InvokeAsync();
            }
            else
            {
                // Without a layout that re-renders the page, switching in place would change only this picker.
                // So reload the page.
                Navigation.Refresh(forceReload: true);
            }
        }
        else
        {
            // On a server, the DefaultThread cultures are shared by every user, and a circuit keeps the culture of
            // the request that opened it. So store the choice in the culture cookie and reload.
            var returnUrl = "/" + Navigation.ToBaseRelativePath(Navigation.Uri);
            Navigation.NavigateTo($"culture/set?culture={Uri.EscapeDataString(name)}&returnUrl={Uri.EscapeDataString(returnUrl)}", forceLoad: true);
        }
    }
}
```

The picker does one of two things:

- **In WebAssembly**, it switches in place. Every language is compiled into your assemblies, so this needs no download and no reload. It needs a layout that handles `LanguageChanged`, because that re-renders the rest of the page ([details](#showing-the-new-language-everywhere)). Without one, the picker reloads the page.
- **On a server**, it stores the choice in the culture cookie and reloads the page. The same happens with `InteractiveAuto` until the WebAssembly runtime has downloaded.

Either way, it stores the choice in the browser's local storage as well, for the WebAssembly runtime to start in (see [Remembering the choice](#remembering-the-choice)).

The rest of this page sets up the parts the picker needs. A standalone Blazor WebAssembly app needs only [Showing the new language everywhere](#showing-the-new-language-everywhere), [Remembering the choice](#remembering-the-choice), and [Dates and numbers](#dates-and-numbers).

### The culture endpoint on the server

Every app with a server needs request localization, which gives each request the culture of its cookie, and the endpoint the picker calls. Add both to the server's `Program.cs`, before `app.MapRazorComponents`, with usings for `CodeChops.Idioma`, `Microsoft.AspNetCore.Http.HttpResults` and `Microsoft.AspNetCore.Localization`:

```csharp
app.UseRequestLocalization([.. LocalizationRegistry.SupportedLanguages]);

app.MapGet("/culture/set", (string culture, string? returnUrl, HttpContext http) =>
{
    if (LocalizationRegistry.SupportedLanguages.Contains(culture, StringComparer.OrdinalIgnoreCase))
        http.Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) });

    // Only back to a page of this site, so the endpoint can't send a visitor elsewhere.
    return returnUrl is not null && RedirectHttpResult.IsLocalUrl(returnUrl)
        ? Results.LocalRedirect(returnUrl)
        : Results.NoContent();
});
```

The cookie gets an expiry, so it lasts as long as the choice in local storage does. A cookie without one is gone when the browser closes. The server would then render the next visit in the browser's language, and the page would switch to the stored language once WebAssembly takes over.

The languages come from the `[assembly: SupportedLanguages]` of the server project, so the server lists them nowhere else. In a Blazor Web App, the server project and the client project are each an app, so each declares the same languages ([details](libraries-and-apps.md#the-apps-languages)).

### Showing the new language everywhere

After a click in WebAssembly, Blazor re-renders only the picker. Other components show the new language when they next render. To show it everywhere at once, key the layout's content on the culture, and let the picker re-render the layout through `LanguageChanged`:

```razor
@* MainLayout.razor *@
@inherits LayoutComponentBase

<LanguagePicker LanguageChanged="@StateHasChanged" />

<div @key="CultureInfo.CurrentUICulture.Name">
    <NavMenu />

    @Body
</div>
```

An `EventCallback` re-renders the component that receives it, so any handler would do. `StateHasChanged` makes that explicit.

When the key changes, Blazor rebuilds everything inside it, so every text reads the new culture. Whatever is rebuilt loses its state, such as what a user typed in a form.

Read a text where you show it. A text kept outside the keyed content, such as in a scoped service, isn't rebuilt, so it stays in the old language.

The layout handles the event, so it must be interactive. In standalone Blazor WebAssembly, it always is. In a Blazor Web App, it is only when the whole app is interactive, as in `<Routes @rendermode="InteractiveAuto" />`. In any other app, leave `LanguageChanged` out, so the picker reloads the page. Switching in place would leave the static parts of the page in the old language until the next navigation.

### Remembering the choice

The WebAssembly runtime starts in the browser's language. That also holds after a server has rendered the page in the language of the cookie, so the page would switch back as soon as WebAssembly takes over. To start in the stored language instead, add usings for `System.Globalization`, `Microsoft.JSInterop` and `CodeChops.Idioma` to the `Program.cs` of the WebAssembly project, and replace `await builder.Build().RunAsync();` with:

```csharp
var host = builder.Build();

var js = host.Services.GetRequiredService<IJSRuntime>();
var name = await js.InvokeAsync<string?>("localStorage.getItem", "language");

// A stored value can be outdated or edited by hand, so accept only a language the app declares.
if (name is not null && LocalizationRegistry.SupportedLanguages.Contains(name, StringComparer.OrdinalIgnoreCase))
    CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo(name);

await host.RunAsync();
```

This also needs all globalization data, because the app can start in a culture other than the browser's (see [Dates and numbers](#dates-and-numbers)).

## A browser language the app doesn't have

When none of your app's languages serves the browser's language, make the default language the UI culture. A German visitor of an app in English and Dutch sees English texts either way, but a text with parameters then shows faster. The method of a set always runs with a UI culture of the set's own language. So with German as the UI culture, Idioma switches the UI culture to English while each such method runs.

Add this to the `Program.cs` of the WebAssembly project, with usings for `System.Globalization` and `CodeChops.Idioma`. Put it right before the host runs: before `await host.RunAsync();` with the code of [Remembering the choice](#remembering-the-choice), or before `await builder.Build().RunAsync();` without it.

```csharp
// The stored language, else the browser's. Where none of the app's languages serves it, texts show the default
// language, so make that the UI culture too.
if (!IsServed(CultureInfo.CurrentUICulture))
    CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo(LocalizationRegistry.DefaultLanguage!);

// Whether one of the app's languages is the culture, or a culture it falls back to, as en-GB falls back to en.
static bool IsServed(CultureInfo culture) => culture.Name.Length > 0 &&
    (LocalizationRegistry.SupportedLanguages.Contains(culture.Name, StringComparer.OrdinalIgnoreCase) || IsServed(culture.Parent));
```

What changes for such a user:

- Only the UI culture changes. Dates and numbers keep the format of the browser's language, so a German visitor still reads a date as 20.11.2026.
- Anything else that follows the UI culture shows the default language too, such as the resx messages of another library.

A server needs nothing of this. Its request localization already gives such a user the default language, for dates and numbers too.

This also needs all globalization data, because the app can start in a culture other than the browser's (see [Dates and numbers](#dates-and-numbers)).

## Dates and numbers

To show dates and numbers correctly in every culture, set `<BlazorWebAssemblyLoadAllGlobalizationData>true</BlazorWebAssemblyLoadAllGlobalizationData>` in the project file of the WebAssembly app. In a Blazor Web App, that's the client project.

Dates and numbers follow the culture too, but they need its globalization data. Blazor loads only the part for the culture the app starts with, so an app starting in English shows Dutch dates in English.