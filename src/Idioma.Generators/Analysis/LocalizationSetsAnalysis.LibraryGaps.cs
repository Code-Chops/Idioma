namespace CodeChops.Idioma.Generators;

/// <inheritdoc cref="LocalizationSetsAnalysis"/>
internal sealed partial record LocalizationSetsAnalysis
{
    /// <summary>
    /// What a referenced library leaves untranslated: a contract no language set serves one of this assembly's
    /// languages for, which adding the set fixes, and inline texts lacking a language, which nothing here can fix.
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
            // A set whose language its build could not read may be the set of any of them.
            if (IsServed(referencedSets, contract, ProvidedLocalizationSet.UnreadableLanguage) || IsServed(ownSets, contract, ProvidedLocalizationSet.UnreadableLanguage))
                continue;

            // ILocalizationSet<TEnum> for the contract of an enum, which every set of it lists (LOC205).
            var setInterface = references
                .SelectMany(static reference => reference.SetsOfEnums)
                .FirstOrDefault(setOfEnum => String.Equals(setOfEnum.Contract, contract, StringComparison.Ordinal))
                .SetInterface ?? SupportedLanguages.SetInterface;

            foreach (var code in declared.Codes)
            {
                if (IsServed(referencedSets, contract, code) || IsServed(ownSets, contract, code))
                    continue;

                // The contract by its full name, each part as code writes it. The set lies wherever the app keeps it, so
                // the fix chooses its namespace.
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

        // Inline texts: a library's sets can be given a language from here, but no text it writes inline can, whether it
        // has sets or not.
        foreach (var reference in references)
        {
            if (!reference.HasStandaloneTexts || reference.Languages.Length is 0)
                continue;

            // Such a text falls back as any text does: to this assembly's default language where the library has it, and
            // else to the library's own default language.
            var appDefault = declared.Codes[0];
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
