using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Operations;

namespace CodeChops.Idioma.Generators;

/// <inheritdoc cref="InlineTextValuesAnalyzer"/>
public sealed partial class InlineTextValuesAnalyzer
{
    /// <summary>
    /// The <see cref="Writes"/> of each operation tree of a compilation, by its root.
    /// </summary>
    // One per compilation, and an entry goes when its tree does. A tree is searched only when a wording first reads a
    // local in it.
    private sealed class TreeWrites
    {
        // By reference, because an operation doesn't override Equals. Thread-safe.
        private ConditionalWeakTable<IOperation, Writes> ByRoot { get; } = new();

        public Writes Find(IOperation operation, CancellationToken cancellationToken)
        {
            var root = operation;
            while (root.Parent is { } parent)
                root = parent;

            // Two threads may both find one tree's writes, and the first to finish is kept.
            var writes = this.ByRoot.GetValue(root, tree => Writes.Find(tree, cancellationToken));
            return writes;
        }
    }

    /// <summary>
    /// The assignments and <c>ref</c> arguments of an operation tree, found in one pass and looked up by local.
    /// </summary>
    private sealed class Writes
    {
        /// <summary>
        /// What each local is assigned after its declaration, in code order.
        /// </summary>
        private Dictionary<ILocalSymbol, List<IOperation>> Assigned { get; } = new(SymbolEqualityComparer.Default);

        /// <summary>
        /// The locals read anywhere in the target of another kind of write, such as a deconstruction or a <c>ref</c>
        /// argument.
        /// </summary>
        private HashSet<ILocalSymbol> WrittenUnreadably { get; } = new(SymbolEqualityComparer.Default);

        public static Writes Find(IOperation root, CancellationToken cancellationToken)
        {
            var writes = new Writes();

            foreach (var part in root.DescendantsAndSelf())
            {
                cancellationToken.ThrowIfCancellationRequested();

                switch (part)
                {
                    case IAssignmentOperation { Target: ILocalReferenceOperation target } assignment:
                        if (!writes.Assigned.TryGetValue(target.Local, out var values))
                            writes.Assigned[target.Local] = values = [];

                        values.Add(assignment.Value);
                        break;

                    case IAssignmentOperation { Target: var target }:
                        writes.AddWrittenUnreadably(target);
                        break;

                    case IArgumentOperation { Parameter.RefKind: RefKind.Ref or RefKind.Out, Value: var value }:
                        writes.AddWrittenUnreadably(value);
                        break;
                }
            }

            return writes;
        }

        public IReadOnlyList<IOperation> AssignedTo(ILocalSymbol local)
        {
            if (!this.Assigned.TryGetValue(local, out var values))
                return [];

            return values;
        }

        /// <summary>
        /// See <see cref="Reads.ReadsUnreadableLocal"/>.
        /// </summary>
        public bool IsWrittenUnreadably(ILocalSymbol local)
        {
            var isWrittenUnreadably = this.WrittenUnreadably.Contains(local);
            return isWrittenUnreadably;
        }

        private void AddWrittenUnreadably(IOperation target)
        {
            foreach (var part in target.DescendantsAndSelf())
                if (part is ILocalReferenceOperation reference)
                    this.WrittenUnreadably.Add(reference.Local);
        }
    }
}
