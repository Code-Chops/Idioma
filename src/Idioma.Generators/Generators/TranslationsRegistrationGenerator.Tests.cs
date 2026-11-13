using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="TranslationsRegistrationGenerator"/>: which sets register themselves, in which tier and order,
/// what the build says about a set that cannot, which referenced registrars an assembly calls first, and that the
/// generated code compiles and runs.
/// </summary>
public sealed class TranslationsRegistrationGeneratorTests
{
    #region Fixture
    private const string ShopContract = """
        using CodeChops.Idioma;

        namespace TestApp;

        public interface IShopTranslations : ITranslationSet
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

        public partial class ShopTranslationsEN : ITranslationSet
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

        public interface ILibraryTranslations : ITranslationSet
        {
            string Checkout { get; }
        }

        public class LibraryTranslationsEN : ILibraryTranslations
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

        public interface ISetsTranslations : ITranslationSet
        {
            string Checkout { get; }
        }

        public sealed class SetsTranslationsEN : ISetsTranslations
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

    private static GeneratorDriverRunResult Run(string source, params MetadataReference[] references)
    {
        return GeneratorTestHelper.Run(new TranslationsRegistrationGenerator(), source, references);
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

            public sealed class ShopTranslationsDE : IShopTranslations
            {
                public string Checkout => "Zur Kasse";
            }
            """);

        // Assert
        var generated = result.Results.ShouldHaveSingleItem().GeneratedSources.ShouldHaveSingleItem();
        generated.HintName.ShouldBe("TranslationsRegistrar.g.cs");

        return Verify(generated.SourceText.ToString());
    }

    [Test]
    public void SetOfContractInOtherAssembly_RegistersAsOverride()
    {
        // Arrange & Act: an app's own language for a library's contract.
        var result = Run("""
            namespace TestApp;

            public sealed class LibraryTranslationsDE : ShopLibrary.ILibraryTranslations
            {
                public string Checkout => "Zur Kasse";
            }
            """, Library);

        // Assert
        var text = GeneratedText(result);
        text.ShouldContain("TranslationsRegistry.Register(");
        text.ShouldContain("new TestApp.LibraryTranslationsDE()");
        text.ShouldNotContain("RegisterDefaults");
    }

    [Test]
    public void SetDerivedFromSetInOtherAssembly_RegistersAsOverride()
    {
        // Arrange & Act: an app rewording one of a library's texts.
        var result = Run("""
            namespace TestApp;

            public sealed class MyLibraryTranslationsEN : ShopLibrary.LibraryTranslationsEN
            {
                public override string Checkout => "Pay";
            }
            """, Library);

        // Assert
        var text = GeneratedText(result);
        text.ShouldContain("TranslationsRegistry.Register(");
        text.ShouldNotContain("RegisterDefaults");
    }

    [Test]
    public void SetDerivedFromSetOfProjectReferencedAsCompilation_RegistersAsOverride()
    {
        // Arrange: an IDE hands a generator a project of the same solution as its compilation, where a build hands over
        // the assembly. The LanguageCode the library's set implements then has syntax, in a tree of that other
        // compilation, which this one has no semantic model for.
        var library = GeneratorTestHelper.CompileProjectWithGenerators("RegionalLibrary", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace RegionalLibrary;

            public partial class GridTranslationsEN : ITranslationSet
            {
                public virtual string Colour => "Color";
            }

            public class GridTranslationsNL : IGridTranslations
            {
                public virtual string Colour => "Kleur";
            }

            public class GridTranslationsEnGB : GridTranslationsEN, ITranslationSet
            {
                string ITranslationSet.LanguageCode => "en-GB";

                public override string Colour => "Colour";
            }
            """);

        // Act
        var result = Run("""
            namespace TestApp;

            public sealed class MyGridTranslationsEnGB : RegionalLibrary.GridTranslationsEnGB
            {
                public override string Colour => "Hue";
            }
            """, library);

        // Assert: the registrar is written as a build writes it.
        var text = GeneratedText(result);
        text.ShouldContain("TranslationsRegistry.Register(");
        text.ShouldContain("new TestApp.MyGridTranslationsEnGB()");
        text.ShouldNotContain("RegisterDefaults");
    }

    [Test]
    public void SetDerivedFromConcreteSetInSameAssembly_RegistersAsDefaultAfterItsBase()
    {
        // Arrange & Act: no override (a library's own regional set must not outrank an app's), yet it must still win
        // over its base, which sorted by name would come last.
        var result = Run($$"""
            {{ShopContract}}

            public class ZuluTranslationsEN : IShopTranslations
            {
                public virtual string Checkout => "Checkout";
            }

            public sealed class AlphaTranslationsEN : ZuluTranslationsEN
            {
                public override string Checkout => "Pay";
            }
            """);

        // Assert
        var text = GeneratedText(result);
        text.ShouldNotContain("TranslationsRegistry.Register(");
        text.IndexOf("RegisterDefaults(", StringComparison.Ordinal).ShouldBeLessThan(text.IndexOf("new TestApp.ZuluTranslationsEN()", StringComparison.Ordinal));
        text.IndexOf("new TestApp.ZuluTranslationsEN()", StringComparison.Ordinal).ShouldBeLessThan(text.IndexOf("new TestApp.AlphaTranslationsEN()", StringComparison.Ordinal));
    }

    [Test]
    public void SetDerivedFromAbstractSet_RegistersAsDefault()
    {
        // Arrange & Act: an abstract base is no language of its own, so nothing is being reworded.
        var result = Run($$"""
            {{ShopContract}}

            public abstract class ShopTranslationsBase : IShopTranslations
            {
                public abstract string Checkout { get; }
            }

            public sealed class ShopTranslationsDE : ShopTranslationsBase
            {
                public override string Checkout => "Zur Kasse";
            }
            """);

        // Assert
        var text = GeneratedText(result);
        text.ShouldContain("RegisterDefaults(");
        text.ShouldNotContain("TranslationsRegistry.Register(");
        text.ShouldNotContain("ShopTranslationsBase");
    }

    [Test]
    public void SetDerivedFromAbstractSetOfGeneratedContract_RegistersAsDefault()
    {
        // Arrange & Act: only the abstract base names the contract generated here. The Dutch set used to be missed, and
        // never registered.
        var result = Run($$"""
            {{GeneratedShopContract}}

            public abstract class ShopTranslationsBase : IShopTranslations
            {
                public abstract string Checkout { get; }
            }

            public sealed class ShopTranslationsNL : ShopTranslationsBase
            {
                public override string Checkout => "Afrekenen";
            }
            """);

        // Assert
        var text = GeneratedText(result);
        text.ShouldContain("new TestApp.ShopTranslationsNL()");
        text.ShouldNotContain("ShopTranslationsBase");
    }

    [Test]
    public void SetRewordingSetOfGeneratedContract_RegistersAfterItsBase()
    {
        // Arrange & Act: its base implements the contract generated here, so it counts as a set it derives from, though
        // sorted by name the base would come last and win.
        var result = Run($$"""
            {{GeneratedShopContract}}

            public class ShopTranslationsNL : IShopTranslations
            {
                public virtual string Checkout => "Afrekenen";
            }

            public sealed class MyShopTranslationsNL : ShopTranslationsNL
            {
                public override string Checkout => "Betalen";
            }
            """);

        // Assert
        var text = GeneratedText(result);
        text.IndexOf("new TestApp.ShopTranslationsNL()", StringComparison.Ordinal)
            .ShouldBeLessThan(text.IndexOf("new TestApp.MyShopTranslationsNL()", StringComparison.Ordinal));
    }

    [Test]
    public void SeveralSets_EmitOneCreationEachInNameOrder()
    {
        // Arrange: declared out of order, and one twice through a partial, to pin the sorted, distinct emission.
        var source = $$"""
            {{ShopContract}}

            public sealed partial class ShopTranslationsNL : IShopTranslations
            {
                public string Checkout => "Afrekenen";
            }

            public sealed partial class ShopTranslationsNL : ITranslationSet;

            public sealed class ShopTranslationsDE : IShopTranslations
            {
                public string Checkout => "Zur Kasse";
            }
            """;

        // Act
        var text = GeneratedText(Run(source));

        // Assert
        text.Split('\n').Select(line => line.Trim().TrimEnd(',', ')', ';', '\r')).Where(line => line.StartsWith("new ", StringComparison.Ordinal)).ToArray().ShouldBe(
        [
            "new TestApp.ShopTranslationsDE(",
            "new TestApp.ShopTranslationsNL(",
        ]);
    }

    [Test]
    public void SetOfContractGeneratedInSameCompilation_IsDiscoveredAsDefault()
    {
        // Arrange: IShopTranslations is written by TranslationSetsGenerator from the default-language set, so it does
        // not exist in the compilation this generator sees; the Dutch set's base type binds to an error type named after
        // it. The set naming a type that no generator writes stays out (it is a compile error anyway).
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public sealed partial class ShopTranslationsEN : ITranslationSet
            {
                public string Checkout => "Checkout";
            }

            public sealed class ShopTranslationsNL : IShopTranslations
            {
                public string Checkout => "Afrekenen";
            }

            public sealed class ShopTranslationsDE : IOtherTranslations
            {
                public string Checkout => "Zur Kasse";
            }
            """;

        // Act
        var text = GeneratedText(Run(source));

        // Assert
        text.ShouldContain("RegisterDefaults(");
        text.ShouldContain("new TestApp.ShopTranslationsEN()");
        text.ShouldContain("new TestApp.ShopTranslationsNL()");
        text.ShouldNotContain("ShopTranslationsDE");
        text.ShouldNotContain("TranslationsRegistry.Register(");
    }

    [Test]
    public void SetOfContractWithoutDeclaredLanguages_IsLeftOut()
    {
        // Arrange: without a default language no contract is generated, so its name is unresolved like any other.
        const string source = """
            namespace TestApp;

            public sealed class ShopTranslationsNL : IShopTranslations
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
        // Arrange & Act: an unresolved base type alone (a typo, a missing reference) is no translations set.
        var result = Run("""
            namespace TestApp;

            public sealed class ShopTranslationsDE : IMissingTranslations
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

            internal sealed class ShopTranslationsFR : IShopTranslations
            {
                internal ShopTranslationsFR() { }
                public string Checkout => "Payer";
            }
            """);

        // Assert
        GeneratedText(result).ShouldContain("new TestApp.ShopTranslationsFR()");
    }

    [Test]
    public void RecordSet_IsDiscovered()
    {
        // Arrange & Act
        var result = Run($$"""
            {{ShopContract}}

            public sealed record ShopTranslationsFR : IShopTranslations
            {
                public string Checkout => "Payer";
            }
            """);

        // Assert
        GeneratedText(result).ShouldContain("new TestApp.ShopTranslationsFR()");
    }

    [Test]
    public void FileLocalSet_IsSkippedWhileItsSiblingIsEmitted()
    {
        // Arrange: a file-local set reports internal accessibility, but the generated registrar lives in another file
        // and cannot name it. The regular sibling shows discovery ran.
        var source = $$"""
            {{ShopContract}}

            file sealed class ShopTranslationsFR : IShopTranslations
            {
                public string Checkout => "Payer";
            }

            public sealed class ShopTranslationsDE : IShopTranslations
            {
                public string Checkout => "Zur Kasse";
            }
            """;

        // Act
        var text = GeneratedText(Run(source));

        // Assert
        text.ShouldContain("new TestApp.ShopTranslationsDE()");
        text.ShouldNotContain("ShopTranslationsFR");
    }

    [Test]
    [Arguments("[ExcludeFromTranslationDiscovery] public sealed class ShopTranslationsDE : IShopTranslations { public string Checkout => \"\"; }")]
    [Arguments("public abstract class ShopTranslationsDE : IShopTranslations { public string Checkout => \"\"; }")]
    [Arguments("public sealed class ShopTranslationsDE<T> : IShopTranslations { public string Checkout => \"\"; }")]
    [Arguments("public static class Container { public sealed class ShopTranslationsDE : IShopTranslations { public string Checkout => \"\"; } }")]
    [Arguments("public sealed class ShopWording : System.IEquatable<ShopWording> { public bool Equals(ShopWording? other) => true; }")]
    public void SetThatDoesNotRegisterItself_EmitsNothing(string declaration)
    {
        // Arrange & Act
        var result = Run($$"""
            {{ShopContract}}

            {{declaration}}
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

            [ExcludeFromTranslationDiscovery]
            public class ShopTranslationsDE : IShopTranslations
            {
                public virtual string Checkout => "Zur Kasse";
            }

            public sealed class MyShopTranslationsDE : ShopTranslationsDE;
            """);

        // Assert
        var text = GeneratedText(result);
        text.ShouldContain("new TestApp.MyShopTranslationsDE()");
        text.ShouldNotContain("new TestApp.ShopTranslationsDE()");
    }

    [Test]
    [Arguments("public ShopTranslationsDE(string wording) { }", "it has no parameterless constructor")]
    [Arguments("private ShopTranslationsDE() { }", "it has no parameterless constructor")]
    [Arguments("public ShopTranslationsDE(string wording = \"\") { } public ShopTranslationsDE(int count = 0) { }", "it has no parameterless constructor")]
    public void SetWithoutParameterlessConstructor_ReportsLOC003AndIsLeftOut(string constructor, string expectedReason)
    {
        // Arrange & Act: the sibling shows that the rest of the assembly still registers.
        var result = Run($$"""
            {{ShopContract}}

            public sealed class ShopTranslationsDE : IShopTranslations
            {
                {{constructor}}
                public string Checkout => "Zur Kasse";
            }

            public sealed class ShopTranslationsNL : IShopTranslations
            {
                public string Checkout => "Afrekenen";
            }
            """);

        // Assert
        var run = result.Results.ShouldHaveSingleItem();
        var diagnostic = run.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(TranslationsRegistrationGenerator.CannotRegisterDiagnosticId);
        diagnostic.Id.ShouldBe("LOC003");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().ShouldContain("TestApp.ShopTranslationsDE");
        diagnostic.GetMessage().ShouldContain(expectedReason);
        diagnostic.GetMessage().ShouldContain("[ExcludeFromTranslationDiscovery]");
        var text = run.GeneratedSources.ShouldHaveSingleItem().SourceText.ToString();
        text.ShouldContain("ShopTranslationsNL");
        text.ShouldNotContain("ShopTranslationsDE");
    }

    [Test]
    [Arguments("public ShopTranslationsDE(string wording = \"Zur Kasse\") { }")]
    [Arguments("public ShopTranslationsDE(params string[] wordings) { }")]
    [Arguments("public ShopTranslationsDE(string wording = \"Zur Kasse\") { } public ShopTranslationsDE() { }")]
    public void SetWithConstructorCallableWithoutArguments_IsRegistered(string constructor)
    {
        // Arrange & Act: new T() calls it, so the set registers itself like one with a parameterless constructor.
        var result = Run($$"""
            {{ShopContract}}

            public sealed class ShopTranslationsDE : IShopTranslations
            {
                {{constructor}}
                public string Checkout => "Zur Kasse";
            }
            """);

        // Assert
        var run = result.Results.ShouldHaveSingleItem();
        run.Diagnostics.ShouldBeEmpty();
        run.GeneratedSources.ShouldHaveSingleItem().SourceText.ToString().ShouldContain("ShopTranslationsDE()");
    }

    [Test]
    public void SetWithPrimaryConstructor_ReportsLOC003()
    {
        // Arrange & Act
        var result = Run($$"""
            {{ShopContract}}

            public sealed class ShopTranslationsDE(string wording) : IShopTranslations
            {
                public string Checkout => wording;
            }
            """);

        // Assert
        var run = result.Results.ShouldHaveSingleItem();
        run.Diagnostics.ShouldHaveSingleItem().Id.ShouldBe("LOC003");
        run.GeneratedSources.ShouldBeEmpty();
    }

    [Test]
    public void DefaultLanguageSetWithoutParameterlessConstructor_ReportsNoLOC003()
    {
        // Arrange & Act: such a set gets no contract and no texts either, which LOC002 reports. The advice of LOC003, to
        // exclude the set and register it by hand, is no way out for it.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public partial class ShopTranslationsEN(string shopName) : ITranslationSet
            {
                public virtual string Checkout => $"Pay at {shopName}";
            }

            public class ShopTranslationsNL : IShopTranslations
            {
                public virtual string Checkout => "Afrekenen";
            }
            """);

        // Assert: nothing reported here, and nothing registered: the Dutch set implements a contract that is not there.
        var run = result.Results.ShouldHaveSingleItem();
        run.Diagnostics.ShouldBeEmpty();
        run.GeneratedSources.ShouldBeEmpty();
    }

    [Test]
    public void SetWithoutLanguageCode_ReportsLOC003RatherThanThrowingAtLoad()
    {
        // Arrange & Act: registering this would throw while the assembly loads.
        var result = Run($$"""
            {{ShopContract}}

            public sealed class ShopTranslations1 : IShopTranslations
            {
                public string Checkout => "Zur Kasse";
            }
            """);

        // Assert
        var run = result.Results.ShouldHaveSingleItem();
        var diagnostic = run.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC003");
        diagnostic.GetMessage().ShouldContain("its name does not end in a language code");
        run.GeneratedSources.ShouldBeEmpty();
    }

    [Test]
    public void SetNamedAfterDeclaredCodeEndingInDigits_ReportsNoLOC003()
    {
        // Arrange & Act: es-419 (Latin American Spanish) ends in digits, which the name gives through the declaration.
        var text = GeneratedText(Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "es-419")]

            namespace TestApp;

            public interface IShopTranslations : ITranslationSet
            {
                string Checkout { get; }
            }

            public sealed class ShopTranslationsEs419 : IShopTranslations
            {
                public string Checkout => "Pagar";
            }
            """));

        // Assert
        text.ShouldContain("ShopTranslationsEs419>(\"es-419\");");
        text.ShouldContain("ShopTranslationsEs419()");
    }

    [Test]
    public void SetsNamedAfterDeclaredLongCodes_RecordTheirCodesBeforeRegistering()
    {
        // Arrange & Act
        var text = GeneratedText(Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl-BE", "zh-Hans")]

            namespace TestApp;

            public interface IShopTranslations : ITranslationSet
            {
                string Checkout { get; }
            }

            public sealed class ShopTranslationsEN : IShopTranslations
            {
                public string Checkout => "Checkout";
            }

            public sealed class ShopTranslationsNlBE : IShopTranslations
            {
                public string Checkout => "Afrekenen";
            }

            public sealed class ShopTranslationsZhHans : IShopTranslations
            {
                public string Checkout => "Jiezhang";
            }
            """));

        // Assert: before the sets register, as the registry files each under the code it reads from it; a two-letter
        // code needs no record.
        text.ShouldContain("GeneratedCode.RegisterLanguageCode<");
        text.ShouldContain("ShopTranslationsNlBE>(\"nl-BE\");");
        text.ShouldContain("ShopTranslationsZhHans>(\"zh-Hans\");");
        text.ShouldNotContain("ShopTranslationsEN>(");
        text.LastIndexOf("RegisterLanguageCode<", StringComparison.Ordinal)
            .ShouldBeLessThan(text.IndexOf("RegisterDefaults(", StringComparison.Ordinal));
    }

    [Test]
    public void ExcludedSetNamedAfterDeclaredLongCode_RecordsItsCodeWithoutRegistering()
    {
        // Arrange & Act: registered by hand, it must serve the language the build counted it for.
        var text = GeneratedText(Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl-BE")]

            namespace TestApp;

            public interface IShopTranslations : ITranslationSet
            {
                string Checkout { get; }
            }

            [ExcludeFromTranslationDiscovery]
            public sealed class ShopTranslationsNlBE : IShopTranslations
            {
                public string Checkout => "Afrekenen";
            }
            """));

        // Assert: a registrar with nothing to register is internal.
        text.ShouldContain("internal static class TranslationsRegistrar_TestAssembly");
        text.ShouldContain("ShopTranslationsNlBE>(\"nl-BE\");");
        text.ShouldNotContain("ShopTranslationsNlBE()");
    }

    [Test]
    [Arguments("[assembly: SupportedLanguages(\"en\", \"nl-BE\")]", "string ITranslationSet.LanguageCode => \"nl-BE\";")]
    [Arguments("[assembly: SupportedLanguages(\"en\", \"nl\")]", "")]
    public void SetWhoseNameGivesNoDeclaredLongCode_RecordsNothing(string attribute, string languageCode)
    {
        // Arrange & Act: a set implementing its code needs no record; and without nl-BE declared, the name gives 'be'.
        var text = GeneratedText(Run($$"""
            using CodeChops.Idioma;

            {{attribute}}

            namespace TestApp;

            public interface IShopTranslations : ITranslationSet
            {
                string Checkout { get; }
            }

            public sealed class ShopTranslationsNlBE : IShopTranslations
            {
                {{languageCode}}
                public string Checkout => "Afrekenen";
            }
            """));

        // Assert
        text.ShouldNotContain("RegisterLanguageCode");
    }

    [Test]
    public void SetNamedAfterTwoLetterCode_RecordsNoLongerCodeItsNameHappensToEndIn()
    {
        // Arrange & Act: "MessagesAR" ends in the letters of EsAR, the suffix of es-AR, but not at a capital, so it is
        // the Arabic set its last two letters say; "MessagesEsAR" is the Argentinian one.
        var text = GeneratedText(Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "ar", "es-AR")]

            namespace TestApp;

            public interface IMessages : ITranslationSet
            {
                string Save { get; }
            }

            public sealed class MessagesEN : IMessages
            {
                public string Save => "Save";
            }

            public sealed class MessagesAR : IMessages
            {
                public string Save => "Hifz";
            }

            public sealed class MessagesEsAR : IMessages
            {
                public string Save => "Guardar";
            }
            """));

        // Assert
        text.ShouldContain("MessagesEsAR>(\"es-AR\");");
        text.ShouldNotContain("MessagesAR>(");
        text.ShouldContain("new TestApp.MessagesAR()");
    }

    [Test]
    [Arguments("public string LanguageCode => \"de\";")]
    [Arguments("string ITranslationSet.LanguageCode => \"de\";")]
    public void SetImplementingLanguageCode_NeedsNoLanguageInItsName(string languageCode)
    {
        // Arrange & Act
        var result = Run($$"""
            {{ShopContract}}

            public sealed class ShopTranslations1 : IShopTranslations
            {
                {{languageCode}}
                public string Checkout => "Zur Kasse";
            }
            """);

        // Assert
        var run = result.Results.ShouldHaveSingleItem();
        run.Diagnostics.ShouldBeEmpty();
        run.GeneratedSources.ShouldHaveSingleItem().SourceText.ToString().ShouldContain("new TestApp.ShopTranslations1()");
    }

    [Test]
    public void LanguageCodeOfDerivedSetNotListingInterface_ReportsLOC015()
    {
        // Arrange & Act: the set inherits the mapping of ITranslationSet from its base, so its own LanguageCode is
        // never read, and it registers under the last two letters of its name (sh).
        var result = Run($$"""
            {{ShopContract}}

            public class ShopTranslationsEN : IShopTranslations
            {
                public virtual string Checkout => "Checkout";
            }

            public sealed class ShopTranslationsBritish : ShopTranslationsEN
            {
                public string LanguageCode => "en-GB";
            }
            """);

        // Assert
        var diagnostic = result.Results.ShouldHaveSingleItem().Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(TranslationsRegistrationGenerator.UnmappedLanguageCodeDiagnosticId);
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("The LanguageCode of 'ShopTranslationsBritish' implements nothing");
        diagnostic.GetMessage().ShouldContain("'class ShopTranslationsBritish : ShopTranslationsEN, ITranslationSet'");
    }

    [Test]
    public void LanguageCodeOfSetDerivedFromSetOfGeneratedContract_ReportsLOC015()
    {
        // Arrange & Act: the base implements the contract generated here, which binds to nothing yet.
        var result = Run($$"""
            {{GeneratedShopContract}}

            public class ShopTranslationsNL : IShopTranslations
            {
                public virtual string Checkout => "Afrekenen";
            }

            public sealed class ShopTranslationsBelgian : ShopTranslationsNL
            {
                public string LanguageCode => "nl-BE";
            }
            """);

        // Assert
        var diagnostic = result.Results.ShouldHaveSingleItem().Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(TranslationsRegistrationGenerator.UnmappedLanguageCodeDiagnosticId);
    }

    [Test]
    [Arguments("ShopTranslationsEN, ITranslationSet", "public string LanguageCode => \"en-GB\";")]
    [Arguments("ShopTranslationsEN, ITranslationSet", "string ITranslationSet.LanguageCode => \"en-GB\";")]
    [Arguments("ShopTranslationsEN, IShopTranslations", "public string LanguageCode => \"en-GB\";")]
    public void LanguageCodeOfDerivedSetListingInterface_ReportsNothing(string baseList, string languageCode)
    {
        // Arrange & Act
        var result = Run($$"""
            {{ShopContract}}

            public class ShopTranslationsEN : IShopTranslations
            {
                public virtual string Checkout => "Checkout";
            }

            public sealed class ShopTranslationsBritish : {{baseList}}
            {
                {{languageCode}}
            }
            """);

        // Assert
        var run = result.Results.ShouldHaveSingleItem();
        run.Diagnostics.ShouldBeEmpty();
        run.GeneratedSources.ShouldHaveSingleItem().SourceText.ToString().ShouldContain("new TestApp.ShopTranslationsBritish()");
    }

    [Test]
    public void LanguageCodeOverridingImplementation_ReportsNothing()
    {
        // Arrange & Act: the override is what the base's implementation runs.
        var result = Run($$"""
            {{ShopContract}}

            public class ShopTranslationsEN : IShopTranslations
            {
                public virtual string LanguageCode => "en";
                public virtual string Checkout => "Checkout";
            }

            public sealed class ShopTranslationsBritish : ShopTranslationsEN
            {
                public override string LanguageCode => "en-GB";
            }
            """);

        // Assert
        result.Results.ShouldHaveSingleItem().Diagnostics.ShouldBeEmpty();
    }

    [Test]
    public void LanguageCodeImplementingNothingOnSetWithoutLanguageInName_ReportsLOC015AndLOC003()
    {
        // Arrange & Act: the name gives no language either, so registering it would throw while the assembly loads. It
        // used to be registered, as its property seemed to implement LanguageCode.
        var result = Run($$"""
            {{ShopContract}}

            public class ShopTranslationsEN : IShopTranslations
            {
                public virtual string Checkout => "Checkout";
            }

            public sealed class ShopTranslationsBritish2 : ShopTranslationsEN
            {
                public string LanguageCode => "en-GB";
            }
            """);

        // Assert
        var run = result.Results.ShouldHaveSingleItem();
        run.Diagnostics.Select(diagnostic => diagnostic.Id).Order(StringComparer.Ordinal).ShouldBe(
        [
            TranslationsRegistrationGenerator.CannotRegisterDiagnosticId,
            TranslationsRegistrationGenerator.UnmappedLanguageCodeDiagnosticId,
        ]);
        run.GeneratedSources.ShouldHaveSingleItem().SourceText.ToString().ShouldNotContain("ShopTranslationsBritish2");
    }

    [Test]
    public void ExcludedSetWithoutParameterlessConstructor_ReportsNothing()
    {
        // Arrange & Act: excluding it is the fix LOC003 names.
        var result = Run($$"""
            {{ShopContract}}

            [ExcludeFromTranslationDiscovery]
            public sealed class ShopTranslationsDE(string wording) : IShopTranslations
            {
                public string Checkout => wording;
            }
            """);

        // Assert
        ShouldEmitNothing(result);
        result.Results.ShouldHaveSingleItem().Diagnostics.ShouldBeEmpty();
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

            public partial class ShopTranslationsEN : ITranslationSet
            {
                public virtual string Checkout => "Checkout";
            }

            public sealed class ShopTranslationsNL : IShopTranslations
            {
                public string Checkout => "Afrekenen";
            }

            public sealed class MyShopTranslationsEN : ShopTranslationsEN
            {
                public override string Checkout => "Pay";
            }

            public static partial class ShopTranslations
            {
                public static string Shout() => Checkout.ToString().ToUpperInvariant();
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

            public sealed class ShopTranslationsDE : IShopTranslations
            {
                public string Checkout => "Zur Kasse";
            }
            """);

        // Act
        var text = GeneratedText(GeneratorTestHelper.RunAsApplication(new TranslationsRegistrationGenerator(), "namespace TestApp;", library));

        // Assert
        text.ShouldContain("RegisterReferenced(\"My-App.Shop\", static () => CodeChops.Idioma.Generated.TranslationsRegistrar_My_App_Shop.Register());");
    }

    [Test]
    public Task ApplicationReferencingAssemblyWithSets_CallsItsRegistrarFirst()
    {
        // Arrange & Act: an app without sets or languages of its own still registers what it references, used or not.
        var text = GeneratedText(GeneratorTestHelper.RunAsApplication(new TranslationsRegistrationGenerator(), "namespace TestApp;", SetsLibrary));

        // Assert: and, declaring no languages, it leaves every text its own default language.
        text.ShouldNotContain("RegisterLanguages");

        return Verify(text);
    }

    [Test]
    public Task ApplicationDeclaringLanguages_RegistersItsLanguagesFirst()
    {
        // Arrange & Act
        var text = GeneratedText(GeneratorTestHelper.RunAsApplication(new TranslationsRegistrationGenerator(), """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("nl", "en")]

            namespace TestApp;
            """, SetsLibrary));

        // Assert: every language, the default first, and before the referenced registrars, as only the first
        // registration counts, and a referenced application (the app a test project tests) must not get there first.
        text.IndexOf("GeneratedCode.RegisterLanguages(\"nl\", \"en\");", StringComparison.Ordinal)
            .ShouldBeLessThan(text.IndexOf("TranslationsRegistrar_SetsLibrary.Register());", StringComparison.Ordinal));

        return Verify(text);
    }

    [Test]
    public void ApplicationDeclaringLanguagesWithNothingElse_StillRegistersItsLanguages()
    {
        // Arrange & Act: no sets and no references; its own inline texts need nothing, but those of a library it loads
        // later fall back to its default language, also for a culture it does not declare.
        var text = GeneratedText(GeneratorTestHelper.RunAsApplication(new TranslationsRegistrationGenerator(), """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("nl-BE", "en")]

            namespace TestApp;
            """));

        // Assert: public, so that a host referencing this application registers its languages at its own start.
        text.ShouldContain("public static class TranslationsRegistrar_TestAssembly");
        text.ShouldContain("GeneratedCode.RegisterLanguages(\"nl-BE\", \"en\");");
    }

    [Test]
    public void ApplicationReferencingApplicationDeclaringLanguages_CallsItsRegistrarFirst()
    {
        // Arrange: a Blazor Web App's server referencing its client, which declares languages and writes only inline
        // texts. While the client's registrar was internal, the server had no app languages until the client's own code
        // first ran, so what it rendered before that ignored the client's default language.
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
        var text = GeneratedText(GeneratorTestHelper.RunAsApplication(new TranslationsRegistrationGenerator(), "namespace TestApp;", client));

        // Assert
        text.ShouldContain("RegisterReferenced(\"LanguagesClient\", static () => CodeChops.Idioma.Generated.TranslationsRegistrar_LanguagesClient.Register());");
    }

    [Test]
    public void LibraryDeclaringLanguages_RegistersNoLanguages()
    {
        // Arrange & Act: a library's languages must not decide for the app that uses it.
        var text = GeneratedText(Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("de")]

            namespace TestApp;

            public interface IShopTranslations : ITranslationSet
            {
                string Checkout { get; }
            }

            public sealed class ShopTranslationsDE : IShopTranslations
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
        var text = GeneratedText(GeneratorTestHelper.RunWithBuildProperties(new TranslationsRegistrationGenerator(), """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("nl", "en")]

            namespace TestApp;
            """, [], (TranslationsRegistrationGenerator.AndroidApplicationProperty, "true")));

        // Assert: as for an application built as an executable, by a registrar a host referencing it can call.
        text.ShouldContain("public static class TranslationsRegistrar_TestAssembly");
        text.ShouldContain("GeneratedCode.RegisterLanguages(\"nl\", \"en\");");
    }

    [Test]
    public void AndroidApplicationReferencingAssemblyWithSets_CallsItsRegistrar()
    {
        // Arrange & Act: no sets and no languages of its own, like the plain library below, which emits nothing.
        var text = GeneratedText(GeneratorTestHelper.RunWithBuildProperties(
            new TranslationsRegistrationGenerator(),
            "namespace TestApp;",
            [SetsLibrary],
            (TranslationsRegistrationGenerator.AndroidApplicationProperty, "true")));

        // Assert
        text.ShouldContain("RegisterReferenced(\"SetsLibrary\", static () => CodeChops.Idioma.Generated.TranslationsRegistrar_SetsLibrary.Register());");
    }

    [Test]
    [Arguments("false")]
    [Arguments("")]
    public void AndroidLibraryDeclaringLanguages_RegistersNoLanguages(string androidApplication)
    {
        // Arrange & Act: every Android project has the property, and a library has it false.
        var result = GeneratorTestHelper.RunWithBuildProperties(new TranslationsRegistrationGenerator(), """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("nl", "en")]

            namespace TestApp;
            """, [], (TranslationsRegistrationGenerator.AndroidApplicationProperty, androidApplication));

        // Assert: nothing to register, as for any library that only declares languages.
        ShouldEmitNothing(result);
    }

    [Test]
    public void ApplicationReferencingAssemblyUnderExternAliasOnly_DoesNotCallItsRegistrar()
    {
        // Arrange & Act: code without the alias cannot name the registrar, so the assembly registers its sets when it
        // loads, which its use under the alias makes it do.
        var result = GeneratorTestHelper.RunAsApplication(new TranslationsRegistrationGenerator(), "namespace TestApp;", SetsLibrary.WithAliases(["SetsAlias"]));

        // Assert: nothing else to register, so no registrar at all.
        ShouldEmitNothing(result);
    }

    [Test]
    public void ApplicationReferencingAssemblyUnderExternAliasAndGlobal_CallsItsRegistrar()
    {
        // Arrange & Act
        var text = GeneratedText(GeneratorTestHelper.RunAsApplication(new TranslationsRegistrationGenerator(), "namespace TestApp;", SetsLibrary.WithAliases(["SetsAlias", "global"])));

        // Assert
        text.ShouldContain("TranslationsRegistrar_SetsLibrary.Register());");
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
                public static string Read() => new SetsAlias::SetsLibrary.SetsTranslationsEN().Checkout;
            }
            """;

        // Act
        var errors = GeneratorTestHelper.CompileWithGenerators(source, SetsLibrary.WithAliases(["SetsAlias"]));

        // Assert
        errors.ShouldBeEmpty();
    }

    [Test]
    [Arguments("public static class Rewordings { public sealed class MyLibraryTranslationsEN : ShopLibrary.LibraryTranslationsEN { public override string Checkout => \"Pay now\"; } }", "MyLibraryTranslationsEN", "nested in 'Rewordings'")]
    [Arguments("file sealed class MyLibraryTranslationsEN : ShopLibrary.LibraryTranslationsEN { public override string Checkout => \"Pay now\"; }", "MyLibraryTranslationsEN", "file-local")]
    [Arguments("public static class Rewordings { public sealed class LibraryTranslationsDE : ShopLibrary.ILibraryTranslations { public string Checkout => \"Zur Kasse\"; } }", "LibraryTranslationsDE", "nested in 'Rewordings'")]
    public void NestedOrFileLocalSetRewordingAnotherAssembly_ReportsCannotRegister(string declaration, string expectedName, string expectedReason)
    {
        // Arrange & Act: generated code cannot name it, so the app would keep showing the library's wording.
        var result = Run($$"""
            namespace TestApp;

            {{declaration}}
            """, Library);

        // Assert
        var diagnostic = result.Results.ShouldHaveSingleItem().Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(TranslationsRegistrationGenerator.CannotRegisterDiagnosticId);
        diagnostic.GetMessage().ShouldContain(expectedName);
        diagnostic.GetMessage().ShouldContain(expectedReason);
        diagnostic.GetMessage().ShouldContain("[ExcludeFromTranslationDiscovery]");
    }

    [Test]
    [Arguments("public static class Rewordings { [CodeChops.Idioma.ExcludeFromTranslationDiscovery] public sealed class MyLibraryTranslationsEN : ShopLibrary.LibraryTranslationsEN { } }")]
    [Arguments("public static class Rewordings { public abstract class MyLibraryTranslationsEN : ShopLibrary.LibraryTranslationsEN { } }")]
    [Arguments("public static class Rewordings { public sealed class MyLibraryTranslationsEN<T> : ShopLibrary.LibraryTranslationsEN { } }")]
    public void NestedSetRewordingAnotherAssemblyThatIsExcludedOrNoInstance_ReportsNothing(string declaration)
    {
        // Arrange & Act
        var result = Run($$"""
            namespace TestApp;

            {{declaration}}
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

            public sealed class ShopTranslationsDE : IShopTranslations
            {
                public string Checkout => "Zur Kasse";
            }
            """, SetsLibrary));

        // Assert
        text.ShouldContain("public static class TranslationsRegistrar_TestAssembly");
        text.IndexOf("TranslationsRegistrar_SetsLibrary.Register());", StringComparison.Ordinal).ShouldBeLessThan(text.IndexOf("RegisterDefaults(", StringComparison.Ordinal));
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
        text.ShouldContain("RegisterReferenced(\"SetsLibrary\", static () => CodeChops.Idioma.Generated.TranslationsRegistrar_SetsLibrary.Register());");
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
        var text = GeneratedText(GeneratorTestHelper.RunAsApplication(new TranslationsRegistrationGenerator(), "namespace TestApp;", SetsLibrary, languagesOnly));

        // Assert
        text.ShouldContain("TranslationsRegistrar_SetsLibrary.Register());");
        text.ShouldNotContain("LanguagesOnly");
    }

    [Test]
    [NotInParallel(nameof(TranslationsRegistry))]
    public void AppReferencingAssemblyItNeverUses_ShouldRegisterItsSetsAtStart()
    {
        // Arrange: a library, an add-on holding only a German set for the library's contract, and an app that declares
        // German and never touches the add-on. Before the app called the add-on's registrar, the add-on never loaded,
        // while its set still satisfied the app's LOC008 check.
        var library = GeneratorTestHelper.EmitLibraryWithGenerators("UnusedAddOnLibrary", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace UnusedAddOnLibrary;

            public partial class AddOnShopTranslationsEN : ITranslationSet
            {
                public virtual string Checkout => "Checkout";
            }

            public sealed class AddOnShopTranslationsNL : IAddOnShopTranslations
            {
                public string Checkout => "Afrekenen";
            }
            """);
        var addOn = GeneratorTestHelper.EmitLibraryWithGenerators("UnusedAddOn", """
            namespace UnusedAddOn;

            public sealed class AddOnShopTranslationsDE : UnusedAddOnLibrary.IAddOnShopTranslations
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
        var texts = context.LoadFromAssemblyName(new AssemblyName("UnusedAddOnLibrary")).GetType("UnusedAddOnLibrary.AddOnShopTranslations")!;
        var checkout = (LocalizableText)texts.GetProperty("Checkout")!.GetValue(null)!;
        checkout.ToString(CultureInfo.GetCultureInfo("de-DE"), TranslationsRegistry.Current, appLanguages: ["en", "nl", "de"]).ShouldBe("Zur Kasse");
    }

    [Test]
    [NotInParallel(nameof(TranslationsRegistry))]
    public void AppReferencingAssemblyThatIsNotThere_ShouldStartAndRegisterItsOwnSets()
    {
        // Arrange: the app's folder lacks a library it references, as a Blazor WebAssembly app lacks an assembly it
        // loads lazily. Before the call to the library's registrar was guarded, the app's module initializer threw, so
        // the app did not start.
        var library = GeneratorTestHelper.EmitLibraryWithGenerators("AbsentLibrary", """
            using CodeChops.Idioma;

            namespace AbsentLibrary;

            public interface IAbsentTranslations : ITranslationSet
            {
                string Checkout { get; }
            }

            public sealed class AbsentTranslationsEN : IAbsentTranslations
            {
                public string Checkout => "Checkout";
            }
            """);
        var app = GeneratorTestHelper.EmitLibraryWithGenerators("AbsentLibraryApp", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace AbsentLibraryApp;

            public partial class AbsentAppTranslationsEN : ITranslationSet
            {
                public string Checkout => "Checkout";
            }

            public sealed class AbsentAppTranslationsNL : IAbsentAppTranslations
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
        var texts = appAssembly.GetType("AbsentLibraryApp.AbsentAppTranslations")!;
        var checkout = (LocalizableText)texts.GetProperty("Checkout")!.GetValue(null)!;
        checkout.ToString(CultureInfo.GetCultureInfo("nl-NL")).ShouldBe("Afrekenen");
    }

    [Test]
    [NotInParallel(nameof(TranslationsRegistry))]
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

            public partial class LateTranslationsEN : ITranslationSet
            {
                public virtual string Checkout => "Checkout";
            }

            public sealed class LateTranslationsNL : ILateTranslations
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
                public static string ReadDutchCheckout() => LateLibrary.LateTranslations.Checkout.ToString(CultureInfo.GetCultureInfo("nl-NL"));
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
        var checkout = (string)appAssembly.GetType("LateLibraryApp.Reader")!.GetMethod("ReadDutchCheckout")!.Invoke(obj: null, parameters: null)!;

        // Assert: the library's own registrar ran with its code, so its Dutch set words the text.
        checkout.ShouldBe("Afrekenen");
    }

    [Test]
    public void ReferencedRegistrar_IsCalledOnlyAfterItsAssemblyIsFoundByName()
    {
        // Arrange & Act: the WebAssembly runtime remembers a call into an assembly that was not there, and refuses
        // every later use of that assembly from the calling one, also once it has loaded lazily. Seen in a browser: a
        // lookup by name that fails leaves nothing behind, and a call that fails does.
        var text = GeneratedText(GeneratorTestHelper.RunAsApplication(new TranslationsRegistrationGenerator(), "namespace TestApp;", SetsLibrary));

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
    [NotInParallel(nameof(TranslationsRegistry))]
    public void SetDerivedFromAbstractSetOfGeneratedContract_ShouldShowItsLanguageAtRunTime()
    {
        // Arrange: only the abstract base names the contract generated here. The Dutch set used to be left out of the
        // registration, so Dutch users saw English.
        var image = GeneratorTestHelper.EmitLibraryWithGenerators("AbstractBaseLibrary", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace AbstractBaseLibrary;

            public partial class BaseShopTranslationsEN : ITranslationSet
            {
                public string Checkout => "Checkout";
            }

            public abstract class BaseShopTranslationsBase : IBaseShopTranslations
            {
                public abstract string Checkout { get; }
            }

            public sealed class BaseShopTranslationsNL : BaseShopTranslationsBase
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
        var texts = assembly.GetType("AbstractBaseLibrary.BaseShopTranslations")!;
        var checkout = (LocalizableText)texts.GetProperty("Checkout")!.GetValue(null)!;
        checkout.ToString(CultureInfo.GetCultureInfo("nl-NL")).ShouldBe("Afrekenen");
    }

    [Test]
    [NotInParallel(nameof(TranslationsRegistry))]
    public void SetNamedAfterDeclaredLongCode_ShouldServeItAtRunTime()
    {
        // Arrange: neither set implements LanguageCode; the rewording's name gives no language of its own.
        var image = GeneratorTestHelper.EmitLibraryWithGenerators("RegionalNameLibrary", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl-BE")]

            namespace RegionalNameLibrary;

            public partial class ShopTranslationsEN : ITranslationSet
            {
                public virtual string Checkout => "Checkout";
                public virtual string Total => "Total";
            }

            public class ShopTranslationsNlBE : IShopTranslations
            {
                public virtual string Checkout => "Afrekenen";
                public virtual string Total => "Totaal";
            }

            public sealed class ShopRewording : ShopTranslationsNlBE
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
        var belgian = (ITranslationSet)Activator.CreateInstance(assembly.GetType("RegionalNameLibrary.ShopTranslationsNlBE")!)!;
        var rewording = (ITranslationSet)Activator.CreateInstance(assembly.GetType("RegionalNameLibrary.ShopRewording")!)!;
        belgian.LanguageCode.ShouldBe("nl-BE");
        rewording.LanguageCode.ShouldBe("nl-BE");

        var texts = assembly.GetType("RegionalNameLibrary.ShopTranslations")!;
        var checkout = (LocalizableText)texts.GetProperty("Checkout")!.GetValue(null)!;
        var total = (LocalizableText)texts.GetProperty("Total")!.GetValue(null)!;
        checkout.ToString(CultureInfo.GetCultureInfo("nl-BE")).ShouldBe("Betalen");
        total.ToString(CultureInfo.GetCultureInfo("nl-BE")).ShouldBe("Totaal");
    }

    [Test]
    public void ReparsedSet_StillEmitsAcrossEditWithoutThrowing()
    {
        // Arrange & Act: the same driver runs over a re-parsed copy of the file (the IDE edit scenario that surfaces
        // stale cached symbols).
        var result = GeneratorTestHelper.RunAcrossEdit(new TranslationsRegistrationGenerator(), $$"""
            {{ShopContract}}

            public sealed class ShopTranslationsDE : IShopTranslations
            {
                public string Checkout => "Zur Kasse";
            }
            """);

        // Assert
        GeneratedText(result).ShouldContain("new TestApp.ShopTranslationsDE()");
    }

    [Test]
    public void UnrelatedEdit_ShouldLeaveGeneratorFullyCached()
    {
        // Arrange & Act: editing a file that declares no sets must not re-run the output step (lag on every keystroke).
        var reasons = GeneratorTestHelper.GetOutputReasonsAfterUnrelatedEdit(new TranslationsRegistrationGenerator(), $$"""
            {{ShopContract}}

            public sealed class ShopTranslationsDE : IShopTranslations
            {
                public string Checkout => "Zur Kasse";
            }
            """);

        // Assert
        reasons.ShouldNotBeEmpty();
        reasons.ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }

    [Test]
    public void EditInFileOfSet_ShouldReportInItsNewTreeAndRewriteNoSource()
    {
        // Arrange & Act: a set that registers, one that cannot and is reported, and an edit in their file which moves
        // nothing.
        var (run, edited) = GeneratorTestHelper.RunAcrossEditInSameFile(new TranslationsRegistrationGenerator(), $$"""
            {{ShopContract}}

            public sealed class ShopTranslationsDE : IShopTranslations
            {
                public string Checkout => "Zur Kasse";
            }

            public sealed class ShopTranslationsNL(string wording) : IShopTranslations
            {
                public string Checkout => wording;
            }
            """);

        // Assert: the diagnostic lies in the tree the file has now, where a pragma of that file applies to it, and the
        // registrar is not written again.
        var diagnostic = run.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC003");
        diagnostic.Location.SourceTree.ShouldBeSameAs(edited);
        GeneratorTestHelper.GetSourceOutputReasons(run).ShouldHaveSingleItem().ShouldBe(IncrementalStepRunReason.Cached);
    }
}
