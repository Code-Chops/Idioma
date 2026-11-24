using CodeChops.Idioma.Tests.TestHelpers;

namespace CodeChops.Idioma.Tests;

public sealed partial class LocalizationRegistrationPartTests
{
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
}
