using System.Reflection;
using System.Runtime.Loader;

namespace CodeChops.Idioma;

/// <summary>
/// Finds the texts of members marked <c>[Localized]</c>: a property, a field, an enum member, or a type. Each assembly
/// records one function that gives a member's wording per language. This class keeps what those functions find.
/// Every overload of <c>LocalizedText.GetTextByMember</c> and <c>LocalizedText.GetTextOfType</c> reads from it.
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
    public static string? Get<T>(string memberName)
    {
        var text = GetTextsOf<T>().Get(memberName)?.Word();
        return text;
    }

    /// <summary>
    /// The wording of a member of a type the code holds, for the current UI culture, or <see langword="null"/> for a
    /// member without a text.
    /// </summary>
    public static string? Get(Type type, string memberName)
    {
        var text = GetTextsOf(type).Get(memberName)?.Word();
        return text;
    }

    /// <summary>
    /// The wording of a type itself, for a type the code names, or <see langword="null"/> for a type without a text.
    /// </summary>
    public static string? GetOwn<T>()
    {
        var text = GetTextsOf<T>().GetOwn()?.Word();
        return text;
    }

    /// <summary>
    /// The wording of a type itself, for a type the code holds, or <see langword="null"/> for a type without a text.
    /// </summary>
    public static string? GetOwn(Type type)
    {
        var text = GetTextsOf(type).GetOwn()?.Word();
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
        CollectibleAssemblies.Watch(find.Method.Module.Assembly);

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
    /// Forgets the functions of the assemblies of a context that unloads (<see cref="CollectibleAssemblies"/>). Each
    /// is also emptied, because a thread may still hold the whole it was part of, through the texts of the last type it
    /// asked for.
    /// </summary>
    public static void Forget(AssemblyLoadContext context)
    {
        MemberTextSources current, next;

        do
        {
            current = Volatile.Read(ref Sources);
            var kept = current.Finders.Where(finder => !finder.IsOf(context)).ToArray();

            if (kept.Length == current.Finders.Length)
                return;

            next = new MemberTextSources(kept);
        }
        while (!ReferenceEquals(Interlocked.CompareExchange(ref Sources, next, current), current));

        foreach (var finder in current.Finders)
            if (finder.IsOf(context))
                finder.Forget();
    }

    /// <summary>
    /// The texts of a type the code names. Kept per type, so that asking again is a read of a field.
    /// </summary>
    private static TypeTexts GetTextsOf<T>()
    {
        var texts = TextsOf<T>.Value;

        // Found again when an assembly recorded its texts since: what was found before may lack one of them.
        if (texts is null || !ReferenceEquals(texts.Sources, Volatile.Read(ref Sources)))
            TextsOf<T>.Value = texts = GetTextsOf(typeof(T));

        return texts;
    }

    /// <summary>
    /// The texts of a type the code holds.
    /// </summary>
    private static TypeTexts GetTextsOf(Type type)
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

        // The field doesn't hold on to a type that can be unloaded either.
        if (!type.IsCollectible)
            LastTextsByType = texts;

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

        // Null once its assembly unloads, so that nothing here holds the assembly's code any more.
        private volatile Func<Type, string?, string[]?>? _find = find;

        /// <summary>
        /// Gives the wording of a member per language, in the order of <see cref="Languages"/>, or
        /// <see langword="null"/> for what has no text.
        /// </summary>
        public string[]? Get(Type type, string? member)
        {
            var wordings = this._find?.Invoke(type, member);
            return wordings;
        }

        /// <summary>
        /// Whether the function lies in an assembly of a context that unloads.
        /// </summary>
        public bool IsOf(AssemblyLoadContext context)
        {
            var isOf = this._find is { } find && CollectibleAssemblies.IsIn(find.Method.Module.Assembly, context);
            return isOf;
        }

        /// <summary>
        /// Lets go of the function, whose assembly unloads.
        /// </summary>
        public void Forget()
        {
            this._find = null;
        }
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
            var index = InlineLanguage.IndexOfShownLanguage(languages);

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
        /// <c>Page&lt;Order&gt;</c>, has those of its definition, <c>Page&lt;T&gt;</c>.
        /// </summary>
        public ConcurrentDictionary<Type, TypeTexts> TextsByType { get; } = new();

        /// <summary>
        /// Asks every function for the member of the type, and then of each class it derives from. A type's own text
        /// is asked of the type alone.
        /// </summary>
        public MemberText? Get(Type type, string? member)
        {
            for (var current = type; current is not null && current != typeof(object); current = member is null ? null : current.BaseType)
            {
                var definition = current.IsConstructedGenericType ? current.GetGenericTypeDefinition() : current;

                foreach (var finder in this.Finders)
                    if (finder.Get(definition, member) is { Length: > 0 } wordings)
                        return new MemberText(finder.Languages, wordings);
            }

            return null;
        }

        /// <summary>
        /// The texts of a type that wasn't asked for before.
        /// </summary>
        public TypeTexts AddTextsOf(Type type)
        {
            // A type that can be unloaded isn't kept, because the texts found would hold on to it.
            if (type.IsCollectible)
                return new TypeTexts(this, type);

            // A type with type arguments is kept as it's asked for, and not as its definition. A thread's last lookup
            // compares the type it was asked for, so it recognizes such a type only this way. Get finds the texts of
            // the definition.
            var texts = this.TextsByType.GetOrAdd(type, static (type, sources) => new TypeTexts(sources, type), this);
            return texts;
        }
    }

    /// <summary>
    /// The texts found so far for the members of one type, and for the type itself, a member without a text
    /// included.
    /// </summary>
    /// <remarks>
    /// A name is looked up without hashing all of it. Its place comes from its length and three of its characters.
    /// It's compared as an object first. Nearly every lookup ends at that comparison, because a name is nearly always
    /// the same string on every call: one written as <c>nameof(Address.City)</c>, and the name a
    /// <see cref="PropertyInfo"/> gives. Hashing the whole name took most of the time of a lookup in WebAssembly,
    /// where the runtime's own hash is interpreted.
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
        public MemberText? GetOwn()
        {
            if (this._hasOwnText)
                return this._ownText;

            var text = this.Sources.Get(this.Type, member: null);

            this._ownText = text;
            this._hasOwnText = true;

            return text;
        }

        /// <summary>
        /// The text of a member, by its name.
        /// </summary>
        public MemberText? Get(string member)
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

            var text = this.Sources.Get(this.Type, member);

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
