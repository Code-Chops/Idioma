namespace CodeChops.Idioma.Generators;

/// <summary>
/// Holds the generated code to the C# version it needs. The code generated for an assembly that declares languages
/// uses extension members, which C# 14 brings, so a project with an older <c>LangVersion</c> gets none of it. It gets
/// <c>LOC102</c> instead, which says why, and not a dozen errors inside generated files.
/// </summary>
/// <remarks>
/// An assembly that declares no languages gets only its registrar, which compiles with any version. So a project that
/// merely references a library that uses this package builds with an older version too.
/// </remarks>
internal static class LanguageVersionCheck
{
    /// <summary>
    /// The version the generated code needs.
    /// </summary>
    public const LanguageVersion Required = LanguageVersion.CSharp14;

    /// <summary>
    /// Whether code generated for this assembly compiles: it declares no languages, or it compiles with C# 14 or later.
    /// </summary>
    /// <param name="context">The generator's context, for the version of C#.</param>
    /// <param name="languages">The languages the assembly declares (<see cref="SupportedLanguages.Read"/>).</param>
    public static IncrementalValueProvider<bool> Read(IncrementalGeneratorInitializationContext context, IncrementalValueProvider<DeclaredLanguages?> languages)
    {
        var provider = languages
            .Select(static (languages, _) => languages is { Codes.Length: > 0 })
            .Combine(context.ParseOptionsProvider.Select(static (options, _) => !IsTooOld(options)))
            .Select(static (data, _) => !data.Left || data.Right);

        return provider;
    }

    /// <summary>
    /// Whether a project compiles with a version of C# older than the generated code needs.
    /// </summary>
    public static bool IsTooOld(ParseOptions options)
    {
        var isTooOld = options is CSharpParseOptions { LanguageVersion: < Required };
        return isTooOld;
    }

    /// <summary>
    /// What a generator writes, only where it compiles (<see cref="Read"/>).
    /// </summary>
    public static IncrementalValuesProvider<T> WhereItCompiles<T>(this IncrementalValueProvider<T> source, IncrementalValueProvider<bool> compiles)
    {
        var provider = source
            .Combine(compiles)
            .SelectMany(static (data, _) => data.Right ? [data.Left] : ImmutableArray<T>.Empty);

        return provider;
    }

    /// <inheritdoc cref="WhereItCompiles{T}(IncrementalValueProvider{T}, IncrementalValueProvider{bool})"/>
    public static IncrementalValuesProvider<T> WhereItCompiles<T>(this IncrementalValuesProvider<T> source, IncrementalValueProvider<bool> compiles)
    {
        var provider = source
            .Combine(compiles).Where(static data => data.Right)
            .Select(static (data, _) => data.Left);

        return provider;
    }
}
