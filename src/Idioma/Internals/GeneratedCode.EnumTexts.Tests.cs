using System.Reflection;
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
    // Alone, because the library's sets are registered with those of the whole app.
    [NotInParallel]
    public void GetEnumText_ShouldGiveTheTextOfTheAppOverThatOfALibraryThatLoadedLater()
    {
        // Arrange: the app's texts, and then those of a library that loaded and registered its sets after the app.
        GeneratedCode.RegisterEnumTexts<LaterStatus, GeneratedCodeTests>(static _ => "of the app");

        var library = new AssemblyLoadContext("LaterLibrary").LoadFromStream(new MemoryStream(LaterLibrary));
        RuntimeHelpers.RunModuleConstructor(library.ManifestModule.ModuleHandle);

        var register = typeof(GeneratedCode).GetMethod(nameof(GeneratedCode.RegisterEnumTexts))!
            .MakeGenericMethod(typeof(LaterStatus), library.GetType("LaterLibrary.ILaterLocalization")!);
        register.Invoke(null, [(Func<LaterStatus, string>)(static _ => "of a library")]);

        // Act
        var text = GeneratedCode.GetEnumText(LaterStatus.Open);

        // Assert
        text.ShouldBe("of the app");
    }

    [Test]
    // Alone, because the sets of the assembly with the set are registered with those of the whole app.
    [NotInParallel]
    public void GetText_OfMemberThatASetOfAnotherAssemblyHasNoTextOf_ShouldGiveTheTextOfItsAttribute()
    {
        // Arrange & Act: a set of a library's enum, built against a version of the library that a newer one replaces.
        var (closed, cancelled) = ReadStatusesWithSetOfOlderLibrary();

        // Assert: the set's text of a member it knows, and the [Localized] text of the member the newer version adds.
        // The set gave that member its name before, in every language.
        closed.ShouldBe("Dicht");
        cancelled.ShouldBe("Geannuleerd");
    }

    /// <summary>
    /// A library's enum whose members are marked <c>[Localized]</c>, in the version a set of it was built against.
    /// </summary>
    private const string OlderLibraryWithStatus = """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace LibraryWithStatus;

        public enum Status
        {
            [Localized(en: "Open", nl: "Open")]
            Open,

            [Localized(en: "Closed", nl: "Gesloten")]
            Closed,
        }

        public static class Texts
        {
            public static string Of(Status status) => status.GetText();
        }
        """;

    /// <summary>
    /// The newer version of <see cref="OlderLibraryWithStatus"/>, with a member more. It has the same name, so it
    /// stands in for the older one at run time.
    /// </summary>
    private const string NewerLibraryWithStatus = """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace LibraryWithStatus;

        public enum Status
        {
            [Localized(en: "Open", nl: "Open")]
            Open,

            [Localized(en: "Closed", nl: "Gesloten")]
            Closed,

            [Localized(en: "Cancelled", nl: "Geannuleerd")]
            Cancelled,
        }

        public static class Texts
        {
            public static string Of(Status status) => status.GetText();
        }
        """;

    /// <summary>
    /// A set of the library's enum in another assembly, which rewords one member.
    /// </summary>
    private const string SetOfStatus = """
        using CodeChops.Idioma;
        using LibraryWithStatus;

        [assembly: SupportedLanguages("en", "nl")]

        namespace SetOfStatus;

        public class StatusLocalizationEn : ILocalizationSet<Status>, IStatusLocalization
        {
            public string Open => "Open";
            public string Closed => "Shut";
        }

        public class StatusLocalizationNl : ILocalizationSet<Status>, IStatusLocalization
        {
            public string Open => "Open";
            public string Closed => "Dicht";
        }
        """;

    private static (string Closed, string Cancelled) ReadStatusesWithSetOfOlderLibrary()
    {
        var older = GeneratorTestHelper.CompileLibraryWithGenerators("LibraryWithStatus", OlderLibraryWithStatus);
        var newer = GeneratorTestHelper.EmitLibraryWithGenerators("LibraryWithStatus", NewerLibraryWithStatus);
        var setOfStatus = GeneratorTestHelper.EmitLibraryWithGenerators("SetOfStatus", SetOfStatus, older);

        var context = new ContextOfNewerLibrary();
        context.Library = context.LoadFromStream(new MemoryStream(newer));
        var sets = context.LoadFromStream(new MemoryStream(setOfStatus));

        // Registers as an assembly the app loads later does, as its code first runs.
        RuntimeHelpers.RunModuleConstructor(sets.ManifestModule.ModuleHandle);

        var of = context.Library.GetType("LibraryWithStatus.Texts")!.GetMethod("Of")!;
        var status = context.Library.GetType("LibraryWithStatus.Status")!;

        using (LocalizationRegistry.UseCulture(CultureInfo.GetCultureInfo("nl-NL")))
            return ((string)of.Invoke(null, [Enum.ToObject(status, 1)])!, (string)of.Invoke(null, [Enum.ToObject(status, 2)])!);
    }

    /// <summary>
    /// A context that gives the newer library to the set built against the older one, and takes every other assembly
    /// from the app, Idioma included.
    /// </summary>
    private sealed class ContextOfNewerLibrary() : AssemblyLoadContext("ContextOfNewerLibrary")
    {
        public Assembly? Library { get; set; }

        protected override Assembly? Load(AssemblyName assemblyName) =>
            String.Equals(assemblyName.Name, "LibraryWithStatus", StringComparison.Ordinal) ? this.Library : null;
    }
}
