using System.Runtime.Loader;
using CodeChops.Idioma.Internals;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeChops.Idioma.Tests;

// The tests of LocalizationRegistry.Registering.cs: the sets of the whole app with their tiers, the app's
// languages, and how the build described a set. A plain comment, because the summary and the fixture are on the
// declaration in LocalizationRegistry.Tests.cs.
public sealed partial class LocalizationRegistryTests
{
    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void Register_ShouldWinOverRegisterLibrarySetsWhicheverComesFirst()
    {
        // Arrange: an app's override registered before the library it rewords has loaded, which is the order module
        // initializers can run in.
        var appOverride = new RegistryTierOverrideLocalizationNl();
        var libraryDefault = new RegistryTierDefaultLocalizationNl();

        // Act
        LocalizationRegistry.Register(appOverride);
        LocalizationRegistry.RegisterLibrarySets(libraryDefault);

        // Assert
        var current = LocalizationRegistry.Current;
        current.GetByCulture<IRegistryTierLocalization>(Culture("nl-NL")).ShouldBeSameAs(appOverride);
        current.AllSets.IndexOf(libraryDefault).ShouldBeLessThan(current.AllSets.IndexOf(appOverride));
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void RegisterSets_AppRewordingShouldWinOverLibraryRewordingWhicheverRegistersFirst_AndRegisterOverBoth()
    {
        // Arrange: a library's rewording, from an assembly of its own, next to the app's. The app's registers first, as
        // the app's registrar runs before that of a library it loads later.
        var library = new AssemblyLoadContext("RewordingLibrary").LoadFromStream(new MemoryStream(EmitRewordingLibrary()))
            .GetType("RewordingLibrary.LibraryRegistryPrecedenceLocalizationNl", throwOnError: true)!;

        static string Name() => LocalizationRegistry.GetLocalizationSet<IRegistryPrecedenceLocalization>(Culture("nl"))!.Name;

        // Act & Assert: the app's rewording wins, also once the library's registers again.
        GeneratedCode.RegisterSets(SetTier.LibrarySets, (typeof(RegistryPrecedenceLocalizationNl), static () => new RegistryPrecedenceLocalizationNl()));
        GeneratedCode.RegisterSets(SetTier.ApplicationRewordings, (typeof(AppRegistryPrecedenceLocalizationNl), static () => new AppRegistryPrecedenceLocalizationNl()));
        GeneratedCode.RegisterSets(SetTier.Rewordings, (library, () => (ILocalizationSet)Activator.CreateInstance(library)!));
        Name().ShouldBe("App");

        // A set registered by hand wins over both, also over a rewording registered after it.
        LocalizationRegistry.Register(new HandRegistryPrecedenceLocalizationNl());
        GeneratedCode.RegisterSets(SetTier.Rewordings, (library, () => (ILocalizationSet)Activator.CreateInstance(library)!));
        Name().ShouldBe("Hand");
    }

    /// <summary>
    /// The image of a library whose set rewords <see cref="RegistryPrecedenceLocalizationNl"/>, compiled against this
    /// test assembly.
    /// </summary>
    private static byte[] EmitRewordingLibrary()
    {
        var compilation = CSharpCompilation.Create(
            "RewordingLibrary",
            [CSharpSyntaxTree.ParseText("""
                namespace RewordingLibrary;

                public sealed class LibraryRegistryPrecedenceLocalizationNl : CodeChops.Idioma.Tests.LocalizationRegistryTests.RegistryPrecedenceLocalizationNl
                {
                    public override string Name => "Library rewording";
                }
                """)],
            [.. TestHelpers.GeneratorTestHelper.References, MetadataReference.CreateFromFile(typeof(LocalizationRegistryTests).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        compilation.Emit(stream).Success.ShouldBeTrue();

        return stream.ToArray();
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void Register_ShouldLetLastRegistrationWinAndReplaceSameType()
    {
        // Arrange
        var first = new RegistryOrderFirstLocalizationNl();
        var second = new RegistryOrderSecondLocalizationNl();
        var firstAgain = new RegistryOrderFirstLocalizationNl();

        // Act & Assert: the later set wins; registering a type again replaces its instance and makes it the last.
        LocalizationRegistry.Register(first, second);
        LocalizationRegistry.GetLocalizationSet<IRegistryOrderLocalization>(Culture("nl")).ShouldBeSameAs(second);

        LocalizationRegistry.Register(firstAgain);
        var current = LocalizationRegistry.Current;
        current.GetByCulture<IRegistryOrderLocalization>(Culture("nl")).ShouldBeSameAs(firstAgain);
        current.AllSets.Count(set => set is RegistryOrderFirstLocalizationNl).ShouldBe(1);
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void Register_TypeAndLanguageTwiceInOneCall_ShouldKeepTheLastOnce()
    {
        // Arrange
        var first = new RegistryRepeatedLocalizationDe();
        var french = new RegistryRepeatedLocalizationFr();
        var last = new RegistryRepeatedLocalizationDe();

        // Act: also registered again afterwards, as a second service provider does.
        LocalizationRegistry.Register(first, french, last);
        LocalizationRegistry.Register(french);

        // Assert: one per type and language, in the order of the last registrations.
        var sets = LocalizationRegistry.Sets.Where(static set => set is RegistryRepeatedLocalizationDe or RegistryRepeatedLocalizationFr).ToArray();
        sets.ShouldBe([last, french]);
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void Register_WithTypeArgument_ShouldCreateAndRegisterSet()
    {
        // Arrange & Act
        LocalizationRegistry.Register<RegistryGenericLocalizationNl>();

        // Assert
        LocalizationRegistry.GetLocalizationSet<IRegistryGenericLocalization>(Culture("nl")).ShouldBeOfType<RegistryGenericLocalizationNl>();
    }

    [Test]
    // Apart from the tests that count when a set is created, because reading the sets of the app creates every set.
    [NotInParallel(nameof(LocalizationRegistry))]
    public void Register_ShouldThrowAtRegistrationForSetWithoutLanguage()
    {
        // Arrange
        var invalid = new RegistryInvalidLocalization1();

        // Act & Assert: both tiers fail here, not at the first text rendered afterwards, and neither keeps the set.
        var exception = Should.Throw<InvalidOperationException>(() => LocalizationRegistry.Register(invalid));
        Should.Throw<InvalidOperationException>(() => LocalizationRegistry.RegisterLibrarySets(invalid));
        LocalizationRegistry.Sets.ShouldNotContain(invalid);

        // The message names the set and how to give it a language.
        exception.Message.ShouldContain(nameof(RegistryInvalidLocalization1));
        exception.Message.ShouldContain("ShopLocalizationDe");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void Register_OfNestedSet_ShouldFileItUnderTheLanguageOfItsName()
    {
        // Arrange: a nested set the build doesn't describe.
        var italian = new RegistryLocalizationIt();

        // Act
        LocalizationRegistry.Register(italian);

        // Assert
        LocalizationRegistry.RegisteredLanguages.ShouldContain("it", StringComparer.Ordinal);
        LocalizationRegistry.GetLocalizationSet<ILocalizationSet>(Culture("it-IT")).ShouldBeSameAs(italian);
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void RegisterSets_ShouldCreateEachSetOnceWhenTheRegistryIsFirstRead()
    {
        // Arrange: the generated registrar runs in a module initializer, before Main, so it creates no set there.
        var before = RegistryLazyLocalizationNl.Created;

        // Act
        GeneratedCode.RegisterSets(SetTier.LibrarySets, (typeof(RegistryLazyLocalizationNl), static () => new RegistryLazyLocalizationNl()));
        var createdAtRegistration = RegistryLazyLocalizationNl.Created - before;

        // Building the registry files the set under its language without creating it.
        LocalizationRegistry.RegisteredLanguages.ShouldContain("nl", StringComparer.Ordinal);
        var createdWhenBuilt = RegistryLazyLocalizationNl.Created - before;

        var found = LocalizationRegistry.GetLocalizationSet<IRegistryLazyLocalization>(Culture("nl"));
        LocalizationRegistry.Register(new RegistryLocalizationIt());
        var foundAgain = LocalizationRegistry.GetLocalizationSet<IRegistryLazyLocalization>(Culture("nl"));

        // Assert: created at the first text of its contract, under the language of its name, and kept when the
        // registry is built again.
        createdAtRegistration.ShouldBe(0);
        createdWhenBuilt.ShouldBe(0);
        found.ShouldBeOfType<RegistryLazyLocalizationNl>();
        foundAgain.ShouldBeSameAs(found);
        (RegistryLazyLocalizationNl.Created - before).ShouldBe(1);
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public async Task RegisterSets_CreatedByTwoThreadsAtOnce_ShouldGiveEveryCallerOneSet()
    {
        // Arrange: described as the build describes a set, so the registry files it under its language without creating
        // it. Its constructor waits until a second thread creates it too.
        GeneratedCode.DescribeSet<RegistryRacedLocalizationNl>("nl");
        GeneratedCode.RegisterSets(SetTier.LibrarySets, (typeof(RegistryRacedLocalizationNl), static () => new RegistryRacedLocalizationNl()));

        var dutch = Culture("nl");
        using var bothCreating = new Barrier(participantCount: 2);
        RegistryRacedLocalizationNl.BothCreating = bothCreating;

        // Act: the first text of its contract on two threads at once.
        ILocalizationSet?[] found;

        try
        {
            found = await Task.WhenAll(
                Task.Run(() => LocalizationRegistry.GetLocalizationSet<IRegistryRacedLocalization>(dutch)),
                Task.Run(() => LocalizationRegistry.GetLocalizationSet<IRegistryRacedLocalization>(dutch)));
        }
        finally
        {
            RegistryRacedLocalizationNl.BothCreating = null;
        }

        var later = LocalizationRegistry.GetLocalizationSet<IRegistryRacedLocalization>(dutch);

        // Assert: both created it, and the set of the one that finished first is every caller's.
        RegistryRacedLocalizationNl.Created.ShouldBe(2);
        found[0].ShouldNotBeNull().ShouldBeSameAs(found[1]);
        later.ShouldBeSameAs(found[0]);
    }

    [Test]
    public async Task GetDefaultSet_CreatedByTwoThreadsAtOnce_ShouldGiveEveryTextOneSet()
    {
        // Arrange: a set of a default language whose constructor waits until a second thread creates it too.
        using var bothCreating = new Barrier(participantCount: 2);
        RegistryRacedDefaultLocalizationEn.BothCreating = bothCreating;

        // Act: the first text that needs it, on two threads at once.
        RegistryRacedDefaultLocalizationEn[] created;

        try
        {
            created = await Task.WhenAll(
                Task.Run(GeneratedCode.GetDefaultSet<RegistryRacedDefaultLocalizationEn>),
                Task.Run(GeneratedCode.GetDefaultSet<RegistryRacedDefaultLocalizationEn>));
        }
        finally
        {
            RegistryRacedDefaultLocalizationEn.BothCreating = null;
        }

        // Assert: both created it, and the set of the one that finished first is every text's.
        RegistryRacedDefaultLocalizationEn.Created.ShouldBe(2);
        created[0].ShouldBeSameAs(created[1]);
        GeneratedCode.GetDefaultSet<RegistryRacedDefaultLocalizationEn>().ShouldBeSameAs(created[0]);
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void RegisterSets_BeforeTheSetIsDescribed_ShouldFileItUnderTheDescribedLanguage()
    {
        // Arrange: registered before its description, so it takes the language of the set it derives from, en.
        GeneratedCode.DescribeSet<RegistryLateDescribedBase>("en");
        GeneratedCode.RegisterSets(SetTier.LibrarySets, (typeof(RegistryLateDescribedLocalizationEnGb), static () => new RegistryLateDescribedLocalizationEnGb()));
        LocalizationRegistry.GetLocalizationSet<IRegistryLateDescribedLocalization>(Culture("en")).ShouldBeOfType<RegistryLateDescribedLocalizationEnGb>();

        // Act
        GeneratedCode.DescribeSet<RegistryLateDescribedLocalizationEnGb>("en-GB");

        // Assert: filed under en-GB once the registry is built again, though it wasn't created to tell.
        LocalizationRegistry.GetLocalizationSet<IRegistryLateDescribedLocalization>(Culture("en-GB")).ShouldBeOfType<RegistryLateDescribedLocalizationEnGb>();
        LocalizationRegistry.GetLocalizationSet<IRegistryLateDescribedLocalization>(Culture("en")).ShouldBeNull();
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void Register_WithoutSets_ShouldKeepTheRegistry()
    {
        // Arrange
        var before = LocalizationRegistry.Current;

        // Act
        LocalizationRegistry.Register();
        LocalizationRegistry.RegisterLibrarySets();

        // Assert: a new registry would drop every lookup and set the old one kept.
        LocalizationRegistry.Current.ShouldBeSameAs(before);
    }

    [Test]
    public void CreateSet_WhoseConstructorThrows_ShouldSayWhichSetThrew()
    {
        // Arrange
        var thrown = new FormatException("No such setting.");

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => LocalizationRegistry.CreateSet(typeof(RegistryLazyLocalizationNl), () => throw thrown));

        // Assert
        exception.Message.ShouldStartWith($"'{typeof(RegistryLazyLocalizationNl)}' threw when it was created, so the texts of its contract can't be shown.");
        exception.InnerException.ShouldBeSameAs(thrown);
    }

    [Test]
    // Alone, because any test that reads the sets of the app creates this set too, and would take its one failure.
    [NotInParallel]
    public void RegisterSets_WhoseConstructorThrowsOnce_ShouldFailOnlyTheTextsOfItsContractAndOnlyOnce()
    {
        // Arrange: described as the build describes a set, so the registry files it under its language without creating it.
        RegistryFlakyLocalizationNl.FailuresLeft = 1;
        GeneratedCode.DescribeSet<RegistryFlakyLocalizationNl>("nl");
        GeneratedCode.RegisterSets(
            SetTier.LibrarySets,
            (typeof(RegistryFlakyLocalizationNl), static () => new RegistryFlakyLocalizationNl()),
            (typeof(RegistrySteadyLocalizationNl), static () => new RegistrySteadyLocalizationNl()));

        var dutch = Culture("nl");

        // Act: the language an inline text shows, a set of another contract, and the set itself, twice.
        var standaloneLanguage = StandaloneLanguage.IndexOfShownLanguage(["en", "nl"], LocalizationRegistry.Current.Resolve(dutch));
        var other = LocalizationRegistry.GetLocalizationSet<IRegistrySteadyLocalization>(dutch);
        var failure = Should.Throw<InvalidOperationException>(() => LocalizationRegistry.GetLocalizationSet<IRegistryFlakyLocalization>(dutch));
        var retried = LocalizationRegistry.GetLocalizationSet<IRegistryFlakyLocalization>(dutch);

        // Assert: only the texts of its contract fail, and the next one creates the set again.
        standaloneLanguage.ShouldBe(1);
        other.ShouldBeOfType<RegistrySteadyLocalizationNl>();
        failure.Message.ShouldStartWith($"'{typeof(RegistryFlakyLocalizationNl)}' threw when it was created");
        failure.InnerException.ShouldBeOfType<TimeoutException>();
        retried.ShouldBeOfType<RegistryFlakyLocalizationNl>();
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void RegisterSets_WhoseFieldInitializerReadsATextOfItsOwnContract_ShouldSayWhatToDoAndCreateItOnceItDoesNot()
    {
        // Arrange: described as the build describes a set, so the registry creates it at the first text of its contract.
        // The text needs the set, which reads the text again as it's created. That overflowed the stack.
        GeneratedCode.DescribeSet<RegistrySelfReadingLocalizationNl>("nl");
        GeneratedCode.RegisterSets(SetTier.LibrarySets, (typeof(RegistrySelfReadingLocalizationNl), static () => new RegistrySelfReadingLocalizationNl()));

        using var cultures = new TestHelpers.CulturesScope("nl");
        InvalidOperationException failure;
        InvalidOperationException failureAgain;

        // Act
        RegistrySelfReadingLocalizationNl.ReadsOwnText = true;

        try
        {
            failure = Should.Throw<InvalidOperationException>(static () => ReadBrand<IRegistrySelfReadingLocalization, RegistrySelfReadingLocalizationEn>());
            failureAgain = Should.Throw<InvalidOperationException>(static () => ReadBrand<IRegistrySelfReadingLocalization, RegistrySelfReadingLocalizationEn>());
        }
        finally
        {
            RegistrySelfReadingLocalizationNl.ReadsOwnText = false;
        }

        var retried = ReadBrand<IRegistrySelfReadingLocalization, RegistrySelfReadingLocalizationEn>();

        // Assert: the exception names the set and isn't wrapped, every text of the contract says the same, and the
        // next one creates the set again.
        failure.Message.ShouldBe(
            $"'{typeof(RegistrySelfReadingLocalizationNl)}' reads a text while it's being created, and that text needs '{typeof(RegistrySelfReadingLocalizationNl)}' itself. "
            + "Read the text in a member with =>, not in the constructor or a field initializer. For example: public string Title => ShopLocalization.Brand;");
        failure.InnerException.ShouldBeNull();
        failureAgain.Message.ShouldBe(failure.Message);
        retried.ShouldBe("Winkel");
    }

    [Test]
    public void GetDefaultSet_WhoseFieldInitializerReadsATextOfItsOwnContract_ShouldSayWhatToDo()
    {
        // Arrange: no set of the contract is registered, so the text needs the set it was declared with.
        using var cultures = new TestHelpers.CulturesScope("en");
        InvalidOperationException failure;

        // Act
        RegistrySelfReadingDefaultLocalizationEn.ReadsOwnText = true;

        try
        {
            failure = Should.Throw<InvalidOperationException>(static () => ReadBrand<IRegistrySelfReadingDefaultLocalization, RegistrySelfReadingDefaultLocalizationEn>());
        }
        finally
        {
            RegistrySelfReadingDefaultLocalizationEn.ReadsOwnText = false;
        }

        var retried = ReadBrand<IRegistrySelfReadingDefaultLocalization, RegistrySelfReadingDefaultLocalizationEn>();

        // Assert
        failure.Message.ShouldStartWith($"'{typeof(RegistrySelfReadingDefaultLocalizationEn)}' reads a text while it's being created");
        retried.ShouldBe("Shop");
    }

    [Test]
    public void Register_ShouldThrowForNullSet()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentNullException>(() => LocalizationRegistry.Register([null!]));
        Should.Throw<ArgumentNullException>(() => LocalizationRegistry.RegisterLibrarySets([null!]));
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void Current_ShouldStayTheSameInstanceUntilNextRegistration()
    {
        // Arrange
        var before = LocalizationRegistry.Current;

        // Act
        var unchanged = LocalizationRegistry.Current;
        LocalizationRegistry.Register(new RegistryLocalizationIt());
        var changed = LocalizationRegistry.Current;

        // Assert: the lookup tables are built once per change, since every text reads this on every render.
        unchanged.ShouldBeSameAs(before);
        changed.ShouldNotBeSameAs(before);
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void RegisterLanguages_ShouldKeepFirstRegistration()
    {
        // Arrange: this test project's own registrar registered its languages when it started.
        var registered = LocalizationRegistry.SupportedLanguages;
        registered.ShouldNotBeEmpty();

        // Act: what the registrar of an application this one references would do afterwards.
        LocalizationRegistry.RegisterLanguages(typeof(Uri).Assembly, ["de", "zh-TW"], [null, "Hant"]);

        // Assert
        LocalizationRegistry.SupportedLanguages.ShouldBe(registered);
        LocalizationRegistry.DefaultLanguage.ShouldBe(registered[0]);
        LocalizationRegistry.ApplicationAssembly.ShouldBe(typeof(LocalizationRegistryTests).Assembly);
        LocalizationRegistry.Current.AppScripts.ShouldNotContainKey("zh-TW");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public async Task Register_WhileTextsAreRead_ShouldShowOnlyWordingOfRegisteredSets()
    {
        // Arrange: a registration builds the lookup tables anew for the next read, which every text of every user reads
        // without a lock, so reads and registrations run side by side. Two sets of one language and contract, which the
        // writer registers one after the other, so that either may be the last.
        // The text is read as a property of a generated class reads it, which keeps the set it found per registry.
        static string Read() => GeneratedCode.GetLocalizationSet<IRegistryConcurrentLocalization>()?.Name
            ?? GeneratedCode.ResolveWording<IRegistryConcurrentLocalization, RegistryConcurrentLocalizationEn>("Name", static set => set.Name);

        var first = new RegistryConcurrentFirstLocalizationNl();
        var second = new RegistryConcurrentSecondLocalizationNl();
        var dutch = Culture("nl");
        LocalizationRegistry.Register(first);
        LocalizationRegistry.Register(second);

        using var stop = new CancellationTokenSource();
        var writing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // The writer must run during the reads for the test to mean anything. So it has a thread of its own, and the
        // reads start once it has registered. Taken from the thread pool, it could start only after the reads on a
        // machine with few cores, such as a runner of CI.
        var writer = Task.Factory.StartNew(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                LocalizationRegistry.Register(first);
                LocalizationRegistry.Register(second);
                LocalizationRegistry.RegisterLibrarySets(new RegistryConcurrentDefaultLocalizationNl());
                writing.TrySetResult();
            }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        // A writer that throws before its first registration ends the wait too, and the await below throws it.
        await Task.WhenAny(writing.Task, writer);

        // Act
        var wrong = await Task.WhenAll(Enumerable.Range(start: 0, count: 4).Select(_ => Task.Run(() =>
        {
            CultureInfo.CurrentUICulture = dutch;
            var problems = new List<string>();

            for (var i = 0; i < 50_000; i++)
            {
                try
                {
                    var wording = Read();
                    var set = LocalizationRegistry.GetLocalizationSet<IRegistryConcurrentLocalization>(dutch);

                    if (wording is not ("Eerste" or "Tweede"))
                        problems.Add($"wording '{wording}'");

                    if (set is null)
                        problems.Add("no set found");
                }
                catch (Exception exception)
                {
                    problems.Add($"{exception.GetType().Name}: {exception.Message}");
                }
            }

            return problems;
        })));

        await stop.CancelAsync();
        await writer;

        // Assert: nothing but the wording of a registered set, and a registry that follows the last registration once
        // the writes have ended.
        wrong.SelectMany(static problems => problems).Distinct(StringComparer.Ordinal).ShouldBeEmpty();

        using var cultures = new TestHelpers.CulturesScope(dutch, dutch);
        LocalizationRegistry.Register(first);
        Read().ShouldBe("Eerste");
        LocalizationRegistry.Register(second);
        Read().ShouldBe("Tweede");
    }
}
