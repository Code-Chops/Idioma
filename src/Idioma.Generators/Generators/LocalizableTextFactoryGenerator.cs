using System.Text.RegularExpressions;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Writes <c>LocalizableText.Inline</c> for the languages of <c>[assembly: SupportedLanguages(...)]</c>, one required
/// parameter per language, so that leaving a language out is a compile error. It is a static extension on the one shared
/// <c>LocalizableText</c>, so that every assembly writes a text the same way, whatever its languages. It reports
/// <c>LOC101</c> for an invalid or repeated language, and still writes the factory for the valid ones, so that error
/// stays the only one wherever no call names the language that is wrong. A language with a part no culture has, such
/// as <c>dk</c> for Danish, gets the warning <c>LOC104</c>, and its parameter all the same. Next to the factory it writes the attribute
/// <c>[Localized]</c>, with the same parameters, which puts a text on a member.
/// </summary>
/// <remarks>
/// <para>
/// The class that holds the extension lies in the global namespace. An extension is found only where its namespace is
/// in reach, and the global namespace is in reach of every file, so <c>LocalizableText.Inline</c> binds however the
/// call names the type: by its name alone, or by its full name in a file without a using for its namespace.
/// </para>
/// <para>
/// The class is marked <c>[Embedded]</c> because a library and a test project with its <c>InternalsVisibleTo</c> both
/// generate one, and two visible factories with the same languages would make every call ambiguous.
/// </para>
/// <para>
/// The attribute is a type of its own per assembly, which it can be because it never stands in a signature. It's only
/// there for the build: <see cref="LocalizedMembers"/> reads its arguments and <see cref="LocalizedMembersSource"/>
/// writes the texts as code, so the attribute is marked <c>[Conditional]</c> and stays out of the compiled assembly.
/// Its wording would otherwise be stored twice.
/// </para>
/// <para>
/// An assembly that calls the factory, in code or in Razor markup, is marked <c>[assembly: InlineTexts]</c>. No
/// assembly referencing it can add a language to such a text, so its build warns about the languages it lacks
/// (<c>LOC701</c>). An assembly with a text on a member gets the same mark from the file that holds those texts.
/// </para>
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class LocalizableTextFactoryGenerator : IIncrementalGenerator
{
    public const string InvalidLanguagesDiagnosticId = "LOC101";
    public const string LanguageVersionTooOldDiagnosticId = "LOC102";
    public const string UnknownLanguageCodeDiagnosticId = "LOC104";

    internal const string ClassName = "GeneratedLocalizableText";
    internal const string FactoryName = "Inline";

    /// <summary>
    /// The symbol the generated attribute is conditional on. Nothing defines it, so the compiler checks each
    /// <c>[Localized]</c> and leaves it out of the assembly.
    /// </summary>
    internal const string AttributeCondition = "IDIOMA_LOCALIZED_IN_METADATA";

    private const string TextName = PackageNames.LocalizableText;

    /// <summary>
    /// A call of the factory in Razor markup, which other generators can't see as C#, because Razor's own generator
    /// writes it.
    /// </summary>
    private static readonly Regex InlineTextInMarkup = new(@"\bLocalizableText\s*\.\s*Inline\s*\(", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly DiagnosticDescriptor InvalidLanguagesRule = new(
        id: InvalidLanguagesDiagnosticId,
        title: "Invalid language in [assembly: SupportedLanguages]",
        messageFormat: "[assembly: SupportedLanguages] can't use one of its languages, because {0}. List each language once, as a culture name such as en or en-GB.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Each language must be a culture name and appear once, because it becomes a parameter of LocalizableText.Inline and is matched against the UI culture at run time.",
        helpLinkUri: PackageNames.HelpLink(InvalidLanguagesDiagnosticId));

    /// <summary>
    /// A language code that has the form of a culture name, with a language or a region no culture has
    /// (<see cref="SupportedLanguages.FindUnknownPart"/>). Reported for a language of
    /// <c>[assembly: SupportedLanguages]</c>, and by <see cref="LocalizationRegistrationPart"/> for a set's
    /// <c>LocalizationLanguageCode</c>.
    /// </summary>
    // A warning, because the tables the build carries can lag behind the standards, and a warning can be turned off.
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
    /// The warning for a language code with a part no culture has (<c>LOC104</c>), at the place that writes the code.
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

    // The code of a set in an assembly without languages, and of a call of LocalizableText.Inline there: all say that
    // the attribute is missing, and all have the same fix.
#pragma warning disable RS1019 // Diagnostic Id is already used by another analyzer
    private static readonly DiagnosticDescriptor LocalizedWithoutLanguagesRule = new(
        id: LocalizationSetsPart.NoLanguagesDeclaredDiagnosticId,
        title: "Text on a member in an assembly without languages",
        messageFormat: "{0} takes a wording per language, but this assembly declares no languages. Add [assembly: SupportedLanguages(\"en\", \"nl\")] with your languages, the default first.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The source generator writes [Localized] with one parameter per language of [assembly: SupportedLanguages]. Without the attribute it writes none, so the compiler finds no such attribute (CS0246).",
        helpLinkUri: PackageNames.HelpLink(LocalizationSetsPart.NoLanguagesDeclaredDiagnosticId));
#pragma warning restore RS1019

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static context => context.AddEmbeddedAttributeDefinition());

        var languages = SupportedLanguages.Read(context);
        var compiles = LanguageVersionCheck.Read(context, languages);

        // Two outputs: the declaration holds its location, which every edit in the file of the attribute renews, and
        // the factory is written from the languages alone, so such an edit does not write it again.
        context.RegisterSourceOutput(languages, static (context, languages) =>
        {
            if (languages is not { } declared)
                return;

            foreach (var problem in declared.Problems)
                context.ReportDiagnostic(Diagnostic.Create(InvalidLanguagesRule, declared.Location.ToLocation(), problem));

            // A language the build uses, with a part no culture has: 'dk' for Danish, or 'en-UK'.
            foreach (var code in declared.Codes)
                if (SupportedLanguages.FindUnknownPart(code) is { } unknown)
                    context.ReportDiagnostic(CreateUnknownLanguageCode(unknown, declared.Location.ToLocation()));
        });

        // Once, at the attribute, for all the generators: none of them writes code for the languages.
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

            context.AddSource("LocalizableText.Factory.g.cs", GenerateSource(codes));
            context.AddSource("LocalizedAttribute.g.cs", GenerateAttribute(codes));
        });

        // Reported here, and not by an analyzer: the compiler stops at an attribute that binds to nothing (CS0246)
        // before any analyzer runs, so a build would show only that, and the advice to add a using directive.
        var localizedWithoutLanguages = context.SyntaxProvider
            .CreateSyntaxProvider(static (node, _) => LocalizedMembers.IsNamedLocalized(node), static (context, cancellationToken) => FindLocalizedWithoutLanguages(context, cancellationToken))
            .Where(static location => location is not null)
            .Select(static (location, _) => location!.Value)
            .Collect();

        context.RegisterSourceOutput(localizedWithoutLanguages, static (context, locations) =>
        {
            foreach (var location in locations)
                context.ReportDiagnostic(Diagnostic.Create(LocalizedWithoutLanguagesRule, location.ToLocation(), $"[{LocalizedMembers.AttributeName}]"));
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
                context.AddSource("InlineTexts.g.cs", $"""
                    // <auto-generated/>
                    {GeneratedFile.DisableWarnings(GeneratedFile.ObsoleteWarnings, "Only generated code applies InlineTexts.")}
                    [assembly: {PackageNames.InlineTextsAttribute}]
                    {GeneratedFile.RestoreWarnings(GeneratedFile.ObsoleteWarnings)}

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
    /// Where an attribute named <c>Localized</c> is written, when its assembly declares no languages and it binds to
    /// nothing, else <see langword="null"/>. An attribute of that name that binds is one the code brought itself.
    /// </summary>
    // The languages first, which the compilation reads once: with them, the attribute is the generated one, and nothing
    // needs binding.
    private static DiagnosticLocation? FindLocalizedWithoutLanguages(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        var attribute = (AttributeSyntax)context.Node;

        var isWithoutLanguages = SupportedLanguages.ReadCodes(context.SemanticModel.Compilation.Assembly).Length is 0
                                 && LocalizedMembers.IsUnbound(attribute, context.SemanticModel, cancellationToken);

        return isWithoutLanguages ? DiagnosticLocation.From(attribute.Name.GetLocation()) : null;
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

    /// <summary>
    /// Writes the attribute <c>[Localized]</c>, with one required parameter per language, as the factory has.
    /// </summary>
    // A constructor with a body, and not a primary constructor: its parameters are read by the source generator alone,
    // and a primary constructor's unread parameters are a warning (CS9113).
    private static string GenerateAttribute(EquatableArray<string> codes)
    {
        var parameters = String.Join(", ", codes.Select(static code => $"string {SupportedLanguages.ToParameterName(code)}"));
        var sample = String.Join(", ", codes.Select(static code => $"{SupportedLanguages.ToParameterName(code)}: \"...\""));

        var sb = new StringBuilder();
        sb.AppendLine($$"""
            // <auto-generated/>
            #nullable enable

            // In the global namespace, which every file reaches, as the factory of LocalizableText.Inline is.

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
        var languages = String.Join(", ", codes.Select(static code => SymbolDisplay.FormatLiteral(code, quote: true)));

        // The wording of the language the package picks, by its position among the languages. Only that wording is
        // checked for null, because the others aren't read.
        var wordings = codes.Skip(1).Select(static (code, index) =>
        {
            var parameter = SupportedLanguages.ToParameterName(code);
            return $"                {index + 1} => {parameter} ?? throw new System.ArgumentNullException(nameof({parameter})),";
        });
        var sample = String.Join(", ", codes.Select(static code => $"{SupportedLanguages.ToParameterName(code)}: \"...\""));

        var sb = new StringBuilder();
        sb.AppendLine($$"""
            {{GeneratedFile.GetHeader(GeneratedFile.ObsoleteWarnings, "Only generated code calls GeneratedCode.")}}

            // In the global namespace, which every file reaches: LocalizableText.Inline then binds however a call names
            // the type, also by its full name in a file without a using for its namespace.

            /// <summary>
            /// Writes texts in every language of this assembly's <see cref="{{PackageNames.SupportedLanguagesAttribute}}"/>.
            /// </summary>
            [Microsoft.CodeAnalysis.EmbeddedAttribute]
            internal static class {{ClassName}}
            {
                // The languages, the default first, as the package picks one of them by its position. One array for
                // every call, which the package tells apart by reference.
                private static readonly string[] Languages = [{{languages}}];

                extension({{TextName}})
                {
                    /// <summary>
                    /// A text in every language this assembly supports: <c>LocalizableText.Inline({{sample}})</c>.
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
                        return {{PackageNames.GeneratedCode}}.FindLanguage(Languages) switch
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
