using CodeChops.Idioma.Internals;

namespace CodeChops.Idioma.Tests;

// The tests of LocalizationRegistry.Registration.cs: the sets of the whole app with their two tiers, the app's
// languages, and what the build recorded about a set. A plain comment, because the summary and the fixture are on the
// declaration in LocalizationRegistry.Tests.cs.
public sealed partial class LocalizationRegistryTests
{
    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void Register_ShouldWinOverRegisterDefaultsWhicheverComesFirst()
    {
        // Arrange: an app's override registered before the library it rewords has loaded, which is the order module
        // initializers can run in.
        var appOverride = new RegistryTierOverrideLocalizationNl();
        var libraryDefault = new RegistryTierDefaultLocalizationNl();

        // Act
        LocalizationRegistry.Register(appOverride);
        LocalizationRegistry.RegisterDefaults(libraryDefault);

        // Assert
        var current = LocalizationRegistry.Current;
        current.Find<IRegistryTierLocalization>(Culture("nl-NL")).ShouldBeSameAs(appOverride);
        current.Sets.IndexOf(libraryDefault).ShouldBeLessThan(current.Sets.IndexOf(appOverride));
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
        LocalizationRegistry.Current.Find<IRegistryOrderLocalization>(Culture("nl")).ShouldBeSameAs(second);

        LocalizationRegistry.Register(firstAgain);
        var current = LocalizationRegistry.Current;
        current.Find<IRegistryOrderLocalization>(Culture("nl")).ShouldBeSameAs(firstAgain);
        current.Sets.Count(set => set is RegistryOrderFirstLocalizationNl).ShouldBe(1);
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
        current.Sets.ShouldContain(german);
        current.Sets.ShouldContain(french);
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
        var sets = LocalizationRegistry.Current.Sets.OfType<RegistryRepeatedLocalization>().ToArray();
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
        current.Sets.ShouldContain(changing);

        changing.LocalizationLanguageCode = "fr";
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void Register_WithTypeArgument_ShouldCreateAndRegisterSet()
    {
        // Arrange & Act
        LocalizationRegistry.Register<RegistryGenericLocalizationNl>();

        // Assert
        LocalizationRegistry.Current.Find<IRegistryGenericLocalization>(Culture("nl")).ShouldBeOfType<RegistryGenericLocalizationNl>();
    }

    [Test]
    public void Register_ShouldThrowAtRegistrationForSetWithoutLanguageCode()
    {
        // Arrange
        var invalid = new RegistryInvalidLocalization1();

        // Act & Assert: both tiers fail here, not at the first text rendered afterwards, and neither keeps the set.
        Should.Throw<InvalidOperationException>(() => LocalizationRegistry.Register(invalid));
        Should.Throw<InvalidOperationException>(() => LocalizationRegistry.RegisterDefaults(invalid));
        LocalizationRegistry.Current.Sets.ShouldNotContain(invalid);
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void RegisterSets_ShouldCreateEachSetOnceWhenTheRegistryIsFirstRead()
    {
        // Arrange: the generated registrar runs in a module initializer, before Main, so it creates no set there.
        var before = RegistryLazyLocalizationNl.Created;

        // Act
        Internals.GeneratedCode.RegisterSets(asDefaults: true, (typeof(RegistryLazyLocalizationNl), static () => new RegistryLazyLocalizationNl()));
        var createdAtRegistration = RegistryLazyLocalizationNl.Created - before;

        var found = LocalizationRegistry.Current.Find<IRegistryLazyLocalization>(Culture("nl"));
        LocalizationRegistry.Register(new RegistryConfiguredLocalization("it"));
        var foundAgain = LocalizationRegistry.Current.Find<IRegistryLazyLocalization>(Culture("nl"));

        // Assert: created at the first read, under the language of its name, and kept when the registry is built again.
        createdAtRegistration.ShouldBe(0);
        found.ShouldBeOfType<RegistryLazyLocalizationNl>();
        foundAgain.ShouldBeSameAs(found);
        (RegistryLazyLocalizationNl.Created - before).ShouldBe(1);
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
    [NotInParallel(nameof(LocalizationRegistry))]
    public void RegisterSets_WhoseConstructorThrowsOnce_ShouldFailOnlyTheTextsOfItsContractAndOnlyOnce()
    {
        // Arrange: recorded as the build records a set, so the registry files it under its language without creating it.
        RegistryFlakyLocalizationNl.FailuresLeft = 1;
        GeneratedCode.RecordSet<RegistryFlakyLocalizationNl>("nl");
        GeneratedCode.RegisterSets(
            asDefaults: true,
            (typeof(RegistryFlakyLocalizationNl), static () => new RegistryFlakyLocalizationNl()),
            (typeof(RegistrySteadyLocalizationNl), static () => new RegistrySteadyLocalizationNl()));

        var dutch = Culture("nl");

        // Act: the language an inline text shows, a set of another contract, and the set itself, twice.
        var inlineLanguage = InlineLanguage.IndexOfShownLanguage(["en", "nl"], LocalizationRegistry.Current.Resolve(dutch));
        var other = LocalizationRegistry.Current.Find<IRegistrySteadyLocalization>(dutch);
        var failure = Should.Throw<InvalidOperationException>(() => LocalizationRegistry.Current.Find<IRegistryFlakyLocalization>(dutch));
        var retried = LocalizationRegistry.Current.Find<IRegistryFlakyLocalization>(dutch);

        // Assert: only the texts of its contract fail, and the next one creates the set again.
        inlineLanguage.ShouldBe(1);
        other.ShouldBeOfType<RegistrySteadyLocalizationNl>();
        failure.Message.ShouldStartWith($"'{typeof(RegistryFlakyLocalizationNl)}' threw when it was created");
        failure.InnerException.ShouldBeOfType<TimeoutException>();
        retried.ShouldBeOfType<RegistryFlakyLocalizationNl>();
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void RegisterSets_WhoseConstructorThrowsBeforeItGivesItsLanguage_ShouldFailOnlyTheTextsOfItsContract()
    {
        // Arrange: a set that gives its language itself, which the registry creates when it's built, to file it. That
        // fails, and so does the first text of its contract.
        RegistryFlakyCodedLocalization.FailuresLeft = 2;
        GeneratedCode.RegisterSets(
            asDefaults: true,
            (typeof(RegistryFlakyCodedLocalization), static () => new RegistryFlakyCodedLocalization()),
            (typeof(RegistrySteadyLocalizationNl), static () => new RegistrySteadyLocalizationNl()));

        var dutch = Culture("nl");

        // Act: the second text of its contract creates it, and the registry files it for the texts after that.
        var other = LocalizationRegistry.Current.Find<IRegistrySteadyLocalization>(dutch);
        var failure = Should.Throw<InvalidOperationException>(() => LocalizationRegistry.Current.Find<IRegistryFlakyCodedLocalization>(dutch));
        _ = LocalizationRegistry.Current.Find<IRegistryFlakyCodedLocalization>(dutch);
        var filed = LocalizationRegistry.Current.Find<IRegistryFlakyCodedLocalization>(dutch);

        // Assert
        other.ShouldBeOfType<RegistrySteadyLocalizationNl>();
        failure.InnerException.ShouldBeOfType<TimeoutException>();
        filed.ShouldBeOfType<RegistryFlakyCodedLocalization>();
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void RegisterSets_WhoseFieldInitializerReadsATextOfItsOwnContract_ShouldSayWhatToDoAndCreateItOnceItDoesNot()
    {
        // Arrange: recorded as the build records a set, so the registry creates it at the first text of its contract.
        // The text needs the set, which reads the text again as it's created. That overflowed the stack.
        GeneratedCode.RecordSet<RegistrySelfReadingLocalizationNl>("nl");
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
    [NotInParallel(nameof(LocalizationRegistry))]
    public void RegisterSets_WhoseFieldInitializerReadsATextOfItsOwnContractBeforeItGivesItsLanguage_ShouldFailOnlyTheTextsOfItsContract()
    {
        // Arrange: a set that gives its language itself, which the registry creates when it's built, to file it. The
        // text the set reads builds the registry too, which created the set again and overflowed the stack.
        var dutch = Culture("nl");
        using var cultures = new TestHelpers.CulturesScope(dutch, dutch);
        ILocalizationSet? other;
        InvalidOperationException failure;

        // The set reads its text from before it's registered. Any test that reads a text builds the registry, and one
        // that runs next to this test could otherwise create the set, and so file it, before it reads.
        RegistrySelfReadingCodedLocalization.ReadsOwnText = true;

        try
        {
            GeneratedCode.RegisterSets(
                asDefaults: true,
                (typeof(RegistrySelfReadingCodedLocalization), static () => new RegistrySelfReadingCodedLocalization()),
                (typeof(RegistrySteadyLocalizationNl), static () => new RegistrySteadyLocalizationNl()));

            // Act
            other = Should.NotThrow(() => LocalizationRegistry.Current.Find<IRegistrySteadyLocalization>(dutch));
            failure = Should.Throw<InvalidOperationException>(static () => ReadBrand<IRegistrySelfReadingCodedLocalization, RegistrySelfReadingCodedLocalizationEn>());
        }
        finally
        {
            RegistrySelfReadingCodedLocalization.ReadsOwnText = false;
        }

        // The next text of its contract creates the set, and the registry files it for the texts after that.
        _ = ReadBrand<IRegistrySelfReadingCodedLocalization, RegistrySelfReadingCodedLocalizationEn>();
        var retried = ReadBrand<IRegistrySelfReadingCodedLocalization, RegistrySelfReadingCodedLocalizationEn>();

        // Assert
        other.ShouldBeOfType<RegistrySteadyLocalizationNl>();
        failure.Message.ShouldStartWith($"'{typeof(RegistrySelfReadingCodedLocalization)}' reads a text while it's being created");
        failure.InnerException.ShouldBeNull();
        retried.ShouldBe("Winkel");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void RegisterSets_WhoseFieldInitializerReadsATextOfAnotherContractBeforeItGivesItsLanguage_ShouldFileTheSetAtOnce()
    {
        // Arrange: the registry creates the set when it's built, and the text the set reads builds a registry too.
        // That one can't file the set, which is still being created, so it isn't the one every reader gets.
        LocalizationRegistry.Register(new RegistryReadLocalizationNl());
        GeneratedCode.RegisterSets(asDefaults: true, (typeof(RegistryReaderCodedLocalization), static () => new RegistryReaderCodedLocalization()));

        var dutch = Culture("nl");
        using var cultures = new TestHelpers.CulturesScope(dutch, dutch);

        // Act
        var found = LocalizationRegistry.Current.Find<IRegistryReaderLocalization>(dutch);

        // Assert: filed at the first read, with the text it read. That's the Dutch wording, unless a test beside this
        // one built the registry first, for an English reader.
        found.ShouldBeOfType<RegistryReaderCodedLocalization>().Brand.ShouldBeOneOf("Winkel", "Shop");
    }

    [Test]
    public void Register_ShouldThrowForNullSet()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentNullException>(() => LocalizationRegistry.Register([null!]));
        Should.Throw<ArgumentNullException>(() => LocalizationRegistry.RegisterDefaults([null!]));
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
        LocalizationRegistry.RegisterLanguages("de", "fr");

        // Assert
        LocalizationRegistry.SupportedLanguages.ShouldBe(registered);
        LocalizationRegistry.DefaultLanguage.ShouldBe(registered[0]);
    }

    [Test]
    public void RegisterLanguages_ShouldThrowForLanguageThatIsNoCultureName()
    {
        // Arrange & Act & Assert: checked before the first-call rule, so also once the languages are registered.
        Should.Throw<ArgumentException>(() => LocalizationRegistry.RegisterLanguages(" "));
        Should.Throw<ArgumentException>(() => LocalizationRegistry.RegisterLanguages("en", "nl", " "));
        Should.Throw<ArgumentException>(() => LocalizationRegistry.RegisterLanguages("nl_BE")).ParamName.ShouldBe("defaultLanguage");
        Should.Throw<ArgumentException>(() => LocalizationRegistry.RegisterLanguages("en", "nl_BE")).ParamName.ShouldBe("otherLanguages");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public async Task Register_WhileTextsAreRead_ShouldShowOnlyWordingOfRegisteredSets()
    {
        // Arrange: a registration builds the lookup tables anew for the next read, which every text of every user reads
        // without a lock, so reads and registrations run side by side. Two sets of one language and contract, which the
        // writer registers one after the other, so that either may be the last.
        // The text is read as a property of a generated class reads it, which keeps the set it found per registry.
        static string Read() => GeneratedCode.FindSet<IRegistryConcurrentLocalization>(0, 1)?.Name
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
                LocalizationRegistry.RegisterDefaults(new RegistryConcurrentDefaultLocalizationNl());
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
                    var set = LocalizationRegistry.Current.Find<IRegistryConcurrentLocalization>(dutch);

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
    public void RecordedSet_Words_ShouldFindEveryOwnTextAndNoOther(int count)
    {
        // Arrange: in an order that isn't sorted, as a caller may hand the texts over. A few are told apart one by one,
        // and many are looked up in sorted order.
        var names = Enumerable.Range(0, count).Select(static i => $"Text{(i * 7919) % 1000:D3}").Distinct(StringComparer.Ordinal).ToArray();
        var recorded = new LocalizationRegistry.RecordedSet(languageCode: null, [.. names]);

        // Act & Assert
        foreach (var name in names)
            recorded.Words(name).ShouldBeTrue(name);

        recorded.Words("Text").ShouldBeFalse();
        recorded.Words("Text0000").ShouldBeFalse();
        recorded.Words("Checkout").ShouldBeFalse();
        recorded.Words("").ShouldBeFalse();
    }
}
