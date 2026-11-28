using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="TransformCache"/>: a driver carried across edits, as an IDE runs one, gives what a cold run over
/// a fresh parse of the same files gives, and reads again only what an edit can have changed.
/// </summary>
public sealed class TransformCacheTests
{
    #region Fixture
    private const string English = """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl", "en-GB")]

        namespace TestApp;

        public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
        {
            public virtual string Cart => "Cart";
            public virtual string Greeting(string name) => $"Hello, {name}!";
        }
        """;

    private const string Dutch = """
        using CodeChops.Idioma;

        namespace TestApp;

        public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
        {
            public string Cart => "Winkelwagen";
            public string Greeting(string name) => $"Hallo, {name}!";
        }
        """;

    private const string British = """
        namespace TestApp;

        public sealed class ShopLocalizationEnGb : ShopLocalizationEn
        {
        }
        """;

    private const string Plain = """
        namespace TestApp;

        public static class Plain
        {
            public static int Count() => 1;
        }
        """;

    /// <summary>
    /// Runs every generator over the files, then over each edit in turn with the same driver, as an IDE does keystroke by
    /// keystroke. Returns the output of that driver, that of a cold run over a fresh parse of the files as they end up,
    /// and how many results the edits read anew.
    /// </summary>
    private static (string Carried, string Cold, int Reads) RunAcrossEdits(string[] files, params (int File, string Find, string Replace)[] edits)
    {
        var trees = files.Select(static (text, index) => CSharpSyntaxTree.ParseText(text, GeneratorTestHelper.ParseOptions, path: $"File{index}.cs"));
        Compilation compilation = CSharpCompilation.Create("TestAssembly", trees, GeneratorTestHelper.References, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = GeneratorTestHelper.CreateFullDriver(GeneratorTestHelper.ParseOptions).RunGenerators(compilation);
        var readsBefore = TransformCache.ReadsOnCurrentThread;

        foreach (var (file, find, replace) in edits)
        {
            var tree = compilation.SyntaxTrees.Single(tree => String.Equals(tree.FilePath, $"File{file}.cs", StringComparison.Ordinal));
            var text = tree.GetText();
            var start = text.ToString().IndexOf(find, StringComparison.Ordinal);
            start.ShouldBeGreaterThanOrEqualTo(0, $"'{find}' isn't in File{file}.cs");

            // An incremental parse, as an IDE makes of each keystroke.
            compilation = compilation.ReplaceSyntaxTree(tree, tree.WithChangedText(text.WithChanges(new TextChange(new TextSpan(start, find.Length), replace))));
            driver = driver.RunGenerators(compilation);
        }

        var reads = TransformCache.ReadsOnCurrentThread - readsBefore;

        var fresh = CSharpCompilation.Create(
            "TestAssembly",
            compilation.SyntaxTrees.Select(static tree => CSharpSyntaxTree.ParseText(tree.GetText(), GeneratorTestHelper.ParseOptions, tree.FilePath)),
            GeneratorTestHelper.References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var cold = GeneratorTestHelper.CreateFullDriver(GeneratorTestHelper.ParseOptions).RunGenerators(fresh).GetRunResult();

        return (Describe(driver.GetRunResult()), Describe(cold), reads);
    }

    private static string Describe(GeneratorDriverRunResult run)
    {
        var sources = run.Results
            .SelectMany(static result => result.GeneratedSources)
            .OrderBy(static source => source.HintName, StringComparer.Ordinal)
            .Select(static source => $"{source.HintName}\n{source.SourceText}");

        var diagnostics = run.Diagnostics
            .Select(static diagnostic => $"{diagnostic.Id} {diagnostic.Location.SourceTree?.FilePath}{diagnostic.Location.SourceSpan} {diagnostic.GetMessage(CultureInfo.InvariantCulture)}")
            .Order(StringComparer.Ordinal);

        return String.Join("\n", sources.Concat(diagnostics));
    }
    #endregion

    [Test]
    public void EditInsideAMethodOfAFileWithoutSets_ShouldReadNothingAnew()
    {
        // Arrange & Act: the keystroke an IDE sees most, which can change no set.
        var (carried, cold, reads) = RunAcrossEdits([English, Dutch, British, Plain], (3, "=> 1;", "=> 2;"), (3, "=> 2;", "=> 3;"));

        // Assert
        carried.ShouldBe(cold);
        reads.ShouldBe(0);
    }

    [Test]
    public void WordingOfASet_ShouldReadThatSetAndTheSetsThatInheritIt()
    {
        // Arrange & Act: the British set inherits the English wording of Cart from another file.
        var (carried, cold, reads) = RunAcrossEdits([English, Dutch, British, Plain], (0, "=> \"Cart\"", "=> \"Trolley\""));

        // Assert: the English set and the British set are read again, and the Dutch one isn't.
        carried.ShouldBe(cold);
        carried.ShouldContain("Trolley");
        reads.ShouldBe(2);
    }

    [Test]
    public void ConstantAWordingReadsInAnotherFile_ShouldBeFollowed()
    {
        // Arrange
        const string brand = """
            namespace TestApp;

            public static class Brand
            {
                public const string Name = "Contoso";
            }
            """;

        var english = English.Replace("=> \"Cart\";", "=> Brand.Name;", StringComparison.Ordinal);

        // Act: a constant's value is a declaration, so every result is read anew.
        var (carried, cold, _) = RunAcrossEdits([english, Dutch, British, brand], (3, "\"Contoso\"", "\"Fabrikam\""));

        // Assert
        carried.ShouldBe(cold);
        carried.ShouldContain("Fabrikam");
    }

    [Test]
    public void DeclarationInAnotherFile_ShouldReadEveryResultAnew()
    {
        // Arrange & Act: an interface of the contract's name, written by hand, changes what the sets list.
        var (carried, cold, reads) = RunAcrossEdits(
            [English, Dutch, British, Plain],
            (3, "public static class Plain", "public interface IShopLocalization { string Cart { get; } }\n\npublic static class Plain"));

        // Assert
        carried.ShouldBe(cold);
        reads.ShouldBeGreaterThanOrEqualTo(3);
    }

    [Test]
    public void DocCommentInAnotherPartOfAPartialSet_ShouldBeFollowed()
    {
        // Arrange: the English set's Cart is declared in a part of its own, in another file.
        var english = English.Replace("public class ShopLocalizationEn", "public partial class ShopLocalizationEn", StringComparison.Ordinal)
            .Replace("    public virtual string Cart => \"Cart\";\n", "", StringComparison.Ordinal)
            .Replace("    public virtual string Cart => \"Cart\";\r\n", "", StringComparison.Ordinal);

        const string part = """
            namespace TestApp;

            public partial class ShopLocalizationEn
            {
                public virtual string Cart => "Cart";
            }
            """;

        // Act: a doc comment is trivia, which no declaration hash holds, in a file the set's part reads.
        var (carried, cold, _) = RunAcrossEdits([english, Dutch, British, part], (3, "    public virtual string Cart", "    /// <summary>The cart.</summary>\n    public virtual string Cart"));

        // Assert
        carried.ShouldBe(cold);
    }

    [Test]
    public void ExcludedMemberOfABaseSetInAnotherFile_ShouldBeFollowed()
    {
        // Arrange & Act: marking a member of the English set as no text changes the contract of every set.
        var (carried, cold, _) = RunAcrossEdits([English, Dutch, British, Plain], (0, "    public virtual string Cart", "    [ExcludeFromLocalizationDiscovery]\n    public virtual string Cart"));

        // Assert
        carried.ShouldBe(cold);
    }

    [Test]
    public void LocalizedMemberEditedInsideAMethodOfItsFile_ShouldReadOnlyThatFileAnew()
    {
        // Arrange: a member marked [Localized] next to a method, and a set in another file.
        const string address = """
            namespace TestApp;

            public sealed class Address
            {
                [CodeChops.Idioma.Localized(en: "City", nl: "Plaats", enGb: "Town")]
                public string City { get; set; } = "";

                public int Count() => 1;
            }
            """;

        // Act
        var (carried, cold, reads) = RunAcrossEdits([English, Dutch, British, address], (3, "=> 1;", "=> 2;"));

        // Assert: the attribute's file changed, so it's read again, and no set is.
        carried.ShouldBe(cold);
        reads.ShouldBe(1);
    }

    [Test]
    public void WordingOfALocalizedMember_ShouldReadOnlyThatAttributeAnew()
    {
        // Arrange
        const string address = """
            namespace TestApp;

            public sealed class Address
            {
                [CodeChops.Idioma.Localized(en: "City", nl: "Plaats", enGb: "Town")]
                public string City { get; set; } = "";
            }
            """;

        // Act: a [Localized] wording is read by its attribute's transform alone, so it declares nothing for the others.
        var (carried, cold, reads) = RunAcrossEdits([English, Dutch, British, address], (3, "en: \"City\"", "en: \"Place\""));

        // Assert
        carried.ShouldBe(cold);
        carried.ShouldContain("\"Place\"");
        reads.ShouldBe(1);
    }
}
