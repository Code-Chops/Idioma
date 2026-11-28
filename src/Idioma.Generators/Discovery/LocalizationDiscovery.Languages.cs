namespace CodeChops.Idioma.Generators;

// Reads the language of a set from its name, which the registrar describes every set with, because the registry reads
// no name.
/// <inheritdoc cref="LocalizationDiscovery"/>
internal static partial class LocalizationDiscovery
{
    /// <summary>
    /// The language a set of this compilation serves, or <see langword="null"/>, for a message that names it.
    /// </summary>
    public static string? ReadLanguage(INamedTypeSymbol set, Compilation compilation, CancellationToken cancellationToken)
    {
        var reader = new WordingReader(compilation);
        var language = GetLanguage(set, GetSourceAncestors(set, reader, cancellationToken));
        return language;
    }

    /// <summary>
    /// The language the set serves, or <see langword="null"/> when it has none:
    /// <list type="number">
    /// <item>The code that the name of the set, or of the nearest set it derives from, gives among the codes of that set's
    /// assembly (<see cref="SupportedLanguages.FindNamedLanguage"/>): <c>ShopLocalizationEnGb</c> gives
    /// <c>en-GB</c>. A set it derives from gives its code unless the set's own last two letters are another language
    /// the assembly declares (<see cref="IsOtherDeclaredLanguage"/>). So <c>ShopLocalizationDe : ShopLocalizationEnGb</c>
    /// stays German where <c>de</c> is declared, and <c>GiftLocalizationUk : ShopLocalizationEnGb</c> is British where
    /// <c>uk</c> isn't.</item>
    /// <item>Else the last two letters of the set's own name (<see cref="GetNameLanguage"/>).</item>
    /// <item>Else the language of the nearest set it derives from, as the registry reads a set that wasn't described.</item>
    /// </list>
    /// </summary>
    /// <param name="ancestors">The classes of this assembly it derives from, nearest first.</param>
    private static string? GetLanguage(INamedTypeSymbol type, ImmutableArray<SourceAncestor> ancestors)
    {
        var nameLanguage = GetNameLanguage(type.Name);

        var index = -1;
        for (var current = type; current is { TypeKind: TypeKind.Class, SpecialType: not SpecialType.System_Object }; current = current.BaseType, index++)
        {
            if ((index < 0 || IsNameableSetAt(current, index, ancestors)) && FindNamedLanguageCode(current) is { } named)
            {
                if (index < 0 || nameLanguage is null || !IsOtherDeclaredLanguage(nameLanguage, named, type))
                    return named;

                // Another language: the set's own name decides, and LOC204 reports what it derives from.
                break;
            }
        }

        if (nameLanguage is not null)
            return nameLanguage;

        index = 0;
        for (var current = type.BaseType; current is { TypeKind: TypeKind.Class, SpecialType: not SpecialType.System_Object }; current = current.BaseType, index++)
        {
            if (IsNameableSetAt(current, index, ancestors) && GetLanguage(current, [.. ancestors.Skip(index + 1)]) is { } inherited)
                return inherited;
        }

        return null;
    }

    /// <summary>
    /// Whether a base class, <paramref name="index"/> steps above the set, is a set generated code can name. A class of
    /// this assembly implementing a contract generated here is a set as well (see <see cref="GetDepth"/>).
    /// </summary>
    /// <param name="ancestors">The classes of this assembly the set derives from, nearest first.</param>
    private static bool IsNameableSetAt(INamedTypeSymbol baseType, int index, ImmutableArray<SourceAncestor> ancestors)
    {
        var isSet = ImplementsLocalizationSet(baseType)
                    || (index < ancestors.Length && ancestors.Skip(index).Any(static ancestor => ancestor.UnresolvedBaseTypes.Length > 0));

        return isSet && (IsNameableSet(baseType) || IsNameableNestedSet(baseType));
    }

    /// <summary>
    /// The last two letters of a name in lower case, when they're ASCII letters and the first is a capital, else
    /// <see langword="null"/>. <c>ShopLocalizationNl</c> gives <c>nl</c>.
    /// </summary>
    private static string? GetNameLanguage(string name)
    {
        var language = EndsInTwoLetterSuffix(name)
            ? name.Substring(name.Length - 2).ToLowerInvariant()
            : null;

        return language;
    }

    /// <summary>
    /// Whether the last two letters of a set's name are a language its assembly declares, other than the longer code of
    /// a set it derives from. They're not where they're its language, as <c>en</c> is of <c>en-GB</c>, or the end of its
    /// suffix, as <c>Gb</c> is of <c>EnGb</c>. A language that isn't declared, as <c>uk</c> in a name meant for the
    /// United Kingdom, doesn't count either.
    /// </summary>
    private static bool IsOtherDeclaredLanguage(string nameLanguage, string inherited, INamedTypeSymbol set)
    {
        if (SupportedLanguages.Serves(nameLanguage, inherited)
            || SupportedLanguages.ToTypeSuffix(inherited).EndsWith(nameLanguage, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var isDeclared = SupportedLanguages.ReadCodes(set.ContainingAssembly)
            .Any(code => SupportedLanguages.Serves(code, nameLanguage) || SupportedLanguages.Serves(nameLanguage, code));

        return isDeclared;
    }

    /// <summary>
    /// The code a set's name gives it among the codes its assembly declares (<see cref="SupportedLanguages.FindNamedLanguage"/>),
    /// or <see langword="null"/>. <c>ShopLocalizationEnGb</c> gives <c>en-GB</c> where the assembly declares <c>en-GB</c>,
    /// or only <c>en</c>.
    /// </summary>
    private static string? FindNamedLanguageCode(INamedTypeSymbol type)
    {
        var languageCode = SupportedLanguages.FindNamedLanguage(type.Name, SupportedLanguages.ReadCodes(type.ContainingAssembly));
        return languageCode;
    }

    // The capital marks where the code starts, so LocalizationSet isn't a set for Estonian (et).
    private static bool EndsInTwoLetterSuffix(string name)
    {
        var endsInTwoLetterSuffix = name.Length >= 2 && Ascii.IsLetter(name[name.Length - 1]) && name[name.Length - 2] is >= 'A' and <= 'Z';
        return endsInTwoLetterSuffix;
    }
}
