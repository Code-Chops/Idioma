namespace CodeChops.Idioma.Generators;

/// <summary>
/// Checks an assembly's sets against <c>[assembly: SupportedLanguages(...)]</c>, and writes the contract and generated
/// class of each default-language set. For <c>ShopLocalizationEn : ILocalizationSet, IShopLocalization</c>, it writes
/// <c>IShopLocalization</c> and the static partial class <c>ShopLocalization</c>, both with the set's accessibility.
/// A set that lists <c>ILocalizationSet&lt;TEnum&gt;</c> holds an enum's texts, and its class gets
/// <c>GetTextByValue</c> and the extension <c>GetText</c>.
/// </summary>
/// <remarks>
/// It also lists every set of a public contract in an assembly attribute, so a referencing build needn't search the
/// types. It reports the languages referenced libraries lack, and the rewordings that leave out a set. The generator
/// sees the compilation without its own output, so a set's interfaces are exactly the handwritten ones.
/// </remarks>
public static partial class LocalizationSetsPart
{
    /// <summary>
    /// The pipeline step of <see cref="LocalizationSetsAnalysis.Analyze"/>, for a test to read it.
    /// </summary>
    internal const string AnalysisTrackingName = "Analysis";

    internal static void Initialize(IncrementalGeneratorInitializationContext context, LocalizationInputs inputs)
    {
        var languages = inputs.Languages;

        // No code below C# 14 (LOC104), but diagnostics are still reported.
        var compiles = inputs.Compiles;

        var analysis = inputs.ContractDiscovery
            .Combine(languages)
            .Combine(inputs.Contracts)
            .Select(static (data, cancellationToken) => LocalizationSetsAnalysis.Analyze(
                data.Left.Left.Sets,
                data.Left.Left.UnextendableSets,
                data.Left.Right,
                data.Right,
                cancellationToken))
            .WithTrackingName(AnalysisTrackingName);

        // Separate outputs, so an edit that only moves locations reports anew without rewriting the generated code.
        context.RegisterSourceOutput(analysis.Select(static (analysis, _) => analysis.Findings), Report);

        context.RegisterSourceOutput(analysis.SelectMany(static (analysis, _) => analysis.Contracts.AsImmutableArray()).WhereItCompiles(compiles), static (context, contract) =>
            context.AddSource($"{contract.HintName}.Localization.g.cs", LocalizationContractSource.Generate(contract)));

        var providedSets = analysis.Select(static (analysis, _) => analysis.ProvidedSets);

        context.RegisterSourceOutput(providedSets.WhereItCompiles(compiles), static (context, sets) =>
        {
            if (sets.Length > 0)
                context.AddSource("LocalizationSets.g.cs", ProvidedSetsSource.Generate(sets));
        });

        // Runs on every compilation change, so it reads only a few assembly attributes into a plain, cacheable value.
        var references = context.CompilationProvider.Select(static (compilation, _) => ReferencedLocalizationReader.Read(compilation));

        var gaps = languages
            .Combine(providedSets)
            .Combine(references)
            .Select(static (data, _) => LocalizationSetsAnalysis.FindLibraryGaps(data.Left.Left, data.Left.Right, data.Right));

        context.RegisterSourceOutput(gaps, Report);

        var rewordingGaps = inputs.ContractDiscovery
            .Combine(references)
            .Select(static (data, _) => LocalizationSetsAnalysis.FindRewordingGaps(data.Left.Sets, data.Right));

        context.RegisterSourceOutput(rewordingGaps, Report);
    }
}
