using System.Text.RegularExpressions;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Writes <c>LocalizedText.Inline</c> and <c>[Localized]</c> with one required parameter per language, so leaving a
/// language out is a compile error. It reports <c>LOC102</c> and <c>LOC103</c>, and still writes the factory for the
/// usable languages.
/// </summary>
/// <remarks>
/// <para>
/// The extension's class lies in the global namespace, so <c>LocalizedText.Inline</c> binds however a call names the
/// type. It's <c>[Embedded]</c>, because a library and its test project with <c>InternalsVisibleTo</c> both generate one.
/// </para>
/// <para>
/// The attribute lies in <c>CodeChops.Idioma</c>, so another library's <c>LocalizedAttribute</c> is ambiguous
/// (<c>CS0104</c>) rather than silently hidden. It's <c>[Conditional]</c>, so its wording isn't stored twice.
/// </para>
/// <para>
/// An assembly that calls the factory, in code or in Razor markup, is marked <c>[assembly: StandaloneTexts]</c>
/// (<c>LOC701</c>).
/// </para>
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class LocalizedTextFactoryGenerator : IIncrementalGenerator
{
    public const string InvalidLanguagesDiagnosticId = "LOC102";
    public const string LanguageVersionTooOldDiagnosticId = "LOC104";
    public const string UnknownLanguageCodeDiagnosticId = "LOC103";

    internal const string ClassName = "GeneratedLocalizedText";
    internal const string FactoryName = "Inline";

    /// <summary>
    /// The field with the assembly's languages, the default first, shared with <see cref="LocalizedMembersSource"/>.
    /// </summary>
    internal const string LanguagesName = "Languages";

    /// <summary>
    /// Nothing defines it, so the compiler checks each <c>[Localized]</c> and leaves it out of the assembly.
    /// </summary>
    internal const string AttributeCondition = "IDIOMA_LOCALIZED_IN_METADATA";

    private const string TextName = PackageNames.LocalizedText;

    /// <summary>
    /// A call in Razor markup, which other generators can't see as C#.
    /// </summary>
    private static readonly Regex InlineTextInMarkup = new(@"\bLocalizedText\s*\.\s*Inline\s*\(", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly DiagnosticDescriptor InvalidLanguagesRule = new(
        id: InvalidLanguagesDiagnosticId,
        title: "Invalid language in [assembly: SupportedLanguages]",
        messageFormat: "[assembly: SupportedLanguages] can't use one of its languages, because {0}. List each language once, as a culture name such as en or en-GB.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Each language must be a culture name and appear once, because it becomes a parameter of LocalizedText.Inline and is matched against the UI culture at run time.",
        helpLinkUri: PackageNames.HelpLink(InvalidLanguagesDiagnosticId));

    /// <summary>
    /// A code with a language or region no culture has (<see cref="SupportedLanguages.FindUnknownPart"/>).
    /// </summary>
    // A warning, because the build's tables can lag behind the standards.
    internal static readonly DiagnosticDescriptor UnknownLanguageCodeRule = new(
        id: UnknownLanguageCodeDiagnosticId,
        title: "Language code that doesn't exist",
        messageFormat: "'{0}' won't match any user's culture, because '{1}' isn't an {2} code. {3}.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A language code is matched against the user's UI culture at run time. A code such as dk has the form of a culture name, so it compiles and its sets are complete. Danish is da, so no user ever sees the texts of dk. The build checks a language of two letters against ISO 639-1, and a region of two letters against ISO 3166-1. It doesn't ask the cultures of the build machine, so every machine reports the same. Longer languages, scripts, and regions of three digits aren't checked.",
        helpLinkUri: PackageNames.HelpLink(UnknownLanguageCodeDiagnosticId));

    /// <summary>
    /// <c>LOC103</c> at the place that writes the code.
    /// </summary>
    internal static Diagnostic CreateUnknownLanguageCode(UnknownLanguagePart unknown, Location location)
    {
        var diagnostic = Diagnostic.Create(UnknownLanguageCodeRule, location, unknown.Code, unknown.Part, unknown.Kind, unknown.Advice);
        return diagnostic;
    }

    private static readonly DiagnosticDescriptor LanguageVersionTooOldRule = new(
        id: LanguageVersionTooOldDiagnosticId,
        title: "C# version older than 14",
        messageFormat: "Idioma needs C# 14 or later, and this project uses C# {0}. Remove <LangVersion> from the project file, or set it to 14 or later.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The code generated for an assembly that declares languages uses extension members, which C# 14 brings, so the generators write none of it for an older version. C# 14 is the default on .NET 10, unless the project sets an older LangVersion.",
        helpLinkUri: PackageNames.HelpLink(LanguageVersionTooOldDiagnosticId));

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static context => context.AddEmbeddedAttributeDefinition());

        var languages = SupportedLanguages.Read(context);
        var compiles = LanguageVersionCheck.Read(context, languages);

        // Two outputs, so an edit that only moves the attribute's location doesn't rewrite the factory.
        context.RegisterSourceOutput(languages, static (context, languages) =>
        {
            if (languages is not { } declared)
                return;

            foreach (var problem in declared.Problems)
                context.ReportDiagnostic(Diagnostic.Create(InvalidLanguagesRule, declared.Location.ToLocation(), problem));

            // Such as 'dk' for Danish, or 'en-UK'.
            foreach (var code in declared.Codes)
                if (SupportedLanguages.FindUnknownPart(code) is { } unknown)
                    context.ReportDiagnostic(CreateUnknownLanguageCode(unknown, declared.Location.ToLocation()));
        });

        // Once, at the attribute, for all the generators.
        var version = context.ParseOptionsProvider.Select(static (options, _) => (options as CSharpParseOptions)?.LanguageVersion);

        context.RegisterSourceOutput(languages.Combine(version), static (context, data) =>
        {
            if (data is ({ Codes.Length: > 0 } declared, { } languageVersion and < LanguageVersionCheck.Required))
                context.ReportDiagnostic(Diagnostic.Create(LanguageVersionTooOldRule, declared.Location.ToLocation(), languageVersion.ToDisplayString()));
        });

        context.RegisterSourceOutput(languages.Select(static (languages, _) => languages?.Codes ?? default).WhereItCompiles(compiles), static (context, codes) =>
        {
            if (codes.Length is 0)
                return;

            context.AddSource("LocalizedText.Factory.g.cs", GenerateSource(codes));
            context.AddSource("LocalizedAttribute.g.cs", GenerateAttribute(codes));
        });

        // Not an analyzer, because the compiler stops at the unbound attribute (CS0246) before any analyzer runs.
        var localizedWithoutLanguages = context.SyntaxProvider
            .CreateSyntaxProvider(static (node, _) => LocalizedMembers.IsNamedLocalized(node), static (context, cancellationToken) => FindLocalizedWithoutLanguages(context, cancellationToken))
            .Where(static location => location is not null)
            .Select(static (location, _) => location!.Value)
            .Collect();

        context.RegisterSourceOutput(localizedWithoutLanguages, static (context, locations) =>
        {
            foreach (var location in locations)
                context.ReportDiagnostic(Diagnostic.Create(SharedRules.NoLanguagesDeclared, location.ToLocation(), $"[{LocalizedMembers.AttributeName}] takes a wording per language"));
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

        context.RegisterSourceOutput(writesInlineTexts.WhereItCompiles(compiles), static (context, writesInlineTexts) =>
        {
            if (writesInlineTexts)
            {
                context.AddSource("StandaloneTexts.g.cs", $"""
                    // <auto-generated/>
                    {GeneratedFile.DisableWarnings(GeneratedFile.ObsoleteWarnings, "Only generated code applies StandaloneTexts.")}
                    [assembly: {PackageNames.StandaloneTextsAttribute}]
                    {GeneratedFile.RestoreWarnings(GeneratedFile.ObsoleteWarnings)}

                    """);
            }
        });
    }

    /// <summary>
    /// Whether a node calls <c>Something.Inline(...)</c>, which is all the syntax can tell.
    /// </summary>
    private static bool IsInlineCall(SyntaxNode node)
    {
        var isInlineCall = node is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access }
               && String.Equals(access.Name.Identifier.ValueText, FactoryName, StringComparison.Ordinal);

        return isInlineCall;
    }

    /// <summary>
    /// Where an unbound <c>[Localized]</c> is written in an assembly without languages, else <see langword="null"/>.
    /// </summary>
    // The languages first, because they're cheap, and with them nothing needs binding.
    private static DiagnosticLocation? FindLocalizedWithoutLanguages(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        var attribute = (AttributeSyntax)context.Node;

        var isWithoutLanguages = SupportedLanguages.ReadCodes(context.SemanticModel.Compilation.Assembly).Length is 0
                                 && LocalizedMembers.IsUnbound(attribute, context.SemanticModel, cancellationToken);

        return isWithoutLanguages ? DiagnosticLocation.From(attribute.Name.GetLocation()) : null;
    }

    /// <summary>
    /// Whether <c>Something.Inline(...)</c> calls the factory, also qualified or through an alias. The receiver's name
    /// decides first, and only another name is bound.
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

        if (String.Equals(name, "LocalizedText", StringComparison.Ordinal))
            return true;

        // Only a plain name can be an alias.
        var isFactoryCall = receiver is IdentifierNameSyntax
               && context.SemanticModel.GetSymbolInfo(receiver, cancellationToken).Symbol is INamedTypeSymbol type
               && String.Equals(type.ToDisplayString(), PackageNames.LocalizedText, StringComparison.Ordinal);

        return isFactoryCall;
    }

    /// <summary>
    /// Writes <c>[Localized]</c>, with one required parameter per language.
    /// </summary>
    // Not a primary constructor, because its unread parameters would warn (CS9113).
    private static string GenerateAttribute(EquatableArray<string> codes)
    {
        var parameters = String.Join(", ", codes.Select(static code => $"string {SupportedLanguages.ToParameterName(code)}"));
        var sample = String.Join(", ", codes.Select(static code => $"{SupportedLanguages.ToParameterName(code)}: \"...\""));

        var sb = new StringBuilder();
        sb.AppendLine($$"""
            // <auto-generated/>
            #nullable enable

            namespace CodeChops.Idioma;

            /// <summary>
            /// The text of a member, in every language this assembly supports: <c>[{{LocalizedMembers.AttributeName}}({{sample}})]</c> on a property, a
            /// field, a member of an enum, or a type. <see cref="{{TextName}}.GetTextByMember(System.Reflection.MemberInfo)"/> gives
            /// the text, and <c>{{LocalizedMembers.ExtensionName}}()</c> on a value of an enum gives that of its member.
            /// </summary>
            /// <remarks>
            /// The source generator reads the wording as this assembly builds, and writes the text as code. So nothing reads this attribute at
            /// run time, and it isn't part of the compiled assembly.
            /// </remarks>
            [Microsoft.CodeAnalysis.EmbeddedAttribute]
            [System.Diagnostics.Conditional("{{AttributeCondition}}")]
            [System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field | System.AttributeTargets.Class | System.AttributeTargets.Struct | System.AttributeTargets.Interface | System.AttributeTargets.Enum, AllowMultiple = false)]
            internal sealed class {{LocalizedMembers.AttributeTypeName}} : System.Attribute
            {
                /// <summary>
                /// The wording of the member's text in every language: <c>[{{LocalizedMembers.AttributeName}}({{sample}})]</c>.
                /// </summary>
                /// <remarks>
                /// Name each argument after its language. An unnamed one is a build error (<c>LOC500</c>), because two swapped languages
                /// would go unnoticed. The wording is a constant, so it can't show a value. Write a text with a value as a method of a set.
                /// </remarks>
            """);

        foreach (var code in codes)
        {
            var name = SupportedLanguages.ToParameterName(code).TrimStart('@');
            sb.AppendLine($"    /// <param name=\"{name}\">The wording in <c>{code}</c>.</param>");
        }

        sb.AppendLine($$"""
                public {{LocalizedMembers.AttributeTypeName}}({{parameters}})
                {
                }
            }
            #nullable restore
            """);

        return sb.ToString();
    }

    private static string GenerateSource(EquatableArray<string> codes)
    {
        var defaultParameter = SupportedLanguages.ToParameterName(codes[0]);
        var parameters = String.Join(", ", codes.Select(static code => $"string {SupportedLanguages.ToParameterName(code)}"));
        var languages = String.Join(", ", codes.Select(static code => SymbolDisplay.FormatLiteral(SupportedLanguages.Normalize(code), quote: true)));

        // Only the picked wording is checked for null, because the others aren't read.
        var wordings = codes.Skip(1).Select(static (code, index) =>
        {
            var parameter = SupportedLanguages.ToParameterName(code);
            return $"                {index + 1} => {parameter} ?? throw new System.ArgumentNullException(nameof({parameter})),";
        });
        var sample = String.Join(", ", codes.Select(static code => $"{SupportedLanguages.ToParameterName(code)}: \"...\""));

        var sb = new StringBuilder();
        sb.AppendLine($$"""
            {{GeneratedFile.GetHeader(GeneratedFile.ObsoleteWarnings, "Only generated code calls GeneratedCode.")}}

            // In the global namespace, which every file reaches: LocalizedText.Inline then binds however a call names
            // the type, also by its full name in a file without a using for its namespace.

            /// <summary>
            /// Writes texts in every language of this assembly's <see cref="{{PackageNames.SupportedLanguagesAttribute}}"/>.
            /// </summary>
            [Microsoft.CodeAnalysis.EmbeddedAttribute]
            {{GeneratedFile.CodeAttributes}}
            internal static class {{ClassName}}
            {
                // The languages, the default first, as the package picks one of them by its position. One array for
                // every call, which the package tells apart by reference. The texts on members pass it too, because
                // the package keeps its last pick for one array.
                internal static readonly string[] {{LanguagesName}} = [{{languages}}];

                extension({{TextName}})
                {
                    /// <summary>
                    /// A text in every language this assembly supports: <c>LocalizedText.Inline({{sample}})</c>.
                    /// It gives the wording for the current UI culture. A culture that none of them serves gets the wording in the app's
                    /// default language, else the <c>{{codes[0]}}</c> wording.
                    /// </summary>
                    /// <remarks>
                    /// Name each argument after its language. An unnamed one is a build error (<c>LOC500</c>), because two swapped languages
                    /// would go unnoticed. To include a value, write it into every language, as in <c>$"Hello, {name}!"</c>.
                    /// The string holds the wording of the moment it's made, so call this where you show the text.
                    /// </remarks>
            """);

        foreach (var code in codes)
        {
            var name = SupportedLanguages.ToParameterName(code).TrimStart('@');
            sb.AppendLine($"        /// <param name=\"{name}\">The wording in <c>{code}</c>.</param>");
        }

        sb.AppendLine($$"""
                    public static string Inline({{parameters}})
                    {
                        return {{PackageNames.GeneratedCode}}.GetLanguageIndexForStandaloneText({{LanguagesName}}) switch
                        {
            """);

        foreach (var wording in wordings)
            sb.AppendLine(wording);

        sb.AppendLine($$"""
                            _ => {{defaultParameter}} ?? throw new System.ArgumentNullException(nameof({{defaultParameter}})),
                        };
                    }
                }
            }
            #nullable restore
            """);

        return sb.ToString();
    }
}
