using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Operations;

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
    // Discovery asks for it for every set on every edit. Held by the assembly's symbol and holding only strings, so it
    // goes with the compilation and keeps nothing of it alive.
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
    /// The default language an assembly declares (<see cref="DeclaredLanguages.DefaultLanguage"/>), or
    /// <see langword="null"/> when it's no culture name or there is none.
    /// </summary>
    // Two attributes don't compile and give none, because the generators may read either.
    public static string? ReadDefaultLanguage(IAssemblySymbol assembly)
    {
        var defaultLanguage = LanguagesByAssembly.GetValue(assembly, ReadAssembly).DefaultLanguage;
        return defaultLanguage;
    }

    /// <summary>
    /// Reads the attribute of an assembly for <see cref="ReadCodes"/> and <see cref="ReadDefaultLanguage"/>.
    /// </summary>
    // In a compilation, the attribute is found by its syntax and bound alone, because GetAttributes binds every assembly
    // attribute, one per set included. Where the syntax shows none, as through an alias, GetAttributes decides.
    private static AssemblyLanguages ReadAssembly(IAssemblySymbol assembly)
    {
        if (assembly is ISourceAssemblySymbol { Compilation: { } compilation } && ReadFromSyntax(compilation) is { } read)
            return read;

        AttributeData? first = null;
        var count = 0;

        foreach (var attribute in assembly.GetAttributes())
        {
            if (!IsPackageAttribute(attribute.AttributeClass))
                continue;

            first ??= attribute;
            count++;
        }

        return first is null ? AssemblyLanguages.None : ToLanguages([.. ReadArguments(first)], count);
    }

    /// <summary>
    /// What the attribute of a compilation's own assembly declares, read from its syntax, or <see langword="null"/> when
    /// no assembly attribute is written by that name.
    /// </summary>
    private static AssemblyLanguages? ReadFromSyntax(Compilation compilation)
    {
        ImmutableArray<(string? Code, bool IsDefault)>? first = null;
        var count = 0;

        foreach (var tree in compilation.SyntaxTrees)
        {
            if (tree.GetRoot() is not CompilationUnitSyntax { AttributeLists.Count: > 0 } root)
                continue;

            SemanticModel? model = null;

            foreach (var list in root.AttributeLists)
            {
                if (list.Target?.Identifier.IsKind(SyntaxKind.AssemblyKeyword) is not true)
                    continue;

                foreach (var attribute in list.Attributes)
                {
                    // By its name first, which binds nothing.
                    if (MethodWording.GetRightmostName(attribute.Name) is not ("SupportedLanguages" or "SupportedLanguagesAttribute"))
                        continue;

                    model ??= compilation.GetSemanticModel(tree);

                    if (model.GetOperation(attribute) is not IAttributeOperation { Operation: IObjectCreationOperation { Constructor: { } constructor } creation }
                        || !IsPackageAttribute(constructor.ContainingType))
                    {
                        continue;
                    }

                    first ??= ReadArguments(creation).ToImmutableArray();
                    count++;
                }
            }
        }

        return first is { } arguments ? ToLanguages(arguments, count) : null;
    }

    private static bool IsPackageAttribute(INamedTypeSymbol? type)
    {
        var isPackageAttribute = type is { Name: "SupportedLanguagesAttribute", Arity: 0, ContainingType: null, ContainingNamespace: { Name: "Idioma", ContainingNamespace: { Name: "CodeChops", ContainingNamespace.IsGlobalNamespace: true } } };
        return isPackageAttribute;
    }

    /// <summary>
    /// What the first attribute of an assembly declares. With more than one, there's no default language.
    /// </summary>
    private static AssemblyLanguages ToLanguages(ImmutableArray<(string? Code, bool IsDefault)> arguments, int count)
    {
        var codes = arguments.Select(static argument => argument.Code).OfType<string>().ToImmutableArray();
        string? defaultLanguage = null;

        if (count is 1 && arguments is [var (code, isDefault), ..])
            defaultLanguage = isDefault && code is not null && IsCultureName(code) ? code : null;

        return new AssemblyLanguages(codes, defaultLanguage);
    }

    private sealed class AssemblyLanguages(ImmutableArray<string> codes, string? defaultLanguage)
    {
        public static AssemblyLanguages None { get; } = new(ImmutableArray<string>.Empty, defaultLanguage: null);

        public ImmutableArray<string> Codes { get; } = codes;

        public string? DefaultLanguage { get; } = defaultLanguage;
    }

    /// <summary>
    /// Whether a set for <paramref name="provided"/> serves <paramref name="requested"/>: <c>en</c> serves <c>en-GB</c>,
    /// <c>zh-Hant</c> serves <c>zh-TW</c>, and <c>zh-TW</c> serves <c>zh-Hant-TW</c>.
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
    /// A Chinese region with its script, without that script: <c>zh-TW</c> for <c>zh-Hant-TW</c>. At run time a text
    /// tries that name right after the culture. <see langword="null"/> for any other code, and for a region with the
    /// other script, such as <c>zh-Hant-CN</c>.
    /// </summary>
    // Only Chinese, whose regions have a script as their parent culture. At run time CultureFallback compares the scripts
    // the build passes for the app's languages (LikelyScripts).
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
    /// The parent culture of a Chinese region without its script, which its name doesn't start with: <c>zh-Hans</c> for
    /// <c>zh-CN</c> and <c>zh-SG</c>, and <c>zh-Hant</c> for <c>zh-HK</c>, <c>zh-MO</c> and <c>zh-TW</c>. Else
    /// <see langword="null"/>.
    /// </summary>
    // The parents .NET gives these on every platform. Written out, because a compiler may run under invariant
    // globalization, and a build must report the same on every machine. SupportedLanguages.Tests.cs fails when a runtime
    // gives a culture a parent this table and the prefix rule don't.
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
    /// The parameter a language has in the generated factory: its set's suffix (<see cref="ToTypeSuffix"/>) in
    /// camelCase, such as <c>enGb</c>. A keyword is escaped: <c>@is</c>.
    /// </summary>
    public static string ToParameterName(string code)
    {
        var suffix = ToTypeSuffix(code);
        var name = suffix.Length is 0 ? suffix : Char.ToLowerInvariant(suffix[0]) + suffix.Substring(1);

        return SyntaxFacts.GetKeywordKind(name) is SyntaxKind.None ? name : $"@{name}";
    }

    /// <summary>
    /// The language a parameter of the generated factory stands for, such as <c>en-GB</c> for <c>enGb</c>, else the
    /// parameter's name.
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
    /// The suffix a set's name carries for a language, which is its code in PascalCase: <c>Nl</c> for <c>nl</c>, and
    /// <c>EnGb</c> for <c>en-GB</c>, however the attribute writes the code.
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
    /// <c>'class ShopLocalizationNl : ILocalizationSet, IShopLocalization'</c>. The name alone gives the language,
    /// because a suggested language is always declared (<see cref="FindNamedLanguage"/>).
    /// </summary>
    /// <param name="setInterface">
    /// What the set lists before its contract, such as <c>ILocalizationSet&lt;OrderStatus&gt;</c> for a set of an enum.
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
    /// Whether a type's name ends in the suffix of a language (<see cref="ToTypeSuffix"/>), with something before it. Any
    /// letter case counts if it starts with a capital, so <c>ShopLocalizationNL</c> does.
    /// </summary>
    // The capital marks where the suffix starts, so MessagesAr is Arabic and not es-AR.
    public static bool EndsInTypeSuffix(string typeName, string code)
    {
        var suffix = ToTypeSuffix(code);

        if (suffix.Length is 0 || typeName.Length <= suffix.Length)
            return false;

        return typeName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && typeName[typeName.Length - suffix.Length] is >= 'A' and <= 'Z';
    }

    /// <summary>
    /// The code a type's name gives among the declared codes, or <see langword="null"/> when only its last two letters
    /// can give one:
    /// <list type="number">
    /// <item>The longest declared code of more than two letters whose suffix the name ends in
    /// (<see cref="EndsInTypeSuffix"/>), such as <c>en-GB</c> for <c>ShopLocalizationEnGb</c>.</item>
    /// <item>Else a declared code without a region, followed by a region, such as <c>en-GB</c> for
    /// <c>ShopLocalizationEnGb</c> where only <c>en</c> is declared. The region is two letters that start with a capital,
    /// and a region a culture has (<see cref="RegionCodes"/>).</item>
    /// </list>
    /// The second rule gives way where the last two letters are another declared code. So <c>YesNoDe</c> is German where
    /// <c>no</c> and <c>de</c> are declared, and <c>ShopLocalizationDeDe</c> stays <c>de-DE</c>.
    /// </summary>
    // Only declared languages, because a name alone can't tell ShopLocalizationEnGb (en-GB) from AdminUiNl (nl).
    public static string? FindNamedLanguage(string typeName, IEnumerable<string> codes)
    {
        string? found = null;
        string? language = null;
        var region = typeName.Length > 2 && typeName[typeName.Length - 2] is >= 'A' and <= 'Z' && Ascii.IsLetter(typeName[typeName.Length - 1])
            ? typeName.Substring(typeName.Length - 2).ToUpperInvariant()
            : null;
        var withoutRegion = region is not null && RegionCodes.Contains(region) ? typeName.Substring(0, typeName.Length - 2) : null;
        var regionIsDeclared = false;

        foreach (var code in codes)
        {
            regionIsDeclared |= String.Equals(code, region, StringComparison.OrdinalIgnoreCase);

            if (!IsCultureName(code))
                continue;

            if (code.Length > 2 && (found is null || found.Length < code.Length) && EndsInTypeSuffix(typeName, code))
                found = code;

            if (withoutRegion is not null && !HasRegion(code) && (language is null || language.Length < code.Length) && EndsInTypeSuffix(withoutRegion, code))
                language = code;
        }

        // Another declared language at the end wins, as it did before the region rule. Declaring fr-CA gives
        // ShopLocalizationFrCa its region where ca is declared too.
        if (language is not null && regionIsDeclared && !String.Equals(language, region, StringComparison.OrdinalIgnoreCase))
            language = null;

        return found ?? (language is null ? null : $"{language}-{region}");
    }

    /// <summary>
    /// Whether a culture name has a region: two letters or three digits after its language or script, as in <c>en-GB</c>
    /// and <c>es-419</c>.
    /// </summary>
    private static bool HasRegion(string code)
    {
        var parts = code.Split('-');
        var hasRegion = parts.Skip(1).Any(static part => (part.Length is 2 && part.All(Ascii.IsLetter)) || (part.Length is 3 && part.All(static c => c is >= '0' and <= '9')));

        return hasRegion;
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
    /// The languages the attribute lists, in order, with <see langword="null"/> for one written as <c>null</c>. A null
    /// list of other languages counts as one. A constant the compiler can't read yet is left out.
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

        // SupportedLanguages("en", null) passes no array, whose Values would throw.
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
    /// The languages of the attribute as its syntax binds them (<see cref="ReadFromSyntax"/>), read as
    /// <see cref="ReadArguments(AttributeData)"/> reads them.
    /// </summary>
    private static IEnumerable<(string? Code, bool IsDefault)> ReadArguments(IObjectCreationOperation creation)
    {
        // By parameter, because named arguments can come in another order.
        if (creation.Arguments.FirstOrDefault(static argument => argument.Parameter?.Ordinal is 0) is not { } first)
            yield break;

        if (TryReadLanguage(first.Value, out var defaultLanguage))
            yield return (defaultLanguage, IsDefault: true);

        var others = creation.Arguments.FirstOrDefault(static argument => argument.Parameter?.Ordinal is 1)?.Value;

        while (others is IConversionOperation { ConstantValue.HasValue: false } conversion)
            others = conversion.Operand;

        IEnumerable<IOperation> elements;

        switch (others)
        {
            case null:
                yield break;

            // SupportedLanguages("en", null) passes no array at all.
            case { ConstantValue: { HasValue: true, Value: null } }:
                yield return (null, IsDefault: false);
                yield break;

            case IArrayCreationOperation { Initializer: { } initializer }:
                elements = initializer.ElementValues;
                break;

            case ICollectionExpressionOperation collection:
                elements = collection.Elements;
                break;

            default:
                yield break;
        }

        foreach (var element in elements)
            if (TryReadLanguage(element, out var language))
                yield return (language, IsDefault: false);

        static bool TryReadLanguage(IOperation value, out string? language)
        {
            language = value.ConstantValue.HasValue ? value.ConstantValue.Value as string : null;

            return value.ConstantValue.HasValue && (language is not null || value.ConstantValue.Value is null);
        }
    }

    /// <summary>
    /// Whether two languages are the same without hyphens, as <c>sr-Latn-RS</c> and <c>sr-LatnRS</c> are. A parameter
    /// and a set's suffix have no hyphens, so they couldn't tell them apart.
    /// </summary>
    private static bool DifferInHyphensAlone(string code, string other)
    {
        return String.Equals(code.Replace("-", ""), other.Replace("-", ""), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Whether a code has the form of a culture name: a first part of two to eight ASCII letters, and further parts of one
    /// to eight ASCII letters or digits. Not checked against the machine's cultures, because a compiler may run under
    /// invariant globalization.
    /// </summary>
    // The only check of a code, because the registry takes the codes the generated code passes as they are.
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
    /// Returns a code in the letter case that culture names use, so <c>ZH-hans-tw</c> becomes <c>zh-Hans-TW</c>. Each
    /// part is cased as follows:
    /// <list type="bullet">
    /// <item>The language is lower case.</item>
    /// <item>Each later part of four letters is capitalized, as a script is.</item>
    /// <item>Each later part of two letters is upper case, as a region is.</item>
    /// <item>Every other part is lower case.</item>
    /// </list>
    /// Every part after a part of one character, such as an extension or private use, is lower case. So
    /// <c>EN-ca-X-CA</c> becomes <c>en-CA-x-ca</c>.
    /// </summary>
    // RFC 5646 (section 2.1.1), not CultureInfo.Name, which depends on the machine. The generated code passes every code
    // cased this way, so the registry compares them as they are.
    public static string Normalize(string code)
    {
        var parts = code.Split('-');
        var isAfterSingleCharacter = false;

        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            var hasCaseOfItsOwn = i > 0 && !isAfterSingleCharacter;

            if (hasCaseOfItsOwn && part.Length is 2 && part.All(Ascii.IsLetter))
                parts[i] = part.ToUpperInvariant();
            else if (hasCaseOfItsOwn && part.Length is 4 && part.All(Ascii.IsLetter))
                parts[i] = Char.ToUpperInvariant(part[0]) + part.Substring(1).ToLowerInvariant();
            else
                parts[i] = part.ToLowerInvariant();

            isAfterSingleCharacter |= part.Length is 1;
        }

        return String.Join("-", parts);
    }

    /// <summary>
    /// The two-letter language codes of ISO 639-1, without the ones it withdrew, such as <c>iw</c> for Hebrew.
    /// </summary>
    // Written out, for the reason IsCultureName gives. SupportedLanguages.Tests.cs fails when the runtime has a culture
    // this table doesn't know.
    private static readonly HashSet<string> LanguageCodes = new(
        ("aa ab ae af ak am an ar as av ay az ba be bg bi bm bn bo br bs ca ce ch co cr cs cu cv cy da de dv dz "
         + "ee el en eo es et eu fa ff fi fj fo fr fy ga gd gl gn gu gv ha he hi ho hr ht hu hy hz "
         + "ia id ie ig ii ik io is it iu ja jv ka kg ki kj kk kl km kn ko kr ks ku kv kw ky "
         + "la lb lg li ln lo lt lu lv mg mh mi mk ml mn mr ms mt my na nb nd ne ng nl nn no nr nv ny "
         + "oc oj om or os pa pi pl ps pt qu rm rn ro ru rw sa sc sd se sg si sk sl sm sn so sq sr ss st su sv sw "
         + "ta te tg th ti tk tl tn to tr ts tt tw ty ug uk ur uz ve vi vo wa wo xh yi yo za zh zu").Split(' '),
        StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The two-letter region codes of ISO 3166-1, and the four others .NET cultures use: <c>XK</c>, <c>DG</c>,
    /// <c>EA</c> and <c>IC</c>. The United Kingdom is <c>GB</c>, not <c>UK</c>.
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
    /// The first part of a culture name that no culture has, for <c>LOC103</c>, or <see langword="null"/>. <c>dk</c> has
    /// the form of a culture name, but Danish is <c>da</c>, so a set for <c>dk</c> matches no user's culture.
    /// </summary>
    /// <remarks>
    /// Only a two-letter language (<see cref="LanguageCodes"/>) and a two-letter region (<see cref="RegionCodes"/>) are
    /// checked.
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

            return new UnknownLanguagePart(code, language, "ISO 639-1 language", Advise(parts, index: 0, meant), Correct(parts, index: 0, meant));
        }

        // A region follows the language or its script: en-GB, sr-Latn-RS. Two letters further on are something else, as
        // the 'ca' of en-US-u-ca-gregory.
        var regionIndex = parts.Length > 2 && parts[1].Length is 4 ? 2 : 1;

        if (parts.Length > regionIndex && parts[regionIndex] is { Length: 2 } region && region.All(Ascii.IsLetter) && !RegionCodes.Contains(region))
        {
            var meant = region.ToUpperInvariant() switch
            {
                // uk-UK is Ukrainian, so the region meant is Ukraine.
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

            return new UnknownLanguagePart(code, region, "ISO 3166-1 region", Advise(parts, regionIndex, meant), Correct(parts, regionIndex, meant));
        }

        return null;

        // The whole code corrected, so it can be copied: 'en-GB' for 'en-UK'.
        static string Advise(string[] parts, int index, (string Name, string Code)? meant)
        {
            if (meant is not { } known)
                return $"Check the code. If it's right, turn {LocalizedTextFactoryGenerator.UnknownLanguageCodeDiagnosticId} off where it's reported";

            return $"{known.Name} is '{known.Code}', so write '{Correct(parts, index, known)}'";
        }

        static string? Correct(string[] parts, int index, (string Name, string Code)? meant)
        {
            return meant is { } known ? String.Join("-", parts.Select((part, i) => i == index ? known.Code : part)) : null;
        }
    }
}

/// <summary>
/// A part of a culture name that no culture has (<c>LOC103</c>). <see cref="Kind"/> is what the part should be, such as
/// <c>ISO 639-1 language</c>, and <see cref="Advice"/> names the likely code when the mistake is a known one.
/// <see cref="Corrected"/> is that code, or <see langword="null"/>.
/// </summary>
internal readonly record struct UnknownLanguagePart(
    string Code,
    string Part,
    string Kind,
    string Advice,
    string? Corrected);

/// <summary>
/// What an assembly's attribute declares: the default language, the valid languages in order, what's wrong with the
/// others, and where the attribute is.
/// </summary>
/// <remarks>
/// The default language is <see langword="null"/> when the first argument isn't a usable language (<c>LOC102</c>). The
/// next language doesn't take its place, so nothing is held to a default language then. Otherwise it's the first of
/// <see cref="Codes"/>.
/// </remarks>
internal readonly record struct DeclaredLanguages(
    string? DefaultLanguage,
    EquatableArray<string> Codes,
    EquatableArray<string> Problems,
    DiagnosticLocation Location);
