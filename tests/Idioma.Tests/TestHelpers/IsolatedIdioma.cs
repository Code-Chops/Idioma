using System.Reflection;
using System.Runtime.Loader;

namespace CodeChops.Idioma.Tests.TestHelpers;

/// <summary>
/// A copy of Idioma, loaded into a context of its own, with a registry apart from that of this test process: its sets,
/// its app and its languages. An assembly loaded into a context from <see cref="CreateContext"/> uses this copy.
/// Disposing it unloads the copy.
/// </summary>
/// <remarks>
/// For a test whose failure would end the process, such as one of a stack that overflows, so that it can't leave the
/// registry of this process in a state other tests see.
/// </remarks>
internal sealed class IsolatedIdioma : IDisposable
{
    private CopyContext Context { get; } = new();

    /// <summary>
    /// The copy of Idioma.
    /// </summary>
    public Assembly Assembly { get; }

    public IsolatedIdioma()
    {
        this.Assembly = this.Context.LoadFromAssemblyPath(typeof(LocalizationRegistry).Assembly.Location);
    }

    /// <summary>
    /// The copy's <see cref="LocalizationRegistry"/>.
    /// </summary>
    public Type Registry => this.Assembly.GetType(typeof(LocalizationRegistry).FullName!, throwOnError: true)!;

    /// <summary>
    /// A context that can unload, which gives the assemblies loaded into it this copy of Idioma, and every other
    /// assembly from the test process.
    /// </summary>
    public AssemblyLoadContext CreateContext(string name) => new ContextUsingCopy(name, this.Assembly);

    public void Dispose() => this.Context.Unload();

    private sealed class CopyContext() : AssemblyLoadContext(nameof(IsolatedIdioma), isCollectible: true)
    {
        protected override Assembly? Load(AssemblyName assemblyName) => null;
    }

    private sealed class ContextUsingCopy(string name, Assembly idioma) : AssemblyLoadContext(name, isCollectible: true)
    {
        protected override Assembly? Load(AssemblyName assemblyName) =>
            String.Equals(assemblyName.Name, idioma.GetName().Name, StringComparison.Ordinal) ? idioma : null;
    }
}
