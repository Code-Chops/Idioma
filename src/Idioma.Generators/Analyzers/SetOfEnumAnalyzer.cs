using Microsoft.CodeAnalysis.Diagnostics;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Reports a set whose <c>ILocalizationSet&lt;TEnum&gt;</c> doesn't match the enums of its contract (<c>LOC205</c>):
/// a Dutch set that lists <c>ILocalizationSet</c> where the contract is of an enum, or a set that lists
/// <c>ILocalizationSet&lt;TEnum&gt;</c> for an enum its contract has no texts of.
/// </summary>
/// <remarks>
/// <para>
/// The contract of a set of an enum names its enums, as in <c>[LocalizationContract(typeof(OrderStatus))]</c>. Every
/// set of it lists <c>ILocalizationSet&lt;OrderStatus&gt;</c>, in each language, and no other enum, so each set shows
/// which enum it words. It compiles either way, because the interface has no members, so this is a warning.
/// </para>
/// <para>
/// A missing enum is reported once per set: at the <c>ILocalizationSet</c> or the wrong enum it takes the place of, or
/// else at the set. Each other wrong enum is reported where it's listed. These aren't reported:
/// </para>
/// <list type="bullet">
/// <item>An enum missing from a set that lists no such contract itself, as a regional set that only derives from the
/// set of its language. It lists what that set lists, which is reported there.</item>
/// <item>An enum the set's assembly can't name, as an internal enum of a library, which no set there can list.</item>
/// <item>A contract written by hand. A contract another assembly generated without naming an enum isn't checked
/// either, because a version of this package before the enums were named wrote it so.</item>
/// </list>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SetOfEnumAnalyzer : DiagnosticAnalyzer
{
    public const string SetOfEnumDiagnosticId = "LOC205";

    private const string Title = "Set whose enums don't match its contract";
    private const string Description = "The contract of a set of an enum has a text for each member of the enum. Every set of it lists ILocalizationSet<TEnum>, in each language, and no other enum, so each set shows which enum it words. List it in place of ILocalizationSet.";

    private static readonly DiagnosticDescriptor MissingEnumRule = new(
        id: SetOfEnumDiagnosticId,
        title: Title,
        messageFormat: "'{0}' doesn't list {1}, which every set of '{2}' lists. {3}.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description,
        helpLinkUri: PackageNames.HelpLink(SetOfEnumDiagnosticId));

    private static readonly DiagnosticDescriptor ExtraEnumRule = new(
        id: SetOfEnumDiagnosticId,
        title: Title,
        messageFormat: "'{0}' lists '{1}', but '{2}' has no texts of '{3}'. {4}.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description,
        helpLinkUri: PackageNames.HelpLink(SetOfEnumDiagnosticId));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [MissingEnumRule, ExtraEnumRule];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();

        // A set is written by hand, and what a generator writes is not its author's to change.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(Start);
    }

    private static void Start(CompilationStartAnalysisContext context)
    {
        if (context.Compilation.GetTypeByMetadataName(PackageNames.LocalizationSet) is null)
            return;

        // Each part of a class, with the model that binds what its base list names.
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

        // Next to the contract, as nothing takes its place: at the first part of the set.
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
            MissingEnumRule,
            location,
            set.Name,
            JoinNames(plan.Missing),
            plan.Contract.Name,
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
            ExtraEnumRule,
            location,
            set.Name,
            $"{PackageNames.LocalizationSetName}<{@enum.Name}>",
            plan.Contract.Name,
            @enum.Name,
            advice));
    }

    /// <summary>
    /// What's wrong with the <c>ILocalizationSet&lt;TEnum&gt;</c> a set lists, and what the fix does about it, or
    /// <see langword="null"/> when nothing is. The fix lists each missing enum in the place of <c>ILocalizationSet</c>,
    /// or else of the first wrong enum, or else next to the contract. It removes every other wrong enum. A set that
    /// would then implement <c>ILocalizationSet</c> no more gets it in the place of its first wrong enum.
    /// </summary>
    internal static SetOfEnumPlan? Plan(INamedTypeSymbol set, Compilation compilation)
    {
        if (!set.AllInterfaces.Any(PackageNames.IsLocalizationSet))
            return null;

        // The contracts whose enums are known: each one generated here, and one of another assembly that names an
        // enum. One that names none may be of an enum, written by a version before the enums were named.
        var contracts = set.AllInterfaces
            .Where(PackageNames.IsGeneratedContract)
            .Select(contract => new KnownContract(contract, PackageNames.GetContractEnums(contract), SymbolEqualityComparer.Default.Equals(contract.ContainingAssembly, compilation.Assembly)))
            .ToArray();

        var known = contracts.Where(static contract => contract.IsOwn || contract.Enums.Length > 0).ToArray();

        if (known.Length is 0)
            return null;

        // An enum is missing only where the set lists the contract itself: a set deriving from another lists its enums.
        var missing = known
            .Where(contract => set.Interfaces.Contains(contract.Contract, SymbolEqualityComparer.Default))
            .SelectMany(static contract => contract.Enums)
            .Where(@enum => compilation.IsSymbolAccessibleWithin(@enum, compilation.Assembly) && !ListsSetOfEnum(set, @enum))
            .Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default)
            .ToImmutableArray();

        // An enum is wrong only where every contract of the set is known, as its enums are then all known.
        var extras = known.Length == contracts.Length
            ? set.Interfaces
                .Where(static @interface => PackageNames.IsLocalizationSetOfEnum(@interface) && @interface.TypeArguments[0].TypeKind is TypeKind.Enum)
                .Where(setOfEnum => !known.Any(contract => contract.Enums.Any(@enum => SymbolEqualityComparer.Default.Equals(@enum, setOfEnum.TypeArguments[0]))))
                .ToImmutableArray()
            : [];

        if (missing.IsEmpty && extras.IsEmpty)
            return null;

        var replaced = missing.IsEmpty
            ? null
            : set.Interfaces.FirstOrDefault(PackageNames.IsLocalizationSet) ?? extras.FirstOrDefault();

        // Without its wrong enums, the set still implements ILocalizationSet through a missing enum the fix lists, another
        // interface it lists, or a class it derives from.
        var keepsSetInterface = !missing.IsEmpty
                                || set.BaseType?.AllInterfaces.Any(PackageNames.IsLocalizationSet) is true
                                || set.Interfaces.Any(@interface => !extras.Contains(@interface, SymbolEqualityComparer.Default)
                                                                    && (PackageNames.IsLocalizationSet(@interface) || @interface.AllInterfaces.Any(PackageNames.IsLocalizationSet)));

        var named = known.FirstOrDefault(contract => set.Interfaces.Contains(contract.Contract, SymbolEqualityComparer.Default)) ?? known[0];

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
    /// The interfaces a message names: <c>'ILocalizationSet&lt;OrderStatus&gt;'</c>, or two or more joined with
    /// <c>and</c>.
    /// </summary>
    private static string JoinNames(ImmutableArray<INamedTypeSymbol> enums)
    {
        var names = enums.Select(static @enum => $"'{PackageNames.LocalizationSetName}<{@enum.Name}>'").ToArray();

        var joined = names.Length is 1
            ? names[0]
            : $"{String.Join(", ", names, 0, names.Length - 1)} and {names[^1]}";

        return joined;
    }

    private sealed record KnownContract(INamedTypeSymbol Contract, ImmutableArray<INamedTypeSymbol> Enums, bool IsOwn);
}

/// <summary>
/// What's wrong with the <c>ILocalizationSet&lt;TEnum&gt;</c> a set lists (<c>LOC205</c>), as
/// <see cref="SetOfEnumAnalyzer.Plan"/> finds it.
/// </summary>
/// <param name="Contract">The contract a message names: the first the set lists itself whose enums are known.</param>
/// <param name="Missing">The enums of the contracts the set lists itself, which it doesn't list.</param>
/// <param name="Extras">Each <c>ILocalizationSet&lt;TEnum&gt;</c> the set lists for an enum its contracts don't
/// have.</param>
/// <param name="Replaced">The interface the missing enums take the place of: <c>ILocalizationSet</c>, or else the
/// first of <paramref name="Extras"/>. <see langword="null"/> where they go next to the contract.</param>
/// <param name="KeepsSetInterface">Whether the set implements <c>ILocalizationSet</c> without
/// <paramref name="Extras"/>.</param>
/// <param name="IsOwnContract">Whether this assembly generates <paramref name="Contract"/>, so that its default-language
/// set can list an enum too.</param>
internal sealed record SetOfEnumPlan(
    INamedTypeSymbol Contract,
    ImmutableArray<INamedTypeSymbol> Missing,
    ImmutableArray<INamedTypeSymbol> Extras,
    INamedTypeSymbol? Replaced,
    bool KeepsSetInterface,
    bool IsOwnContract)
{
    /// <summary>
    /// The enum of a listed interface that is one of <see cref="Extras"/>, other than the one the missing enums take the
    /// place of. <see langword="null"/> for any other interface.
    /// </summary>
    public INamedTypeSymbol? FindExtra(INamedTypeSymbol listed)
    {
        if (!this.Extras.Contains(listed, SymbolEqualityComparer.Default) || SymbolEqualityComparer.Default.Equals(listed, this.Replaced))
            return null;

        return listed.TypeArguments[0] as INamedTypeSymbol;
    }

    /// <summary>
    /// Whether the fix puts <c>ILocalizationSet</c> in the place of the interface of this enum: the first wrong enum of a
    /// set that would implement <c>ILocalizationSet</c> no more.
    /// </summary>
    public bool IsReplacedBySetInterface(INamedTypeSymbol @enum)
    {
        var isReplaced = !this.KeepsSetInterface
                         && this.Extras.Length > 0
                         && SymbolEqualityComparer.Default.Equals(this.Extras[0].TypeArguments[0], @enum);

        return isReplaced;
    }
}
