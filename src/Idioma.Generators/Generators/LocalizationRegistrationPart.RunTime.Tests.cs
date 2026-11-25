using System.Reflection;
using System.Runtime.CompilerServices;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

public sealed partial class LocalizationRegistrationPartTests
{
    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void AppReferencingAssemblyItNeverUses_ShouldRegisterItsSetsAtStart()
    {
        // Arrange: a library, an add-on holding only a German set for the library's contract, and an app that declares
        // German and never touches the add-on. Before the app called the add-on's registrar, the add-on never loaded,
        // while its set still satisfied the app's LOC700 check.
        var library = GeneratorTestHelper.EmitLibraryWithGenerators("UnusedAddOnLibrary", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace UnusedAddOnLibrary;

            public class AddOnShopLocalizationEn : ILocalizationSet, IAddOnShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            public sealed class AddOnShopLocalizationNl : ILocalizationSet, IAddOnShopLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """);
        var addOn = GeneratorTestHelper.EmitLibraryWithGenerators("UnusedAddOn", """
            namespace UnusedAddOn;

            public sealed class AddOnShopLocalizationDe : CodeChops.Idioma.ILocalizationSet, UnusedAddOnLibrary.IAddOnShopLocalization
            {
                public string Checkout => "Zur Kasse";
            }
            """, MetadataReference.CreateFromImage(library));
        var app = GeneratorTestHelper.EmitLibraryWithGenerators("UnusedAddOnApp", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]

            namespace UnusedAddOnApp;
            """, MetadataReference.CreateFromImage(library), MetadataReference.CreateFromImage(addOn));
        var context = new InMemoryLoadContext(new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["UnusedAddOnLibrary"] = library,
            ["UnusedAddOn"] = addOn,
            ["UnusedAddOnApp"] = app,
        });

        // Act: the app's module initializer, as at its start.
        var appAssembly = context.LoadFromAssemblyName(new AssemblyName("UnusedAddOnApp"));
        RuntimeHelpers.RunModuleConstructor(appAssembly.ManifestModule.ModuleHandle);

        // Assert: read as the app declaring German sees it; this test process declares only en and nl itself, so its
        // own app languages would send de-DE to English.
        context.Assemblies.ShouldContain(assembly => String.Equals(assembly.GetName().Name, "UnusedAddOn", StringComparison.Ordinal));
        var contract = context.LoadFromAssemblyName(new AssemblyName("UnusedAddOnLibrary")).GetType("UnusedAddOnLibrary.IAddOnShopLocalization")!;
        var german = LocalizationRegistry.Find(contract, CultureInfo.GetCultureInfo("de-DE")).ShouldNotBeNull();
        contract.GetProperty("Checkout")!.GetValue(german).ShouldBe("Zur Kasse");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void AppReferencingAssemblyThatIsNotThere_ShouldStartAndRegisterItsOwnSets()
    {
        // Arrange: the app's folder lacks a library it references, as a Blazor WebAssembly app lacks an assembly it
        // loads lazily. Before the call to the library's registrar was guarded, the app's module initializer threw, so
        // the app did not start.
        var library = GeneratorTestHelper.EmitLibraryWithGenerators("AbsentLibrary", """
            using CodeChops.Idioma;

            namespace AbsentLibrary;

            public interface IAbsentLocalization : ILocalizationSet
            {
                string Checkout { get; }
            }

            public sealed class AbsentLocalizationEn : ILocalizationSet, IAbsentLocalization
            {
                public string Checkout => "Checkout";
            }
            """);
        var app = GeneratorTestHelper.EmitLibraryWithGenerators("AbsentLibraryApp", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace AbsentLibraryApp;

            public class AbsentAppLocalizationEn : ILocalizationSet, IAbsentAppLocalization
            {
                public string Checkout => "Checkout";
            }

            public sealed class AbsentAppLocalizationNl : ILocalizationSet, IAbsentAppLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """, MetadataReference.CreateFromImage(library));
        var context = new InMemoryLoadContext(new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["AbsentLibraryApp"] = app,
        });

        // Act: the app's module initializer, as at its start.
        var appAssembly = context.LoadFromAssemblyName(new AssemblyName("AbsentLibraryApp"));
        RuntimeHelpers.RunModuleConstructor(appAssembly.ManifestModule.ModuleHandle);

        // Assert
        context.Assemblies.ShouldNotContain(assembly => String.Equals(assembly.GetName().Name, "AbsentLibrary", StringComparison.Ordinal));
        var texts = appAssembly.GetType("AbsentLibraryApp.AbsentAppLocalization")!;
        ReadText(texts, "Checkout", "nl-NL").ShouldBe("Afrekenen");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void AssemblyThatArrivesAfterAppStarted_ShouldBeUsableAndRegisterItsSetsWhenItsCodeFirstRuns()
    {
        // Arrange: the app starts without a library it references, as a Blazor WebAssembly app starts without an
        // assembly it loads lazily, and the library arrives afterwards. This runtime tries again when a call into an
        // assembly failed before, so the test passed with the call alone as well; the WebAssembly runtime does not,
        // which is what the test below is for.
        var library = GeneratorTestHelper.EmitLibraryWithGenerators("LateLibrary", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace LateLibrary;

            public class LateLocalizationEn : ILocalizationSet, ILateLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            public sealed class LateLocalizationNl : ILocalizationSet, ILateLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """);
        var app = GeneratorTestHelper.EmitLibraryWithGenerators("LateLibraryApp", """
            using System.Globalization;
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace LateLibraryApp;

            public static class Reader
            {
                public static string ReadCheckout() => LateLibrary.LateLocalization.Checkout;
            }
            """, MetadataReference.CreateFromImage(library));
        var images = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["LateLibraryApp"] = app,
        };
        var context = new InMemoryLoadContext(images);
        var appAssembly = context.LoadFromAssemblyName(new AssemblyName("LateLibraryApp"));
        RuntimeHelpers.RunModuleConstructor(appAssembly.ManifestModule.ModuleHandle);
        context.Assemblies.ShouldNotContain(assembly => String.Equals(assembly.GetName().Name, "LateLibrary", StringComparison.Ordinal));

        // Act: the library arrives, and the app's code calls into it for the first time.
        images["LateLibrary"] = library;
        string checkout;

        using (new CulturesScope("nl-NL"))
            checkout = (string)appAssembly.GetType("LateLibraryApp.Reader")!.GetMethod("ReadCheckout")!.Invoke(obj: null, parameters: null)!;

        // Assert: the library's own registrar ran with its code, so its Dutch set words the text.
        checkout.ShouldBe("Afrekenen");
    }

    [Test]
    public void ReferencedRegistrar_IsCalledOnlyAfterItsAssemblyIsFoundByName()
    {
        // Arrange & Act: the WebAssembly runtime remembers a call into an assembly that was not there, and refuses
        // every later use of that assembly from the calling one, also once it has loaded lazily. Seen in a browser: a
        // lookup by name that fails leaves nothing behind, and a call that fails does.
        var text = GeneratedText(GeneratorTestHelper.RunAsApplication(LocalizationParts.Registration(), "namespace TestApp;", SetsLibrary));

        // Assert: the lookup comes first, inside the same try, so a missing assembly skips the call.
        var guard = text.Substring(text.IndexOf("private static void RegisterReferenced(string assemblyName, System.Action register)", StringComparison.Ordinal));
        var lookup = guard.IndexOf("System.Reflection.Assembly.Load(new System.Reflection.AssemblyName { Name = assemblyName });", StringComparison.Ordinal);
        var call = guard.IndexOf("register();", StringComparison.Ordinal);
        var handler = guard.IndexOf("catch (System.IO.FileNotFoundException)", StringComparison.Ordinal);

        lookup.ShouldBeGreaterThan(guard.IndexOf("try", StringComparison.Ordinal));
        call.ShouldBeGreaterThan(lookup);
        handler.ShouldBeGreaterThan(call);
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void SetDerivedFromAbstractSetOfGeneratedContract_ShouldShowItsLanguageAtRunTime()
    {
        // Arrange: only the abstract base names the contract generated here. The Dutch set used to be left out of the
        // registration, so Dutch users saw English.
        var image = GeneratorTestHelper.EmitLibraryWithGenerators("AbstractBaseLibrary", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace AbstractBaseLibrary;

            public class BaseShopLocalizationEn : ILocalizationSet, IBaseShopLocalization
            {
                public string Checkout => "Checkout";
            }

            public abstract class BaseShopLocalizationBase : ILocalizationSet, IBaseShopLocalization
            {
                public abstract string Checkout { get; }
            }

            public sealed class BaseShopLocalizationNl : BaseShopLocalizationBase
            {
                public override string Checkout => "Afrekenen";
            }
            """);
        var context = new InMemoryLoadContext(new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["AbstractBaseLibrary"] = image,
        });

        // Act: the library's module initializer, as when it loads.
        var assembly = context.LoadFromAssemblyName(new AssemblyName("AbstractBaseLibrary"));
        RuntimeHelpers.RunModuleConstructor(assembly.ManifestModule.ModuleHandle);

        // Assert
        var texts = assembly.GetType("AbstractBaseLibrary.BaseShopLocalization")!;
        ReadText(texts, "Checkout", "nl-NL").ShouldBe("Afrekenen");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void SetNamedAfterDeclaredLongCode_ShouldServeItAtRunTime()
    {
        // Arrange: neither set implements LocalizationLanguageCode; the rewording's name gives no language of its own.
        var image = GeneratorTestHelper.EmitLibraryWithGenerators("RegionalNameLibrary", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl-BE")]

            namespace RegionalNameLibrary;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
                public virtual string Total => "Total";
            }

            public class ShopLocalizationNlBe : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Afrekenen";
                public virtual string Total => "Totaal";
            }

            public sealed class ShopRewording : ShopLocalizationNlBe
            {
                public override string Checkout => "Betalen";
            }
            """);
        var context = new InMemoryLoadContext(new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["RegionalNameLibrary"] = image,
        });

        // Act: the library's module initializer, as when it loads.
        var assembly = context.LoadFromAssemblyName(new AssemblyName("RegionalNameLibrary"));
        RuntimeHelpers.RunModuleConstructor(assembly.ManifestModule.ModuleHandle);

        // Assert: both serve nl-BE, not be; the rewording wins over its base, and keeps the base's other texts.
        var belgian = (ILocalizationSet)Activator.CreateInstance(assembly.GetType("RegionalNameLibrary.ShopLocalizationNlBe")!)!;
        var rewording = (ILocalizationSet)Activator.CreateInstance(assembly.GetType("RegionalNameLibrary.ShopRewording")!)!;
        belgian.LocalizationLanguageCode.ShouldBe("nl-BE");
        rewording.LocalizationLanguageCode.ShouldBe("nl-BE");

        var texts = assembly.GetType("RegionalNameLibrary.ShopLocalization")!;
        ReadText(texts, "Checkout", "nl-BE").ShouldBe("Betalen");
        ReadText(texts, "Total", "nl-BE").ShouldBe("Totaal");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void SetOfLibraryEnumWithLocalizedMembers_ShouldGiveItsTextsThroughTheGetTextOfTheLibrary()
    {
        // Arrange: a library whose enum carries its texts as [Localized], in English and Dutch, and an app that adds
        // German with a set of the enum. The app's code sees the namespaces of both, where two GetText() would be
        // ambiguous (CS0121).
        var library = GeneratorTestHelper.EmitLibraryWithGenerators("EnumTextsLibrary", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace EnumTextsLibrary;

            public enum ParcelStatus
            {
                [Localized(en: "Picked", nl: "Gepickt")]
                Picked,

                [Localized(en: "Delivered", nl: "Bezorgd")]
                Delivered,
            }

            public static class LibraryReader
            {
                public static string Read(ParcelStatus status) => status.GetText();
            }
            """);
        var app = GeneratorTestHelper.EmitLibraryWithGenerators("EnumTextsApp", """
            using CodeChops.Idioma;
            using EnumTextsApp.Texts;
            using EnumTextsLibrary;

            [assembly: SupportedLanguages("en", "nl", "de")]

            namespace EnumTextsApp.Texts
            {
                public class ParcelStatusLocalizationEn : ILocalizationSet<ParcelStatus>, IParcelStatusLocalization
                {
                    public string Picked => "Being picked";
                    public string Delivered => "Delivered";
                }

                public class ParcelStatusLocalizationNl : ILocalizationSet<ParcelStatus>, IParcelStatusLocalization
                {
                    public string Picked => "Wordt gepickt";
                    public string Delivered => "Bezorgd";
                }

                public class ParcelStatusLocalizationDe : ILocalizationSet<ParcelStatus>, IParcelStatusLocalization
                {
                    public string Picked => "Wird kommissioniert";
                    public string Delivered => "Zugestellt";
                }
            }

            namespace EnumTextsApp.Pages
            {
                public static class AppReader
                {
                    public static string Read(ParcelStatus status) => status.GetText();
                }
            }
            """, MetadataReference.CreateFromImage(library));
        var context = new InMemoryLoadContext(new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["EnumTextsLibrary"] = library,
            ["EnumTextsApp"] = app,
        });

        // Act: the app's module initializers, as at its start.
        var appAssembly = context.LoadFromAssemblyName(new AssemblyName("EnumTextsApp"));
        RuntimeHelpers.RunModuleConstructor(appAssembly.ManifestModule.ModuleHandle);

        var libraryAssembly = context.LoadFromAssemblyName(new AssemblyName("EnumTextsLibrary"));
        var picked = Enum.ToObject(libraryAssembly.GetType("EnumTextsLibrary.ParcelStatus")!, 0);

        string Read(string reader, string culture)
        {
            var type = reader is "app" ? appAssembly.GetType("EnumTextsApp.Pages.AppReader")! : libraryAssembly.GetType("EnumTextsLibrary.LibraryReader")!;

            using (LocalizationRegistry.UseCulture(CultureInfo.GetCultureInfo(culture)))
                return (string)type.GetMethod("Read")!.Invoke(obj: null, [picked])!;
        }

        // Assert: the set's wording, also in Dutch, which the library has its own wording for, and also where the
        // library's own code asks for the text. Read in the languages of this test process (en, nl): the app is
        // compiled as a library here, so its languages don't become the process's.
        Read("app", "nl-NL").ShouldBe("Wordt gepickt");
        Read("app", "en-US").ShouldBe("Being picked");
        Read("library", "nl-NL").ShouldBe("Wordt gepickt");
        Read("library", "en-US").ShouldBe("Being picked");
    }
}
