using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Reports a text that is read once and kept for the whole app: one in the initializer of a static field or a static
/// property (<c>LOC602</c>), as in <c>static readonly string Label = ShopLocalization.Checkout;</c>.
/// </summary>
/// <remarks>
/// A static initializer runs once, so every user gets the wording of the first user's language. A text inside a
/// lambda or a local function isn't reported, because it's read when that runs. Generated code is left alone, except
/// what Razor generates from the user's markup.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class KeptTextAnalyzer : DiagnosticAnalyzer
{
    public const string KeptTextDiagnosticId = "LOC602";

    private const string ContractAttributeName = "LocalizationContractAttribute";

    private static readonly DiagnosticDescriptor KeptTextRule = new(
        id: KeptTextDiagnosticId,
        title: "Text kept in a static member",
        messageFormat: "'{0}' is read once, when '{1}' is initialized, so every user gets the wording of the first user's language. Read the text where you show it, such as in a property written with '=>'.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A text is a string with the wording for the user's language at the moment it's read. The initializer of a static field or a static property runs once, so it keeps the wording of whoever used the class first. On a server, every user then sees that language. A text inside a lambda isn't reported, because it's read when the lambda runs.",
        helpLinkUri: PackageNames.HelpLink(KeptTextDiagnosticId));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [KeptTextRule];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();

        // Razor's output counts as generated code, but a field in a component's @code is the user's own.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.RegisterCompilationStartAction(Start);
    }

    private static void Start(CompilationStartAnalysisContext context)
    {
        // Null in a project that doesn't reference the package, which has no texts.
        if (context.Compilation.GetTypeByMetadataName(PackageNames.LocalizedText) is not { } text)
            return;

        context.RegisterOperationAction(c => AnalyzeInitializer(c, text), OperationKind.FieldInitializer, OperationKind.PropertyInitializer);
    }

    private static void AnalyzeInitializer(OperationAnalysisContext context, INamedTypeSymbol text)
    {
        var initializer = (ISymbolInitializerOperation)context.Operation;

        ISymbol? member = initializer switch
        {
            IFieldInitializerOperation { InitializedFields: [var field, ..] } => field,
            IPropertyInitializerOperation { InitializedProperties: [var property, ..] } => property,
            _ => null,
        };

        if (member is not { IsStatic: true }
            || member is IFieldSymbol { IsConst: true }
            || !AnalyzerSupport.IsHandWritten(context.IsGeneratedCode, initializer.Syntax.SyntaxTree))
        {
            return;
        }

        if (FindText(initializer.Value, text, isEnumerated: false, runsLambdas: false) is { } read)
            context.ReportDiagnostic(Diagnostic.Create(KeptTextRule, read.Syntax.GetLocation(), Describe(read), member.Name));
    }

    /// <summary>
    /// Longer than this, a text is named by what it reads, such as <c>LocalizedText.Inline</c>, rather than written out.
    /// </summary>
    private const int MaxWrittenLength = 80;

    /// <summary>
    /// The text as the code writes it, on one line, such as <c>ShopLocalization.Greeting("Ada")</c>. A long one, such as
    /// an inline text with its wording in every language, by the member it reads.
    /// </summary>
    // On one line, because a build prints a message up to its first line break.
    private static string Describe(IOperation read)
    {
        // Each run of white space as one space, and none inside brackets: LocalizedText.Inline(en: "...", nl: "...").
        var written = new StringBuilder();
        var isSpaced = false;

        foreach (var character in read.Syntax.ToString())
        {
            if (Char.IsWhiteSpace(character))
            {
                isSpaced = written.Length > 0;
                continue;
            }

            if (isSpaced && written[written.Length - 1] is not ('(' or '[') && character is not (')' or ']'))
                written.Append(' ');

            written.Append(character);
            isSpaced = false;
        }

        if (written.Length <= MaxWrittenLength)
            return written.ToString();

        var name = read switch
        {
            // The top-level type that holds the member, also where an extension block lies inside it.
            IPropertyReferenceOperation { Property: var property } => $"{AnalyzerSupport.GetContainingTypes(property).Last().Name}.{property.Name}",
            IInvocationOperation { TargetMethod: var method } when AnalyzerSupport.IsFactory(method) => $"{PackageNames.LocalizedTextName}.{method.Name}",
            IInvocationOperation { TargetMethod: var method } => $"{AnalyzerSupport.GetContainingTypes(method).Last().Name}.{method.Name}",
            _ => written.ToString(0, MaxWrittenLength) + "…",
        };

        return name;
    }

    /// <summary>
    /// The first text an expression reads as it's evaluated, or <see langword="null"/>. A lambda or a local function
    /// isn't looked into, unless the lambda runs as the initializer does.
    /// </summary>
    /// <param name="isEnumerated">Whether the initializer enumerates what the expression gives, as <c>ToList()</c> does.</param>
    /// <param name="runsLambdas">Whether a lambda the expression is runs as the initializer does.</param>
    private static IOperation? FindText(IOperation operation, INamedTypeSymbol text, bool isEnumerated, bool runsLambdas)
    {
        // A nameof names a text and reads none.
        if (operation is ILocalFunctionOperation or INameOfOperation || (operation is IAnonymousFunctionOperation && !runsLambdas))
            return null;

        if (IsTextRead(operation, text))
            return operation;

        // A LINQ query runs its lambdas only when it's enumerated, as ToList() or a collection does. A lambda handed to
        // another method may run later or never. Lazy<T> runs its lambda once and keeps the value, as an initializer does.
        var (enumeratesParts, runsLambdasOfParts) = operation switch
        {
            IAnonymousFunctionOperation => (false, false),
            IArgumentOperation or IDelegateCreationOperation or IConversionOperation or IParenthesizedOperation => (isEnumerated, runsLambdas),
            IInvocationOperation { TargetMethod: var method } when IsQuery(method) => (isEnumerated, isEnumerated),
            IInvocationOperation { TargetMethod: var method } when IsOfLinq(method) => (true, true),
            IObjectCreationOperation { Type: INamedTypeSymbol { Name: "Lazy", Arity: 1, ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } } } => (true, true),
            _ => (true, false),
        };

        foreach (var child in operation.ChildOperations)
            if (FindText(child, text, enumeratesParts, runsLambdasOfParts) is { } read)
                return read;

        return null;
    }

    /// <summary>
    /// Whether a method is one of LINQ's for a sequence, such as <c>Select</c> or <c>ToList</c>.
    /// </summary>
    private static bool IsOfLinq(IMethodSymbol method)
    {
        var isOfLinq = method.ContainingType is { Name: "Enumerable", ContainingNamespace: { Name: "Linq", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } } };
        return isOfLinq;
    }

    /// <summary>
    /// Whether a method of LINQ gives a query that runs its lambdas only when it's enumerated, such as <c>Select</c> or
    /// <c>OrderBy</c>. <c>ToLookup</c> gives an <c>ILookup</c>, which it fills at once.
    /// </summary>
    private static bool IsQuery(IMethodSymbol method)
    {
        var isQuery = IsOfLinq(method)
                      && method.ReturnType is INamedTypeSymbol { IsGenericType: true, TypeKind: TypeKind.Interface } returned
                      && returned.ConstructedFrom.ToDisplayString() is "System.Collections.Generic.IEnumerable<T>" or "System.Linq.IOrderedEnumerable<TElement>";

        return isQuery;
    }

    /// <summary>
    /// Whether an operation reads a text itself: a text of a generated class, or a call of a method that gives one
    /// (<see cref="IsTextMethod"/>).
    /// </summary>
    /// <param name="text">The class <c>LocalizedText</c>.</param>
    internal static bool IsTextRead(IOperation operation, INamedTypeSymbol text)
    {
        var isText = operation switch
        {
            IPropertyReferenceOperation { Property: { IsStatic: true } property } => IsTextOfGeneratedClass(property),
            IInvocationOperation { TargetMethod: var method } => IsTextMethod(method, text),
            _ => false,
        };

        return isText;
    }

    /// <summary>
    /// Whether a method gives a text: a method of a generated class, the inline factory, a lookup of a member's text,
    /// or the generated <c>GetText()</c> of an enum.
    /// </summary>
    private static bool IsTextMethod(IMethodSymbol method, INamedTypeSymbol text)
    {
        if (method.ReturnType.SpecialType is not SpecialType.System_String)
            return false;

        var isTextMethod = SymbolEqualityComparer.Default.Equals(method.ContainingType, text)
                           || AnalyzerSupport.IsFactory(method)
                           || IsTextOfGeneratedClass(method)
                           || AnalyzerSupport.IsGetTextOfLocalizedMembers(method);

        return isTextMethod;
    }

    /// <summary>
    /// Whether a class is a generated class, such as <c>ShopLocalization</c>, whose namespace holds its marked contract.
    /// </summary>
    internal static bool IsGeneratedClass(INamedTypeSymbol? type)
    {
        return FindContractOfGeneratedClass(type) is not null;
    }

    /// <summary>
    /// Whether a member of a generated class gives a text: one its contract has, or a lookup the generator writes. A
    /// helper written in the partial class gives none.
    /// </summary>
    internal static bool IsTextOfGeneratedClass(ISymbol member)
    {
        // An extension member's method lies in a block inside the static class that holds it.
        foreach (var type in AnalyzerSupport.GetContainingTypes(member))
        {
            if (FindContractOfGeneratedClass(type) is not { } contract)
                continue;

            var isText = !contract.GetMembers(member.Name).IsEmpty
                         || member.Name is LocalizationSetsAnalysis.GetName or LocalizationSetsAnalysis.LookupName or LocalizationSetsAnalysis.ExtensionName;

            return isText;
        }

        return false;
    }

    /// <summary>
    /// The contract of a generated class, or <see langword="null"/> for any other type.
    /// </summary>
    private static INamedTypeSymbol? FindContractOfGeneratedClass(INamedTypeSymbol? type)
    {
        if (type is not { IsStatic: true, ContainingType: null, Arity: 0 })
            return null;

        foreach (var contract in type.ContainingNamespace.GetTypeMembers($"I{type.Name}", arity: 0))
        {
            if (contract.TypeKind is not TypeKind.Interface)
                continue;

            foreach (var attribute in contract.GetAttributes())
            {
                if (attribute.AttributeClass is { Name: ContractAttributeName } attributeClass
                    && String.Equals(attributeClass.ContainingNamespace.ToDisplayString(), PackageNames.InternalsNamespace, StringComparison.Ordinal))
                {
                    return contract;
                }
            }
        }

        return null;
    }
}
