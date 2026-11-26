using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using CodeChops.Idioma.Internals;
using CodeChops.Idioma.Tests.TestHelpers;

namespace CodeChops.Idioma.Tests;

// The tests of GeneratedCode.EnumTexts.cs: which set gives the values of an enum their texts. A plain comment, because
// the summary and the fixture are on the declaration in GeneratedCode.Tests.cs.
public sealed partial class GeneratedCodeTests
{
    #region Fixture
    /// <summary>
    /// An enum that only the tests of <see cref="GeneratedCode.RegisterEnumTexts{TEnum, TContract}"/> register texts for, because
    /// what they register stays for the process.
    /// </summary>
    private enum RegisteredStatus
    {
        Open,
        Closed,
    }

    /// <summary>
    /// An enum that nothing registers texts for.
    /// </summary>
    private enum UnregisteredStatus
    {
        Open,
    }

    /// <summary>
    /// An enum that only the test of the app's texts over a library's registers texts for.
    /// </summary>
    private enum OrderedStatus
    {
        Open,
    }

    /// <summary>
    /// An enum that only the test of two libraries registers texts for.
    /// </summary>
    private enum LibraryStatus
    {
        Open,
    }

    /// <summary>
    /// An enum that only the test of a library that loads after the app started registers texts for.
    /// </summary>
    private enum LaterStatus
    {
        Open,
    }

    /// <summary>
    /// A library with a set, which registers its sets when its code first runs, as one an app loads lazily does.
    /// </summary>
    private static byte[] LaterLibrary { get; } = GeneratorTestHelper.EmitLibraryWithGenerators("LaterLibrary", """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en")]

        namespace LaterLibrary;

        public class LaterLocalizationEn : ILocalizationSet, ILaterLocalization
        {
            public string Title => "Later";
        }
        """);
    #endregion

    [Test]
    public void GetEnumText_ShouldGiveTheTextOfTheSetRegisteredLastAndNullForAnEnumWithoutOne()
    {
        // Arrange: two sets of one enum, as a library and the app that uses it may both have.
        GeneratedCode.RegisterEnumTexts<RegisteredStatus, GeneratedCodeTests>(static value => $"first {value}");
        GeneratedCode.RegisterEnumTexts<RegisteredStatus, GeneratedCodeTests>(static value => value is RegisteredStatus.Open ? "Open" : "Gesloten");

        // Act
        var closed = GeneratedCode.GetEnumText(RegisteredStatus.Closed);
        var unregistered = GeneratedCode.GetEnumText(UnregisteredStatus.Open);

        // Assert
        closed.ShouldBe("Gesloten");
        unregistered.ShouldBeNull();
        Should.Throw<ArgumentNullException>(static () => GeneratedCode.RegisterEnumTexts<RegisteredStatus, GeneratedCodeTests>(null!));
    }

    [Test]
    public void GetEnumText_ShouldGiveTheTextOfTheAppWhicheverRegisteredItsTextsFirst()
    {
        // Arrange: this assembly is the app, as it registered the languages. The assembly of Uri stands for a library,
        // whose texts come later, as a library's may when it loads after the app started.
        GeneratedCode.RegisterEnumTexts<OrderedStatus, GeneratedCodeTests>(static _ => "of the app");
        GeneratedCode.RegisterEnumTexts<OrderedStatus, Uri>(static _ => "of a library");

        // Act
        var text = GeneratedCode.GetEnumText(OrderedStatus.Open);

        // Assert
        text.ShouldBe("of the app");
    }

    [Test]
    public void GetEnumText_OfTwoLibraries_ShouldGiveTheTextRegisteredLast()
    {
        // Arrange: the assemblies of Uri and of string stand for two libraries, neither of them the app.
        GeneratedCode.RegisterEnumTexts<LibraryStatus, Uri>(static _ => "of the first library");
        GeneratedCode.RegisterEnumTexts<LibraryStatus, string>(static _ => "of the second library");

        // Act
        var text = GeneratedCode.GetEnumText(LibraryStatus.Open);

        // Assert
        text.ShouldBe("of the second library");
    }

    [Test]
    // Alone, because the library's sets are registered with those of the whole app until its context unloads.
    [NotInParallel]
    public void GetEnumText_ShouldGiveTheTextOfTheAppOverThatOfALibraryThatLoadedLater()
    {
        // Arrange: the app's texts, and then those of a library that loaded and registered its sets after the app.
        GeneratedCode.RegisterEnumTexts<LaterStatus, GeneratedCodeTests>(static _ => "of the app");

        var context = new AssemblyLoadContext("LaterLibrary", isCollectible: true);
        var library = context.LoadFromStream(new MemoryStream(LaterLibrary));
        RuntimeHelpers.RunModuleConstructor(library.ManifestModule.ModuleHandle);

        var register = typeof(GeneratedCode).GetMethod(nameof(GeneratedCode.RegisterEnumTexts))!
            .MakeGenericMethod(typeof(LaterStatus), library.GetType("LaterLibrary.ILaterLocalization")!);
        register.Invoke(null, [(Func<LaterStatus, string>)(static _ => "of a library")]);

        try
        {
            // Act
            var text = GeneratedCode.GetEnumText(LaterStatus.Open);

            // Assert
            text.ShouldBe("of the app");
        }
        finally
        {
            context.Unload();
        }
    }
}
