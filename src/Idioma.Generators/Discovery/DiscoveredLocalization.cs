namespace CodeChops.Idioma.Generators;

/// <summary>
/// A candidate set, which implements <c>ILocalizationSet</c>, with what the checks and the generated code need to know
/// about it.
/// </summary>
/// <remarks>
/// <para>
/// Its names and shape:
/// </para>
/// <list type="bullet">
/// <item>Its names, and whether it's a record, sealed, or public.</item>
/// <item>The base types that bind to nothing, which it or a class it derives from lists
/// (<see cref="UnresolvedBaseTypes"/>). A contract generated in the same build is one.</item>
/// <item>The classes of its assembly it derives from (<see cref="AncestorNames"/>), and the set it derives from
/// directly (<see cref="BaseSetName"/>). <see cref="BaseSetContracts"/> holds the unresolved base types through which
/// that set would implement a contract generated here. It's empty when that set implements <c>ILocalizationSet</c>
/// already.</item>
/// <item>The public contracts it implements (<see cref="ContractNames"/>), and those written by hand in its assembly
/// (<see cref="OwnContractNames"/>).</item>
/// </list>
/// <para>
/// Its registration:
/// </para>
/// <list type="bullet">
/// <item>Whether it wins over the default tier (<see cref="IsOverride"/>), and how many sets it derives from
/// (<see cref="Depth"/>).</item>
/// <item>Why it can't be registered automatically, if so (<see cref="RegistrationProblem"/>), and whether its
/// parameterless constructor is public, as the texts of a default-language set need it.</item>
/// <item>Whether it's marked <c>[ExcludeFromLocalizationDiscovery]</c>. Such a set is neither registered nor checked,
/// and counts only as a set of its language.</item>
/// </list>
/// <para>
/// Its language:
/// </para>
/// <list type="bullet">
/// <item><see cref="Language"/> is always a culture name, or <see langword="null"/> when the build can't read one.
/// <see cref="NameGivesNoLanguage"/> tells a set without any language from one whose <c>LanguageCode</c> the build
/// can't read.</item>
/// <item><see cref="InvalidLanguageCode"/> is the constant its <c>LanguageCode</c> holds when that's no culture
/// name.</item>
/// <item><see cref="RecordedLanguageCode"/> is the code the generated registrar records for it. It's read from its
/// name, or from that of a set it derives from, when the set doesn't implement <c>LanguageCode</c>.</item>
/// <item>The class whose <c>LanguageCode</c> implements nothing, if any, with where to report it.</item>
/// </list>
/// <para>
/// Its texts:
/// </para>
/// <list type="bullet">
/// <item><see cref="OwnTexts"/> are the texts its own class words, for a set deriving from another. The registrar
/// records them, and only its discovery reads them (<see cref="DiscoveredTexts.OfRecord"/>).</item>
/// <item><see cref="Texts"/> are the texts of a set listing <c>ILocalizationSet</c>, and
/// <see cref="UnsupportedTexts"/> its methods that have the shape of a text but can't be one. <see cref="Usings"/>
/// are the using directives its contract repeats, for a text that takes a value of a type another generator
/// writes.</item>
/// <item><see cref="DeclaredTexts"/>, <see cref="InheritedTexts"/>, and <see cref="Wordings"/> are what the doc
/// comments of the generated contract link to and show.</item>
/// <item>These are read only for the generator that writes the contracts
/// (<see cref="DiscoveredTexts.OfContract"/>).</item>
/// </list>
/// </remarks>
internal readonly record struct DiscoveredLocalization(
    string MetadataName,
    string FullyQualifiedName,
    string Name,
    string Namespace,
    bool IsOverride,
    int Depth,
    EquatableArray<UnresolvedBaseType> UnresolvedBaseTypes,
    string? RegistrationProblem,
    bool HasPublicParameterlessConstructor,
    bool IsExcluded,
    DiagnosticLocation Location,
    string? Language,
    bool NameGivesNoLanguage,
    InvalidLanguageCode? InvalidLanguageCode,
    string? RecordedLanguageCode,
    EquatableArray<string> OwnTexts,
    bool ListsLocalizationSet,
    EquatableArray<string> ContractNames,
    EquatableArray<string> OwnContractNames,
    EquatableArray<string> AncestorNames,
    string? BaseSetName,
    string? BaseSetLanguage,
    EquatableArray<UnresolvedBaseType> BaseSetContracts,
    string? UnmappedLanguageCodeType,
    string? UnmappedLanguageCodeBaseType,
    DiagnosticLocation UnmappedLanguageCodeLocation,
    bool IsRecord,
    bool IsSealed,
    bool IsPublic,
    EquatableArray<string> DeclaredTexts,
    EquatableArray<InheritedText> InheritedTexts,
    EquatableArray<TextWording> Wordings,
    EquatableArray<LocalizationMember> Texts,
    EquatableArray<UnsupportedText> UnsupportedTexts,
    RepeatedUsings Usings);

/// <summary>
/// The wording a set gives one of its texts, when that is a constant, or what the method of a text that takes values
/// writes (<see cref="MethodWording"/>).
/// </summary>
internal readonly record struct TextWording(
    string Name,
    string Wording);

/// <summary>
/// A text a set inherits, with the nearest class declaring it as a cref names that class: its namespace (<c>""</c> for
/// the global namespace), and its name there, with containing types and with type parameters in braces
/// (<c>DutchBase{T}</c>).
/// </summary>
internal readonly record struct InheritedText(
    string Name,
    string Namespace,
    string TypeName);

/// <summary>
/// A <c>LanguageCode</c> holding a constant that is no culture name: the class or interface that declares it, the
/// constant, and where the property is. What <c>LOC402</c> reports.
/// </summary>
internal readonly record struct InvalidLanguageCode(
    string DeclaringType,
    string Code,
    DiagnosticLocation Location);

/// <summary>
/// A base type a candidate lists that binds to nothing in the input compilation. It has a simple name, whether it's
/// written with a qualifier, the namespace that qualifier names (<c>""</c> for <c>global::</c>, and
/// <see langword="null"/> for one that names no namespace), and where it's written, for the compiler to bind it
/// (<see cref="LocalizationDiscovery.BindContracts"/>).
/// </summary>
internal readonly record struct UnresolvedBaseType(
    string Name,
    bool IsQualified,
    string? Namespace,
    DiagnosticLocation Location);

/// <summary>
/// The generated contract a base type binds to, as the compiler binds it where two generated contracts share its name.
/// </summary>
internal readonly record struct ContractBinding(
    DiagnosticLocation Location,
    string Contract);

/// <summary>
/// What the compiler is asked to bind: the generated contracts that share a simple name with another, and the base
/// types written with that name alone. Empty where no two contracts share a name.
/// </summary>
internal readonly record struct ContractBindingQuestion(
    EquatableArray<string> Contracts,
    EquatableArray<UnresolvedBaseType> BaseTypes);

/// <summary>
/// The full names of the contracts the current compilation generates, and which of them a base type binds to where
/// two share its name (<see cref="LocalizationDiscovery.BindContracts"/>).
/// </summary>
internal sealed class GeneratedContracts(ImmutableArray<string> names, EquatableArray<ContractBinding> bindings)
{
    public ImmutableArray<string> Names { get; } = names;

    private EquatableArray<ContractBinding> Bindings { get; } = bindings;

    public bool Contains(string fullName)
    {
        var contains = this.Names.Contains(fullName, StringComparer.Ordinal);
        return contains;
    }

    /// <summary>
    /// The contract the compiler bound a base type to, or <see langword="null"/> where it wasn't asked or bound none.
    /// </summary>
    public string? FindBinding(DiagnosticLocation location)
    {
        foreach (var binding in this.Bindings)
            if (binding.Location.Equals(location))
                return binding.Contract;

        return null;
    }
}

/// <summary>
/// A class of the current assembly a candidate derives from, with the base types it lists itself that bind to nothing.
/// Lives within one transform only, so it may hold a symbol.
/// </summary>
internal readonly record struct SourceAncestor(
    INamedTypeSymbol Type,
    ImmutableArray<UnresolvedBaseType> UnresolvedBaseTypes);

/// <summary>
/// Which <c>LanguageCode</c> property implements <c>ILocalizationSet.LanguageCode</c> for a set
/// (<see langword="null"/>: the default, from the type's name), and the most derived one that implements nothing.
/// </summary>
internal readonly record struct LanguageCodeMapping(
    IPropertySymbol? Implementation,
    IPropertySymbol? Unmapped);

/// <summary>
/// One text of a default-language set: the name of its property or method, whether that carries a doc comment (then
/// the generated members inherit it), its wording when the property returns a constant or the method writes one
/// (<see cref="MethodWording"/>; then a text without a doc comment is documented with that wording), and the
/// parameters of a text that takes values, which is a method. A property has none.
/// </summary>
internal readonly record struct LocalizationMember(
    string Name,
    bool HasDocComment,
    string? DefaultText,
    EquatableArray<TextParameter> Parameters)
{
    /// <summary>
    /// Whether the text takes values, and so is a method of the set, of the contract, and of the generated class.
    /// </summary>
    public bool IsMethod => this.Parameters.Length > 0;
}

/// <summary>
/// A method of a default-language set that has the shape of a text that takes values but can't be one: its name, why
/// not, as the clause of the message, and where it is. What <c>LOC300</c> reports.
/// </summary>
internal readonly record struct UnsupportedText(
    string Name,
    string Problem,
    DiagnosticLocation Location);

/// <summary>
/// A set the generated code registers, in which tier, and how many sets it derives from. Holds no location, so that an
/// edit in the file of a set does not rewrite the generated code.
/// </summary>
internal readonly record struct RegisteredLocalization(
    string MetadataName,
    string FullyQualifiedName,
    bool IsOverride,
    int Depth);

/// <summary>
/// A set that can't be registered automatically: what <c>LOC400</c> reports.
/// </summary>
internal readonly record struct UnregistrableLocalization(
    string MetadataName,
    string Problem,
    DiagnosticLocation Location);

/// <summary>
/// A class listing <c>ILocalizationSet</c> and a contract that binds to nothing yet, as a default-language set does,
/// which the generator can't use for its shape (nested, file-local, generic, or abstract): what <c>LOC200</c> reports
/// when its language is the default language, and no contract it lists is generated from another set. Its language is
/// <see langword="null"/> when the build can't read it.
/// </summary>
internal readonly record struct UnextendableSet(
    string MetadataName,
    string Name,
    string Problem,
    string? Language,
    EquatableArray<UnresolvedBaseType> UnresolvedBaseTypes,
    DiagnosticLocation Location);

/// <summary>
/// What discovery finds in the current compilation: the candidate sets, the nested or file-local sets rewording another
/// assembly's texts (<c>LOC400</c>), and the default-language sets the generator can't use (<c>LOC200</c>), the last
/// two ordered by name.
/// </summary>
internal readonly record struct Discovery(
    EquatableArray<DiscoveredLocalization> Sets,
    EquatableArray<UnregistrableLocalization> UnnameableOverrides,
    EquatableArray<UnextendableSet> UnextendableSets);

/// <summary>
/// Which texts of a set discovery reads. It reads them again on every edit, so each generator asks for what it uses.
/// </summary>
internal enum DiscoveredTexts
{
    /// <summary>
    /// What a contract is written from: <see cref="DiscoveredLocalization.Texts"/>,
    /// <see cref="DiscoveredLocalization.UnsupportedTexts"/>, <see cref="DiscoveredLocalization.Usings"/>,
    /// <see cref="DiscoveredLocalization.DeclaredTexts"/>,
    /// <see cref="DiscoveredLocalization.InheritedTexts"/>, and <see cref="DiscoveredLocalization.Wordings"/>.
    /// <see cref="DiscoveredLocalization.OwnTexts"/> stays empty.
    /// </summary>
    OfContract,

    /// <summary>
    /// What the registrar records: <see cref="DiscoveredLocalization.OwnTexts"/> alone, which are names. No wording is
    /// read, and the texts a contract is written from stay empty.
    /// </summary>
    OfRecord,
}

/// <summary>
/// What discovery finds in one class: a candidate set, or what is reported about a class generated code can't name.
/// </summary>
internal sealed record DiscoveryResult(
    DiscoveredLocalization? Set,
    UnregistrableLocalization? UnnameableOverride,
    UnextendableSet? UnextendableSet);
