using System.Collections.Immutable;
using CodeChops.Idioma.CodeFixes;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="UnmarkedEnumMemberCodeFixProvider"/>: a member of an enum without <c>[Localized]</c> next to
/// members that have it (<c>LOC503</c>) gets the attribute with its name as the default wording, and the compiler then
/// asks for the other languages.
/// </summary>
public sealed class UnmarkedEnumMemberCodeFixProviderTests
{
    #region Fixture
    private static string Source(string members) => $$"""
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace Shop.Domain;

        public enum ParcelStatus
        {
        {{members}}
        }
        """;

    private static Task<ImmutableArray<(string Title, ImmutableArray<string> Sources)>> FixAsync(string source)
    {
        return CodeFixTestHelper.FixWithGeneratedCodeAsync(new UnmarkedEnumMemberCodeFixProvider(), new LocalizedMemberAnalyzer(), source);
    }
    #endregion

    [Test]
    public async Task MemberAddedLater_ShouldBeMarkedWithItsName()
    {
        // Arrange
        const string members = """
                [Localized(en: "Assigned", nl: "Toegewezen")]
                Assigned,

                // Lost in transit.
                Lost,
            """;

        // Act
        var fixes = await FixAsync(Source(members));

        // Assert: on a line of its own, as the other attribute is, below the comment.
        var fix = fixes.ShouldHaveSingleItem();
        fix.Title.ShouldBe("Mark it [Localized(en: \"Lost\")]");

        var source = fix.Sources.ShouldHaveSingleItem();
        source.ShouldBe(Source("""
                [Localized(en: "Assigned", nl: "Toegewezen")]
                Assigned,

                // Lost in transit.
                [Localized(en: "Lost")]
                Lost,
            """));

        (await GeneratorTestHelper.AnalyzeAsync(new LocalizedMemberAnalyzer(), source, [])).ShouldBeEmpty();

        var error = GeneratorTestHelper.CompileWithGenerators(source).ShouldHaveSingleItem();
        error.Id.ShouldBe("CS7036");
        error.GetMessage(System.Globalization.CultureInfo.InvariantCulture).ShouldContain("'nl'");
    }

    [Test]
    public async Task EnumWithAttributesOnTheLineOfTheirMember_ShouldGetItThereToo()
    {
        // Arrange & Act
        var fixes = await FixAsync(Source("""
                [Localized(en: "Assigned", nl: "Toegewezen")] Assigned,
                [System.Obsolete] Lost,
            """));

        // Assert: before the member's other attributes.
        fixes.ShouldHaveSingleItem().Sources.ShouldHaveSingleItem().ShouldBe(Source("""
                [Localized(en: "Assigned", nl: "Toegewezen")] Assigned,
                [Localized(en: "Lost")] [System.Obsolete] Lost,
            """));
    }

    [Test]
    public async Task FixAll_ShouldMarkEveryMember()
    {
        // Arrange
        var source = Source("""
                [Localized(en: "Assigned", nl: "Toegewezen")]
                Assigned,
                Lost,
                Returned = 9,
            """);

        // Act
        var fixedSource = (await CodeFixTestHelper.FixAllAfterGeneratorsAsync(new UnmarkedEnumMemberCodeFixProvider(), new LocalizedMemberAnalyzer(), source)).ShouldHaveSingleItem();

        // Assert
        fixedSource.ShouldBe(Source("""
                [Localized(en: "Assigned", nl: "Toegewezen")]
                Assigned,
                [Localized(en: "Lost")]
                Lost,
                [Localized(en: "Returned")]
                Returned = 9,
            """));
    }
}
