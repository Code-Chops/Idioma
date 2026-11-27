using CodeChops.Idioma.Internals;

namespace CodeChops.Idioma.Tests;

// The tests of LocalizationRegistry.Registering.cs: the sets of the whole app with their two tiers, the app's
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
    public void Register_ShouldKeepInstancesOfOneTypeForDifferentLanguages()
    {
        // Arrange
        var german = new RegistryConfiguredLocalization("de");
        var french = new RegistryConfiguredLocalization("fr");

        // Act
        LocalizationRegistry.Register(german, french);

        // Assert
        var current = LocalizationRegistry.Current;
        current.AllSets.ShouldContain(german);
        current.AllSets.ShouldContain(french);
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void Register_TypeAndLanguageTwiceInOneCall_ShouldKeepTheLastOnce()
    {
        // Arrange
        var first = new RegistryRepeatedLocalization("de");
        var french = new RegistryRepeatedLocalization("fr");
        var last = new RegistryRepeatedLocalization("DE");

        // Act: also registered again afterwards, as a second service provider does.
        LocalizationRegistry.Register(first, french, last);
        LocalizationRegistry.Register(french);

        // Assert: one per language, whatever the letter case of its code, in the order of the last registrations.
        var sets = LocalizationRegistry.Sets.OfType<RegistryRepeatedLocalization>().ToArray();
        sets.ShouldBe([last, french]);
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void Register_AfterTheLanguageCodeOfARegisteredSetTurnedInvalid_ShouldKeepEveryTextWorking()
    {
        // Arrange
        var changing = new RegistryChangingLocalization();
        LocalizationRegistry.Register(changing);

        // Act: the next registration builds the registry again, and reads every set's code. Not a set of the class
        // the test above counts, which would be counted there when this test runs first.
        changing.LocalizationLanguageCode = "not a culture";
        LocalizationRegistry.Register(new RegistryConfiguredLocalization("it"));

        // Assert: the set stays under the language it registered with, so no text fails.
        var current = Should.NotThrow(static () => LocalizationRegistry.Current);
        current.AllSets.ShouldContain(changing);

        changing.LocalizationLanguageCode = "fr";
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
    public void Register_ShouldThrowAtRegistrationForSetWithoutLanguageCode()
    {
        // Arrange
        var invalid = new RegistryInvalidLocalization1();

        // Act & Assert: both tiers fail here, not at the first text rendered afterwards, and neither keeps the set.
        Should.Throw<InvalidOperationException>(() => LocalizationRegistry.Register(invalid));
        Should.Throw<InvalidOperationException>(() => LocalizationRegistry.RegisterLibrarySets(invalid));
        LocalizationRegistry.Sets.ShouldNotContain(invalid);
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void RegisterSets_ShouldCreateEachSetOnceWhenTheRegistryIsFirstRead()
    {
        // Arrange: the generated registrar runs in a module initializer, before Main, so it creates no set there.
        var before = RegistryLazyLocalizationNl.Created;

        // Act
        GeneratedCode.RegisterSets(asDefaults: true, (typeof(RegistryLazyLocalizationNl), static () => new RegistryLazyLocalizationNl()));
        var createdAtRegistration = RegistryLazyLocalizationNl.Created - before;

        // Building the registry files the set under its language without creating it.
        LocalizationRegistry.RegisteredLanguages.ShouldContain("nl", StringComparer.Ordinal);
        var createdWhenBuilt = RegistryLazyLocalizationNl.Created - before;

        var found = LocalizationRegistry.GetLocalizationSet<IRegistryLazyLocalization>(Culture("nl"));
        LocalizationRegistry.Register(new RegistryConfiguredLocalization("it"));
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
        GeneratedCode.RegisterSets(asDefaults: true, (typeof(RegistryRacedLocalizationNl), static () => new RegistryRacedLocalizationNl()));

        var dutch = Culture("nl");
        using var bothCreating = new Barrier(participantCount: 2);
        RegistryRacedLocalizationNl.BothCreating = bothCreating;

        // Act: the first text of its contract on two threads at once.
        ILocalizationSet?[] found;

        try
        {
            found = await Task.WhenAll(
                Task.Run(() => LocalizationRegistry.GetLocalizationSet(typeof(IRegistryRacedLocalization), dutch)),
                Task.Run(() => LocalizationRegistry.GetLocalizationSet(typeof(IRegistryRacedLocalization), dutch)));
        }
        finally
        {
            RegistryRacedLocalizationNl.BothCreating = null;
        }

        var later = LocalizationRegistry.GetLocalizationSet(typeof(IRegistryRacedLocalization), dutch);

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
    public void Register_OfSetWhoseLanguageCodeReadsTheRegistry_ShouldFileItUnderTheCodeItRegisteredWith()
    {
        // Arrange: a copy of Idioma of its own, because a stack that overflows ends the process. The set's code reads the
        // registry, which is built from that code.
        using var idioma = new TestHelpers.IsolatedIdioma();
        var context = idioma.CreateContext("SetReadingTheRegistry");

        try
        {
            var assembly = context.LoadFromStream(new MemoryStream(TestHelpers.GeneratorTestHelper.EmitLibrary("SetReadingTheRegistry", """
                using CodeChops.Idioma;

                namespace SetReadingTheRegistry;

                public interface IReadingLocalization : ILocalizationSet
                {
                    string Title { get; }
                }

                public sealed class ReadingLocalizationNl : IReadingLocalization
                {
                    public string LocalizationLanguageCode => LocalizationRegistry.RegisteredLanguages.Length >= 0 ? "nl" : "";

                    public string Title => "Titel";
                }
                """)));

            var set = assembly.GetType("SetReadingTheRegistry.ReadingLocalizationNl")!;
            var contract = assembly.GetType("SetReadingTheRegistry.IReadingLocalization")!;

            // Act: registered, and then looked up, which builds the registry from the set's code.
            idioma.Registry.GetMethods()
                .Single(static method => method is { Name: nameof(LocalizationRegistry.Register), IsGenericMethodDefinition: true })
                .MakeGenericMethod(set)
                .Invoke(null, null);

            var found = idioma.Registry.GetMethod(nameof(LocalizationRegistry.GetLocalizationSet), [typeof(Type), typeof(CultureInfo)])!
                .Invoke(null, [contract, Culture("nl")]);

            // Assert: under the language it registered with, where the build asked for the registry it was building
            // without end.
            found.ShouldNotBeNull().ShouldBeOfType(set);
        }
        finally
        {
            context.Unload();
        }
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void RegisterSets_BeforeTheSetIsDescribed_ShouldFileItUnderTheDescribedLanguage()
    {
        // Arrange: registered before its description, so it takes the language of its name, gb.
        GeneratedCode.RegisterSets(asDefaults: true, (typeof(RegistryLateDescribedLocalizationEnGb), static () => new RegistryLateDescribedLocalizationEnGb()));
        LocalizationRegistry.GetLocalizationSet<IRegistryLateDescribedLocalization>(Culture("en-GB")).ShouldBeNull();

        // Act
        GeneratedCode.DescribeSet<RegistryLateDescribedLocalizationEnGb>("en-GB");

        // Assert: filed under en-GB once the registry is built again, though it wasn't created to tell.
        LocalizationRegistry.GetLocalizationSet<IRegistryLateDescribedLocalization>(Culture("en-GB")).ShouldBeOfType<RegistryLateDescribedLocalizationEnGb>();
        LocalizationRegistry.GetLocalizationSet<IRegistryLateDescribedLocalization>(Culture("gb")).ShouldBeNull();
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
            asDefaults: true,
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
        GeneratedCode.RegisterSets(asDefaults: true, (typeof(RegistrySelfReadingLocalizationNl), static () => new RegistrySelfReadingLocalizationNl()));

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
        LocalizationRegistry.Register(new RegistryConfiguredLocalization("it"));
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
        LocalizationRegistry.RegisterLanguages(typeof(Uri).Assembly, "de", "fr");

        // Assert
        LocalizationRegistry.SupportedLanguages.ShouldBe(registered);
        LocalizationRegistry.DefaultLanguage.ShouldBe(registered[0]);
        LocalizationRegistry.ApplicationAssembly.ShouldBe(typeof(LocalizationRegistryTests).Assembly);
    }

    [Test]
    public void RegisterLanguages_ShouldThrowForLanguageThatIsNoCultureName()
    {
        // Arrange & Act & Assert: checked before the first-call rule, so also once the languages are registered.
        Should.Throw<ArgumentException>(() => LocalizationRegistry.RegisterLanguages(typeof(Uri).Assembly, " "));
        Should.Throw<ArgumentException>(() => LocalizationRegistry.RegisterLanguages(typeof(Uri).Assembly, "en", "nl", " "));
        Should.Throw<ArgumentException>(() => LocalizationRegistry.RegisterLanguages(typeof(Uri).Assembly, "nl_BE")).ParamName.ShouldBe("defaultLanguage");
        Should.Throw<ArgumentException>(() => LocalizationRegistry.RegisterLanguages(typeof(Uri).Assembly, "en", "nl_BE")).ParamName.ShouldBe("otherLanguages");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public async Task Register_WhileTextsAreRead_ShouldShowOnlyWordingOfRegisteredSets()
    {
        // Arrange: a registration builds the lookup tables anew for the next read, which every text of every user reads
        // without a lock, so reads and registrations run side by side. Two sets of one language and contract, which the
        // writer registers one after the other, so that either may be the last.
        // The text is read as a property of a generated class reads it, which keeps the set it found per registry.
        static string Read() => GeneratedCode.GetLocalizationSet<IRegistryConcurrentLocalization>(0, 1)?.Name
            ?? GeneratedCode.ResolveWording<IRegistryConcurrentLocalization, RegistryConcurrentLocalizationEn>(0, 1, "Name", static set => set.Name);

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

    [Test]
    [Arguments(3)]
    [Arguments(8)]
    [Arguments(9)]
    [Arguments(500)]
    public void SetDescription_Words_ShouldFindEveryOwnTextAndNoOther(int count)
    {
        // Arrange: in an order that isn't sorted, as a caller may hand the texts over. A few are told apart one by one,
        // and many are looked up in sorted order.
        var names = Enumerable.Range(0, count).Select(static i => $"Text{(i * 7919) % 1000:D3}").Distinct(StringComparer.Ordinal).ToArray();
        var description = new LocalizationRegistry.SetDescription(languageCode: null, [.. names]);

        // Act & Assert
        foreach (var name in names)
            description.Words(name).ShouldBeTrue(name);

        description.Words("Text").ShouldBeFalse();
        description.Words("Text0000").ShouldBeFalse();
        description.Words("Checkout").ShouldBeFalse();
        description.Words("").ShouldBeFalse();
    }
}
