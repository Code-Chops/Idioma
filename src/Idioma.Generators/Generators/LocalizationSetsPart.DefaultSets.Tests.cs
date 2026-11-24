using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

public sealed partial class LocalizationSetsPartTests
{
    [Test]
    [Arguments("public sealed class ShopLocalization : ILocalizationSet { public string LocalizationLanguageCode => \"en\"; public string Checkout => \"Checkout\"; }", "its name doesn't end in En")]
    [Arguments("public abstract class ShopLocalizationEn : ILocalizationSet, IShopLocalization { public string Checkout => \"Checkout\"; }", "it's abstract")]
    [Arguments("public sealed class ShopLocalizationEn<T> : ILocalizationSet, IShopLocalization { public string Checkout => \"Checkout\"; }", "it's generic")]
    [Arguments("file sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization { public string Checkout => \"Checkout\"; }", "it's file-local")]
    [Arguments("public static partial class Container { public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization { public string Checkout => \"Checkout\"; } }", "it's nested in 'Container'")]
    public void DefaultSetGeneratorCannotExtend_ShouldReportLOC200AndEmitNothing(string declaration, string expectedReason)
    {
        // Arrange
        var source = $"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            {declaration}
            """;

        // Act
        var result = Run(source);

        // Assert
        var diagnostic = SingleDiagnostic(result, LocalizationSetsPart.CannotExtendDiagnosticId);
        diagnostic.Id.ShouldBe("LOC200");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("'ShopLocalization");
        diagnostic.GetMessage().ShouldContain(expectedReason);
        result.Results.ShouldHaveSingleItem().GeneratedSources.ShouldBeEmpty();
    }

    [Test]
    [Arguments("public sealed class FakeShopEn : ILocalizationSet, IShopLocalization")]
    [Arguments("public sealed class FakeShopEn : ILocalizationSet, IShopLocalization, System.IDisposable")]
    [Arguments("public static class Tests { public sealed class FakeLocalizationEn : ILocalizationSet, IShopLocalization")]
    [Arguments("public static partial class Tests { private sealed partial class FakeLocalizationEn : ILocalizationSet, IShopLocalization")]
    [Arguments("file sealed class FakeLocalizationEn : ILocalizationSet, IShopLocalization")]
    [Arguments("public sealed class FakeLocalizationEn<T> : ILocalizationSet, IShopLocalization")]
    [Arguments("public abstract class FakeLocalizationEn : ILocalizationSet, IShopLocalization")]
    public void DefaultLanguageClassImplementingContractOfAnotherSet_ShouldBeNoStartOfContract(string declaration)
    {
        // Arrange: a second class in the default language, such as a test double, which lists ILocalizationSet next to
        // the contract generated from ShopLocalizationEn, as every set does.
        var closing = declaration.Contains("class Tests", StringComparison.Ordinal) ? "} }" : "}";
        var source = $$"""
            {{ShopSets}}

            {{declaration}}
            {
                public string Checkout => "Check out";
                public string Total => "Sum";
                public void Dispose() { }
            {{closing}}
            """;

        // Act
        var result = Run(source);

        // Assert: no LOC200 for its shape, no LOC204 for a contract of its own, and one contract, from ShopLocalizationEn.
        DiagnosticIds(result).ShouldBeEmpty();
        ContractText(result).ShouldContain("interface IShopLocalization");
    }

    [Test]
    public void DefaultSetListingContractOfAnotherSetNextToItsOwn_ShouldStillStartItsContract()
    {
        // Arrange: a set that starts IOrderLocalization and translates IShopLocalization in the same class.
        var source = $$"""
            {{ShopSets}}

            public sealed class OrderLocalizationEn : ILocalizationSet, IOrderLocalization, IShopLocalization
            {
                public string Placed => "Placed";
                string IShopLocalization.Checkout => "Checkout";
                string IShopLocalization.Total => "Total";
            }

            public sealed class OrderLocalizationNl : ILocalizationSet, IOrderLocalization
            {
                public string Placed => "Geplaatst";
            }
            """;

        // Act
        var result = Run(source);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
        result.Results.ShouldHaveSingleItem().GeneratedSources
            .ShouldContain(static generated => String.Equals(generated.HintName, "TestApp.OrderLocalizationEn.Localization.g.cs", StringComparison.Ordinal));
    }

    [Test]
    public void DefaultSetWithoutParameterlessConstructor_ShouldReportLOC200AsItsOnlyError()
    {
        // Arrange & Act: the generated texts create the set for their default wording, as the registrar creates it to
        // register. With all three generators at work, as in a build.
        var errors = GeneratorTestHelper.CompileWithGenerators("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public sealed class ShopLocalizationEn(string shopName) : ILocalizationSet, IShopLocalization
            {
                public string Checkout => $"Pay at {shopName}";
            }
            """);

        // Assert: one error, at the set, and none in code the generators wrote (CS7036 on a 'new()' nobody typed).
        var error = errors.ShouldHaveSingleItem();
        error.Id.ShouldBe("LOC200");
        error.GetMessage().ShouldContain("it has no parameterless constructor");
        error.GetMessage().ShouldEndWith("Generated code creates it, so give it a public parameterless constructor and no required members.");
    }

    [Test]
    public void DefaultSetWhoseGeneratedClassIsNamedLikeNamespace_ShouldReportLOC200AsItsOnlyError()
    {
        // Arrange & Act: a folder Texts gives the namespace MyApp.Texts, which the generated class MyApp.Texts would meet
        // (CS0101).
        var errors = GeneratorTestHelper.CompileWithGenerators("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace MyApp.Texts
            {
                public static class Helper { }
            }

            namespace MyApp
            {
                public sealed class TextsEn : ILocalizationSet, ITexts
                {
                    public string Checkout => "Checkout";
                }

                public sealed class TextsNl : ILocalizationSet, ITexts
                {
                    public string Checkout => "Afrekenen";
                }
            }
            """);

        // Assert: the contract is still written, so the sets compile.
        var error = errors.ShouldHaveSingleItem();
        error.Id.ShouldBe("LOC200");
        error.GetMessage().ShouldStartWith("'TextsEn' is the default-language set of a contract, but its generated class would be named 'Texts', like the namespace 'MyApp.Texts'.");
        error.GetMessage().ShouldEndWith("Rename the set or that namespace.");
    }

    [Test]
    public void InterfaceWithTheNameOfTheContract_ShouldReportLOC200AsItsOnlyError()
    {
        // Arrange & Act: an interface written by hand that doesn't derive from ILocalizationSet isn't a contract, and the
        // generated one would meet it (CS0101).
        var errors = GeneratorTestHelper.CompileWithGenerators("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public interface IHandLocalization
            {
                string Checkout { get; }
            }

            public sealed class HandLocalizationEn : ILocalizationSet, IHandLocalization
            {
                public string Checkout => "Checkout";
            }

            public sealed class HandLocalizationNl : ILocalizationSet, IHandLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """);

        // Assert: no LOC204 asks a set to list the interface it lists.
        var error = errors.ShouldHaveSingleItem();
        error.Id.ShouldBe("LOC200");
        error.GetMessage().ShouldStartWith("'HandLocalizationEn' is the default-language set of a contract, but its contract would be named 'IHandLocalization', like the interface 'IHandLocalization'.");
        error.GetMessage().ShouldEndWith("Rename the set or that type. To write the contract yourself, derive it from ILocalizationSet.");
    }

    [Test]
    public void DefaultSetImplementingAnotherContract_ShouldReportLOC200()
    {
        // Arrange & Act: one English class that would start the app's contract and serve the library's.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public sealed class AppLocalizationEn : ILocalizationSet, IAppLocalization, GridLibrary.IGridLocalization
            {
                public string First => "First";
                public string Title => "Orders";
            }
            """, GridLibrary);

        // Assert: it says why IAppLocalization isn't there, where the compiler only says it isn't found (CS0246).
        var message = SingleDiagnostic(result, LocalizationSetsPart.CannotExtendDiagnosticId).GetMessage();
        message.ShouldStartWith("'AppLocalizationEn' is the default-language set of a contract, but it implements 'GridLibrary.IGridLocalization' too, and a set starts a contract only when it implements no other.");
        message.ShouldEndWith("Write the texts of 'IAppLocalization' in a set of their own, and implement 'GridLibrary.IGridLocalization' in another class.");
    }

    [Test]
    [Arguments("public required string Checkout { get; init; }", "it has a required member ('Checkout'), which generated code can't set")]
    [Arguments("internal ShopLocalizationEn() { } public string Checkout => \"Checkout\";", "it has no public parameterless constructor")]
    [Arguments("public ShopLocalizationEn(string shop = \"\") { } public string Checkout => \"Checkout\";", "it has no public parameterless constructor")]
    [Arguments("private ShopLocalizationEn() { } public string Checkout => \"Checkout\";", "it has no parameterless constructor")]
    [Arguments("public ShopLocalizationEn(string shop) { } public string Checkout => \"Checkout\";", "it has no parameterless constructor")]
    public void DefaultSetGeneratedCodeCannotCreate_ShouldReportLOC200AsItsOnlyError(string members, string expectedReason)
    {
        // Arrange & Act: the generated texts name the set as a type argument of GeneratedCode.WordSetText, which takes
        // a public parameterless constructor (CS0310) and no member left to set (CS9040). With every generator at work.
        var errors = GeneratorTestHelper.CompileWithGenerators($$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                {{members}}
            }
            """);

        // Assert: one error, at the set, and none in code the generators wrote.
        var error = errors.ShouldHaveSingleItem();
        error.Id.ShouldBe("LOC200");
        error.GetMessage().ShouldContain(expectedReason);
    }

    [Test]
    [Arguments("public enum OrderStatus { Pending }", "ILocalizationSet<OrderStatus>", "the enum 'OrderStatus'")]
    [Arguments("public enum Other { Pending } public class OrderStatus;", "ILocalizationSet<Other>", "the class 'OrderStatus'")]
    [Arguments("public enum Other { Pending } public static class OrderStatus;", "ILocalizationSet<Other>", "the class 'OrderStatus'")]
    [Arguments("public enum Other { Pending } public record struct OrderStatus;", "ILocalizationSet", "the record 'OrderStatus'")]
    [Arguments("public enum Other { Pending } public interface OrderStatus;", "ILocalizationSet", "the interface 'OrderStatus'")]
    public void DefaultSetWhoseGeneratedClassIsNamedLikeAnotherType_ShouldReportLOC200AsItsOnlyError(string types, string setInterface, string expectedType)
    {
        // Arrange & Act: a set named after its enum, without a word such as Localization in between. The generated
        // class would be a second type of that name (CS0101), in a file the user can't change.
        var errors = GeneratorTestHelper.CompileWithGenerators($$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            {{types}}

            public sealed class OrderStatusEn : {{setInterface}}, IOrderStatus
            {
                public string Pending => "Pending";
            }
            """);

        // Assert: one error, at the set. The contract is still written, so the set compiles.
        var error = errors.ShouldHaveSingleItem();
        error.Id.ShouldBe("LOC200");
        error.GetMessage().ShouldStartWith($"'OrderStatusEn' is the default-language set of a contract, but its generated class would be named 'OrderStatus', like {expectedType}.");
        error.GetMessage().ShouldEndWith("Rename the set or that type.");
    }

    [Test]
    [Arguments("using Domain;", "ILocalizationSet<OrderStatus>", true)]
    [Arguments("using Domain;", "CodeChops.Idioma.ILocalizationSet<OrderStatus>", true)]
    [Arguments("", "ILocalizationSet<Domain.OrderStatus>", false)]
    public void DefaultSetWhoseGeneratedClassIsNamedLikeItsEnumOfAnotherNamespace_ShouldReportLOC200OnlyWhereItNamesTheEnumAlone(
        string usings, string setInterface, bool isReported)
    {
        // Arrange & Act: the enum lies in another namespace than the set that is named after it. Inside the set's
        // namespace the generated class would stand before the enum. So the set's own ILocalizationSet<OrderStatus>
        // would name that class, which is static (CS0718). A set that writes the enum with its namespace still names it.
        var errors = GeneratorTestHelper.CompileWithGenerators($$"""
            using CodeChops.Idioma;
            {{usings}}

            [assembly: SupportedLanguages("en")]

            namespace Domain
            {
                public enum OrderStatus { Pending }
            }

            namespace TestApp
            {
                public sealed class OrderStatusEn : {{setInterface}}, IOrderStatus
                {
                    public string Pending => "Pending";
                }
            }
            """);

        // Assert: one error, at the set, where it's reported. The contract is still written, so the set compiles.
        if (!isReported)
        {
            errors.ShouldBeEmpty();
            return;
        }

        var error = errors.ShouldHaveSingleItem();
        error.Id.ShouldBe("LOC200");
        error.GetMessage().ShouldStartWith("'OrderStatusEn' is the default-language set of a contract, but its generated class would be named 'OrderStatus', like the enum 'Domain.OrderStatus' it holds the texts of.");
        error.GetMessage().ShouldEndWith("Rename the set or that type.");
    }

    [Test]
    public void DefaultSetNextToStaticPartialClassNamedLikeItsGeneratedClass_ShouldCompile()
    {
        // Arrange & Act: the class is a part of the generated class, with a helper next to the texts. A type of that
        // name with a type parameter is another type.
        var errors = GeneratorTestHelper.CompileWithGenerators("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public static partial class ShopLocalization
            {
                public static string Both => $"{Checkout} {Checkout}";
            }

            public sealed class ShopLocalization<T>;

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }
            """);

        // Assert
        errors.ShouldBeEmpty();
    }

    [Test]
    public void DefaultSetWhoseConstructorSetsItsRequiredMembers_ShouldCompile()
    {
        // Arrange & Act
        var errors = GeneratorTestHelper.CompileWithGenerators("""
            using System.Diagnostics.CodeAnalysis;
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                [SetsRequiredMembers]
                public ShopLocalizationEn()
                {
                    this.Checkout = "Checkout";
                }

                public required string Checkout { get; init; }
            }
            """);

        // Assert
        errors.ShouldBeEmpty();
    }

    [Test]
    public void SetsEndingInTheirLanguageInUpperCase_ShouldCountAsThoseInPascalCase()
    {
        // Arrange: the suffix counts in any letter case that starts with a capital, so sets named before the suffix
        // was written in PascalCase still build.
        const string Source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "en-GB")]

            namespace TestApp;

            public class ShopLocalizationEN : ILocalizationSet, IShopLocalization
            {
                public virtual string Cart => "Cart";
            }

            public class ShopLocalizationNL : ILocalizationSet, IShopLocalization
            {
                public string Cart => "Winkelwagen";
            }

            public class ShopLocalizationEnGB : ShopLocalizationEN
            {
                public override string Cart => "Basket";
            }
            """;

        // Act
        var errors = GeneratorTestHelper.CompileWithGenerators(Source);
        var result = Run(Source);

        // Assert: the contract and the generated class drop the suffix, whatever its letter case.
        errors.ShouldBeEmpty();
        DiagnosticIds(result).ShouldBeEmpty();
        ContractText(result).ShouldContain("interface IShopLocalization");
        ContractText(result).ShouldContain("static partial class ShopLocalization");
    }

    [Test]
    public void TwoDefaultSetsWhoseNamesDifferInTheCaseOfTheirSuffix_ShouldReportLOC200AndWriteOneContract()
    {
        // Arrange: both names end in the default language, so both start the contract IShopLocalization.
        const string Source = $$"""
            {{Header}}

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Cart => "Cart";
            }

            public class ShopLocalizationEN : ILocalizationSet, IShopLocalization
            {
                public string Cart => "Shopping cart";
            }

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Cart => "Winkelwagen";
            }
            """;

        // Act
        var result = Run(Source);
        var errors = GeneratorTestHelper.CompileWithGenerators(Source);

        // Assert: the contract comes from the first of them by name, and the other is reported where it is declared.
        // The generator neither stops at two files of one name nor writes the contract twice.
        var diagnostic = SingleDiagnostic(result, LocalizationSetsPart.CannotExtendDiagnosticId);
        diagnostic.GetMessage().ShouldStartWith("'ShopLocalizationEn' is the default-language set of a contract, but 'ShopLocalizationEN' is the default-language set of 'IShopLocalization' already, and a contract has only one. The generator can't use it, so it writes no texts for it.");
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).ShouldBe("ShopLocalizationEn");
        diagnostic.Properties.ShouldNotContainKey(LocalizationSetsPart.SuggestedNameProperty);
        DiagnosticIds(result).ShouldBe(["LOC200"]);
        ContractText(result).ShouldContain("WordSetText<IShopLocalization, ShopLocalizationEN>(");
        errors.Select(static error => error.Id).ShouldBe(["LOC200"]);
    }

    [Test]
    [Arguments("namespace Shop", "namespace shop", "TextsEn", "TextsEn")]
    [Arguments("namespace Shop", "namespace Shop", "TextsEn", "TEXTSEn")]
    public void ContractsWhoseFilesDifferOnlyInLetterCase_ShouldBothBeWritten(string firstNamespace, string secondNamespace, string firstSet, string secondSet)
    {
        // Arrange: C# tells Shop from shop, and the compiler compares the names of generated files without regard to
        // case.
        var source = $$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            {{firstNamespace}}
            {
                public class {{firstSet}} : ILocalizationSet, I{{firstSet.Substring(0, firstSet.Length - 2)}}
                {
                    public string Cart => "Cart";
                }
            }

            {{secondNamespace}}
            {
                public class {{secondSet}} : ILocalizationSet, I{{secondSet.Substring(0, secondSet.Length - 2)}}
                {
                    public string Checkout => "Checkout";
                }
            }
            """;

        // Act
        var run = Run(source).Results.ShouldHaveSingleItem();
        var errors = GeneratorTestHelper.CompileWithGenerators(source);

        // Assert: a file per contract, the second by name with a number, and the same on every build.
        run.Exception.ShouldBeNull();
        run.Diagnostics.ShouldBeEmpty();
        var hintNames = run.GeneratedSources.Select(static generated => generated.HintName).Where(static name => name.EndsWith(".Localization.g.cs", StringComparison.Ordinal)).ToArray();
        hintNames.Length.ShouldBe(2);
        hintNames.Distinct(StringComparer.OrdinalIgnoreCase).Count().ShouldBe(2);
        hintNames.ShouldContain(name => name.EndsWith("_2.Localization.g.cs", StringComparison.Ordinal));
        errors.ShouldBeEmpty();
    }

    [Test]
    [Arguments("ShopLocalizationen", "ShopLocalizationEn")]
    [Arguments("ShopTexts", "ShopTextsEn")]
    public void DefaultSetNotNamedAfterItsLanguage_ShouldBeToldTheNameThatEndsInIt(string name, string expectedName)
    {
        // Arrange & Act: a name that ends in its language without a capital gets the suffix in place of that ending,
        // not after it (ShopLocalizationenEn, whose generated class would be ShopLocalizationen).
        var result = Run($$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public sealed partial class {{name}} : ILocalizationSet
            {
                public string LocalizationLanguageCode => "en";
                public string Checkout => "Checkout";
            }
            """);

        // Assert
        var diagnostic = SingleDiagnostic(result, LocalizationSetsPart.CannotExtendDiagnosticId);
        diagnostic.Properties[LocalizationSetsPart.SuggestedNameProperty].ShouldBe(expectedName);
    }

    [Test]
    [Arguments("ShopLocalization", "ShopLocalizationEn")]
    [Arguments("ShopTexts", "ShopTextsEn")]
    [Arguments("ShopLocalizationen", "ShopLocalizationEn")]
    public void DefaultSetWhoseNameGivesNoLanguage_ShouldBeToldTheNameThatEndsInIt(string name, string expectedName)
    {
        // Arrange & Act: the set lists the contract generated from it, as a default-language set does. Its name ends in
        // no language, and it implements no LocalizationLanguageCode.
        var result = Run($$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public sealed class {{name}} : ILocalizationSet, I{{name}}
            {
                public string Checkout => "Checkout";
            }
            """);

        // Assert: the error names the suffix, and carries the name for the fix.
        var diagnostic = SingleDiagnostic(result, LocalizationSetsPart.CannotExtendDiagnosticId);
        diagnostic.GetMessage().ShouldContain("its name doesn't end in En, the suffix of the default language");
        diagnostic.Properties[LocalizationSetsPart.SuggestedNameProperty].ShouldBe(expectedName);
    }

    [Test]
    public void SetWhoseNameGivesNoLanguage_OfAContractAnotherSetStarts_ShouldNotReportLOC200()
    {
        // Arrange & Act: the English set starts the contract. The second set lists that contract under a name that ends
        // in no language, so it's a set of the contract in a language the build can't read, not a second start.
        var result = Run($$"""
            {{Header}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            public sealed class ShopLocalizationDutch : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """);

        // Assert
        DiagnosticIds(result).Contains(LocalizationSetsPart.CannotExtendDiagnosticId, StringComparer.Ordinal).ShouldBeFalse();
    }
}
