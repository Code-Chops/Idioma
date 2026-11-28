namespace CodeChops.Idioma.Generators;

/// <inheritdoc cref="LocalizationSetsAnalysis"/>
internal sealed partial record LocalizationSetsAnalysis
{
    /// <summary>
    /// What a referenced library leaves untranslated: a contract without a set for a language (<c>LOC700</c>), and
    /// standalone texts without a language, which the app can't fix (<c>LOC701</c>).
    /// </summary>
    public static EquatableArray<LocalizationSetsFinding> FindLibraryGaps(
        DeclaredLanguages? languages,
        EquatableArray<ProvidedLocalizationSet> ownSets,
        EquatableArray<ReferencedLocalization> references)
    {
        if (languages is not { Codes.Length: > 0 } declared)
            return default;

        var findings = ImmutableArray.CreateBuilder<LocalizationSetsFinding>();
        var referencedSets = references.SelectMany(static reference => reference.Sets).ToArray();
        var contracts = referencedSets.Select(static set => set.Contract).Distinct(StringComparer.Ordinal).OrderBy(static name => name, StringComparer.Ordinal);

        foreach (var contract in contracts)
        {
            // ILocalizationSet<TEnum> for an enum's contract (LOC206).
            var setInterface = references
                .SelectMany(static reference => reference.SetsOfEnums)
                .FirstOrDefault(setOfEnum => String.Equals(setOfEnum.Contract, contract, StringComparison.Ordinal))
                .SetInterface ?? SupportedLanguages.SetInterface;

            foreach (var code in declared.Codes)
            {
                if (IsServed(referencedSets, contract, code) || IsServed(ownSets, contract, code))
                    continue;

                // The fix chooses the namespace, because the set goes wherever the app keeps it.
                var setName = SuggestSetName(contract, code);
                var writtenContract = String.Join(".", contract.Split('.').Select(CodeEscaping.EscapeIdentifier));

                findings.Add(new LocalizationSetsFinding(
                    LocalizationSetsRule.LibraryMissingLanguage,
                    declared.Location,
                    new EquatableArray<string>([contract, code, SupportedLanguages.SuggestSet(setName, contract, setInterface)]),
                    SuggestedName: setName,
                    NewSet: new NewLocalizationSet(Namespace: null, $"public class {setName} : {setInterface}, {writtenContract}")));
            }
        }

        // Without a usable default language (LOC102), no fallback can be named.
        if (declared.DefaultLanguage is not { } appDefault)
            return new EquatableArray<LocalizationSetsFinding>(findings.ToImmutable());

        foreach (var reference in references)
        {
            if (!reference.HasStandaloneTexts || reference.Languages.Length is 0)
                continue;

            // The fallback is the app's default language where the library has it, and else the library's.
            var shown = reference.Languages.Any(language => SupportedLanguages.Serves(language, appDefault)) ? appDefault : reference.Languages[0];

            foreach (var code in declared.Codes)
            {
                if (reference.Languages.Any(language => SupportedLanguages.Serves(language, code)))
                    continue;

                findings.Add(new LocalizationSetsFinding(
                    LocalizationSetsRule.LibraryCannotBeTranslated,
                    declared.Location,
                    new EquatableArray<string>([reference.AssemblyName, code, shown])));
            }
        }

        return new EquatableArray<LocalizationSetsFinding>(findings.ToImmutable());

        static bool IsServed(IEnumerable<ProvidedLocalizationSet> sets, string contract, string code)
        {
            return sets.Any(set => String.Equals(set.Contract, contract, StringComparison.Ordinal) && SupportedLanguages.Serves(set.Language, code));
        }
    }
}
