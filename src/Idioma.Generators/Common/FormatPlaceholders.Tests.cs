using System.Collections.Immutable;
using CodeChops.Idioma.Generators;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="FormatPlaceholders"/>: it reads placeholders as <c>String.Format</c> reads them, and rejects
/// what <c>String.Format</c> rejects.
/// </summary>
public sealed class FormatPlaceholdersTests
{
    [Test]
    [Arguments("Hello", "")]
    [Arguments("Hello, {0}!", "0")]
    [Arguments("{1} of {0}, {1}", "0,1")]
    [Arguments("{0,-10}|{1:N2}|{2 , 5:d}|{3 }", "0,1,2,3")]
    [Arguments("{{0}} and }}", "")]
    [Arguments("{{{0}}}", "0")]
    public void TryRead_ShouldGiveDistinctIndexesInOrder(string wording, string expected)
    {
        // Arrange & Act
        var read = FormatPlaceholders.TryRead(wording, out var placeholders);

        // Assert: String.Format takes the wording with a value for each index.
        read.ShouldBeTrue();
        String.Join(",", placeholders).ShouldBe(expected);
        Should.NotThrow(() => String.Format(CultureInfo.InvariantCulture, wording, 1, 2, 3, 4));
    }

    [Test]
    [Arguments("Use { here")]
    [Arguments("Use } here")]
    [Arguments("{x}")]
    [Arguments("{ 0}")]
    [Arguments("{0")]
    [Arguments("{0,}")]
    [Arguments("{0:{N}}")]
    [Arguments("{1000000}")]
    public void TryRead_ShouldRejectWhatStringFormatRejects(string wording)
    {
        // Arrange & Act
        var read = FormatPlaceholders.TryRead(wording, out var placeholders);

        // Assert
        read.ShouldBeFalse();
        placeholders.ShouldBeEmpty();
        Should.Throw<FormatException>(() => String.Format(CultureInfo.InvariantCulture, wording, 1, 2, 3, 4));
    }

    [Test]
    public void Describe_ShouldNamePlaceholdersAsMessagesDo()
    {
        // Arrange & Act & Assert
        FormatPlaceholders.Describe([]).ShouldBe("no placeholders");
        FormatPlaceholders.Describe([0]).ShouldBe("placeholder {0}");
        FormatPlaceholders.Describe([0, 1]).ShouldBe("placeholders {0} and {1}");
        FormatPlaceholders.Describe(ImmutableArray.Create(0, 1, 2)).ShouldBe("placeholders {0}, {1}, and {2}");
    }
}
