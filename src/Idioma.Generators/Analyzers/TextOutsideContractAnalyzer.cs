using Microsoft.CodeAnalysis.Diagnostics;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Reports a text of a set that its contract doesn't have (<c>LOC304</c>), such as a Dutch text that the
/// default-language set lacks. In a set of an enum, it reports a text that isn't a member of the enum instead
/// (<c>LOC305</c>).
/// </summary>
/// <remarks>
/// <para>
/// The contract is written from the texts of the default-language set. A text that only another language declares isn't
/// in it, so no generated text reads it, and no user sees its wording. It compiles without a warning, because a class
/// may have members that its interfaces don't.
/// </para>
/// <para>
/// A set of an enum lists <c>ILocalizationSet&lt;TEnum&gt;</c>, and has a text for each member of the enum and no
/// other. <c>GetText()</c> never gives a text of another name. Such a text is usually left over from a member that was
/// removed, or names a member that was never added. It's reported in every set that lists the interface, the
/// default-language set too, because the contract has the text then. The generator reports it in the default-language
/// set (<see cref="TextOutsideEnums"/>), because there it makes every other language fail to compile.
/// </para>
/// <para>
/// A member is a text by the shape discovery reads a text by: a public string property, or a method of the shape of a
/// text that takes values. A member marked <c>[ExcludeFromLocalizationDiscovery]</c> isn't reported, and neither is a
/// method that can't be a text (<c>LOC400</c>).
/// </para>
/// <para>
/// <c>LOC304</c> doesn't report these:
/// </para>
/// <list type="bullet">
/// <item>A member of the name of a member of an interface the set implements. What's wrong with it is reported
/// elsewhere: a member that hides a text (<c>LOC302</c>), a property named like a text that takes values
/// (<c>LOC403</c>), or a method with other parameters (<c>CS0535</c>). So the default-language set isn't reported,
/// because its contract has each text it declares.</item>
/// <item>A member of the name of a member of a class the set derives from. It overrides or hides that member, which is
/// reported where it's declared.</item>
/// </list>
/// <para>
/// <c>LOC305</c> doesn't report these, which are still checked for <c>LOC304</c>:
/// </para>
/// <list type="bullet">
/// <item>A text with a name the generated class takes (<c>LOC300</c>).</item>
/// <item>A text of a contract of another assembly, which the set has to implement.</item>
/// <item>A member of the name of a member of a set of an enum the set derives from, which is reported there. A class
/// that isn't such a set isn't analyzed, so a text it declares is reported where it's overridden.</item>
/// </list>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TextOutsideContractAnalyzer : DiagnosticAnalyzer
{
    public const string TextOutsideContractDiagnosticId = "LOC304";
    public const string TextOutsideEnumDiagnosticId = "LOC305";

    private const string ExcludeAdvice = "or mark it [ExcludeFromLocalizationDiscovery] if it isn't a text";

    private static readonly DiagnosticDescriptor TextOutsideContractRule = new(
        id: TextOutsideContractDiagnosticId,
        title: "Text the default-language set lacks",
        messageFormat: "'{0}.{1}' isn't a text of '{2}', so users of {3} won't see it. {4}.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The contract is written from the texts of the default-language set. A text that only another language declares isn't in it, so no generated text reads it, and no user sees its wording. Add it to the default-language set, so that every language words it. A set of a library's contract can't add a text to it, so remove the text there. Mark a member that isn't a text [ExcludeFromLocalizationDiscovery].",
        helpLinkUri: PackageNames.HelpLink(TextOutsideContractDiagnosticId));

    // Shared with LocalizationSetsPart, which reports such a text of the default-language set.
    internal static readonly DiagnosticDescriptor TextOutsideEnumRule = new(
        id: TextOutsideEnumDiagnosticId,
        title: "Text of a set of an enum that isn't a member",
        messageFormat: "'{0}.{1}' isn't a member of {2}, so GetText() never gives it. {3}.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A set of an enum (ILocalizationSet<TEnum>) has a text for each member of the enum, and no other. GetText() never gives a text of another name. Such a text is usually left over from a member that was removed, or names a member that was never added. Add the member to the enum, or move the text to a set of its own. Mark a member that isn't a text [ExcludeFromLocalizationDiscovery].",
        helpLinkUri: PackageNames.HelpLink(TextOutsideEnumDiagnosticId));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [TextOutsideContractRule, TextOutsideEnumRule];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();

        // A set is written by hand, and what a generator writes is not its author's to change.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(Start);
    }

    private static void Start(CompilationStartAnalysisContext context)
    {
        if (context.Compilation.GetTypeByMetadataName(PackageNames.LocalizationSet) is not { } localizationSet)
            return;

        context.RegisterSymbolAction(c => AnalyzeSet(c, localizationSet), SymbolKind.NamedType);
    }

    private static void AnalyzeSet(SymbolAnalysisContext context, INamedTypeSymbol localizationSet)
    {
        if (context.Symbol is not INamedTypeSymbol { TypeKind: TypeKind.Class } set
            || !set.AllInterfaces.Contains(localizationSet, SymbolEqualityComparer.Default))
        {
            return;
        }

        // The contract the message names, which is the first the set implements. A set without one is LOC204.
        var contract = set.AllInterfaces.FirstOrDefault(PackageNames.IsContract);

        if (contract is null)
            return;

        var enums = GetEnums(set);

        // The generator reports such a text of the default-language set, because the build drops this analyzer's
        // diagnostics next to the error that set of another language then gets (CS0535).
        var isDefaultSetOfEnum = enums.Length > 0 && IsDefaultSet(set, contract, context.Compilation);

        // Read once, and only for a set that has a text to report.
        HashSet<string>? interfaceMembers = null;
        string? language = null;

        foreach (var member in set.GetMembers())
        {
            if (!IsText(member))
                continue;

            // A text of a set of an enum that isn't a member is reported as such, also where the contract has it, and
            // in place of LOC304.
            if (enums.Length > 0 && !IsMemberOfAny(enums, member.Name) && IsOutsideEnums(set, member.Name, contract, context.Compilation))
            {
                if (isDefaultSetOfEnum)
                    continue;

                context.ReportDiagnostic(Diagnostic.Create(
                    TextOutsideEnumRule,
                    member.Locations[0],
                    set.Name,
                    member.Name,
                    JoinNames(enums),
                    AdviseOnEnums(enums, member.Name, context.Compilation.Assembly)));

                continue;
            }

            interfaceMembers ??= new HashSet<string>(set.AllInterfaces.SelectMany(static @interface => @interface.MemberNames), StringComparer.Ordinal);

            if (interfaceMembers.Contains(member.Name) || HasInheritedMember(set, member.Name))
                continue;

            language ??= LocalizationDiscovery.ReadLanguage(set, context.Compilation, context.CancellationToken) is { } code
                ? $"'{code}'"
                : "that language";

            context.ReportDiagnostic(Diagnostic.Create(
                TextOutsideContractRule,
                member.Locations[0],
                set.Name,
                member.Name,
                contract.Name,
                language,
                Advise(contract, member.Name, context.Compilation)));
        }
    }

    /// <summary>
    /// Whether a member is a text by its shape, as discovery reads the texts of a default-language set: a public string
    /// property, or a method of the shape of a text that takes values that can be one. A member marked
    /// <c>[ExcludeFromLocalizationDiscovery]</c> is no text, and neither is <c>LocalizationLanguageCode</c>.
    /// </summary>
    private static bool IsText(ISymbol member)
    {
        var isText = (LocalizationDiscovery.IsPublicStringProperty(member, out _)
                      || (TextParameters.IsTextMethod(member, out var method) && TextParameters.FindProblem(method) is null))
                     && !String.Equals(member.Name, LocalizationDiscovery.LanguageCodeMemberName, StringComparison.Ordinal)
                     && !LocalizationDiscovery.IsExcluded(member);

        return isText;
    }

    /// <summary>
    /// Whether a set is the default-language set its contract is generated from: the contract is generated in this
    /// assembly, and named after the set without the default language, as <c>IOrderStatusLocalization</c> is after
    /// <c>OrderStatusLocalizationEn</c>.
    /// </summary>
    private static bool IsDefaultSet(INamedTypeSymbol set, INamedTypeSymbol contract, Compilation compilation)
    {
        var isDefaultSet = SymbolEqualityComparer.Default.Equals(contract.ContainingAssembly, compilation.Assembly)
                           && PackageNames.IsGeneratedContract(contract)
                           && SupportedLanguages.ReadDefaultLanguage(compilation.Assembly) is { } defaultLanguage
                           && String.Equals(LocalizationDiscovery.DeriveBaseName(set.Name, defaultLanguage), LocalizationSetsAnalysis.GetContractBaseName(contract.Name), StringComparison.Ordinal);

        return isDefaultSet;
    }

    /// <summary>
    /// Whether a class the set derives from has a member of the name, which the set's member overrides or hides.
    /// </summary>
    private static bool HasInheritedMember(INamedTypeSymbol set, string name)
    {
        for (var current = set.BaseType; current is not null && current.SpecialType is not SpecialType.System_Object; current = current.BaseType)
        {
            if (current.GetMembers(name).Length > 0)
                return true;
        }

        return false;
    }

    /// <summary>
    /// The enums a set has the texts of, one for each <c>ILocalizationSet&lt;TEnum&gt;</c> it implements, by name. An
    /// argument that binds to no enum, as one another generator writes, is left out.
    /// </summary>
    internal static ImmutableArray<INamedTypeSymbol> GetEnums(INamedTypeSymbol set)
    {
        var enums = set.AllInterfaces
            .Where(PackageNames.IsLocalizationSetOfEnum)
            .Select(static setOfEnum => setOfEnum.TypeArguments[0])
            .OfType<INamedTypeSymbol>()
            .Where(static type => type.TypeKind is TypeKind.Enum)
            .OrderBy(static @enum => @enum.Name, StringComparer.Ordinal)
            .ToImmutableArray();

        return enums;
    }

    internal static bool IsMemberOfAny(ImmutableArray<INamedTypeSymbol> enums, string name)
    {
        var isMember = enums.Any(@enum => @enum.GetMembers(name).Any(static member => member is IFieldSymbol { HasConstantValue: true }));
        return isMember;
    }

    /// <summary>
    /// Whether a text of a set of an enum that isn't a member is reported here. Another code reports a name the
    /// generated class takes (<c>LOC300</c>). A text of a contract of another assembly is that assembly's, and the set
    /// has to implement it. A set of an enum the set derives from reports a text it declares, which the set overrides or
    /// hides. A class that isn't such a set isn't analyzed, so its text is reported where it's overridden.
    /// </summary>
    private static bool IsOutsideEnums(INamedTypeSymbol set, string name, INamedTypeSymbol contract, Compilation compilation)
    {
        var textsName = LocalizationSetsAnalysis.GetContractBaseName(contract.Name);

        if (LocalizationSetsAnalysis.IsReservedTextName(name, textsName, hasLookup: true))
            return false;

        foreach (var @interface in set.AllInterfaces)
        {
            if (!SymbolEqualityComparer.Default.Equals(@interface.ContainingAssembly, compilation.Assembly)
                && PackageNames.IsContract(@interface)
                && @interface.MemberNames.Contains(name, StringComparer.Ordinal))
            {
                return false;
            }
        }

        for (var current = set.BaseType; current is not null && current.SpecialType is not SpecialType.System_Object; current = current.BaseType)
        {
            if (current.GetMembers(name).Length > 0 && GetEnums(current).Length > 0)
                return false;
        }

        return true;
    }

    /// <summary>
    /// The names of enums as a message lists them: <c>'OrderStatus'</c>, or <c>'OrderStatus' or 'PaymentStatus'</c>.
    /// </summary>
    internal static string JoinNames(IReadOnlyList<INamedTypeSymbol> enums)
    {
        var names = enums.Select(static @enum => $"'{@enum.Name}'").ToArray();

        var joined = names.Length is 1
            ? names[0]
            : $"{String.Join(", ", names, 0, names.Length - 1)} or {names[^1]}";

        return joined;
    }

    /// <summary>
    /// What to do about a text of a set of an enum that isn't a member, as the last sentence of the message. An enum of
    /// another assembly, such as <c>DayOfWeek</c>, can't get a member, so the text can only move.
    /// </summary>
    internal static string AdviseOnEnums(ImmutableArray<INamedTypeSymbol> enums, string text, IAssemblySymbol assembly)
    {
        var own = enums.Where(@enum => SymbolEqualityComparer.Default.Equals(@enum.ContainingAssembly, assembly)).ToArray();

        var advice = own.Length > 0
            ? $"Add '{text}' to {JoinNames(own)}, move it to a set of its own, {ExcludeAdvice}"
            : $"Move it to a set of its own, {ExcludeAdvice}";

        return advice;
    }

    /// <summary>
    /// What to do, as the last sentence of the message. A text goes in the default-language set, which a generated
    /// contract is written from, or in a contract written by hand. A set can't add a text to a contract of another
    /// assembly.
    /// </summary>
    private static string Advise(INamedTypeSymbol contract, string text, Compilation compilation)
    {
        if (!SymbolEqualityComparer.Default.Equals(contract.ContainingAssembly, compilation.Assembly))
            return $"Remove it, {ExcludeAdvice}";

        var advice = PackageNames.IsGeneratedContract(contract)
            ? $"Add '{text}' to the default-language set, {ExcludeAdvice}"
            : $"Add '{text}' to '{contract.Name}', {ExcludeAdvice}";

        return advice;
    }
}
