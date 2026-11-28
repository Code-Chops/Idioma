using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Holds every language of <c>LocalizedText.Inline</c> to the values its default wording shows (<c>LOC402</c>), as
/// <see cref="TextParametersAnalyzer"/> holds a text of a set.
/// </summary>
/// <remarks>
/// <para>
/// The default wording is the first language's argument. It shows the locals, parameters, fields and properties its
/// interpolated strings hold, without those it only chooses by (<see cref="TextParameters"/>). A constant or a text
/// (<see cref="TextProperties"/>) is wording and no value.
/// </para>
/// <para>
/// Another language uses a value when its argument reads it anywhere, directly or through a local's declaration.
/// Nothing is reported where the build can't tell:
/// </para>
/// <list type="bullet">
/// <item>An argument that isn't written in place.</item>
/// <item>An argument that calls a local function or a delegate.</item>
/// <item>For a field, property or primary constructor parameter, an argument that calls code of this assembly.</item>
/// <item>A call that doesn't compile.</item>
/// </list>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed partial class InlineTextValuesAnalyzer : DiagnosticAnalyzer
{
    // Shared with a set's method that leaves a value out, because both have the same fix.
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [SharedRules.ValueLeftOut];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();

        // Razor's output counts as generated code, and a text written in markup must be read there.
        // AnalyzerSupport.IsHandWritten leaves out the rest.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.RegisterCompilationStartAction(Start);
    }

    private static void Start(CompilationStartAnalysisContext context)
    {
        var codes = SupportedLanguages.ReadCodes(context.Compilation.Assembly);

        if (codes.Length < 2)
            return;

        // Found once per method for all its inline texts, because Razor writes a whole page in one method.
        var writes = new TreeWrites();

        var text = context.Compilation.GetTypeByMetadataName(PackageNames.LocalizedText);

        context.RegisterOperationAction(c => AnalyzeInvocation(c, codes, writes, text), OperationKind.Invocation);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context, ImmutableArray<string> codes, TreeWrites writes, INamedTypeSymbol? text)
    {
        var invocation = (IInvocationOperation)context.Operation;

        if (!AnalyzerSupport.IsFactory(invocation.TargetMethod)
            || !AnalyzerSupport.IsHandWritten(context.IsGeneratedCode, invocation.Syntax.SyntaxTree)
            || invocation.Syntax.ContainsDiagnostics)
        {
            return;
        }

        // The first parameter is the default language.
        if (invocation.Arguments.FirstOrDefault(static argument => argument.Parameter is { Ordinal: 0 }) is not { Parameter: { } defaultParameter } defaultArgument)
            return;

        var texts = new TextProperties(context.Compilation, invocation.SemanticModel, text, context.CancellationToken);
        var shown = FindShownValues(defaultArgument, texts);

        if (shown.Count is 0)
            return;

        if (invocation.Descendants().Any(static operation => operation is IInvalidOperation))
            return;

        string? defaultLanguage = null;

        foreach (var argument in invocation.Arguments)
        {
            // Only a wording written in place, because a variable or a call may hold the values unseen.
            if (argument == defaultArgument
                || argument.Parameter is not { } parameter
                || !WrittenWording.FindStrings(argument.Value.Syntax as ExpressionSyntax).Any())
            {
                continue;
            }

            var reads = new Reads(writes);
            reads.Add(argument.Value, context.CancellationToken);

            if (reads.CallsLocalCode || reads.ReadsUnreadableLocal)
                continue;

            foreach (var value in shown)
            {
                if (reads.Symbols.Contains(value) || (reads.CallsOwnCode && IsReadableByOwnCode(value, context.CancellationToken)))
                    continue;

                var language = SupportedLanguages.FindLanguageOfParameter(parameter.Name, codes);
                defaultLanguage ??= SupportedLanguages.FindLanguageOfParameter(defaultParameter.Name, codes);

                context.ReportDiagnostic(Diagnostic.Create(SharedRules.ValueLeftOut, argument.Syntax.GetLocation(), $"The '{language}' wording", $"'{value.Name}'", $"the '{defaultLanguage}' wording", value.Name, $"'{language}'"));
            }
        }
    }

    /// <summary>
    /// Whether code of this assembly can read a value: a field, a property, or a primary constructor parameter.
    /// </summary>
    private static bool IsReadableByOwnCode(ISymbol value, CancellationToken cancellationToken)
    {
        if (value is IFieldSymbol or IPropertySymbol)
            return true;

        if (value is not IParameterSymbol { ContainingSymbol: IMethodSymbol { MethodKind: MethodKind.Constructor } })
            return false;

        // A primary constructor declares its parameters in the list of the type itself.
        foreach (var reference in value.DeclaringSyntaxReferences)
            if (reference.GetSyntax(cancellationToken) is ParameterSyntax { Parent.Parent: TypeDeclarationSyntax })
                return true;

        return false;
    }

    /// <summary>
    /// The values an argument's interpolated strings show, in order, without those they only choose by.
    /// </summary>
    private static List<ISymbol> FindShownValues(IArgumentOperation argument, TextProperties texts)
    {
        var shown = new List<ISymbol>();

        foreach (var interpolated in FindWrittenInterpolatedStrings(argument.Value))
            foreach (var part in interpolated.Parts)
                if (part is IInterpolationOperation value)
                    AddShownValues(value.Expression, argument, shown, texts);

        return shown;
    }

    /// <summary>
    /// The interpolated strings an operation shows, where <see cref="WrittenWording"/> finds a wording's strings.
    /// </summary>
    private static IEnumerable<IInterpolatedStringOperation> FindWrittenInterpolatedStrings(IOperation? operation)
    {
        if (operation is IInterpolatedStringOperation interpolated)
        {
            yield return interpolated;
            yield break;
        }

        IOperation?[] parts = operation switch
        {
            IBinaryOperation { OperatorKind: BinaryOperatorKind.Add } addition => [addition.LeftOperand, addition.RightOperand],
            ICoalesceOperation coalesce => [coalesce.Value, coalesce.WhenNull],
            IConditionalOperation conditional => [conditional.WhenTrue, conditional.WhenFalse],
            ISwitchExpressionOperation @switch => [.. @switch.Arms.Select(static arm => arm.Value)],
            IConversionOperation conversion => [conversion.Operand],
            IParenthesizedOperation parenthesized => [parenthesized.Operand],
            _ => [],
        };

        foreach (var part in parts)
            foreach (var found in FindWrittenInterpolatedStrings(part))
                yield return found;
    }

    /// <summary>
    /// Adds the values an interpolation shows. A value in a condition, switch, pattern or comparison only chooses, so
    /// it's left out, as in <see cref="TextParameters"/>.
    /// </summary>
    private static void AddShownValues(IOperation? operation, IArgumentOperation argument, List<ISymbol> shown, TextProperties texts)
    {
        switch (operation)
        {
            case null or IIsPatternOperation:
            case { ConstantValue.HasValue: true }:
                return;

            case IConditionalOperation conditional:
                AddShownValues(conditional.WhenTrue, argument, shown, texts);
                AddShownValues(conditional.WhenFalse, argument, shown, texts);
                return;

            case ISwitchExpressionOperation @switch:
                foreach (var arm in @switch.Arms)
                    AddShownValues(arm.Value, argument, shown, texts);

                return;

            case IBinaryOperation
            {
                OperatorKind: BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals
                    or BinaryOperatorKind.LessThan or BinaryOperatorKind.LessThanOrEqual
                    or BinaryOperatorKind.GreaterThan or BinaryOperatorKind.GreaterThanOrEqual,
            }:
                return;
        }

        if (FindValue(operation) is { } value)
        {
            // A text is wording, so another language may word it its own way.
            if (IsValueOfCaller(value, argument) && !texts.IsText(value) && !shown.Contains(value, SymbolEqualityComparer.Default))
                shown.Add(value);

            return;
        }

        foreach (var child in operation.ChildOperations)
            AddShownValues(child, argument, shown, texts);
    }

    /// <summary>
    /// The local, parameter, or field or property of this instance or a type that an operation reads. For
    /// <c>user.Name</c>, the value is <c>user</c>.
    /// </summary>
    private static ISymbol? FindValue(IOperation operation)
    {
        ISymbol? value = operation switch
        {
            ILocalReferenceOperation local => local.Local,
            IParameterReferenceOperation parameter => parameter.Parameter,
            IFieldReferenceOperation { Instance: null or IInstanceReferenceOperation } field => field.Field,
            IPropertyReferenceOperation { Instance: null or IInstanceReferenceOperation, Arguments.IsEmpty: true } property => property.Property,
            _ => null,
        };

        return value;
    }

    /// <summary>
    /// Whether a value is declared in this assembly and outside the argument, as a lambda's parameter isn't.
    /// </summary>
    private static bool IsValueOfCaller(ISymbol value, IArgumentOperation argument)
    {
        if (value.IsImplicitlyDeclared || value.DeclaringSyntaxReferences.IsEmpty)
            return false;

        foreach (var reference in value.DeclaringSyntaxReferences)
            if (reference.SyntaxTree == argument.Syntax.SyntaxTree && argument.Syntax.Span.Contains(reference.Span))
                return false;

        return true;
    }

    /// <summary>
    /// What the wording of a language reads, and whether it calls code that may read more.
    /// </summary>
    private sealed class Reads(TreeWrites treeWrites)
    {
        /// <summary>
        /// The symbols read, also through the declarations and assignments of the locals read.
        /// </summary>
        public HashSet<ISymbol> Symbols { get; } = new(SymbolEqualityComparer.Default);

        /// <summary>
        /// Whether a local function or a delegate is called, which may read any value around the call.
        /// </summary>
        public bool CallsLocalCode { get; private set; }

        /// <summary>
        /// Whether a method, constructor or property with a body of this assembly is called.
        /// </summary>
        public bool CallsOwnCode { get; private set; }

        /// <summary>
        /// Whether a local is read whose value the build can't follow, such as one from a pattern, <c>out var</c>,
        /// <see langword="foreach"/> or a <see langword="ref"/> argument.
        /// </summary>
        public bool ReadsUnreadableLocal { get; private set; }

        /// <summary>
        /// Found when the wording first reads a local.
        /// </summary>
        private Writes? Writes { get; set; }

        public void Add(IOperation operation, CancellationToken cancellationToken)
        {
            foreach (var read in operation.DescendantsAndSelf())
            {
                switch (read)
                {
                    case ILocalReferenceOperation { Local: var local }:
                        if (this.Symbols.Add(local))
                        {
                            this.AddDeclaration(local, read.SemanticModel, cancellationToken);
                            this.AddWrites(local, read, cancellationToken);
                        }

                        break;

                    case IParameterReferenceOperation { Parameter: var parameter }:
                        this.Symbols.Add(parameter);
                        break;

                    case IFieldReferenceOperation { Field: var field }:
                        this.Symbols.Add(field);
                        break;

                    case IPropertyReferenceOperation { Property: var property }:
                        this.Symbols.Add(property);
                        this.CallsOwnCode |= HasBody(property, cancellationToken);
                        break;

                    case IInvocationOperation { TargetMethod: var method }:
                        this.CallsLocalCode |= method.MethodKind is MethodKind.LocalFunction or MethodKind.DelegateInvoke;
                        this.CallsOwnCode |= !method.DeclaringSyntaxReferences.IsEmpty;
                        break;

                    case IObjectCreationOperation { Constructor: { } constructor }:
                        this.CallsOwnCode |= !constructor.ContainingType.DeclaringSyntaxReferences.IsEmpty;
                        break;

                    // What is bound when the app runs may read anything.
                    case IDynamicInvocationOperation or IDynamicMemberReferenceOperation or IDynamicIndexerAccessOperation or IDynamicObjectCreationOperation:
                        this.CallsLocalCode = true;
                        break;
                }
            }
        }

        /// <summary>
        /// Adds what a local's declarator reads. A local declared another way sets <see cref="ReadsUnreadableLocal"/>.
        /// </summary>
        private void AddDeclaration(ILocalSymbol local, SemanticModel? semanticModel, CancellationToken cancellationToken)
        {
            if (semanticModel is null)
                return;

            foreach (var reference in local.DeclaringSyntaxReferences)
            {
                if (reference.GetSyntax(cancellationToken) is not VariableDeclaratorSyntax declarator)
                {
                    this.ReadsUnreadableLocal = true;
                    continue;
                }

                if (reference.SyntaxTree == semanticModel.SyntaxTree
                    && declarator.Initializer?.Value is { } initializer
                    && semanticModel.GetOperation(initializer, cancellationToken) is { } operation)
                {
                    this.Add(operation, cancellationToken);
                }
            }
        }

        /// <summary>
        /// Adds what a local is assigned after its declaration. Another kind of write, such as a <c>ref</c> argument,
        /// sets <see cref="ReadsUnreadableLocal"/>.
        /// </summary>
        private void AddWrites(ILocalSymbol local, IOperation read, CancellationToken cancellationToken)
        {
            var writes = this.Writes ??= treeWrites.Find(read, cancellationToken);

            foreach (var value in writes.AssignedTo(local))
                this.Add(value, cancellationToken);

            this.ReadsUnreadableLocal |= writes.IsWrittenUnreadably(local);
        }

        /// <summary>
        /// Whether a property of this assembly isn't an auto-property.
        /// </summary>
        private static bool HasBody(IPropertySymbol property, CancellationToken cancellationToken)
        {
            foreach (var reference in property.DeclaringSyntaxReferences)
            {
                if (reference.GetSyntax(cancellationToken) is not PropertyDeclarationSyntax declaration)
                    return true;

                if (declaration.ExpressionBody is not null
                    || declaration.AccessorList?.Accessors.Any(static accessor => accessor.Body is not null || accessor.ExpressionBody is not null) is true)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
