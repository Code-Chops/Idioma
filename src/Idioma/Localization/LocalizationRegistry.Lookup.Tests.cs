namespace CodeChops.Idioma.Tests;

// The tests of LocalizationRegistry.Lookup.cs: how a culture or a language code resolves to the sets of a registry. A
// plain comment, because the summary and the fixture are on the declaration in LocalizationRegistry.Tests.cs.
public sealed partial class LocalizationRegistryTests
{
    [Test]
    public void Resolve_ForCultureOfSameName_ShouldGiveLookupKeptForThatName()
    {
        // Arrange: an app in English and Dutch, and two Dutch cultures that are separate objects, as an app may create
        // one per request.
        var registry = LocalizationRegistry.Create([new RegistryLocalizationEn(), new RegistryLocalizationNl()], "en", "nl");
        var dutch = new CultureInfo("nl-NL");
        var otherDutch = new CultureInfo("nl-NL");

        // Act
        var lookup = registry.Resolve(dutch);

        // Assert: worked out once per name, with the names to try and whether the app's languages serve the culture. A
        // German user gets English, the default app language.
        registry.Resolve(otherDutch).ShouldBeSameAs(lookup);
        lookup.Names.ShouldBe(["nl-NL", "nl"]);
        lookup.ShowsCulture.ShouldBeTrue();
        registry.Resolve(Culture("de-DE")).ShowsCulture.ShouldBeFalse();
    }

    [Test]
    public void Resolve_ForTwoCulturesOfSameNameInTurn_ShouldLookUpEachByNameOnce()
    {
        // Arrange: the lookup holds the culture object that asked first. A second object of the same name asks next, as
        // ASP.NET Core's options hold one of their own.
        var registry = LocalizationRegistry.Create([new RegistryLocalizationEn(), new RegistryLocalizationNl()], "en", "nl");
        var withoutDutch = LocalizationRegistry.Create([new RegistryLocalizationEn()]);
        var first = new RegistryCountingCulture("nl-NL");
        var other = new RegistryCountingCulture("nl-NL");

        var lookup = registry.Resolve(first);
        registry.Resolve(other).ShouldBeSameAs(lookup);
        var (firstReads, otherReads) = (first.NameReads, other.NameReads);

        // Act: both in turn, as a thread reads texts for either.
        for (var i = 0; i < 10; i++)
        {
            registry.Resolve(first).ShouldBeSameAs(lookup);
            registry.Resolve(other).ShouldBeSameAs(lookup);
        }

        // Assert: told by reference, so neither name was read again. Another registry still gives its own lookup.
        first.NameReads.ShouldBe(firstReads);
        other.NameReads.ShouldBe(otherReads);
        withoutDutch.Resolve(other).Registry.ShouldBeSameAs(withoutDutch);
        registry.Resolve(other).ShouldBeSameAs(lookup);
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void ResolvedSetsFind_ForTwoCulturesOfSameNameInTurn_ShouldLookUpByNameOnceUntilNextRegistration()
    {
        // Arrange: the sets a text of a generated class reads, in the registry of the whole app. Its lookup holds the
        // culture object that asked first, which is neither of these once another test asked for Dutch.
        var first = new RegistryCountingCulture("nl-NL");
        var other = new RegistryCountingCulture("nl-NL");

        var resolved = ResolvedSets<IRegistryLocalization>.Find(first, count: 1);
        var kept = resolved.Lookup.Culture!;
        ResolvedSets<IRegistryLocalization>.Find(other, count: 1).ShouldBeSameAs(resolved);
        var otherReads = other.NameReads;

        // Act: the lookup's own culture and the other object in turn.
        for (var i = 0; i < 10; i++)
        {
            ResolvedSets<IRegistryLocalization>.Find(kept, count: 1).ShouldBeSameAs(resolved);
            ResolvedSets<IRegistryLocalization>.Find(other, count: 1).ShouldBeSameAs(resolved);
        }

        var readsInTurn = other.NameReads - otherReads;

        LocalizationRegistry.Register(new RegistryConfiguredLocalization("it"));
        var afterRegistration = ResolvedSets<IRegistryLocalization>.Find(other, count: 1);

        // Assert: told by reference, so the name wasn't read again. A registration drops what the thread kept.
        readsInTurn.ShouldBe(0);
        afterRegistration.ShouldNotBeSameAs(resolved);
        afterRegistration.Lookup.Registry.ShouldBeSameAs(LocalizationRegistry.Current);
        other.NameReads.ShouldBeGreaterThan(otherReads);
    }

    [Test]
    public void Resolve_ShouldTakeSetsOfItsOwnRegistry()
    {
        // Arrange: one registry with a Dutch set, and one without.
        var withDutch = LocalizationRegistry.Create([new RegistryLocalizationEn(), new RegistryLocalizationNl()]);
        var withoutDutch = LocalizationRegistry.Create([new RegistryLocalizationEn()]);

        // Act & Assert: the same culture has a level of sets in one, and none in the other.
        withDutch.Resolve(Culture("nl-NL")).Levels.Length.ShouldBe(1);
        withoutDutch.Resolve(Culture("nl-NL")).Levels.ShouldBeEmpty();
        withDutch.ResolveLanguage("en").Names.ShouldBe(["en"]);
    }

    [Test]
    public void Resolve_AfterAnotherRegistryOnSameThread_ShouldGiveLookupOfItsOwnRegistry()
    {
        // Arrange: two registries, asked in turn for one culture object and one language code on this thread, which
        // keeps the lookup it resolved last.
        var withDutch = LocalizationRegistry.Create([new RegistryLocalizationEn(), new RegistryLocalizationNl()]);
        var withoutDutch = LocalizationRegistry.Create([new RegistryLocalizationEn()]);
        var dutch = new CultureInfo("nl-NL");
        const string language = "nl";

        // Act & Assert: each registry gives its own lookup, whichever was asked last.
        var lookup = withDutch.Resolve(dutch);
        lookup.Registry.ShouldBeSameAs(withDutch);
        withoutDutch.Resolve(dutch).Registry.ShouldBeSameAs(withoutDutch);
        withDutch.Resolve(dutch).ShouldBeSameAs(lookup);

        var languageLookup = withDutch.ResolveLanguage(language);
        languageLookup.Levels.Length.ShouldBe(1);
        withoutDutch.ResolveLanguage(language).Levels.ShouldBeEmpty();
        withDutch.ResolveLanguage(language).ShouldBeSameAs(languageLookup);
    }

    [Test]
    public void ResolveLanguage_OfCodeThatIsACultureName_ShouldKeepItsLookupApartFromTheReaders()
    {
        // Arrange: a reader's culture object of its own, as the options of ASP.NET Core hold one.
        var registry = LocalizationRegistry.Create([new RegistryLocalizationEn(), new RegistryLocalizationNl()], "en", "nl");
        var reader = new CultureInfo("en");

        // Act: the code first, as a text falling back to the default language resolves it.
        var language = registry.ResolveLanguage("en");
        var culture = registry.Resolve(reader);

        // Assert: the reader's lookup holds the reader's culture, which a thread compares by reference, and the code's
        // lookup holds the culture of its language. Both walk the same names.
        culture.ShouldNotBeSameAs(language);
        culture.Culture.ShouldBeSameAs(reader);
        culture.Language.ShouldBeNull();
        language.Culture.ShouldBe(CultureInfo.GetCultureInfo("en"));
        language.Language.ShouldBe("en");
        language.Names.ShouldBe(culture.Names);
    }

    [Test]
    public void Resolve_OnThreadsOfDifferentCultures_ShouldGiveEachItsOwnCulture()
    {
        // Arrange: one registry, and threads that each render for a user of another language, as a server's do.
        var registry = LocalizationRegistry.Create([new RegistryLocalizationEn(), new RegistryLocalizationNl()], "en", "nl");
        CultureInfo[] cultures = [new("en-US"), new("nl-NL"), new("en-GB"), new("de-DE")];

        // Act & Assert: every thread gets the lookup of the culture it asked for, also while the others ask for theirs.
        Parallel.For(fromInclusive: 0, toExclusive: 8, thread =>
        {
            var culture = cultures[thread % cultures.Length];

            for (var i = 0; i < 20_000; i++)
            {
                var lookup = registry.Resolve(culture);

                if (!ReferenceEquals(lookup.Culture, culture) || !String.Equals(lookup.Names[0], culture.Name, StringComparison.Ordinal))
                    throw new InvalidOperationException($"Asked for {culture.Name}, and got the lookup of {lookup.Culture?.Name}.");
            }
        });
    }
}
