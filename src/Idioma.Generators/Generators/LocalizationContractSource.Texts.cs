namespace CodeChops.Idioma.Generators;

/// <inheritdoc cref="LocalizationContractSource"/>
internal static partial class LocalizationContractSource
{
    /// <summary>
    /// Writes the generated class with its texts. For an enum's texts, it adds the switch to <paramref name="lookups"/>.
    /// </summary>
    private static void AppendTexts(StringBuilder sb, StringBuilder lookups, ContractFile file)
    {
        var contract = file.Contract;
        var sets = file.Sets;
        var className = file.ClassName;
        var name = file.ContractName;
        var textsName = file.TextsName;
        var texts = file.Texts;
        var accessibility = file.Accessibility;
        var sampleMember = GetSampleText(texts);

        sb.AppendLine($$"""
            /// <summary>
            /// The texts of <see cref="{{className}}"/>, one <see cref="string"/> per text.
            /// </summary>
            /// <remarks>
            /// Each text gives the wording for the current UI culture at the moment it's read, so read it where you show it.
            /// <c>@{{contract.TextsName}}.{{sampleMember}}</c> in markup shows the wording of the first of these:
            /// <list type="number">
            /// <item><description>The <see cref="{{name}}"/> set registered for the current UI culture.</description></item>
            /// <item><description>The set registered for the app's <see cref="{{PackageNames.LocalizationRegistry}}.DefaultLanguage"/>.</description></item>
            /// <item><description>The set registered for the language of <see cref="{{className}}"/>.</description></item>
            /// <item><description>The wording in <see cref="{{className}}"/>.</description></item>
            /// </list>
            /// </remarks>
            {{accessibility}} static partial class {{textsName}}
            {
            """);

        // Nothing but the texts, so a text can take any name but those of LOC300. Positions are literals, because a
        // constant would take a name from the texts.
        for (var i = 0; i < texts.Length; i++)
        {
            var member = texts[i];
            var memberName = CodeEscaping.EscapeIdentifier(member.Name);
            var nameLiteral = SymbolDisplay.FormatLiteral(member.Name, quote: true);

            if (i > 0)
                sb.AppendLine();

            ContractDocComments.AppendMemberDocs(sb, contract, sets, className, member);

            sb.AppendLine($"    {GeneratedFile.ImportedCodeAttributes}");

            // [Obsolete] as the set has it, so code that shows the text is told.
            if (member.Marks is { } marks)
                sb.AppendLine($"    {marks}");

            // Worded on every read. GetLocalizationSet gives the cached set of the user's language, and else
            // ResolveWording finds the wording in the fallback order. Both take a type argument, because a name with
            // one isn't looked up among the texts, so no text can hide them.
            if (!member.IsMethod)
            {
                sb.AppendLine($"    public static {(HidesObjectMember(member) ? "new " : "")}string {memberName} => GetLocalizationSet<{name}>()?.{memberName} ?? ResolveWording<{name}, {file.DefaultSet}>({nameLiteral}, static set => set.{memberName});");
                continue;
            }

            // The fallback path takes a static function and the values, so one function serves every call. Several
            // values go as a tuple.
            var arguments = String.Join(", ", member.Parameters.Select(static parameter => CodeEscaping.EscapeIdentifier(parameter.Name)));
            var set = GetNameApartFrom(member, "set");
            string valuesType, selector, values;

            if (member.Parameters.Length is 1)
            {
                valuesType = ContractTypeNames.RenderType(member.Parameters[0].Type, contract.Namespace);
                selector = $"static ({set}, {arguments}) => {set}.{memberName}({arguments})";
                values = arguments;
            }
            else
            {
                var tuple = GetNameApartFrom(member, "values");
                var items = String.Join(", ", member.Parameters.Select((_, index) => $"{tuple}.Item{index + 1}"));

                valuesType = $"({String.Join(", ", member.Parameters.Select(parameter => ContractTypeNames.RenderType(parameter.Type, contract.Namespace)))})";
                selector = $"static ({set}, {tuple}) => {set}.{memberName}({items})";
                values = $"({arguments})";
            }

            sb.AppendLine($"    public static {(HidesObjectMember(member) ? "new " : "")}string {memberName}({ContractTypeNames.RenderParameters(member, contract.Namespace)}) => GetLocalizationSetForValues<{name}>()?.{memberName}({arguments}) ?? ResolveWording<{name}, {file.DefaultSet}, {valuesType}>({nameLiteral}, {selector}, {values});");
        }

        foreach (var @enum in contract.Enums)
            AppendLookup(sb, lookups, file, @enum);

        AppendGet(sb, file);

        // No static field creates the default-language set here, because a throwing constructor would fail the class
        // for good. The package creates it when a text first needs it.
        sb.AppendLine("}");
    }

    /// <summary>
    /// Writes <c>GetTextByValue</c> and the extension <c>GetText()</c> for an enum. A value that's no member, such as a
    /// combination of flags, shows as <c>ToString()</c> writes it.
    /// </summary>
    // Of two members of one value, the first gives its text. A member with a reserved name (LOC300) has no arm.
    private static void AppendLookup(StringBuilder sb, StringBuilder lookups, ContractFile file, LocalizedEnum @enum)
    {
        var contract = file.Contract;
        var name = file.ContractName;
        var textsName = file.TextsName;
        var texts = file.Texts;

        var type = ContractTypeNames.RenderType(@enum.Type, contract.Namespace);
        var properties = new HashSet<string>(texts.Where(static text => !text.IsMethod).Select(static text => text.Name), StringComparer.Ordinal);
        var members = @enum.Members.Where(member => member.IsFirstOfItsValue && properties.Contains(member.Name)).ToArray();

        // A cref can't hold a type with type arguments, such as an enum inside a generic class.
        var enumLink = type.Contains("<") ? $"<c>{CodeEscaping.EscapeXml(type)}</c>" : $"<see cref=\"{type}\"/>";
        var example = members.Length > 0
            ? $", such as <see cref=\"{CodeEscaping.EscapeIdentifier(members[0].Name)}\"/> for <c>{CodeEscaping.EscapeXml(@enum.Name)}.{members[0].Name}</c>"
            : "";

        // The method by its parameter, because a set of two enums has two of that name.
        var lookupLink = type.Contains("<")
            ? $"<c>{LocalizationSetsAnalysis.LookupName}</c>"
            : $"<see cref=\"{LocalizationSetsAnalysis.LookupName}({type})\"/>";

        sb.AppendLine();
        sb.AppendLine($$"""
                /// <summary>
                /// The text of a value of {{enumLink}}{{example}}.
                /// </summary>
                /// <remarks>
                /// A value that isn't a member of the enum, such as a combination of flags, shows as <c>ToString()</c> writes it.
                /// </remarks>
                {{GeneratedFile.ImportedCodeAttributes}}
                {{(@enum.IsPublic ? "public" : "internal")}} static string {{LocalizationSetsAnalysis.LookupName}}({{type}} value) => TextsByValue<{{name}}>.Get(value);
            """);

        // No GetText() where the enum's assembly has one already (AppendEnumTextsRegistration), because a second would
        // make calls ambiguous (CS0121). For any other enum of another assembly, such as DayOfWeek, it's internal for
        // the same reason.
        if (@enum.GetText is not GetTextAccessibility.None)
        {
            var extensionAccessibility = @enum.GetText is GetTextAccessibility.Public ? "public" : "internal";

            sb.AppendLine($$"""

                    /// <param name="value">The value to give the text of.</param>
                    extension({{type}} value)
                    {
                        /// <summary>
                        /// The text of this value of {{enumLink}}, as in <c>status.{{LocalizationSetsAnalysis.ExtensionName}}()</c>. It's the text {{lookupLink}} gives.
                        /// </summary>
                        /// <remarks>
                        /// A value that isn't a member of the enum, such as a combination of flags, shows as <c>ToString()</c> writes it.
                        /// </remarks>
                        {{GeneratedFile.ImportedCodeAttributes}}
                        {{extensionAccessibility}} string {{LocalizationSetsAnalysis.ExtensionName}}() => TextsByValue<{{name}}>.Get(value);
                    }
                """);
        }

        var arms = new StringBuilder();

        foreach (var member in members)
        {
            arms.AppendLine($"        {type}.{CodeEscaping.EscapeIdentifier(member.Name)} => {NameText(file, texts.First(text => String.Equals(text.Name, member.Name, StringComparison.Ordinal)))},");
        }

        if (lookups.Length > 0)
            lookups.AppendLine();

        lookups.AppendLine($$"""
                internal static string Get({{type}} value) => value switch
                {
            """);
        lookups.Append(arms);
        lookups.AppendLine("""
                    _ => value.ToString(),
                };
            """);

        // Registered with the enum's assembly (AppendEnumTextsRegistration). A value without a text here gives null, so
        // GetText() shows the member's [Localized] wording.
        if (@enum.HasTextsOfItsAssembly)
        {
            lookups.AppendLine($$"""

                    internal static string? {{FindName}}({{type}} value) => value switch
                    {
                """);
            lookups.Append(arms);
            lookups.AppendLine("""
                        _ => null,
                    };
                """);
        }
    }

    /// <summary>
    /// The method of <c>TextsByValue</c> that gives <see langword="null"/> for a value without a text.
    /// </summary>
    private const string FindName = "Find";

    /// <summary>
    /// Writes <c>GetTextByKey</c> and <c>TextKeys</c>, such as
    /// <c>ShopLocalization.GetTextByKey("Greeting", customer.Name)</c>.
    /// </summary>
    // Texts are qualified by their class, because the parameters key and values would hide a text of that name. The
    // keys are in TextKeysOf, because a static field here would give every text read an initializer check.
    private static void AppendGet(StringBuilder sb, ContractFile file)
    {
        var contract = file.Contract;
        var name = file.ContractName;
        var texts = file.Texts;

        var example = texts.Length > 0 ? $", such as <c>{CodeEscaping.EscapeXml(SymbolDisplay.FormatLiteral(texts[0].Name, quote: true))}</c>" : "";

        sb.AppendLine();
        sb.AppendLine($$"""
                /// <summary>
                /// The text with a key, or <see langword="null"/> for an unknown key. The key is the name of the text{{example}}.
                /// </summary>
                /// <remarks>
                /// A key may be one your code only has at run time, such as one from a database. <see cref="{{LocalizationSetsAnalysis.KeysName}}"/> lists every key.
                /// </remarks>
                /// <param name="key">The key of the text: its name, in the same letter case.</param>
                /// <param name="values">The values of a text that takes them, in the order of its parameters. A value isn't converted to the type of its parameter, and only a nullable parameter takes <see langword="null"/>.</param>
                /// <exception cref="ArgumentNullException">When <paramref name="key"/> is <see langword="null"/>.</exception>
                /// <exception cref="ArgumentException">When <paramref name="values"/> doesn't hold one value of the right type for each parameter of the text.</exception>
                {{GeneratedFile.ImportedCodeAttributes}}
                public static string? {{LocalizationSetsAnalysis.GetName}}(string key, params ReadOnlySpan<object?> values) => key switch
                {
            """);

        foreach (var member in texts)
        {
            var keyLiteral = SymbolDisplay.FormatLiteral(member.Name, quote: true);
            var text = NameText(file, member);

            if (!member.IsMethod)
            {
                sb.AppendLine($"        {keyLiteral} => ResolveKeyedWording<{name}>({text}, key, values),");
                continue;
            }

            var count = member.Parameters.Length;
            var arguments = String.Join(", ", member.Parameters.Select((parameter, index)
                => $"GetKeyedValue<{ContractTypeNames.RenderType(parameter.Type, contract.Namespace)}>(key, values, {index}, {count}, {SymbolDisplay.FormatLiteral(parameter.Name, quote: true)}, acceptsNull: {(parameter.AcceptsNull ? "true" : "false")})"));

            sb.AppendLine($"        {keyLiteral} => {text}({arguments}),");
        }

        // A null key is a mistake in the call, so it throws, while an unknown key gives null.
        sb.AppendLine($$"""
                    null => throw CreateNullKeyException<{{name}}>(key),
                    _ => null,
                };

                /// <summary>
                /// The key of every text, sorted by name in ordinal order. <see cref="{{LocalizationSetsAnalysis.GetName}}"/> takes each of them.
                /// </summary>
                {{GeneratedFile.ImportedCodeAttributes}}
                public static ImmutableArray<string> {{LocalizationSetsAnalysis.KeysName}} => {{TextKeysName}}<{{name}}>.Keys;
            """);
    }

    /// <summary>
    /// Registers this set's texts for an enum of another assembly whose members are <c>[Localized]</c>, so that
    /// assembly's <c>GetText()</c> gives them.
    /// </summary>
    // A module initializer, so it runs before any code of this assembly. Not generic, which a module initializer can't
    // be, but nothing names it, so no text can hide it.
    private static void AppendEnumTextsRegistration(StringBuilder sb, ContractFile file)
    {
        var contract = file.Contract;

        sb.AppendLine($$"""

            /// <summary>
            /// Gives the values of each enum of another assembly whose members are marked <c>[Localized]</c> there the texts of this set.
            /// The <c>GetText()</c> of that assembly then gives them.
            /// </summary>
            {{GeneratedFile.ImportedCodeAttributes}}
            file static class EnumTextsOfOtherAssemblies
            {
                [System.Runtime.CompilerServices.ModuleInitializer]
                internal static void Register()
                {
            """);

        foreach (var @enum in contract.Enums.Where(static @enum => @enum.HasTextsOfItsAssembly))
            sb.AppendLine($"        RegisterEnumTexts<{ContractTypeNames.RenderType(@enum.Type, contract.Namespace)}, {file.ContractName}>(TextsByValue<{file.ContractName}>.{FindName});");

        sb.AppendLine("""
                }
            }
            """);
    }

    private const string TextKeysName = "TextKeysOf";

    /// <summary>
    /// Writes the class behind <c>TextKeys</c>. Generic, so no text can hide it.
    /// </summary>
    private static void AppendTextKeys(StringBuilder sb, ContractFile file)
    {
        // Sorted ordinally, so a binary search finds a key.
        var keys = String.Join(", ", file.Texts
            .Select(static text => text.Name)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .Select(static name => SymbolDisplay.FormatLiteral(name, quote: true)));

        sb.AppendLine($$"""

            {{GeneratedFile.ImportedCodeAttributes}}
            file static class {{TextKeysName}}<TContract>
            {
                internal static readonly ImmutableArray<string> Keys = [{{keys}}];
            }
            """);
    }

    /// <summary>
    /// A text qualified by its class.
    /// </summary>
    private static string NameText(ContractFile file, LocalizationMember member)
    {
        var text = $"{file.TextsName}.{CodeEscaping.EscapeIdentifier(member.Name)}";
        return text;
    }

    /// <summary>
    /// Whether a text hides a member of <see cref="object"/>, such as <c>Equals</c>, so it needs <see langword="new"/>
    /// (<c>CS0108</c>). A method hides one only with the same parameters.
    /// </summary>
    private static bool HidesObjectMember(LocalizationMember member)
    {
        if (!member.IsMethod)
            return member.Name is "Equals" or "GetHashCode" or "GetType" or "MemberwiseClone" or "ReferenceEquals" or "ToString";

        var hidesObjectMember = member.Parameters.All(static parameter => parameter.Type is [{ Namespace: null, Text: "object" or "object?" }])
               && member is { Name: "Equals", Parameters.Length: 1 or 2 } or { Name: "ReferenceEquals", Parameters.Length: 2 };

        return hidesObjectMember;
    }

    /// <summary>
    /// The example text in the generated class's doc comment: the first text without values, and else the first text.
    /// </summary>
    private static string GetSampleText(LocalizationMember[] texts)
    {
        foreach (var text in texts)
            if (!text.IsMethod)
                return text.Name;

        var sample = texts.Length > 0 ? $"{texts[0].Name}(...)" : "Name";
        return sample;
    }

    /// <summary>
    /// The name with underscores added until no parameter of the text has it.
    /// </summary>
    private static string GetNameApartFrom(LocalizationMember member, string name)
    {
        while (member.Parameters.Any(parameter => String.Equals(parameter.Name, name, StringComparison.Ordinal)))
            name += "_";

        return name;
    }
}
