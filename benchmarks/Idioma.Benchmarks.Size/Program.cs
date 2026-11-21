using Benchmarks;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
#if LAZY
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Components.WebAssembly.Services;
#endif

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// The page of the approach the project file chose (-p:SizeApproach).
#if IDIOMA
builder.RootComponents.Add<IdiomaStorePage>("#app");
#elif LOCALIZER
builder.RootComponents.Add<LocalizerStorePage>("#app");
builder.Services.AddLocalization();
#else
builder.RootComponents.Add<PlainStorePage>("#app");
#endif

#if LAZY
var host = builder.Build();

// The Dutch set is in an assembly of its own (-p:LazyLanguage=true), which only a Dutch user downloads, before the
// first text shows. Loading the assembly doesn't register its set. Its module initializer does, and runs here because
// nothing else runs code of the assembly.
if (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName is "nl")
{
    var loader = host.Services.GetRequiredService<LazyAssemblyLoader>();

    foreach (var assembly in await loader.LoadAssembliesAsync(["Idioma.Benchmarks.Size.Texts.Nl.wasm"]))
        RuntimeHelpers.RunModuleConstructor(assembly.ManifestModule.ModuleHandle);
}

await host.RunAsync();
#else
await builder.Build().RunAsync();
#endif
