namespace CodeChops.Idioma.Generators;

/// <summary>
/// Writes the file of a contract (<see cref="LocalizationContract"/>): the generated class with its texts, then the
/// contract interface, and for a set of the texts of an enum the switch that gives the text of a value.
/// </summary>
internal static partial class LocalizationContractSource
{
    // The namespaces the file of a contract imports, for what the method that gives a text by its key names (AppendGet).
    private static readonly string[] ImportedNamespaces = ["System", "System.Collections.Generic", "System.Collections.Immutable", "System.Diagnostics.CodeAnalysis"];

    /// <summary>
    /// What the parts of a contract's file are written from: the contract, the sets it links to, and the names the
    /// file writes throughout.
    /// </summary>
    /// <param name="Contract">The contract the file is written for.</param>
    /// <param name="Sets">The sets the contract links to, each with its texts by name.</param>
    /// <param name="ClassName">The default-language set, as code writes its name.</param>
    /// <param name="DefaultSet">
    /// What the texts take their default wording from, as code writes its name: the default-language set, or the class
    /// that implements the contract for it where it doesn't list the contract (<see cref="AppendDefaultSetAsContract"/>).
    /// </param>
    /// <param name="ContractName">The name of the contract interface.</param>
    /// <param name="TextsName">The generated class, as code writes its name.</param>
    /// <param name="Texts">The texts the generated class holds, which leaves out a text with a reserved name (<c>LOC300</c>).</param>
    /// <param name="Accessibility">The accessibility of the generated types: <c>public</c> or <c>internal</c>.</param>
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

        // The set by its simple name: this file lies in the set's namespace. Its full name would start with a name the
        // namespace may hold something else under, as a namespace Shop that holds a class Shop does.
        var file = new ContractFile(
            contract,
            ContractDocComments.IndexSets(contract),
            ClassName: CodeEscaping.EscapeIdentifier(contract.ClassName),
            DefaultSet: CodeEscaping.EscapeIdentifier(contract.DefaultSetListsContract ? contract.ClassName : $"{contract.ClassName}AsContract"),
            ContractName: contract.ContractName,
            TextsName: CodeEscaping.EscapeIdentifier(contract.TextsName),
            texts,
            Accessibility: contract.IsPublic ? "public" : "internal");

        // The framework's types are imported here, at the top of the file, and named by their name alone. So is what
        // words a text, which is called by its name alone. Inside the generated class a text stands before every
        // namespace of its name, so behind a text named CodeChops the package's namespace could not be reached to call
        // it. The pragma also covers a set marked [Obsolete] or [Experimental], whose texts read it for as long as it
        // exists, and an enum, a member of it or a parameter type marked so, which the file names too.
        var warnings = GeneratedFile.WithSuppressed(GeneratedFile.ObsoleteWarnings, contract.SuppressedDiagnostics);
        var sb = new StringBuilder();
        AppendHeader(sb, contract, warnings);

        // The texts first, because they're what a reader of this file looks for. Partial, so that a helper can sit next
        // to them, unless the generated code can't create the set for their default wording (LOC200). A switch it
        // calls, for a set of the texts of an enum, comes last.
        var lookups = new StringBuilder();

        if (contract.HasTexts)
        {
            AppendTexts(sb, lookups, file);
            sb.AppendLine();
        }

        AppendContract(sb, file);

        // The text of each member of an enum, outside the generated class: there a text named like the enum, or like
        // what its name starts with, would stand before it in a pattern. File-local, so that inside this file its name
        // binds to it even next to another type of that name. Generic, so that no text can stand before it: a name
        // with a type argument is not looked for among the texts.
        if (lookups.Length > 0)
        {
            sb.AppendLine($$"""

                file static class TextsByValue<TContract>
                {
                """);
            sb.Append(lookups);
            sb.AppendLine("}");
        }

        if (contract.HasTexts)
            AppendTextKeys(sb, file);

        if (contract.HasTexts && !contract.DefaultSetListsContract)
            AppendDefaultSetAsContract(sb, file);

        if (contract.HasTexts && texts.Any(static text => text.IsObsoleteAsError))
            AppendObsoleteTexts(sb, file);

        GeneratedFile.AppendFooter(sb, warnings);

        return sb.ToString();
    }

    /// <summary>
    /// Writes the class that implements the contract for a default-language set that doesn't list it (<c>LOC204</c>).
    /// Each member hands on to the set. So the texts compile and show the set's wording, and LOC204 is the one error,
    /// at the set, rather than an error per text in this file.
    /// </summary>
    // Explicit implementations, so that no text can clash with a member of the class, such as one named ToString or
    // set. File-local, so that it's no type of the assembly: neither the generator nor the registry takes it for a set.
    // Marked [Obsolete] where a text of the set is obsolete as an error, which code inside such a class may name.
    private static void AppendDefaultSetAsContract(StringBuilder sb, ContractFile file)
    {
        var contract = file.Contract;
        var name = file.ContractName;

        sb.AppendLine($$"""

            /// <summary>
            /// Implements <see cref="{{name}}"/> for <see cref="{{file.ClassName}}"/>, which doesn't list it yet (LOC204), so that the texts compile.
            /// </summary>
            {{(contract.Members.Any(static member => member.IsObsoleteAsError) ? "[Obsolete] " : "")}}file sealed class {{file.DefaultSet}} : {{PackageNames.LocalizationSet}}, {{name}}
            {
                private readonly {{file.ClassName}} set = new();

                string {{PackageNames.LocalizationSet}}.LocalizationLanguageCode => (({{PackageNames.LocalizationSet}})this.set).LocalizationLanguageCode;
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
    /// Writes what stands above the types of a contract's file: the pragma that turns off
    /// <paramref name="warnings"/>, the using directives, and the namespace.
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

        // The using directives of the default-language set, for a text that takes a value of a type that bound to
        // nothing when this was written, as one another generator writes. Such a type stands here by the words the
        // set writes it in, and those name it only next to the directives the set has. A type that did bind needs
        // none of them: it stands here by its full name.
        AppendUsings(sb, contract.Usings.OfFile);
        sb.AppendLine();

        if (@namespace.Length > 0)
        {
            sb.AppendLine($"""
                namespace {@namespace};

                """);

            // Those the set has inside its namespace, where a name may be written from that namespace on.
            if (contract.Usings.OfNamespace.Length > 0)
            {
                AppendUsings(sb, contract.Usings.OfNamespace);
                sb.AppendLine();
            }
        }
    }

    /// <summary>
    /// Writes the contract interface, with one member per text.
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

        // Each member of the contract inherits the doc comment of its text, which says the same. A member writes its
        // own when the class doesn't hold its text (LOC300). A text named like the class doesn't hide the class from a
        // cref, which looks for a type in front of the dot.
        var inheritingDocs = new HashSet<string>(StringComparer.Ordinal);

        if (contract.HasTexts)
            inheritingDocs.UnionWith(texts.Select(static text => text.Name));

        // The contract, which every set lists itself. The contract of a set of an enum names its enums, which each set
        // lists as ILocalizationSet<TEnum> (LOC205).
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

            // Each value the default wording shows, which TextParametersAnalyzer holds every other set to, in this
            // assembly and in those referencing it. On the method and not on the parameter, because an IDE's "Implement
            // interface" copies the attributes of a parameter into the set it writes, where this obsolete one would warn.
            foreach (var parameter in member.Parameters)
            {
                if (parameter.IsShown)
                    sb.AppendLine($"    [{PackageNames.InternalsNamespace}.ShownValue({SymbolDisplay.FormatLiteral(parameter.Name, quote: true)})]");
            }

            // A text that takes values is a method: its parameters are what the compiler holds every language to.
            sb.AppendLine($"    string {CodeEscaping.EscapeIdentifier(member.Name)}({ContractTypeNames.RenderParameters(member, contract.Namespace)});");
        }

        sb.AppendLine("}");
    }

    /// <summary>
    /// Writes the using directives a contract's file repeats (<see cref="RepeatedUsings"/>), but for those it has
    /// already, because a repeated one is a warning (CS0105).
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
