using System.Reflection;

namespace CodeChops.Idioma;

/// <summary>
/// Finds the texts of members marked <c>[Localized]</c>: a property, a field, an enum member, or a type. Each assembly
/// records one function that gives a member's wording per language, and this class keeps what those functions find.
/// <see cref="LocalizableText.GetTextByMember{T}(string)"/> and <see cref="LocalizableText.GetTextOfType{T}()"/> read
/// from it.
/// </summary>
internal static class MemberTexts
{
    /// <summary>
    /// What finds a member's text: one function per assembly that marks members <c>[Localized]</c>, and the texts
    /// found so far. Replaced as a whole when an assembly records its function, so that a member looked up before
    /// that isn't kept as one without a text.
    /// </summary>
    private static MemberTextSources Sources = new([]);

    /// <summary>
    /// The texts of the type this thread asked for last by a <see cref="Type"/>. A form asks for the labels of one
    /// model's properties one after another, so most lookups by a type find theirs here.
    /// </summary>
    // Per thread, because threads of a server render forms of different models at once. Shared by them, every lookup
    // of another type wrote the field the others read, and a label took 63 to 103 ns where one thread takes 6 to 12 ns
    // (8 threads, each asking for its own model). A thread keeps the texts of its last type until it asks for another.
    [ThreadStatic]
    private static TypeTexts? LastTextsByType;

    /// <summary>
    /// The wording of a member of a type the code names, for the current UI culture, or <see langword="null"/> for a
    /// member without a text.
    /// </summary>
    public static string? Find<T>(string memberName)
    {
        var text = FindTextsOf<T>().Find(memberName)?.Word();
        return text;
    }

    /// <summary>
    /// The wording of a member of a type the code holds, for the current UI culture, or <see langword="null"/> for a
    /// member without a text.
    /// </summary>
    public static string? Find(Type type, string memberName)
    {
        var text = FindTextsOf(type).Find(memberName)?.Word();
        return text;
    }

    /// <summary>
    /// The wording of a type itself, for a type the code names, or <see langword="null"/> for a type without a text.
    /// </summary>
    public static string? FindOwn<T>()
    {
        var text = FindTextsOf<T>().FindOwn()?.Word();
        return text;
    }

    /// <summary>
    /// The wording of a type itself, for a type the code holds, or <see langword="null"/> for a type without a text.
    /// </summary>
    public static string? FindOwn(Type type)
    {
        var text = FindTextsOf(type).FindOwn()?.Word();
        return text;
    }

    /// <summary>
    /// Adds the function that finds the texts of an assembly's members.
    /// <see cref="Internals.GeneratedCode.RecordMemberTexts"/> calls this when the assembly registers.
    /// </summary>
    public static void Record(string[] languages, Func<Type, string?, string[]?> find)
    {
        ArgumentNullException.ThrowIfNull(languages);
        ArgumentNullException.ThrowIfNull(find);

        var finder = new MemberTextFinder(languages, find);

        // A new whole, without the texts found so far: a member looked up before its assembly registered was kept as
        // one without a text. A lookup that started on the old whole keeps what it finds there, where no one reads it.
        MemberTextSources current, next;

        do
        {
            current = Volatile.Read(ref Sources);
            next = new MemberTextSources([.. current.Finders, finder]);
        }
        while (!ReferenceEquals(Interlocked.CompareExchange(ref Sources, next, current), current));
    }

    /// <summary>
    /// The texts of a type the code names. Kept per type, so that asking again is a read of a field.
    /// </summary>
    private static TypeTexts FindTextsOf<T>()
    {
        var texts = TextsOf<T>.Value;

        // Found again when an assembly recorded its texts since: what was found before may lack one of them.
        if (texts is null || !ReferenceEquals(texts.Sources, Volatile.Read(ref Sources)))
            TextsOf<T>.Value = texts = FindTextsOf(typeof(T));

        return texts;
    }

    /// <summary>
    /// The texts of a type the code holds.
    /// </summary>
    private static TypeTexts FindTextsOf(Type type)
    {
        var sources = Volatile.Read(ref Sources);
        var last = LastTextsByType;

        if (last is not null && ReferenceEquals(last.Type, type) && ReferenceEquals(last.Sources, sources))
            return last;

        // A type found here is kept already, so the field holds on to nothing more. A type that can be unloaded is
        // never kept, and so never found here.
        if (sources.TextsByType.TryGetValue(type, out var texts))
        {
            LastTextsByType = texts;
            return texts;
        }

        texts = sources.AddTextsOf(type);
        return texts;
    }

    /// <summary>
    /// The texts of <typeparamref name="T"/>, once they were asked for by that type.
    /// </summary>
    private static class TextsOf<T>
    {
        public static TypeTexts? Value;
    }

    /// <summary>
    /// The function that finds the texts of one assembly's members, with the languages of that assembly, the default
    /// first.
    /// </summary>
    private sealed class MemberTextFinder(string[] languages, Func<Type, string?, string[]?> find)
    {
        public string[] Languages { get; } = languages;

        /// <summary>
        /// Gives the wording of a member per language, in the order of <see cref="Languages"/>, or
        /// <see langword="null"/> for what has no text.
        /// </summary>
        public Func<Type, string?, string[]?> Find { get; } = find;
    }

    /// <summary>
    /// The text of one member: its wording per language of its assembly.
    /// </summary>
    private sealed class MemberText(string[] languages, string[] wordings)
    {
        /// <summary>
        /// The wording for the current UI culture.
        /// </summary>
        public string Word()
        {
            var index = InlineLanguage.Find(languages);

            // A wording per language, as the generator writes them. Fewer would be code written by hand.
            var wording = wordings[index < wordings.Length ? index : 0];
            return wording;
        }
    }

    /// <summary>
    /// The functions that find a member's text, and the texts found so far, by type.
    /// </summary>
    private sealed class MemberTextSources(MemberTextFinder[] finders)
    {
        public MemberTextFinder[] Finders { get; } = finders;

        /// <summary>
        /// The texts found for each type that was asked for. A type with type arguments, such as
        /// <c>Page&lt;Order&gt;</c>, has those of its definition.
        /// </summary>
        public ConcurrentDictionary<Type, TypeTexts> TextsByType { get; } = new();

        /// <summary>
        /// Asks every function for the member of the type, and then of each class it derives from. A type's own text
        /// is asked of the type alone.
        /// </summary>
        public MemberText? Find(Type type, string? member)
        {
            for (var current = type; current is not null && current != typeof(object); current = member is null ? null : current.BaseType)
            {
                var definition = current.IsConstructedGenericType ? current.GetGenericTypeDefinition() : current;

                foreach (var finder in this.Finders)
                    if (finder.Find(definition, member) is { Length: > 0 } wordings)
                        return new MemberText(finder.Languages, wordings);
            }

            return null;
        }

        /// <summary>
        /// The texts of a type that wasn't asked for before.
        /// </summary>
        public TypeTexts AddTextsOf(Type type)
        {
            // A type with type arguments by its definition, which is the type the attribute is written in.
            var definition = type.IsConstructedGenericType ? type.GetGenericTypeDefinition() : type;

            // A type that can be unloaded isn't kept, because the texts found would hold on to it.
            if (type.IsCollectible)
                return new TypeTexts(this, definition);

            var texts = this.TextsByType.GetOrAdd(definition, static (definition, sources) => new TypeTexts(sources, definition), this);

            if (!ReferenceEquals(definition, type))
                this.TextsByType.TryAdd(type, texts);

            return texts;
        }
    }

    /// <summary>
    /// The texts found so far for the members of one type, and for the type itself, a member without a text
    /// included.
    /// </summary>
    /// <remarks>
    /// A name is looked up without hashing all of it. Its place comes from its length and three of its characters,
    /// and it's compared as an object first. A name written as <c>nameof(Address.City)</c> is the same string on
    /// every call, and so is the name a <see cref="PropertyInfo"/> gives, so nearly every lookup ends at that
    /// comparison. Hashing the whole name took most of the time of a lookup in WebAssembly, where the runtime's own
    /// hash is interpreted.
    /// </remarks>
    private sealed class TypeTexts(MemberTextSources sources, Type type)
    {
        /// <summary>
        /// How many names are kept at most. A caller that asks for names it makes up, such as a user's input, would
        /// otherwise let the names grow without end. A name beyond these is found anew each time.
        /// </summary>
        private const int MaxKept = 256;

        public MemberTextSources Sources { get; } = sources;
        public Type Type { get; } = type;

        // Replaced as a whole when a name is added, so that a reader sees the names before it or after it, and never
        // half of them. Two threads that add a name at once may lose one, which is then found anew.
        private volatile MemberTable _table = MemberTable.Empty;

        private MemberText? _ownText;
        private volatile bool _hasOwnText;

        /// <summary>
        /// The text of the type itself.
        /// </summary>
        public MemberText? FindOwn()
        {
            if (this._hasOwnText)
                return this._ownText;

            var text = this.Sources.Find(this.Type, member: null);

            this._ownText = text;
            this._hasOwnText = true;

            return text;
        }

        /// <summary>
        /// The text of a member, by its name.
        /// </summary>
        public MemberText? Find(string member)
        {
            var kept = this._table;
            var names = kept.Names;
            var mask = names.Length - 1;

            // Written out here, because a call costs more than this in WebAssembly's interpreter. MemberTable.Hash is
            // the same.
            var length = member.Length;
            var hash = length is 0 ? 0 : (length * 31) + (member[0] * 7) + (member[length / 2] * 3) + member[length - 1];

            // The table is never full, so the search ends at an empty place.
            for (var place = hash & mask; names[place] is { } name; place = (place + 1) & mask)
                if (ReferenceEquals(name, member) || String.Equals(name, member, StringComparison.Ordinal))
                    return kept.Texts[place];

            var text = this.Sources.Find(this.Type, member);

            if (kept.Count < MaxKept)
                this._table = kept.With(member, text);

            return text;
        }
    }

    /// <summary>
    /// Names and the text of each, in places a name's length and three of its characters give. Never changed once
    /// made.
    /// </summary>
    private sealed class MemberTable
    {
        public static MemberTable Empty { get; } = new(new string?[8], new MemberText?[8], count: 0);

        // Two arrays, and not one of pairs: an array of a struct of two is code the runtime compiles when the app
        // starts, where arrays of objects are there already.
        public string?[] Names { get; }
        public MemberText?[] Texts { get; }
        public int Count { get; }

        private MemberTable(string?[] names, MemberText?[] texts, int count)
        {
            this.Names = names;
            this.Texts = texts;
            this.Count = count;
        }

        /// <summary>
        /// This table with one name more. At most half of its places are taken, so that a search is short and always
        /// ends.
        /// </summary>
        public MemberTable With(string name, MemberText? text)
        {
            var size = (this.Count + 1) * 2 > this.Names.Length ? this.Names.Length * 2 : this.Names.Length;
            var names = new string?[size];
            var texts = new MemberText?[size];

            for (var i = 0; i < this.Names.Length; i++)
                if (this.Names[i] is { } kept)
                    Put(names, texts, kept, this.Texts[i]);

            Put(names, texts, name, text);

            return new MemberTable(names, texts, this.Count + 1);
        }

        private static void Put(string?[] names, MemberText?[] texts, string name, MemberText? text)
        {
            var mask = names.Length - 1;
            var place = Hash(name) & mask;

            while (names[place] is not null)
                place = (place + 1) & mask;

            names[place] = name;
            texts[place] = text;
        }

        private static int Hash(string name)
        {
            var length = name.Length;

            var hash = length is 0 ? 0 : (length * 31) + (name[0] * 7) + (name[length / 2] * 3) + name[length - 1];
            return hash;
        }
    }
}
