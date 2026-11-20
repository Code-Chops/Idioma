using System.Reflection;

namespace CodeChops.Idioma;

// The half of LocalizableText for the texts of members: a property, a field, an enum member, or a type marked
// [Localized]. A plain comment: the compiler merges the XML docs of every partial declaration, and the type's summary
// lives in LocalizableText.cs.
public sealed partial class LocalizableText
{
    /// <summary>
    /// What finds a member's text: one function per assembly that marks members <c>[Localized]</c>, and the texts
    /// found so far. Replaced as a whole when an assembly records its function, so that a member looked up before
    /// that isn't kept as one without a text.
    /// </summary>
    private static MemberTextSources MemberTexts = new([]);

    /// <summary>
    /// The texts of the type that was asked for last by a <see cref="Type"/>. A form asks for the labels of one
    /// model's properties one after another, so most lookups by a type find theirs here.
    /// </summary>
    private static TypeTexts? LastTextsByType;

    /// <summary>
    /// The text of a member marked <c>[Localized]</c>, by the type that declares it and its name, or
    /// <see langword="null"/> for a member without one.
    /// <c>LocalizableText.GetTextByMember&lt;Address&gt;(nameof(Address.City))</c> gives the text of
    /// <c>Address.City</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A member is a property, a field, or a member of an enum. Mark it in the assembly that declares it:
    /// <c>[Localized(en: "City", nl: "Plaats")]</c>. The source generator reads the wording as the assembly builds,
    /// so nothing reads the attribute at run time.
    /// </para>
    /// <para>
    /// A class without a text for the member gets that of its nearest base class. So an override without
    /// <c>[Localized]</c> keeps the text of the property it overrides. An interface's texts aren't inherited.
    /// </para>
    /// <para>
    /// The text is the same object on every call. Its <see cref="Key"/> is the member's name, and its
    /// <see cref="FullKey"/> is the member's full name, such as <c>Shop.Domain.Address.City</c>.
    /// </para>
    /// <para>
    /// For a value of an enum, <c>status.GetText()</c> gives its text without a name. For the text of a type itself,
    /// call <see cref="GetTextOfType{T}()"/>.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The type that declares the member, or one that derives from it.</typeparam>
    /// <param name="memberName">The name of the member, in the same letter case, such as <c>nameof(Address.City)</c>.</param>
    /// <exception cref="ArgumentNullException">When <paramref name="memberName"/> is <see langword="null"/>.</exception>
    public static LocalizableText? GetTextByMember<T>(string memberName)
    {
        ArgumentNullException.ThrowIfNull(memberName);

        var text = FindTextsOf<T>().Find(memberName);
        return text;
    }

    /// <summary>
    /// The text of a member marked <c>[Localized]</c>, by a type your code only has at run time and the member's name,
    /// or <see langword="null"/> for a member without one. Where your code knows the type, write
    /// <see cref="GetTextByMember{T}(string)"/>.
    /// </summary>
    /// <remarks>
    /// A type with type arguments, such as <c>Page&lt;Order&gt;</c>, gives the text of <c>Page&lt;T&gt;</c>.
    /// </remarks>
    /// <param name="type">The type that declares the member, or one that derives from it.</param>
    /// <param name="memberName">The name of the member, in the same letter case.</param>
    /// <exception cref="ArgumentNullException">
    /// When <paramref name="type"/> or <paramref name="memberName"/> is <see langword="null"/>.
    /// </exception>
    public static LocalizableText? GetTextByMember(Type type, string memberName)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(memberName);

        var text = FindTextsOf(type).Find(memberName);
        return text;
    }

    /// <summary>
    /// The text of a member your code holds, such as the <see cref="PropertyInfo"/> of a form field, or
    /// <see langword="null"/> for a member without one.
    /// </summary>
    /// <remarks>
    /// Only the member's name and the type that declares it are read. A <see cref="Type"/> is a member too, and gives
    /// the text <see cref="GetTextOfType(Type)"/> gives.
    /// </remarks>
    /// <param name="member">A property, a field, a member of an enum, or a type.</param>
    /// <exception cref="ArgumentNullException">When <paramref name="member"/> is <see langword="null"/>.</exception>
    public static LocalizableText? GetTextByMember(MemberInfo member)
    {
        ArgumentNullException.ThrowIfNull(member);

        if (member is Type type)
            return FindTextsOf(type).FindOwn();

        var text = member.DeclaringType is { } declaringType
            ? FindTextsOf(declaringType).Find(member.Name)
            : null;

        return text;
    }

    /// <summary>
    /// The text of a type marked <c>[Localized]</c> itself, such as the name of an enum, or <see langword="null"/>
    /// for a type without one. <c>LocalizableText.GetTextOfType&lt;OrderStatus&gt;()</c> gives the text of
    /// <c>OrderStatus</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A type's own text isn't inherited, so a class doesn't get the text of its base class.
    /// </para>
    /// <para>
    /// The text is the same object on every call. Its <see cref="Key"/> is the type's name, and its
    /// <see cref="FullKey"/> is the type's full name, such as <c>Shop.Domain.OrderStatus</c>.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The type that is marked.</typeparam>
    public static LocalizableText? GetTextOfType<T>()
    {
        var text = FindTextsOf<T>().FindOwn();
        return text;
    }

    /// <summary>
    /// The text of a type marked <c>[Localized]</c> itself, by a type your code only has at run time, or
    /// <see langword="null"/> for a type without one. Where your code knows the type, write
    /// <see cref="GetTextOfType{T}()"/>.
    /// </summary>
    /// <remarks>
    /// A type with type arguments, such as <c>Page&lt;Order&gt;</c>, gives the text of <c>Page&lt;T&gt;</c>.
    /// </remarks>
    /// <param name="type">The type that is marked.</param>
    /// <exception cref="ArgumentNullException">When <paramref name="type"/> is <see langword="null"/>.</exception>
    public static LocalizableText? GetTextOfType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var text = FindTextsOf(type).FindOwn();
        return text;
    }

    /// <summary>
    /// Creates the text of a member marked <c>[Localized]</c>. <see cref="Internals.GeneratedCode.CreateMemberText"/>
    /// calls this once per member, for the generated code that holds the text.
    /// </summary>
    internal static LocalizableText CreateMemberText(string fullKey, string key, ReadOnlySpan<(string Language, string Text)> wordings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullKey);
        ArgumentNullException.ThrowIfNull(key);

        if (wordings.IsEmpty)
            throw new ArgumentException($"The text '{fullKey}' needs a wording in its default language.", nameof(wordings));

        var inline = FromGeneratedCode(wordings[0].Text, wordings);
        var text = new LocalizableText(inline) { Origin = new MemberTextOrigin(fullKey, key) };

        return text;
    }

    /// <summary>
    /// Adds the function that finds the texts of an assembly's members.
    /// <see cref="Internals.GeneratedCode.RecordMemberTexts"/> calls this when the assembly registers.
    /// </summary>
    internal static void RecordMemberTexts(Func<Type, string?, LocalizableText?> find)
    {
        ArgumentNullException.ThrowIfNull(find);

        // A new whole, without the texts found so far: a member looked up before its assembly registered was kept as
        // one without a text. A lookup that started on the old whole keeps what it finds there, where no one reads it.
        MemberTextSources current, next;

        do
        {
            current = Volatile.Read(ref MemberTexts);
            next = new MemberTextSources([.. current.Finders, find]);
        }
        while (!ReferenceEquals(Interlocked.CompareExchange(ref MemberTexts, next, current), current));
    }

    /// <summary>
    /// The texts of a type the code names. Kept per type, so that asking again is a read of a field.
    /// </summary>
    private static TypeTexts FindTextsOf<T>()
    {
        var texts = TextsOf<T>.Value;

        // Found again when an assembly recorded its texts since: what was found before may lack one of them.
        if (texts is null || !ReferenceEquals(texts.Sources, Volatile.Read(ref MemberTexts)))
            TextsOf<T>.Value = texts = FindTextsOf(typeof(T));

        return texts;
    }

    /// <summary>
    /// The texts of a type the code holds.
    /// </summary>
    private static TypeTexts FindTextsOf(Type type)
    {
        var sources = Volatile.Read(ref MemberTexts);
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
    /// The functions that find a member's text, and the texts found so far, by type.
    /// </summary>
    private sealed class MemberTextSources(Func<Type, string?, LocalizableText?>[] finders)
    {
        public Func<Type, string?, LocalizableText?>[] Finders { get; } = finders;

        /// <summary>
        /// The texts found for each type that was asked for. A type with type arguments, such as
        /// <c>Page&lt;Order&gt;</c>, has those of its definition.
        /// </summary>
        public ConcurrentDictionary<Type, TypeTexts> TextsByType { get; } = new();

        /// <summary>
        /// Asks every function for the member of the type, and then of each class it derives from. A type's own text
        /// is asked of the type alone.
        /// </summary>
        public LocalizableText? Find(Type type, string? member)
        {
            for (var current = type; current is not null && current != typeof(object); current = member is null ? null : current.BaseType)
            {
                var definition = current.IsConstructedGenericType ? current.GetGenericTypeDefinition() : current;

                foreach (var find in this.Finders)
                    if (find(definition, member) is { } text)
                        return text;
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

        private LocalizableText? _ownText;
        private volatile bool _hasOwnText;

        /// <summary>
        /// The text of the type itself.
        /// </summary>
        public LocalizableText? FindOwn()
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
        public LocalizableText? Find(string member)
        {
            var kept = this._table;
            var names = kept.Names;
            var mask = names.Length - 1;

            // Written out here, as in FindOrCreateInline, because a call costs more than this in WebAssembly's
            // interpreter. MemberTable.Hash is the same.
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
        public static MemberTable Empty { get; } = new(new string?[8], new LocalizableText?[8], count: 0);

        // Two arrays, and not one of pairs: an array of a struct of two is code the runtime compiles when the app
        // starts, where arrays of objects are there already.
        public string?[] Names { get; }
        public LocalizableText?[] Texts { get; }
        public int Count { get; }

        private MemberTable(string?[] names, LocalizableText?[] texts, int count)
        {
            this.Names = names;
            this.Texts = texts;
            this.Count = count;
        }

        /// <summary>
        /// This table with one name more. At most half of its places are taken, so that a search is short and always
        /// ends.
        /// </summary>
        public MemberTable With(string name, LocalizableText? text)
        {
            var size = (this.Count + 1) * 2 > this.Names.Length ? this.Names.Length * 2 : this.Names.Length;
            var names = new string?[size];
            var texts = new LocalizableText?[size];

            for (var i = 0; i < this.Names.Length; i++)
                if (this.Names[i] is { } kept)
                    Put(names, texts, kept, this.Texts[i]);

            Put(names, texts, name, text);

            return new MemberTable(names, texts, this.Count + 1);
        }

        private static void Put(string?[] names, LocalizableText?[] texts, string name, LocalizableText? text)
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
