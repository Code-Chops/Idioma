using System.Reflection;
using System.Runtime.Loader;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="LocalizationRegistrationPart"/>: which sets are registered automatically, in which tier
/// and order, what the build says about a set that can't be, which referenced registrars an assembly calls first, and
/// that the generated code compiles and runs.
/// </summary>
public sealed partial class LocalizationRegistrationPartTests
{
    #region Fixture
    private const string ShopContract = """
        using CodeChops.Idioma;

        namespace TestApp;

        public interface IShopLocalization : ILocalizationSet
        {
            string Checkout { get; }
        }
        """;

    /// <summary>
    /// The default-language set of a contract the generator writes in the same compilation, so a set implementing that
    /// contract binds it to nothing while the generators run.
    /// </summary>
    private const string GeneratedShopContract = """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace TestApp;

        public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
        {
            public virtual string Checkout => "Checkout";
        }
        """;

    /// <summary>
    /// A contract and its English set in an assembly of their own: what a library is to an app.
    /// </summary>
    private static MetadataReference Library { get; } = GeneratorTestHelper.CompileLibrary("ShopLibrary", """
        using CodeChops.Idioma;

        namespace ShopLibrary;

        public interface ILibraryLocalization : ILocalizationSet
        {
            string Checkout { get; }
        }

        public class LibraryLocalizationEn : ILocalizationSet, ILibraryLocalization
        {
            public virtual string Checkout => "Checkout";
        }
        """);

    /// <summary>
    /// A library with a set, compiled with the generators: it has a public registrar an assembly referencing it calls.
    /// </summary>
    private static MetadataReference SetsLibrary { get; } = GeneratorTestHelper.CompileLibraryWithGenerators("SetsLibrary", """
        using CodeChops.Idioma;

        namespace SetsLibrary;

        public interface ISetsLocalization : ILocalizationSet
        {
            string Checkout { get; }
        }

        public sealed class SetsLocalizationEn : ILocalizationSet, ISetsLocalization
        {
            public string Checkout => "Checkout";
        }
        """);

    /// <summary>
    /// Loads in-memory assemblies by name, apart from the test's own: what an app's folder of assemblies is to the app.
    /// </summary>
    private sealed class InMemoryLoadContext(IReadOnlyDictionary<string, byte[]> images) : AssemblyLoadContext(name: null)
    {
        private IReadOnlyDictionary<string, byte[]> Images { get; } = images;

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            return this.Images.TryGetValue(assemblyName.Name!, out var image) ? this.LoadFromStream(new MemoryStream(image)) : null;
        }
    }

    /// <summary>
    /// The wording a text of a generated class gives a user of a culture.
    /// </summary>
    private static string ReadText(Type texts, string name, string culture)
    {
        using var _ = new CulturesScope(culture);

        var wording = (string)texts.GetProperty(name)!.GetValue(null)!;
        return wording;
    }

    private static GeneratorDriverRunResult Run(string source, params MetadataReference[] references)
    {
        return GeneratorTestHelper.Run(LocalizationParts.Registration(), source, references);
    }

    private static string GeneratedText(GeneratorDriverRunResult result)
    {
        var run = result.Results.ShouldHaveSingleItem();
        run.Exception.ShouldBeNull();

        return run.GeneratedSources.ShouldHaveSingleItem().SourceText.ToString();
    }

    private static void ShouldEmitNothing(GeneratorDriverRunResult result)
    {
        var run = result.Results.ShouldHaveSingleItem();
        run.Exception.ShouldBeNull();
        run.GeneratedSources.ShouldBeEmpty();
    }
    #endregion

    [Test]
    public Task SetOfContractInSameAssembly_RegistersAsDefault()
    {
        // Arrange & Act
        var result = Run($$"""
            {{ShopContract}}

            public sealed class ShopLocalizationDe : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Zur Kasse";
            }
            """);

        // Assert
        var generated = result.Results.ShouldHaveSingleItem().GeneratedSources.ShouldHaveSingleItem();
        generated.HintName.ShouldBe("LocalizationRegistrar.g.cs");

        return Verify(generated.SourceText.ToString());
    }

    [Test]
    public void SetOfContractInOtherAssembly_RegistersAsOverride()
    {
        // Arrange & Act: an app's own language for a library's contract.
        var result = Run("""
            namespace TestApp;

            public sealed class LibraryLocalizationDe : CodeChops.Idioma.ILocalizationSet, ShopLibrary.ILibraryLocalization
            {
                public string Checkout => "Zur Kasse";
            }
            """, Library);

        // Assert
        var text = GeneratedText(result);
        text.ShouldContain("RegisterSets(asDefaults: false,");
        text.ShouldContain("new TestApp.LibraryLocalizationDe()");
        text.ShouldNotContain("RegisterSets(asDefaults: true");
    }

    [Test]
    public void SetDerivedFromSetInOtherAssembly_RegistersAsOverride()
    {
        // Arrange & Act: an app rewording one of a library's texts.
        var result = Run("""
            namespace TestApp;

            public sealed class MyLibraryLocalizationEn : ShopLibrary.LibraryLocalizationEn
            {
                public override string Checkout => "Pay";
            }
            """, Library);

        // Assert
        var text = GeneratedText(result);
        text.ShouldContain("RegisterSets(asDefaults: false,");
        text.ShouldNotContain("RegisterSets(asDefaults: true");
    }

    [Test]
    public void SetDerivedFromSetOfProjectReferencedAsCompilation_RegistersAsOverride()
    {
        // Arrange: an IDE hands a generator a project of the same solution as its compilation, where a build hands over
        // the assembly. The LocalizationLanguageCode the library's set implements then has syntax, in a tree of that other
        // compilation, which this one has no semantic model for.
        var library = GeneratorTestHelper.CompileProjectWithGenerators("RegionalLibrary", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace RegionalLibrary;

            public class GridLocalizationEn : ILocalizationSet, IGridLocalization
            {
                public virtual string Colour => "Color";
            }

            public class GridLocalizationNl : ILocalizationSet, IGridLocalization
            {
                public virtual string Colour => "Kleur";
            }

            public class GridLocalizationEnGb : GridLocalizationEn, ILocalizationSet
            {
                string ILocalizationSet.LocalizationLanguageCode => "en-GB";

                public override string Colour => "Colour";
            }
            """);

        // Act
        var result = Run("""
            namespace TestApp;

            public sealed class MyGridLocalizationEnGb : RegionalLibrary.GridLocalizationEnGb
            {
                public override string Colour => "Hue";
            }
            """, library);

        // Assert: the registrar is written as a build writes it.
        var text = GeneratedText(result);
        text.ShouldContain("RegisterSets(asDefaults: false,");
        text.ShouldContain("new TestApp.MyGridLocalizationEnGb()");
        text.ShouldNotContain("RegisterSets(asDefaults: true");
    }

    [Test]
    public void SetDerivedFromConcreteSetInSameAssembly_RegistersAsDefaultAfterItsBase()
    {
        // Arrange & Act: no override (a library's own regional set must not outrank an app's), yet it must still win
        // over its base, which sorted by name would come last.
        var result = Run($$"""
            {{ShopContract}}

            public class ZuluLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            public sealed class AlphaLocalizationEn : ZuluLocalizationEn
            {
                public override string Checkout => "Pay";
            }
            """);

        // Assert
        var text = GeneratedText(result);
        text.ShouldNotContain("RegisterSets(asDefaults: false,");
        text.IndexOf("RegisterSets(asDefaults: true,", StringComparison.Ordinal).ShouldBeLessThan(text.IndexOf("new TestApp.ZuluLocalizationEn()", StringComparison.Ordinal));
        text.IndexOf("new TestApp.ZuluLocalizationEn()", StringComparison.Ordinal).ShouldBeLessThan(text.IndexOf("new TestApp.AlphaLocalizationEn()", StringComparison.Ordinal));
    }

    [Test]
    public void SetDerivedFromAbstractSet_RegistersAsDefault()
    {
        // Arrange & Act: an abstract base is no language of its own, so nothing is being reworded.
        var result = Run($$"""
            {{ShopContract}}

            public abstract class ShopLocalizationBase : ILocalizationSet, IShopLocalization
            {
                public abstract string Checkout { get; }
            }

            public sealed class ShopLocalizationDe : ShopLocalizationBase
            {
                public override string Checkout => "Zur Kasse";
            }
            """);

        // Assert: and its record holds its language alone. The base is never registered, so the set rewords nothing,
        // and the registry never asks which texts it words itself.
        var text = GeneratedText(result);
        text.ShouldContain("RegisterSets(asDefaults: true,");
        text.ShouldNotContain("RegisterSets(asDefaults: false,");
        text.ShouldNotContain("ShopLocalizationBase");
        text.ShouldContain("RecordSet<TestApp.ShopLocalizationDe>(\"de\");");
    }

    [Test]
    public void SetDerivedFromAbstractSetOfGeneratedContract_RegistersAsDefault()
    {
        // Arrange & Act: only the abstract base names the contract generated here. The Dutch set used to be missed, and
        // never registered.
        var result = Run($$"""
            {{GeneratedShopContract}}

            public abstract class ShopLocalizationBase : ILocalizationSet, IShopLocalization
            {
                public abstract string Checkout { get; }
            }

            public sealed class ShopLocalizationNl : ShopLocalizationBase
            {
                public override string Checkout => "Afrekenen";
            }
            """);

        // Assert
        var text = GeneratedText(result);
        text.ShouldContain("new TestApp.ShopLocalizationNl()");
        text.ShouldNotContain("ShopLocalizationBase");
    }

    [Test]
    public void SetRewordingSetOfGeneratedContract_RegistersAfterItsBase()
    {
        // Arrange & Act: its base implements the contract generated here, so it counts as a set it derives from, though
        // sorted by name the base would come last and win.
        var result = Run($$"""
            {{GeneratedShopContract}}

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Afrekenen";
            }

            public sealed class MyShopLocalizationNl : ShopLocalizationNl
            {
                public override string Checkout => "Betalen";
            }
            """);

        // Assert
        var text = GeneratedText(result);
        text.IndexOf("new TestApp.ShopLocalizationNl()", StringComparison.Ordinal)
            .ShouldBeLessThan(text.IndexOf("new TestApp.MyShopLocalizationNl()", StringComparison.Ordinal));
    }

    [Test]
    public void SeveralSets_EmitOneCreationEachInNameOrder()
    {
        // Arrange: declared out of order, and one twice through a partial, to pin the sorted, distinct emission.
        const string source = $$"""
            {{ShopContract}}

            public sealed partial class ShopLocalizationNl : IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }

            public sealed partial class ShopLocalizationNl : ILocalizationSet;

            public sealed class ShopLocalizationDe : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Zur Kasse";
            }
            """;

        // Act
        var text = GeneratedText(Run(source));

        // Assert
        text.Split('\n')
            .Where(static line => line.Contains("static () => new ", StringComparison.Ordinal))
            .Select(static line => line[(line.IndexOf("static () => ", StringComparison.Ordinal) + "static () => ".Length)..].TrimEnd(',', ')', ';', '\r'))
            .ToArray()
            .ShouldBe(
        [
            "new TestApp.ShopLocalizationDe(",
            "new TestApp.ShopLocalizationNl(",
        ]);
    }

    [Test]
    public void GeneratedCode_ShouldCompileWithEveryGenerator()
    {
        // Arrange: both tiers, a generated contract, and a helper in the partial class.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }

            public sealed class MyShopLocalizationEn : ShopLocalizationEn
            {
                public override string Checkout => "Pay";
            }

            public static partial class ShopLocalization
            {
                public static string Shout() => Checkout.ToUpperInvariant();
            }
            """;

        // Act
        var errors = GeneratorTestHelper.CompileWithGenerators(source);

        // Assert
        errors.ShouldBeEmpty();
    }
}
