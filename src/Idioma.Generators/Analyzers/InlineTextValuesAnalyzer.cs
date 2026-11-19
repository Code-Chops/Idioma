using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Holds every language of <c>LocalizableText.Inline</c> to the values its default wording shows (<c>LOC302</c>), as
/// <see cref="TextParametersAnalyzer"/> holds a text of a set.
/// </summary>
/// <remarks>
/// <para>
/// Every parameter of <c>Inline</c> is a string, so the compiler doesn't mind a wording that leaves a value out:
/// <c>LocalizableText.Inline(en: $"Hello, {name}!", nl: "Hallo!")</c> compiles, and Dutch users don't see the name.
/// </para>
/// <para>
/// The default wording is the argument of the first language. It shows a value where an interpolated string it writes
/// holds one: a local variable, a parameter, or a field or property of this assembly. <c>{count:N0}</c> and
/// <c>{count.ToString("N0")}</c> show <c>count</c>, and <c>{user.Name}</c> shows <c>user</c>. A value it only chooses
/// by, as in <c>count == 1 ? "1 item" : $"{count} items"</c> without the last part, isn't shown, as for a text of a
/// set (<see cref="TextParameters"/>). A constant is wording and no value.
/// </para>
/// <para>
/// Another language uses a value when its argument reads it anywhere, also in a condition, or reads a local variable
/// that is declared with it. Nothing is reported where the build can't tell:
/// </para>
/// <list type="bullet">
/// <item>An argument that isn't written in place, such as a variable or the result of a call.</item>
/// <item>An argument that calls a local function or a delegate, which may read the value.</item>
/// <item>For a field or property, an argument that calls a method of this assembly, or reads a property of it that has
/// a body.</item>
/// <item>A call that doesn't compile.</item>
/// </list>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InlineTextValuesAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The code a set that leaves a value out gets too (<see cref="TextParametersAnalyzer"/>).
    /// </summary>
    public const string ValueLeftOutDiagnosticId = TextParametersAnalyzer.ValueLeftOutDiagnosticId;

    // The code of a set's method that leaves a value out: both say that a language doesn't show a value the default
    // wording shows, and both have the same fix. So the code is shared on purpose.
#pragma warning disable RS1019 // Diagnostic Id is already used by another analyzer
    private static readonly DiagnosticDescriptor ValueLeftOutRule = new(
        id: ValueLeftOutDiagnosticId,
        title: "Value of an inline text left out of a wording",
        messageFormat: "The '{0}' wording doesn't use '{1}', but the '{2}' wording shows it. Use '{1}' in every language, or users of '{0}' won't see it.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every parameter of LocalizableText.Inline is a string, so wording that leaves a value out compiles. Every language must use a value that the wording of the default language shows in an interpolated string. A value the default wording only uses to choose between wordings may go unused.",
        helpLinkUri: PackageNames.HelpLink(ValueLeftOutDiagnosticId));
#pragma warning restore RS1019

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [ValueLeftOutRule];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();

        // Razor's output counts as generated code, and a text written in markup must be read there.
        // OneLanguageTextAnalyzer.IsHandWritten leaves out the rest.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.RegisterCompilationStartAction(Start);
    }

    private static void Start(CompilationStartAnalysisContext context)
    {
        // Read once per compilation, and not per call: an app may write hundreds of inline texts.
        var codes = SupportedLanguages.ReadCodes(context.Compilation.Assembly);

        // One language has no other to hold to it.
        if (codes.Length < 2)
            return;

        context.RegisterOperationAction(c => AnalyzeInvocation(c, codes), OperationKind.Invocation);
    }

    /// <summary>
    /// Holds each language of a call of <c>LocalizableText.Inline</c> to the values the first language shows.
    /// </summary>
    /// <param name="context">The invocation.</param>
    /// <param name="codes">The languages the assembly declares, which name the wording of each parameter.</param>
    private static void AnalyzeInvocation(OperationAnalysisContext context, ImmutableArray<string> codes)
    {
        var invocation = (IInvocationOperation)context.Operation;

        // By the name of the method first, which costs nothing: nearly every call is another one.
        if (!OneLanguageTextAnalyzer.IsFactory(invocation.TargetMethod)
            || !OneLanguageTextAnalyzer.IsHandWritten(context.IsGeneratedCode, invocation.Syntax.SyntaxTree)
            || invocation.Syntax.ContainsDiagnostics)
        {
            return;
        }

        // The factory takes the languages in the order the assembly declares them, the default first.
        if (invocation.Arguments.FirstOrDefault(static argument => argument.Parameter is { Ordinal: 0 }) is not { Parameter: { } defaultParameter } defaultArgument)
            return;

        var shown = FindShownValues(defaultArgument);

        if (shown.Count is 0)
            return;

        // Nothing to say about values in a call of which a part binds to nothing.
        if (invocation.Descendants().Any(static operation => operation is IInvalidOperation))
            return;

        string? defaultLanguage = null;

        foreach (var argument in invocation.Arguments)
        {
            // Only a wording written in place is held to the values: a variable or a call may hold them unseen.
            if (argument == defaultArgument
                || argument.Parameter is not { } parameter
                || !WrittenWording.FindStrings(argument.Value.Syntax as ExpressionSyntax).Any())
            {
                continue;
            }

            var reads = new Reads();
            reads.Add(argument.Value, context.CancellationToken);

            if (reads.CallsLocalCode)
                continue;

            foreach (var value in shown)
            {
                if (reads.Symbols.Contains(value) || (value is IFieldSymbol or IPropertySymbol && reads.CallsOwnCode))
                    continue;

                var language = SupportedLanguages.FindLanguageOfParameter(parameter.Name, codes);
                defaultLanguage ??= SupportedLanguages.FindLanguageOfParameter(defaultParameter.Name, codes);

                context.ReportDiagnostic(Diagnostic.Create(ValueLeftOutRule, argument.Syntax.GetLocation(), language, value.Name, defaultLanguage));
            }
        }
    }

    /// <summary>
    /// The values the wording of an argument shows, in the order it shows them: what the interpolated strings it writes
    /// hold, without what they only choose by.
    /// </summary>
    private static List<ISymbol> FindShownValues(IArgumentOperation argument)
    {
        var shown = new List<ISymbol>();

        foreach (var interpolated in FindWrittenInterpolatedStrings(argument.Value))
            foreach (var part in interpolated.Parts)
                if (part is IInterpolationOperation value)
                    AddShownValues(value.Expression, argument, shown);

        return shown;
    }

    /// <summary>
    /// The interpolated strings an operation shows one of, or shows next to each other: where
    /// <see cref="WrittenWording"/> finds the strings of a wording.
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
    /// Adds the values an operation shows, when it stands in an interpolated string. A value that only chooses what is
    /// shown is left out, as <see cref="TextParameters"/> leaves it out for a text of a set: one in a condition, in
    /// what a switch switches on, in a pattern, or in a comparison.
    /// </summary>
    private static void AddShownValues(IOperation? operation, IArgumentOperation argument, List<ISymbol> shown)
    {
        switch (operation)
        {
            case null or IIsPatternOperation:
            case { ConstantValue.HasValue: true }:
                return;

            case IConditionalOperation conditional:
                AddShownValues(conditional.WhenTrue, argument, shown);
                AddShownValues(conditional.WhenFalse, argument, shown);
                return;

            case ISwitchExpressionOperation @switch:
                foreach (var arm in @switch.Arms)
                    AddShownValues(arm.Value, argument, shown);

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
            if (IsValueOfCaller(value, argument) && !shown.Contains(value, SymbolEqualityComparer.Default))
                shown.Add(value);

            return;
        }

        foreach (var child in operation.ChildOperations)
            AddShownValues(child, argument, shown);
    }

    /// <summary>
    /// The value an operation reads by itself: a local variable, a parameter, or a field or property that is read of
    /// this instance or of a type. A member of another value, such as <c>user.Name</c>, is no value by itself: the
    /// value is <c>user</c>.
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
    /// Whether a value is one the code around the call holds: declared in this assembly, and not inside the argument
    /// that shows it, as the parameter of a lambda is. Another language can't be held to a value of the argument's own,
    /// and a member of another assembly, such as <c>DateTime.Now</c>, is no value of the text.
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
    private sealed class Reads
    {
        /// <summary>
        /// The local variables, parameters, fields, and properties read, also those read where a local variable that is
        /// read is declared: <c>var fullName = $"{first} {last}";</c> reads both names for a wording that shows
        /// <c>fullName</c>.
        /// </summary>
        public HashSet<ISymbol> Symbols { get; } = new(SymbolEqualityComparer.Default);

        /// <summary>
        /// Whether a local function or a delegate is called, which may read any value of the code around the call.
        /// </summary>
        public bool CallsLocalCode { get; private set; }

        /// <summary>
        /// Whether code of this assembly is called, which may read a field or property: a method, a constructor, or a
        /// property with a body.
        /// </summary>
        public bool CallsOwnCode { get; private set; }

        public void Add(IOperation operation, CancellationToken cancellationToken)
        {
            foreach (var read in operation.DescendantsAndSelf())
            {
                switch (read)
                {
                    case ILocalReferenceOperation { Local: var local }:
                        if (Symbols.Add(local))
                            AddDeclaration(local, read.SemanticModel, cancellationToken);

                        break;

                    case IParameterReferenceOperation { Parameter: var parameter }:
                        Symbols.Add(parameter);
                        break;

                    case IFieldReferenceOperation { Field: var field }:
                        Symbols.Add(field);
                        break;

                    case IPropertyReferenceOperation { Property: var property }:
                        Symbols.Add(property);
                        CallsOwnCode |= HasBody(property, cancellationToken);
                        break;

                    case IInvocationOperation { TargetMethod: var method }:
                        CallsLocalCode |= method.MethodKind is MethodKind.LocalFunction or MethodKind.DelegateInvoke;
                        CallsOwnCode |= !method.DeclaringSyntaxReferences.IsEmpty;
                        break;

                    case IObjectCreationOperation { Constructor: { } constructor }:
                        CallsOwnCode |= !constructor.ContainingType.DeclaringSyntaxReferences.IsEmpty;
                        break;

                    // What is bound when the app runs may read anything.
                    case IDynamicInvocationOperation or IDynamicMemberReferenceOperation or IDynamicIndexerAccessOperation or IDynamicObjectCreationOperation:
                        CallsLocalCode = true;
                        break;
                }
            }
        }

        /// <summary>
        /// Adds what the declaration of a local variable reads, as in <c>var name = user.Name;</c>.
        /// </summary>
        private void AddDeclaration(ILocalSymbol local, SemanticModel? semanticModel, CancellationToken cancellationToken)
        {
            if (semanticModel is null)
                return;

            foreach (var reference in local.DeclaringSyntaxReferences)
            {
                if (reference.SyntaxTree == semanticModel.SyntaxTree
                    && reference.GetSyntax(cancellationToken) is VariableDeclaratorSyntax { Initializer.Value: { } initializer }
                    && semanticModel.GetOperation(initializer, cancellationToken) is { } operation)
                {
                    Add(operation, cancellationToken);
                }
            }
        }

        /// <summary>
        /// Whether a property of this assembly has code that may read another member, and so isn't an auto-property.
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
