namespace CodeChops.Idioma;

/// <inheritdoc cref="LocalizationRegistry"/>
public sealed partial class LocalizationRegistry
{
    /// <summary>
    /// A set of a contract, with the set registered last among those of its contract and language that its class
    /// derives from, or <see langword="null"/> when it derives from none.
    /// </summary>
    // A class, kept in arrays, because ReadyToRun can't compile the framework's generic types for a struct of this
    // library, and the framework already holds their shared code for classes. As a struct, it left a
    // ConcurrentDictionary and an ImmutableArray of it to the JIT, about 20 methods at the first text of every app.
    internal sealed class ContractSet
    {
        /// <summary>
        /// The set, as it's registered for its contract and language.
        /// </summary>
        public ILocalizationSet Set { get; }

        /// <summary>
        /// The set registered last among those of the same contract and language that the class of <see cref="Set"/>
        /// derives from, or <see langword="null"/> when it derives from none. <see cref="WordsItselfOverBasis"/> tells
        /// which texts <see cref="Set"/> leaves to it.
        /// </summary>
        public ILocalizationSet? Basis { get; }

        /// <summary>
        /// The class of <see cref="Basis"/>, or <see langword="null"/> when there's none. Kept, so a text doesn't ask
        /// <see cref="Basis"/> for it.
        /// </summary>
        private Type? BasisType { get; }

        /// <summary>
        /// The set's class and each class it derives from, in that order, up to <see cref="object"/>.
        /// </summary>
        private Type[] Classes { get; }

        /// <summary>
        /// The build's description of each of <see cref="Classes"/>, or <see langword="null"/> for a class without one.
        /// </summary>
        // Looked up once, here, and not in SetDescriptions on every text, which cost a British user's text about 10 ns on
        // .NET, as did walking Type.BaseType. A registry is built anew after every description (DescribeSet), so none is missed.
        private SetDescription?[] Descriptions { get; }

        public ContractSet(ILocalizationSet set, ILocalizationSet? basis)
        {
            this.Set = set;
            this.Basis = basis;
            this.BasisType = basis?.GetType();

            var classes = new List<Type>(capacity: 2);
            var descriptions = new List<SetDescription?>(capacity: 2);

            for (var current = set.GetType(); current is not null && current != typeof(object); current = current.BaseType)
            {
                classes.Add(current);
                descriptions.Add(SetDescriptions.GetValueOrDefault(current));
            }

            this.Classes = [.. classes];
            this.Descriptions = [.. descriptions];
        }

        /// <summary>
        /// Whether the set words a text itself rather than leaving it to <see cref="Basis"/>, for a set that has one.
        /// See <see cref="WordsItselfOver"/>.
        /// </summary>
        public bool WordsItselfOverBasis(string name)
        {
            var wordsItself = this.WordsItselfOver(this.BasisType!, name);
            return wordsItself;
        }

        /// <summary>
        /// Whether the set words a text itself rather than leaving it to a set of the given class it derives from, as
        /// the build described it: the set's class, or a class between it and that one, words the text. Also where one
        /// of those classes has no description, such as a nested one the build didn't see, because it may word every text.
        /// </summary>
        public bool WordsItselfOver(Type basisType, string name)
        {
            var classes = this.Classes;
            var descriptions = this.Descriptions;

            for (var i = 0; i < classes.Length; i++)
            {
                if (classes[i] == basisType)
                    return false;

                if (descriptions[i] is not { } description || description.Words(name))
                    return true;
            }

            // Not a class the set derives from.
            return true;
        }
    }
}
