namespace CodeChops.Idioma.Generators;

/// <summary>
/// Writes the texts of the <c>[Localized]</c> members (<see cref="LocalizedMembers"/>), and one function that finds a
/// member's wording by type and name, so <c>LocalizedText.GetTextByMember</c> reads no attribute at run time. The
/// registrar registers that function. An enum also gets <c>status.GetText()</c>.
/// </summary>
/// <remarks>
/// A type the file can't name from the top level gets no text (<c>LOC502</c>). The file marks the assembly
/// <c>[assembly: StandaloneTexts]</c>, so no referencing assembly can add a language (<c>LOC701</c>).
/// </remarks>
internal static class LocalizedMembersSource
{
    public const string HintName = "LocalizedMembers.g.cs";

    /// <summary>
    /// The class that finds the texts, at the top level of the file.
    /// </summary>
    private const string OwnMembersName = "OwnLocalizedMembers";

    /// <summary>
    /// The name of a class of one type's texts, before its number.
    /// </summary>
    private const string TextsName = "LocalizedTexts";

    private const string OwnTextName = "OwnText";

    /// <summary>
    /// The assembly's languages in the class of <c>LocalizedText.Inline</c>, in the order of each text's wordings.
    /// </summary>
    // Shared with inline texts, because the package caches its last pick for one array (StandaloneLanguage).
    private const string LanguagesReference = LocalizedTextFactoryGenerator.ClassName + "." + LocalizedTextFactoryGenerator.LanguagesName;

    public static string Generate(LocalizedMembersModel model)
    {
        var sb = new StringBuilder();

        // The pragma also covers an obsolete type or enum member the file names.
        const string warnings = GeneratedFile.ObsoleteWarnings;

        sb.AppendLine(GeneratedFile.GetHeader(warnings, "Only generated code calls GeneratedCode and applies StandaloneTexts."));
        sb.AppendLine();

        // For GeneratedFile.ImportedCodeAttributes, because a GetText() class lies in the enum's namespace.
        foreach (var imported in GeneratedFile.CodeAttributeNamespaces)
            sb.AppendLine($"using {imported};");

        sb.AppendLine($$"""

            // The texts below carry their wording themselves, as an inline text does.
            [assembly: {{PackageNames.StandaloneTextsAttribute}}]

            namespace {{LocalizedMembers.MemberTextsNamespace}}
            {
                /// <summary>
                /// Registers the texts of this assembly's members marked <c>[Localized]</c>, so that <c>LocalizedText.GetTextByMember</c> finds them.
                /// The assembly's registrar calls it.
                /// </summary>
                [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
                {{GeneratedFile.ImportedCodeAttributes}}
                internal static class {{model.MemberTextsClassName}}
                {
                    internal static void Register()
                    {
                        {{PackageNames.GeneratedCode}}.RegisterMemberTexts({{LanguagesReference}}, static (type, member) => {{OwnMembersName}}.Get(type, member));
                    }
                }
            }

            /// <summary>
            /// Finds the texts of this assembly's members marked <c>[Localized]</c>. This class is outside the namespace above, so a type's
            /// name is resolved from the global namespace, not from that namespace first.
            /// </summary>
            {{GeneratedFile.ImportedCodeAttributes}}
            file static class {{OwnMembersName}}
            {
                /// <summary>
                /// The wording of a text for the current UI culture. Each text below has its wording in the order of
                /// <see cref="{{LanguagesReference}}"/>, the languages of this assembly.
                /// </summary>
                internal static string Word(string[] wordings) => wordings[{{PackageNames.GeneratedCode}}.GetLanguageIndexForStandaloneText({{LanguagesReference}})];

                /// <summary>
                /// The wording of a member of a type per language, in the order of <see cref="{{LanguagesReference}}"/>, or the type's own for a member that is
                /// <see langword="null"/>.
                /// </summary>
                internal static string[]? Get(System.Type type, string? member)
                {
            """);

        for (var i = 0; i < model.Types.Length; i++)
        {
            var type = model.Types[i];
            var number = 0;

            sb.AppendLine($$"""
                        if (type == typeof({{type.Reference}}))
                        {
                            return member switch
                            {
                """);

            foreach (var text in type.Texts)
            {
                var pattern = text.MemberName is null ? "null" : SymbolDisplay.FormatLiteral(text.MemberName, quote: true);
                sb.AppendLine($"                {pattern} => {TextsName}{i}.{GetFieldName(text, ref number)},");
            }

            sb.AppendLine("""
                                _ => null,
                            };
                        }

                """);
        }

        sb.AppendLine("""
                    return null;
                }
            }
            """);

        for (var i = 0; i < model.Types.Length; i++)
            AppendTexts(sb, model, i);

        for (var i = 0; i < model.Types.Length; i++)
            if (model.Types[i].Extension is { } extension)
                AppendExtension(sb, extension, i);

        GeneratedFile.AppendFooter(sb, warnings);

        return sb.ToString();
    }

    /// <summary>
    /// Writes the class of one type's texts. For an enum, it also holds the switch the extension method calls.
    /// </summary>
    // Numbered fields, because a field named like a namespace would hide it in this class.
    private static void AppendTexts(StringBuilder sb, LocalizedMembersModel model, int index)
    {
        var type = model.Types[index];
        var number = 0;

        sb.AppendLine($$"""

            /// <summary>
            /// The texts of <c>{{CodeEscaping.EscapeXml(type.Reference)}}</c>.
            /// </summary>
            {{GeneratedFile.ImportedCodeAttributes}}
            file static class {{TextsName}}{{index}}
            {
            """);

        foreach (var text in type.Texts)
        {
            var wordings = String.Join(", ", model.Codes.Select((_, language) => SymbolDisplay.FormatLiteral(text.Wordings[language], quote: true)));

            sb.AppendLine($"    internal static readonly string[] {GetFieldName(text, ref number)} = [{wordings}];");
        }

        if (type.Extension is not null)
        {
            // A set of the enum that a referencing app registered goes first, so it can add its own languages.
            sb.AppendLine($$"""

                    internal static string Get({{type.Reference}} value) => CodeChops.Idioma.Internals.GeneratedCode.GetEnumText(value) ?? value switch
                    {
                """);

            // Of two members of one value, the first gives its text.
            var values = new HashSet<string>(StringComparer.Ordinal);
            number = 0;

            foreach (var text in type.Texts)
            {
                var field = GetFieldName(text, ref number);

                if (text.MemberName is null || (text.EnumValue is not null && !values.Add(text.EnumValue)))
                    continue;

                sb.AppendLine($"        {type.Reference}.{CodeEscaping.EscapeIdentifier(text.MemberName)} => {OwnMembersName}.Word({field}),");
            }

            sb.AppendLine($$"""
                        _ => value.ToString(),
                    };
                """);
        }

        sb.AppendLine("}");
    }

    /// <summary>
    /// Writes <c>GetText()</c> in the enum's namespace, so it's in reach wherever the enum is.
    /// </summary>
    private static void AppendExtension(StringBuilder sb, LocalizedEnumExtension extension, int index)
    {
        var accessibility = extension.IsPublic ? "public" : "internal";
        var hasNamespace = extension.Namespace.Length > 0;
        var indent = hasNamespace ? "    " : "";

        var enumLink = $"<see cref=\"{extension.EnumName}\"/>";

        sb.AppendLine();

        if (hasNamespace)
        {
            sb.AppendLine($"namespace {extension.Namespace}");
            sb.AppendLine("{");
        }

        var holder = $$"""
            /// <summary>
            /// Gives a value of {{enumLink}} its text, as in <c>status.{{LocalizedMembers.ExtensionName}}()</c>.
            /// </summary>
            {{GeneratedFile.ImportedCodeAttributes}}
            {{accessibility}} static class {{extension.HolderName}}
            {
                /// <param name="value">The value to give the text of.</param>
                extension({{extension.EnumName}} value)
                {
                    /// <summary>
                    /// The text of this value of {{enumLink}}, which the <c>[Localized]</c> of its member words.
                    /// </summary>
                    /// <remarks>
                    /// A value without such a member shows as <c>ToString()</c> writes it. That's a member that isn't marked, or a value that isn't a
                    /// member, such as a combination of flags.
                    /// </remarks>
                    {{accessibility}} string {{LocalizedMembers.ExtensionName}}() => {{TextsName}}{{index}}.Get(value);
                }
            }
            """;

        foreach (var line in holder.Split('\n'))
            sb.AppendLine(line.Length is 0 ? line : indent + line.TrimEnd('\r'));

        if (hasNamespace)
            sb.AppendLine("}");
    }

    /// <summary>
    /// The type's own text by name, and each member's by number.
    /// </summary>
    private static string GetFieldName(LocalizedText text, ref int number)
    {
        var name = text.MemberName is null ? OwnTextName : $"Text{number++}";
        return name;
    }
}
