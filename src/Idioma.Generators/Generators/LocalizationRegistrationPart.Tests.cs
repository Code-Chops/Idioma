using System.Reflection;
using System.Runtime.CompilerServices;
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
public sealed class LocalizationRegistrationPartTests
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

        // Assert
        var text = GeneratedText(result);
        text.ShouldContain("RegisterSets(asDefaults: true,");
        text.ShouldNotContain("RegisterSets(asDefaults: false,");
        text.ShouldNotContain("ShopLocalizationBase");
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
    public void SetOfContractGeneratedInSameCompilation_IsDiscoveredAsDefault()
    {
        // Arrange: IShopLocalization is written by LocalizationSetsPart from the default-language set, so it does
        // not exist in the compilation this generator sees; the Dutch set's base type binds to an error type named after
        // it. The set naming a type that no generator writes stays out (it is a compile error anyway).
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }

            public sealed class ShopLocalizationDe : IOtherLocalization
            {
                public string Checkout => "Zur Kasse";
            }
            """;

        // Act
        var text = GeneratedText(Run(source));

        // Assert
        text.ShouldContain("RegisterSets(asDefaults: true,");
        text.ShouldContain("new TestApp.ShopLocalizationEn()");
        text.ShouldContain("new TestApp.ShopLocalizationNl()");
        text.ShouldNotContain("ShopLocalizationDe");
        text.ShouldNotContain("RegisterSets(asDefaults: false,");
    }

    [Test]
    public void ClassImplementingGeneratedContractWithoutILocalizationSet_IsLeftOut()
    {
        // Arrange & Act: the generated contract doesn't derive from ILocalizationSet, so such a class is no set, and
        // can't be passed to the registry either.
        var text = GeneratedText(Run($$"""
            {{GeneratedShopContract}}

            public sealed class ShopLocalizationNl : IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """));

        // Assert
        text.ShouldContain("new TestApp.ShopLocalizationEn()");
        text.ShouldNotContain("ShopLocalizationNl");
    }

    [Test]
    public void SetOfContractDerivingFromILocalizationSet_ShouldRegisterWithoutListingIt()
    {
        // Arrange & Act: a contract written by hand, or generated by an earlier version, derives from ILocalizationSet,
        // so a set of it is one without listing ILocalizationSet itself.
        var text = GeneratedText(Run($$"""
            {{ShopContract}}

            public sealed class ShopLocalizationDe : IShopLocalization
            {
                public string Checkout => "Zur Kasse";
            }
            """));

        // Assert
        text.ShouldContain("new TestApp.ShopLocalizationDe()");
    }

    [Test]
    public void SetOfContractWithoutDeclaredLanguages_IsLeftOut()
    {
        // Arrange: without a default language no contract is generated, so its name is unresolved like any other.
        const string source = """
            namespace TestApp;

            public sealed class ShopLocalizationNl : IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """;

        // Act
        var result = Run(source);

        // Assert
        ShouldEmitNothing(result);
    }

    [Test]
    public void ClassWithUnresolvedBaseTypeThatIsNoGeneratedContract_EmitsNothing()
    {
        // Arrange & Act: an unresolved base type alone (a typo, a missing reference) is no localization set.
        var result = Run("""
            namespace TestApp;

            public sealed class ShopLocalizationDe : IMissingLocalization
            {
                public string Checkout => "Zur Kasse";
            }
            """);

        // Assert
        ShouldEmitNothing(result);
    }

    [Test]
    public void InternalSet_IsDiscovered()
    {
        // Arrange & Act: an internal module initializer can name an internal type, and an internal constructor.
        var result = Run($$"""
            {{ShopContract}}

            internal sealed class ShopLocalizationFr : ILocalizationSet, IShopLocalization
            {
                internal ShopLocalizationFr() { }
                public string Checkout => "Payer";
            }
            """);

        // Assert
        GeneratedText(result).ShouldContain("new TestApp.ShopLocalizationFr()");
    }

    [Test]
    public void RecordSet_IsDiscovered()
    {
        // Arrange & Act
        var result = Run($$"""
            {{ShopContract}}

            public sealed record ShopLocalizationFr : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Payer";
            }
            """);

        // Assert
        GeneratedText(result).ShouldContain("new TestApp.ShopLocalizationFr()");
    }

    [Test]
    public void FileLocalSet_IsSkippedWhileItsSiblingIsEmitted()
    {
        // Arrange: a file-local set reports internal accessibility, but the generated registrar lives in another file
        // and can't name it. The regular sibling shows discovery ran.
        const string source = $$"""
            {{ShopContract}}

            file sealed class ShopLocalizationFr : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Payer";
            }

            public sealed class ShopLocalizationDe : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Zur Kasse";
            }
            """;

        // Act
        var text = GeneratedText(Run(source));

        // Assert
        text.ShouldContain("new TestApp.ShopLocalizationDe()");
        text.ShouldNotContain("ShopLocalizationFr");
    }

    [Test]
    [Arguments("public abstract class ShopLocalizationDe : ILocalizationSet, IShopLocalization { public string Checkout => \"\"; }")]
    [Arguments("public sealed class ShopLocalizationDe<T> : ILocalizationSet, IShopLocalization { public string Checkout => \"\"; }")]
    [Arguments("public static class Container { public sealed class ShopLocalizationDe : ILocalizationSet, IShopLocalization { public string Checkout => \"\"; } }")]
    [Arguments("public sealed class ShopWording : System.IEquatable<ShopWording> { public bool Equals(ShopWording? other) => true; }")]
    public void SetThatDoesNotRegisterItself_EmitsNothing(string declaration)
    {
        // Arrange & Act
        var result = Run($"""
            {ShopContract}

            {declaration}
            """);

        // Assert: nothing, and no warning either: each of these was left out on purpose, or is no set.
        ShouldEmitNothing(result);
        result.Results.ShouldHaveSingleItem().Diagnostics.ShouldBeEmpty();
    }

    [Test]
    public void SubclassOfExcludedSet_IsDiscovered()
    {
        // Arrange & Act: the attribute is not inherited.
        var result = Run($$"""
            {{ShopContract}}

            [ExcludeFromLocalizationDiscovery]
            public class ShopLocalizationDe : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Zur Kasse";
            }

            public sealed class MyShopLocalizationDe : ShopLocalizationDe;
            """);

        // Assert
        var text = GeneratedText(result);
        text.ShouldContain("new TestApp.MyShopLocalizationDe()");
        text.ShouldNotContain("new TestApp.ShopLocalizationDe()");
    }

    [Test]
    [Arguments("public ShopLocalizationDe(string wording) { }", "it has no parameterless constructor")]
    [Arguments("private ShopLocalizationDe() { }", "it has no parameterless constructor")]
    [Arguments("public ShopLocalizationDe(string wording = \"\") { } public ShopLocalizationDe(int count = 0) { }", "it has no parameterless constructor")]
    public void SetWithoutParameterlessConstructor_ReportsLOC208AndIsLeftOut(string constructor, string expectedReason)
    {
        // Arrange & Act: the sibling shows that the rest of the assembly still registers.
        var result = Run($$"""
            {{ShopContract}}

            public sealed class ShopLocalizationDe : ILocalizationSet, IShopLocalization
            {
                {{constructor}}
                public string Checkout => "Zur Kasse";
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """);

        // Assert
        var run = result.Results.ShouldHaveSingleItem();
        var diagnostic = run.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(LocalizationRegistrationPart.CannotRegisterDiagnosticId);
        diagnostic.Id.ShouldBe("LOC208");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().ShouldContain("TestApp.ShopLocalizationDe");
        diagnostic.GetMessage().ShouldContain(expectedReason);
        diagnostic.GetMessage().ShouldContain("[ExcludeFromLocalizationDiscovery]");
        var text = run.GeneratedSources.ShouldHaveSingleItem().SourceText.ToString();
        text.ShouldContain("new TestApp.ShopLocalizationNl()");
        text.ShouldNotContain("new TestApp.ShopLocalizationDe(");
        text.ShouldContain("ShopLocalizationDe>(\"de\");");
    }

    [Test]
    [Arguments("public ShopLocalizationDe(string wording = \"Zur Kasse\") { }")]
    [Arguments("public ShopLocalizationDe(params string[] wordings) { }")]
    [Arguments("public ShopLocalizationDe(string wording = \"Zur Kasse\") { } public ShopLocalizationDe() { }")]
    [Arguments("public ShopLocalizationDe(string wording = \"Zur Kasse\") { } public ShopLocalizationDe(params string[] wordings) { }")]
    [Arguments("private ShopLocalizationDe() { } public ShopLocalizationDe(string wording = \"Zur Kasse\") { }")]
    public void SetWithConstructorCallableWithoutArguments_IsRegistered(string constructor)
    {
        // Arrange & Act: new T() calls it, so the set is registered automatically, like one with a parameterless
        // constructor. Of a constructor with a default and one taking params it calls the first, and a private one is
        // out of its reach.
        var source = $$"""
            {{ShopContract}}

            public sealed class ShopLocalizationDe : ILocalizationSet, IShopLocalization
            {
                {{constructor}}
                public string Checkout => "Zur Kasse";
            }
            """;
        GeneratorTestHelper.CompileWithGenerators(source).ShouldBeEmpty();

        var result = Run($$"""
            {{ShopContract}}

            public sealed class ShopLocalizationDe : ILocalizationSet, IShopLocalization
            {
                {{constructor}}
                public string Checkout => "Zur Kasse";
            }
            """);

        // Assert
        var run = result.Results.ShouldHaveSingleItem();
        run.Diagnostics.ShouldBeEmpty();
        run.GeneratedSources.ShouldHaveSingleItem().SourceText.ToString().ShouldContain("ShopLocalizationDe()");
    }

    [Test]
    public void SetWithPrimaryConstructor_ReportsLOC208()
    {
        // Arrange & Act
        var result = Run($$"""
            {{ShopContract}}

            public sealed class ShopLocalizationDe(string wording) : ILocalizationSet, IShopLocalization
            {
                public string Checkout => wording;
            }
            """);

        // Assert: not registered, and its language recorded for when it's registered by hand.
        var run = result.Results.ShouldHaveSingleItem();
        run.Diagnostics.ShouldHaveSingleItem().Id.ShouldBe("LOC208");
        var text = run.GeneratedSources.ShouldHaveSingleItem().SourceText.ToString();
        text.ShouldNotContain("new TestApp.ShopLocalizationDe(");
        text.ShouldContain("ShopLocalizationDe>(\"de\");");
    }

    [Test]
    public void DefaultLanguageSetWithoutParameterlessConstructor_ReportsNoLOC208()
    {
        // Arrange & Act: such a set gets its contract but no texts, which LOC200 reports. The advice of LOC208, to
        // exclude the set and register it by hand, is no way out for it.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public class ShopLocalizationEn(string shopName) : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => $"Pay at {shopName}";
            }

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Afrekenen";
            }
            """);

        // Assert: nothing reported here. The Dutch set registers, as its contract is written, and the English set can't.
        var run = result.Results.ShouldHaveSingleItem();
        run.Diagnostics.ShouldBeEmpty();
        var text = run.GeneratedSources.ShouldHaveSingleItem().SourceText.ToString();
        text.ShouldContain("new TestApp.ShopLocalizationNl()");
        text.ShouldNotContain("new TestApp.ShopLocalizationEn(");
    }

    [Test]
    [Arguments("ShopLocalization1")]
    [Arguments("ShopLocalizationSet")]
    public void SetWithoutLanguageCode_ReportsLOC208RatherThanThrowingAtLoad(string name)
    {
        // Arrange & Act: registering this would throw while the assembly loads. The letters of "Set" hold no code,
        // because a code starts with a capital, so the set isn't Estonian (et).
        var result = Run($$"""
            {{ShopContract}}

            public sealed class {{name}} : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Zur Kasse";
            }
            """);

        // Assert
        var run = result.Results.ShouldHaveSingleItem();
        var diagnostic = run.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC208");
        diagnostic.GetMessage().ShouldContain("its name doesn't end in a language code");
        run.GeneratedSources.ShouldBeEmpty();
    }

    [Test]
    public void SetNamedAfterDeclaredCodeEndingInDigits_ReportsNoLOC208()
    {
        // Arrange & Act: es-419 (Latin American Spanish) ends in digits, which the name gives through the declaration.
        var text = GeneratedText(Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "es-419")]

            namespace TestApp;

            public interface IShopLocalization : ILocalizationSet
            {
                string Checkout { get; }
            }

            public sealed class ShopLocalizationEs419 : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Pagar";
            }
            """));

        // Assert
        text.ShouldContain("ShopLocalizationEs419>(\"es-419\");");
        text.ShouldContain("ShopLocalizationEs419()");
    }

    [Test]
    public void SetsNamedAfterDeclaredLongCodes_RecordTheirCodesBeforeRegistering()
    {
        // Arrange & Act
        var text = GeneratedText(Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl-BE", "zh-Hans")]

            namespace TestApp;

            public interface IShopLocalization : ILocalizationSet
            {
                string Checkout { get; }
            }

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            public sealed class ShopLocalizationNlBe : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }

            public sealed class ShopLocalizationZhHans : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Jiezhang";
            }
            """));

        // Assert: before the sets register, as the registry files each under the code it reads from it. A two-letter
        // code is recorded too, so that the run time never reads a set's name.
        text.ShouldContain("GeneratedCode.RecordSet<");
        text.ShouldContain("ShopLocalizationNlBe>(\"nl-BE\");");
        text.ShouldContain("ShopLocalizationZhHans>(\"zh-Hans\");");
        text.ShouldContain("ShopLocalizationEn>(\"en\");");
        text.LastIndexOf("RecordSet<", StringComparison.Ordinal)
            .ShouldBeLessThan(text.IndexOf("RegisterSets(asDefaults: true,", StringComparison.Ordinal));
    }

    [Test]
    public void SetDerivingFromAnother_RecordsTheTextsItsOwnClassWords()
    {
        // Arrange & Act: a regional set overriding a property and a method, an app's rewording implementing a text
        // explicitly, and a British set implementing its own LocalizationLanguageCode, which the build records no language for.
        var text = GeneratedText(Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "en-GB")]

            namespace TestApp;

            public interface IShopLocalization : ILocalizationSet
            {
                string Cart { get; }
                string Checkout { get; }
                string Greeting(string name);
            }

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Cart => "Cart";
                public virtual string Checkout => "Checkout";
                public virtual string Greeting(string name) => $"Hello, {name}!";
            }

            public sealed class ShopLocalizationEnGb : ShopLocalizationEn
            {
                public override string Cart => "Basket";
                public override string Greeting(string name) => $"Hello, {name}!";
            }

            public sealed class MyShopLocalizationEn : ShopLocalizationEn, IShopLocalization
            {
                string IShopLocalization.Checkout => "Pay";
            }

            public sealed class ShopLocalizationBritish : ShopLocalizationEn, ILocalizationSet
            {
                string ILocalizationSet.LocalizationLanguageCode => "en-GB";

                public override string Cart => "Basket";
            }
            """));

        // Assert: by name and in order, and nothing of the texts for a set deriving from none.
        text.ShouldContain("RecordSet<TestApp.ShopLocalizationEnGb>(\"en-GB\", \"Cart\", \"Greeting\");");
        text.ShouldContain("RecordSet<TestApp.MyShopLocalizationEn>(\"en\", \"Checkout\");");
        text.ShouldContain("RecordSet<TestApp.ShopLocalizationBritish>(\"en-GB\", \"Cart\");");
        text.ShouldContain("RecordSet<TestApp.ShopLocalizationEn>(\"en\");");
    }

    [Test]
    public void ExcludedSetNamedAfterDeclaredLongCode_RecordsItsCodeWithoutRegistering()
    {
        // Arrange & Act: registered by hand, it must serve the language the build counted it for.
        var text = GeneratedText(Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl-BE")]

            namespace TestApp;

            public interface IShopLocalization : ILocalizationSet
            {
                string Checkout { get; }
            }

            [ExcludeFromLocalizationDiscovery]
            public sealed class ShopLocalizationNlBe : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """));

        // Assert: a registrar with nothing to register is internal.
        text.ShouldContain("internal static class LocalizationRegistrar_TestAssembly");
        text.ShouldContain("ShopLocalizationNlBe>(\"nl-BE\");");
        text.ShouldNotContain("ShopLocalizationNlBe()");
    }

    [Test]
    [Arguments("[assembly: SupportedLanguages(\"en\", \"nl-BE\")]", "string ILocalizationSet.LocalizationLanguageCode => \"nl-BE\";", "ShopLocalizationNlBe>(\"nl-BE\");")]
    [Arguments("[assembly: SupportedLanguages(\"en\", \"nl-BE\")]", "string ILocalizationSet.LocalizationLanguageCode => System.Environment.GetEnvironmentVariable(\"SHOP_LANGUAGE\") ?? \"nl-BE\";", null)]
    [Arguments("[assembly: SupportedLanguages(\"en\", \"nl\")]", "", "ShopLocalizationNlBe>(\"be\");")]
    public void SetWhoseNameGivesNoDeclaredLongCode_RecordsWhatTheBuildReads(string attribute, string languageCode, string? recorded)
    {
        // Arrange & Act: a set implementing its code records it where it's a constant, so that the registry needn't
        // create the set to file it. One the build can't read gets no record. Without nl-BE declared, the name gives 'be'.
        var text = GeneratedText(Run($$"""
            using CodeChops.Idioma;

            {{attribute}}

            namespace TestApp;

            public interface IShopLocalization : ILocalizationSet
            {
                string Checkout { get; }
            }

            public sealed class ShopLocalizationNlBe : ILocalizationSet, IShopLocalization
            {
                {{languageCode}}
                public string Checkout => "Afrekenen";
            }
            """));

        // Assert
        if (recorded is null)
            text.ShouldNotContain("RecordSet");
        else
            text.ShouldContain(recorded);
    }

    [Test]
    public void SetNamedAfterTwoLetterCode_RecordsNoLongerCodeItsNameHappensToEndIn()
    {
        // Arrange & Act: "MessagesAr" ends in the letters of EsAr, the suffix of es-AR, but not at a capital, so it is
        // the Arabic set its last two letters say; "MessagesEsAr" is the Argentinian one.
        var text = GeneratedText(Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "ar", "es-AR")]

            namespace TestApp;

            public interface IMessages : ILocalizationSet
            {
                string Save { get; }
            }

            public sealed class MessagesEn : ILocalizationSet, IMessages
            {
                public string Save => "Save";
            }

            public sealed class MessagesAr : ILocalizationSet, IMessages
            {
                public string Save => "Hifz";
            }

            public sealed class MessagesEsAr : ILocalizationSet, IMessages
            {
                public string Save => "Guardar";
            }
            """));

        // Assert
        text.ShouldContain("MessagesEsAr>(\"es-AR\");");
        text.ShouldContain("MessagesAr>(\"ar\");");
        text.ShouldContain("new TestApp.MessagesAr()");
    }

    [Test]
    [Arguments("public string LocalizationLanguageCode => \"de\";")]
    [Arguments("string ILocalizationSet.LocalizationLanguageCode => \"de\";")]
    public void SetImplementingLanguageCode_NeedsNoLanguageInItsName(string languageCode)
    {
        // Arrange & Act
        var result = Run($$"""
            {{ShopContract}}

            public sealed class ShopLocalization1 : ILocalizationSet, IShopLocalization
            {
                {{languageCode}}
                public string Checkout => "Zur Kasse";
            }
            """);

        // Assert
        var run = result.Results.ShouldHaveSingleItem();
        run.Diagnostics.ShouldBeEmpty();
        run.GeneratedSources.ShouldHaveSingleItem().SourceText.ToString().ShouldContain("new TestApp.ShopLocalization1()");
    }

    [Test]
    public void LanguageCodeOfDerivedSetNotListingInterface_ReportsLOC206()
    {
        // Arrange & Act: the set inherits the mapping of ILocalizationSet from its base, so its own LocalizationLanguageCode is
        // never read, and it registers under the last two letters of its name: uk, which is Ukrainian.
        var result = Run($$"""
            {{ShopContract}}

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            public sealed class ShopLocalizationUk : ShopLocalizationEn
            {
                public string LocalizationLanguageCode => "en-GB";
            }
            """);

        // Assert
        var diagnostic = result.Results.ShouldHaveSingleItem().Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(LocalizationRegistrationPart.UnmappedLanguageCodeDiagnosticId);
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("'ShopLocalizationUk' inherits ILocalizationSet without listing it, so its LocalizationLanguageCode implements nothing");
        diagnostic.GetMessage().ShouldContain("'class ShopLocalizationUk : ShopLocalizationEn, ILocalizationSet'");
    }

    [Test]
    public void LanguageCodeOfSetDerivedFromSetOfGeneratedContract_ReportsLOC206()
    {
        // Arrange & Act: the base implements the contract generated here, which binds to nothing yet. The set registers
        // under be, which is Belarusian.
        var result = Run($$"""
            {{GeneratedShopContract}}

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Afrekenen";
            }

            public sealed class ShopLocalizationBe : ShopLocalizationNl
            {
                public string LocalizationLanguageCode => "nl-BE";
            }
            """);

        // Assert
        var diagnostic = result.Results.ShouldHaveSingleItem().Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(LocalizationRegistrationPart.UnmappedLanguageCodeDiagnosticId);
    }

    [Test]
    [Arguments("ShopLocalizationEn, ILocalizationSet", "public string LocalizationLanguageCode => \"en-GB\";")]
    [Arguments("ShopLocalizationEn, ILocalizationSet", "string ILocalizationSet.LocalizationLanguageCode => \"en-GB\";")]
    [Arguments("ShopLocalizationEn, IShopLocalization", "public string LocalizationLanguageCode => \"en-GB\";")]
    public void LanguageCodeOfDerivedSetListingInterface_ReportsNothing(string baseList, string languageCode)
    {
        // Arrange & Act
        var result = Run($$"""
            {{ShopContract}}

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            public sealed class ShopLocalizationBritish : {{baseList}}
            {
                {{languageCode}}
            }
            """);

        // Assert
        var run = result.Results.ShouldHaveSingleItem();
        run.Diagnostics.ShouldBeEmpty();
        run.GeneratedSources.ShouldHaveSingleItem().SourceText.ToString().ShouldContain("new TestApp.ShopLocalizationBritish()");
    }

    [Test]
    public void LanguageCodeOverridingImplementation_ReportsNothing()
    {
        // Arrange & Act: the override is what the base's implementation runs.
        var result = Run($$"""
            {{ShopContract}}

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string LocalizationLanguageCode => "en";
                public virtual string Checkout => "Checkout";
            }

            public sealed class ShopLocalizationBritish : ShopLocalizationEn
            {
                public override string LocalizationLanguageCode => "en-GB";
            }
            """);

        // Assert
        result.Results.ShouldHaveSingleItem().Diagnostics.ShouldBeEmpty();
    }

    [Test]
    public void LanguageCodeImplementingNothingOnSetWithoutLanguageInName_ReportsLOC206AndLOC208()
    {
        // Arrange & Act: the name gives no language either, so registering it would throw while the assembly loads. It
        // used to be registered, as its property seemed to implement LocalizationLanguageCode.
        var result = Run($$"""
            {{ShopContract}}

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            public sealed class ShopLocalizationBritish2 : ShopLocalizationEn
            {
                public string LocalizationLanguageCode => "en-GB";
            }
            """);

        // Assert
        var run = result.Results.ShouldHaveSingleItem();
        run.Diagnostics.Select(diagnostic => diagnostic.Id).Order(StringComparer.Ordinal).ShouldBe(
        [
            LocalizationRegistrationPart.UnmappedLanguageCodeDiagnosticId,
            LocalizationRegistrationPart.CannotRegisterDiagnosticId,
        ]);
        run.GeneratedSources.ShouldHaveSingleItem().SourceText.ToString().ShouldNotContain("new TestApp.ShopLocalizationBritish2(");
    }

    [Test]
    public void ExcludedSetWithoutParameterlessConstructor_ReportsNothing()
    {
        // Arrange & Act: excluding it is the fix LOC208 names.
        var result = Run($$"""
            {{ShopContract}}

            [ExcludeFromLocalizationDiscovery]
            public sealed class ShopLocalizationDe(string wording) : ILocalizationSet, IShopLocalization
            {
                public string Checkout => wording;
            }
            """);

        // Assert: its language is recorded for when it's registered by hand.
        var run = result.Results.ShouldHaveSingleItem();
        run.Diagnostics.ShouldBeEmpty();
        var text = run.GeneratedSources.ShouldHaveSingleItem().SourceText.ToString();
        text.ShouldNotContain("new TestApp.ShopLocalizationDe(");
        text.ShouldContain("ShopLocalizationDe>(\"de\");");
    }

    [Test]
    public void ExcludedSet_RecordsItsLanguageWithoutRegistering()
    {
        // Arrange & Act: registered by hand, it must serve the language the build counted it for.
        var result = Run($$"""
            {{ShopContract}}

            [ExcludeFromLocalizationDiscovery]
            public sealed class ShopLocalizationDe : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Zur Kasse";
            }
            """);

        // Assert: a registrar with nothing to register is internal.
        var run = result.Results.ShouldHaveSingleItem();
        run.Diagnostics.ShouldBeEmpty();
        var text = run.GeneratedSources.ShouldHaveSingleItem().SourceText.ToString();
        text.ShouldContain("internal static class LocalizationRegistrar_TestAssembly");
        text.ShouldContain("ShopLocalizationDe>(\"de\");");
        text.ShouldNotContain("new TestApp.ShopLocalizationDe(");
    }

    [Test]
    public void GeneratedCodeCallingReferencedRegistrar_ShouldCompile()
    {
        // Arrange & Act
        var errors = GeneratorTestHelper.CompileWithGenerators("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;
            """, SetsLibrary);

        // Assert
        errors.ShouldBeEmpty();
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

    [Test]
    public void AssemblyNameThatIsNoIdentifier_ShouldGiveRegistrarValidNameOthersCall()
    {
        // Arrange: compiling and emitting fails on an invalid name.
        var library = GeneratorTestHelper.CompileLibraryWithGenerators("My-App.Shop", $$"""
            {{ShopContract}}

            public sealed class ShopLocalizationDe : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Zur Kasse";
            }
            """);

        // Act
        var text = GeneratedText(GeneratorTestHelper.RunAsApplication(LocalizationParts.Registration(), "namespace TestApp;", library));

        // Assert
        text.ShouldContain("RegisterReferenced(\"My-App.Shop\", static () => CodeChops.Idioma.Generated.LocalizationRegistrar_My_App_Shop.Register());");
    }

    [Test]
    public Task ApplicationReferencingAssemblyWithSets_CallsItsRegistrarFirst()
    {
        // Arrange & Act: an app without sets of its own still registers what it references, used or not. Without
        // languages it doesn't build (LOC103), and its registrar is written all the same.
        var text = GeneratedText(GeneratorTestHelper.RunAsApplication(LocalizationParts.Registration(), "namespace TestApp;", SetsLibrary));

        // Assert
        text.ShouldNotContain("RegisterLanguages");

        return Verify(text);
    }

    [Test]
    public Task ApplicationDeclaringLanguages_RegistersItsLanguagesFirst()
    {
        // Arrange & Act
        var text = GeneratedText(GeneratorTestHelper.RunAsApplication(LocalizationParts.Registration(), """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("nl", "en")]

            namespace TestApp;
            """, SetsLibrary));

        // Assert: every language, the default first, and before the referenced registrars, as only the first
        // registration counts, and a referenced application (the app a test project tests) must not get there first.
        text.IndexOf("GeneratedCode.RegisterLanguages(\"nl\", \"en\");", StringComparison.Ordinal)
            .ShouldBeLessThan(text.IndexOf("LocalizationRegistrar_SetsLibrary.Register());", StringComparison.Ordinal));

        return Verify(text);
    }

    [Test]
    public void ApplicationDeclaringLanguagesWithNothingElse_StillRegistersItsLanguages()
    {
        // Arrange & Act: no sets and no references; its own inline texts need nothing, but those of a library it loads
        // later fall back to its default language, also for a culture it does not declare.
        var text = GeneratedText(GeneratorTestHelper.RunAsApplication(LocalizationParts.Registration(), """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("nl-BE", "en")]

            namespace TestApp;
            """));

        // Assert: internal, because a host referencing this application registers its own languages first.
        text.ShouldContain("internal static class LocalizationRegistrar_TestAssembly");
        text.ShouldContain("GeneratedCode.RegisterLanguages(\"nl-BE\", \"en\");");
    }

    [Test]
    public void ApplicationReferencingApplicationDeclaringLanguages_RegistersItsOwnLanguagesAlone()
    {
        // Arrange: a Blazor Web App's server referencing its client, which declares languages and writes only inline
        // texts. The server declares its own, and those of the client never count.
        var client = GeneratorTestHelper.CompileApplicationWithGenerators("LanguagesClient", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("nl", "en")]

            namespace LanguagesClient;

            public static class Program
            {
                public static void Main()
                {
                }
            }
            """);

        // Act
        var text = GeneratedText(GeneratorTestHelper.RunAsApplication(LocalizationParts.Registration(), """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;
            """, client));

        // Assert: the client's registrar is internal, as it has nothing the server must register.
        text.ShouldContain("GeneratedCode.RegisterLanguages(\"en\", \"nl\");");
        text.ShouldNotContain("LanguagesClient");
    }

    [Test]
    public void ApplicationWithoutLanguages_ReportsLOC103()
    {
        // Arrange & Act: a host without texts of its own, whose process would have no languages.
        var result = GeneratorTestHelper.RunAsApplication(LocalizationParts.Registration(), "namespace TestApp;", SetsLibrary);

        // Assert: by the name of the assembly, and without a location, as no source says that it's an app.
        var diagnostic = result.Results.ShouldHaveSingleItem().Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(LocalizationRegistrationPart.AppWithoutLanguagesDiagnosticId);
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.Location.ShouldBe(Location.None);
        diagnostic.GetMessage().ShouldBe("'TestAssembly' is an app, but it declares no languages. Add [assembly: SupportedLanguages(\"en\", \"nl\")] with your languages, the default first.");
    }

    [Test]
    public void ApplicationWithoutLanguagesOrReferences_ReportsLOC103()
    {
        // Arrange & Act: nothing uses the package but the app itself.
        var result = GeneratorTestHelper.RunAsApplication(LocalizationParts.Registration(), "namespace TestApp;");

        // Assert
        result.Results.ShouldHaveSingleItem().Diagnostics.ShouldHaveSingleItem().Id.ShouldBe(LocalizationRegistrationPart.AppWithoutLanguagesDiagnosticId);
    }

    [Test]
    public void AndroidApplicationWithoutLanguages_ReportsLOC103()
    {
        // Arrange & Act: built as a library, and an app by the Android SDK's property.
        var result = GeneratorTestHelper.RunWithBuildProperties(
            LocalizationParts.Registration(),
            "namespace TestApp;",
            [SetsLibrary],
            (LocalizationRegistrationPart.AndroidApplicationProperty, "true"));

        // Assert
        result.Results.ShouldHaveSingleItem().Diagnostics.ShouldHaveSingleItem().Id.ShouldBe(LocalizationRegistrationPart.AppWithoutLanguagesDiagnosticId);
    }

    [Test]
    public void LibraryWithoutLanguages_ReportsNoLOC103()
    {
        // Arrange & Act: a library's attribute describes only its own texts, and it has none.
        var result = Run("namespace TestApp; public static class Nothing;", SetsLibrary);

        // Assert
        result.Results.ShouldHaveSingleItem().Diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("\"en\", \"nl\"")]
    [Arguments("\"e\", \"nl\"")]
    public void ApplicationWithLanguagesOrAnInvalidAttribute_ReportsNoLOC103(string languages)
    {
        // Arrange & Act: an invalid attribute is there, and LOC101 says what is wrong with it.
        var result = GeneratorTestHelper.RunAsApplication(LocalizationParts.Registration(), $$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages({{languages}})]

            namespace TestApp;
            """, SetsLibrary);

        // Assert
        result.Results.ShouldHaveSingleItem().Diagnostics.ShouldNotContain(diagnostic => diagnostic.Id == LocalizationRegistrationPart.AppWithoutLanguagesDiagnosticId);
    }

    [Test]
    public void LibraryDeclaringLanguages_RegistersNoLanguages()
    {
        // Arrange & Act: a library's languages must not decide for the app that uses it.
        var text = GeneratedText(Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("de")]

            namespace TestApp;

            public interface IShopLocalization : ILocalizationSet
            {
                string Checkout { get; }
            }

            public sealed class ShopLocalizationDe : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Zur Kasse";
            }
            """));

        // Assert
        text.ShouldNotContain("RegisterLanguages");
    }

    [Test]
    public void AndroidApplicationDeclaringLanguages_RegistersItsLanguages()
    {
        // Arrange & Act: an Android app builds as a library, as it has no Main, so only the Android SDK's own property
        // tells it from one.
        var text = GeneratedText(GeneratorTestHelper.RunWithBuildProperties(LocalizationParts.Registration(), """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("nl", "en")]

            namespace TestApp;
            """, [], (LocalizationRegistrationPart.AndroidApplicationProperty, "true")));

        // Assert: as for an application built as an executable.
        text.ShouldContain("internal static class LocalizationRegistrar_TestAssembly");
        text.ShouldContain("GeneratedCode.RegisterLanguages(\"nl\", \"en\");");
    }

    [Test]
    public void AndroidApplicationReferencingAssemblyWithSets_CallsItsRegistrar()
    {
        // Arrange & Act: no sets of its own, like the plain library below, which emits nothing.
        var text = GeneratedText(GeneratorTestHelper.RunWithBuildProperties(
            LocalizationParts.Registration(),
            "namespace TestApp;",
            [SetsLibrary],
            (LocalizationRegistrationPart.AndroidApplicationProperty, "true")));

        // Assert
        text.ShouldContain("RegisterReferenced(\"SetsLibrary\", static () => CodeChops.Idioma.Generated.LocalizationRegistrar_SetsLibrary.Register());");
    }

    [Test]
    [Arguments("false")]
    [Arguments("")]
    public void AndroidLibraryDeclaringLanguages_RegistersNoLanguages(string androidApplication)
    {
        // Arrange & Act: every Android project has the property, and a library has it false.
        var result = GeneratorTestHelper.RunWithBuildProperties(LocalizationParts.Registration(), """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("nl", "en")]

            namespace TestApp;
            """, [], (LocalizationRegistrationPart.AndroidApplicationProperty, androidApplication));

        // Assert: nothing to register, as for any library that only declares languages.
        ShouldEmitNothing(result);
    }

    [Test]
    public void ApplicationReferencingAssemblyUnderExternAliasOnly_DoesNotCallItsRegistrar()
    {
        // Arrange & Act: code without the alias can't name the registrar, so the assembly registers its sets when it
        // loads, which its use under the alias makes it do.
        var result = GeneratorTestHelper.RunAsApplication(LocalizationParts.Registration(), "namespace TestApp;", SetsLibrary.WithAliases(["SetsAlias"]));

        // Assert: nothing else to register, so no registrar at all.
        ShouldEmitNothing(result);
    }

    [Test]
    public void ApplicationReferencingAssemblyUnderExternAliasAndGlobal_CallsItsRegistrar()
    {
        // Arrange & Act
        var text = GeneratedText(GeneratorTestHelper.RunAsApplication(LocalizationParts.Registration(), "namespace TestApp;", SetsLibrary.WithAliases(["SetsAlias", "global"])));

        // Assert
        text.ShouldContain("LocalizationRegistrar_SetsLibrary.Register());");
    }

    [Test]
    public void ApplicationReferencingAssemblyUnderExternAlias_Compiles()
    {
        // Arrange: the app uses the library under its alias only, as a build with Aliases on the reference does.
        const string source = """
            extern alias SetsAlias;

            namespace TestApp;

            public static class Usage
            {
                public static string Read() => new SetsAlias::SetsLibrary.SetsLocalizationEn().Checkout;
            }
            """;

        // Act
        var errors = GeneratorTestHelper.CompileWithGenerators(source, SetsLibrary.WithAliases(["SetsAlias"]));

        // Assert
        errors.ShouldBeEmpty();
    }

    [Test]
    [Arguments("public static class Rewordings { public sealed class MyLibraryLocalizationEn : ShopLibrary.LibraryLocalizationEn { public override string Checkout => \"Pay now\"; } }", "MyLibraryLocalizationEn", "nested in 'Rewordings'")]
    [Arguments("file sealed class MyLibraryLocalizationEn : ShopLibrary.LibraryLocalizationEn { public override string Checkout => \"Pay now\"; }", "MyLibraryLocalizationEn", "file-local")]
    [Arguments("public static class Rewordings { public sealed class LibraryLocalizationDe : ILocalizationSet, ShopLibrary.ILibraryLocalization { public string Checkout => \"Zur Kasse\"; } }", "LibraryLocalizationDe", "nested in 'Rewordings'")]
    public void NestedOrFileLocalSetRewordingAnotherAssembly_ReportsCannotRegister(string declaration, string expectedName, string expectedReason)
    {
        // Arrange & Act: generated code can't name it, so the app would keep showing the library's wording.
        var result = Run($"""
            namespace TestApp;

            {declaration}
            """, Library);

        // Assert
        var diagnostic = result.Results.ShouldHaveSingleItem().Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(LocalizationRegistrationPart.CannotRegisterDiagnosticId);
        diagnostic.GetMessage().ShouldContain(expectedName);
        diagnostic.GetMessage().ShouldContain(expectedReason);
        diagnostic.GetMessage().ShouldContain("[ExcludeFromLocalizationDiscovery]");
    }

    [Test]
    [Arguments("public static class Rewordings { [CodeChops.Idioma.ExcludeFromLocalizationDiscovery] public sealed class MyLibraryLocalizationEn : ShopLibrary.LibraryLocalizationEn { } }")]
    [Arguments("public static class Rewordings { public abstract class MyLibraryLocalizationEn : ShopLibrary.LibraryLocalizationEn { } }")]
    [Arguments("public static class Rewordings { public sealed class MyLibraryLocalizationEn<T> : ShopLibrary.LibraryLocalizationEn { } }")]
    public void NestedSetRewordingAnotherAssemblyThatIsExcludedOrNoInstance_ReportsNothing(string declaration)
    {
        // Arrange & Act
        var result = Run($"""
            namespace TestApp;

            {declaration}
            """, Library);

        // Assert
        result.Results.ShouldHaveSingleItem().Diagnostics.ShouldBeEmpty();
    }

    [Test]
    public void AssemblyWithSetsReferencingAssemblyWithSets_CallsItsRegistrarBeforeRegisteringItsOwn()
    {
        // Arrange & Act
        var text = GeneratedText(Run($$"""
            {{ShopContract}}

            public sealed class ShopLocalizationDe : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Zur Kasse";
            }
            """, SetsLibrary));

        // Assert
        text.ShouldContain("public static class LocalizationRegistrar_TestAssembly");
        text.IndexOf("LocalizationRegistrar_SetsLibrary.Register());", StringComparison.Ordinal).ShouldBeLessThan(text.IndexOf("RegisterSets(asDefaults: true,", StringComparison.Ordinal));
    }

    [Test]
    public void LibraryDeclaringLanguagesReferencingAssemblyWithSets_CallsItsRegistrar()
    {
        // Arrange & Act: its build counted the referenced sets toward its languages, so it makes sure they register.
        var text = GeneratedText(Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;
            """, SetsLibrary));

        // Assert
        text.ShouldContain("RegisterReferenced(\"SetsLibrary\", static () => CodeChops.Idioma.Generated.LocalizationRegistrar_SetsLibrary.Register());");
    }

    [Test]
    public void PlainLibraryReferencingAssemblyWithSets_EmitsNothing()
    {
        // Arrange & Act: no sets, no languages, and no application: the app above it calls the registrar.
        var result = Run("namespace TestApp; public static class Nothing;", SetsLibrary);

        // Assert
        ShouldEmitNothing(result);
    }

    [Test]
    public void ReferencedAssemblyWithoutSets_IsNotCalled()
    {
        // Arrange: a library declaring languages but holding no sets gets an internal registrar, which no one can call.
        var languagesOnly = GeneratorTestHelper.CompileLibraryWithGenerators("LanguagesOnly", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace LanguagesOnly;
            """, SetsLibrary);

        // Act
        var text = GeneratedText(GeneratorTestHelper.RunAsApplication(LocalizationParts.Registration(), "namespace TestApp;", SetsLibrary, languagesOnly));

        // Assert
        text.ShouldContain("LocalizationRegistrar_SetsLibrary.Register());");
        text.ShouldNotContain("LanguagesOnly");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void AppReferencingAssemblyItNeverUses_ShouldRegisterItsSetsAtStart()
    {
        // Arrange: a library, an add-on holding only a German set for the library's contract, and an app that declares
        // German and never touches the add-on. Before the app called the add-on's registrar, the add-on never loaded,
        // while its set still satisfied the app's LOC700 check.
        var library = GeneratorTestHelper.EmitLibraryWithGenerators("UnusedAddOnLibrary", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace UnusedAddOnLibrary;

            public class AddOnShopLocalizationEn : ILocalizationSet, IAddOnShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            public sealed class AddOnShopLocalizationNl : ILocalizationSet, IAddOnShopLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """);
        var addOn = GeneratorTestHelper.EmitLibraryWithGenerators("UnusedAddOn", """
            namespace UnusedAddOn;

            public sealed class AddOnShopLocalizationDe : CodeChops.Idioma.ILocalizationSet, UnusedAddOnLibrary.IAddOnShopLocalization
            {
                public string Checkout => "Zur Kasse";
            }
            """, MetadataReference.CreateFromImage(library));
        var app = GeneratorTestHelper.EmitLibraryWithGenerators("UnusedAddOnApp", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]

            namespace UnusedAddOnApp;
            """, MetadataReference.CreateFromImage(library), MetadataReference.CreateFromImage(addOn));
        var context = new InMemoryLoadContext(new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["UnusedAddOnLibrary"] = library,
            ["UnusedAddOn"] = addOn,
            ["UnusedAddOnApp"] = app,
        });

        // Act: the app's module initializer, as at its start.
        var appAssembly = context.LoadFromAssemblyName(new AssemblyName("UnusedAddOnApp"));
        RuntimeHelpers.RunModuleConstructor(appAssembly.ManifestModule.ModuleHandle);

        // Assert: read as the app declaring German sees it; this test process declares only en and nl itself, so its
        // own app languages would send de-DE to English.
        context.Assemblies.ShouldContain(assembly => String.Equals(assembly.GetName().Name, "UnusedAddOn", StringComparison.Ordinal));
        var contract = context.LoadFromAssemblyName(new AssemblyName("UnusedAddOnLibrary")).GetType("UnusedAddOnLibrary.IAddOnShopLocalization")!;
        var german = LocalizationRegistry.Current.Find(contract, CultureInfo.GetCultureInfo("de-DE")).ShouldNotBeNull();
        contract.GetProperty("Checkout")!.GetValue(german).ShouldBe("Zur Kasse");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void AppReferencingAssemblyThatIsNotThere_ShouldStartAndRegisterItsOwnSets()
    {
        // Arrange: the app's folder lacks a library it references, as a Blazor WebAssembly app lacks an assembly it
        // loads lazily. Before the call to the library's registrar was guarded, the app's module initializer threw, so
        // the app did not start.
        var library = GeneratorTestHelper.EmitLibraryWithGenerators("AbsentLibrary", """
            using CodeChops.Idioma;

            namespace AbsentLibrary;

            public interface IAbsentLocalization : ILocalizationSet
            {
                string Checkout { get; }
            }

            public sealed class AbsentLocalizationEn : ILocalizationSet, IAbsentLocalization
            {
                public string Checkout => "Checkout";
            }
            """);
        var app = GeneratorTestHelper.EmitLibraryWithGenerators("AbsentLibraryApp", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace AbsentLibraryApp;

            public class AbsentAppLocalizationEn : ILocalizationSet, IAbsentAppLocalization
            {
                public string Checkout => "Checkout";
            }

            public sealed class AbsentAppLocalizationNl : ILocalizationSet, IAbsentAppLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """, MetadataReference.CreateFromImage(library));
        var context = new InMemoryLoadContext(new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["AbsentLibraryApp"] = app,
        });

        // Act: the app's module initializer, as at its start.
        var appAssembly = context.LoadFromAssemblyName(new AssemblyName("AbsentLibraryApp"));
        RuntimeHelpers.RunModuleConstructor(appAssembly.ManifestModule.ModuleHandle);

        // Assert
        context.Assemblies.ShouldNotContain(assembly => String.Equals(assembly.GetName().Name, "AbsentLibrary", StringComparison.Ordinal));
        var texts = appAssembly.GetType("AbsentLibraryApp.AbsentAppLocalization")!;
        ReadText(texts, "Checkout", "nl-NL").ShouldBe("Afrekenen");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void AssemblyThatArrivesAfterAppStarted_ShouldBeUsableAndRegisterItsSetsWhenItsCodeFirstRuns()
    {
        // Arrange: the app starts without a library it references, as a Blazor WebAssembly app starts without an
        // assembly it loads lazily, and the library arrives afterwards. This runtime tries again when a call into an
        // assembly failed before, so the test passed with the call alone as well; the WebAssembly runtime does not,
        // which is what the test below is for.
        var library = GeneratorTestHelper.EmitLibraryWithGenerators("LateLibrary", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace LateLibrary;

            public class LateLocalizationEn : ILocalizationSet, ILateLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            public sealed class LateLocalizationNl : ILocalizationSet, ILateLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """);
        var app = GeneratorTestHelper.EmitLibraryWithGenerators("LateLibraryApp", """
            using System.Globalization;
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace LateLibraryApp;

            public static class Reader
            {
                public static string ReadCheckout() => LateLibrary.LateLocalization.Checkout;
            }
            """, MetadataReference.CreateFromImage(library));
        var images = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["LateLibraryApp"] = app,
        };
        var context = new InMemoryLoadContext(images);
        var appAssembly = context.LoadFromAssemblyName(new AssemblyName("LateLibraryApp"));
        RuntimeHelpers.RunModuleConstructor(appAssembly.ManifestModule.ModuleHandle);
        context.Assemblies.ShouldNotContain(assembly => String.Equals(assembly.GetName().Name, "LateLibrary", StringComparison.Ordinal));

        // Act: the library arrives, and the app's code calls into it for the first time.
        images["LateLibrary"] = library;
        string checkout;

        using (new CulturesScope("nl-NL"))
            checkout = (string)appAssembly.GetType("LateLibraryApp.Reader")!.GetMethod("ReadCheckout")!.Invoke(obj: null, parameters: null)!;

        // Assert: the library's own registrar ran with its code, so its Dutch set words the text.
        checkout.ShouldBe("Afrekenen");
    }

    [Test]
    public void ReferencedRegistrar_IsCalledOnlyAfterItsAssemblyIsFoundByName()
    {
        // Arrange & Act: the WebAssembly runtime remembers a call into an assembly that was not there, and refuses
        // every later use of that assembly from the calling one, also once it has loaded lazily. Seen in a browser: a
        // lookup by name that fails leaves nothing behind, and a call that fails does.
        var text = GeneratedText(GeneratorTestHelper.RunAsApplication(LocalizationParts.Registration(), "namespace TestApp;", SetsLibrary));

        // Assert: the lookup comes first, inside the same try, so a missing assembly skips the call.
        var guard = text.Substring(text.IndexOf("private static void RegisterReferenced(string assemblyName, System.Action register)", StringComparison.Ordinal));
        var lookup = guard.IndexOf("System.Reflection.Assembly.Load(new System.Reflection.AssemblyName { Name = assemblyName });", StringComparison.Ordinal);
        var call = guard.IndexOf("register();", StringComparison.Ordinal);
        var handler = guard.IndexOf("catch (System.IO.FileNotFoundException)", StringComparison.Ordinal);

        lookup.ShouldBeGreaterThan(guard.IndexOf("try", StringComparison.Ordinal));
        call.ShouldBeGreaterThan(lookup);
        handler.ShouldBeGreaterThan(call);
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void SetDerivedFromAbstractSetOfGeneratedContract_ShouldShowItsLanguageAtRunTime()
    {
        // Arrange: only the abstract base names the contract generated here. The Dutch set used to be left out of the
        // registration, so Dutch users saw English.
        var image = GeneratorTestHelper.EmitLibraryWithGenerators("AbstractBaseLibrary", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace AbstractBaseLibrary;

            public class BaseShopLocalizationEn : ILocalizationSet, IBaseShopLocalization
            {
                public string Checkout => "Checkout";
            }

            public abstract class BaseShopLocalizationBase : ILocalizationSet, IBaseShopLocalization
            {
                public abstract string Checkout { get; }
            }

            public sealed class BaseShopLocalizationNl : BaseShopLocalizationBase
            {
                public override string Checkout => "Afrekenen";
            }
            """);
        var context = new InMemoryLoadContext(new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["AbstractBaseLibrary"] = image,
        });

        // Act: the library's module initializer, as when it loads.
        var assembly = context.LoadFromAssemblyName(new AssemblyName("AbstractBaseLibrary"));
        RuntimeHelpers.RunModuleConstructor(assembly.ManifestModule.ModuleHandle);

        // Assert
        var texts = assembly.GetType("AbstractBaseLibrary.BaseShopLocalization")!;
        ReadText(texts, "Checkout", "nl-NL").ShouldBe("Afrekenen");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void SetNamedAfterDeclaredLongCode_ShouldServeItAtRunTime()
    {
        // Arrange: neither set implements LocalizationLanguageCode; the rewording's name gives no language of its own.
        var image = GeneratorTestHelper.EmitLibraryWithGenerators("RegionalNameLibrary", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl-BE")]

            namespace RegionalNameLibrary;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
                public virtual string Total => "Total";
            }

            public class ShopLocalizationNlBe : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Afrekenen";
                public virtual string Total => "Totaal";
            }

            public sealed class ShopRewording : ShopLocalizationNlBe
            {
                public override string Checkout => "Betalen";
            }
            """);
        var context = new InMemoryLoadContext(new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["RegionalNameLibrary"] = image,
        });

        // Act: the library's module initializer, as when it loads.
        var assembly = context.LoadFromAssemblyName(new AssemblyName("RegionalNameLibrary"));
        RuntimeHelpers.RunModuleConstructor(assembly.ManifestModule.ModuleHandle);

        // Assert: both serve nl-BE, not be; the rewording wins over its base, and keeps the base's other texts.
        var belgian = (ILocalizationSet)Activator.CreateInstance(assembly.GetType("RegionalNameLibrary.ShopLocalizationNlBe")!)!;
        var rewording = (ILocalizationSet)Activator.CreateInstance(assembly.GetType("RegionalNameLibrary.ShopRewording")!)!;
        belgian.LocalizationLanguageCode.ShouldBe("nl-BE");
        rewording.LocalizationLanguageCode.ShouldBe("nl-BE");

        var texts = assembly.GetType("RegionalNameLibrary.ShopLocalization")!;
        ReadText(texts, "Checkout", "nl-BE").ShouldBe("Betalen");
        ReadText(texts, "Total", "nl-BE").ShouldBe("Totaal");
    }

    [Test]
    public void SetWithRequiredMember_ReportsLOC208AndIsLeftOut()
    {
        // Arrange: new T() sets no member, so the generated registration would not compile (CS9035).
        const string source = $$"""
            {{ShopContract}}

            public sealed class ShopLocalizationDe : ILocalizationSet, IShopLocalization
            {
                public required string Checkout { get; init; }
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """;

        // Act
        var run = Run(source).Results.ShouldHaveSingleItem();
        var errors = GeneratorTestHelper.CompileWithGenerators(source);

        // Assert
        var diagnostic = run.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC208");
        diagnostic.GetMessage().ShouldContain("it has a required member ('Checkout'), which generated code can't set");
        var text = run.GeneratedSources.ShouldHaveSingleItem().SourceText.ToString();
        text.ShouldContain("new TestApp.ShopLocalizationNl()");
        text.ShouldNotContain("new TestApp.ShopLocalizationDe(");
        errors.ShouldBeEmpty();
    }

    [Test]
    public void SetWhoseConstructorSetsItsRequiredMembers_IsRegistered()
    {
        // Arrange
        const string source = $$"""
            {{ShopContract}}

            public sealed class ShopLocalizationDe : ILocalizationSet, IShopLocalization
            {
                [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
                public ShopLocalizationDe()
                {
                    this.Checkout = "Zur Kasse";
                }

                public required string Checkout { get; init; }
            }
            """;

        // Act
        var run = Run(source).Results.ShouldHaveSingleItem();
        var errors = GeneratorTestHelper.CompileWithGenerators(source);

        // Assert
        run.Diagnostics.ShouldBeEmpty();
        run.GeneratedSources.ShouldHaveSingleItem().SourceText.ToString().ShouldContain("new TestApp.ShopLocalizationDe()");
        errors.ShouldBeEmpty();
    }

    [Test]
    [Arguments("public string LocalizationLanguageCode => \"en_GB\";", "en_GB")]
    [Arguments("string ILocalizationSet.LocalizationLanguageCode => \"\";", "")]
    [Arguments("public string LocalizationLanguageCode => \"english\" + \" \";", "english ")]
    public void SetWithLanguageCodeThatIsNoCultureName_ReportsLOC207(string languageCode, string expectedCode)
    {
        // Arrange & Act: in an assembly without [assembly: SupportedLanguages], where no language check looks at the
        // set. Registering it throws when the assembly loads, far from the cause.
        var result = Run($$"""
            {{ShopContract}}

            public sealed class ShopLocalizationBritish : ILocalizationSet, IShopLocalization
            {
                {{languageCode}}
                public string Checkout => "Checkout";
            }
            """);

        // Assert: at the property that holds the code.
        var diagnostic = result.Results.ShouldHaveSingleItem().Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(LocalizationRegistrationPart.InvalidLanguageCodeDiagnosticId);
        diagnostic.Id.ShouldBe("LOC207");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldStartWith($"'ShopLocalizationBritish' gives \"{expectedCode}\" as its LocalizationLanguageCode, which isn't a culture name");
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).ShouldBe("LocalizationLanguageCode");
    }

    [Test]
    [Arguments("public string LocalizationLanguageCode => \"en-GB\";")]
    [Arguments("public string LocalizationLanguageCode => \"EN\";")]
    [Arguments("public string LocalizationLanguageCode => \"zh-Hans-CN\";")]
    [Arguments("public string LocalizationLanguageCode => System.Globalization.CultureInfo.CurrentUICulture.Name;")]
    public void SetWithLanguageCodeThatIsCultureNameOrNoConstant_ReportsNothing(string languageCode)
    {
        // Arrange & Act
        var result = Run($$"""
            {{ShopContract}}

            public sealed class ShopLocalizationBritish : ILocalizationSet, IShopLocalization
            {
                {{languageCode}}
                public string Checkout => "Checkout";
            }
            """);

        // Assert
        result.Results.ShouldHaveSingleItem().Diagnostics.ShouldBeEmpty();
    }

    [Test]
    public void LanguageCodeThatIsNoCultureName_IsReportedOnceForTheSetsInheritingIt()
    {
        // Arrange & Act: each of the three sets carries the code of the class they derive from.
        var result = Run($$"""
            {{ShopContract}}

            public abstract class BritishWording : ILocalizationSet, IShopLocalization
            {
                public string LocalizationLanguageCode => "en_GB";
                public virtual string Checkout => "Checkout";
            }

            public sealed class ShopWording : BritishWording;

            public sealed class GiftWording : BritishWording;

            [ExcludeFromLocalizationDiscovery]
            public sealed class BookWording : BritishWording;
            """);

        // Assert
        var diagnostic = result.Results.ShouldHaveSingleItem().Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC207");
        diagnostic.GetMessage().ShouldStartWith("'BritishWording' gives \"en_GB\" as its LocalizationLanguageCode");
    }

    [Test]
    public void ExcludedSetWithLanguageCodeThatIsNoCultureName_ReportsLOC207()
    {
        // Arrange & Act: registered by hand, the set throws just the same, where it is registered.
        var result = Run($$"""
            {{ShopContract}}

            [ExcludeFromLocalizationDiscovery]
            public sealed class ShopWording(string checkout) : ILocalizationSet, IShopLocalization
            {
                public string LocalizationLanguageCode => "en_GB";
                public string Checkout => checkout;
            }
            """);

        // Assert
        result.Results.ShouldHaveSingleItem().Diagnostics.ShouldHaveSingleItem().Id.ShouldBe("LOC207");
    }

    [Test]
    public void SetWithLanguageCodeThatIsNoCultureNameInAssemblyDeclaringLanguages_ReportsLOC207Alone()
    {
        // Arrange & Act: with every generator at work. Taken for a language, the code would be reported as unlisted,
        // with the advice to list it, which the attribute then refuses (LOC101).
        var diagnostics = GeneratorTestHelper.GetGeneratorDiagnostics($$"""
            {{GeneratedShopContract}}

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Afrekenen";
            }

            public class ShopLocalizationBritish : ShopLocalizationEn, ILocalizationSet
            {
                string ILocalizationSet.LocalizationLanguageCode => "en_GB";
            }
            """);

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("LOC207");
    }

    [Test]
    public void LanguageCodeImplementingNothing_IsReportedOnceAtTheClassDeclaringIt()
    {
        // Arrange & Act: the two sets deriving from the class carry its LocalizationLanguageCode, and have nothing to fix.
        var result = Run($$"""
            {{ShopContract}}

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            public class ShopLocalizationUk : ShopLocalizationEn
            {
                public string LocalizationLanguageCode => "en-GB";
            }

            public class GiftLocalizationUk : ShopLocalizationUk;

            public sealed class BookLocalizationUk : GiftLocalizationUk;
            """);

        // Assert
        var diagnostic = result.Results.ShouldHaveSingleItem().Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC206");
        diagnostic.GetMessage().ShouldContain("'ShopLocalizationUk' inherits ILocalizationSet without listing it, so its LocalizationLanguageCode implements nothing");
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).ShouldBe("ShopLocalizationUk");
    }

    [Test]
    public void LanguageCodeImplementingNothingOnAbstractClass_IsReportedAtThatClass()
    {
        // Arrange & Act: the class that declares it is no set itself, and still where the interface belongs.
        var result = Run($$"""
            {{ShopContract}}

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            public abstract class BritishWording : ShopLocalizationEn
            {
                public string LocalizationLanguageCode => "en-GB";
            }

            public sealed class ShopLocalizationUk : BritishWording;
            """);

        // Assert
        var diagnostic = result.Results.ShouldHaveSingleItem().Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC206");
        diagnostic.GetMessage().ShouldContain("'class BritishWording : ShopLocalizationEn, ILocalizationSet'");
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).ShouldBe("BritishWording");
    }

    [Test]
    [Arguments("[System.Obsolete]")]
    [Arguments("[System.Obsolete(\"Dutch is no longer sold in.\")]")]
    public void ObsoleteSet_RegistersWithoutWarningInGeneratedCode(string attribute)
    {
        // Arrange: a set on its way out still registers. A warning about naming it in generated code is one its author
        // can't act on, and stops a build that treats warnings as errors.
        var source = $$"""
            {{GeneratedShopContract}}

            {{attribute}}
            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Afrekenen";
            }
            """;

        // Act
        var text = GeneratedText(Run(source));
        var warnings = GeneratorTestHelper.GetWarningsWithGenerators(source);

        // Assert: the pragma every generated file has covers the set too.
        text.ShouldContain("#pragma warning disable CS0612, CS0618");
        text.ShouldContain("#pragma warning restore CS0612, CS0618");
        warnings.ShouldBeEmpty();
    }

    [Test]
    public void ObsoleteDefaultLanguageSet_LeavesNoWarningInGeneratedCode()
    {
        // Arrange: the generated texts name the set to take their default wording from it.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "en-GB")]

            namespace TestApp;

            [System.Obsolete("Use the texts of the new shop.")]
            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            [System.Obsolete("Use the texts of the new shop.")]
            public class ShopLocalizationEnGb : ShopLocalizationEn;
            """;

        // Act
        var warnings = GeneratorTestHelper.GetWarningsWithGenerators(source);

        // Assert
        warnings.ShouldBeEmpty();
    }

    [Test]
    public void SetObsoleteAsError_ShouldReportLOC208AndLeaveGeneratedCodeCompiling()
    {
        // Arrange: naming the set is an error (CS0619), which no pragma turns off, so generated code mustn't name it.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            [System.Obsolete("Use the new Dutch set.", error: true)]
            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """;

        // Act
        var errors = GeneratorTestHelper.CompileWithGenerators(source);
        var warnings = GeneratorTestHelper.GetWarningsWithGenerators(source);

        // Assert: it isn't registered, and the warning says why.
        errors.ShouldBeEmpty();
        var warning = warnings.ShouldHaveSingleItem();
        warning.Id.ShouldBe("LOC208");
        warning.GetMessage().ShouldContain("because it's marked [Obsolete] as an error, and generated code can't name such a class");
    }

    [Test]
    public void DefaultLanguageSetObsoleteAsError_ShouldReportLOC200AsItsOnlyError()
    {
        // Arrange & Act
        var errors = GeneratorTestHelper.CompileWithGenerators("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            [System.Obsolete("Use the texts of the new shop.", error: true)]
            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """);

        // Assert: the contract is still written, so the Dutch set compiles.
        var error = errors.ShouldHaveSingleItem();
        error.Id.ShouldBe("LOC200");
        error.GetMessage().ShouldEndWith("Mark it [Obsolete] as a warning while code still uses it, or remove it.");
    }

    [Test]
    [Arguments("[System.Diagnostics.CodeAnalysis.Experimental(\"SHOP001\")]", "")]
    [Arguments("", "[System.Diagnostics.CodeAnalysis.Experimental(\"SHOP001\")]")]
    [Arguments("[System.Obsolete(\"Use the new shop.\", DiagnosticId = \"SHOP002\")]", "")]
    [Arguments("", "[System.Obsolete(\"Use the new shop.\", DiagnosticId = \"SHOP002\")]")]
    public void SetWithDiagnosticOfItsOwn_LeavesNothingInGeneratedCode(string english, string dutch)
    {
        // Arrange: naming the set reports a diagnostic of its author's choosing, which a build treats as an error
        // (Experimental) or stops at when it treats warnings as errors (Obsolete).
        var source = $$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            {{english}}
            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            {{dutch}}
            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """;

        // Act
        var errors = GeneratorTestHelper.CompileWithGenerators(source);
        var warnings = GeneratorTestHelper.GetWarningsWithGenerators(source);

        // Assert
        errors.ShouldBeEmpty();
        warnings.ShouldBeEmpty();
    }

    [Test]
    [Arguments("\"e\", \"nl\"")]
    [Arguments("null, \"nl\"")]
    public void ApplicationWithInvalidDefaultLanguage_RegistersNoLanguages(string languages)
    {
        // Arrange & Act: the first valid language must not become the app's default, at run time either.
        var result = GeneratorTestHelper.RunAsApplication(LocalizationParts.Registration(), $$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages({{languages}})]

            namespace TestApp;

            public interface IShopLocalization : ILocalizationSet
            {
                string Checkout { get; }
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }

            public static class Program
            {
                public static void Main() { }
            }
            """);

        // Assert
        var text = GeneratedText(result);
        text.ShouldContain("new TestApp.ShopLocalizationNl()");
        text.ShouldNotContain("RegisterLanguages");
    }

    [Test]
    public void PartialSetListingItsContractInOnePartOnly_IsDiscoveredOnce()
    {
        // Arrange: discovery passes by a declaration whose base list names nothing that leads to a set, and finds the
        // class through the part that does.
        const string first = $$"""
            {{ShopContract}}

            public sealed partial class ShopLocalizationNl : System.IDisposable
            {
                public void Dispose() { }
            }
            """;
        const string second = """
            namespace TestApp;

            public sealed partial class ShopLocalizationNl : CodeChops.Idioma.ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """;

        // Act
        var text = GeneratedText(GeneratorTestHelper.RunOverFiles(LocalizationParts.Registration(), first, second));

        // Assert
        text.Split('\n').Count(line => line.Contains("new TestApp.ShopLocalizationNl()", StringComparison.Ordinal)).ShouldBe(1);
    }

    [Test]
    public void SetListingContractThatSharesItsNameWithAnotherTypeInAnOuterScope_IsDiscovered()
    {
        // Arrange & Act: the same words name another type in each scope. What they lead to is kept per scope, so the
        // class that lists the contract is not passed by for the class above it that lists a plain interface.
        var text = GeneratedText(Run("""
            using CodeChops.Idioma;

            namespace TestApp
            {
                public interface IWording;

                public sealed class Plain : IWording;

                public sealed class Plainer : IWording;

                namespace Shop
                {
                    public interface IWording : ILocalizationSet
                    {
                        string Checkout { get; }
                    }

                    public sealed class ShopLocalizationNl : ILocalizationSet, IWording
                    {
                        public string Checkout => "Afrekenen";
                    }

                    public sealed class ShopLocalizationDe : ILocalizationSet, IWording
                    {
                        public string Checkout => "Zur Kasse";
                    }
                }
            }
            """));

        // Assert
        text.ShouldContain("new TestApp.Shop.ShopLocalizationDe()");
        text.ShouldContain("new TestApp.Shop.ShopLocalizationNl()");
        text.ShouldNotContain("Plain");
    }

    [Test]
    public void SetNextToGenericClassWhoseTypeParameterIsNamedLikeItsContract_IsDiscovered()
    {
        // Arrange & Act: in the base list of the generic class the words name its type parameter (which does not
        // compile, CS0689), and in that of the set the contract.
        var text = GeneratedText(Run($$"""
            {{ShopContract}}

            public class Odd<IShopLocalization> : ILocalizationSet, IShopLocalization;

            public class Holder<IShopLocalization> : System.Collections.Generic.List<IShopLocalization>;

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """));

        // Assert
        text.ShouldContain("new TestApp.ShopLocalizationNl()");
        text.ShouldNotContain("Odd");
        text.ShouldNotContain("Holder");
    }

    [Test]
    public void SetListingItsContractThroughAnAlias_IsDiscovered()
    {
        // Arrange & Act
        var text = GeneratedText(Run("""
            using CodeChops.Idioma;
            using Contract = TestApp.IShopLocalization;
            using Sets = CodeChops.Idioma.ILocalizationSet;

            namespace TestApp;

            public interface IShopLocalization : Sets
            {
                string Checkout { get; }
            }

            public sealed class ShopLocalizationNl : Contract
            {
                public string Checkout => "Afrekenen";
            }
            """));

        // Assert
        text.ShouldContain("new TestApp.ShopLocalizationNl()");
    }

    [Test]
    public void RecordStruct_IsNoSet()
    {
        // Arrange & Act: a struct can't be created by the registry's rules, and discovery looks at classes alone.
        var result = Run($$"""
            {{ShopContract}}

            public record struct ShopLocalizationNl : IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """);

        // Assert
        ShouldEmitNothing(result);
    }

    [Test]
    public void ReparsedSet_StillEmitsAcrossEditWithoutThrowing()
    {
        // Arrange & Act: the same driver runs over a re-parsed copy of the file (the IDE edit scenario that surfaces
        // stale cached symbols).
        var result = GeneratorTestHelper.RunAcrossEdit(LocalizationParts.Registration(), $$"""
            {{ShopContract}}

            public sealed class ShopLocalizationDe : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Zur Kasse";
            }
            """);

        // Assert
        GeneratedText(result).ShouldContain("new TestApp.ShopLocalizationDe()");
    }

    [Test]
    public void UnrelatedEdit_ShouldLeaveGeneratorFullyCached()
    {
        // Arrange & Act: editing a file that declares no sets must not re-run the output step (lag on every keystroke).
        var reasons = GeneratorTestHelper.GetOutputReasonsAfterUnrelatedEdit(LocalizationParts.Registration(), $$"""
            {{ShopContract}}

            public sealed class ShopLocalizationDe : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Zur Kasse";
            }
            """);

        // Assert
        reasons.ShouldNotBeEmpty();
        reasons.ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }

    [Test]
    public void Discovery_ShouldReadOnlyTheTextsTheRegistrarRecords()
    {
        // Arrange & Act: discovery reads every set again on every edit, in any file. The registrar records the names of
        // the texts a derived set words, and writes nothing from a wording.
        var discovery = GeneratorTestHelper.GetStepValue<Discovery>(LocalizationParts.Registration(), LocalizationDiscovery.RecordTrackingName, """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "en-GB")]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Cart => "Cart";
                public virtual string Greeting(string name) => $"Hello, {name}!";
            }

            public sealed class ShopLocalizationEnGb : ShopLocalizationEn
            {
                public override string Cart => "Basket";
            }
            """);

        // Assert
        discovery.Sets.Length.ShouldBe(2);
        discovery.Sets.Single(static set => set.Name is "ShopLocalizationEn").OwnTexts.ShouldBeEmpty();
        discovery.Sets.Single(static set => set.Name is "ShopLocalizationEnGb").OwnTexts.ShouldBe(["Cart"]);

        // Nothing of what the contracts are written from, so that a change of it leaves the registrar as it was. The
        // names of the enums whose texts a set holds are the registrar's.
        discovery.Sets.ShouldAllBe(static set => set.OfContract == default);
    }

    [Test]
    public void Discovery_ShouldNotRecordATextThatAMemberOnlyHides()
    {
        // Arrange & Act: a member that hides a text words nothing, because the contract calls the member it hides
        // (LOC302). Recorded, it would keep the registry from a rewording of English. A set that lists the contract again
        // maps the contract to its own member, which then words the text.
        var discovery = GeneratorTestHelper.GetStepValue<Discovery>(LocalizationParts.Registration(), LocalizationDiscovery.RecordTrackingName, """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "en-GB", "en-AU")]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Color => "Color";
                public string Greeting(string name) => $"Hello, {name}!";
            }

            public sealed class ShopLocalizationEnGb : ShopLocalizationEn
            {
                public new string Color => "Colour";
                public new string Greeting(string name) => $"Hiya, {name}!";
            }

            public sealed class ShopLocalizationEnAu : ShopLocalizationEn, IShopLocalization
            {
                public new string Color => "Colour";
            }
            """);

        // Assert
        discovery.Sets.Single(static set => set.Name is "ShopLocalizationEnGb").OwnTexts.ShouldBeEmpty();
        discovery.Sets.Single(static set => set.Name is "ShopLocalizationEnAu").OwnTexts.ShouldBe(["Color"]);
    }

    [Test]
    public void EditInFileOfSet_ShouldReportInItsNewTreeAndRewriteNoSource()
    {
        // Arrange & Act: a set that registers, one that can't and is reported, and an edit in their file which moves
        // nothing.
        var (run, edited) = GeneratorTestHelper.RunAcrossEditInSameFile(LocalizationParts.Registration(), $$"""
            {{ShopContract}}

            public sealed class ShopLocalizationDe : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Zur Kasse";
            }

            public sealed class ShopLocalizationNl(string wording) : ILocalizationSet, IShopLocalization
            {
                public string Checkout => wording;
            }
            """);

        // Assert: the diagnostic lies in the tree the file has now, where a pragma of that file applies to it, and the
        // registrar is not written again.
        var diagnostic = run.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC208");
        diagnostic.Location.SourceTree.ShouldBeSameAs(edited);
        GeneratorTestHelper.GetSourceOutputReasons(run).ShouldHaveSingleItem().ShouldBe(IncrementalStepRunReason.Cached);
    }

    [Test]
    public void DefaultSetWhoseNameGivesNoLanguage_ReportsNoLOC208()
    {
        // Arrange & Act: LOC200 reports this set, with the name to give it. The advice of LOC208, to exclude the set
        // and register it by hand, is no way out for a default-language set.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public sealed class ShopLocalization : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }
            """);

        // Assert
        var run = result.Results.ShouldHaveSingleItem();
        run.Exception.ShouldBeNull();
        run.Diagnostics.ShouldBeEmpty();
    }
}
