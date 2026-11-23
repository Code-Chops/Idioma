using CodeChops.Idioma.Generators;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests.TestHelpers;

/// <summary>
/// The parts of <see cref="LocalizationGenerator"/>, each run as a generator of its own, so that a test sees what one
/// part writes and reports, and nothing of the other.
/// </summary>
internal static class LocalizationParts
{
    /// <summary>
    /// The part that writes the contracts and the generated classes (<see cref="LocalizationSetsPart"/>).
    /// </summary>
    public static IIncrementalGenerator Sets()
    {
        return new Part(LocalizationSetsPart.Initialize);
    }

    /// <summary>
    /// The part that writes the registrar (<see cref="LocalizationRegistrationPart"/>).
    /// </summary>
    public static IIncrementalGenerator Registration()
    {
        return new Part(LocalizationRegistrationPart.Initialize);
    }

    /// <summary>
    /// One part, with the inputs the generator reads once for both.
    /// </summary>
    private sealed class Part(Action<IncrementalGeneratorInitializationContext, LocalizationInputs> initialize) : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            initialize(context, LocalizationInputs.Read(context));
        }
    }
}
