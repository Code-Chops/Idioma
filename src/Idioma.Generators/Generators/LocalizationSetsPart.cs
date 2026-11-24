namespace CodeChops.Idioma.Generators;

/// <summary>
/// Holds the language sets of an assembly to the languages of <c>[assembly: SupportedLanguages(...)]</c>, and writes the
/// contract of each default-language set. For a top-level class listing <c>ILocalizationSet</c> and named after the
/// default language (<c>ShopLocalizationEn : ILocalizationSet, IShopLocalization</c>) it emits the contract interface
/// (<c>IShopLocalization</c>, one <see cref="string"/> member per public string property, marked
/// <c>[LocalizationContract]</c>), which the set lists itself, and the static partial class named after the set without
/// its language suffix
/// (<c>ShopLocalization</c>, one <see cref="string"/> property per member, which gives the wording of the set of the
/// user's language, and a method <c>GetTextByKey</c> that gives the
/// text with a key, which is its name); the interface and the class take the set's accessibility. A public method of the set that returns a <see cref="string"/> and takes
/// parameters is a text that takes values: the contract repeats it, and the class holds a method of its name and
/// parameters that gives the wording for the values it is called with. A set that lists
/// <c>ILocalizationSet&lt;TEnum&gt;</c> holds the texts of an enum: the contract gets a member for every member of
/// the enum, and the class a method <c>GetTextByValue</c> that gives the text of a value, with an extension method
/// <c>GetText</c> that gives a value its text itself, as in <c>order.Status.GetText()</c>. The class comes first in the
/// file. Each text inherits the property's doc comment through <c>&lt;inheritdoc cref/&gt;</c>, or else is documented
/// with its default-language wording, and links to its wording in every language, which it shows next to the link. Each
/// member of the contract inherits the doc comment of its text.
/// </summary>
/// <remarks>
/// <para>
/// It also records every set of a public contract in an assembly attribute, so that the build of an assembly referencing
/// this one can tell which languages exist without searching its types, and reports the languages referenced libraries
/// leave open. The generator sees the compilation before its own output is added, so the interfaces of a set are exactly
/// the handwritten ones.
/// </para>
/// <para>
/// It's a part of <see cref="LocalizationGenerator"/>, which reads the sets once for this part and the registrar's.
/// </para>
/// </remarks>
public static partial class LocalizationSetsPart
{
    /// <summary>
    /// The name of the pipeline step that holds the analysis of the sets (<see cref="LocalizationSetsAnalysis.Analyze"/>),
    /// for a test to read it.
    /// </summary>
    internal const string AnalysisTrackingName = "Analysis";

    /// <summary>
    /// Adds this part's steps to the pipeline of <see cref="LocalizationGenerator"/>.
    /// </summary>
    internal static void Initialize(IncrementalGeneratorInitializationContext context, LocalizationInputs inputs)
    {
        var languages = inputs.Languages;

        // No code below C# 14, which LOC102 reports. What is found about the sets is reported all the same.
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

        // Separate outputs, so that an edit in the file of a set, which renews its location, reports its diagnostics
        // anew without rewriting the generated code: the contracts and the recorded sets hold no location. The analysis
        // itself runs again on such an edit, because it reads the wordings and the locations.
        context.RegisterSourceOutput(analysis.Select(static (analysis, _) => analysis.Findings), Report);

        // Each contract under a hint name of its own, which the analysis gives it (LocalizationContract.HintName).
        context.RegisterSourceOutput(analysis.SelectMany(static (analysis, _) => analysis.Contracts.AsImmutableArray()).WhereItCompiles(compiles), static (context, contract) =>
            context.AddSource($"{contract.HintName}.Localization.g.cs", LocalizationContractSource.Generate(contract)));

        var providedSets = analysis.Select(static (analysis, _) => analysis.ProvidedSets);

        context.RegisterSourceOutput(providedSets.WhereItCompiles(compiles), static (context, sets) =>
        {
            if (sets.Length > 0)
                context.AddSource("LocalizationSets.g.cs", ProvidedSetsSource.Generate(sets));
        });

        // Runs on every change of the compilation in an IDE, so it reads no more than a few assembly attributes, and
        // ends in a plain value that keeps everything after it cached.
        var references = context.CompilationProvider.Select(static (compilation, _) => ReferencedLocalizationReader.Read(compilation));

        var gaps = languages
            .Combine(providedSets)
            .Combine(references)
            .Select(static (data, _) => LocalizationSetsAnalysis.FindLibraryGaps(data.Left.Left, data.Left.Right, data.Right));

        context.RegisterSourceOutput(gaps, Report);
    }
}
