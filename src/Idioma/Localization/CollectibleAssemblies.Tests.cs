using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using CodeChops.Idioma.Tests.TestHelpers;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="CollectibleAssemblies"/>: a plugin that uses Idioma unloads with its context.
/// </summary>
public sealed class CollectibleAssembliesTests
{
    /// <summary>
    /// A plugin with sets, a text with a value, an inline text, and a text on a member.
    /// </summary>
    private static byte[] Plugin { get; } = GeneratorTestHelper.EmitLibraryWithGenerators("UnloadedPlugin", """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace UnloadedPlugin;

        public class PluginLocalizationEn : ILocalizationSet, IPluginLocalization
        {
            public string Title => "Plugin";
            public string Greeting(string name) => $"Hello, {name}";
        }

        public class PluginLocalizationNl : ILocalizationSet, IPluginLocalization
        {
            public string Title => "Insteekmodule";
            public string Greeting(string name) => $"Hallo, {name}";
        }

        public class Address
        {
            [Localized(en: "City", nl: "Plaats")]
            public string City { get; set; } = "";
        }

        public static class Texts
        {
            public static string Read() => $"{PluginLocalization.Title}, {PluginLocalization.Greeting("Max")}, {LocalizedText.Inline(en: "Yes", nl: "Ja")}, {LocalizedText.GetTextByMember<Address>(nameof(Address.City))}";
        }
        """);

    [Test]
    // Alone, because a test next to it that holds the sets of the app, as LocalizationRegistry.Sets gives them, would
    // hold the plugin's too.
    [NotInParallel]
    public void Plugin_WhoseContextUnloads_ShouldBeLetGo()
    {
        // Arrange & Act: the plugin registers as its code first runs, and shows each kind of text.
        var (texts, context) = ReadTextsOfPluginAndUnload();

        for (var i = 0; i < 20 && context.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        // Assert
        texts.ShouldBe("Insteekmodule, Hallo, Max, Ja, Plaats");
        context.IsAlive.ShouldBeFalse();
        LocalizationRegistry.Sets.ShouldNotContain(set => set.GetType().Assembly.IsCollectible);
    }

    // Not inlined, so that nothing of the plugin is left on the stack of the test.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (string Texts, WeakReference Context) ReadTextsOfPluginAndUnload()
    {
        var context = new PluginContext();
        var assembly = context.LoadFromStream(new MemoryStream(Plugin));
        var read = assembly.GetType("UnloadedPlugin.Texts")!.GetMethod("Read")!;

        string texts;

        using (LocalizationRegistry.UseCulture(CultureInfo.GetCultureInfo("nl-NL")))
            texts = (string)read.Invoke(null, null)!;

        context.Unload();

        return (texts, new WeakReference(context));
    }

    /// <summary>
    /// A context that can unload, which takes every assembly but the plugin from the app, Idioma included.
    /// </summary>
    private sealed class PluginContext() : AssemblyLoadContext("Plugin", isCollectible: true)
    {
        protected override Assembly? Load(AssemblyName assemblyName) => null;
    }
}
