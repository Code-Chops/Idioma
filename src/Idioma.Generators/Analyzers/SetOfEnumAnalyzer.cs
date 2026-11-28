using Microsoft.CodeAnalysis.Diagnostics;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Reports a set whose <c>ILocalizationSet&lt;TEnum&gt;</c> doesn't match the enums of its contract (<c>LOC206</c>):
/// a Dutch set that lists <c>ILocalizationSet</c> where the contract is of an enum, or a set that lists
/// <c>ILocalizationSet&lt;TEnum&gt;</c> for an enum its contract has no texts of.
/// </summary>
/// <remarks>
/// A missing enum is reported once per set, at what it takes the place of or else at the set. These aren't reported:
/// <list type="bullet">
/// <item>An enum missing from a set that doesn't list the contract itself, because its base set is reported.</item>
/// <item>An enum the set's assembly can't name.</item>
/// <item>A contract written by hand.</item>
/// </list>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SetOfEnumAnalyzer : DiagnosticAnalyzer
{
    public const string SetOfEnumDiagnosticId = "LOC206";

    private const string Title = "Set whose enums don't match its contract";
    private const string Description = "The contract of a set of an enum has a text for each member of the enum. Every set of it lists ILocalizationSet<TEnum>, in each language, and no other enum, so each set shows which enum it words. List it in place of ILocalizationSet.";

    // One descriptor for a missing enum and an extra one, so an IDE lists the code once.
    private static readonly DiagnosticDescriptor SetOfEnumRule = new(
        id: SetOfEnumDiagnosticId,
        title: Title,
        messageFormat: "{0}. {1}.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description,
        helpLinkUri: PackageNames.HelpLink(SetOfEnumDiagnosticId));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [SetOfEnumRule];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();

        // A set is written by hand.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(Start);
    }

    private static void Start(CompilationStartAnalysisContext context)
    {
        if (context.Compilation.GetTypeByMetadataName(PackageNames.LocalizationSet) is null)
            return;

        // Per part, so each base list binds with its own model.
        context.RegisterSyntaxNodeAction(AnalyzePart, SyntaxKind.ClassDeclaration, SyntaxKind.RecordDeclaration);
    }

    private static void AnalyzePart(SyntaxNodeAnalysisContext context)
    {
        var part = (TypeDeclarationSyntax)context.Node;

        if (context.SemanticModel.GetDeclaredSymbol(part, context.CancellationToken) is not { TypeKind: TypeKind.Class } set
            || Plan(set, context.Compilation) is not { } plan)
        {
            return;
        }

        foreach (var entry in part.BaseList is { } baseList ? baseList.Types : [])
        {
            if (context.SemanticModel.GetTypeInfo(entry.Type, context.CancellationToken).Type is not INamedTypeSymbol listed)
                continue;

            if (plan.Replaced is { } replaced && SymbolEqualityComparer.Default.Equals(listed, replaced))
                ReportMissing(context, set, plan, entry.GetLocation());
            else if (plan.FindExtra(listed) is { } extra)
                ReportExtra(context, set, plan, extra, entry.GetLocation());
        }

        // Nothing to replace, so at the first part of the set.
        if (plan is { Missing.IsEmpty: false, Replaced: null } && IsFirstPart(set, part))
            ReportMissing(context, set, plan, part.Identifier.GetLocation());
    }

    private static bool IsFirstPart(INamedTypeSymbol set, TypeDeclarationSyntax part)
    {
        var first = set.DeclaringSyntaxReferences[0];

        var isFirst = first.SyntaxTree == part.SyntaxTree && first.Span == part.Span;
        return isFirst;
    }

    private static void ReportMissing(SyntaxNodeAnalysisContext context, INamedTypeSymbol set, SetOfEnumPlan plan, Location location)
    {
        var pronoun = plan.Missing.Length is 1 ? "it" : "them";

        var advice = plan.Replaced is { } replaced
            ? $"List {pronoun} in place of '{Name(replaced)}'"
            : $"List {pronoun} next to '{plan.Contract.Name}'";

        context.ReportDiagnostic(Diagnostic.Create(
            SetOfEnumRule,
            location,
            $"'{set.Name}' doesn't list {JoinNames(plan.Missing)}, which every set of '{plan.Contract.Name}' lists",
            advice));
    }

    private static void ReportExtra(SyntaxNodeAnalysisContext context, INamedTypeSymbol set, SetOfEnumPlan plan, INamedTypeSymbol @enum, Location location)
    {
        var fix = plan.IsReplacedBySetInterface(@enum)
            ? $"Replace it with '{PackageNames.LocalizationSetName}'"
            : "Remove it";

        // A contract generated here takes its enums from the default-language set, which can list the enum too.
        var advice = plan.IsOwnContract
            ? $"{fix}, or list it in the default-language set too"
            : fix;

        context.ReportDiagnostic(Diagnostic.Create(
            SetOfEnumRule,
            location,
            $"'{set.Name}' lists '{PackageNames.LocalizationSetName}<{@enum.Name}>', but '{plan.Contract.Name}' has no texts of '{@enum.Name}'",
            advice));
    }

    /// <summary>
    /// What's wrong with the <c>ILocalizationSet&lt;TEnum&gt;</c> a set lists, and how the fix changes it, or
    /// <see langword="null"/> when nothing is.
    /// </summary>
    internal static SetOfEnumPlan? Plan(INamedTypeSymbol set, Compilation compilation)
    {
        if (!set.AllInterfaces.Any(PackageNames.IsLocalizationSet))
            return null;

        // Only a generated contract names its enums.
        var contracts = set.AllInterfaces
            .Where(PackageNames.IsGeneratedContract)
            .Select(contract => new GeneratedContract(contract, PackageNames.GetContractEnums(contract), SymbolEqualityComparer.Default.Equals(contract.ContainingAssembly, compilation.Assembly)))
            .ToArray();

        if (contracts.Length is 0)
            return null;

        // Missing only where the set lists the contract itself, because a derived set inherits its base's enums.
        var missing = contracts
            .Where(contract => set.Interfaces.Contains(contract.Contract, SymbolEqualityComparer.Default))
            .SelectMany(static contract => contract.Enums)
            .Where(@enum => compilation.IsSymbolAccessibleWithin(@enum, compilation.Assembly) && !ListsSetOfEnum(set, @enum))
            .Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default)
            .ToImmutableArray();

        var extras = set.Interfaces
            .Where(static @interface => PackageNames.IsLocalizationSetOfEnum(@interface) && @interface.TypeArguments[0].TypeKind is TypeKind.Enum)
            .Where(setOfEnum => !contracts.Any(contract => contract.Enums.Any(@enum => SymbolEqualityComparer.Default.Equals(@enum, setOfEnum.TypeArguments[0]))))
            .ToImmutableArray();

        if (missing.IsEmpty && extras.IsEmpty)
            return null;

        var replaced = missing.IsEmpty
            ? null
            : set.Interfaces.FirstOrDefault(PackageNames.IsLocalizationSet) ?? extras.FirstOrDefault();

        // Whether the set still implements ILocalizationSet once its wrong enums are gone.
        var keepsSetInterface = !missing.IsEmpty
                                || set.BaseType?.AllInterfaces.Any(PackageNames.IsLocalizationSet) is true
                                || set.Interfaces.Any(@interface => !extras.Contains(@interface, SymbolEqualityComparer.Default)
                                                                    && (PackageNames.IsLocalizationSet(@interface) || @interface.AllInterfaces.Any(PackageNames.IsLocalizationSet)));

        var named = contracts.FirstOrDefault(contract => set.Interfaces.Contains(contract.Contract, SymbolEqualityComparer.Default)) ?? contracts[0];

        return new SetOfEnumPlan(named.Contract, missing, extras, replaced, keepsSetInterface, named.IsOwn);
    }

    private static bool ListsSetOfEnum(INamedTypeSymbol set, INamedTypeSymbol @enum)
    {
        var lists = set.AllInterfaces.Any(@interface => PackageNames.IsLocalizationSetOfEnum(@interface)
                                                        && SymbolEqualityComparer.Default.Equals(@interface.TypeArguments[0], @enum));

        return lists;
    }

    /// <summary>
    /// <c>ILocalizationSet</c> or <c>ILocalizationSet&lt;OrderStatus&gt;</c>, as a message names it.
    /// </summary>
    private static string Name(INamedTypeSymbol setInterface)
    {
        var name = PackageNames.IsLocalizationSetOfEnum(setInterface)
            ? $"{PackageNames.LocalizationSetName}<{setInterface.TypeArguments[0].Name}>"
            : PackageNames.LocalizationSetName;

        return name;
    }

    /// <summary>
    /// The interfaces a message names, joined with <c>and</c>.
    /// </summary>
    private static string JoinNames(ImmutableArray<INamedTypeSymbol> enums)
    {
        var names = enums.Select(static @enum => $"'{PackageNames.LocalizationSetName}<{@enum.Name}>'").ToArray();

        var joined = names.Length is 1
            ? names[0]
            : $"{String.Join(", ", names, 0, names.Length - 1)} and {names[^1]}";

        return joined;
    }

    private sealed record GeneratedContract(INamedTypeSymbol Contract, ImmutableArray<INamedTypeSymbol> Enums, bool IsOwn);
}

/// <summary>
/// What's wrong with the <c>ILocalizationSet&lt;TEnum&gt;</c> a set lists (<c>LOC206</c>), as
/// <see cref="SetOfEnumAnalyzer.Plan"/> finds it.
/// </summary>
/// <param name="Contract">The contract a message names: the first the set lists itself.</param>
/// <param name="Extras">
/// Each <c>ILocalizationSet&lt;TEnum&gt;</c> the set lists for an enum its contracts don't have.
/// </param>
/// <param name="Replaced">
/// The interface the missing enums take the place of, or <see langword="null"/> where they go next to the contract.
/// </param>
/// <param name="KeepsSetInterface">
/// Whether the set implements <c>ILocalizationSet</c> without <paramref name="Extras"/>.
/// </param>
/// <param name="IsOwnContract">Whether this assembly generates <paramref name="Contract"/>.</param>
internal sealed record SetOfEnumPlan(
    INamedTypeSymbol Contract,
    ImmutableArray<INamedTypeSymbol> Missing,
    ImmutableArray<INamedTypeSymbol> Extras,
    INamedTypeSymbol? Replaced,
    bool KeepsSetInterface,
    bool IsOwnContract)
{
    /// <summary>
    /// The enum of a listed interface that's one of <see cref="Extras"/> but not <see cref="Replaced"/>, or
    /// <see langword="null"/>.
    /// </summary>
    public INamedTypeSymbol? FindExtra(INamedTypeSymbol listed)
    {
        if (!this.Extras.Contains(listed, SymbolEqualityComparer.Default) || SymbolEqualityComparer.Default.Equals(listed, this.Replaced))
            return null;

        return listed.TypeArguments[0] as INamedTypeSymbol;
    }

    /// <summary>
    /// Whether the fix puts <c>ILocalizationSet</c> in the place of this enum's interface, so the set keeps implementing it.
    /// </summary>
    public bool IsReplacedBySetInterface(INamedTypeSymbol @enum)
    {
        var isReplaced = !this.KeepsSetInterface
                         && this.Extras.Length > 0
                         && SymbolEqualityComparer.Default.Equals(this.Extras[0].TypeArguments[0], @enum);

        return isReplaced;
    }
}
