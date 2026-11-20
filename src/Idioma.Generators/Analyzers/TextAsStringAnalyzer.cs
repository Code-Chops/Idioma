using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Reports a text passed as a <see cref="string"/> value to a text of a set (<c>LOC303</c>):
/// <c>ShopLocalization.PressToPay(ShopLocalization.Checkout)</c>, where <c>PressToPay</c> takes a
/// <c>string button</c>.
/// </summary>
/// <remarks>
/// <para>
/// The text converts to a string at the call, in the language in use at that moment. The text around it is worded
/// when it's shown, so a Dutch user can see "Druk op Checkout om te betalen". A parameter of the type
/// <c>LocalizableText</c> keeps the text, which then shows in the language of the wording around it.
/// </para>
/// <para>
/// Only a call of a method of a generated class is reported: a static method that returns a text, in a class whose
/// contract has a method of that name. Only a text that converts by itself is reported. A call of <c>ToString()</c>
/// on the text says the wording of the moment is meant, and so does an interpolated string that holds it.
/// </para>
/// <para>
/// An inline text isn't reported. Its wording is filled in at once, and where it's shown at once too, that's right.
/// </para>
/// <para>
/// The IDE fix gives the parameter the type <c>LocalizableText</c> in every set (<c>TextAsStringCodeFixProvider</c> in
/// the code fixes).
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TextAsStringAnalyzer : DiagnosticAnalyzer
{
    public const string TextAsStringDiagnosticId = "LOC303";

    private static readonly DiagnosticDescriptor TextAsStringRule = new(
        id: TextAsStringDiagnosticId,
        title: "Text passed as a string to a text with parameters",
        messageFormat: "'{0}.{1}' takes this text as the string '{2}', so the text keeps the wording of the language in use at this call. Make '{2}' a LocalizableText in every set, so the text follows the user's language. To keep the wording on purpose, call ToString() on the text.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A text passed to a string parameter of a text with parameters becomes a string at the call, in the language in use at that moment. The text around it is worded when it's shown, so a user of another language sees the two in different languages. Give the parameter the type LocalizableText in every set. The value then shows in the language of the wording around it.",
        helpLinkUri: PackageNames.HelpLink(TextAsStringDiagnosticId));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [TextAsStringRule];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();

        // Razor's output counts as generated code, and a text shown in markup must be read there.
        // OneLanguageTextAnalyzer.IsHandWritten leaves out the rest.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.RegisterCompilationStartAction(Start);
    }

    private static void Start(CompilationStartAnalysisContext context)
    {
        if (context.Compilation.GetTypeByMetadataName(PackageNames.LocalizableText) is not { } localizableText)
            return;

        context.RegisterOperationAction(c => AnalyzeInvocation(c, localizableText), OperationKind.Invocation);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context, INamedTypeSymbol localizableText)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;

        // By what costs nothing first: nearly every call is another one.
        if (!method.IsStatic
            || method.Parameters.IsEmpty
            || !SymbolEqualityComparer.Default.Equals(method.ReturnType, localizableText)
            || !OneLanguageTextAnalyzer.IsHandWritten(context.IsGeneratedCode, invocation.Syntax.SyntaxTree))
        {
            return;
        }

        bool? isTextOfSet = null;

        foreach (var argument in invocation.Arguments)
        {
            // A text that converts by itself. A conversion the code writes, as ToString() or a cast, is meant.
            if (argument.Parameter is not { Type.SpecialType: SpecialType.System_String } parameter
                || argument.Value is not IConversionOperation { IsImplicit: true, Operand.Type: { } converted }
                || !SymbolEqualityComparer.Default.Equals(converted, localizableText))
            {
                continue;
            }

            if (!(isTextOfSet ??= IsTextOfSet(method)))
                return;

            context.ReportDiagnostic(Diagnostic.Create(TextAsStringRule, argument.Value.Syntax.GetLocation(), method.ContainingType.Name, method.Name, parameter.Name));
        }
    }

    /// <summary>
    /// Whether a method is a text of a generated class, such as <c>ShopLocalization.Greeting</c>: its class lies next
    /// to a contract of its name, <c>IShopLocalization</c>, that has a method of the same name.
    /// </summary>
    private static bool IsTextOfSet(IMethodSymbol method)
    {
        if (method.ContainingType is not { IsStatic: true, ContainingType: null } texts)
            return false;

        foreach (var contract in texts.ContainingNamespace.GetTypeMembers($"I{texts.Name}"))
            if (PackageNames.IsContract(contract) && contract.GetMembers(method.Name).OfType<IMethodSymbol>().Any())
                return true;

        return false;
    }
}
