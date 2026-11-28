namespace CodeChops.Idioma.Generators;

/// <summary>
/// Writes the doc comments in a contract's file: a text's summary, and a link to its wording in every language.
/// </summary>
internal static class ContractDocComments
{
    /// <summary>
    /// The line terminators of C#, the pair first so it counts as one break.
    /// </summary>
    private static readonly string[] LineBreaks = ["\r\n", "\r", "\n", "\u0085", "\u2028", "\u2029"];

    /// <summary>
    /// The longest a wording stands next to its link, because a tooltip lists every language.
    /// </summary>
    private const int MaxWordingLength = 80;

    /// <summary>
    /// The sets a contract links to, in the order of <see cref="LocalizationContract.Sets"/>.
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
    /// A set a contract links to, with its texts by name, because every set is asked about every text.
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
        /// Whether another linked set has this language, as a rewording does. The links to both then name the set.
        /// </summary>
        public bool SharesLanguage { get; }

        public HashSet<string> DeclaredTexts { get; }

        public Dictionary<string, InheritedText> InheritedTexts { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, string> Wordings { get; } = new(StringComparer.Ordinal);
    }

    /// <summary>
    /// Where a set's wording of a text is: the set's own member, the nearest class that declares an inherited one, or
    /// else the set itself.
    /// </summary>
    // A cref doesn't find an inherited member (CS1574), an explicit implementation has no name, and an overloaded
    // method has no name of its own (CS0419).
    private static (string Namespace, string TypeName, bool HasMember, bool IsInherited) GetCrefTarget(IndexedSet set, string member)
    {
        if (set.DeclaredTexts.Contains(member))
            return (set.Link.Namespace, CodeEscaping.EscapeIdentifier(set.Link.Name), HasMember: true, IsInherited: false);

        if (set.InheritedTexts.TryGetValue(member, out var inherited))
            return (inherited.Namespace, inherited.TypeName, HasMember: true, IsInherited: true);

        return (set.Link.Namespace, CodeEscaping.EscapeIdentifier(set.Link.Name), HasMember: false, IsInherited: false);
    }

    /// <summary>
    /// The cref of a set's wording of a text (<see cref="GetCrefTarget"/>).
    /// </summary>
    private static string GetCref(string @namespace, IndexedSet set, string member)
    {
        var target = GetCrefTarget(set, member);
        var type = GetCrefType(@namespace, target.Namespace, target.TypeName);

        var cref = target.HasMember ? $"{type}.{CodeEscaping.EscapeIdentifier(member)}" : type;
        return cref;
    }

    /// <summary>
    /// A class as the contract's file names it.
    /// </summary>
    private static string GetCrefType(string @namespace, string typeNamespace, string typeName)
    {
        var type = ContractTypeNames.GetNamespacePrefix(@namespace, typeNamespace) + typeName;
        return type;
    }

    /// <summary>
    /// What follows the link to a wording: the wording when the build can read it, shortened, then details in
    /// parentheses, such as <c>: Checkout (inherited from ShopLocalizationEn)</c>.
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
    /// The first line, cut at <see cref="MaxWordingLength"/> characters, with an ellipsis where anything was left out.
    /// </summary>
    private static string Shorten(string wording)
    {
        var lines = wording.Split(LineBreaks, StringSplitOptions.None);
        var line = lines[0];
        var isCut = lines.Length > 1;

        if (line.Length > MaxWordingLength)
        {
            // Not inside a surrogate pair, which XML can't hold half of.
            var length = Char.IsHighSurrogate(line[MaxWordingLength - 1]) ? MaxWordingLength - 1 : MaxWordingLength;
            line = line.Substring(0, length);
            isCut = true;
        }

        var shortened = isCut ? line + "…" : line;
        return shortened;
    }

    /// <summary>
    /// Writes a generated member's doc comment: the property's own comment, else its default wording, else a plain
    /// description, then a link to its wording in every language.
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
                // One /// line per line of text, because any line break C# knows would end the comment.
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
