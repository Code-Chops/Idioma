namespace CodeChops.Idioma.Generators;

/// <summary>
/// Reads <c>[assembly: SupportedLanguages(...)]</c>, and the rules every part of this project shares about language
/// codes.
/// </summary>
internal static class SupportedLanguages
{
    /// <summary>
    /// The languages the current compilation declares, or <see langword="null"/> when it declares none.
    /// </summary>
    public static IncrementalValueProvider<DeclaredLanguages?> Read(IncrementalGeneratorInitializationContext context)
    {
        return context.SyntaxProvider
            .ForAttributeWithMetadataName(
                fullyQualifiedMetadataName: PackageNames.SupportedLanguagesAttribute,
                predicate: static (node, _) => node is CompilationUnitSyntax,
                transform: static (context, _) => FromAttribute(context.Attributes[0]))
            .Collect()
            .Select(static (declarations, _) => declarations.Length > 0 ? declarations[0] : (DeclaredLanguages?)null);
    }

    /// <summary>
    /// The languages an assembly declares in its attribute, in order, or empty when it declares none.
    /// </summary>
    public static ImmutableArray<string> ReadCodes(IAssemblySymbol assembly)
    {
        foreach (var attribute in assembly.GetAttributes())
            if (String.Equals(attribute.AttributeClass?.ToDisplayString(), PackageNames.SupportedLanguagesAttribute, StringComparison.Ordinal))
                return ReadArguments(attribute).Select(static argument => argument.Code).OfType<string>().ToImmutableArray();

        return ImmutableArray<string>.Empty;
    }

    /// <summary>
    /// Whether a set for <paramref name="provided"/> serves <paramref name="requested"/>: the same language, or a culture
    /// that resolves through it at run time (<c>en</c> serves <c>en-GB</c>, and <c>zh-Hant</c> serves <c>zh-TW</c>).
    /// </summary>
    public static bool Serves(string provided, string requested)
    {
        var serves = String.Equals(provided, requested, StringComparison.OrdinalIgnoreCase)
               || requested.StartsWith(provided + "-", StringComparison.OrdinalIgnoreCase)
               || (FindScriptParentOfChineseRegion(requested) is { } parent && Serves(provided, parent));

        return serves;
    }

    /// <summary>
    /// The parent culture of a Chinese region written without its script, which its name does not start with:
    /// <c>zh-Hans</c> for <c>zh-CN</c> and <c>zh-SG</c>, and <c>zh-Hant</c> for <c>zh-HK</c>, <c>zh-MO</c>, and
    /// <c>zh-TW</c>; <see langword="null"/> for every other code, whose parents are what its name starts with.
    /// </summary>
    // The parents .NET itself gives these five on every platform (CultureInfo.Parent), which a text follows at run time.
    // Written out, and not asked of the cultures of this machine, because a compiler may run under invariant
    // globalization, and a build must not report on one machine what it accepts on another. SupportedLanguages.Tests.cs
    // fails when a runtime gives any culture a parent this table and the prefix rule don't.
    internal static string? FindScriptParentOfChineseRegion(string code)
    {
        if (code.Length is not 5 || !code.StartsWith("zh-", StringComparison.OrdinalIgnoreCase))
            return null;

        var parent = code.Substring(3).ToUpperInvariant() switch
        {
            "CN" or "SG" => "zh-Hans",
            "HK" or "MO" or "TW" => "zh-Hant",
            _ => null,
        };
        return parent;
    }

    /// <summary>
    /// The parameter a language has in the generated factory: the suffix of its set (<see cref="ToTypeSuffix"/>) in
    /// camelCase, so <c>nl</c> where a set ends in <c>Nl</c>, <c>enGb</c> for <c>EnGb</c>, and <c>zhHans</c> for
    /// <c>ZhHans</c>. A keyword is escaped: <c>@is</c>.
    /// </summary>
    public static string ToParameterName(string code)
    {
        var suffix = ToTypeSuffix(code);
        var name = suffix.Length is 0 ? suffix : Char.ToLowerInvariant(suffix[0]) + suffix.Substring(1);

        return SyntaxFacts.GetKeywordKind(name) is SyntaxKind.None ? name : $"@{name}";
    }

    /// <summary>
    /// The language a parameter of the generated factory stands for among the languages an assembly lists, such as
    /// <c>en-GB</c> for <c>enGb</c>. The name of the parameter itself when none of them gives it.
    /// </summary>
    public static string FindLanguageOfParameter(string parameterName, IEnumerable<string> codes)
    {
        // A symbol's name comes without the @ that escapes a keyword.
        foreach (var code in codes)
            if (String.Equals(ToParameterName(code).TrimStart('@'), parameterName, StringComparison.Ordinal))
                return code;

        return parameterName;
    }

    /// <summary>
    /// The suffix a set's name carries for a language, which is its code in PascalCase: <c>Nl</c> for <c>nl</c>,
    /// <c>EnGb</c> for <c>en-GB</c>, and <c>ZhHans</c> for <c>zh-Hans</c>. Each part of the code starts with a capital,
    /// and the rest of it is in lower case, however the attribute writes the code.
    /// </summary>
    public static string ToTypeSuffix(string code)
    {
        var suffix = new StringBuilder(code.Length);

        foreach (var part in code.Split('-'))
            if (part.Length > 0)
                suffix.Append(Char.ToUpperInvariant(part[0])).Append(part.Substring(1).ToLowerInvariant());

        return suffix.ToString();
    }

    /// <summary>
    /// The set to add for a language, as a diagnostic names it:
    /// <c>'class ShopLocalizationNl : ILocalizationSet, IShopLocalization'</c>.
    /// The name alone gives the language, also for a code that is not two letters (<c>ShopLocalizationEnGb</c>), as
    /// every language a diagnostic suggests a set for is one the assembly declares (<see cref="FindNamedLanguage"/>).
    /// </summary>
    /// <param name="setName">The name of the set to add.</param>
    /// <param name="contract">The contract the set implements.</param>
    /// <param name="setInterface">
    /// What the set lists before its contract: <c>ILocalizationSet</c>, or <c>ILocalizationSet&lt;OrderStatus&gt;</c>
    /// for a set of the texts of an enum.
    /// </param>
    public static string SuggestSet(string setName, string contract, string setInterface = SetInterface)
    {
        return $"'class {setName} : {setInterface}, {contract}'";
    }

    /// <summary>
    /// The interface every set lists, as a diagnostic and a code fix write it.
    /// </summary>
    public const string SetInterface = "ILocalizationSet";

    /// <summary>
    /// Whether a type's name ends in the suffix of a language (<see cref="ToTypeSuffix"/>), with something before it.
    /// The suffix is written in PascalCase (<c>ShopLocalizationNl</c>, <c>ShopLocalizationEnGb</c>). It counts in any
    /// letter case that starts with a capital, so <c>ShopLocalizationNL</c> and <c>ShopLocalizationENGB</c> end in it
    /// too.
    /// </summary>
    // The capital marks where the suffix starts. Without it MessagesAr, an Arabic set, would end in the suffix of es-AR
    // (the "es" of Messages, then Ar), and ReceiptBr, a Breton one, in that of pt-BR.
    public static bool EndsInTypeSuffix(string typeName, string code)
    {
        var suffix = ToTypeSuffix(code);

        // No suffix at all for a code without a letter or digit, which no name ends in.
        if (suffix.Length is 0 || typeName.Length <= suffix.Length)
            return false;

        return typeName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && typeName[typeName.Length - suffix.Length] is >= 'A' and <= 'Z';
    }

    /// <summary>
    /// The language a type's name gives it among declared languages that are not two letters: the longest code whose
    /// suffix the name ends in (<see cref="EndsInTypeSuffix"/>), such as <c>en-GB</c> for <c>ShopLocalizationEnGb</c>
    /// or <c>fil</c> for <c>ShopLocalizationFil</c>; <see langword="null"/> when none does. A two-letter code needs no
    /// such match, because the last two letters of a name give it at run time.
    /// </summary>
    // Only declared codes: a name alone can't tell ShopLocalizationEnGb (en-GB) from AdminUiNl (nl).
    public static string? FindNamedLanguage(string typeName, IEnumerable<string> codes)
    {
        string? found = null;

        foreach (var code in codes)
        {
            if (code.Length is 2 || !IsCultureName(code) || (found is not null && found.Length >= code.Length))
                continue;

            if (EndsInTypeSuffix(typeName, code))
                found = code;
        }

        return found;
    }

    private static DeclaredLanguages FromAttribute(AttributeData attribute)
    {
        var codes = ImmutableArray.CreateBuilder<string>();
        var problems = ImmutableArray.CreateBuilder<string>();
        string? defaultLanguage = null;

        foreach (var (code, isDefault) in ReadArguments(attribute))
        {
            if (code is null)
            {
                problems.Add("null isn't a culture name");
            }
            else if (!IsCultureName(code))
            {
                problems.Add($"'{code}' isn't a culture name");
            }
            else if (codes.Contains(code, StringComparer.OrdinalIgnoreCase))
            {
                problems.Add($"'{code}' is listed twice");
            }
            else if (codes.FirstOrDefault(listed => DifferInHyphensAlone(listed, code)) is { } other)
            {
                problems.Add($"'{code}' and '{other}' differ only in their hyphens");
            }
            else
            {
                codes.Add(code);

                if (isDefault)
                    defaultLanguage = code;
            }
        }

        // From the reference itself, which holds the tree and the span: the node isn't needed for its location.
        var location = attribute.ApplicationSyntaxReference is { } reference
            ? DiagnosticLocation.From(Location.Create(reference.SyntaxTree, reference.Span))
            : default;

        return new DeclaredLanguages(
            defaultLanguage,
            new EquatableArray<string>(codes.ToImmutable()),
            new EquatableArray<string>(problems.ToImmutable()),
            location);
    }

    /// <summary>
    /// The languages the attribute lists, in order, each with whether it is the default language, and with
    /// <see langword="null"/> for each one written as <c>null</c>; a list of other languages written as <c>null</c>
    /// counts as one. A constant the compiler can't read yet, while the user is still typing, is left out.
    /// </summary>
    private static IEnumerable<(string? Code, bool IsDefault)> ReadArguments(AttributeData attribute)
    {
        // By position: (string defaultLanguage, params string[] otherLanguages).
        if (attribute.ConstructorArguments.Length is 0)
            yield break;

        if (TryReadLanguage(attribute.ConstructorArguments[0], out var defaultLanguage))
            yield return (defaultLanguage, IsDefault: true);

        if (attribute.ConstructorArguments.Length < 2 || attribute.ConstructorArguments[1].Kind is not TypedConstantKind.Array)
            yield break;

        // SupportedLanguages("en", null) passes no array at all, whose Values would throw.
        if (attribute.ConstructorArguments[1].IsNull)
        {
            yield return (null, IsDefault: false);
            yield break;
        }

        foreach (var other in attribute.ConstructorArguments[1].Values)
            if (TryReadLanguage(other, out var language))
                yield return (language, IsDefault: false);

        static bool TryReadLanguage(TypedConstant constant, out string? language)
        {
            language = constant.Value as string;

            return constant.Kind is not TypedConstantKind.Error && (language is not null || constant.IsNull);
        }
    }

    /// <summary>
    /// Whether two languages are the same once their hyphens are left out, as <c>sr-Latn-RS</c> and <c>sr-LatnRS</c>
    /// are. A parameter and the suffix of a set are written without hyphens, so neither could tell the two apart.
    /// </summary>
    private static bool DifferInHyphensAlone(string code, string other)
    {
        return String.Equals(code.Replace("-", ""), other.Replace("-", ""), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A first part of two to eight ASCII letters, and further parts of one to eight ASCII letters or digits: what fits
    /// a parameter name, and what a culture name looks like. Not checked against the cultures of this machine, because
    /// a compiler may run under invariant globalization.
    /// </summary>
    // The rule CultureName.IsValid applies at run time, so that a code the build accepts is one the registry accepts.
    public static bool IsCultureName(string code)
    {
        var parts = code.Split('-');

        if (parts[0].Length is < 2 or > 8 || !parts[0].All(Ascii.IsLetter))
            return false;

        for (var i = 1; i < parts.Length; i++)
            if (parts[i].Length is 0 or > 8 || !parts[i].All(c => Ascii.IsLetter(c) || c is >= '0' and <= '9'))
                return false;

        return true;
    }
}

/// <summary>
/// What an assembly's attribute declares: the default language, the valid languages in order, what is wrong with the
/// others, and where the attribute is.
/// </summary>
/// <remarks>
/// The default language is <see langword="null"/> when the attribute's first argument is no language the build can use
/// (<c>LOC101</c>), or can't be read yet. The next language does not take its place: nothing is held to a default
/// language then, and the valid languages are all other languages. With a default language, it is the first of them.
/// </remarks>
internal readonly record struct DeclaredLanguages(
    string? DefaultLanguage,
    EquatableArray<string> Codes,
    EquatableArray<string> Problems,
    DiagnosticLocation Location);
