using System.Collections.Immutable;
using CodeChops.Idioma.CodeFixes;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="UnmappedLanguageCodeCodeFixProvider"/>: a set whose <c>LocalizationLanguageCode</c> implements
/// nothing (<c>LOC401</c>) lists <c>ILocalizationSet</c> again, so the property implements it.
/// </summary>
public sealed class UnmappedLanguageCodeCodeFixProviderTests
{
    #region Fixture
    private const string Header = """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "en-GB")]

        namespace TestApp;
        """;

    private const string DefaultSet = """
        public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
        {
            public virtual string Checkout => "Checkout";
        }
        """;

    private const string Body = """
        {
            public string LocalizationLanguageCode => "en-GB";
        }
        """;

    private static Task<ImmutableArray<(string Title, ImmutableArray<string> Sources)>> FixAsync(params string[] sources)
    {
        return CodeFixTestHelper.FixAsync(new UnmappedLanguageCodeCodeFixProvider(), LocalizationParts.Registration(), sources);
    }

    /// <summary>
    /// Neither generator reports anything, so the set serves the language its property returns and no language is
    /// missing.
    /// </summary>
    private static void ShouldReportNothing(params string[] sources)
    {
        GeneratorTestHelper.RunOverFiles(LocalizationParts.Registration(), sources).Results.ShouldHaveSingleItem().Diagnostics.ShouldBeEmpty();
        GeneratorTestHelper.RunOverFiles(LocalizationParts.Sets(), sources).Results.ShouldHaveSingleItem().Diagnostics.ShouldBeEmpty();
    }
    #endregion

    [Test]
    [Arguments("public sealed class ShopLocalizationBritish : ShopLocalizationEn", "public sealed class ShopLocalizationBritish : ShopLocalizationEn, ILocalizationSet")]
    [Arguments("public sealed class ShopLocalizationBritish : ShopLocalizationEn // British", "public sealed class ShopLocalizationBritish : ShopLocalizationEn, ILocalizationSet // British")]
    [Arguments("public sealed class ShopLocalizationBritish : ShopLocalizationEn, System.IDisposable", "public sealed class ShopLocalizationBritish : ShopLocalizationEn, System.IDisposable, ILocalizationSet")]
    public async Task SetWithLanguageCodeThatImplementsNothing_ShouldListTheInterfaceAgain(string declaration, string expectedDeclaration)
    {
        // Arrange
        var body = Body.Replace("}", "    public void Dispose() { }\n}", StringComparison.Ordinal);
        var source = $"{Header}\n\n{DefaultSet}\n\n{declaration}\n{body}";

        // Act
        var fixes = await FixAsync(source);

        // Assert: only the base list changes, and the set then serves en-GB.
        var fix = fixes.ShouldHaveSingleItem();
        fix.Title.ShouldBe("List 'ILocalizationSet' on 'ShopLocalizationBritish'");
        fix.Sources.ShouldHaveSingleItem().ShouldBe($"{Header}\n\n{DefaultSet}\n\n{expectedDeclaration}\n{body}");
        ShouldReportNothing([.. fix.Sources]);
    }

    [Test]
    public async Task FileThatCannotNameTheInterface_ShouldGetItsFullName()
    {
        // Arrange: the file of the set has no using of the package.
        var first = $"{Header}\n\n{DefaultSet}";
        var second = $"namespace TestApp;\n\npublic sealed class ShopLocalizationBritish : ShopLocalizationEn\n{Body}";

        // Act
        var fixes = await FixAsync(first, second);

        // Assert
        var fix = fixes.ShouldHaveSingleItem();
        fix.Sources[0].ShouldBe(first);
        fix.Sources[1].ShouldBe(second.Replace(": ShopLocalizationEn", ": ShopLocalizationEn, CodeChops.Idioma.ILocalizationSet", StringComparison.Ordinal));
        ShouldReportNothing([.. fix.Sources]);
    }

    [Test]
    public async Task PartialSetWithItsBaseListInAnotherFile_ShouldBeFixedThere()
    {
        // Arrange: the part that holds the property and the part that derives, either of which the diagnostic may be on.
        var first = $"{Header}\n\n{DefaultSet}\n\npublic sealed partial class ShopLocalizationBritish\n{Body}";
        const string Second = "using CodeChops.Idioma; namespace TestApp; public sealed partial class ShopLocalizationBritish : ShopLocalizationEn;";

        // Act
        var fixes = await FixAsync(first, Second);

        // Assert
        var fix = fixes.ShouldHaveSingleItem();
        fix.Sources[0].ShouldBe(first);
        fix.Sources[1].ShouldBe(Second.Replace(": ShopLocalizationEn;", ": ShopLocalizationEn, ILocalizationSet;", StringComparison.Ordinal));
        ShouldReportNothing([.. fix.Sources]);
    }
}
