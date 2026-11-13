using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="DiagnosticLocation"/>: what a generator reports lies inside the syntax tree of the code it is
/// about, so that this code can suppress it, as it can suppress what the compiler and the analyzers report.
/// </summary>
public sealed class DiagnosticLocationTests
{
    #region Fixture
    private const string Text = "public sealed class Shop;";

    /// <summary>
    /// The span of the name <c>Shop</c> in <see cref="Text"/>.
    /// </summary>
    private static TextSpan Name { get; } = new(start: 20, length: 4);
    #endregion

    [Test]
    public void Location_ShouldComeBackInsideItsTree()
    {
        // Arrange
        var tree = CSharpSyntaxTree.ParseText(Text);
        var location = Location.Create(tree, Name);

        // Act
        var carried = DiagnosticLocation.From(location);

        // Assert: the same place in the same tree, and equal to another location made of it, so that a pipeline step
        // holding one is reused while the file stays as it is.
        carried.ToLocation().ShouldBe(location);
        carried.ToLocation().SourceTree.ShouldBeSameAs(tree);
        carried.ShouldBe(DiagnosticLocation.From(Location.Create(tree, Name)));
    }

    [Test]
    public void LocationInFileParsedAgain_ShouldDifferFromEarlierOne()
    {
        // Arrange & Act: the same text at the same place, as after an edit elsewhere in the file.
        var earlier = DiagnosticLocation.From(Location.Create(CSharpSyntaxTree.ParseText(Text), Name));
        var later = DiagnosticLocation.From(Location.Create(CSharpSyntaxTree.ParseText(Text), Name));

        // Assert: a location of the tree before the edit must not stand in for one of the tree after it.
        later.ShouldNotBe(earlier);
    }

    [Test]
    public void LocationInNoTree_ShouldBeNoLocation()
    {
        // Arrange & Act & Assert
        default(DiagnosticLocation).ToLocation().ShouldBe(Location.None);
        DiagnosticLocation.From(Location.None).ToLocation().ShouldBe(Location.None);
    }

    [Test]
    [Arguments("", false)]
    [Arguments("#pragma warning disable LOC003", true)]
    [Arguments("[System.Diagnostics.CodeAnalysis.SuppressMessage(\"Design\", \"LOC003\")]", true)]
    public void DiagnosticAtSet_ShouldFollowSuppressionInItsSource(string suppression, bool expected)
    {
        // Arrange: a Dutch set that cannot register itself, which the registration generator reports at the set.
        var source = $$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public partial class ShopTranslationsEN : ITranslations
            {
                public virtual string Checkout => "Checkout";
            }

            {{suppression}}
            public class ShopTranslationsNL(string shopName) : IShopTranslations
            {
                public virtual string Checkout => $"Afrekenen bij {shopName}";
            }
            """;

        // Act
        var diagnostics = GeneratorTestHelper.GetGeneratorDiagnostics(source);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC003");
        diagnostic.IsSuppressed.ShouldBe(expected);
    }

    [Test]
    public void DiagnosticAtDefaultSet_ShouldBeSuppressedByPragma()
    {
        // Arrange: a contract without its Dutch set, which the sets generator reports at the default-language set.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            #pragma warning disable LOC004
            public partial class ShopTranslationsEN : ITranslations
            #pragma warning restore LOC004
            {
                public virtual string Checkout => "Checkout";
            }
            """;

        // Act
        var diagnostics = GeneratorTestHelper.GetGeneratorDiagnostics(source);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC004");
        diagnostic.IsSuppressed.ShouldBeTrue();
    }

    [Test]
    public void DiagnosticAtAttribute_ShouldBeSuppressedByPragma()
    {
        // Arrange: a language listed twice, which the factory generator reports at the attribute.
        const string source = """
            using CodeChops.Idioma;

            #pragma warning disable LOC010
            [assembly: SupportedLanguages("en", "en")]
            #pragma warning restore LOC010
            """;

        // Act
        var diagnostics = GeneratorTestHelper.GetGeneratorDiagnostics(source);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC010");
        diagnostic.IsSuppressed.ShouldBeTrue();
    }
}
