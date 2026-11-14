using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Reports a string compared as a text (<c>LOC017</c>): <c>text.Equals("Afrekenen")</c>, <c>texts.Contains(wording)</c>,
/// <c>Array.IndexOf(texts, wording)</c>, <c>counts[wording]</c> on a dictionary of texts, and the other lookups of a
/// collection. These compare texts, not the wording they show, and the string becomes a text without translations,
/// which equals no translated text, even one that shows the same wording.
/// </summary>
/// <remarks>
/// Applies wherever the package is referenced, whatever languages the assembly declares: a text of a set equals no
/// string, also in an assembly with one language or none. A string cast to a text, <c>(LocalizableText)wording</c>, is
/// compared as a text on purpose and passes, and so does <c>wording.Equals(text)</c>, which compares the wording, as
/// <c>==</c> does. A comparer passed to the call, as in <c>texts.Contains(wording, comparer)</c> or
/// <c>comparer.Equals(text, wording)</c>, may compare the wording, so only the default one is reported; a comparer given
/// to a collection as it was made cannot be seen at the lookup. Generated code is left alone, except what Razor generates
/// from the user's markup.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TextComparisonAnalyzer : DiagnosticAnalyzer
{
    public const string StringComparedAsTextDiagnosticId = "LOC017";

    /// <summary>
    /// The methods of a collection, and of LINQ, that find an item by <c>Equals</c>. <c>GetOrAdd</c> and
    /// <c>AddOrUpdate</c> of a concurrent dictionary add an entry beside the one they do not find.
    /// </summary>
    private static readonly ImmutableHashSet<string> LookupNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "AddOrUpdate", "Contains", "ContainsKey", "ContainsValue", "GetOrAdd", "GetValueOrDefault", "IndexOf", "IndexOfKey",
        "IndexOfValue", "LastIndexOf", "Remove", "TryGetValue", "TryRemove", "TryUpdate");

    private static readonly DiagnosticDescriptor StringComparedAsTextRule = new(
        id: StringComparedAsTextDiagnosticId,
        title: "String compared as a text",
        messageFormat: "'{0}' compares texts, not the wording they show: this string becomes a text without translations, which equals no translated text. Compare the wording with ==, or cast the string to LocalizableText to compare texts.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Equals, and a lookup of a collection such as Contains, IndexOf, Remove, or the indexer of a dictionary, compares texts: the same default wording and the same translations. A string converts to a text without translations, so it equals no translated text, even while that text shows the same wording. The == operator compares a text with the wording for the current UI culture.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(StringComparedAsTextRule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();

        // Razor's output counts as generated code, and a comparison in a component's code must be caught there.
        // OneLanguageTextAnalyzer.IsHandWritten leaves out the rest.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.RegisterCompilationStartAction(Start);
    }

    private static void Start(CompilationStartAnalysisContext context)
    {
        if (context.Compilation.GetTypeByMetadataName(PackageNames.LocalizableText) is not { } text)
            return;

        context.RegisterOperationAction(c => Analyze(c, text), OperationKind.Conversion);
        context.RegisterOperationAction(c => AnalyzeObjectComparison(c, text), OperationKind.Invocation);
    }

    /// <summary>
    /// Reports <c>Array.IndexOf(texts, wording)</c>, <c>LastIndexOf</c>, and <c>object.Equals(text, wording)</c>. These
    /// ask for an <see cref="object"/>, which the compiler takes over a generic overload, so the string is never
    /// converted to a text, and no conversion shows it. They compare texts all the same, by
    /// <see cref="object.Equals(object)"/>, so they find nothing.
    /// </summary>
    private static void AnalyzeObjectComparison(OperationAnalysisContext context, INamedTypeSymbol text)
    {
        var invocation = (IInvocationOperation)context.Operation;

        if (invocation.TargetMethod is not { IsStatic: true } method || invocation.Arguments.Length < 2)
            return;

        var first = Unwrap(invocation.Arguments[0].Value);
        var second = Unwrap(invocation.Arguments[1].Value);

        var wording = method switch
        {
            {
                Name: "IndexOf" or "LastIndexOf",
                ContainingType: { Name: "Array", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } },
                Parameters: [{ Type.SpecialType: SpecialType.System_Array }, { Type.SpecialType: SpecialType.System_Object }, ..],
            } when first.Type is IArrayTypeSymbol { ElementType: INamedTypeSymbol element } && InheritsFrom(element, text) && IsString(second)
                => invocation.Arguments[1],

            // Either way round: object.Equals calls the Equals of its first argument, and neither a text nor a string
            // equals an object of the other type.
            { Name: "Equals", ContainingType.SpecialType: SpecialType.System_Object, Parameters.Length: 2 }
                => IsText(first) && IsString(second) ? invocation.Arguments[1]
                    : IsString(first) && IsText(second) ? invocation.Arguments[0]
                    : null,

            _ => null,
        };

        if (wording is not null && OneLanguageTextAnalyzer.IsHandWritten(context.IsGeneratedCode, invocation.Syntax.SyntaxTree))
            context.ReportDiagnostic(Diagnostic.Create(StringComparedAsTextRule, wording.Value.Syntax.GetLocation(), $"{method.ContainingType.Name}.{method.Name}"));

        bool IsText(IOperation operation) => InheritsFrom(operation.Type as INamedTypeSymbol, text);

        static bool IsString(IOperation operation) => operation.Type is { SpecialType: SpecialType.System_String };
    }

    /// <summary>
    /// What the compiler converted to a parameter's type (<see cref="Array"/>, <see cref="object"/>, an interface), to
    /// the operation it converted.
    /// </summary>
    private static IOperation Unwrap(IOperation operation)
    {
        while (operation is IConversionOperation { IsImplicit: true } conversion)
            operation = conversion.Operand;

        return operation;
    }

    private static void Analyze(OperationAnalysisContext context, INamedTypeSymbol text)
    {
        var conversion = (IConversionOperation)context.Operation;

        if (FindComparison(conversion, text) is { } member
            && OneLanguageTextAnalyzer.IsHandWritten(context.IsGeneratedCode, conversion.Syntax.SyntaxTree))
        {
            // An indexer is named as C# names it: Dictionary.this[].
            var name = member is IPropertySymbol { IsIndexer: true } ? "this[]" : member.Name;

            context.ReportDiagnostic(Diagnostic.Create(StringComparedAsTextRule, conversion.Syntax.GetLocation(), $"{member.ContainingType.Name}.{name}"));
        }
    }

    /// <summary>
    /// The method or indexer that compares a string as a text, converted without a cast, or <see langword="null"/> when
    /// the text it becomes is not compared: passed on, stored, or added to a collection.
    /// </summary>
    internal static ISymbol? FindComparison(IConversionOperation conversion, INamedTypeSymbol text)
    {
        if (!conversion.IsImplicit
            || conversion.OperatorMethod is not { Parameters: [{ Type.SpecialType: SpecialType.System_String }] } @operator
            || !SymbolEqualityComparer.Default.Equals(@operator.ContainingType, text)
            || conversion.Parent is not IArgumentOperation { Parameter: { } parameter } argument)
        {
            return null;
        }

        // Of a generic collection or comparer, which compares its items by Equals.
        var isItem = parameter.OriginalDefinition.Type is ITypeParameterSymbol;

        switch (argument.Parent)
        {
            case IInvocationOperation { TargetMethod: var method } invocation:
            {
                // Equals compares each of its arguments. A lookup compares only the item it looks for, its first argument
                // after the collection, and not a value to return when it finds none. The collection comes first in a
                // static method too, not only in an extension: Array.IndexOf(texts, wording).
                var compares = method.Name is "Equals"
                    ? isItem || InheritsFrom(method.ContainingType, text)
                    : isItem && LookupNames.Contains(method.Name) && parameter.Ordinal == (method.IsStatic ? 1 : 0);

                return compares && !UsesOtherComparer(invocation) ? method : null;
            }

            // The key of a dictionary read through its indexer: counts[wording] finds the key by Equals as ContainsKey
            // does. Written, as in counts[wording] = 1, the key is stored, like an item added to a collection.
            case IPropertyReferenceOperation { Property.IsIndexer: true } reference when isItem && !IsWrittenOnly(reference):
                return reference.Property;

            default:
                return null;
        }
    }

    /// <summary>
    /// Whether a comparer other than the default one compares: one passed to the lookup, as in
    /// <c>texts.Contains(wording, comparer)</c>, or the one whose <c>Equals</c> is called, as in
    /// <c>comparer.Equals(text, wording)</c>. It may compare the wording. <see langword="null"/>, <c>default</c>, a
    /// comparer left out, and <c>EqualityComparer&lt;T&gt;.Default</c> compare by <c>Equals</c>.
    /// </summary>
    private static bool UsesOtherComparer(IInvocationOperation invocation)
    {
        if (invocation.TargetMethod is { Name: "Equals", IsStatic: false } method && IsComparerType(method.ContainingType))
            return invocation.Instance is not { } comparer || !IsDefaultComparer(comparer);

        foreach (var argument in invocation.Arguments)
        {
            if (argument.Parameter is { Type: INamedTypeSymbol type } && IsComparerType(type)
                && argument.ArgumentKind is not ArgumentKind.DefaultValue
                && !IsDefaultComparer(argument.Value))
            {
                return true;
            }
        }

        return false;

        static bool IsComparerType(INamedTypeSymbol type)
        {
            return type.OriginalDefinition is
            {
                Name: "IEqualityComparer" or "EqualityComparer",
                Arity: 1,
                ContainingNamespace: { Name: "Generic", ContainingNamespace: { Name: "Collections", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } } },
            };
        }

        static bool IsDefaultComparer(IOperation operation)
        {
            return Unwrap(operation) switch
            {
                { ConstantValue: { HasValue: true, Value: null } } or IDefaultValueOperation => true,
                IPropertyReferenceOperation { Property: { Name: "Default", IsStatic: true, ContainingType: var type } } => IsComparerType(type),
                _ => false,
            };
        }
    }

    /// <summary>
    /// Whether an indexer is only assigned to (<c>counts[key] = 1</c>, also in an object initializer or as one of the
    /// targets of a deconstruction, <c>(counts[key], other) = (1, 2)</c>), which adds an entry instead of finding one.
    /// <c>counts[key] += 1</c> reads the entry first.
    /// </summary>
    private static bool IsWrittenOnly(IPropertyReferenceOperation reference)
    {
        if (reference.Parent is ISimpleAssignmentOperation assignment)
            return assignment.Target == reference;

        // The targets of a deconstruction are a tuple, which may hold tuples of its own: (counts[key], (a, b)) = ....
        IOperation target = reference;

        while (target.Parent is ITupleOperation tuple)
            target = tuple;

        var isWrittenOnly = target != reference
                            && target.Parent is IDeconstructionAssignmentOperation deconstruction
                            && deconstruction.Target == target;

        return isWrittenOnly;
    }

    private static bool InheritsFrom(INamedTypeSymbol? type, INamedTypeSymbol text)
    {
        for (; type is not null; type = type.BaseType)
            if (SymbolEqualityComparer.Default.Equals(type, text))
                return true;

        return false;
    }
}
