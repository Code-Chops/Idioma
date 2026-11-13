namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="CultureName"/>: what a language code must look like, and the letter case it is written in.
/// </summary>
public sealed class CultureNameTests
{
    [Test]
    [Arguments("nl")]
    [Arguments("NL")]
    [Arguments("fil")]
    [Arguments("nl-BE")]
    [Arguments("zh-Hans")]
    [Arguments("zh-Hans-CN")]
    [Arguments("es-419")]
    [Arguments("ca-ES-valencia")]
    public void IsValid_ForCultureName_ShouldBeTrue(string code)
    {
        // Arrange & Act & Assert
        CultureName.IsValid(code).ShouldBeTrue();
    }

    [Test]
    [Arguments("")]
    [Arguments(" ")]
    [Arguments("n")]
    [Arguments("nl ")]
    [Arguments(" nl")]
    [Arguments("nl_BE")]
    [Arguments("nl-")]
    [Arguments("-nl")]
    [Arguments("nl--BE")]
    [Arguments("n1")]
    [Arguments("abcdefghi")]
    [Arguments("nl-abcdefghi")]
    [Arguments("n\u00E9")]
    public void IsValid_ForAnythingElse_ShouldBeFalse(string code)
    {
        // Arrange & Act & Assert
        CultureName.IsValid(code).ShouldBeFalse();
    }

    [Test]
    [Arguments("nl", "nl")]
    [Arguments("NL", "nl")]
    [Arguments("NL-be", "nl-BE")]
    [Arguments("ZH-hans-cn", "zh-Hans-CN")]
    [Arguments("ES-419", "es-419")]
    [Arguments("ca-es-VALENCIA", "ca-ES-valencia")]
    [Arguments("en-us-posix", "en-US-posix")]
    public void Normalize_ShouldWriteCodeAsCultureNamesAreWritten(string code, string expected)
    {
        // Arrange & Act & Assert
        CultureName.Normalize(code).ShouldBe(expected);
    }

    [Test]
    public void Normalize_OfCodeAlreadyNormalized_ShouldReturnSameString()
    {
        // Arrange
        const string code = "zh-Hans-CN";

        // Act & Assert: no copy for the usual case.
        CultureName.Normalize(code).ShouldBeSameAs(code);
    }
}
