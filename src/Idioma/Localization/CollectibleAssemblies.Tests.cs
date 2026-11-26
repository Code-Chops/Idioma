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
    /// A plugin with sets, a text with a value, an inline text, and a text on a member. It asks for the member's text by
    /// the type it names and by a <see cref="Type"/>.
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
            public static string Read() => $"{PluginLocalization.Title}, {PluginLocalization.Greeting("Max")}, {LocalizedText.Inline(en: "Yes", nl: "Ja")}, {LocalizedText.GetTextByMember<Address>(nameof(Address.City))}, {LocalizedText.GetTextByMember(typeof(Address), nameof(Address.City))}";
        }
        """);

    [Test]
    // Alone, because a test next to it that holds the sets of the app, as LocalizationRegistry.Sets gives them, would
    // hold the plugin's too.
    [NotInParallel]
    public void Plugin_WhoseContextUnloads_ShouldBeLetGo()
    {
        // Arrange & Act: the plugin registers as its code first runs, and shows each kind of text.
        var (texts, _, context) = ReadTextsOfPluginAndUnload(readAfterUnload: false);

        for (var i = 0; i < 20 && context.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        // Assert
        texts.ShouldBe("Insteekmodule, Hallo, Max, Ja, Plaats, Plaats");
        context.IsAlive.ShouldBeFalse();
        LocalizationRegistry.Sets.ShouldNotContain(set => set.GetType().Assembly.IsCollectible);
    }

    [Test]
    [NotInParallel]
    public void Plugin_WhoseTextsAreReadAfterItsContextUnloads_ShouldBeLetGo()
    {
        // Arrange & Act: code of the plugin can still run until its context is collected, and then reads the registry
        // without the plugin's sets.
        var (_, textsAfterUnload, context) = ReadTextsOfPluginAndUnload(readAfterUnload: true);

        for (var i = 0; i < 20 && context.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        // Assert
        textsAfterUnload.ShouldNotBeNullOrEmpty();
        context.IsAlive.ShouldBeFalse();
    }

    [Test]
    [NotInParallel]
    public void Plugin_WhoseTypeClosesAGenericSetOfALastingAssembly_ShouldBeLetGo()
    {
        // Arrange & Act: the set's type lies in the lasting assembly, and only its type argument in the plugin.
        var context = RegisterGenericSetOfPluginAndUnload();

        for (var i = 0; i < 20 && context.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        // Assert
        context.IsAlive.ShouldBeFalse();
        LocalizationRegistry.Sets.ShouldNotContain(set => set.GetType().IsCollectible);
    }

    /// <summary>
    /// A generic set in an assembly that stays loaded, which a plugin's type can close.
    /// </summary>
    private static Assembly LastingSets { get; } = new AssemblyLoadContext("LastingSets").LoadFromStream(new MemoryStream(GeneratorTestHelper.EmitLibrary("LastingSets", """
        using CodeChops.Idioma;

        namespace LastingSets;

        public interface ISetOfPlugin;

        public class SetOf<T> : ILocalizationSet, ISetOfPlugin
        {
            string ILocalizationSet.LocalizationLanguageCode => "en";
        }
        """)));

    // Not inlined, so that nothing of the plugin is left on the stack of the test.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference RegisterGenericSetOfPluginAndUnload()
    {
        var context = new PluginContext();
        var plugin = context.LoadFromStream(new MemoryStream(GeneratorTestHelper.EmitLibrary("PluginOfGenericSet", "namespace PluginOfGenericSet; public sealed class Thing;")));
        var setType = LastingSets.GetType("LastingSets.SetOf`1")!.MakeGenericType(plugin.GetType("PluginOfGenericSet.Thing")!);

        LocalizationRegistry.Register((ILocalizationSet)Activator.CreateInstance(setType)!);
        context.Unload();

        return new WeakReference(context);
    }

    // Not inlined, so that nothing of the plugin is left on the stack of the test.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (string Texts, string? TextsAfterUnload, WeakReference Context) ReadTextsOfPluginAndUnload(bool readAfterUnload)
    {
        var context = new PluginContext();
        var assembly = context.LoadFromStream(new MemoryStream(Plugin));
        var read = assembly.GetType("UnloadedPlugin.Texts")!.GetMethod("Read")!;

        string texts;
        string? textsAfterUnload = null;

        using (LocalizationRegistry.UseCulture(CultureInfo.GetCultureInfo("nl-NL")))
        {
            texts = (string)read.Invoke(null, null)!;
            context.Unload();

            if (readAfterUnload)
                textsAfterUnload = (string)read.Invoke(null, null)!;
        }

        return (texts, textsAfterUnload, new WeakReference(context));
    }

    /// <summary>
    /// A context that can unload, which takes every assembly but the plugin from the app, Idioma included.
    /// </summary>
    private sealed class PluginContext() : AssemblyLoadContext("Plugin", isCollectible: true)
    {
        protected override Assembly? Load(AssemblyName assemblyName) => null;
    }
}
