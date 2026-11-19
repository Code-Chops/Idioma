using Benchmarks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<BenchmarkPage>("#app");
builder.Services.AddLocalization();

var host = builder.Build();

// Set before RunAsync, which loads the satellite assemblies of the culture, as in an app whose users pick a language.
BenchmarkRun.Current = BenchmarkRun.Parse(host.Services.GetRequiredService<NavigationManager>().Uri);
Culture.Use(BenchmarkRun.Current.Culture);

await host.RunAsync();
