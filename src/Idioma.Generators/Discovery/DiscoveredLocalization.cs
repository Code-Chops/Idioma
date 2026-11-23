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
/// <item>The public contracts it implements (<see cref="ContractNames"/>), those written by hand in its assembly
/// (<see cref="OwnContractNames"/>), and whether it implements an internal contract of another assembly, which it
/// sees through <c>InternalsVisibleTo</c> (<see cref="ImplementsInternalContract"/>).</item>
/// </list>
/// <para>
/// Its registration:
/// </para>
/// <list type="bullet">
/// <item>Whether it wins over the default tier (<see cref="IsOverride"/>), and how many sets it derives from
/// (<see cref="Depth"/>).</item>
/// <item>Why it can't be registered automatically, if so (<see cref="RegistrationProblem"/>), and whether its
/// parameterless constructor is public, as the texts of a default-language set need it.</item>
/// <item>Whether it's marked <c>[Obsolete]</c> as an error (<see cref="IsObsoleteAsError"/>), which no generated code
/// may name, and the diagnostics that naming it reports besides <c>CS0612</c> and <c>CS0618</c>
/// (<see cref="SuppressedDiagnostics"/>), which generated code turns off: the id of <c>[Experimental]</c>, and the
/// <c>DiagnosticId</c> of <c>[Obsolete]</c>.</item>
/// <item>Whether it's marked <c>[ExcludeFromLocalizationDiscovery]</c>. Such a set is neither registered nor checked,
/// and counts only as a set of its language.</item>
/// </list>
/// <para>
/// Its language:
/// </para>
/// <list type="bullet">
/// <item><see cref="Language"/> is always a culture name, or <see langword="null"/> when the build can't read one.
/// <see cref="NameGivesNoLanguage"/> tells a set without any language from one whose <c>LocalizationLanguageCode</c> the build
/// can't read.</item>
/// <item><see cref="InvalidLanguageCode"/> is the constant its <c>LocalizationLanguageCode</c> holds when that's no culture
/// name.</item>
/// <item><see cref="RecordedLanguageCode"/> is the code the generated registrar records for it. It's read from its
/// name, or from that of a set it derives from, when the set doesn't implement <c>LocalizationLanguageCode</c>.</item>
/// <item>The class whose <c>LocalizationLanguageCode</c> implements nothing, if any, with where to report it.</item>
/// </list>
/// <para>
/// Its texts:
/// </para>
/// <list type="bullet">
/// <item><see cref="OwnTexts"/> are the texts its own class words, for a set deriving from another. The registrar
/// records them, and only its part of the generator reads them (<see cref="DiscoveredTexts.OfRecord"/>).</item>
/// <item><see cref="Texts"/> are the texts of a set listing <c>ILocalizationSet</c>, and
/// <see cref="UnsupportedTexts"/> its methods that have the shape of a text but can't be one. <see cref="Usings"/>
/// are the using directives its contract repeats, for a text that takes a value of a type another generator
/// writes.</item>
/// <item><see cref="Enums"/> are the enums whose members it has a text for, which it lists as
/// <c>ILocalizationSet&lt;TEnum&gt;</c>. <see cref="Texts"/> holds a text for each of their members, also for one the
/// set doesn't declare.</item>
/// <item><see cref="DeclaredTexts"/>, <see cref="InheritedTexts"/>, and <see cref="Wordings"/> are what the doc
/// comments of the generated contract link to and show.</item>
/// <item><see cref="TypeNamedLikeTexts"/> is the type or namespace in its namespace that has the name its generated
/// class would get, as a message names it (<c>the enum 'OrderStatus'</c>), if any. <see cref="TypeNamedLikeContract"/>
/// is the same for the name of its contract, such as an interface <c>IShopLocalization</c> written by hand that isn't a
/// contract.</item>
/// <item><see cref="ReservedTexts"/> are its texts with a name its generated class can't give them, with where each is
/// written. Most sets have none.</item>
/// <item><see cref="UndeclaredTexts"/> are the texts it inherits from a class of its assembly without declaring them,
/// which its contract leaves out (<c>LOC208</c>). Most sets have none.</item>
/// <item>These are read only for the part of the generator that writes the contracts
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
    bool IsObsoleteAsError,
    EquatableArray<string> SuppressedDiagnostics,
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
    bool ImplementsInternalContract,
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
    RepeatedUsings Usings,
    EquatableArray<LocalizedEnum> Enums,
    string? TypeNamedLikeTexts,
    string? TypeNamedLikeContract,
    EquatableArray<ReservedText> ReservedTexts,
    EquatableArray<UndeclaredText> UndeclaredTexts);

/// <summary>
/// A text a set inherits from a class of its assembly without declaring it: its name, the class that declares it, and
/// where it's written. A contract takes only the texts its set declares, so it leaves such a text out.
/// </summary>
internal readonly record struct UndeclaredText(
    string Name,
    string DeclaringType,
    DiagnosticLocation Location);

/// <summary>
/// An enum whose members a set has a text for, because the set lists <c>ILocalizationSet&lt;TEnum&gt;</c>: its type as
/// generated code can write it, its full name, its name as a message writes it, with the types
/// that hold it (<c>Order.Status</c>), its namespace (<c>""</c> for the global namespace), whether code outside its
/// assembly can name it, and its members in the order they're written.
/// </summary>
internal readonly record struct LocalizedEnum(
    EquatableArray<TypeNamePart> Type,
    string FullyQualifiedName,
    string Name,
    string Namespace,
    bool IsPublic,
    EquatableArray<LocalizedEnumMember> Members);

/// <summary>
/// A member of an enum a set has a text for: its name, which is the name of its text, and whether it's the first
/// member of its value. Of <c>Default = 0</c> and <c>None = 0</c>, only <c>Default</c> is.
/// </summary>
internal readonly record struct LocalizedEnumMember(
    string Name,
    bool IsFirstOfItsValue);

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
/// A <c>LocalizationLanguageCode</c> holding a constant that is no culture name: the class or interface that declares it, the
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
internal sealed class GeneratedContracts
{
    // Looked up for every base type of every set, so by hash: a scan of the names made the analysis of a project with
    // hundreds of contracts take seconds on each edit.
    private HashSet<string> Names { get; }

    /// <summary>
    /// The contract of each simple name, or <see langword="null"/> for a name two contracts share.
    /// </summary>
    private Dictionary<string, string?> BySimpleName { get; }

    private Dictionary<DiagnosticLocation, string> Bindings { get; }

    public GeneratedContracts(ImmutableArray<string> names, EquatableArray<ContractBinding> bindings)
    {
        this.Names = new HashSet<string>(names, StringComparer.Ordinal);
        this.BySimpleName = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var name in this.Names)
        {
            var simpleName = LocalizationDiscovery.GetSimpleName(name);
            this.BySimpleName[simpleName] = this.BySimpleName.ContainsKey(simpleName) ? null : name;
        }

        this.Bindings = [];

        foreach (var binding in bindings)
            if (!this.Bindings.ContainsKey(binding.Location))
                this.Bindings.Add(binding.Location, binding.Contract);
    }

    public bool Contains(string fullName)
    {
        var contains = this.Names.Contains(fullName);
        return contains;
    }

    /// <summary>
    /// Whether a contract has the simple name. <paramref name="contract"/> is that contract, or
    /// <see langword="null"/> where two share the name.
    /// </summary>
    public bool TryFindBySimpleName(string simpleName, out string? contract)
    {
        var found = this.BySimpleName.TryGetValue(simpleName, out contract);
        return found;
    }

    /// <summary>
    /// The contract the compiler bound a base type to, or <see langword="null"/> where it wasn't asked or bound none.
    /// </summary>
    public string? FindBinding(DiagnosticLocation location)
    {
        var binding = this.Bindings.TryGetValue(location, out var contract) ? contract : null;
        return binding;
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
/// Which <c>LocalizationLanguageCode</c> property implements <c>ILocalizationSet.LocalizationLanguageCode</c> for a set
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
/// A text of a default-language set with a name its generated class can't give it: its name, and where it's written.
/// That's the member of the set, or the member of the enum for a text the set doesn't declare. What <c>LOC205</c>
/// reports.
/// </summary>
internal readonly record struct ReservedText(
    string Name,
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
/// Which texts of a set discovery reads. It reads them again on every edit, so each part of the generator asks for what
/// it uses.
/// </summary>
internal enum DiscoveredTexts
{
    /// <summary>
    /// What a contract is written from: <see cref="DiscoveredLocalization.Texts"/>,
    /// <see cref="DiscoveredLocalization.UnsupportedTexts"/>, <see cref="DiscoveredLocalization.Usings"/>,
    /// <see cref="DiscoveredLocalization.Enums"/>, <see cref="DiscoveredLocalization.DeclaredTexts"/>,
    /// <see cref="DiscoveredLocalization.InheritedTexts"/>, and <see cref="DiscoveredLocalization.Wordings"/>.
    /// <see cref="DiscoveredLocalization.OwnTexts"/> stays empty.
    /// </summary>
    OfContract,

    /// <summary>
    /// What the registrar records: <see cref="DiscoveredLocalization.OwnTexts"/> alone, which are names. No wording is
    /// read, and the texts a contract is written from stay empty.
    /// </summary>
    OfRecord,

    /// <summary>
    /// What both parts of the generator read, as discovery reads a class once for the two of them. Each gets its own
    /// share of it (<see cref="LocalizationDiscovery.ForPart(DiscoveredLocalization, DiscoveredTexts)"/>), so neither sees
    /// what changes only for the other.
    /// </summary>
    OfBoth,
}

/// <summary>
/// What discovery finds in one class: a candidate set, or what is reported about a class generated code can't name.
/// </summary>
internal sealed record DiscoveryResult(
    DiscoveredLocalization? Set,
    UnregistrableLocalization? UnnameableOverride,
    UnextendableSet? UnextendableSet);
