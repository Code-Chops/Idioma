namespace CodeChops.Idioma.Generators;

/// <summary>
/// Writes the doc comments of the members of a contract's file: the summary of a text, and the link to its wording in
/// every language, with that wording next to it.
/// </summary>
internal static class ContractDocComments
{
    /// <summary>
    /// The line terminators of C#, the pair first so that it counts as one break.
    /// </summary>
    private static readonly string[] LineBreaks = ["\r\n", "\r", "\n", "\u0085", "\u2028", "\u2029"];

    /// <summary>
    /// The longest a wording stands next to its link, in characters. A tooltip lists every language, and a long wording
    /// in each would bury the rest.
    /// </summary>
    private const int MaxWordingLength = 80;

    /// <summary>
    /// The sets a contract links to, each with its texts by name (<see cref="IndexedSet"/>), in the order of
    /// <see cref="LocalizationContract.Sets"/>.
    /// </summary>
    public static IndexedSet[] IndexSets(LocalizationContract contract)
    {
        var setsPerLanguage = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var set in contract.Sets)
            setsPerLanguage[set.Language] = setsPerLanguage.TryGetValue(set.Language, out var count) ? count + 1 : 1;

        var sets = contract.Sets.Select(set => new IndexedSet(set, sharesLanguage: setsPerLanguage[set.Language] > 1)).ToArray();
        return sets;
    }

    /// <summary>
    /// A set a contract links to, with its texts by name. The file of a contract asks every set about every text, so
    /// a search through the set's texts for each would take the square of their number.
    /// </summary>
    public sealed class IndexedSet
    {
        public IndexedSet(SetLink link, bool sharesLanguage)
        {
            this.Link = link;
            this.SharesLanguage = sharesLanguage;
            this.DeclaredTexts = new HashSet<string>(link.DeclaredTexts, StringComparer.Ordinal);

            // The first class by a text's name is the nearest one declaring it.
            foreach (var inherited in link.InheritedTexts)
                if (!this.InheritedTexts.ContainsKey(inherited.Name))
                    this.InheritedTexts.Add(inherited.Name, inherited);

            foreach (var wording in link.Wordings)
                this.Wordings[wording.Name] = wording.Wording;
        }

        public SetLink Link { get; }

        /// <summary>
        /// Whether another set linked from the contract has the language of this one, as a set rewording its own
        /// language has. The links to both then name the set.
        /// </summary>
        public bool SharesLanguage { get; }

        public HashSet<string> DeclaredTexts { get; }

        public Dictionary<string, InheritedText> InheritedTexts { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, string> Wordings { get; } = new(StringComparer.Ordinal);
    }

    /// <summary>
    /// Where the wording a set gives a text is found: the set's own property or method when it declares the text, that
    /// of the nearest class declaring it when the set inherits the text (<c>ShopLocalizationEn.Checkout</c> for a
    /// British set that leaves <c>Checkout</c> as it is), and else the set itself.
    /// </summary>
    // A cref does not find a member through a class that only inherits it (CS1574), a text implemented explicitly has
    // no name to link to at all, and a method that shares its name with another has none of its own (CS0419).
    private static (string Namespace, string TypeName, bool HasMember, bool IsInherited) GetCrefTarget(IndexedSet set, string member)
    {
        if (set.DeclaredTexts.Contains(member))
            return (set.Link.Namespace, CodeEscaping.EscapeIdentifier(set.Link.Name), HasMember: true, IsInherited: false);

        if (set.InheritedTexts.TryGetValue(member, out var inherited))
            return (inherited.Namespace, inherited.TypeName, HasMember: true, IsInherited: true);

        return (set.Link.Namespace, CodeEscaping.EscapeIdentifier(set.Link.Name), HasMember: false, IsInherited: false);
    }

    /// <summary>
    /// The wording a set gives a text (<see cref="GetCrefTarget"/>), as the file of a contract names it: a class of the
    /// contract's own namespace by its name alone, as the file itself lies there, and a class of another namespace by
    /// its full name.
    /// </summary>
    private static string GetCref(string @namespace, IndexedSet set, string member)
    {
        var target = GetCrefTarget(set, member);
        var type = GetCrefType(@namespace, target.Namespace, target.TypeName);

        var cref = target.HasMember ? $"{type}.{CodeEscaping.EscapeIdentifier(member)}" : type;
        return cref;
    }

    /// <summary>
    /// A class as the file of a contract names it (<see cref="GetCref"/>).
    /// </summary>
    private static string GetCrefType(string @namespace, string typeNamespace, string typeName)
    {
        var type = ContractTypeNames.GetNamespacePrefix(@namespace, typeNamespace) + typeName;
        return type;
    }

    /// <summary>
    /// What follows the link to the wording a set gives a text: the wording, when the build can read it
    /// (<see cref="ConstantWording"/>, <see cref="MethodWording"/>), as one line of at most
    /// <see cref="MaxWordingLength"/> characters, then, in parentheses, the set when another set has the same
    /// language, as a set rewording its own language has, whether it is marked <c>[ExcludeFromLocalizationDiscovery]</c>,
    /// and the class the wording comes from when the set inherits it (<c>: Checkout (inherited from
    /// ShopLocalizationEn)</c> for a British set that leaves <c>Checkout</c> as it is).
    /// </summary>
    private static string GetWordingNote(string @namespace, IndexedSet set, string member)
    {
        set.Wordings.TryGetValue(member, out var wording);

        var details = new List<string>();

        if (set.SharesLanguage)
            details.Add($"<see cref=\"{GetCrefType(@namespace, set.Link.Namespace, CodeEscaping.EscapeIdentifier(set.Link.Name))}\"/>");

        if (set.Link.IsExcluded)
            details.Add("excluded from discovery");

        var target = GetCrefTarget(set, member);
        if (target.IsInherited && !String.IsNullOrEmpty(wording))
            details.Add($"inherited from <see cref=\"{GetCrefType(@namespace, target.Namespace, target.TypeName)}\"/>");

        var note = String.IsNullOrEmpty(wording) ? "" : $": {CodeEscaping.EscapeXml(Shorten(wording!))}";
        if (details.Count > 0)
            note += $" ({String.Join(", ", details)})";

        return note;
    }

    /// <summary>
    /// A wording as it stands next to its link: its first line, cut at <see cref="MaxWordingLength"/> characters, and
    /// an ellipsis where anything was left out.
    /// </summary>
    private static string Shorten(string wording)
    {
        var lines = wording.Split(LineBreaks, StringSplitOptions.None);
        var line = lines[0];
        var isCut = lines.Length > 1;

        if (line.Length > MaxWordingLength)
        {
            // Not between the halves of a surrogate pair, which would leave a character XML can't hold.
            var length = Char.IsHighSurrogate(line[MaxWordingLength - 1]) ? MaxWordingLength - 1 : MaxWordingLength;
            line = line.Substring(0, length);
            isCut = true;
        }

        var shortened = isCut ? line + "…" : line;
        return shortened;
    }

    /// <summary>
    /// Writes the doc comment of a generated member: the property's own comment when it has one
    /// (<c>&lt;inheritdoc cref/&gt;</c>), else its default-language wording, else a plain description, followed by a link
    /// to its wording in every language, with that wording next to it (<see cref="GetWordingNote"/>).
    /// </summary>
    public static void AppendMemberDocs(StringBuilder sb, LocalizationContract contract, IndexedSet[] sets, string className,
        LocalizationMember member)
    {
        if (member.HasDocComment)
        {
            sb.AppendLine($"    /// <inheritdoc cref=\"{className}.{CodeEscaping.EscapeIdentifier(member.Name)}\"/>");
        }
        else
        {
            sb.AppendLine("    /// <summary>");
            if (member.DefaultText is { } text)
            {
                // A multi-line wording stays a valid doc comment: one /// line per line of text, split at every line
                // break C# knows, since any of them would end the comment and leave the rest as code.
                foreach (var line in CodeEscaping.EscapeXml(text).Split(LineBreaks, StringSplitOptions.None))
                    sb.AppendLine($"    /// {line}");
            }
            else
            {
                sb.AppendLine($"    /// The <c>{member.Name}</c> text of <see cref=\"{className}\"/>.");
            }
            sb.AppendLine("    /// </summary>");
        }

        sb.AppendLine("""
                /// <remarks>
                /// Wording:
                /// <list type="bullet">
            """);
        foreach (var set in sets)
        {
            var cref = GetCref(contract.Namespace, set, member.Name);
            var note = GetWordingNote(contract.Namespace, set, member.Name);

            sb.AppendLine($"    /// <item><description><see cref=\"{cref}\">{set.Link.Language}</see>{note}</description></item>");
        }
        sb.AppendLine("""
                /// </list>
                /// </remarks>
            """);
    }
}
