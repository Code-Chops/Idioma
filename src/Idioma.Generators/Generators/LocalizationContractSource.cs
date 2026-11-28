namespace CodeChops.Idioma.Generators;

/// <summary>
/// Writes a contract's file: the generated class with its texts, the contract, and for an enum's texts the switch that
/// gives the text of a value.
/// </summary>
internal static partial class LocalizationContractSource
{
    // For what AppendGet names, and for GeneratedFile.ImportedCodeAttributes.
    private static readonly string[] ImportedNamespaces = ["System", "System.Collections.Immutable", .. GeneratedFile.CodeAttributeNamespaces];

    /// <summary>
    /// What the parts of a contract's file are written from. Names are escaped as code writes them.
    /// </summary>
    /// <param name="ClassName">The default-language set.</param>
    /// <param name="DefaultSet">
    /// The default-language set, or the class that implements the contract for it when it doesn't list it
    /// (<see cref="AppendDefaultSetAsContract"/>).
    /// </param>
    /// <param name="TextsName">The generated class.</param>
    /// <param name="Texts">The texts of the generated class, without reserved names (<c>LOC300</c>).</param>
    private sealed record ContractFile(
        LocalizationContract Contract,
        ContractDocComments.IndexedSet[] Sets,
        string ClassName,
        string DefaultSet,
        string ContractName,
        string TextsName,
        LocalizationMember[] Texts,
        string Accessibility);

    public static string Generate(LocalizationContract contract)
    {
        var texts = contract.Members
            .Where(member => !LocalizationSetsAnalysis.IsReservedTextName(member.Name, contract.TextsName, hasLookup: contract.Enums.Length > 0))
            .ToArray();

        // The set by its simple name, because the file lies in its namespace, and a full name could be shadowed, as by a
        // class Shop in a namespace Shop.
        var file = new ContractFile(
            contract,
            ContractDocComments.IndexSets(contract),
            ClassName: CodeEscaping.EscapeIdentifier(contract.ClassName),
            DefaultSet: CodeEscaping.EscapeIdentifier(contract.DefaultSetListsContract ? contract.ClassName : $"{contract.ClassName}AsContract"),
            ContractName: contract.ContractName,
            TextsName: CodeEscaping.EscapeIdentifier(contract.TextsName),
            texts,
            Accessibility: contract.IsPublic ? "public" : "internal");

        // Types are imported at the top and named alone, because inside the generated class a text named CodeChops
        // would hide the package's namespace. The pragma also covers an obsolete set, enum, member or parameter type the
        // file names.
        const string warnings = GeneratedFile.ObsoleteWarnings;
        var sb = new StringBuilder();
        AppendHeader(sb, contract, warnings);

        // The texts first, because they're what someone opening the file looks for. An enum's switch comes last.
        var lookups = new StringBuilder();

        if (contract.HasTexts)
        {
            AppendTexts(sb, lookups, file);
            sb.AppendLine();
        }

        AppendContract(sb, file);

        // Outside the generated class, where a text named like the enum would hide it in a pattern. File-local, so its
        // name binds here next to another type of that name. Generic, because a name with a type argument isn't looked
        // up among the texts.
        if (lookups.Length > 0)
        {
            sb.AppendLine($$"""

                {{GeneratedFile.ImportedCodeAttributes}}
                file static class TextsByValue<TContract>
                {
                """);
            sb.Append(lookups);
            sb.AppendLine("}");
        }

        if (contract.HasTexts)
            AppendTextKeys(sb, file);

        if (contract is { HasTexts: true, DefaultSetListsContract: false })
            AppendDefaultSetAsContract(sb, file);

        if (contract.HasTexts && contract.Enums.Any(static @enum => @enum.HasTextsOfItsAssembly))
            AppendEnumTextsRegistration(sb, file);

        GeneratedFile.AppendFooter(sb, warnings);

        return sb.ToString();
    }

    /// <summary>
    /// Writes the class that implements the contract for a default-language set that doesn't list it (<c>LOC205</c>),
    /// so LOC205 is the one error rather than an error per text.
    /// </summary>
    // Explicit implementations, so no text clashes with a member such as ToString. File-local, so neither the generator
    // nor the registry takes it for a set. It has no language, because LOC205 fails the build before any text is read.
    private static void AppendDefaultSetAsContract(StringBuilder sb, ContractFile file)
    {
        var contract = file.Contract;
        var name = file.ContractName;

        sb.AppendLine($$"""

            /// <summary>
            /// Implements <see cref="{{name}}"/> for <see cref="{{file.ClassName}}"/>, which doesn't list it yet (LOC205), so that the texts compile.
            /// </summary>
            {{GeneratedFile.ImportedCodeAttributes}}
            file sealed class {{file.DefaultSet}} : {{PackageNames.LocalizationSet}}, {{name}}
            {
                private readonly {{file.ClassName}} set = new();
            """);

        foreach (var member in contract.Members)
        {
            var memberName = CodeEscaping.EscapeIdentifier(member.Name);

            if (!member.IsMethod)
            {
                sb.AppendLine($"    string {name}.{memberName} => this.set.{memberName};");
                continue;
            }

            var arguments = String.Join(", ", member.Parameters.Select(static parameter => CodeEscaping.EscapeIdentifier(parameter.Name)));
            sb.AppendLine($"    string {name}.{memberName}({ContractTypeNames.RenderParameters(member, contract.Namespace)}) => this.set.{memberName}({arguments});");
        }

        sb.AppendLine("}");
    }

    /// <summary>
    /// Writes the pragma, the using directives and the namespace.
    /// </summary>
    private static void AppendHeader(StringBuilder sb, LocalizationContract contract, string warnings)
    {
        var @namespace = CodeEscaping.EscapeNamespace(contract.Namespace);

        sb.AppendLine($"""
            {GeneratedFile.GetHeader(warnings, "Only generated code calls GeneratedCode.")}

            """);

        foreach (var imported in ImportedNamespaces)
            sb.AppendLine($"using {imported};");

        sb.AppendLine($"using static {PackageNames.GeneratedCode};");

        // The set's using directives, for a parameter type that didn't bind, such as one another generator writes. It's
        // written as the set writes it, so it needs the set's directives.
        AppendUsings(sb, contract.Usings.OfFile);
        sb.AppendLine();

        if (@namespace.Length > 0)
        {
            sb.AppendLine($"""
                namespace {@namespace};

                """);

            // The directives the set has inside its namespace.
            if (contract.Usings.OfNamespace.Length > 0)
            {
                AppendUsings(sb, contract.Usings.OfNamespace);
                sb.AppendLine();
            }
        }
    }

    /// <summary>
    /// Writes the contract, with one member per text.
    /// </summary>
    private static void AppendContract(StringBuilder sb, ContractFile file)
    {
        var contract = file.Contract;
        var sets = file.Sets;
        var className = file.ClassName;
        var name = file.ContractName;
        var textsName = file.TextsName;
        var texts = file.Texts;
        var accessibility = file.Accessibility;

        // A member inherits its text's doc comment, or writes its own when the class doesn't hold the text (LOC300).
        var inheritingDocs = new HashSet<string>(StringComparer.Ordinal);

        if (contract.HasTexts)
            inheritingDocs.UnionWith(texts.Select(static text => text.Name));

        // An enum's sets list ILocalizationSet<TEnum> (LOC206).
        var setInterface = contract.Enums.Length is 0
            ? $"<see cref=\"{PackageNames.LocalizationSet}\"/>"
            : $"<c>{CodeEscaping.EscapeXml(String.Join(", ", contract.Enums.Select(@enum => $"{SupportedLanguages.SetInterface}<{ContractTypeNames.RenderType(@enum.Type, contract.Namespace)}>")))}</c>";
        var marker = contract.Enums.Length is 0
            ? $"{PackageNames.InternalsNamespace}.LocalizationContract"
            : $"{PackageNames.InternalsNamespace}.LocalizationContract({String.Join(", ", contract.Enums.Select(@enum => $"typeof({ContractTypeNames.RenderType(@enum.Type, contract.Namespace)})"))})";

        sb.AppendLine($$"""
            /// <summary>
            /// The contract of <see cref="{{className}}"/>, with one member per text.
            /// Every language in <c>[assembly: SupportedLanguages]</c> has a set that implements it next to {{setInterface}}, named with its language as suffix.
            /// A language without a set, or a set without a text, is a build error rather than a fallback at run time.
            /// Each set is registered automatically, so there's nothing to register.
            /// </summary>
            [{{marker}}]
            {{GeneratedFile.ImportedGeneratedCodeAttribute}}
            {{accessibility}} interface {{name}}
            {
            """);
        foreach (var member in contract.Members)
        {
            if (inheritingDocs.Contains(member.Name))
                sb.AppendLine($"    /// <inheritdoc cref=\"{textsName}.{CodeEscaping.EscapeIdentifier(member.Name)}\"/>");
            else
                ContractDocComments.AppendMemberDocs(sb, contract, sets, className, member);

            if (!member.IsMethod)
            {
                sb.AppendLine($"    string {CodeEscaping.EscapeIdentifier(member.Name)} {{ get; }}");
                continue;
            }

            // Each value the default wording shows, for TextParametersAnalyzer. On the method, because "Implement
            // interface" copies a parameter's attributes into the set, where this obsolete one would warn.
            foreach (var parameter in member.Parameters)
                if (parameter.IsShown)
                    sb.AppendLine($"    [{PackageNames.InternalsNamespace}.ShownValue({SymbolDisplay.FormatLiteral(parameter.Name, quote: true)})]");

            sb.AppendLine($"    string {CodeEscaping.EscapeIdentifier(member.Name)}({ContractTypeNames.RenderParameters(member, contract.Namespace)});");
        }

        sb.AppendLine("}");
    }

    /// <summary>
    /// Writes the repeated using directives (<see cref="RepeatedUsings"/>), skipping those the file has (CS0105).
    /// </summary>
    private static void AppendUsings(StringBuilder sb, EquatableArray<string> usings)
    {
        foreach (var directive in usings)
        {
            var isWritten = String.Equals(directive, $"using static {PackageNames.GeneratedCode};", StringComparison.Ordinal)
                            || ImportedNamespaces.Any(imported => String.Equals(directive, $"using {imported};", StringComparison.Ordinal));

            if (!isWritten)
                sb.AppendLine(directive);
        }
    }
}
