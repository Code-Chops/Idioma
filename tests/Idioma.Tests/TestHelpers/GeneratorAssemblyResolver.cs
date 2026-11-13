using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace CodeChops.Idioma.Tests.TestHelpers;

/// <summary>
/// The generator project is marked <c>DevelopmentDependency</c>, which keeps it out of the test host's <c>deps.json</c>
/// even though its DLL is copied next to the tests, so instantiating a generator would fail to load it. This probes the
/// app base directory for an unresolved assembly.
/// </summary>
internal static class GeneratorAssemblyResolver
{
    [ModuleInitializer]
    internal static void Register()
    {
        AssemblyLoadContext.Default.Resolving += static (context, name) =>
        {
            if (name.Name is null)
                return null;

            var candidate = Path.Combine(AppContext.BaseDirectory, name.Name + ".dll");

            return File.Exists(candidate)
                ? context.LoadFromAssemblyPath(candidate)
                : null;
        };
    }
}
