namespace CodeChops.Idioma.Generators;

/// <inheritdoc cref="LocalizationSetsAnalysis"/>
internal sealed partial record LocalizationSetsAnalysis
{
    /// <summary>
    /// What a rewording of another assembly's set leaves out, because for each contract and language one set wins whole
    /// at run time. The other sets come from the referenced assemblies' <c>[assembly: LocalizationSet]</c>.
    /// <list type="bullet">
    /// <item><c>LOC208</c>: the reworded set's assembly has a regional set this assembly doesn't reword.</item>
    /// <item><c>LOC209</c>: a rewording of this assembly next to one of a referenced assembly, or two of referenced
    /// assemblies, neither deriving from the other. Two of this assembly are reported with <c>LOC203</c>
    /// (<see cref="ReportRepeatedLanguages"/>).</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// A set marked <c>[ExcludeFromLocalizationDiscovery]</c> is registered by hand and wins over every other set, so
    /// neither rule applies to it. It does serve its language, so it settles <c>LOC208</c> for its region. A rewording
    /// that can't be registered (<c>LOC207</c>) never shows, so neither rule applies to it either.
    /// </remarks>
    public static EquatableArray<LocalizationSetsFinding> FindRewordingGaps(
        EquatableArray<DiscoveredLocalization> discovered,
        EquatableArray<ReferencedLocalization> references)
    {
        var ownSets = LocalizationDiscovery.Merge(discovered);

        var ownRewordings = ownSets
            .Where(static set => set is { IsExcluded: false, Reworded: not null, RegistrationProblem: null })
            .OrderBy(static set => set.MetadataName, StringComparer.Ordinal)
            .ToImmutableArray();

        var findings = ImmutableArray.CreateBuilder<LocalizationSetsFinding>();

        ReportUnreachedRegionalSets(ownSets, ownRewordings, references, findings);

        var referencedRewordings = references
            .SelectMany(static reference => reference.Sets
                .Where(static set => set.Reworded is not null)
                .GroupBy(static set => set.Set, StringComparer.Ordinal)
                .OrderBy(static group => group.Key, StringComparer.Ordinal)
                .Select(group => Rewording.Of(group.First(), reference.AssemblyName)))
            .ToImmutableArray();

        ReportRewordingsOfReferencedSets([.. ownRewordings.Select(Rewording.Of)], referencedRewordings, references, findings);

        return new EquatableArray<LocalizationSetsFinding>(findings.ToImmutable());
    }

    /// <summary>
    /// Reports a regional set of the reworded set's assembly that no set of this assembly serves (<c>LOC208</c>).
    /// </summary>
    private static void ReportUnreachedRegionalSets(
        ImmutableArray<DiscoveredLocalization> ownSets,
        ImmutableArray<DiscoveredLocalization> ownRewordings,
        EquatableArray<ReferencedLocalization> references,
        ImmutableArray<LocalizationSetsFinding>.Builder findings)
    {
        foreach (var rewording in ownRewordings)
        {
            var reworded = rewording.Reworded!.Value;

            // A rewording in another language is LOC204.
            if (rewording.Language is not { } language || !String.Equals(reworded.Language, language, StringComparison.OrdinalIgnoreCase))
                continue;

            var setsOfAssembly = references
                .Where(reference => String.Equals(reference.AssemblyName, reworded.AssemblyName, StringComparison.Ordinal))
                .SelectMany(static reference => reference.Sets)
                .ToArray();

            var contracts = new HashSet<string>(
                setsOfAssembly.Where(set => String.Equals(set.Set, reworded.MetadataName, StringComparison.Ordinal)).Select(static set => set.Contract),
                StringComparer.Ordinal);

            foreach (var regional in setsOfAssembly.GroupBy(static set => set.Set, StringComparer.Ordinal).OrderBy(static group => group.Key, StringComparer.Ordinal))
            {
                var region = regional.First().Language;

                if (String.Equals(region, language, StringComparison.OrdinalIgnoreCase)
                    || !SupportedLanguages.Serves(language, region)
                    || !regional.Any(set => contracts.Contains(set.Contract)))
                {
                    continue;
                }

                // Any set of this assembly for the region, including one registered by hand.
                var isServed = ownSets.Any(set => set.Language is { } own
                                                  && String.Equals(own, region, StringComparison.OrdinalIgnoreCase)
                                                  && set.ContractNames.Any(contracts.Contains));

                if (isServed)
                    continue;

                var suggestedName = (LocalizationDiscovery.DeriveBaseName(rewording.Name, language) ?? rewording.Name) + SupportedLanguages.ToTypeSuffix(region);

                findings.Add(new LocalizationSetsFinding(
                    LocalizationSetsRule.UnreachedRegionalSet,
                    rewording.Location,
                    new EquatableArray<string>([rewording.Name, LocalizationDiscovery.GetSimpleName(reworded.MetadataName), region, LocalizationDiscovery.GetSimpleName(regional.Key), suggestedName])));
            }
        }
    }

    /// <summary>
    /// Reports two rewordings for one language that share a set, neither deriving from the other (<c>LOC209</c>): one
    /// of this assembly next to one of a referenced assembly, or two of referenced assemblies, which have no location.
    /// </summary>
    private static void ReportRewordingsOfReferencedSets(
        ImmutableArray<Rewording> own,
        ImmutableArray<Rewording> referenced,
        EquatableArray<ReferencedLocalization> references,
        ImmutableArray<LocalizationSetsFinding>.Builder findings)
    {
        var all = own.Concat(referenced).ToImmutableArray();

        for (var i = 0; i < all.Length; i++)
        {
            for (var j = Math.Max(i + 1, own.Length); j < all.Length; j++)
            {
                var (first, second) = (all[i], all[j]);

                if (Rewording.FindShared(first, second) is not { } shared)
                    continue;

                if (first.IsOwn)
                {
                    // This assembly's set registers after those of the assemblies it references, and an app's wins anyway.
                    findings.Add(new LocalizationSetsFinding(
                        LocalizationSetsRule.RewordingsDontAddUp,
                        first.Location,
                        new EquatableArray<string>([first.Name, second.Name, shared, $"'{first.Name}'", $"Derive '{first.Name}' from '{second.Name}', so both show"])));
                }
                // Two of one library are that library's to fix, and its own build reports them (LOC203, LOC209), so an
                // app that references it doesn't. A rewording of this assembly that derives from one of them is reported
                // itself, and shows anyway.
                else if (!String.Equals(first.AssemblyName, second.AssemblyName, StringComparison.Ordinal)
                         && !own.Any(rewording => rewording.DerivesFrom(first) || rewording.DerivesFrom(second)))
                {
                    var shown = FindShownReferenced(first, second, references);
                    var (named, other) = shown is null || ReferenceEquals(shown, first) ? (first, second) : (second, first);

                    findings.Add(new LocalizationSetsFinding(
                        LocalizationSetsRule.RewordingsDontAddUp,
                        default,
                        new EquatableArray<string>([named.Name, other.Name, shared, shown is null ? "one of them" : $"'{shown.Name}'", "Derive one from the other, so both show"])));
                }
            }
        }
    }

    /// <summary>
    /// Reports two rewordings of this assembly for one contract and language that share a set, neither deriving from
    /// the other (<c>LOC209</c>), at the one that doesn't show.
    /// </summary>
    private static void ReportOwnRewordingsThatDontAddUp(
        IReadOnlyList<DiscoveredLocalization> ofLanguage,
        HashSet<(string, string)> reportedPairs,
        ImmutableArray<LocalizationSetsFinding>.Builder findings)
    {
        var rewordings = ofLanguage
            .Where(static set => set.Reworded is not null)
            .OrderBy(static set => set.MetadataName, StringComparer.Ordinal)
            .Select(Rewording.Of)
            .ToArray();

        for (var i = 0; i < rewordings.Length; i++)
        {
            for (var j = i + 1; j < rewordings.Length; j++)
            {
                var (first, second) = (rewordings[i], rewordings[j]);

                if (Rewording.FindShared(first, second) is not { } shared || !reportedPairs.Add((first.Set, second.Set)))
                    continue;

                var (shown, hidden) = first.ComesLater(second) ? (first, second) : (second, first);

                findings.Add(new LocalizationSetsFinding(
                    LocalizationSetsRule.RewordingsDontAddUp,
                    hidden.Location,
                    new EquatableArray<string>([shown.Name, hidden.Name, shared, $"'{shown.Name}'", "Merge them into one set, or derive one from the other, so both show"])));
            }
        }
    }

    /// <summary>
    /// Which of two rewordings of different referenced assemblies shows: that of the assembly that references the
    /// other's, because it registers last. <see langword="null"/> where the build can't tell.
    /// </summary>
    private static Rewording? FindShownReferenced(Rewording first, Rewording second, EquatableArray<ReferencedLocalization> references)
    {
        if (References(first, second))
            return first;

        if (References(second, first))
            return second;

        return null;

        bool References(Rewording rewording, Rewording other)
        {
            return references.Any(reference => String.Equals(reference.AssemblyName, rewording.AssemblyName, StringComparison.Ordinal)
                                               && reference.References.Contains(other.AssemblyName!, StringComparer.Ordinal));
        }
    }

    /// <summary>
    /// A rewording as the checks compare it: of this assembly, with its location, or of a referenced one, with its
    /// assembly's name.
    /// </summary>
    /// <param name="Bases">The full names of the concrete sets it derives from, nearest first.</param>
    /// <param name="Depth">How many concrete sets it derives from, which orders its registration (<see cref="DiscoveredLocalization.Depth"/>).</param>
    private sealed record Rewording(string Set, string? Language, EquatableArray<string> Bases, int Depth, string? AssemblyName, DiagnosticLocation Location)
    {
        public string Name => LocalizationDiscovery.GetSimpleName(this.Set);

        public bool IsOwn => this.AssemblyName is null;

        public static Rewording Of(DiscoveredLocalization set)
        {
            return new Rewording(set.MetadataName, set.Language, set.BaseSetNames, set.Depth, AssemblyName: null, set.Location);
        }

        public static Rewording Of(ProvidedLocalizationSet set, string assemblyName)
        {
            return new Rewording(set.Set, set.Language, set.Bases, set.Bases.Length, assemblyName, Location: default);
        }

        public bool DerivesFrom(Rewording other)
        {
            var derivesFrom = this.Bases.Contains(other.Set, StringComparer.Ordinal);
            return derivesFrom;
        }

        /// <summary>
        /// Whether the generated registrar registers this rewording after the other, so it wins: by depth, then by name.
        /// </summary>
        public bool ComesLater(Rewording other)
        {
            var comesLater = this.Depth != other.Depth
                ? this.Depth > other.Depth
                : String.CompareOrdinal(this.Set, other.Set) > 0;

            return comesLater;
        }

        /// <summary>
        /// The name of the nearest set both rewordings derive from, where they serve one language and neither derives
        /// from the other. Else <see langword="null"/>.
        /// </summary>
        public static string? FindShared(Rewording first, Rewording second)
        {
            if (first.DerivesFrom(second) || second.DerivesFrom(first)
                || first.Language is not { } language
                || !String.Equals(second.Language, language, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var shared = first.Bases.FirstOrDefault(name => second.Bases.Contains(name, StringComparer.Ordinal));
            return shared is null ? null : LocalizationDiscovery.GetSimpleName(shared);
        }
    }
}
