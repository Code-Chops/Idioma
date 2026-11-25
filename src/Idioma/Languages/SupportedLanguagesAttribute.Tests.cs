using System.Reflection;
using System.Xml.Linq;

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

    [Test]
    public void DocComment_ShouldNameEveryWayToWriteText()
    {
        // Arrange: the docs an IDE shows for the attribute, from the XML file the build writes next to the assembly.
        var path = Path.ChangeExtension(typeof(SupportedLanguagesAttribute).Assembly.Location, ".xml");
        var member = XDocument.Load(path)
            .Descendants("member")
            .Single(static member => String.Equals((string?)member.Attribute("name"), $"T:{typeof(SupportedLanguagesAttribute).FullName}", StringComparison.Ordinal));

        // Act
        var remarks = member.Element("remarks").ShouldNotBeNull().ToString();

        // Assert: a set, an inline text, and an attribute on a member, each of which the attribute's languages hold.
        remarks.ShouldContain("three ways");
        remarks.ShouldContain("T:CodeChops.Idioma.ILocalizationSet");
        remarks.ShouldContain("LocalizedText.Inline(");
        remarks.ShouldContain("[Localized(");
    }
}
