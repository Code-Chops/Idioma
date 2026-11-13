using System.Reflection;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="SupportedLanguagesAttribute"/>: its usage, what it holds, and the metadata the generators and the
/// analyzer read it by. What the build does with it is tested with them.
/// </summary>
public sealed class SupportedLanguagesAttributeTests
{
    [Test]
    public void ShouldTargetAssembliesOnly()
    {
        // Arrange & Act
        var usage = typeof(SupportedLanguagesAttribute).GetCustomAttribute<AttributeUsageAttribute>()!;

        // Assert: one list per assembly.
        usage.ValidOn.ShouldBe(AttributeTargets.Assembly);
        usage.AllowMultiple.ShouldBeFalse();
    }

    [Test]
    public void ShouldHoldDefaultLanguageFirst()
    {
        // Arrange & Act
        var attribute = new SupportedLanguagesAttribute("en", "nl", "de");

        // Assert
        attribute.DefaultLanguage.ShouldBe("en");
        attribute.Languages.ShouldBe(["en", "nl", "de"]);
    }

    [Test]
    public void ShouldAcceptOneLanguage()
    {
        // Arrange & Act
        var attribute = new SupportedLanguagesAttribute("en");

        // Assert
        attribute.Languages.ShouldBe(["en"]);
    }

    [Test]
    public void Languages_ShouldBeReadableFromDeclaringAssembly()
    {
        // Arrange & Act: what an app reads to offer a choice of language; this test project declares en and nl.
        var attribute = typeof(SupportedLanguagesAttributeTests).Assembly.GetCustomAttribute<SupportedLanguagesAttribute>();

        // Assert
        attribute.ShouldNotBeNull().Languages.ShouldBe(["en", "nl"]);
    }

    [Test]
    public void Metadata_ShouldMatchWhatGeneratorsRead()
    {
        // Arrange & Act
        var parameters = typeof(SupportedLanguagesAttribute).GetConstructors().ShouldHaveSingleItem().GetParameters();

        // Assert: the generators match the attribute by this name, and read its arguments by position, so a change here
        // must update them in the same commit.
        typeof(SupportedLanguagesAttribute).FullName.ShouldBe("CodeChops.Idioma.SupportedLanguagesAttribute");
        parameters.Select(parameter => parameter.ParameterType).ShouldBe([typeof(string), typeof(string[])]);
    }
}
