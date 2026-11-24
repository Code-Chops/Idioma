namespace CodeChops.Idioma.Generators;

internal sealed partial record LocalizationSetsAnalysis
{
    /// <summary>
    /// The name to give a default-language set whose name does not end in the suffix of its language: the suffix in
    /// place of an ending that spells it without a capital (<c>ShopLocalizationen</c> becomes
    /// <c>ShopLocalizationEn</c>), and else the suffix added (<c>ShopTexts</c> becomes <c>ShopTextsEn</c>).
    /// </summary>
    private static string SuggestNameEndingIn(string name, string suffix)
    {
        var suggestedName = name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? name.Substring(startIndex: 0, name.Length - suffix.Length) + suffix
            : name + suffix;

        return suggestedName;
    }

    /// <summary>
    /// The set to add for a language a contract of this assembly misses: as the diagnostic names it, and as the code fix
    /// writes it (<see cref="NewLocalizationSet"/>). A region of a language that has a set derives from that set, to hold
    /// only the texts that differ: <c>'class ShopLocalizationEnGb : ShopLocalizationEn'</c>. Any other language
    /// implements the contract.
    /// </summary>
    /// <param name="setName">The name of the set to add.</param>
    /// <param name="setInterface">What the set lists before its contract (<see cref="GetSetInterface"/>).</param>
    /// <param name="contract">The contract as the namespace of the new set names it.</param>
    /// <param name="code">The language the set is for.</param>
    /// <param name="namespace">The namespace of the new set: that of the contract.</param>
    /// <param name="isPublic">Whether a set that implements the contract is public, as the contract's sets are.</param>
    /// <param name="sets">The sets of the contract, among which a set of a language above the region.</param>
    private static (string Suggestion, NewLocalizationSet NewSet) SuggestSet(string setName, string setInterface, string contract, string code, string @namespace, bool isPublic,
        IEnumerable<DiscoveredLocalization> sets)
    {
        // The nearest one: zh-Hans before zh for zh-Hans-CN. A sealed set has nothing deriving from it.
        var above = sets
            .Where(set => set is { Language: not null, IsSealed: false } && !IsSetOf(set, code) && SupportedLanguages.Serves(set.Language, code))
            .OrderByDescending(static set => set.Language!.Length)
            .ThenBy(static set => set.MetadataName, StringComparer.Ordinal)
            .FirstOrDefault();

        if (above.Language is null)
        {
            var declaration = $"{(isPublic ? "public" : "internal")} class {setName} : {setInterface}, {contract}";
            return (SupportedLanguages.SuggestSet(setName, contract, setInterface), new NewLocalizationSet(@namespace, declaration));
        }

        // As accessible as the set it derives from, which a derived class can't exceed. Named by its full name where it
        // lies in another namespace than the new set.
        var keyword = above.IsRecord ? "record" : "class";
        var baseClass = String.Equals(above.Namespace, @namespace, StringComparison.Ordinal)
            ? CodeEscaping.EscapeIdentifier(above.Name)
            : above.FullyQualifiedName;

        var suggestion = $"'{keyword} {setName} : {above.Name}', which keeps the wording of '{above.Language}' for every text it doesn't override";
        var derived = $"{(above.IsPublic ? "public" : "internal")} {keyword} {setName} : {baseClass}";

        return (suggestion, new NewLocalizationSet(@namespace, derived));
    }

    /// <summary>
    /// What a set of a contract lists before the contract, as its default-language set does: <c>ILocalizationSet</c>,
    /// or <c>ILocalizationSet&lt;OrderStatus&gt;</c> for a set of the texts of an enum. The enum stands by its name
    /// alone where it lies in the namespace of the set, and by its full name elsewhere.
    /// </summary>
    private static string GetSetInterface(DiscoveredLocalization defaultSet)
    {
        if (defaultSet.OfContract.Enums.Length is 0)
            return SupportedLanguages.SetInterface;

        var setInterface = String.Join(", ", defaultSet.OfContract.Enums.Select(@enum =>
        {
            var name = String.Equals(@enum.Namespace, defaultSet.Namespace, StringComparison.Ordinal)
                ? @enum.Name
                : @enum.FullyQualifiedName;

            return $"{SupportedLanguages.SetInterface}<{name}>";
        }));

        return setInterface;
    }

    /// <summary>
    /// The set an app would add for a contract of a library: <c>GridLocalizationDe</c> for <c>Lib.IGridLocalization</c>.
    /// </summary>
    private static string SuggestSetName(string contract, string code)
    {
        var setName = $"{GetContractBaseName(contract)}{SupportedLanguages.ToTypeSuffix(code)}";
        return setName;
    }

    /// <summary>
    /// The name a contract's sets share before their language suffix: <c>GridLocalization</c> for
    /// <c>Lib.IGridLocalization</c>.
    /// </summary>
    internal static string GetContractBaseName(string contract)
    {
        var name = contract.Substring(contract.LastIndexOf('.') + 1);

        var baseName = name.Length > 1 && name[0] == 'I' && Char.IsUpper(name[1]) ? name.Substring(1) : name;
        return baseName;
    }
}
