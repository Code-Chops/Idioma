namespace CodeChops.Idioma.Generators;

internal static partial class LocalizationContractSource
{
    /// <summary>
    /// Writes the generated class with the texts of a contract, and the set they take their default wording from. For
    /// a set of the texts of an enum, it adds the switch that gives the text of a value to <paramref name="lookups"/>.
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

        // Nothing but the texts, so that a text can take any name but the class's own and those of the methods below (LOC300). Each text
        // passes its position among the texts and their number as literals: a constant for either would take a name
        // from the texts.
        for (var i = 0; i < texts.Length; i++)
        {
            var member = texts[i];
            var memberName = CodeEscaping.EscapeIdentifier(member.Name);
            var nameLiteral = SymbolDisplay.FormatLiteral(member.Name, quote: true);

            if (i > 0)
                sb.AppendLine();

            ContractDocComments.AppendMemberDocs(sb, contract, sets, className, member);

            // [Obsolete] and [Experimental] as the set has them, so that the code that shows the text is told.
            if (member.Marks is { } marks)
                sb.AppendLine($"    {marks}");

            // A text is worded on every read, by the set of the user's language. The package keeps that set per
            // culture and per position (FindSet), so a read is a call of the set's own member. Where no set is kept
            // yet, or the kept set gives no wording, the package finds the wording in the order the class's doc
            // comment gives (ResolveWording), which also creates the set of the default language when a text first
            // needs it. Both are called with a type argument, so that no text can stand before them: a name with a type
            // argument is not looked for among the texts.
            if (!member.IsMethod)
            {
                sb.AppendLine($"    public static {(HidesObjectMember(member) ? "new " : "")}string {memberName} => FindSet<{name}>({i}, {texts.Length})?.{memberName} ?? ResolveWording<{name}, {file.DefaultSet}>({i}, {texts.Length}, {nameLiteral}, static set => set.{memberName});");
                continue;
            }

            // A text that takes values hands them to the set's method. On the path that finds the wording, the set is
            // picked by a function that is handed the values and holds none itself: one function then serves every
            // call. Its parameters repeat the names of the method's, which they shadow, and the set is named apart
            // from them.
            var types = String.Join(", ", member.Parameters.Select(parameter => ContractTypeNames.RenderType(parameter.Type, contract.Namespace)));
            var arguments = String.Join(", ", member.Parameters.Select(static parameter => CodeEscaping.EscapeIdentifier(parameter.Name)));
            var set = GetNameApartFrom(member, "set");

            sb.AppendLine($"    public static {(HidesObjectMember(member) ? "new " : "")}string {memberName}({ContractTypeNames.RenderParameters(member, contract.Namespace)}) => FindSetForValues<{name}>({i}, {texts.Length})?.{memberName}({arguments}) ?? ResolveWording<{name}, {file.DefaultSet}, {types}>({i}, {texts.Length}, {nameLiteral}, static ({set}, {arguments}) => {set}.{memberName}({arguments}), {arguments});");
        }

        // The members that are no texts: for a set of the texts of an enum, the method that gives the text of a value,
        // and for every set, the method that gives a text by its key.
        foreach (var @enum in contract.Enums)
            AppendLookup(sb, lookups, file, @enum);

        AppendGet(sb, file);

        // The end of the texts. The set every text takes its default wording from is the package's to create, when a
        // text first needs it. Created here, as this class is first used, a set whose constructor throws would fail
        // the class for good, with an exception that names the class alone.
        sb.AppendLine("}");
    }

    /// <summary>
    /// Writes the method that gives the text of a value of an enum, for a set of the texts of that enum:
    /// <c>OrderStatusLocalization.GetTextByValue(order.Status)</c>. Each member of the enum gives the text named after it, and a
    /// value that is no member, such as a combination of flags, gives a text that shows it as <c>ToString()</c> does.
    /// Next to it, the extension method that gives a value its text itself, <c>order.Status.GetText()</c>, so that
    /// markup need not name the generated class.
    /// </summary>
    // Of two members of one value, the first gives its text: a switch takes a value once. A member whose text the
    // class doesn't hold, because of its name (LOC300), has no arm, and shows as a value that is no member.
    private static void AppendLookup(StringBuilder sb, StringBuilder lookups, ContractFile file, LocalizedEnum @enum)
    {
        var contract = file.Contract;
        var name = file.ContractName;
        var textsName = file.TextsName;
        var texts = file.Texts;

        var type = ContractTypeNames.RenderType(@enum.Type, contract.Namespace);
        var properties = new HashSet<string>(texts.Where(static text => !text.IsMethod).Select(static text => text.Name), StringComparer.Ordinal);
        var members = @enum.Members.Where(member => member.IsFirstOfItsValue && properties.Contains(member.Name)).ToArray();

        // A type with type arguments, as an enum inside a generic class has, is no name a cref can hold.
        var enumLink = type.Contains("<") ? $"<c>{CodeEscaping.EscapeXml(type)}</c>" : $"<see cref=\"{type}\"/>";
        var example = members.Length > 0
            ? $", such as <see cref=\"{CodeEscaping.EscapeIdentifier(members[0].Name)}\"/> for <c>{CodeEscaping.EscapeXml(@enum.Name)}.{members[0].Name}</c>"
            : "";

        // The method by its parameter, because a set of two enums has two of that name.
        var lookupLink = type.Contains("<")
            ? $"<c>{LocalizationSetsAnalysis.LookupName}</c>"
            : $"<see cref=\"{LocalizationSetsAnalysis.LookupName}({type})\"/>";

        // The switch stands in a class outside the generated one (TextsByValue), which is named with a type argument:
        // a name with a type argument is not looked for among the texts.
        sb.AppendLine();
        sb.AppendLine($$"""
                /// <summary>
                /// The text of a value of {{enumLink}}{{example}}.
                /// </summary>
                /// <remarks>
                /// A value that isn't a member of the enum, such as a combination of flags, shows as <c>ToString()</c> writes it.
                /// </remarks>
                {{(@enum.IsPublic ? "public" : "internal")}} static string {{LocalizationSetsAnalysis.LookupName}}({{type}} value) => TextsByValue<{{name}}>.Get(value);
            """);

        // An enum of another assembly that gets GetText() from the [Localized] of its members there: that GetText()
        // gives this set's texts once they're registered (AppendEnumTextsRegistration), and a second one here would make
        // every call that reaches both ambiguous (CS0121).
        if (!@enum.ReachesGetTextOfItsAssembly)
        {
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
                        {{(@enum.IsPublic ? "public" : "internal")}} string {{LocalizationSetsAnalysis.ExtensionName}}() => TextsByValue<{{name}}>.Get(value);
                    }
                """);
        }

        if (lookups.Length > 0)
            lookups.AppendLine();

        lookups.AppendLine($$"""
                internal static string Get({{type}} value) => value switch
                {
            """);

        // Each text by the name of its class in front, as it stands outside that class. A member marked [Obsolete] as an
        // error can't be named (CS0619), so its arm names its value, which a parenthesis keeps whole when it's negative.
        foreach (var member in members)
        {
            var pattern = member.ObsoleteAsErrorValue is { } value ? $"({type})({value})" : $"{type}.{CodeEscaping.EscapeIdentifier(member.Name)}";
            lookups.AppendLine($"        {pattern} => {NameText(file, texts.First(text => String.Equals(text.Name, member.Name, StringComparison.Ordinal)))},");
        }

        lookups.AppendLine($$"""
                    _ => value.ToString(),
                };
            """);
    }

    /// <summary>
    /// Writes the members that give a text by its key, which is its name:
    /// <c>ShopLocalization.GetTextByKey("Checkout")</c>, or
    /// <c>ShopLocalization.GetTextByKey("Greeting", customer.Name)</c> for a text that takes values. A key no text has
    /// throws there, and gives <see langword="false"/> from <c>TryGetTextByKey</c>. Values that aren't those the text
    /// takes throw from both. <c>TextKeys</c> lists every key.
    /// </summary>
    // Each text by the name of its class in front, because the parameters of the method stand before a text named
    // key, text or values. What checks the values is called with a type argument, which no text has. The framework's
    // types stand by their name alone, as the file imports their namespaces.
    //
    // One switch, in TryGetTextByKey, which GetTextByKey calls: two would double what a contract of many texts adds to
    // its assembly. The keys stand in a class outside the generated one (TextKeysOf). A static field of the generated
    // class itself would give that class an initializer, which every read of a text would have to check for.
    private static void AppendGet(StringBuilder sb, ContractFile file)
    {
        var contract = file.Contract;
        var name = file.ContractName;
        var textsName = file.TextsName;
        var texts = file.Texts;

        var example = texts.Length > 0 ? $", such as <c>{CodeEscaping.EscapeXml(SymbolDisplay.FormatLiteral(texts[0].Name, quote: true))}</c>" : "";
        var unknownKey = $"CreateUnknownKeyException<{name}>(key, {SymbolDisplay.FormatLiteral(contract.TextsName, quote: true)})";

        sb.AppendLine();
        sb.AppendLine($$"""
                /// <summary>
                /// The text with a key. The key is the name of the text{{example}}.
                /// </summary>
                /// <remarks>
                /// For a key that may be unknown, such as one from a database, use <see cref="{{LocalizationSetsAnalysis.TryGetName}}"/>.
                /// </remarks>
                /// <param name="key">The key of the text: its name, in the same letter case.</param>
                /// <param name="values">The values of a text that takes them, in the order of its parameters. A value isn't converted to the type of its parameter.</param>
                /// <exception cref="ArgumentNullException">When <paramref name="key"/> is <see langword="null"/>.</exception>
                /// <exception cref="KeyNotFoundException">When no text has the key.</exception>
                /// <exception cref="ArgumentException">When <paramref name="values"/> doesn't hold one value of the right type for each parameter of the text.</exception>
                public static string {{LocalizationSetsAnalysis.GetName}}(string key, params ReadOnlySpan<object?> values) => {{LocalizationSetsAnalysis.TryGetName}}(key, out var text, values) ? text : throw {{unknownKey}};

                /// <summary>
                /// Gives the text with a key, when a text has that key. The key is the name of the text{{example}}.
                /// </summary>
                /// <param name="key">The key of the text: its name, in the same letter case.</param>
                /// <param name="text">The text, or <see langword="null"/> when no text has the key.</param>
                /// <param name="values">The values of a text that takes them, in the order of its parameters. A value isn't converted to the type of its parameter.</param>
                /// <returns><see langword="true"/> when a text has the key.</returns>
                /// <exception cref="ArgumentNullException">When <paramref name="key"/> is <see langword="null"/>.</exception>
                /// <exception cref="ArgumentException">When <paramref name="values"/> doesn't hold one value of the right type for each parameter of the text.</exception>
                public static bool {{LocalizationSetsAnalysis.TryGetName}}(string key, [NotNullWhen(true)] out string? text, params ReadOnlySpan<object?> values)
                {
                    text = key switch
                    {
            """);

        foreach (var member in texts)
        {
            var keyLiteral = SymbolDisplay.FormatLiteral(member.Name, quote: true);
            var text = NameText(file, member);

            if (!member.IsMethod)
            {
                sb.AppendLine($"            {keyLiteral} => ResolveKeyedWording<{name}>({text}, key, values),");
                continue;
            }

            var count = member.Parameters.Length;
            var arguments = String.Join(", ", member.Parameters.Select((parameter, index)
                => $"GetKeyedValue<{ContractTypeNames.RenderType(parameter.Type, contract.Namespace)}>(key, values, {index}, {count}, {SymbolDisplay.FormatLiteral(parameter.Name, quote: true)})"));

            sb.AppendLine($"            {keyLiteral} => {text}({arguments}),");
        }

        // A key that is null throws from both methods, as a key that isn't one.
        sb.AppendLine($$"""
                        null => throw {{unknownKey}},
                        _ => null,
                    };

                    return text is not null;
                }

                /// <summary>
                /// The key of every text, sorted by name in ordinal order. <see cref="{{LocalizationSetsAnalysis.GetName}}"/> takes each of them.
                /// </summary>
                public static ImmutableArray<string> {{LocalizationSetsAnalysis.KeysName}} => {{TextKeysName}}<{{name}}>.Keys;
            """);
    }

    /// <summary>
    /// Writes what gives the values of an enum of another assembly the texts of this set, where that assembly gives the
    /// enum its <c>GetText()</c> from the <c>[Localized]</c> of its members. That <c>GetText()</c> then gives this set's
    /// wording, in this app's languages.
    /// </summary>
    // A module initializer, which runs before any code of this assembly does: also when an app calls this assembly's
    // registrar as it starts, as it does for every assembly it references. File-local, so that it's no type of the
    // assembly, and not generic, which a module initializer can't be. Nothing names it, so no text can stand before it.
    private static void AppendEnumTextsRegistration(StringBuilder sb, ContractFile file)
    {
        var contract = file.Contract;

        sb.AppendLine("""

            /// <summary>
            /// Gives the values of each enum of another assembly whose members are marked <c>[Localized]</c> there the texts of this set.
            /// The <c>GetText()</c> of that assembly then gives them.
            /// </summary>
            file static class EnumTextsOfOtherAssemblies
            {
                [System.Runtime.CompilerServices.ModuleInitializer]
                internal static void Register()
                {
            """);

        foreach (var @enum in contract.Enums.Where(static @enum => @enum.HasTextsOfItsAssembly))
            sb.AppendLine($"        RegisterEnumTexts<{ContractTypeNames.RenderType(@enum.Type, contract.Namespace)}>({file.TextsName}.{LocalizationSetsAnalysis.LookupName});");

        sb.AppendLine("""
                }
            }
            """);
    }

    /// <summary>
    /// The class that holds the keys of the texts.
    /// </summary>
    private const string TextKeysName = "TextKeysOf";

    /// <summary>
    /// Writes the class that holds the key of every text, which <c>TextKeys</c> of the generated class gives. File-local,
    /// so that it's no type of the assembly. Generic, so that no text can stand before it: a name with a type argument
    /// is not looked for among the texts.
    /// </summary>
    private static void AppendTextKeys(StringBuilder sb, ContractFile file)
    {
        // Sorted as StringComparer.Ordinal sorts them, so that a search such as ImmutableArray.BinarySearch finds a key.
        var keys = String.Join(", ", file.Texts
            .Select(static text => text.Name)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .Select(static name => SymbolDisplay.FormatLiteral(name, quote: true)));

        sb.AppendLine($$"""

            file static class {{TextKeysName}}<TContract>
            {
                internal static readonly ImmutableArray<string> Keys = [{{keys}}];
            }
            """);
    }

    /// <summary>
    /// A text as the file names it outside its member, by the name of its class in front. A text marked
    /// <c>[Obsolete]</c> as an error can't be named there (<c>CS0619</c>), so it's named through the class that hands
    /// it on (<see cref="AppendObsoleteTexts"/>).
    /// </summary>
    private static string NameText(ContractFile file, LocalizationMember member)
    {
        var holder = member.IsObsoleteAsError ? $"{ObsoleteTextsName}<{file.ContractName}>" : file.TextsName;

        var text = $"{holder}.{CodeEscaping.EscapeIdentifier(member.Name)}";
        return text;
    }

    /// <summary>
    /// The class that hands on the texts marked <c>[Obsolete]</c> as an error.
    /// </summary>
    private const string ObsoleteTextsName = "ObsoleteTexts";

    /// <summary>
    /// Writes the class that hands on each text marked <c>[Obsolete]</c> as an error, for the method that gives a text
    /// by its key and the one that gives the text of an enum value. Code inside a class marked <c>[Obsolete]</c> may name
    /// what's obsolete, and naming the class itself is a warning this file turns off (<c>CS0612</c>). File-local, so
    /// that it's no type of the assembly. Generic, so that no text can stand before it: a name with a type argument is
    /// not looked for among the texts.
    /// </summary>
    private static void AppendObsoleteTexts(StringBuilder sb, ContractFile file)
    {
        var contract = file.Contract;

        sb.AppendLine($$"""

            [Obsolete]
            file static class {{ObsoleteTextsName}}<TContract>
            {
            """);

        foreach (var member in file.Texts.Where(static text => text.IsObsoleteAsError))
        {
            var memberName = CodeEscaping.EscapeIdentifier(member.Name);

            if (!member.IsMethod)
            {
                sb.AppendLine($"    internal static string {memberName} => {file.TextsName}.{memberName};");
                continue;
            }

            var arguments = String.Join(", ", member.Parameters.Select(static parameter => CodeEscaping.EscapeIdentifier(parameter.Name)));
            sb.AppendLine($"    internal static string {memberName}({ContractTypeNames.RenderParameters(member, contract.Namespace)}) => {file.TextsName}.{memberName}({arguments});");
        }

        sb.AppendLine("}");
    }

    /// <summary>
    /// Whether a text carries the name of a member every class has, such as <c>Equals</c>, which the generated class
    /// then hides. Its member says so with <see langword="new"/>, because without it the build warns (<c>CS0108</c>)
    /// about code its author can't change. A text that takes values hides such a member only when it takes what that
    /// member takes: <c>Equals(object)</c>, <c>Equals(object, object)</c>, or <c>ReferenceEquals(object, object)</c>.
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
    /// The text the doc comment of the generated class shows as its example: the first text that takes no values, as
    /// it stands in markup by its name alone, and else the first text, with an ellipsis for its values.
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
    /// A name for something the generated method of a text declares itself, which none of the text's parameters
    /// carries: the name asked for, with an underscore for each parameter that took it.
    /// </summary>
    private static string GetNameApartFrom(LocalizationMember member, string name)
    {
        while (member.Parameters.Any(parameter => String.Equals(parameter.Name, name, StringComparison.Ordinal)))
            name += "_";

        return name;
    }
}
