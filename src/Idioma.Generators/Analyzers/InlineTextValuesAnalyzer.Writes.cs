using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Operations;

namespace CodeChops.Idioma.Generators;

/// <inheritdoc cref="InlineTextValuesAnalyzer"/>
public sealed partial class InlineTextValuesAnalyzer
{
    /// <summary>
    /// The <see cref="Writes"/> of each operation tree of a compilation, by its root: the body of a method, or an
    /// initializer.
    /// </summary>
    // One per compilation, so nothing is shared between two of them, and an entry goes when its tree does. The writes of
    // a tree are found when a wording first reads a local variable in it, so a tree without inline texts isn't searched,
    // and nothing is allocated for it.
    private sealed class TreeWrites
    {
        // By reference, because an operation doesn't override Equals. Safe to use from several threads.
        private ConditionalWeakTable<IOperation, Writes> ByRoot { get; } = new();

        /// <summary>
        /// What the code around an operation writes its local variables with.
        /// </summary>
        public Writes Find(IOperation operation, CancellationToken cancellationToken)
        {
            var root = operation;
            while (root.Parent is { } parent)
                root = parent;

            // Two threads that find the writes of one tree at once each find them, and the first to finish is kept.
            var writes = this.ByRoot.GetValue(root, tree => Writes.Find(tree, cancellationToken));
            return writes;
        }
    }

    /// <summary>
    /// What the code around a call writes its local variables with, other than where it declares them: its assignments,
    /// and its arguments passed by reference.
    /// </summary>
    // Found in one pass over the operation tree, and then looked up by the local variable. So a wording that reads a
    // local variable costs as little in a method with thousands of inline texts as in a method with one.
    private sealed class Writes
    {
        /// <summary>
        /// What each local variable is assigned after its declaration, in the order of the code.
        /// </summary>
        private Dictionary<ILocalSymbol, List<IOperation>> Assigned { get; } = new(SymbolEqualityComparer.Default);

        /// <summary>
        /// The local variables written in another way, as by a deconstruction or as an argument passed by reference.
        /// Every local variable that is read where such a write writes to counts.
        /// </summary>
        private HashSet<ILocalSymbol> WrittenUnreadably { get; } = new(SymbolEqualityComparer.Default);

        /// <summary>
        /// Finds the assignments and the arguments passed by reference under the root of an operation tree: the body of
        /// a method, or an initializer.
        /// </summary>
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

        /// <summary>
        /// What a local variable is assigned after its declaration, in the order of the code.
        /// </summary>
        public IReadOnlyList<IOperation> AssignedTo(ILocalSymbol local)
        {
            if (!this.Assigned.TryGetValue(local, out var values))
                return [];

            return values;
        }

        /// <summary>
        /// Whether a local variable is written in a way the build can't read
        /// (<see cref="Reads.ReadsUnreadableLocal"/>).
        /// </summary>
        public bool IsWrittenUnreadably(ILocalSymbol local)
        {
            var isWrittenUnreadably = this.WrittenUnreadably.Contains(local);
            return isWrittenUnreadably;
        }

        /// <summary>
        /// Adds every local variable that is read where a write writes to.
        /// </summary>
        private void AddWrittenUnreadably(IOperation target)
        {
            foreach (var part in target.DescendantsAndSelf())
                if (part is ILocalReferenceOperation reference)
                    this.WrittenUnreadably.Add(reference.Local);
        }
    }
}
