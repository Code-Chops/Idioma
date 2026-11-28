using System.Runtime.CompilerServices;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Gives a transform's result for a node again on an edit that can't change it, instead of reading the node anew.
/// </summary>
/// <remarks>
/// <para>
/// Roslyn runs the transform of a syntax provider for every matching node on every edit, because it hands each a new
/// semantic model. Discovery binds each set's base types, members and constructors there, so every keystroke read every
/// set of the project again: about 130 ms at 200 contracts, most of it binding and the garbage it leaves.
/// </para>
/// <para>
/// A result is given again while all of these hold:
/// </para>
/// <list type="bullet">
/// <item>The node is the same object, so its file didn't change.</item>
/// <item>Every other file the transform read is in the compilation as the same tree (<see cref="RecordRead"/>).</item>
/// <item>The compilation declares the same (<see cref="Shape"/>): no file changed outside the bodies and initializers of
/// its members, and it has the same references and options.</item>
/// </list>
/// <para>
/// A body or an initializer declares nothing, so an edit inside one changes how no name binds and what no symbol is. A
/// transform that reads one, as discovery reads a wording, reads it in its own node or in a file it recorded.
/// </para>
/// <para>
/// An edit of a declaration, such as a text added to a set, a base list or a <c>using</c>, makes every node be read
/// anew, as it did before this cache. Narrowing that further would need to know every member of another class a
/// transform reads, such as one a <c>nameof</c> in a wording names.
/// </para>
/// </remarks>
// The results live as long as their nodes, so a file that's gone takes its results with it. The cache serves an IDE,
// which runs one driver edit after edit. A build parses every file anew, so it finds nothing here.
internal static class TransformCache
{
    private static readonly ConditionalWeakTable<SyntaxNode, Slot> Slots = new();
    private static readonly ConditionalWeakTable<Compilation, Shape> Shapes = new();
    private static readonly ConditionalWeakTable<SyntaxTree, StrongBox<ulong>> DeclarationHashes = new();

    /// <summary>
    /// The shape last worked out, so the compilations of one project share one instance and compare by reference.
    /// </summary>
    private static Shape? LastShape;

    /// <summary>
    /// The files the transform that runs on this thread read, besides its node's own.
    /// </summary>
    [ThreadStatic]
    private static HashSet<SyntaxTree>? ReadTrees;

    /// <summary>
    /// How many results this thread read anew, for a test to tell a result given again from one read anew.
    /// </summary>
    [ThreadStatic]
    private static int ReadsOnThread;

    internal static int ReadsOnCurrentThread => ReadsOnThread;

    /// <summary>
    /// Records that the transform that runs on this thread read a file, such as the file of a base class whose wording
    /// a set inherits. An edit of that file then makes the transform read its node anew.
    /// </summary>
    public static void RecordRead(SyntaxTree tree)
    {
        ReadTrees?.Add(tree);
    }

    /// <summary>
    /// The result of <paramref name="read"/> for a node, given again where nothing it read can have changed.
    /// </summary>
    public static TResult GetOrRead<TState, TResult>(SyntaxNode node, Compilation compilation, TState state,
        Func<TState, CancellationToken, TResult> read, CancellationToken cancellationToken)
    {
        var shape = GetShape(compilation, cancellationToken);
        var slot = Slots.GetValue(node, static _ => new Slot());

        if (slot.Entry is Entry<TResult> entry && (ReferenceEquals(entry.Shape, shape) || entry.Shape.Equals(shape)) && AreAllHeld(entry.ReadTrees, compilation))
            return entry.Result;

        var outer = ReadTrees;
        var readTrees = new HashSet<SyntaxTree> { node.SyntaxTree };
        ReadTrees = readTrees;

        TResult result;

        try
        {
            result = read(state, cancellationToken);
        }
        finally
        {
            ReadTrees = outer;
        }

        outer?.UnionWith(readTrees);
        ReadsOnThread++;
        slot.Entry = new Entry<TResult>(result, shape, [.. readTrees]);

        return result;
    }

    private static bool AreAllHeld(ImmutableArray<SyntaxTree> trees, Compilation compilation)
    {
        foreach (var tree in trees)
            if (!compilation.ContainsSyntaxTree(tree))
                return false;

        return true;
    }

    private static Shape GetShape(Compilation compilation, CancellationToken cancellationToken)
    {
        if (Shapes.TryGetValue(compilation, out var known))
            return known;

        var hash = Fnv.Seed;

        foreach (var tree in compilation.SyntaxTrees)
            hash = Fnv.Mix(hash, GetDeclarationHash(tree, cancellationToken));

        var shape = new Shape(hash, [.. compilation.References], compilation.Options, compilation.AssemblyName);

        // The compilations of one project after an edit in a body share the instance of the one before.
        var last = Volatile.Read(ref LastShape);

        if (last is not null && last.Equals(shape))
            shape = last;
        else
            Volatile.Write(ref LastShape, shape);

        return Shapes.GetValue(compilation, _ => shape);
    }

    /// <summary>
    /// A hash of what a file declares: every token and directive outside the bodies and initializers of its members.
    /// </summary>
    private static ulong GetDeclarationHash(SyntaxTree tree, CancellationToken cancellationToken)
    {
        if (DeclarationHashes.TryGetValue(tree, out var known))
            return known.Value;

        var hash = Fnv.Mix(Fnv.Seed, tree.FilePath);

        foreach (var token in tree.GetRoot(cancellationToken).DescendantTokens(static node => !IsBodyOrInitializer(node)))
        {
            if (token.ContainsDirectives)
            {
                foreach (var trivia in token.LeadingTrivia)
                    if (trivia.IsDirective)
                        hash = Fnv.Mix(hash, trivia.ToFullString());
            }

            hash = Fnv.Mix(Fnv.Mix(hash, (ulong)token.RawKind), token.Text);
        }

        return DeclarationHashes.GetValue(tree, _ => new StrongBox<ulong>(hash)).Value;
    }

    /// <summary>
    /// Whether a node holds code that declares nothing another file can see: the body of a method, an accessor or a
    /// property, the initializer of a field that isn't constant or of a property, or a top-level statement. The wording
    /// of a <c>[Localized]</c> counts too, because only the transform of that attribute reads it. The value of a
    /// constant, of an enum member and of a parameter's default stays, because code elsewhere reads them.
    /// </summary>
    private static bool IsBodyOrInitializer(SyntaxNode node)
    {
        var isBodyOrInitializer = node switch
        {
            BlockSyntax { Parent: BaseMethodDeclarationSyntax or AccessorDeclarationSyntax } => true,
            ArrowExpressionClauseSyntax { Parent: BaseMethodDeclarationSyntax or AccessorDeclarationSyntax or BasePropertyDeclarationSyntax } => true,
            EqualsValueClauseSyntax { Parent: PropertyDeclarationSyntax } => true,
            EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax { Parent.Parent: FieldDeclarationSyntax field } } => !field.Modifiers.Any(SyntaxKind.ConstKeyword),
            AttributeArgumentListSyntax { Parent: AttributeSyntax attribute } => LocalizedMembers.IsNamedLocalized(attribute),
            GlobalStatementSyntax => true,
            _ => false,
        };

        return isBodyOrInitializer;
    }

    /// <summary>
    /// The entry of one node, replaced as a whole, so a thread reads a whole one.
    /// </summary>
    private sealed class Slot
    {
        private volatile object? _entry;

        public object? Entry
        {
            get => this._entry;
            set => this._entry = value;
        }
    }

    private sealed class Entry<TResult>(TResult result, Shape shape, ImmutableArray<SyntaxTree> readTrees)
    {
        public TResult Result { get; } = result;
        public Shape Shape { get; } = shape;
        public ImmutableArray<SyntaxTree> ReadTrees { get; } = readTrees;
    }

    /// <summary>
    /// What decides how every name of a compilation binds: what its files declare, its references and its options.
    /// </summary>
    private sealed class Shape(ulong declarationHash, ImmutableArray<MetadataReference> references, CompilationOptions options, string? assemblyName)
        : IEquatable<Shape>
    {
        private ulong DeclarationHash { get; } = declarationHash;
        private ImmutableArray<MetadataReference> References { get; } = references;
        private CompilationOptions Options { get; } = options;
        private string? AssemblyName { get; } = assemblyName;

        public bool Equals(Shape? other)
        {
            if (ReferenceEquals(this, other))
                return true;

            if (other is null
                || this.DeclarationHash != other.DeclarationHash
                || !String.Equals(this.AssemblyName, other.AssemblyName, StringComparison.Ordinal)
                || this.References.Length != other.References.Length
                || !this.Options.Equals(other.Options))
            {
                return false;
            }

            // By reference, because a reference to another project changes as that project does.
            for (var i = 0; i < this.References.Length; i++)
                if (!ReferenceEquals(this.References[i], other.References[i]))
                    return false;

            return true;
        }

        public override bool Equals(object? obj) => this.Equals(obj as Shape);

        public override int GetHashCode() => this.DeclarationHash.GetHashCode();
    }

    /// <summary>
    /// FNV-1a, 64 bits.
    /// </summary>
    private static class Fnv
    {
        public const ulong Seed = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        public static ulong Mix(ulong hash, ulong value)
        {
            for (var i = 0; i < 8; i++)
            {
                hash ^= (value >> (8 * i)) & 0xFF;
                hash *= Prime;
            }

            return hash;
        }

        public static ulong Mix(ulong hash, string text)
        {
            foreach (var character in text)
            {
                hash ^= character;
                hash *= Prime;
            }

            // A separator, so "ab" + "c" differs from "a" + "bc".
            hash ^= 0xFF;
            hash *= Prime;

            return hash;
        }
    }
}
