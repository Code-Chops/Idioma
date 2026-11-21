using Benchmarks;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

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

await builder.Build().RunAsync();
