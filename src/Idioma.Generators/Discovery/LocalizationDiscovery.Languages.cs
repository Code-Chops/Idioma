namespace CodeChops.Idioma.Generators;

// The part of LocalizationDiscovery that reads the language of a set: from the end of its name, from a code its
// assembly declares, or from its LocalizationLanguageCode. A plain comment: the compiler merges the XML docs of every partial
// declaration, and the type's summary lives in LocalizationDiscovery.cs.
/// <inheritdoc cref="LocalizationDiscovery"/>
internal static partial class LocalizationDiscovery
{
    /// <summary>
    /// The language the set serves, as <c>ILocalizationSet.LocalizationLanguageCode</c> will at run time: its own constant when it
    /// implements the property, else the code the build describes it or a set it derives from with
    /// (<see cref="FindDescribedLanguageCode"/>), else the last two letters of its name (<see cref="GetNameLanguage"/>).
    /// <see langword="null"/> when that
    /// can't be known here: the property is no constant, comes from another assembly, or holds a constant that is no
    /// culture name. A <c>LocalizationLanguageCode</c> that implements nothing counts as meant (<c>LOC206</c> reports it), so the
    /// language checks add no errors of their own about it.
    /// </summary>
    /// <param name="type">The set.</param>
    /// <param name="ancestors">The classes of this assembly it derives from, nearest first.</param>
    /// <param name="reader">What reads a constant.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    private static string? GetLanguage(INamedTypeSymbol type, ImmutableArray<SourceAncestor> ancestors, WordingReader reader, CancellationToken cancellationToken)
    {
        var languageCode = ReadSetLanguageCode(type);

        // The described code only where it's asked for: a set without a LocalizationLanguageCode of any kind.
        var describedCode = languageCode.Property is null ? FindDescribedLanguageCode(type, ancestors, declaresLanguageCode: false) : null;

        var language = GetLanguage(type, languageCode, describedCode, reader, cancellationToken, out _, out _);
        return language;
    }

    /// <summary>
    /// The language a set of this compilation serves, as the language checks read it, or <see langword="null"/> when
    /// the build can't read it. It's meant for a message that names the language.
    /// </summary>
    public static string? ReadLanguage(INamedTypeSymbol set, Compilation compilation, CancellationToken cancellationToken)
    {
        var reader = new WordingReader(compilation);
        var language = GetLanguage(set, GetSourceAncestors(set, reader, cancellationToken), reader, cancellationToken);
        return language;
    }

    /// <summary>
    /// The language the set serves (see the overload without <paramref name="invalidLanguageCode"/>), and the constant
    /// its <c>LocalizationLanguageCode</c> holds when that is no culture name: what <c>LOC207</c> reports. A constant
    /// that is a culture name with a part no culture has, such as <c>"dk"</c>, is the set's language all the same, and
    /// <paramref name="unknownLanguageCode"/> holds it: what <c>LOC104</c> reports.
    /// </summary>
    /// <param name="type">The set.</param>
    /// <param name="languageCode">The <c>LocalizationLanguageCode</c> of the set (<see cref="ReadSetLanguageCode"/>).</param>
    /// <param name="describedCode">The code the build describes the set with (<see cref="FindDescribedLanguageCode(INamedTypeSymbol, ImmutableArray{SourceAncestor}, bool)"/>).
    /// It's read only for a set without a <c>LocalizationLanguageCode</c>.</param>
    /// <param name="reader">What reads a constant.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <param name="invalidLanguageCode">Gets the constant that is no culture name.</param>
    /// <param name="unknownLanguageCode">Gets the constant with a part no culture has.</param>
    // Every language the checks and the generated code work with comes through here, so it is a culture name or nothing:
    // a constant such as "" or "en_GB" is in no language, and is held to none, like one that could not be read.
    private static string? GetLanguage(INamedTypeSymbol type, SetLanguageCode languageCode, string? describedCode, WordingReader reader,
        CancellationToken cancellationToken, out InvalidLanguageCode? invalidLanguageCode, out UnknownLanguageCode? unknownLanguageCode)
    {
        invalidLanguageCode = null;
        unknownLanguageCode = null;

        var mapping = languageCode.Mapping;

        if (languageCode.Property is { } property)
        {
            var code = reader.Read(property, cancellationToken);

            if (code is null)
                return null;

            if (SupportedLanguages.IsCultureName(code))
            {
                // Not for a LocalizationLanguageCode that implements nothing, as below. A property of another assembly
                // has no place in this one, and its own build reports it.
                if (mapping.Unmapped is null && property.Locations[0].IsInSource && SupportedLanguages.FindUnknownPart(code) is not null)
                    unknownLanguageCode = new UnknownLanguageCode(code, DiagnosticLocation.From(property.Locations[0]));

                return code;
            }

            // Not for a LocalizationLanguageCode that implements nothing: nothing reads it at run time, and LOC206 reports it.
            if (mapping.Unmapped is null)
                invalidLanguageCode = new InvalidLanguageCode(property.ContainingType.Name, code, DiagnosticLocation.From(property.Locations[0]));

            return null;
        }

        var language = describedCode ?? GetNameLanguage(type.Name);
        return language;
    }

    /// <summary>
    /// The <c>LocalizationLanguageCode</c> of a set, read once for everything discovery asks about it
    /// (<see cref="SetLanguageCode"/>).
    /// </summary>
    private static SetLanguageCode ReadSetLanguageCode(INamedTypeSymbol type)
    {
        var mapping = MapLanguageCode(type);

        // A contract's own implementation applies only when no class of the set implements the property.
        var languageCode = new SetLanguageCode(mapping, mapping.Implementation is null ? FindInterfaceLanguageCode(type) : null);
        return languageCode;
    }

    /// <summary>
    /// The constant that the <c>LocalizationLanguageCode</c> a set implements returns, when the build can read it and it's a
    /// culture name, or else <see langword="null"/>. The generated registrar passes it on, so that the registry files the set
    /// under its language without creating it.
    /// </summary>
    // The implementation alone, which is what runs, and not a LocalizationLanguageCode that implements nothing (LOC206).
    private static string? ReadImplementedLanguageCode(SetLanguageCode languageCode, WordingReader reader, CancellationToken cancellationToken)
    {
        var code = languageCode.Implemented is { } property ? reader.Read(property, cancellationToken) : null;

        return code is not null && SupportedLanguages.IsCultureName(code) ? code : null;
    }

    /// <summary>
    /// The language the last two letters of a name give, in lower case, or <see langword="null"/> when the name doesn't
    /// end in two ASCII letters that start with a capital. <c>ShopLocalizationNl</c> and <c>ShopLocalizationNL</c> give
    /// <c>nl</c>, and <c>LocalizationSet</c> gives none.
    /// </summary>
    private static string? GetNameLanguage(string name)
    {
        var language = EndsInTwoLetterSuffix(name)
            ? name.Substring(name.Length - 2).ToLowerInvariant()
            : null;

        return language;
    }

    /// <summary>
    /// The code the build describes a set with at run time (<c>GeneratedCode.DescribeSet</c>), or else for
    /// the nearest set it derives from that has one, as <c>ILocalizationSet.GetLanguageCode</c> looks it up; <see langword="null"/>
    /// when none has. Only a set the generated registrar can name gets a description: one that discovery finds.
    /// </summary>
    /// <param name="type">The set.</param>
    /// <param name="ancestors">The classes of this assembly it derives from, nearest first.</param>
    /// <param name="declaresLanguageCode">Whether the set implements <c>LocalizationLanguageCode</c>
    /// (<see cref="SetLanguageCode.IsDeclared"/>).</param>
    private static string? FindDescribedLanguageCode(INamedTypeSymbol type, ImmutableArray<SourceAncestor> ancestors, bool declaresLanguageCode)
    {
        if (!declaresLanguageCode && FindOwnDescribedLanguageCode(type) is { } own)
            return own;

        var index = 0;
        for (var baseType = type.BaseType; baseType is { TypeKind: TypeKind.Class }; baseType = baseType.BaseType, index++)
        {
            // A class of this assembly implementing a contract generated here is a set as well (see GetDepth).
            var isSet = ImplementsLocalizationSet(baseType)
                        || (index < ancestors.Length && ancestors.Skip(index).Any(static ancestor => ancestor.UnresolvedBaseTypes.Length > 0));

            if (isSet && IsNameableSet(baseType) && !DeclaresLanguageCode(baseType) && FindOwnDescribedLanguageCode(baseType) is { } inherited)
                return inherited;
        }

        return null;
    }

    /// <summary>
    /// The code a set's own name gives it among the languages its assembly declares that are not two letters
    /// (<c>ShopLocalizationEnGb</c> for <c>en-GB</c>), or <see langword="null"/>: what the generated registrar of that
    /// assembly describes it with.
    /// </summary>
    private static string? FindOwnDescribedLanguageCode(INamedTypeSymbol type)
    {
        var languageCode = SupportedLanguages.FindNamedLanguage(type.Name, SupportedLanguages.ReadCodes(type.ContainingAssembly));
        return languageCode;
    }

    // The capital marks where the code starts, as it does for a longer one (SupportedLanguages.EndsInTypeSuffix). Without
    // it, LocalizationSet would be a set for Estonian (et), and ShopTexts one for 'ts'.
    private static bool EndsInTwoLetterSuffix(string name)
    {
        var endsInTwoLetterSuffix = name.Length >= 2 && Ascii.IsLetter(name[name.Length - 1]) && name[name.Length - 2] is >= 'A' and <= 'Z';
        return endsInTwoLetterSuffix;
    }

    private static bool DeclaresLanguageCode(INamedTypeSymbol type)
    {
        var declaresLanguageCode = ReadSetLanguageCode(type).IsDeclared;
        return declaresLanguageCode;
    }

    /// <summary>
    /// Which <c>LocalizationLanguageCode</c> of the set's classes implements <c>ILocalizationSet.LocalizationLanguageCode</c> at run time
    /// (<see langword="null"/>: the default, from the type's name), and the most derived one that implements nothing.
    /// </summary>
    // A class maps the interface where it lists it (ILocalizationSet, a contract, or a contract generated here, which binds
    // to nothing yet), looking up from there. A class deriving from it without listing the interface again inherits that
    // mapping, so a LocalizationLanguageCode it declares implements nothing unless it overrides the implementation; an explicit one
    // does not compile there (CS0540).
    private static LanguageCodeMapping MapLanguageCode(INamedTypeSymbol type)
    {
        var chain = new List<INamedTypeSymbol>();
        for (var current = type; current is not null; current = current.BaseType)
            chain.Add(current);

        var listing = chain.FindIndex(ListsContract);
        if (listing < 0)
            return default;

        IPropertySymbol? implementation = null;
        for (var i = listing; i < chain.Count && implementation is null; i++)
            implementation = FindDeclaredLanguageCode(chain[i]);

        IPropertySymbol? @override = null;
        IPropertySymbol? unmapped = null;
        for (var i = 0; i < listing; i++)
        {
            if (FindDeclaredLanguageCode(chain[i]) is not { } declared)
                continue;

            if (implementation is not null && Overrides(declared, implementation))
                @override ??= declared;
            else
                unmapped ??= declared;
        }

        var mappedLanguageCode = new LanguageCodeMapping(@override ?? implementation, unmapped);
        return mappedLanguageCode;

        static bool ListsContract(INamedTypeSymbol type)
        {
            return type.BaseType is { TypeKind: TypeKind.Error }
                   || Enumerable.Any(type.Interfaces, static @interface => @interface.TypeKind is TypeKind.Error || IsLocalizationSetInterface(@interface) || ImplementsLocalizationSet(@interface));
        }

        static bool Overrides(IPropertySymbol declared, IPropertySymbol implementation)
        {
            for (var overridden = declared.OverriddenProperty; overridden is not null; overridden = overridden.OverriddenProperty)
                if (SymbolEqualityComparer.Default.Equals(overridden.OriginalDefinition, implementation.OriginalDefinition))
                    return true;

            return false;
        }
    }

    /// <summary>
    /// A contract's own implementation of <c>LocalizationLanguageCode</c> (<c>string ILocalizationSet.LocalizationLanguageCode =&gt; "nl";</c>
    /// in an interface), which applies when no class of the set implements it.
    /// </summary>
    private static IPropertySymbol? FindInterfaceLanguageCode(INamedTypeSymbol type)
    {
        foreach (var @interface in type.AllInterfaces)
            if (!IsLocalizationSetInterface(@interface) && FindDeclaredLanguageCode(@interface) is { } property)
                return property;

        return null;
    }

    /// <summary>
    /// The <c>LocalizationLanguageCode</c> a type declares itself: a public instance <see cref="string"/> property of that name,
    /// or an explicit implementation of <c>ILocalizationSet.LocalizationLanguageCode</c>. A property that implements the
    /// <c>LocalizationLanguageCode</c> of another interface explicitly, or one of another type, is not it, because the set does not
    /// serve what that returns.
    /// </summary>
    private static IPropertySymbol? FindDeclaredLanguageCode(INamedTypeSymbol type)
    {
        foreach (var member in type.GetMembers())
        {
            // By its name first, which an explicit implementation ends in too: the type of a property is bound when it
            // is asked for, and this is asked of every text of every set on every edit.
            if (!member.Name.EndsWith(LanguageCodeMemberName, StringComparison.Ordinal)
                || member is not IPropertySymbol { IsStatic: false, Type.SpecialType: SpecialType.System_String } property)
            {
                continue;
            }

            if ((property.DeclaredAccessibility is Accessibility.Public && String.Equals(property.Name, LanguageCodeMemberName, StringComparison.Ordinal))
                || Enumerable.Any(property.ExplicitInterfaceImplementations, static implemented
                    => String.Equals(implemented.Name, LanguageCodeMemberName, StringComparison.Ordinal) && IsLocalizationSetInterface(implemented.ContainingType)))
            {
                return property;
            }
        }

        return null;
    }
}
