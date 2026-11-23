using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

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
        run.TrackedSteps[LocalizationDiscovery.RecordTrackingName].ShouldHaveSingleItem();
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

        // Act: the English wording changes, which the contract shows in its doc comments. The registrar and the recorded
        // sets are written from what the registrar's share of discovery holds, which has no wording.
        var run = driver.RunGenerators(compilation.ReplaceSyntaxTree(compilation.SyntaxTrees.Single(), edited)).GetRunResult().Results.ShouldHaveSingleItem();

        // Assert: of the three files, only the contract is written again.
        var reasons = GeneratorTestHelper.GetSourceOutputReasons(run);
        reasons.Length.ShouldBe(3);
        reasons.Count(static reason => reason is IncrementalStepRunReason.Modified).ShouldBe(1);
        reasons.Count(static reason => reason is IncrementalStepRunReason.Cached).ShouldBe(2);
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
}
