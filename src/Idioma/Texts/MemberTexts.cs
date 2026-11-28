using System.Runtime.CompilerServices;

namespace CodeChops.Idioma;

/// <summary>
/// Finds the texts of members marked <c>[Localized]</c>, through one function per assembly, and keeps what they find.
/// Every overload of <c>LocalizedText.GetTextByMember</c> and <c>LocalizedText.GetTextOfType</c> reads from it.
/// </summary>
internal static class MemberTexts
{
    /// <summary>
    /// Replaced as a whole when an assembly registers its function, so a member looked up before isn't kept as one
    /// without a text.
    /// </summary>
    private static MemberTextSources Sources = new([]);

    /// <summary>
    /// The texts of the type this thread asked for last, because a form asks for the labels of one model's properties
    /// one after another.
    /// </summary>
    // Per thread, because threads of a server render forms of different models at once, and a shared field would be
    // written by every lookup.
    [ThreadStatic]
    private static TypeTexts? LastTextsByType;

    public static string? Get<T>(string memberName)
    {
        var text = GetTextsOf<T>().Get(memberName)?.Word();
        return text;
    }

    public static string? Get(Type type, string memberName)
    {
        var text = GetTextsOf(type).Get(memberName)?.Word();
        return text;
    }

    public static string? GetOwn<T>()
    {
        var text = GetTextsOf<T>().GetOwn()?.Word();
        return text;
    }

    public static string? GetOwn(Type type)
    {
        var text = GetTextsOf(type).GetOwn()?.Word();
        return text;
    }

    /// <summary>
    /// Adds the function that finds the texts of an assembly's members
    /// (<see cref="Internals.GeneratedCode.RegisterMemberTexts"/>).
    /// </summary>
    public static void Register(string[] languages, Func<Type, string?, string[]?> find)
    {
        ArgumentNullException.ThrowIfNull(languages);
        ArgumentNullException.ThrowIfNull(find);

        var finder = new MemberTextFinder(languages, find);

        // A new whole, without the texts found so far, because a member looked up before was kept as one without a text.
        MemberTextSources current, next;

        do
        {
            current = Volatile.Read(ref Sources);
            next = new MemberTextSources([.. current.Finders, finder]);
        }
        while (!ReferenceEquals(Interlocked.CompareExchange(ref Sources, next, current), current));
    }

    /// <summary>
    /// Kept per type, so asking again reads a field.
    /// </summary>
    private static TypeTexts GetTextsOf<T>()
    {
        var texts = TextsOf<T>.Value;

        // Found again when an assembly registered its texts since, because what was found before may lack one of them.
        if (texts is null || !ReferenceEquals(texts.Sources, Volatile.Read(ref Sources)))
            TextsOf<T>.Value = texts = GetTextsOf(typeof(T));

        return texts;
    }

    private static TypeTexts GetTextsOf(Type type)
    {
        var sources = Volatile.Read(ref Sources);
        var last = LastTextsByType;

        if (last is not null && ReferenceEquals(last.Type, type) && ReferenceEquals(last.Sources, sources))
            return last;

        if (!sources.TextsByType.TryGetValue(type, out var texts))
        {
            // Its texts may be registered now, which replaces the sources.
            if (RegisterAssembliesOf(type))
                sources = Volatile.Read(ref Sources);

            texts = sources.AddTextsOf(type);
        }

        LastTextsByType = texts;
        return texts;
    }

    /// <summary>
    /// Runs the module initializers of a type's assembly and its base classes' assemblies, and gives whether that
    /// registered something. An assembly that's only read through reflection, such as a plugin's, hasn't run its own.
    /// </summary>
    // Not under NativeAOT, which runs every module initializer as the app starts.
    private static bool RegisterAssembliesOf(Type type)
    {
        if (!RuntimeFeature.IsDynamicCodeSupported)
            return false;

        var before = Volatile.Read(ref Sources);

        for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
        {
            // A module initializer may throw, and a type that isn't of the runtime has no module to run. The lookup then
            // finds the texts registered so far, and the exception reaches the assembly's own code when that first runs.
            try
            {
                RuntimeHelpers.RunModuleConstructor(current.Module.ModuleHandle);
            }
            catch (Exception)
            {
                // Looked up without the texts of this assembly.
            }
        }

        var registered = !ReferenceEquals(before, Volatile.Read(ref Sources));
        return registered;
    }

    private static class TextsOf<T>
    {
        public static TypeTexts? Value;
    }

    /// <summary>
    /// The function that finds the texts of one assembly's members, with that assembly's languages, the default first.
    /// </summary>
    private sealed class MemberTextFinder(string[] languages, Func<Type, string?, string[]?> find)
    {
        public string[] Languages { get; } = languages;

        /// <summary>
        /// Gives a member's wording per language, in the order of <see cref="Languages"/>.
        /// </summary>
        public Func<Type, string?, string[]?> Find { get; } = find;
    }

    private sealed class MemberText(string[] languages, string[] wordings)
    {
        public string Word()
        {
            var index = StandaloneLanguage.IndexOfShownLanguage(languages);

            // A wording per language, as the generator writes them. Fewer would be code written by hand.
            var wording = wordings[index < wordings.Length ? index : 0];
            return wording;
        }
    }

    private sealed class MemberTextSources(MemberTextFinder[] finders)
    {
        public MemberTextFinder[] Finders { get; } = finders;

        public ConcurrentDictionary<Type, TypeTexts> TextsByType { get; } = new();

        /// <summary>
        /// Asks every function for the member of the type, and then of each base class. A type's own text is asked of
        /// the type alone. A constructed generic type gives the texts of its definition.
        /// </summary>
        public MemberText? Get(Type type, string? member)
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

        public TypeTexts AddTextsOf(Type type)
        {
            // Kept as asked for, not as its definition, because a thread's last lookup compares the type it was asked for.
            var texts = this.TextsByType.GetOrAdd(type, static (type, sources) => new TypeTexts(sources, type), this);
            return texts;
        }
    }

    /// <summary>
    /// The texts found so far for one type and its members, a member without a text included.
    /// </summary>
    /// <remarks>
    /// A name is placed by its length and three of its characters, and compared as an object first, because it's nearly
    /// always the same string, such as <c>nameof(Address.City)</c>. Hashing the whole name is slow in WebAssembly.
    /// </remarks>
    private sealed class TypeTexts(MemberTextSources sources, Type type)
    {
        /// <summary>
        /// So names a caller makes up, such as a user's input, can't grow without end. A name beyond these is found
        /// anew each time.
        /// </summary>
        private const int MaxKept = 256;

        public MemberTextSources Sources { get; } = sources;
        public Type Type { get; } = type;

        // Replaced as a whole, so a thread never sees half of it. Two threads adding a name at once may lose one, which
        // is then found anew.
        private volatile MemberTable _table = MemberTable.Empty;

        private MemberText? _ownText;

        // Volatile, so a thread that sees it set also sees _ownText, which is written first. A flag, because a type
        // without a text keeps null in _ownText.
        private volatile bool _hasOwnText;

        public MemberText? GetOwn()
        {
            if (this._hasOwnText)
                return this._ownText;

            var text = this.Sources.Get(this.Type, member: null);

            this._ownText = text;
            this._hasOwnText = true;

            return text;
        }

        public MemberText? Get(string member)
        {
            var kept = this._table;
            var names = kept.Names;
            var mask = names.Length - 1;

            // The table is never full, so the search ends at an empty place.
            for (var place = MemberTable.Hash(member) & mask; names[place] is { } name; place = (place + 1) & mask)
                if (ReferenceEquals(name, member) || String.Equals(name, member, StringComparison.Ordinal))
                    return kept.Texts[place];

            var text = this.Sources.Get(this.Type, member);

            if (kept.Count < MaxKept)
                this._table = kept.With(member, text);

            return text;
        }
    }

    /// <summary>
    /// Names and the text of each, in an open-addressed table. Never changed once made.
    /// </summary>
    private sealed class MemberTable
    {
        public static MemberTable Empty { get; } = new(new string?[8], new MemberText?[8], count: 0);

        // Two arrays, not one of pairs, because ReadyToRun can't precompile an array of this library's struct.
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
        /// This table with one name more. At most half of its places are taken, so a search is short and always ends.
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

        // Inlined, because a call costs more than this in WebAssembly's interpreter.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Hash(string name)
        {
            var length = name.Length;

            var hash = length is 0 ? 0 : (length * 31) + (name[0] * 7) + (name[length / 2] * 3) + name[length - 1];
            return hash;
        }
    }
}
