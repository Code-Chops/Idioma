namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="CultureName"/>: the culture of a language code.
/// </summary>
public sealed class CultureNameTests
{
    [Test]
    public void GetCulture_ShouldGiveCultureOfCode()
    {
        // Arrange & Act & Assert
        CultureName.GetCulture("en-GB").ShouldBe(CultureInfo.GetCultureInfo("en-GB"));
    }

    [Test]
    public void GetCulture_ShouldGiveSameCultureAgain()
    {
        // Arrange
        var first = CultureName.GetCulture("nl-BE");

        // Act & Assert: kept, so a code's culture is looked up once.
        CultureName.GetCulture("nl-BE").ShouldBeSameAs(first);
    }

    [Test]
    public void GetCulture_ShouldThrowForEmptyCode()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentException>(() => CultureName.GetCulture(" "));
    }
}
