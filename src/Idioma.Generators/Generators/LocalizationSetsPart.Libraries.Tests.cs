using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

public sealed partial class LocalizationSetsPartTests
{
    [Test]
    public void LibraryMissingLanguage_ShouldReportLOC700AtAttribute()
    {
        // Arrange & Act
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]

            namespace TestApp;
            """, GridLibrary);

        // Assert
        var diagnostic = SingleDiagnostic(result, LocalizationSetsPart.LibraryMissingLanguageDiagnosticId);
        diagnostic.Id.ShouldBe("LOC700");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("No set serves 'GridLibrary.IGridLocalization' in 'de'");
        diagnostic.GetMessage().ShouldContain("'class GridLocalizationDe : ILocalizationSet, GridLibrary.IGridLocalization'");
        diagnostic.Location.GetLineSpan().StartLinePosition.Line.ShouldBe(2);
    }

    [Test]
    [Arguments("public", "'class OrderStatusLocalizationDe : ILocalizationSet<StatusLibrary.OrderStatus>, StatusLibrary.IOrderStatusLocalization'")]
    [Arguments("internal", "'class OrderStatusLocalizationDe : ILocalizationSet, StatusLibrary.IOrderStatusLocalization'")] // The app can't name the enum
    public void LibraryEnumMissingLanguage_ShouldSuggestSetOfEnum(string accessibility, string suggestion)
    {
        // Arrange
        var library = GeneratorTestHelper.CompileLibraryWithGenerators($"StatusLibrary{accessibility}", $$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace StatusLibrary;

            {{accessibility}} enum OrderStatus { Pending }

            public class OrderStatusLocalizationEn : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
            {
                public string Pending => "Pending";
            }

            public class OrderStatusLocalizationNl : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
            {
                public string Pending => "In behandeling";
            }
            """);

        // Act: every set of the contract of an enum lists ILocalizationSet<TEnum> (LOC205).
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]

            namespace TestApp;
            """, library);

        // Assert
        SingleDiagnostic(result, LocalizationSetsPart.LibraryMissingLanguageDiagnosticId).GetMessage().ShouldContain(suggestion);
    }

    [Test]
    public void LibraryLanguageAddedByApp_ShouldReportNothing()
    {
        // Arrange & Act
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]

            namespace TestApp;

            public sealed class GridLocalizationDe : ILocalizationSet, GridLibrary.IGridLocalization
            {
                public string First => "Erste";
            }
            """, GridLibrary);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void LibraryLanguageAddedByExcludedSet_ShouldReportNothing()
    {
        // Arrange & Act
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]

            namespace TestApp;

            [ExcludeFromLocalizationDiscovery]
            public sealed class GridLocalizationDe(string first) : ILocalizationSet, GridLibrary.IGridLocalization
            {
                public string First => first;
            }
            """, GridLibrary);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void LibraryLanguageOfExcludedSet_ShouldReportNothing()
    {
        // Arrange: a library whose Dutch set needs a constructor argument, so the library registers it itself.
        var library = GeneratorTestHelper.CompileLibraryWithGenerators("ExcludingLibrary", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace ExcludingLibrary;

            public class ListLocalizationEn : ILocalizationSet, IListLocalization
            {
                public virtual string Empty => "Nothing yet";
            }

            [ExcludeFromLocalizationDiscovery]
            public sealed class ListLocalizationNl(string empty) : ILocalizationSet, IListLocalization
            {
                public string Empty => empty;
            }
            """);

        // Act
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;
            """, library);

        // Assert: no LOC700 asking the app for the Dutch set the library has.
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void LibraryLanguageAddedByOtherLibrary_ShouldReportNothing()
    {
        // Arrange: a translations package an app only has to reference.
        var german = GeneratorTestHelper.CompileLibraryWithGenerators("GridLibrary.German", """
            namespace GridLibrary.German;

            public sealed class GridLocalizationDe : CodeChops.Idioma.ILocalizationSet, GridLibrary.IGridLocalization
            {
                public string First => "Erste";
            }
            """, GridLibrary);

        // Act
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]

            namespace TestApp;
            """, GridLibrary, german);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void AppLanguagesWithinLibrary_ShouldReportNothing()
    {
        // Arrange & Act: one language less than the library has is no gap, and a regional one resolves through its
        // language.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("nl-BE")]

            namespace TestApp;
            """, GridLibrary);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void LibraryWithInlineTextsMissingLanguage_ShouldWarnWithLOC701()
    {
        // Arrange & Act: nothing the app could add, so a warning rather than an error it can't fix.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]

            namespace TestApp;
            """, InlineLibrary);

        // Assert
        var diagnostic = SingleDiagnostic(result, LocalizationSetsPart.LibraryCannotBeTranslatedDiagnosticId);
        diagnostic.Id.ShouldBe("LOC701");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().ShouldBe("'InlineLibrary' has standalone texts without 'de', which this assembly supports. No set added here can translate them, so users of 'de' will see them in 'en'.");
    }

    [Test]
    [Arguments("""("nl", "de")""", "nl")]    // The library has the app's default language, which a text falls back to first
    [Arguments("""("en-GB", "de")""", "en-GB")] // Its English serves British English
    [Arguments("""("fr", "de")""", "en")]    // It hasn't, so the text falls back to its own default language
    public void LibraryWithInlineTextsMissingLanguage_ShouldNameTheLanguageUsersSee(string languages, string shown)
    {
        // Arrange & Act
        var result = Run($$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages{{languages}}]

            namespace TestApp;
            """, InlineLibrary);

        // Assert
        result.Results.ShouldHaveSingleItem().Diagnostics
            .Where(static diagnostic => diagnostic.Id is LocalizationSetsPart.LibraryCannotBeTranslatedDiagnosticId)
            .ShouldAllBe(diagnostic => diagnostic.GetMessage().EndsWith($"will see them in '{shown}'.", StringComparison.Ordinal));
    }

    [Test]
    public void LibraryWithTextsOnMembersMissingLanguage_ShouldWarnWithLOC701()
    {
        // Arrange: a library whose only texts are on its members. They carry their wording themselves, as an inline
        // text does, so no set of the app can add German to them.
        var library = GeneratorTestHelper.CompileLibraryWithGenerators("MemberLibrary", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace MemberLibrary;

            public class Address
            {
                [Localized(en: "City", nl: "Plaats")]
                public string City { get; } = "";
            }
            """);

        // Act
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]

            namespace TestApp;
            """, library);

        // Assert
        var diagnostic = SingleDiagnostic(result, LocalizationSetsPart.LibraryCannotBeTranslatedDiagnosticId);
        diagnostic.Id.ShouldBe("LOC701");
        diagnostic.GetMessage().ShouldContain("'MemberLibrary' has standalone texts without 'de'");
    }

    [Test]
    public void LibraryWithSetsAndInlineTextsMissingLanguage_ShouldReportBoth()
    {
        // Arrange: the app can add German to the contract (LOC700), but not to the inline text (LOC701).
        var library = GeneratorTestHelper.CompileLibraryWithGenerators("MixedLibrary", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace MixedLibrary;

            public class MixedLocalizationEn : ILocalizationSet, IMixedLocalization
            {
                public virtual string First => "First";
            }

            public sealed class MixedLocalizationNl : ILocalizationSet, IMixedLocalization
            {
                public string First => "Eerste";
            }

            public static class MixedTexts
            {
                public static string Orders => LocalizedText.Inline(en: "Orders", nl: "Bestellingen");
            }
            """);

        // Act
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]

            namespace TestApp;
            """, library);

        // Assert
        DiagnosticIds(result).ShouldBe(["LOC700", "LOC701"]);
    }

    [Test]
    public void LibraryWithLanguagesButNoInlineTexts_ShouldReportNothing()
    {
        // Arrange: languages declared, nothing written in them.
        var library = GeneratorTestHelper.CompileLibraryWithGenerators("QuietLibrary", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace QuietLibrary;

            public static class Nothing;
            """);

        // Act
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]

            namespace TestApp;
            """, library);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }
}
