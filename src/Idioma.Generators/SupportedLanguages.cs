namespace CodeChops.Idioma.Generators;

/// <summary>
/// Reads <c>[assembly: SupportedLanguages(...)]</c>, and the rules every part of this project shares about language
/// codes.
/// </summary>
internal static class SupportedLanguages
{
    internal const string AttributeFullName = "CodeChops.Idioma.SupportedLanguagesAttribute";

    /// <summary>
    /// The languages the current compilation declares, or <see langword="null"/> when it declares none.
    /// </summary>
    public static IncrementalValueProvider<DeclaredLanguages?> Read(IncrementalGeneratorInitializationContext context)
    {
        return context.SyntaxProvider
            .ForAttributeWithMetadataName(
                fullyQualifiedMetadataName: AttributeFullName,
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
            if (String.Equals(attribute.AttributeClass?.ToDisplayString(), AttributeFullName, StringComparison.Ordinal))
                return ReadArguments(attribute).OfType<string>().ToImmutableArray();

        return ImmutableArray<string>.Empty;
    }

    /// <summary>
    /// Whether a set for <paramref name="provided"/> serves <paramref name="requested"/>: the same language, or a culture
    /// that resolves through it at run time (<c>nl</c> serves <c>nl-BE</c>).
    /// </summary>
    public static bool Serves(string provided, string requested)
    {
        return String.Equals(provided, requested, StringComparison.OrdinalIgnoreCase)
               || requested.StartsWith(provided + "-", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The parameter a language has in the generated factory: <c>nl</c>, <c>zh_Hans</c>, and <c>@is</c>.
    /// </summary>
    public static string ToParameterName(string code)
    {
        var name = code.Replace('-', '_');

        return SyntaxFacts.GetKeywordKind(name) is SyntaxKind.None ? name : $"@{name}";
    }

    /// <summary>
    /// The suffix a set's name carries for a language: <c>NL</c> and <c>ZhHans</c>.
    /// </summary>
    public static string ToTypeSuffix(string code)
    {
        if (code.Length is 2)
            return code.ToUpperInvariant();

        var suffix = new StringBuilder(code.Length);
        foreach (var part in code.Split('-'))
            if (part.Length > 0)
                suffix.Append(Char.ToUpperInvariant(part[0])).Append(part, 1, part.Length - 1);

        return suffix.ToString();
    }

    /// <summary>
    /// The set to add for a language, as a diagnostic names it: <c>'class ShopTranslationsNL : IShopTranslations'</c>.
    /// The name alone gives the language, also for a code that is not two letters (<c>ShopTranslationsNlBE</c>), as
    /// every language a diagnostic suggests a set for is one the assembly declares (<see cref="FindNamedLanguage"/>).
    /// </summary>
    public static string SuggestSet(string setName, string contract)
    {
        return $"'class {setName} : {contract}'";
    }

    /// <summary>
    /// The language a type's name gives it among declared languages that are not two letters: the longest code whose
    /// suffix (<see cref="ToTypeSuffix"/>) the name ends in, in any letter case, such as <c>nl-BE</c> for
    /// <c>ShopTranslationsNlBE</c> or <c>fil</c> for <c>ShopTranslationsFil</c>; <see langword="null"/> when none
    /// does. A two-letter code needs no such match: the last two letters of a name give it at run time.
    /// </summary>
    // Only declared codes: a name alone cannot tell ShopTranslationsNlBE (nl-BE) from AdminUiNL (nl).
    public static string? FindNamedLanguage(string typeName, IEnumerable<string> codes)
    {
        string? found = null;

        foreach (var code in codes)
        {
            if (code.Length is 2 || !IsCultureName(code) || (found is not null && found.Length >= code.Length))
                continue;

            var suffix = ToTypeSuffix(code);

            if (typeName.Length > suffix.Length && typeName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                found = code;
        }

        return found;
    }

    private static DeclaredLanguages FromAttribute(AttributeData attribute)
    {
        var codes = ImmutableArray.CreateBuilder<string>();
        var problems = ImmutableArray.CreateBuilder<string>();

        foreach (var code in ReadArguments(attribute))
        {
            if (code is null)
                problems.Add("null is no culture name");
            else if (!IsCultureName(code))
                problems.Add($"'{code}' is no culture name");
            else if (codes.Contains(code, StringComparer.OrdinalIgnoreCase))
                problems.Add($"'{code}' is listed twice");
            else
                codes.Add(code);
        }

        var location = attribute.ApplicationSyntaxReference is { } reference
            ? DiagnosticLocation.From(reference.GetSyntax().GetLocation())
            : default;

        return new DeclaredLanguages(
            new EquatableArray<string>(codes.ToImmutable()),
            new EquatableArray<string>(problems.ToImmutable()),
            location);
    }

    /// <summary>
    /// The languages the attribute lists, in order, with <see langword="null"/> for each one written as <c>null</c>; a
    /// list of other languages written as <c>null</c> counts as one. A constant the compiler cannot read yet, while the
    /// user is still typing, is left out.
    /// </summary>
    private static IEnumerable<string?> ReadArguments(AttributeData attribute)
    {
        // By position: (string defaultLanguage, params string[] otherLanguages).
        if (attribute.ConstructorArguments.Length is 0)
            yield break;

        if (TryReadLanguage(attribute.ConstructorArguments[0], out var defaultLanguage))
            yield return defaultLanguage;

        if (attribute.ConstructorArguments.Length < 2 || attribute.ConstructorArguments[1].Kind is not TypedConstantKind.Array)
            yield break;

        // SupportedLanguages("en", null) passes no array at all, whose Values would throw.
        if (attribute.ConstructorArguments[1].IsNull)
        {
            yield return null;
            yield break;
        }

        foreach (var other in attribute.ConstructorArguments[1].Values)
            if (TryReadLanguage(other, out var language))
                yield return language;

        static bool TryReadLanguage(TypedConstant constant, out string? language)
        {
            language = constant.Value as string;

            return constant.Kind is not TypedConstantKind.Error && (language is not null || constant.IsNull);
        }
    }

    /// <summary>
    /// A first part of two to eight ASCII letters, and further parts of ASCII letters and digits: what fits a parameter
    /// name, and what a culture name looks like. Not checked against the cultures of this machine, because a compiler
    /// may run under invariant globalization.
    /// </summary>
    private static bool IsCultureName(string code)
    {
        var parts = code.Split('-');

        if (parts[0].Length is < 2 or > 8 || !parts[0].All(IsAsciiLetter))
            return false;

        for (var i = 1; i < parts.Length; i++)
            if (parts[i].Length is 0 or > 8 || !parts[i].All(c => IsAsciiLetter(c) || c is >= '0' and <= '9'))
                return false;

        return true;

        static bool IsAsciiLetter(char c) => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
    }
}

/// <summary>
/// What an assembly's attribute declares: the valid languages in order, the default first, what is wrong with the
/// others, and where the attribute is.
/// </summary>
internal readonly record struct DeclaredLanguages(
    EquatableArray<string> Codes,
    EquatableArray<string> Problems,
    DiagnosticLocation Location);
