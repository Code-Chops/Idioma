using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

public sealed partial class LocalizationRegistrationPartTests
{
    [Test]
    [Arguments("public ShopLocalizationDe(string wording) { }", "it has no parameterless constructor")]
    [Arguments("private ShopLocalizationDe() { }", "it has no parameterless constructor")]
    [Arguments("public ShopLocalizationDe(string wording = \"\") { } public ShopLocalizationDe(int count = 0) { }", "it has no parameterless constructor")]
    public void SetWithoutParameterlessConstructor_ReportsLOC207AndIsLeftOut(string constructor, string expectedReason)
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
        diagnostic.Id.ShouldBe("LOC207");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().ShouldContain("TestApp.ShopLocalizationDe");
        diagnostic.GetMessage().ShouldContain(expectedReason);
        diagnostic.GetMessage().ShouldContain("[ExcludeFromLocalizationDiscovery]");
        var text = run.GeneratedSources.ShouldHaveSingleItem().SourceText.ToString();
        text.ShouldContain("new TestApp.ShopLocalizationNl()");
        text.ShouldNotContain("new TestApp.ShopLocalizationDe()");
        text.ShouldContain("DescribeSet<TestApp.ShopLocalizationDe>(\"de\");");
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
    public void SetWithPrimaryConstructor_ReportsLOC207()
    {
        // Arrange & Act
        var result = Run($$"""
            {{ShopContract}}

            public sealed class ShopLocalizationDe(string wording) : ILocalizationSet, IShopLocalization
            {
                public string Checkout => wording;
            }
            """);

        // Assert: not registered, and described, for a registration by hand.
        var run = result.Results.ShouldHaveSingleItem();
        run.Diagnostics.ShouldHaveSingleItem().Id.ShouldBe("LOC207");
        var text = GeneratedText(result);
        text.ShouldContain("DescribeSet<TestApp.ShopLocalizationDe>(\"de\");");
        text.ShouldNotContain("ShopLocalizationDe()");
    }

    [Test]
    public void DefaultLanguageSetWithoutParameterlessConstructor_ReportsNoLOC207()
    {
        // Arrange & Act: such a set gets its contract but no texts, which LOC200 reports. The advice of LOC207, to
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
    public void SetWithoutLanguageCode_ReportsLOC207RatherThanThrowingAtLoad(string name)
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
        diagnostic.Id.ShouldBe("LOC207");
        diagnostic.GetMessage().ShouldContain("its name doesn't end in a language code");
        run.GeneratedSources.ShouldBeEmpty();
    }

    [Test]
    public void SetNamedAfterDeclaredCodeEndingInDigits_ReportsNoLOC207()
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
    public void ExcludedSetWithoutParameterlessConstructor_ReportsNothing()
    {
        // Arrange & Act: excluding it is the fix LOC207 names.
        var result = Run($$"""
            {{ShopContract}}

            [ExcludeFromLocalizationDiscovery]
            public sealed class ShopLocalizationDe(string wording) : ILocalizationSet, IShopLocalization
            {
                public string Checkout => wording;
            }
            """);

        // Assert: described, for the registration by hand.
        var run = result.Results.ShouldHaveSingleItem();
        run.Diagnostics.ShouldBeEmpty();
        GeneratedText(result).ShouldContain("DescribeSet<TestApp.ShopLocalizationDe>(\"de\");");
    }

    [Test]
    public void ExcludedSetWhoseNameGivesNoLanguage_ReportsLOC207()
    {
        // Arrange & Act: registering it by hand throws, because the registry files a set under the language of its name.
        var result = Run($$"""
            {{ShopContract}}

            [ExcludeFromLocalizationDiscovery]
            public sealed class BrandedShopTexts(string wording) : ILocalizationSet, IShopLocalization
            {
                public string Checkout => wording;
            }
            """);

        // Assert
        var diagnostic = result.Results.ShouldHaveSingleItem().Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC207");
        diagnostic.GetMessage().ShouldContain("BrandedShopTexts' can't be registered by hand because its name doesn't end in a language code");
        diagnostic.GetMessage().ShouldEndWith("LocalizationRegistry.Register throws for it. Rename it, or derive a class from it whose name ends in the code.");
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
    public void SetWithRequiredMember_ReportsLOC207AndIsLeftOut()
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
        diagnostic.Id.ShouldBe("LOC207");
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
    public void DefaultSetWhoseNameGivesNoLanguage_ReportsNoLOC207()
    {
        // Arrange & Act: LOC200 reports this set, with the name to give it. The advice of LOC207, to exclude the set
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
