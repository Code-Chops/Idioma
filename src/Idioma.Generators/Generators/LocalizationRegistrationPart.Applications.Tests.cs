using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

public sealed partial class LocalizationRegistrationPartTests
{
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
        text.IndexOf("GeneratedCode.RegisterLanguages(typeof(LocalizationRegistrar_TestAssembly), \"nl\", \"en\");", StringComparison.Ordinal)
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
        text.ShouldContain("GeneratedCode.RegisterLanguages(typeof(LocalizationRegistrar_TestAssembly), \"nl-BE\", \"en\");");
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
        text.ShouldContain("GeneratedCode.RegisterLanguages(typeof(LocalizationRegistrar_TestAssembly), \"en\", \"nl\");");
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
        text.ShouldContain("GeneratedCode.RegisterLanguages(typeof(LocalizationRegistrar_TestAssembly), \"nl\", \"en\");");
    }

    [Test]
    [Arguments("true")]
    [Arguments("True")]
    public void LibraryMarkedAsApplication_RegistersItsLanguagesFirst(string idiomaApplication)
    {
        // Arrange & Act: a project built as a library that a host loads, such as an AWS Lambda function, says in its
        // project file that it's the app. MSBuild reads a boolean in any letter case.
        var text = GeneratedText(GeneratorTestHelper.RunWithBuildProperties(LocalizationParts.Registration(), """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("nl", "en")]

            namespace TestApp;
            """, [SetsLibrary], (LocalizationRegistrationPart.IdiomaApplicationProperty, idiomaApplication)));

        // Assert: as for an application built as an executable.
        text.ShouldContain("internal static class LocalizationRegistrar_TestAssembly");
        text.IndexOf("GeneratedCode.RegisterLanguages(typeof(LocalizationRegistrar_TestAssembly), \"nl\", \"en\");", StringComparison.Ordinal)
            .ShouldBeLessThan(text.IndexOf("LocalizationRegistrar_SetsLibrary.Register());", StringComparison.Ordinal));
    }

    [Test]
    public void LibraryMarkedAsApplicationWithoutLanguages_ReportsLOC103()
    {
        // Arrange & Act: the property makes it the app, so it declares its languages as an executable does.
        var result = GeneratorTestHelper.RunWithBuildProperties(
            LocalizationParts.Registration(),
            "namespace TestApp;",
            [SetsLibrary],
            (LocalizationRegistrationPart.IdiomaApplicationProperty, "true"));

        // Assert
        result.Results.ShouldHaveSingleItem().Diagnostics.ShouldHaveSingleItem().Id.ShouldBe(LocalizationRegistrationPart.AppWithoutLanguagesDiagnosticId);
    }

    [Test]
    [Arguments("false")]
    [Arguments("")]
    public void LibraryMarkedAsNoApplication_RegistersNoLanguages(string idiomaApplication)
    {
        // Arrange & Act: the build passes an empty value for a property the project doesn't set.
        var result = GeneratorTestHelper.RunWithBuildProperties(LocalizationParts.Registration(), """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("nl", "en")]

            namespace TestApp;
            """, [], (LocalizationRegistrationPart.IdiomaApplicationProperty, idiomaApplication));

        // Assert: as for any library that only declares languages.
        ShouldEmitNothing(result);
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
}
