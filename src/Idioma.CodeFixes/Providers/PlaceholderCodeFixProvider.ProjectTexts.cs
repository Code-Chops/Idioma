using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Text;

namespace CodeChops.Idioma.CodeFixes;

/// <inheritdoc cref="PlaceholderCodeFixProvider"/>
public sealed partial class PlaceholderCodeFixProvider
{
    /// <summary>
    /// What the fix reads of a whole project, cached per compilation: derived and implementing classes, uses of a
    /// name, and each text's fix. The IDE and "Fix all" ask for a fix many times, so each would otherwise read the whole
    /// project again.
    /// </summary>
    private sealed class ProjectTexts(Compilation compilation)
    {
        private static readonly ConditionalWeakTable<Compilation, ProjectTexts> PerCompilation = new();

        private object Gate { get; } = new();
        private Dictionary<PropertyDeclarationSyntax, Dictionary<MemberDeclarationSyntax, MemberDeclarationSyntax>?> Fixes { get; } = [];
        private HashSet<string> ExpectedNames { get; } = new(StringComparer.Ordinal);
        private Dictionary<string, ImmutableArray<IdentifierNameSyntax>> UsesByName { get; } = new(StringComparer.Ordinal);
        private Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>>? ClassesByBase { get; set; }
        private Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>>? ClassesByInterface { get; set; }

        public Compilation Compilation { get; } = compilation;

        public static ProjectTexts Of(Compilation compilation) => PerCompilation.GetValue(compilation, static compilation => new ProjectTexts(compilation));

        /// <summary>
        /// The fix of a property, found with <paramref name="find"/> the first time.
        /// </summary>
        public Dictionary<MemberDeclarationSyntax, MemberDeclarationSyntax>? GetOrAddFix(PropertyDeclarationSyntax property,
            Func<Dictionary<MemberDeclarationSyntax, MemberDeclarationSyntax>?> find)
        {
            lock (this.Gate)
            {
                if (this.Fixes.TryGetValue(property, out var known))
                    return known;
            }

            var found = find();

            lock (this.Gate)
                this.Fixes[property] = found;

            return found;
        }

        /// <summary>
        /// The top-level classes of this assembly that derive from a class, directly or not.
        /// </summary>
        public IReadOnlyList<INamedTypeSymbol> FindClassesDerivingFrom(INamedTypeSymbol @base)
        {
            lock (this.Gate)
            {
                this.ReadClasses();
                return this.ClassesByBase!.TryGetValue(@base, out var classes) ? classes : [];
            }
        }

        /// <summary>
        /// The top-level classes of this assembly that implement an interface, directly or not.
        /// </summary>
        public IReadOnlyList<INamedTypeSymbol> FindClassesImplementing(INamedTypeSymbol @interface)
        {
            lock (this.Gate)
            {
                this.ReadClasses();
                return this.ClassesByInterface!.TryGetValue(@interface, out var classes) ? classes : [];
            }
        }

        private void ReadClasses()
        {
            if (this.ClassesByBase is not null)
                return;

            var byBase = new Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>>(SymbolEqualityComparer.Default);
            var byInterface = new Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>>(SymbolEqualityComparer.Default);

            foreach (var type in CodeFixSupport.GetClasses(this.Compilation.Assembly.GlobalNamespace))
            {
                for (var @base = type.BaseType; @base is { SpecialType: not SpecialType.System_Object }; @base = @base.BaseType)
                    Add(byBase, @base, type);

                foreach (var @interface in type.AllInterfaces)
                    Add(byInterface, @interface, type);
            }

            this.ClassesByInterface = byInterface;
            this.ClassesByBase = byBase;

            static void Add(Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>> classes, INamedTypeSymbol key, INamedTypeSymbol type)
            {
                if (!classes.TryGetValue(key, out var found))
                    classes.Add(key, found = []);

                found.Add(type);
            }
        }

        /// <summary>
        /// Notes a name <see cref="FindUsesOf"/> will be asked for, so one pass finds the uses of all of them.
        /// </summary>
        public void ExpectUsesOf(string name)
        {
            lock (this.Gate)
            {
                if (!this.UsesByName.ContainsKey(name))
                    this.ExpectedNames.Add(name);
            }
        }

        /// <summary>
        /// Each use of a name in the project's code, generated code included.
        /// </summary>
        /// <remarks>
        /// Results are kept per syntax tree, so after an edit only the changed file is searched again.
        /// </remarks>
        public ImmutableArray<IdentifierNameSyntax> FindUsesOf(string name, CancellationToken cancellationToken)
        {
            lock (this.Gate)
            {
                if (this.UsesByName.TryGetValue(name, out var known))
                    return known;

                this.ExpectedNames.Add(name);

                var expected = this.ExpectedNames.ToArray();
                var found = expected.ToDictionary(static expected => expected, static _ => ImmutableArray.CreateBuilder<IdentifierNameSyntax>(), StringComparer.Ordinal);

                foreach (var tree in this.Compilation.SyntaxTrees)
                {
                    var spansByName = UsesPerTree.GetValue(tree, static _ => new ConcurrentDictionary<string, ImmutableArray<TextSpan>>(StringComparer.Ordinal));
                    var root = FindSpans(tree, spansByName, expected, cancellationToken);

                    foreach (var expectedName in expected)
                    {
                        var spans = spansByName[expectedName];

                        if (spans.IsEmpty)
                            continue;

                        root ??= tree.GetRoot(cancellationToken);

                        foreach (var span in spans)
                            if (root.FindNode(span, getInnermostNodeForTie: true) is IdentifierNameSyntax identifier)
                                found[expectedName].Add(identifier);
                    }
                }

                foreach (var uses in found)
                    this.UsesByName[uses.Key] = uses.Value.ToImmutable();

                this.ExpectedNames.Clear();

                return this.UsesByName[name];
            }
        }

        // Spans only, so this doesn't keep the file's syntax alive.
        private static readonly ConditionalWeakTable<SyntaxTree, ConcurrentDictionary<string, ImmutableArray<TextSpan>>> UsesPerTree = new();

        /// <summary>
        /// Searches a file in one pass for the names it hasn't been searched for yet. Returns the root when it searched,
        /// else <see langword="null"/>.
        /// </summary>
        private static SyntaxNode? FindSpans(SyntaxTree tree, ConcurrentDictionary<string, ImmutableArray<TextSpan>> spansByName, string[] names,
            CancellationToken cancellationToken)
        {
            var missing = names.Where(name => !spansByName.ContainsKey(name)).ToDictionary(static name => name, static _ => ImmutableArray.CreateBuilder<TextSpan>(), StringComparer.Ordinal);

            if (missing.Count is 0)
                return null;

            var root = tree.GetRoot(cancellationToken);

            foreach (var identifier in root.DescendantNodes().OfType<IdentifierNameSyntax>())
                if (missing.TryGetValue(identifier.Identifier.ValueText, out var spans))
                    spans.Add(identifier.Span);

            foreach (var spans in missing)
                spansByName.TryAdd(spans.Key, spans.Value.ToImmutable());

            return root;
        }
    }
}
