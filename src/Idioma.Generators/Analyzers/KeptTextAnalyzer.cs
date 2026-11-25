using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Reports a text that is read once and kept for the whole app: one in the initializer of a static field or a static
/// property (<c>LOC602</c>), as in <c>static readonly string Label = ShopLocalization.Checkout;</c>.
/// </summary>
/// <remarks>
/// <para>
/// A text is a string with the wording for the user's language at the moment it's read. A static initializer runs
/// once, for whoever uses the class first, so every user after that gets the wording of that first user's language.
/// The code compiles and reads well, and the mistake only shows with two users of different languages.
/// </para>
/// <para>
/// A text is one of these:
/// </para>
/// <list type="bullet">
/// <item>A member of a generated class, such as <c>ShopLocalization.Checkout</c> or
/// <c>ShopLocalization.GetTextByKey("Checkout")</c>.</item>
/// <item>A call of <c>LocalizedText.Inline</c>, <c>LocalizedText.GetTextByMember</c> or
/// <c>LocalizedText.GetTextOfType</c>.</item>
/// <item>A call of the generated <c>GetText()</c> on a value of an enum.</item>
/// </list>
/// <para>
/// A text inside a lambda or a local function isn't reported, because it's read when that runs:
/// <c>static readonly Func&lt;string&gt; Label = () => ShopLocalization.Checkout;</c>. Neither is a text an instance
/// field keeps, which lives as long as its object. Generated code is left alone, except what Razor generates from the
/// user's markup.
/// </para>
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

        // Razor's output counts as generated code, and a field in the @code of a component is the user's own.
        // UnnamedLanguageAnalyzer.IsHandWritten leaves out the rest.
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

        // The first of the members it initializes names them all: 'static string A = ..., B = ...' has an initializer each.
        ISymbol? member = initializer switch
        {
            IFieldInitializerOperation { InitializedFields: [var field, ..] } => field,
            IPropertyInitializerOperation { InitializedProperties: [var property, ..] } => property,
            _ => null,
        };

        // A constant can't hold a text, because a text is no constant.
        if (member is not { IsStatic: true }
            || member is IFieldSymbol { IsConst: true }
            || !UnnamedLanguageAnalyzer.IsHandWritten(context.IsGeneratedCode, initializer.Syntax.SyntaxTree))
        {
            return;
        }

        if (FindText(initializer.Value, text, isEnumerated: false, runsLambdas: false) is { } read)
            context.ReportDiagnostic(Diagnostic.Create(KeptTextRule, read.Syntax.GetLocation(), read.Syntax.ToString(), member.Name));
    }

    /// <summary>
    /// The first text an expression reads as it's evaluated, or <see langword="null"/>. What a lambda or a local
    /// function reads is read when that runs, so it isn't looked into, unless the lambda runs as the initializer does.
    /// </summary>
    /// <param name="operation">The expression, or a part of it.</param>
    /// <param name="text">The class <c>LocalizedText</c>.</param>
    /// <param name="isEnumerated">
    /// Whether the initializer enumerates what the expression gives, as <c>ToList()</c> or <c>[.. query]</c> does. A
    /// query of LINQ that it stores as it is runs its lambdas only when it's enumerated, later.
    /// </param>
    /// <param name="runsLambdas">Whether a lambda the expression is runs as the initializer does.</param>
    private static IOperation? FindText(IOperation operation, INamedTypeSymbol text, bool isEnumerated, bool runsLambdas)
    {
        // A nameof names a text and reads none.
        if (operation is ILocalFunctionOperation or INameOfOperation || (operation is IAnonymousFunctionOperation && !runsLambdas))
            return null;

        var isText = operation switch
        {
            IPropertyReferenceOperation { Property: { IsStatic: true } property } => IsTextOfGeneratedClass(property),
            IInvocationOperation { TargetMethod: var method } => IsTextMethod(method, text),
            _ => false,
        };

        if (isText)
            return operation;

        // How the parts of the expression run: what a part of a query of LINQ gives is enumerated when the query is,
        // and its lambdas run then. A method that takes a query, such as ToList() or string.Join, enumerates it, and so
        // does a collection, but a lambda handed to another method may run later or never. Lazy<T> runs its lambda once
        // and keeps the value, as an initializer does. A lambda's body runs anew, as code of its own.
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
    /// Whether a method of LINQ gives a query, which runs its lambdas only when it's enumerated, such as <c>Select</c>.
    /// One that gives a list, a value or a dictionary, such as <c>ToList</c> or <c>First</c>, runs them as it's called.
    /// </summary>
    private static bool IsQuery(IMethodSymbol method)
    {
        var isQuery = IsOfLinq(method)
                      && method.ReturnType is INamedTypeSymbol { IsGenericType: true, TypeKind: TypeKind.Interface } returned
                      && returned.ConstructedFrom.ContainingNamespace?.ToDisplayString() is "System.Collections.Generic" or "System.Linq";

        return isQuery;
    }

    /// <summary>
    /// Whether a method gives a text: a method of a generated class, the inline factory, a lookup of a member's text,
    /// or the generated <c>GetText()</c> of an enum.
    /// </summary>
    private static bool IsTextMethod(IMethodSymbol method, INamedTypeSymbol text)
    {
        if (method.ReturnType.SpecialType is not SpecialType.System_String)
            return false;

        if (SymbolEqualityComparer.Default.Equals(method.ContainingType, text) || UnnamedLanguageAnalyzer.IsFactory(method))
            return true;

        if (IsTextOfGeneratedClass(method))
            return true;

        // An extension member's method lies in a block inside the static class that holds it.
        for (var type = method.ContainingType; type is not null; type = type.ContainingType)
        {

            // The class the source generator writes for an enum with [Localized] members.
            if (String.Equals(method.Name, LocalizedMembers.ExtensionName, StringComparison.Ordinal)
                && type.Name.EndsWith(LocalizedMembers.ExtensionHolderSuffix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether a class is one the source generator writes for a set, such as <c>ShopLocalization</c>: a static class
    /// whose namespace holds its contract, <c>IShopLocalization</c>, which the generator marks.
    /// </summary>
    internal static bool IsGeneratedClass(INamedTypeSymbol? type)
    {
        return FindContractOfGeneratedClass(type) is not null;
    }

    /// <summary>
    /// Whether a member of a generated class gives a text: one of its texts, which its contract has too, or a method
    /// the generator writes next to them (<c>GetTextByKey</c>, and <c>GetTextByValue</c> and <c>GetText()</c> for an
    /// enum). A helper written next to them in the partial class gives none.
    /// </summary>
    internal static bool IsTextOfGeneratedClass(ISymbol member)
    {
        // An extension member's method lies in a block inside the static class that holds it.
        for (var type = member.ContainingType; type is not null; type = type.ContainingType)
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
    /// The contract of a class the source generator writes for a set, or <see langword="null"/> for any other type.
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
