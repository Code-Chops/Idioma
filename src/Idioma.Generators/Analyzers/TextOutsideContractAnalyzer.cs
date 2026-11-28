using Microsoft.CodeAnalysis.Diagnostics;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Reports a text of a set that its contract doesn't have (<c>LOC304</c>), such as a Dutch text that the
/// default-language set lacks. In a set of an enum, it reports a text that isn't a member of the enum instead
/// (<c>LOC305</c>).
/// </summary>
/// <remarks>
/// <para>
/// The contract is written from the default-language set, so a text only another language declares is never shown. The
/// compiler doesn't warn, because a class may have members its interfaces don't.
/// </para>
/// <para>
/// The generator reports <c>LOC305</c> in the default-language set (<see cref="TextOutsideEnums"/>), because there it
/// makes every other language fail to compile.
/// </para>
/// <para>
/// <c>LOC304</c> skips a member named like a member of an interface the set implements, because that's reported
/// elsewhere (<c>LOC302</c>, <c>LOC403</c>, <c>CS0535</c>). It also skips one named like a member of a base class,
/// which is reported where it's declared.
/// </para>
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

        // A set is written by hand.
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

        // The message names the first contract. A set without one is LOC205.
        var contract = set.AllInterfaces.FirstOrDefault(PackageNames.IsContract);

        if (contract is null)
            return;

        var enums = GetEnums(set);

        // The generator reports such a text of the default-language set, because the build drops this analyzer's
        // diagnostics next to the CS0535 of the other sets.
        var isDefaultSetOfEnum = enums.Length > 0 && IsDefaultSet(set, contract, context.Compilation);

        // Read only for a set that has a text to report.
        HashSet<string>? interfaceMembers = null;
        string? language = null;

        foreach (var member in set.GetMembers())
        {
            if (!IsText(member))
                continue;

            // LOC305 replaces LOC304, also where the contract has the text.
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

            language ??= AnalyzerSupport.DescribeLanguage(set, context.Compilation, context.CancellationToken);

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
    /// Whether the build would discover a member as a text, by its shape.
    /// </summary>
    private static bool IsText(ISymbol member)
    {
        var isText = (LocalizationDiscovery.IsPublicStringProperty(member, out _)
                      || (TextParameters.IsTextMethod(member, out var method) && TextParameters.FindProblem(method) is null))
                     && !LocalizationDiscovery.IsExcluded(member);

        return isText;
    }

    /// <summary>
    /// Whether a set is the default-language set its contract is generated from, as <c>OrderStatusLocalizationEn</c>
    /// is for <c>IOrderStatusLocalization</c>.
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
    /// Whether a base class has a member of the name.
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
    /// The enums of each <c>ILocalizationSet&lt;TEnum&gt;</c> a set implements, by name.
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
    /// Whether a text that isn't an enum member gets <c>LOC305</c> here. Not for a name the generated class takes
    /// (<c>LOC300</c>), a text of another assembly's contract, or one a base set of an enum declares.
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
    /// The advice for <c>LOC305</c>. An enum of another assembly can't get a member, so the text can only move.
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
    /// The advice for <c>LOC304</c>. A set can't add a text to another assembly's contract.
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
