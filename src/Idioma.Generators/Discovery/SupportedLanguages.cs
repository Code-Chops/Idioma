using System.Runtime.CompilerServices;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Reads <c>[assembly: SupportedLanguages(...)]</c>, and the rules every part of this project shares about language
/// codes.
/// </summary>
internal static class SupportedLanguages
{
    /// <summary>
    /// What the attribute of an assembly declares, read once per assembly (<see cref="ReadAssembly"/>).
    /// </summary>
    // Discovery asks for the languages of the assembly for every set, on every edit, and the attribute is the same
    // for all of them. Held by the assembly's symbol, and holding only strings: the entry goes when the compilation
    // that made the symbol does, and keeps nothing of it alive.
    private static readonly ConditionalWeakTable<IAssemblySymbol, AssemblyLanguages> LanguagesByAssembly = new();

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
        var codes = LanguagesByAssembly.GetValue(assembly, ReadAssembly).Codes;
        return codes;
    }

    /// <summary>
    /// The default language an assembly declares, as <see cref="DeclaredLanguages.DefaultLanguage"/> reads it: the first
    /// language of its attribute, or <see langword="null"/> when that is no culture name, or when the assembly declares
    /// none.
    /// </summary>
    // Two attributes, which don't compile, give none, as the generators may read either.
    public static string? ReadDefaultLanguage(IAssemblySymbol assembly)
    {
        var defaultLanguage = LanguagesByAssembly.GetValue(assembly, ReadAssembly).DefaultLanguage;
        return defaultLanguage;
    }

    /// <summary>
    /// Reads the attribute of an assembly for <see cref="ReadCodes"/> and <see cref="ReadDefaultLanguage"/>.
    /// </summary>
    // It builds no string to tell the attribute by: an assembly has many attributes.
    private static AssemblyLanguages ReadAssembly(IAssemblySymbol assembly)
    {
        AttributeData? first = null;
        var count = 0;

        foreach (var attribute in assembly.GetAttributes())
        {
            if (attribute.AttributeClass is not { Name: "SupportedLanguagesAttribute", Arity: 0, ContainingType: null, ContainingNamespace: { Name: "Idioma", ContainingNamespace: { Name: "CodeChops", ContainingNamespace.IsGlobalNamespace: true } } })
                continue;

            first ??= attribute;
            count++;
        }

        if (first is null)
            return AssemblyLanguages.None;

        var codes = ReadArguments(first).Select(static argument => argument.Code).OfType<string>().ToImmutableArray();
        string? defaultLanguage = null;

        if (count is 1)
        {
            foreach (var (code, isDefault) in ReadArguments(first))
            {
                defaultLanguage = isDefault && code is not null && IsCultureName(code) ? code : null;
                break;
            }
        }

        return new AssemblyLanguages(codes, defaultLanguage);
    }

    /// <summary>
    /// What the attribute of an assembly declares: its languages in order, and its default language.
    /// </summary>
    private sealed class AssemblyLanguages(ImmutableArray<string> codes, string? defaultLanguage)
    {
        public static AssemblyLanguages None { get; } = new(ImmutableArray<string>.Empty, defaultLanguage: null);

        public ImmutableArray<string> Codes { get; } = codes;

        public string? DefaultLanguage { get; } = defaultLanguage;
    }

    /// <summary>
    /// Whether a set for <paramref name="provided"/> serves <paramref name="requested"/>: the same language, or a culture
    /// that resolves through it at run time (<c>en</c> serves <c>en-GB</c>, <c>zh-Hant</c> serves <c>zh-TW</c>, and
    /// <c>zh-TW</c> serves <c>zh-Hant-TW</c>).
    /// </summary>
    public static bool Serves(string provided, string requested)
    {
        var serves = String.Equals(provided, requested, StringComparison.OrdinalIgnoreCase)
               || requested.StartsWith(provided + "-", StringComparison.OrdinalIgnoreCase)
               || (FindScriptParentOfChineseRegion(requested) is { } parent && Serves(provided, parent))
               || (FindChineseRegionWithoutScript(requested) is { } region && Serves(provided, region));

        return serves;
    }

    /// <summary>
    /// A Chinese region written with its script, without that script: <c>zh-TW</c> for <c>zh-Hant-TW</c>, and
    /// <c>zh-CN</c> for <c>zh-Hans-CN</c>. At run time a text tries that name right after the culture. It's
    /// <see langword="null"/> for every other code, and for a region with the other script, such as <c>zh-Hant-CN</c>,
    /// which writes in another script than <c>zh-CN</c> does.
    /// </summary>
    // Only the Chinese regions of FindScriptParentOfChineseRegion, whose scripts are known without the cultures of this
    // machine. At run time a text compares the scripts of any language (CultureFallback), which a build can't, for the
    // reason given there.
    internal static string? FindChineseRegionWithoutScript(string code)
    {
        if (code.Length is not 10 || !code.StartsWith("zh-", StringComparison.OrdinalIgnoreCase) || code[7] is not '-')
            return null;

        var region = "zh-" + code.Substring(8);
        var isSameScript = FindScriptParentOfChineseRegion(region) is { } parent
                           && String.Equals(parent.Substring(3), code.Substring(3, 4), StringComparison.OrdinalIgnoreCase);

        return isSameScript ? region : null;
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

    /// <summary>
    /// The two-letter language codes of ISO 639-1, without the ones it withdrew, such as <c>iw</c> for Hebrew.
    /// </summary>
    // Written out, and not asked of the cultures of this machine, for the reason IsCultureName gives.
    // SupportedLanguages.Tests.cs fails when the runtime has a culture this table doesn't know.
    private static readonly HashSet<string> LanguageCodes = new(
        ("aa ab ae af ak am an ar as av ay az ba be bg bi bm bn bo br bs ca ce ch co cr cs cu cv cy da de dv dz "
         + "ee el en eo es et eu fa ff fi fj fo fr fy ga gd gl gn gu gv ha he hi ho hr ht hu hy hz "
         + "ia id ie ig ii ik io is it iu ja jv ka kg ki kj kk kl km kn ko kr ks ku kv kw ky "
         + "la lb lg li ln lo lt lu lv mg mh mi mk ml mn mr ms mt my na nb nd ne ng nl nn no nr nv ny "
         + "oc oj om or os pa pi pl ps pt qu rm rn ro ru rw sa sc sd se sg si sk sl sm sn so sq sr ss st su sv sw "
         + "ta te tg th ti tk tl tn to tr ts tt tw ty ug uk ur uz ve vi vo wa wo xh yi yo za zh zu").Split(' '),
        StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The two-letter region codes of ISO 3166-1, and the four others that .NET cultures use: <c>XK</c> for Kosovo,
    /// and <c>DG</c>, <c>EA</c>, and <c>IC</c> for parts of a country. <c>UK</c> isn't among them, because the United
    /// Kingdom is <c>GB</c>.
    /// </summary>
    private static readonly HashSet<string> RegionCodes = new(
        ("AD AE AF AG AI AL AM AO AQ AR AS AT AU AW AX AZ BA BB BD BE BF BG BH BI BJ BL BM BN BO BQ BR BS BT BV BW BY BZ "
         + "CA CC CD CF CG CH CI CK CL CM CN CO CR CU CV CW CX CY CZ DE DJ DK DM DO DZ EC EE EG EH ER ES ET "
         + "FI FJ FK FM FO FR GA GB GD GE GF GG GH GI GL GM GN GP GQ GR GS GT GU GW GY HK HM HN HR HT HU "
         + "ID IE IL IM IN IO IQ IR IS IT JE JM JO JP KE KG KH KI KM KN KP KR KW KY KZ "
         + "LA LB LC LI LK LR LS LT LU LV LY MA MC MD ME MF MG MH MK ML MM MN MO MP MQ MR MS MT MU MV MW MX MY MZ "
         + "NA NC NE NF NG NI NL NO NP NR NU NZ OM PA PE PF PG PH PK PL PM PN PR PS PT PW PY QA RE RO RS RU RW "
         + "SA SB SC SD SE SG SH SI SJ SK SL SM SN SO SR SS ST SV SX SY SZ TC TD TF TG TH TJ TK TL TM TN TO TR TT TV TW TZ "
         + "UA UG UM US UY UZ VA VC VE VG VI VN VU WF WS YE YT ZA ZM ZW "
         + "DG EA IC XK").Split(' '),
        StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The part of a culture name that no culture has, with what the message of <c>LOC104</c> says about it, or
    /// <see langword="null"/> when the build knows every part it checks. <c>dk</c> has the form of a culture name, but
    /// Danish is <c>da</c>, so a set for <c>dk</c> matches no user's culture.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only two parts are checked, each against a table the build carries itself:
    /// </para>
    /// <list type="bullet">
    /// <item>A language of two letters is a code of ISO 639-1.</item>
    /// <item>A region of two letters is a code of ISO 3166-1, or one of the four others .NET uses.</item>
    /// </list>
    /// <para>
    /// A language of three letters or more, a script, a region of three digits such as <c>419</c>, and everything after
    /// the region aren't checked. The first unknown part is given, the language before the region.
    /// </para>
    /// </remarks>
    /// <param name="code">A culture name (<see cref="IsCultureName"/>).</param>
    public static UnknownLanguagePart? FindUnknownPart(string code)
    {
        var parts = code.Split('-');
        var language = parts[0];

        if (language.Length is 2 && !LanguageCodes.Contains(language))
        {
            var meant = language.ToLowerInvariant() switch
            {
                "cn" => ("Chinese", "zh"),
                "cz" => ("Czech", "cs"),
                "dk" => ("Danish", "da"),
                "gr" => ("Greek", "el"),
                "in" => ("Indonesian", "id"),
                "iw" => ("Hebrew", "he"),
                "ji" => ("Yiddish", "yi"),
                "jp" => ("Japanese", "ja"),
                "jw" => ("Javanese", "jv"),
                "ua" => ("Ukrainian", "uk"),
                _ => ((string Name, string Code)?)null,
            };

            return new UnknownLanguagePart(code, language, "ISO 639-1 language", Advise(parts, index: 0, meant));
        }

        // A region stands right after the language, or after the script that follows it: en-GB, sr-Latn-RS. Two letters
        // further on are something else, as the 'ca' of en-US-u-ca-gregory.
        var regionIndex = parts.Length > 2 && parts[1].Length is 4 ? 2 : 1;

        if (parts.Length > regionIndex && parts[regionIndex] is { Length: 2 } region && region.All(Ascii.IsLetter) && !RegionCodes.Contains(region))
        {
            var meant = region.ToUpperInvariant() switch
            {
                // Ukrainian, and not English, so the region meant is Ukraine.
                "UK" when String.Equals(language, "uk", StringComparison.OrdinalIgnoreCase) => ("Ukraine", "UA"),
                "UK" => ("The United Kingdom", "GB"),
                "CS" => ("Czechia", "CZ"),
                "DA" => ("Denmark", "DK"),
                "EL" => ("Greece", "GR"),
                "HE" => ("Israel", "IL"),
                "JA" => ("Japan", "JP"),
                "KO" => ("South Korea", "KR"),
                "ZH" => ("China", "CN"),
                _ => ((string Name, string Code)?)null,
            };

            return new UnknownLanguagePart(code, region, "ISO 3166-1 region", Advise(parts, regionIndex, meant));
        }

        return null;

        // The whole code with the part put right, so that it can be copied: 'en-GB' for 'en-UK'.
        static string Advise(string[] parts, int index, (string Name, string Code)? meant)
        {
            if (meant is not { } known)
                return $"Check the code. If it's right, turn {LocalizableTextFactoryGenerator.UnknownLanguageCodeDiagnosticId} off where it's reported";

            var corrected = String.Join("-", parts.Select((part, i) => i == index ? known.Code : part));
            return $"{known.Name} is '{known.Code}', so write '{corrected}'";
        }
    }
}

/// <summary>
/// A part of a culture name that no culture has: the whole code, the part, the kind of code the part should be, as
/// the message of <c>LOC104</c> names it (<c>ISO 639-1 language</c>), and its advice, which names the code that was
/// likely meant when the mistake is a known one.
/// </summary>
internal readonly record struct UnknownLanguagePart(
    string Code,
    string Part,
    string Kind,
    string Advice);

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
