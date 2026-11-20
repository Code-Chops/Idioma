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
        exception.Message.ShouldStartWith($"'{typeof(RegistryLazyLocalizationNl)}' threw when it was created, so no text can be shown.");
        exception.InnerException.ShouldBeSameAs(thrown);
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
        var text = LocalizableText.Create<IRegistryConcurrentLocalization>("Concurrent.Name", static set => set.Name, new RegistryConcurrentLocalizationEn());
        var first = new RegistryConcurrentFirstLocalizationNl();
        var second = new RegistryConcurrentSecondLocalizationNl();
        var dutch = Culture("nl");
        LocalizationRegistry.Register(first);
        LocalizationRegistry.Register(second);

        using var stop = new CancellationTokenSource();

        var writer = Task.Run(() =>
        {
            var registrations = 0;

            while (!stop.IsCancellationRequested)
            {
                LocalizationRegistry.Register(first);
                LocalizationRegistry.Register(second);
                LocalizationRegistry.RegisterDefaults(new RegistryConcurrentDefaultLocalizationNl());
                registrations++;
            }

            return registrations;
        });

        // Act
        var wrong = await Task.WhenAll(Enumerable.Range(start: 0, count: 4).Select(_ => Task.Run(() =>
        {
            var problems = new List<string>();

            for (var i = 0; i < 50_000; i++)
            {
                try
                {
                    var wording = text.ToString(dutch);
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
        var registrations = await writer;

        // Assert: nothing but the wording of a registered set, and a registry that follows the last registration once
        // the writes have ended. The writer must have run during the reads for the test to mean anything.
        wrong.SelectMany(static problems => problems).Distinct(StringComparer.Ordinal).ShouldBeEmpty();
        registrations.ShouldBeGreaterThan(0);

        LocalizationRegistry.Register(first);
        text.ToString(dutch).ShouldBe("Eerste");
        LocalizationRegistry.Register(second);
        text.ToString(dutch).ShouldBe("Tweede");
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
