namespace CodeChops.Idioma;

/// <inheritdoc cref="LocalizationRegistry"/>
public sealed partial class LocalizationRegistry
{
    /// <summary>
    /// How the build described a set: the language it read from the set's name, if any, and the texts the set's
    /// own class words, overriding them, declaring them anew, or implementing them explicitly. Each text by its name,
    /// such as <c>Checkout</c>.
    /// </summary>
    // A class, for the reason ContractSet is one.
    internal sealed class SetDescription
    {
        // Up to this many texts are told apart one by one, which is fastest for the few texts a regional set usually
        // rewords. More are looked up in sorted order, so that a set rewording hundreds of texts doesn't slow every
        // lookup of its users down: scanning 500 names took a text about 0.5 µs on .NET (measured 2026-11-19).
        private const int MaxScannedTexts = 8;

        public string? LanguageCode { get; }

        // In ordinal order when there are more than MaxScannedTexts, and as given otherwise.
        private string[] OwnTexts { get; }

        public SetDescription(string? languageCode, string[] ownTexts)
        {
            this.LanguageCode = languageCode;

            // A copy, because the generated registrar may hand over an array it keeps.
            if (ownTexts.Length > MaxScannedTexts)
            {
                ownTexts = [.. ownTexts];
                Array.Sort(ownTexts, StringComparer.Ordinal);
            }

            this.OwnTexts = ownTexts;
        }

        /// <summary>
        /// Whether the set's own class words the text of this name.
        /// </summary>
        public bool Words(ReadOnlySpan<char> textName)
        {
            if (this.OwnTexts.Length > MaxScannedTexts)
                return IsAmongSorted(this.OwnTexts, textName);

            // The length first, which tells most names apart without a call: in WebAssembly's interpreter, a call costs
            // more than the comparison.
            foreach (var ownText in this.OwnTexts)
                if (ownText.Length == textName.Length && textName.SequenceEqual(ownText))
                    return true;

            return false;
        }

        /// <summary>
        /// Whether a name is among names in ordinal order, found by halving them.
        /// </summary>
        private static bool IsAmongSorted(string[] sortedNames, ReadOnlySpan<char> name)
        {
            var low = 0;
            var high = sortedNames.Length - 1;

            while (low <= high)
            {
                var middle = low + ((high - low) / 2);
                var order = name.SequenceCompareTo(sortedNames[middle]);

                if (order is 0)
                    return true;

                if (order < 0)
                    high = middle - 1;
                else
                    low = middle + 1;
            }

            return false;
        }
    }
}
