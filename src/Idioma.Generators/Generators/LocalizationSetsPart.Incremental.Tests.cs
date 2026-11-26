using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

public sealed partial class LocalizationSetsPartTests
{
    [Test]
    public void ReparsedSet_ShouldStillEmitAcrossEditWithoutThrowing()
    {
        // Arrange & Act: the same driver runs over a re-parsed copy of the set (the IDE edit scenario).
        var result = GeneratorTestHelper.RunAcrossEdit(LocalizationParts.Sets(), ShopSets);

        // Assert
        ContractText(result).ShouldContain("public static partial class ShopLocalization");
    }

    [Test]
    public void UnrelatedEdit_ShouldLeaveGeneratorFullyCached()
    {
        // Arrange & Act: editing a file that declares no set must not re-run the output steps.
        var reasons = GeneratorTestHelper.GetOutputReasonsAfterUnrelatedEdit(LocalizationParts.Sets(), ShopSets);

        // Assert
        reasons.ShouldNotBeEmpty();
        reasons.ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }

    [Test]
    public void Discovery_ShouldReadTheTextsOfAContractAndNotWhatTheRegistrarDescribes()
    {
        // Arrange & Act: discovery reads every set again on every edit, in any file, so it reads what this part writes
        // from and leaves the rest to the part that uses it.
        var discovery = GeneratorTestHelper.GetStepValue<Discovery>(LocalizationParts.Sets(), LocalizationDiscovery.ContractTrackingName, """
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
        var english = discovery.Sets.Single(static set => set.Name is "ShopLocalizationEn");
        var british = discovery.Sets.Single(static set => set.Name is "ShopLocalizationEnGb");
        english.OfContract.Texts.Select(static text => (text.Name, text.DefaultText)).ShouldBe([("Cart", "Cart"), ("Greeting", "Hello, {name}!")]);
        english.OfContract.Wordings.Select(static wording => wording.Wording).ShouldBe(["Cart", "Hello, {name}!"]);
        british.OfContract.Wordings.Select(static wording => wording.Wording).ShouldBe(["Basket", "Hello, {name}!"]);
        british.OfContract.DeclaredTexts.ShouldBe(["Cart"]);
        british.OfContract.InheritedTexts.Select(static text => text.Name).ShouldBe(["Greeting"]);
        british.OwnTexts.ShouldBeEmpty();
    }

    [Test]
    public void Discovery_ShouldReadTheTextsOfTheDefaultLanguageSetAlone()
    {
        // Arrange & Act: the contract is written from the default-language set, and discovery reads every set again on
        // every edit. A set of another language is read for the wording the links of the contract show.
        var discovery = GeneratorTestHelper.GetStepValue<Discovery>(LocalizationParts.Sets(), LocalizationDiscovery.ContractTrackingName, """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Cart => "Cart";
            }

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Cart => "Winkelwagen";
            }
            """);

        // Assert
        var english = discovery.Sets.Single(static set => set.Name is "ShopLocalizationEn");
        var dutch = discovery.Sets.Single(static set => set.Name is "ShopLocalizationNl");
        english.OfContract.Texts.Select(static text => text.Name).ShouldBe(["Cart"]);
        dutch.OfContract.Texts.ShouldBeEmpty();
        dutch.OfContract.Wordings.Select(static wording => wording.Wording).ShouldBe(["Winkelwagen"]);
    }

    [Test]
    public void Discovery_WithoutDefaultLanguage_ShouldReadTheTextsOfEverySet()
    {
        // Arrange & Act: a first language that isn't a culture name (LOC101) leaves the default language unknown, and
        // the build takes a set of any language for the default-language set, so that its contract is still written.
        var discovery = GeneratorTestHelper.GetStepValue<Discovery>(LocalizationParts.Sets(), LocalizationDiscovery.ContractTrackingName, """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en_US", "en", "nl")]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Cart => "Cart";
            }

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Cart => "Winkelwagen";
            }
            """);

        // Assert
        discovery.Sets.ShouldAllBe(static set => set.OfContract.Texts.Length == 1);
    }

    [Test]
    public void EditInFileOfSet_ShouldReportInItsNewTreeAndRewriteNoSource()
    {
        // Arrange & Act: a contract without its Dutch set, reported at the default-language set, and an edit in that
        // file which moves nothing.
        var (run, edited) = GeneratorTestHelper.RunAcrossEditInSameFile(LocalizationParts.Sets(), $$"""
            {{Header}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }
            """);

        // Assert: the diagnostic lies in the tree the file has now, where a pragma of that file applies to it, and the
        // contract and the listed sets are not written again.
        var diagnostic = run.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC201");
        diagnostic.Location.SourceTree.ShouldBeSameAs(edited);
        var reasons = GeneratorTestHelper.GetSourceOutputReasons(run);
        reasons.Length.ShouldBe(2);
        reasons.ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }
}
