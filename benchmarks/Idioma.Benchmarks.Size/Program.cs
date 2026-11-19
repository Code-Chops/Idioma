using Benchmarks;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<StorePage>("#app");
builder.Services.AddLocalization();

await builder.Build().RunAsync();
