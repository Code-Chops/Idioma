using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

public sealed partial class LocalizationRegistrationPartTests
{
    [Test]
    public void ReparsedSet_StillEmitsAcrossEditWithoutThrowing()
    {
        // Arrange & Act: the same driver runs over a re-parsed copy of the file (the IDE edit scenario that surfaces
        // stale cached symbols).
        var result = GeneratorTestHelper.RunAcrossEdit(LocalizationParts.Registration(), $$"""
            {{ShopContract}}

            public sealed class ShopLocalizationDe : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Zur Kasse";
            }
            """);

        // Assert
        GeneratedText(result).ShouldContain("new TestApp.ShopLocalizationDe()");
    }

    [Test]
    public void UnrelatedEdit_ShouldLeaveGeneratorFullyCached()
    {
        // Arrange & Act: editing a file that declares no sets must not re-run the output step (lag on every keystroke).
        var reasons = GeneratorTestHelper.GetOutputReasonsAfterUnrelatedEdit(LocalizationParts.Registration(), $$"""
            {{ShopContract}}

            public sealed class ShopLocalizationDe : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Zur Kasse";
            }
            """);

        // Assert
        reasons.ShouldNotBeEmpty();
        reasons.ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }

    [Test]
    public void Discovery_ShouldReadOnlyTheTextsTheRegistrarDescribes()
    {
        // Arrange & Act: discovery reads every set again on every edit, in any file. The registrar passes on the names of
        // the texts a derived set words, and writes nothing from a wording.
        var discovery = GeneratorTestHelper.GetStepValue<Discovery>(LocalizationParts.Registration(), LocalizationDiscovery.RegistrarTrackingName, """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "en-GB")]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Cart => "Cart";
                public virtual string Greeting(string name) => $"Hello, {name}!";
            }

            public sealed class ShopLocalizationEnGb : ShopLocalizationEn
            {
                public override string Cart => "Basket";
            }
            """);

        // Assert
        discovery.Sets.Length.ShouldBe(2);
        discovery.Sets.Single(static set => set.Name is "ShopLocalizationEn").OwnTexts.ShouldBeEmpty();
        discovery.Sets.Single(static set => set.Name is "ShopLocalizationEnGb").OwnTexts.ShouldBe(["Cart"]);

        // Nothing of what the contracts are written from, so that a change of it leaves the registrar as it was. The
        // names of the enums whose texts a set holds are the registrar's.
        discovery.Sets.ShouldAllBe(static set => set.OfContract == default);
    }

    [Test]
    public void Discovery_ShouldLeaveOutATextThatAMemberOnlyHides()
    {
        // Arrange & Act: a member that hides a text words nothing, because the contract calls the member it hides
        // (LOC302). Passed on, it would keep the registry from a rewording of English. A set that lists the contract again
        // maps the contract to its own member, which then words the text.
        var discovery = GeneratorTestHelper.GetStepValue<Discovery>(LocalizationParts.Registration(), LocalizationDiscovery.RegistrarTrackingName, """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "en-GB", "en-AU")]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Color => "Color";
                public string Greeting(string name) => $"Hello, {name}!";
            }

            public sealed class ShopLocalizationEnGb : ShopLocalizationEn
            {
                public new string Color => "Colour";
                public new string Greeting(string name) => $"Hiya, {name}!";
            }

            public sealed class ShopLocalizationEnAu : ShopLocalizationEn, IShopLocalization
            {
                public new string Color => "Colour";
            }
            """);

        // Assert
        discovery.Sets.Single(static set => set.Name is "ShopLocalizationEnGb").OwnTexts.ShouldBeEmpty();
        discovery.Sets.Single(static set => set.Name is "ShopLocalizationEnAu").OwnTexts.ShouldBe(["Color"]);
    }

    [Test]
    public void EditInFileOfSet_ShouldReportInItsNewTreeAndRewriteNoSource()
    {
        // Arrange & Act: a set that registers, one that can't and is reported, and an edit in their file which moves
        // nothing.
        var (run, edited) = GeneratorTestHelper.RunAcrossEditInSameFile(LocalizationParts.Registration(), $$"""
            {{ShopContract}}

            public sealed class ShopLocalizationDe : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Zur Kasse";
            }

            public sealed class ShopLocalizationNl(string wording) : ILocalizationSet, IShopLocalization
            {
                public string Checkout => wording;
            }
            """);

        // Assert: the diagnostic lies in the tree the file has now, where a pragma of that file applies to it, and the
        // registrar is not written again.
        var diagnostic = run.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC208");
        diagnostic.Location.SourceTree.ShouldBeSameAs(edited);
        GeneratorTestHelper.GetSourceOutputReasons(run).ShouldHaveSingleItem().ShouldBe(IncrementalStepRunReason.Cached);
    }
}
