using System.Text.RegularExpressions;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Writes <c>LocalizableText.Inline</c> for the languages of <c>[assembly: SupportedLanguages(...)]</c>, one required
/// parameter per language, so that leaving a language out is a compile error. It is a static extension on the one shared
/// <c>LocalizableText</c>: a type of its own per assembly could not cross into a library's API. It reports <c>LOC010</c>
/// for an invalid or repeated language, and still writes the factory for the valid ones, so that error stays the only
/// one wherever no call names the language that is wrong.
/// </summary>
/// <remarks>
/// <para>
/// The class that holds the extension lies in the global namespace. An extension is found only where its namespace is
/// in reach, and the global namespace is in reach of every file, so <c>LocalizableText.Inline</c> binds however the
/// call names the type: by its name alone, or by its full name in a file without a using for its namespace.
/// </para>
/// <para>
/// The extension is on <c>LocalizableText</c> alone. Written for that type as such, it would show on
/// <c>ResourceText</c> too, which derives from it: <c>ResourceText.Inline</c> would give a <c>LocalizableText</c>
/// under the name of another type. So it is written for the text type that is its own <c>IInlineText</c>, which
/// <c>LocalizableText</c> is and <c>ResourceText</c> is not.
/// </para>
/// <para>
/// The class is marked <c>[Embedded]</c> because a library and a test project with its <c>InternalsVisibleTo</c> both
/// generate one, and two visible factories with the same languages would make every call ambiguous.
/// </para>
/// <para>
/// An assembly that calls the factory, in code or in Razor markup, is marked <c>[assembly: InlineTexts]</c>: no assembly
/// referencing it can add a language to such a text, so its build warns about the languages it lacks (<c>LOC009</c>).
/// </para>
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class LocalizableTextFactoryGenerator : IIncrementalGenerator
{
    public const string InvalidLanguagesDiagnosticId = "LOC010";

    internal const string ClassName = "GeneratedLocalizableText";
    internal const string FactoryName = "Inline";

    private const string TextName = PackageNames.LocalizableText;
    private const string InlineTextName = PackageNames.InlineText;

    /// <summary>
    /// A call of the factory in Razor markup, which other generators cannot see as C#: Razor's own generator writes it.
    /// </summary>
    private static readonly Regex InlineTextInMarkup = new(@"\bLocalizableText\s*\.\s*Inline\s*\(", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly DiagnosticDescriptor InvalidLanguagesRule = new(
        id: InvalidLanguagesDiagnosticId,
        title: "Invalid language in [assembly: SupportedLanguages]",
        messageFormat: "[assembly: SupportedLanguages] lists an invalid language: {0}. List each language once, as a culture name such as en or en-GB.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A language becomes a parameter of LocalizableText.Inline and is matched against the UI culture at run time, so each must be a culture name, and appear once.");

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static context => context.AddEmbeddedAttributeDefinition());

        var languages = SupportedLanguages.Read(context);

        // Two outputs: the declaration holds its location, which every edit in the file of the attribute renews, and
        // the factory is written from the languages alone, so such an edit does not write it again.
        context.RegisterSourceOutput(languages, static (context, languages) =>
        {
            if (languages is not { } declared)
                return;

            foreach (var problem in declared.Problems)
                context.ReportDiagnostic(Diagnostic.Create(InvalidLanguagesRule, declared.Location.ToLocation(), problem));
        });

        context.RegisterSourceOutput(languages.Select(static (languages, _) => languages?.Codes ?? default), static (context, codes) =>
        {
            if (codes.Length > 0)
                context.AddSource("LocalizableText.Factory.g.cs", GenerateSource(codes));
        });

        var callsInCode = context.SyntaxProvider
            .CreateSyntaxProvider(static (node, _) => IsInlineCall(node), static (context, cancellationToken) => IsFactoryCall(context, cancellationToken))
            .Where(static isFactoryCall => isFactoryCall)
            .Collect()
            .Select(static (calls, _) => calls.Length > 0);

        var callsInMarkup = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase) || file.Path.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase))
            .Select(static (file, cancellationToken) => file.GetText(cancellationToken) is { } text && InlineTextInMarkup.IsMatch(text.ToString()))
            .Collect()
            .Select(static (calls, _) => calls.Contains(true));

        var writesInlineTexts = languages
            .Combine(callsInCode)
            .Combine(callsInMarkup)
            .Select(static (data, _) => data.Left.Left is { Codes.Length: > 0 } && (data.Left.Right || data.Right));

        context.RegisterSourceOutput(writesInlineTexts, static (context, writesInlineTexts) =>
        {
            if (writesInlineTexts)
            {
                context.AddSource("InlineTexts.g.cs", $"""
                    // <auto-generated/>
                    [assembly: {PackageNames.InlineTextsAttribute}]

                    """);
            }
        });
    }

    /// <summary>
    /// Whether a node calls a member named like the factory on something: <c>Something.Inline(...)</c>. All the syntax
    /// can tell, as the factory does not exist in the compilation a generator sees.
    /// </summary>
    private static bool IsInlineCall(SyntaxNode node)
    {
        var isInlineCall = node is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access }
               && String.Equals(access.Name.Identifier.ValueText, FactoryName, StringComparison.Ordinal);

        return isInlineCall;
    }

    /// <summary>
    /// Whether a call of <c>Something.Inline(...)</c> is a call of the factory: <c>LocalizableText.Inline(...)</c>, also
    /// qualified, or through an alias of the text type (<c>using Text = CodeChops.Idioma.LocalizableText;</c>). The
    /// factory itself does not exist in the compilation a generator sees, but the receiver does, so its name decides
    /// first and only another name is bound.
    /// </summary>
    private static bool IsFactoryCall(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        var receiver = ((MemberAccessExpressionSyntax)((InvocationExpressionSyntax)context.Node).Expression).Expression;

        var name = receiver switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
            _ => null,
        };

        if (String.Equals(name, "LocalizableText", StringComparison.Ordinal))
            return true;

        // A name that is none of those can still be the text type under an alias. Only a plain name can be an alias.
        var isFactoryCall = receiver is IdentifierNameSyntax
               && context.SemanticModel.GetSymbolInfo(receiver, cancellationToken).Symbol is INamedTypeSymbol type
               && String.Equals(type.ToDisplayString(), PackageNames.LocalizableText, StringComparison.Ordinal);

        return isFactoryCall;
    }

    private static string GenerateSource(EquatableArray<string> codes)
    {
        var defaultParameter = SupportedLanguages.ToParameterName(codes[0]);
        var parameters = String.Join(", ", codes.Select(static code => $"string {SupportedLanguages.ToParameterName(code)}"));
        // The default language too, under its own code: listed with a language that serves it (en-GB with en), it would
        // otherwise resolve through that language's entry before reaching the default text.
        var translations = String.Concat(codes.Select(static code => $", (\"{code}\", {SupportedLanguages.ToParameterName(code)})"));
        var sample = String.Join(", ", codes.Select(static code => $"{SupportedLanguages.ToParameterName(code)}: \"...\""));

        var sb = new StringBuilder();
        sb.AppendLine($$"""
            // <auto-generated/>
            #nullable enable

            // In the global namespace, which every file reaches: LocalizableText.Inline then binds however a call names
            // the type, also by its full name in a file without a using for its namespace.

            /// <summary>
            /// Creates texts in every language of this assembly's <see cref="{{PackageNames.SupportedLanguagesAttribute}}"/>.
            /// </summary>
            [Microsoft.CodeAnalysis.EmbeddedAttribute]
            internal static class {{ClassName}}
            {
                // For LocalizableText alone: ResourceText derives from it, but is not its own IInlineText.
                extension<TText>(TText)
                    where TText : {{TextName}}, {{InlineTextName}}<TText>
                {
                    /// <summary>
                    /// A text in every language this assembly supports: <c>LocalizableText.Inline({{sample}})</c>.
                    /// It shows the wording for the current UI culture, and a culture none of them serves gets the wording in the app's
                    /// default language, else the <c>{{codes[0]}}</c> wording.
                    /// </summary>
                    /// <remarks>
                    /// Name each argument after its language: an unnamed one is a build error (<c>LOC013</c>), as two swapped languages
                    /// would go unnoticed. For a value inside the wording, write a placeholder such as <c>{0}</c> in every language and
                    /// fill it in with <see cref="{{TextName}}.Format(System.ReadOnlySpan{object})"/>.
                    /// </remarks>
            """);

        foreach (var code in codes)
        {
            var name = SupportedLanguages.ToParameterName(code).TrimStart('@');
            sb.AppendLine($"        /// <param name=\"{name}\">The wording in <c>{code}</c>.</param>");
        }

        sb.AppendLine($$"""
                    public static {{TextName}} Inline({{parameters}})
                    {
                        return {{PackageNames.GeneratedCode}}.CreateText({{defaultParameter}}{{translations}});
                    }
                }
            }
            #nullable restore
            """);

        return sb.ToString();
    }
}
