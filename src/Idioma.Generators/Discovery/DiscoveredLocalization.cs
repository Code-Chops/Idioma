namespace CodeChops.Idioma.Generators;

/// <summary>
/// A candidate set, which implements <c>ILocalizationSet</c>, with what the checks and the generated code need to know
/// about it.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><see cref="UnresolvedBaseTypes"/> are the base types it or a base class lists that bind to nothing, such as a
/// contract generated in the same build.</item>
/// <item><see cref="ImplementsInternalContract"/> says whether it implements an internal contract of another assembly
/// through <c>InternalsVisibleTo</c>.</item>
/// <item>A set marked <c>[ExcludeFromLocalizationDiscovery]</c> is neither registered nor checked, and counts only as
/// a set of its language.</item>
/// <item><see cref="Language"/> is the language the build reads from its name, a culture name, or
/// <see langword="null"/>. The registrar describes the set with it.</item>
/// <item><see cref="BaseSetNames"/> are the concrete sets it derives from, nearest first, of any assembly.</item>
/// <item><see cref="EnumNames"/> are the enums it lists as <c>ILocalizationSet&lt;TEnum&gt;</c>.</item>
/// <item><see cref="Locations"/> and <see cref="OfContract"/> are empty in the registrar's share
/// (<see cref="DiscoveredTexts.OfRegistrar"/>), so an edit of a wording leaves the registrar's pipeline as it was
/// (<see cref="LocalizationDiscovery.ForPart(DiscoveredLocalization, DiscoveredTexts)"/>).</item>
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
    SetLocations Locations,
    string? Language,
    bool ListsLocalizationSet,
    EquatableArray<string> ContractNames,
    EquatableArray<string> OwnContractNames,
    bool ImplementsInternalContract,
    EquatableArray<string> AncestorNames,
    BaseSet? BaseSet,
    EquatableArray<string> BaseSetNames,
    RewordedSet? Reworded,
    bool IsRecord,
    bool IsSealed,
    bool IsPublic,
    EquatableArray<string> EnumNames,
    SetTextsOfContract OfContract)
{
    /// <summary>
    /// Where the set is written, and where most diagnostics about it are reported. It's no location in the registrar's
    /// share.
    /// </summary>
    public DiagnosticLocation Location => this.Locations.Set;
}

/// <summary>
/// Every source location of a set outside its texts. The registrar's share holds it empty
/// (<see cref="DiscoveredTexts.OfRegistrar"/>), so that share stays equal across an edit in the set's file.
/// </summary>
/// <remarks>
/// <see cref="BaseTypes"/> says where each unresolved base type is written, for
/// <see cref="LocalizationDiscovery.BindContracts"/>.
/// </remarks>
internal readonly record struct SetLocations(
    DiagnosticLocation Set,
    EquatableArray<BaseTypeLocation> BaseTypes);

/// <summary>
/// Which entry of which base list a base type is. It holds no location, so it stays equal across an edit that moves the
/// entry or gives its file a new syntax tree.
/// </summary>
/// <param name="Type">The class that has the base list.</param>
/// <param name="Declaration">Which declaration of that class in the file has the base list, counted from 0.</param>
/// <param name="Index">The position of the entry in the base list, counted from 0.</param>
internal readonly record struct BaseTypeSite(
    string FilePath,
    string Type,
    int Declaration,
    int Index);

internal readonly record struct BaseTypeLocation(
    BaseTypeSite Site,
    DiagnosticLocation Location);

/// <summary>
/// The nearest concrete set of another assembly that a set derives from, which it rewords (<c>LOC208</c>, <c>LOC209</c>).
/// Its language is <see langword="null"/> when the build can't read one.
/// </summary>
internal readonly record struct RewordedSet(
    string MetadataName,
    string AssemblyName,
    string? Language);

/// <summary>
/// The set a set derives from directly. Its language is <see langword="null"/> when the build can't read one.
/// <see cref="Contracts"/> are the unresolved base types through which it would implement a contract generated here,
/// and is empty when it implements <c>ILocalizationSet</c> already.
/// </summary>
internal readonly record struct BaseSet(
    string Name,
    string? Language,
    EquatableArray<UnresolvedBaseType> Contracts);

/// <summary>
/// What the part of the generator that writes the contracts reads of a set's texts. The registrar's share holds it
/// empty.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><see cref="DeclaredTexts"/>, <see cref="InheritedTexts"/> and <see cref="Wordings"/> are what the doc comments of
/// the contract link to and show. Every set has them.</item>
/// <item><see cref="Texts"/>, <see cref="UnsupportedTexts"/> and <see cref="Usings"/> are filled only for a set a
/// contract may come from (<see cref="LocalizationDiscovery.ReadsAsDefaultSet"/>). <see cref="Texts"/> holds a text for
/// each member of its enums, also one the set doesn't declare. <see cref="Usings"/> are the using directives the contract
/// repeats, for a parameter of a type another generator writes.</item>
/// <item><see cref="TypeNamedLikeTexts"/> and <see cref="TypeNamedLikeContract"/> name a type that already has the name
/// of the generated class or the contract, as a message names it (<c>the enum 'OrderStatus'</c>).</item>
/// <item><see cref="ReservedTexts"/>, <see cref="UndeclaredTexts"/> (<c>LOC303</c>) and <see cref="TextsOutsideEnums"/>
/// (<c>LOC305</c>) are what's reported about its texts.</item>
/// </list>
/// </remarks>
internal readonly record struct SetTextsOfContract(
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
    EquatableArray<UndeclaredText> UndeclaredTexts,
    EquatableArray<TextOutsideEnums> TextsOutsideEnums);

/// <summary>
/// A text of the default-language set of an enum that isn't a member of its enums (<c>LOC305</c>).
/// </summary>
/// <remarks>
/// The generator reports it here, and <c>TextOutsideContractAnalyzer</c> in every other set. A set of another language
/// that lacks the text fails with <c>CS0535</c>, and a build shows no analyzer diagnostic next to a compiler error, so
/// only the generator can say where the text comes from.
/// </remarks>
internal readonly record struct TextOutsideEnums(
    string Name,
    DiagnosticLocation Location,
    string Enums,
    string Advice);

/// <summary>
/// A text a set inherits from a class of its assembly without declaring it. A contract takes only the texts its set
/// declares, so it leaves such a text out.
/// </summary>
internal readonly record struct UndeclaredText(
    string Name,
    string DeclaringType,
    DiagnosticLocation Location);

/// <summary>
/// An enum a set lists as <c>ILocalizationSet&lt;TEnum&gt;</c>. <see cref="Name"/> is as a message writes it, with its
/// containing types (<c>Order.Status</c>), and <see cref="Members"/> are in the order they're written.
/// </summary>
/// <remarks>
/// <para>
/// An enum of another assembly whose members are marked <c>[Localized]</c> gets its <c>GetText()</c> from that assembly
/// (<see cref="HasTextsOfItsAssembly"/>). Where the set's assembly can call it, the set writes none of its own
/// (<see cref="GetText"/>), because that would make every call ambiguous (<c>CS0121</c>).
/// </para>
/// <para>
/// The <c>GetText()</c> a set writes for an enum of another assembly is internal, so two libraries that each give texts
/// to <c>DayOfWeek</c> don't make a call ambiguous.
/// </para>
/// </remarks>
internal readonly record struct LocalizedEnum(
    EquatableArray<TypeNamePart> Type,
    string FullyQualifiedName,
    string Name,
    string Namespace,
    bool IsPublic,
    EquatableArray<LocalizedEnumMember> Members,
    bool HasTextsOfItsAssembly,
    GetTextAccessibility GetText);

/// <summary>
/// The <c>GetText()</c> a set writes for an enum, if any.
/// </summary>
internal enum GetTextAccessibility
{
    /// <summary>
    /// None, because the set's assembly reaches the one the enum's assembly writes.
    /// </summary>
    None,

    /// <summary>
    /// Internal, for an enum of another assembly or one that isn't public.
    /// </summary>
    Internal,

    /// <summary>
    /// Public, for a public enum of the set's assembly.
    /// </summary>
    Public,
}

/// <summary>
/// A member of an enum a set has a text for.
/// </summary>
/// <param name="Name">The name of the member, which is the name of its text.</param>
/// <param name="IsFirstOfItsValue">Of <c>Default = 0</c> and <c>None = 0</c>, only <c>Default</c> is.</param>
internal readonly record struct LocalizedEnumMember(
    string Name,
    bool IsFirstOfItsValue);

/// <summary>
/// A text's wording when it's a constant, or what the method of a text with parameters writes
/// (<see cref="MethodWording"/>).
/// </summary>
internal readonly record struct TextWording(
    string Name,
    string Wording);

/// <summary>
/// A text a set inherits, with the nearest class declaring it as a cref names it, such as <c>DutchBase{T}</c>.
/// </summary>
internal readonly record struct InheritedText(
    string Name,
    string Namespace,
    string TypeName);

/// <summary>
/// A base type a candidate lists that binds to nothing in the input compilation. <see cref="Namespace"/> is the
/// namespace its qualifier names: <c>""</c> for <c>global::</c>, and <see langword="null"/> for none. The compiler binds
/// it by its site (<see cref="LocalizationDiscovery.BindContracts"/>).
/// </summary>
// A site, and not a location, because a location holds its file's syntax tree and would change with every edit there.
internal readonly record struct UnresolvedBaseType(
    string Name,
    bool IsQualified,
    string? Namespace,
    BaseTypeSite Site);

/// <summary>
/// The generated contract the compiler binds a base type to, where two generated contracts share its name.
/// </summary>
internal readonly record struct ContractBinding(
    BaseTypeSite Site,
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
// Equal by its names and bindings, so an edit that changes neither leaves the later steps of both parts as they were.
internal sealed class GeneratedContracts : IEquatable<GeneratedContracts>
{
    // A hash set, because it's looked up for every base type of every set.
    private HashSet<string> Names { get; }

    // What it's made of, in the order given, for equality.
    private EquatableArray<string> GivenNames { get; }
    private EquatableArray<ContractBinding> GivenBindings { get; }

    /// <summary>
    /// The contract of each simple name, or <see langword="null"/> for a name two contracts share.
    /// </summary>
    private Dictionary<string, string?> BySimpleName { get; }

    private Dictionary<BaseTypeSite, string> Bindings { get; }

    public GeneratedContracts(ImmutableArray<string> names, EquatableArray<ContractBinding> bindings)
    {
        this.GivenNames = new EquatableArray<string>(names);
        this.GivenBindings = bindings;
        this.Names = new HashSet<string>(names, StringComparer.Ordinal);
        this.BySimpleName = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var name in this.Names)
        {
            var simpleName = LocalizationDiscovery.GetSimpleName(name);
            this.BySimpleName[simpleName] = this.BySimpleName.ContainsKey(simpleName) ? null : name;
        }

        this.Bindings = [];

        foreach (var binding in bindings)
            if (!this.Bindings.ContainsKey(binding.Site))
                this.Bindings.Add(binding.Site, binding.Contract);
    }

    public bool Contains(string fullName)
    {
        var contains = this.Names.Contains(fullName);
        return contains;
    }

    /// <summary>
    /// Whether a contract has the simple name. <paramref name="contract"/> is <see langword="null"/> where two share it.
    /// </summary>
    public bool TryFindBySimpleName(string simpleName, out string? contract)
    {
        var found = this.BySimpleName.TryGetValue(simpleName, out contract);
        return found;
    }

    /// <summary>
    /// The contract the compiler bound a base type to, or <see langword="null"/> where it wasn't asked or bound none.
    /// </summary>
    public string? FindBinding(BaseTypeSite site)
    {
        var binding = this.Bindings.TryGetValue(site, out var contract) ? contract : null;
        return binding;
    }

    public bool Equals(GeneratedContracts? other)
    {
        var equals = other is not null && this.GivenNames.Equals(other.GivenNames) && this.GivenBindings.Equals(other.GivenBindings);
        return equals;
    }

    public override bool Equals(object? obj) => this.Equals(obj as GeneratedContracts);

    public override int GetHashCode() => (this.GivenNames.GetHashCode() * 397) ^ this.GivenBindings.GetHashCode();
}

/// <summary>
/// A class of the current assembly a candidate derives from, with the base types it lists itself that bind to nothing.
/// Lives within one transform only, so it may hold a syntax tree.
/// </summary>
internal readonly record struct SourceAncestor(
    string Name,
    ImmutableArray<UnresolvedBaseType> UnresolvedBaseTypes,
    ImmutableArray<BaseTypeLocation> BaseTypeLocations);

/// <summary>
/// One text of a default-language set. With a doc comment, the generated members inherit it. Without one, they're
/// documented with <see cref="DefaultText"/>, its constant wording or what its method writes (<see cref="MethodWording"/>).
/// </summary>
/// <remarks>
/// <see cref="Marks"/> repeats <c>[Obsolete]</c> or <c>[StringSyntax]</c> on the generated member, so code that shows
/// the text is told.
/// </remarks>
internal readonly record struct LocalizationMember(
    string Name,
    bool HasDocComment,
    string? DefaultText,
    EquatableArray<TextParameter> Parameters,
    string? Marks = null)
{
    /// <summary>
    /// Whether the text has parameters, and so is a method of the set, the contract and the generated class.
    /// </summary>
    public bool IsMethod => this.Parameters.Length > 0;
}

/// <summary>
/// A method of a default-language set that has the shape of a text with parameters but can't be one (<c>LOC400</c>).
/// <see cref="Problem"/> is the clause of the message.
/// </summary>
internal readonly record struct UnsupportedText(
    string Name,
    string Problem,
    DiagnosticLocation Location);

/// <summary>
/// A text of a default-language set with a name its generated class can't give it (<c>LOC300</c>). It's reported at
/// the set's member, or at the enum member for a text the set doesn't declare.
/// </summary>
internal readonly record struct ReservedText(
    string Name,
    DiagnosticLocation Location);

/// <summary>
/// A set the generated code registers. Holds no location, so an edit in a set's file doesn't rewrite the generated code.
/// </summary>
internal readonly record struct RegisteredLocalization(
    string MetadataName,
    string FullyQualifiedName,
    bool IsOverride,
    int Depth);

/// <summary>
/// A set that can't be registered automatically (<c>LOC207</c>).
/// </summary>
internal readonly record struct UnregistrableLocalization(
    string MetadataName,
    string Problem,
    DiagnosticLocation Location);

/// <summary>
/// A class that lists <c>ILocalizationSet</c> and an unresolved contract, as a default-language set does, but whose shape
/// the generator can't use: nested, file-local, generic or abstract. <c>LOC200</c> reports it when its language is the
/// default language and no contract it lists comes from another set. The registrar's share holds no location.
/// </summary>
internal readonly record struct UnextendableSet(
    string MetadataName,
    string Name,
    string Problem,
    string? Language,
    EquatableArray<UnresolvedBaseType> UnresolvedBaseTypes,
    DiagnosticLocation Location,
    EquatableArray<BaseTypeLocation> BaseTypeLocations);

/// <summary>
/// What discovery finds in the current compilation: the candidate sets, the nested or file-local rewordings of another
/// assembly's texts (<c>LOC207</c>), and the default-language sets the generator can't use (<c>LOC200</c>), and the nested sets the
/// registrar describes. The last three are ordered by name, and the registrar's share holds only the nested sets.
/// </summary>
internal readonly record struct Discovery(
    EquatableArray<DiscoveredLocalization> Sets,
    EquatableArray<UnregistrableLocalization> UnnameableOverrides,
    EquatableArray<UnextendableSet> UnextendableSets,
    EquatableArray<SetDescription> NestedSets);

/// <summary>
/// Which share of what discovery found a step of the generator reads
/// (<see cref="LocalizationDiscovery.ForPart(DiscoveredLocalization, DiscoveredTexts)"/>), so no step sees what changes
/// only for another.
/// </summary>
internal enum DiscoveredTexts
{
    /// <summary>
    /// What a contract is written from: all of <see cref="DiscoveredLocalization.OfContract"/> and every source location
    /// (<see cref="DiscoveredLocalization.Locations"/>). Both parts of the generator report their diagnostics from it. It
    /// changes with every edit in a set's file.
    /// </summary>
    OfContract,

    /// <summary>
    /// What the registrar is written from. <see cref="DiscoveredLocalization.OfContract"/> and
    /// <see cref="DiscoveredLocalization.Locations"/> stay empty, so it stays equal across an edit of a wording.
    /// </summary>
    OfRegistrar,
}

/// <summary>
/// What discovery finds in one class: a candidate set, or what is reported about a class generated code can't name.
/// <see cref="NestedSet"/> describes a nested set that generated code can name, for a registration by hand.
/// </summary>
internal sealed record DiscoveryResult(
    DiscoveredLocalization? Set,
    UnregistrableLocalization? UnnameableOverride,
    UnextendableSet? UnextendableSet,
    SetDescription? NestedSet = null);
