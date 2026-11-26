using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="LocalizationGenerator"/>: that it runs both parts from one read of the sets, and that an edit
/// which changes only what one part uses leaves the other part as it was. The tests of each part run it alone
/// (<see cref="LocalizationParts"/>).
/// </summary>
public sealed class LocalizationGeneratorTests
{
    #region Fixture
    private const string Sets = """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace TestApp;

        public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
        {
            public string Checkout => "Checkout";
        }

        public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
        {
            public string Checkout => "Afrekenen";
        }
        """;

    private static GeneratorDriver CreateTrackingDriver()
    {
        return CSharpGeneratorDriver.Create(
            generators: [new LocalizationGenerator().AsSourceGenerator()],
            parseOptions: GeneratorTestHelper.ParseOptions,
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));
    }

    private static CSharpCompilation Compile(string source)
    {
        return CSharpCompilation.Create(
            "TestAssembly",
            [CSharpSyntaxTree.ParseText(source, GeneratorTestHelper.ParseOptions)],
            GeneratorTestHelper.References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    /// <summary>
    /// Runs the generator over <paramref name="source"/>, edits its file, and runs the same driver again. Returns that
    /// second run, and the syntax tree the file has after the edit.
    /// </summary>
    private static (GeneratorRunResult Run, SyntaxTree Edited) RunAcrossEdit(string source, Func<string, string> edit)
    {
        var compilation = Compile(source);
        var driver = CreateTrackingDriver().RunGenerators(compilation);
        var edited = CSharpSyntaxTree.ParseText(edit(source), GeneratorTestHelper.ParseOptions);
        var run = driver.RunGenerators(compilation.ReplaceSyntaxTree(compilation.SyntaxTrees.Single(), edited)).GetRunResult().Results.ShouldHaveSingleItem();
        run.Exception.ShouldBeNull();

        return (run, edited);
    }

    /// <summary>
    /// Why a named step of the pipeline ran, for each value it gave. <c>Cached</c> means it didn't run,
    /// <c>Unchanged</c> that it ran and gave what it gave before, and <c>Modified</c> that it gave something else.
    /// </summary>
    private static IncrementalStepRunReason[] GetReasons(GeneratorRunResult run, string stepName)
    {
        return [.. run.TrackedSteps[stepName].SelectMany(static step => step.Outputs).Select(static output => output.Reason)];
    }

    /// <summary>
    /// A longer wording for the English text, which also moves everything written after it in the file.
    /// </summary>
    private static string EditWording(string source)
    {
        return source.Replace("=> \"Checkout\"", "=> \"Go to the checkout\"", StringComparison.Ordinal);
    }
    #endregion

    [Test]
    public void Run_ShouldWriteTheContractsAndTheRegistrarFromOneDiscovery()
    {
        // Arrange & Act
        var run = CreateTrackingDriver().RunGenerators(Compile(Sets)).GetRunResult().Results.ShouldHaveSingleItem();

        // Assert: one step per part's share of what discovery found, and the output of both parts.
        run.Exception.ShouldBeNull();
        run.Diagnostics.ShouldBeEmpty();
        run.TrackedSteps[LocalizationDiscovery.ContractTrackingName].ShouldHaveSingleItem();
        run.TrackedSteps[LocalizationDiscovery.RegistrarTrackingName].ShouldHaveSingleItem();
        run.GeneratedSources.Select(static source => source.HintName).ShouldBe(
            ["TestApp.ShopLocalizationEn.Localization.g.cs", "LocalizationSets.g.cs", "LocalizationRegistrar.g.cs"],
            ignoreOrder: true);
    }

    [Test]
    public void EditOfWording_ShouldRewriteTheContractAlone()
    {
        // Arrange
        var compilation = Compile(Sets);
        var driver = CreateTrackingDriver().RunGenerators(compilation);
        var edited = CSharpSyntaxTree.ParseText(Sets.Replace("=> \"Checkout\"", "=> \"Pay\"", StringComparison.Ordinal), GeneratorTestHelper.ParseOptions);

        // Act: the English wording changes, which the contract shows in its doc comments. The registrar and the listed
        // sets are written from what the registrar's share of discovery holds, which has no wording.
        var run = driver.RunGenerators(compilation.ReplaceSyntaxTree(compilation.SyntaxTrees.Single(), edited)).GetRunResult().Results.ShouldHaveSingleItem();

        // Assert: of the three files, only the contract is written again.
        var reasons = GeneratorTestHelper.GetSourceOutputReasons(run);
        reasons.Length.ShouldBe(3);
        reasons.Count(static reason => reason is IncrementalStepRunReason.Modified).ShouldBe(1);
        reasons.Count(static reason => reason is IncrementalStepRunReason.Cached).ShouldBe(2);
    }

    [Test]
    public void EditOfWording_ShouldLeaveTheGeneratedContractsAsTheyWere()
    {
        // Arrange
        var compilation = Compile(Sets);
        var driver = CreateTrackingDriver().RunGenerators(compilation);
        var edited = CSharpSyntaxTree.ParseText(Sets.Replace("=> \"Checkout\"", "=> \"Pay\"", StringComparison.Ordinal), GeneratorTestHelper.ParseOptions);

        // Act: which contracts the compilation generates is worked out once for both parts, from what the registrar's
        // share of discovery holds, which has no wording.
        var run = driver.RunGenerators(compilation.ReplaceSyntaxTree(compilation.SyntaxTrees.Single(), edited)).GetRunResult().Results.ShouldHaveSingleItem();

        // Assert
        var step = run.TrackedSteps[LocalizationDiscovery.GeneratedContractsTrackingName].ShouldHaveSingleItem();
        step.Outputs.ShouldAllBe(static output => output.Reason == IncrementalStepRunReason.Cached || output.Reason == IncrementalStepRunReason.Unchanged);
    }

    [Test]
    public void UnrelatedEdit_ShouldLeaveGeneratorFullyCached()
    {
        // Arrange & Act: editing a file that declares no sets must not re-run an output step of either part.
        var reasons = GeneratorTestHelper.GetOutputReasonsAfterUnrelatedEdit(new LocalizationGenerator(), Sets);

        // Assert
        reasons.ShouldNotBeEmpty();
        reasons.ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }

    [Test]
    public void EditOfWording_ShouldNotRunTheStepsOfTheRegistrarAgain()
    {
        // Arrange & Act: the wording gets longer, so the edit renews the file's syntax tree and moves the Dutch set.
        var (run, _) = RunAcrossEdit(Sets, EditWording);

        // Assert: the registrar's share of each class is equal to what it was, because it holds no wording and no
        // location. So no step that reads that share alone runs.
        GetReasons(run, LocalizationDiscovery.RegistrarTrackingName).ShouldBe([IncrementalStepRunReason.Cached]);
        GetReasons(run, LocalizationDiscovery.BindingQuestionTrackingName).ShouldBe([IncrementalStepRunReason.Cached]);
        GetReasons(run, LocalizationDiscovery.GeneratedContractsTrackingName).ShouldBe([IncrementalStepRunReason.Cached]);
        GetReasons(run, LocalizationRegistrationPart.RegistrarTrackingName).ShouldBe([IncrementalStepRunReason.Cached]);

        // The share the contracts are written from holds the wording, so the analysis runs again.
        GetReasons(run, LocalizationDiscovery.ContractTrackingName).ShouldBe([IncrementalStepRunReason.Modified]);
        GetReasons(run, LocalizationSetsPart.AnalysisTrackingName).ShouldBe([IncrementalStepRunReason.Modified]);
    }

    [Test]
    public void RegistrarDiscovery_ShouldHoldNoLocation()
    {
        // Arrange: a set that can't be registered, a set whose language code is no culture name, a regional set whose
        // LocalizationLanguageCode implements nothing, and a nested class that reads as a default-language set.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de", "en-GB")]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            public sealed class ShopLocalizationNl(string wording) : ILocalizationSet, IShopLocalization
            {
                public string Checkout => wording;
            }

            public sealed class ShopLocalizationDe : ILocalizationSet, IShopLocalization
            {
                public string LocalizationLanguageCode => "de_DE";
                public string Checkout => "Zur Kasse";
            }

            public sealed class ShopLocalizationEnGb : ShopLocalizationEn
            {
                public string LocalizationLanguageCode => "en-GB";
                public override string Checkout => "Checkout";
            }

            public static class Holder
            {
                public class AdminLocalizationEn : ILocalizationSet, IAdminLocalization
                {
                    public string Delete => "Delete";
                }
            }
            """;

        // Act
        var run = CreateTrackingDriver().RunGenerators(Compile(source)).GetRunResult().Results.ShouldHaveSingleItem();
        var contractDiscovery = (Discovery)run.TrackedSteps[LocalizationDiscovery.ContractTrackingName].Single().Outputs.Single().Value;
        var registrarDiscovery = (Discovery)run.TrackedSteps[LocalizationDiscovery.RegistrarTrackingName].Single().Outputs.Single().Value;

        // Assert: the share the diagnostics are reported from holds where each is reported.
        contractDiscovery.Sets.ShouldAllBe(static set => set.Location.Tree != null && set.Locations.BaseTypes.Length > 0);
        contractDiscovery.Sets.Count(static set => set.Locations.InvalidLanguageCode is not null).ShouldBe(1);
        contractDiscovery.Sets.Count(static set => set.Locations.UnmappedLanguageCode is not null).ShouldBe(1);
        contractDiscovery.UnextendableSets.ShouldHaveSingleItem().Location.Tree.ShouldNotBeNull();

        // The registrar's share holds the same sets and base types, and no location at all.
        registrarDiscovery.Sets.Length.ShouldBe(4);
        registrarDiscovery.Sets.ShouldAllBe(static set => set.Locations == default && set.UnresolvedBaseTypes.Length > 0);
        var unextendable = registrarDiscovery.UnextendableSets.ShouldHaveSingleItem();
        unextendable.Location.ShouldBe(default);
        unextendable.BaseTypeLocations.ShouldBeEmpty();
        unextendable.UnresolvedBaseTypes.ShouldHaveSingleItem().Name.ShouldBe("IAdminLocalization");
        registrarDiscovery.UnnameableOverrides.ShouldBeEmpty();
    }

    [Test]
    public void EditOfWording_ShouldReportInTheNewTreeAndLeaveTheRegistrar()
    {
        // Arrange & Act: a Dutch set that can't be registered (LOC208), and a German set whose language code is no
        // culture name (LOC207). Both are reported by the registrar's part, which writes the registrar from a share
        // without locations.
        var (run, edited) = RunAcrossEdit(
            $$"""
            {{Sets.Replace("\"en\", \"nl\"", "\"en\", \"nl\", \"de\"", StringComparison.Ordinal).Replace("public class ShopLocalizationNl", "public class ShopLocalizationNl(string wording)", StringComparison.Ordinal)}}

            public class ShopLocalizationDe : ILocalizationSet, IShopLocalization
            {
                public string LocalizationLanguageCode => "de_DE";
                public string Checkout => "Zur Kasse";
            }
            """,
            EditWording);

        // Assert: each diagnostic lies in the tree the file has now, where a pragma of that file applies to it.
        var diagnostics = run.Diagnostics.Where(static diagnostic => diagnostic.Id is "LOC207" or "LOC208").ToArray();
        diagnostics.Select(static diagnostic => diagnostic.Id).ShouldBe(["LOC207", "LOC208"], ignoreOrder: true);
        diagnostics.ShouldAllBe(diagnostic => diagnostic.Location.SourceTree == edited);
        diagnostics.Single(static diagnostic => diagnostic.Id is "LOC208").Location.SourceSpan.ShouldBe(TextSpan.FromBounds(
            edited.ToString().IndexOf("ShopLocalizationNl", StringComparison.Ordinal),
            edited.ToString().IndexOf("ShopLocalizationNl", StringComparison.Ordinal) + "ShopLocalizationNl".Length));
        GetReasons(run, LocalizationRegistrationPart.RegistrarTrackingName).ShouldBe([IncrementalStepRunReason.Cached]);
    }

    [Test]
    public void EditOfWording_ShouldKeepTheContractTheCompilerBoundABaseTypeTo()
    {
        // Arrange: two contracts of one name in two namespaces, so the compiler says which of them the Dutch set
        // implements. The answer is kept by which entry of which base list it is, and not by where that is written.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace Shop.Checkout
            {
                public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
                {
                    public string Checkout => "Checkout";
                }
            }

            namespace Shop.Admin
            {
                public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
                {
                    public string Delete => "Delete";
                }

                public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
                {
                    public string Delete => "Verwijderen";
                }
            }
            """;

        // Act: the edit moves the base list of the Dutch set.
        var (run, _) = RunAcrossEdit(source, EditWording);

        // Assert: the Dutch set still counts for the admin's contract, and the contracts are not worked out again.
        var provided = run.GeneratedSources.Single(static generated => generated.HintName is "LocalizationSets.g.cs").SourceText.ToString();
        provided.ShouldContain("LocalizationSetAttribute(\"Shop.Admin.IShopLocalization\", \"nl\")");
        provided.ShouldNotContain("LocalizationSetAttribute(\"Shop.Checkout.IShopLocalization\", \"nl\")");
        run.Diagnostics.Select(static diagnostic => diagnostic.Id).ShouldBe(["LOC201"]);
        GetReasons(run, LocalizationDiscovery.BindingQuestionTrackingName).ShouldBe([IncrementalStepRunReason.Cached]);
        GetReasons(run, LocalizationDiscovery.GeneratedContractsTrackingName).ShouldBe([IncrementalStepRunReason.Cached]);
    }

    [Test]
    public void AddedText_ShouldRunTheAnalysisAgainAndLeaveTheRegistrar()
    {
        // Arrange & Act: a text more in both sets. The registrar names no text of a set that derives from none.
        var (run, _) = RunAcrossEdit(Sets, static source => source
            .Replace("public string Checkout => \"Checkout\";", "public string Checkout => \"Checkout\"; public string Cart => \"Cart\";", StringComparison.Ordinal)
            .Replace("public string Checkout => \"Afrekenen\";", "public string Checkout => \"Afrekenen\"; public string Cart => \"Winkelwagen\";", StringComparison.Ordinal));

        // Assert
        run.Diagnostics.ShouldBeEmpty();
        GetReasons(run, LocalizationSetsPart.AnalysisTrackingName).ShouldBe([IncrementalStepRunReason.Modified]);
        run.GeneratedSources.Single(static generated => generated.HintName is "TestApp.ShopLocalizationEn.Localization.g.cs").SourceText.ToString().ShouldContain("Cart");
        GetReasons(run, LocalizationDiscovery.GeneratedContractsTrackingName).ShouldBe([IncrementalStepRunReason.Cached]);
        GetReasons(run, LocalizationRegistrationPart.RegistrarTrackingName).ShouldBe([IncrementalStepRunReason.Cached]);
    }

    [Test]
    public void RenamedSet_ShouldWriteTheRegistrarAgain()
    {
        // Arrange & Act: the Dutch set becomes a German one, which the registrar names.
        var (run, _) = RunAcrossEdit(Sets, static source => source
            .Replace("ShopLocalizationNl", "ShopLocalizationDe", StringComparison.Ordinal)
            .Replace("\"nl\"", "\"de\"", StringComparison.Ordinal));

        // Assert: the contracts are worked out again from the new sets, and are the same ones.
        GetReasons(run, LocalizationDiscovery.RegistrarTrackingName).ShouldBe([IncrementalStepRunReason.Modified]);
        GetReasons(run, LocalizationDiscovery.GeneratedContractsTrackingName).ShouldBe([IncrementalStepRunReason.Unchanged]);
        GetReasons(run, LocalizationRegistrationPart.RegistrarTrackingName).ShouldBe([IncrementalStepRunReason.Modified]);
        run.GeneratedSources.Single(static generated => generated.HintName is "LocalizationRegistrar.g.cs").SourceText.ToString().ShouldContain("new TestApp.ShopLocalizationDe()");
    }

    [Test]
    public void RenamedDefaultLanguageSet_ShouldWorkOutTheContractsAgain()
    {
        // Arrange & Act: both sets and their contract get another name.
        var (run, _) = RunAcrossEdit(Sets, static source => source.Replace("ShopLocalization", "StoreLocalization", StringComparison.Ordinal));

        // Assert
        run.Diagnostics.ShouldBeEmpty();
        GetReasons(run, LocalizationDiscovery.GeneratedContractsTrackingName).ShouldBe([IncrementalStepRunReason.Modified]);
        GetReasons(run, LocalizationSetsPart.AnalysisTrackingName).ShouldBe([IncrementalStepRunReason.Modified]);
        GetReasons(run, LocalizationRegistrationPart.RegistrarTrackingName).ShouldBe([IncrementalStepRunReason.Modified]);
        run.GeneratedSources.ShouldContain(static generated => generated.HintName == "TestApp.StoreLocalizationEn.Localization.g.cs");
    }
}
