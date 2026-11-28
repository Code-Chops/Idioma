namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="LocalizationRegistry"/>: a registry holding exactly the given sets, lookup by contract with the
/// culture walk, and the sets of the whole app with their tiers. The sets of the whole app are process-wide, so
/// every test registering there uses a contract of its own, and runs apart from the test that compares
/// <see cref="LocalizationRegistry.Current"/> by reference.
/// </summary>
public sealed partial class LocalizationRegistryTests
{
    #region Fixture
    private interface IRegistryLocalization : ILocalizationSet;

    internal sealed class RegistryLocalizationEn : IRegistryLocalization;

    internal sealed class RegistryLocalizationNl : IRegistryLocalization;

    internal sealed class RegistryReplacementLocalizationNl : IRegistryLocalization;

    /// <summary>
    /// A contract no set in <see cref="RegistryLocalizationNl"/>'s family implements.
    /// </summary>
    private interface IRegistryOtherLocalization : ILocalizationSet;

    /// <summary>
    /// A set serving two contracts at once.
    /// </summary>
    internal sealed class RegistryCombinedLocalizationNl : IRegistryLocalization, IRegistryOtherLocalization;

    private interface IRegistryTierLocalization : ILocalizationSet;

    internal sealed class RegistryTierDefaultLocalizationNl : IRegistryTierLocalization;

    internal sealed class RegistryTierOverrideLocalizationNl : IRegistryTierLocalization;

    private interface IRegistryLazyLocalization : ILocalizationSet;

    /// <summary>
    /// A set that counts how often it's created, to show when the registry creates a set it registered by its type.
    /// </summary>
    internal sealed class RegistryLazyLocalizationNl : IRegistryLazyLocalization
    {
        public static int Created;

        public RegistryLazyLocalizationNl() => Interlocked.Increment(ref Created);
    }

    private interface IRegistryLateDescribedLocalization : ILocalizationSet;

    /// <summary>
    /// A set the test describes as <c>en</c>, and a set deriving from it that registers under <c>en</c> until the test
    /// describes it as <c>en-GB</c>.
    /// </summary>
    private class RegistryLateDescribedBase : IRegistryLateDescribedLocalization;

    private sealed class RegistryLateDescribedLocalizationEnGb : RegistryLateDescribedBase;

    private interface IRegistrySteadyLocalization : ILocalizationSet;

    internal sealed class RegistrySteadyLocalizationNl : IRegistrySteadyLocalization;

    private interface IRegistryFlakyLocalization : ILocalizationSet;

    /// <summary>
    /// A set whose constructor throws as often as <see cref="FailuresLeft"/> says, as one that reads a setting that isn't
    /// loaded yet at the start of an app.
    /// </summary>
    internal sealed class RegistryFlakyLocalizationNl : IRegistryFlakyLocalization
    {
        public static int FailuresLeft;

        public RegistryFlakyLocalizationNl()
        {
            if (Interlocked.Decrement(ref FailuresLeft) >= 0)
                throw new TimeoutException("The settings aren't loaded yet.");
        }
    }

    private interface IRegistryRacedLocalization : ILocalizationSet;

    /// <summary>
    /// A set whose constructor waits until a second thread creates it too, while <see cref="BothCreating"/> is set. So
    /// two threads create it at once.
    /// </summary>
    internal sealed class RegistryRacedLocalizationNl : IRegistryRacedLocalization
    {
        private static int _created;

        public static Barrier? BothCreating { get; set; }

        public static int Created => Volatile.Read(ref _created);

        public RegistryRacedLocalizationNl()
        {
            Interlocked.Increment(ref _created);
            BothCreating?.SignalAndWait(TimeSpan.FromSeconds(10));
        }
    }

    /// <summary>
    /// A set of a default language, which a text creates itself (<c>GetDefaultSet</c>). As
    /// <see cref="RegistryRacedLocalizationNl"/> does, its constructor waits until a second thread creates it too, while
    /// <see cref="BothCreating"/> is set.
    /// </summary>
    internal sealed class RegistryRacedDefaultLocalizationEn : IRegistryRacedLocalization
    {
        private static int _created;

        public static Barrier? BothCreating { get; set; }

        public static int Created => Volatile.Read(ref _created);

        public RegistryRacedDefaultLocalizationEn()
        {
            Interlocked.Increment(ref _created);
            BothCreating?.SignalAndWait(TimeSpan.FromSeconds(10));
        }
    }

    /// <summary>
    /// The one text of the contracts below, which a set reads while it's created.
    /// </summary>
    private interface IRegistryBrandTexts
    {
        string Brand { get; }
    }

    /// <summary>
    /// Reads the text as a property of a generated class reads it.
    /// </summary>
    private static string ReadBrand<TContract, TDefaultSet>()
        where TContract : class, IRegistryBrandTexts
        where TDefaultSet : class, ILocalizationSet, TContract, new()
    {
        var brand = Internals.GeneratedCode.GetLocalizationSet<TContract>()?.Brand
            ?? Internals.GeneratedCode.ResolveWording<TContract, TDefaultSet>("Brand", static set => set.Brand);

        return brand;
    }

    private interface IRegistrySelfReadingLocalization : ILocalizationSet, IRegistryBrandTexts;

    /// <summary>
    /// The set the text takes its default wording from. Never registered.
    /// </summary>
    internal sealed class RegistrySelfReadingLocalizationEn : IRegistrySelfReadingLocalization
    {
        public string Brand => "Shop";
    }

    /// <summary>
    /// A set with a field initializer that reads a text of its own contract, on the thread that sets
    /// <see cref="ReadsOwnText"/>. Per thread, so that a test running beside it creates the set without failing.
    /// </summary>
    internal sealed class RegistrySelfReadingLocalizationNl : IRegistrySelfReadingLocalization
    {
        [ThreadStatic]
        public static bool ReadsOwnText;

        private readonly string _brand = ReadsOwnText ? ReadBrand<IRegistrySelfReadingLocalization, RegistrySelfReadingLocalizationEn>() : "Winkel";

        public string Brand => this._brand;
    }

    private interface IRegistrySelfReadingDefaultLocalization : ILocalizationSet, IRegistryBrandTexts;

    /// <summary>
    /// The same, for the set a text takes its default wording from, which no registry holds.
    /// </summary>
    internal sealed class RegistrySelfReadingDefaultLocalizationEn : IRegistrySelfReadingDefaultLocalization
    {
        [ThreadStatic]
        public static bool ReadsOwnText;

        private readonly string _brand = ReadsOwnText ? ReadBrand<IRegistrySelfReadingDefaultLocalization, RegistrySelfReadingDefaultLocalizationEn>() : "Shop";

        public string Brand => this._brand;
    }

    /// <summary>
    /// A culture that counts how often its name is read, which a lookup by name does and a comparison by reference
    /// doesn't.
    /// </summary>
    private sealed class RegistryCountingCulture(string name) : CultureInfo(name)
    {
        public int NameReads { get; private set; }

        public override string Name
        {
            get
            {
                this.NameReads++;
                return base.Name;
            }
        }
    }

    private interface IRegistryConcurrentLocalization : ILocalizationSet
    {
        string Name { get; }
    }

    /// <summary>
    /// The set the text takes its default wording from. Never registered, so a Dutch user never sees it.
    /// </summary>
    internal sealed class RegistryConcurrentLocalizationEn : IRegistryConcurrentLocalization
    {
        public string Name => "Default";
    }

    internal sealed class RegistryConcurrentFirstLocalizationNl : IRegistryConcurrentLocalization
    {
        public string Name => "Eerste";
    }

    internal sealed class RegistryConcurrentSecondLocalizationNl : IRegistryConcurrentLocalization
    {
        public string Name => "Tweede";
    }

    /// <summary>
    /// Registered in the other tier, which the sets above come after.
    /// </summary>
    internal sealed class RegistryConcurrentDefaultLocalizationNl : IRegistryConcurrentLocalization
    {
        public string Name => "Standaard";
    }

    private interface IRegistryOrderLocalization : ILocalizationSet;

    internal sealed class RegistryOrderFirstLocalizationNl : IRegistryOrderLocalization;

    internal sealed class RegistryOrderSecondLocalizationNl : IRegistryOrderLocalization;

    private interface IRegistryGenericLocalization : ILocalizationSet;

    internal sealed class RegistryGenericLocalizationNl : IRegistryGenericLocalization;

    /// <summary>
    /// A nested set registered by hand, whose name gives its language.
    /// </summary>
    internal sealed class RegistryLocalizationIt : ILocalizationSet;

    /// <summary>
    /// Sets the test describes in another letter case than culture names have.
    /// </summary>
    private sealed class RegistryBelgianLocalization : ILocalizationSet;

    private sealed class RegistryChineseLocalization : ILocalizationSet;

    /// <summary>
    /// A nested set the build doesn't describe, because it's private, though its name ends in a language.
    /// </summary>
    private sealed class RegistryUndescribedLocalizationEnGb : ILocalizationSet;

    /// <summary>
    /// A nested set the build describes, because generated code can name it.
    /// </summary>
    internal sealed class RegistryNestedLocalizationIt : ILocalizationSet;

    /// <summary>
    /// Two types no other test registers, for the test that registers each more than once.
    /// </summary>
    internal sealed class RegistryRepeatedLocalizationDe : ILocalizationSet;

    internal sealed class RegistryRepeatedLocalizationFr : ILocalizationSet;

    /// <summary>
    /// A name no language code can be derived from.
    /// </summary>
    private sealed class RegistryInvalidLocalization1 : ILocalizationSet;

    /// <summary>
    /// A culture whose parent is the invariant culture, as every culture's is under invariant globalization.
    /// </summary>
    private sealed class RegistryInvariantParentCulture(string name) : CultureInfo(name)
    {
        public override CultureInfo Parent => InvariantCulture;
    }

    /// <summary>
    /// A contract whose sets a library of its own rewords, so they're public. Nested, so this project's build doesn't
    /// discover them.
    /// </summary>
    public interface IRegistryPrecedenceLocalization : ILocalizationSet
    {
        string Name { get; }
    }

    public class RegistryPrecedenceLocalizationNl : IRegistryPrecedenceLocalization
    {
        public virtual string Name => "Library";
    }

    /// <summary>
    /// The app's rewording, as this test project is the app.
    /// </summary>
    public sealed class AppRegistryPrecedenceLocalizationNl : RegistryPrecedenceLocalizationNl
    {
        public override string Name => "App";
    }

    public sealed class HandRegistryPrecedenceLocalizationNl : RegistryPrecedenceLocalizationNl
    {
        public override string Name => "Hand";
    }

    private static CultureInfo Culture(string name) => CultureInfo.GetCultureInfo(name);
    #endregion

    [Test]
    public void Create_ShouldHoldExactlyGivenSets()
    {
        // Arrange
        var english = new RegistryLocalizationEn();
        var dutch = new RegistryLocalizationNl();

        // Act
        var registry = LocalizationRegistry.Create([english, dutch]);

        // Assert: nothing of the sets of the whole app, in the given order.
        registry.AllSets.ShouldBe([english, dutch]);
        registry.SetLanguages.ShouldBe(["en", "nl"]);
        LocalizationRegistry.Create([]).AllSets.ShouldBeEmpty();
    }

    [Test]
    public void Create_WithoutLanguages_ShouldHaveNoneWhateverAppDeclares()
    {
        // Arrange & Act
        var registry = LocalizationRegistry.Create([new RegistryLocalizationEn()]);

        // Assert: a registry of one's own does not take the languages of the process.
        registry.AppLanguages.ShouldBeEmpty();
    }

    [Test]
    public void Create_WithLanguages_ShouldKeepThemDefaultFirst()
    {
        // Arrange & Act
        var registry = LocalizationRegistry.Create([new RegistryLocalizationEn()], "nl", "en", "en-GB");

        // Assert
        registry.AppLanguages.ShouldBe(["nl", "en", "en-GB"]);
    }

    [Test]
    public void Create_WithNullLanguage_ShouldThrow()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentNullException>(() => LocalizationRegistry.Create([], null!));
        Should.Throw<ArgumentNullException>(() => LocalizationRegistry.Create([], "en", null!, "nl"));
    }

    [Test]
    public void Create_WithLanguageListedTwice_ShouldThrow()
    {
        // Arrange & Act & Assert: the same language twice, in two spellings.
        Should.Throw<ArgumentException>(() => LocalizationRegistry.Create([], "en", "nl", "NL")).ParamName.ShouldBe("otherLanguages");
        Should.Throw<ArgumentException>(() => LocalizationRegistry.Create([], "en", "EN")).ParamName.ShouldBe("otherLanguages");
    }

    [Test]
    public void GetByCulture_AskedTwiceForOneContract_ShouldGiveSameSetAmongSetsOfOtherContracts()
    {
        // Arrange: the sets of a contract are found once per language and kept.
        var dutch = new RegistryLocalizationNl();
        var replacement = new RegistryReplacementLocalizationNl();
        var registry = LocalizationRegistry.Create([dutch, new RegistryTierDefaultLocalizationNl(), replacement, new RegistryOrderFirstLocalizationNl()]);
        var culture = CultureInfo.GetCultureInfo("nl-NL");

        // Act & Assert
        registry.GetByCulture<IRegistryLocalization>(culture).ShouldBeSameAs(replacement);
        registry.GetByCulture<IRegistryLocalization>(culture).ShouldBeSameAs(replacement);
        registry.GetByCulture<IRegistryTierLocalization>(culture).ShouldBeOfType<RegistryTierDefaultLocalizationNl>();
        registry.GetByCulture<IRegistryOtherLocalization>(culture).ShouldBeNull();
    }

    [Test]
    public void Create_ShouldListLanguagesInOrdinalOrder()
    {
        // Arrange: described as the build does, cased as culture names are.
        LocalizationRegistry.DescribeSet<RegistryBelgianLocalization>("nl-BE");
        LocalizationRegistry.DescribeSet<RegistryChineseLocalization>("zh-Hans");
        var belgian = new RegistryBelgianLocalization();
        var chinese = new RegistryChineseLocalization();

        // Act
        var registry = LocalizationRegistry.Create([chinese, belgian]);

        // Assert: in ordinal order whatever order the sets registered in, and found by the user's culture.
        registry.SetLanguages.ShouldBe(["nl-BE", "zh-Hans"]);
        registry.GetByCulture<ILocalizationSet>(CultureInfo.GetCultureInfo("nl-BE")).ShouldBeSameAs(belgian);
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void Register_OfNestedSetTheBuildDescribed_ShouldFileItUnderTheDescribedLanguage()
    {
        // Arrange: an internal set nested in a public class, which the registrar of this assembly describes with the
        // language of its name.
        var set = new RegistryNestedLocalizationIt();

        // Act
        LocalizationRegistry.Register(set);

        // Assert
        LocalizationRegistry.GetLanguageCode(typeof(RegistryNestedLocalizationIt)).ShouldBe("it");
        LocalizationRegistry.GetLocalizationSet<ILocalizationSet>(CultureInfo.GetCultureInfo("it-IT")).ShouldBeSameAs(set);
    }

    [Test]
    public void Register_OfSetTheBuildDidNotSee_ShouldThrowNamingTheTypeAndTheFix()
    {
        // Arrange: a private nested set, which generated code can't name, so nothing describes it, whatever its name.
        var set = new RegistryUndescribedLocalizationEnGb();

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => LocalizationRegistry.Register(set));

        // Assert: the registry reads no name.
        exception.Message.ShouldContain(typeof(RegistryUndescribedLocalizationEnGb).ToString());
        exception.Message.ShouldContain("has no language. The build reads a set's language from its name");
        exception.Message.ShouldContain("as in ShopLocalizationDe");
        exception.Message.ShouldContain("nested in a public or internal class");
        Should.Throw<InvalidOperationException>(() => LocalizationRegistry.Create([set]));
    }

    [Test]
    public void GetByCulture_ShouldWalkUpToNeutralCulture()
    {
        // Arrange
        var english = new RegistryLocalizationEn();
        var dutch = new RegistryLocalizationNl();
        var registry = LocalizationRegistry.Create([english, dutch]);

        // Act & Assert: nl-BE resolves through nl, en-GB through en; the invariant culture and an unknown language
        // yield null.
        registry.GetByCulture<IRegistryLocalization>(Culture("nl-BE")).ShouldBeSameAs(dutch);
        registry.GetByCulture<IRegistryLocalization>(Culture("en-GB")).ShouldBeSameAs(english);
        registry.GetByCulture<IRegistryLocalization>(Culture("de-DE")).ShouldBeNull();
        registry.GetByCulture<IRegistryLocalization>(CultureInfo.InvariantCulture).ShouldBeNull();
    }

    [Test]
    public void GetByCulture_ForRegionWithInvariantParent_ShouldFallBackByName()
    {
        // Arrange
        var dutch = new RegistryLocalizationNl();
        var registry = LocalizationRegistry.Create([new RegistryLocalizationEn(), dutch]);

        // Act & Assert: under invariant globalization nl-BE has no parent to walk to, and nl still serves it.
        registry.GetByCulture<IRegistryLocalization>(new RegistryInvariantParentCulture("nl-BE")).ShouldBeSameAs(dutch);
    }

    [Test]
    public void GetByCulture_ShouldLetLastSetWinPerContractAndLanguage()
    {
        // Arrange
        var first = new RegistryLocalizationNl();
        var last = new RegistryReplacementLocalizationNl();
        var other = new RegistryCombinedLocalizationNl();

        // Act
        var registry = LocalizationRegistry.Create([first, last]);
        var withOther = LocalizationRegistry.Create([other, first]);

        // Assert: the last set wins for its contract, and a contract only an earlier set implements still finds that
        // one.
        registry.GetByCulture<IRegistryLocalization>(Culture("nl")).ShouldBeSameAs(last);
        withOther.GetByCulture<IRegistryLocalization>(Culture("nl")).ShouldBeSameAs(first);
        withOther.GetByCulture<IRegistryOtherLocalization>(Culture("nl")).ShouldBeSameAs(other);
    }

    [Test]
    public void GetByCulture_ShouldServeEveryContractSetImplements()
    {
        // Arrange
        var combined = new RegistryCombinedLocalizationNl();
        var registry = LocalizationRegistry.Create([combined]);

        // Act & Assert
        registry.GetByCulture<IRegistryLocalization>(Culture("nl")).ShouldBeSameAs(combined);
        registry.GetByCulture<IRegistryOtherLocalization>(Culture("nl")).ShouldBeSameAs(combined);
        registry.GetByCulture<IRegistryOtherLocalization>(Culture("nl")).ShouldBeSameAs(combined);
    }

    [Test]
    public void GetByCulture_AndCreate_ShouldThrowForNullArguments()
    {
        // Arrange
        var registry = LocalizationRegistry.Create([]);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => registry.GetByCulture(null!, CultureInfo.InvariantCulture));
        Should.Throw<ArgumentNullException>(() => registry.GetByCulture(typeof(IRegistryLocalization), null!));
        Should.Throw<ArgumentNullException>(() => LocalizationRegistry.Create(null!));
        Should.Throw<ArgumentNullException>(() => LocalizationRegistry.Create([null!])).ParamName.ShouldBe("sets");
    }

    [Test]
    public void MetadataName_ShouldMatchSourceGeneratorReferences()
    {
        // Assert: the generated module initializers call these by name, so a rename or namespace change must update the
        // generator's emission in the same commit.
        typeof(LocalizationRegistry).FullName.ShouldBe("CodeChops.Idioma.LocalizationRegistry");
        typeof(LocalizationRegistry).GetMember(nameof(LocalizationRegistry.Register)).ShouldNotBeEmpty();
    }
}
