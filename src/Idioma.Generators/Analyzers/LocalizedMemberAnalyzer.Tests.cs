using System.Collections.Immutable;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="LocalizedMemberAnalyzer"/>: a member marked <c>[Localized]</c> that gets no text is reported
/// (<c>LOC400</c>), and so is a member of an enum without <c>[Localized]</c> next to members that have it
/// (<c>LOC503</c>), and a call that asks for the text of a member or a type without one (<c>LOC504</c>).
/// </summary>
public sealed class LocalizedMemberAnalyzerTests
{
    #region Fixture
    private const string Header = """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace Shop.Domain;
        """;

    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source, params (string Path, string Text)[] files)
    {
        return GeneratorTestHelper.AnalyzeAsync(new LocalizedMemberAnalyzer(), source, [], files);
    }

    private static string At(Diagnostic diagnostic)
    {
        return diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan);
    }

    /// <summary>
    /// The one <c>LOC400</c> for a source, which is a warning.
    /// </summary>
    private static async Task<Diagnostic> SingleCannotRegisterAsync(string source, params (string Path, string Text)[] files)
    {
        var diagnostic = (await AnalyzeAsync(source, files)).ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(LocalizedMemberAnalyzer.CannotRegisterDiagnosticId);
        diagnostic.Id.ShouldBe("LOC400");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);

        return diagnostic;
    }

    /// <summary>
    /// The model and a call of <paramref name="call"/>, as a form or a table of the shop writes it.
    /// </summary>
    private static string Lookup(string call) => $$"""
        {{Header}}

        [Localized(en: "Person", nl: "Persoon")]
        public class Person
        {
            [Localized(en: "Name", nl: "Naam")]
            public virtual string Name { get; set; } = "";

            public string Email { get; set; } = "";
        }

        public class Customer : Person
        {
            public override string Name { get; set; } = "";

            public string Phone = "";
        }

        public class Page<T>
        {
            [Localized(en: "Items", nl: "Items")]
            public List<T> Items { get; } = [];
        }

        public enum Status
        {
            [Localized(en: "Open", nl: "Open")]
            Open,
        }

        public static class Labels
        {
            public static LocalizableText? Of(System.Type type, string member, System.Reflection.PropertyInfo property) => {{call}};
        }
        """;
    #endregion

    [Test]
    public async Task MarkedMembersThatGetTheirText_ShouldReportNothing()
    {
        // Arrange & Act: a property, a field, a type, the property of a record's parameter, and an enum whose
        // members are all marked.
        var diagnostics = await AnalyzeAsync($$"""
            {{Header}}

            [Localized(en: "Address", nl: "Adres")]
            public class Address
            {
                [Localized(en: "City", nl: "Plaats")]
                public string City { get; } = "";

                [Localized(en: "Note", nl: "Notitie")]
                public string Note = "";

                public string Unmarked { get; } = "";
            }

            public record Line([property: Localized(en: "Quantity", nl: "Aantal")] int Quantity);

            public enum Status
            {
                [Localized(en: "Open", nl: "Open")]
                Open,

                [Localized(en: "Closed", nl: "Gesloten")]
                Closed,
            }
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("private", "private")]
    [Arguments("protected", "protected")]
    [Arguments("private protected", "private protected")]
    public async Task MemberOfTypeGeneratedCodeCannotName_ShouldReportLOC400(string accessibility, string kind)
    {
        // Arrange & Act
        var diagnostic = await SingleCannotRegisterAsync($$"""
            {{Header}}

            public class Order
            {
                {{accessibility}} class Hidden
                {
                    [Localized(en: "Article", nl: "Artikel")]
                    public string Article { get; } = "";
                }
            }
            """);

        // Assert: at the attribute, with what to do about it.
        diagnostic.GetMessage().ShouldBe($"The text of 'Hidden.Article' can't be registered because generated code can't name 'Hidden', which is {kind}, so it won't be found. Make 'Hidden' internal or public.");
        At(diagnostic).ShouldBe("Localized(en: \"Article\", nl: \"Artikel\")");
    }

    [Test]
    public async Task MarkedTypeInsideAPrivateType_ShouldReportTheTypeThatIsInTheWay()
    {
        // Arrange & Act: the marked type is public, and the type around it is not.
        var diagnostic = await SingleCannotRegisterAsync($$"""
            {{Header}}

            public class Order
            {
                private class Hidden
                {
                    [Localized(en: "Line", nl: "Regel")]
                    public class Line;
                }
            }
            """);

        // Assert
        diagnostic.GetMessage().ShouldBe("The text of 'Line' can't be registered because generated code can't name 'Hidden', which is private, so it won't be found. Make 'Hidden' internal or public.");
    }

    [Test]
    public async Task MemberOfFileLocalType_ShouldReportLOC400()
    {
        // Arrange & Act
        var diagnostic = await SingleCannotRegisterAsync($$"""
            {{Header}}

            file class Hidden
            {
                [Localized(en: "Article", nl: "Artikel")]
                public string Article { get; } = "";
            }
            """);

        // Assert
        diagnostic.GetMessage().ShouldBe("The text of 'Hidden.Article' can't be registered because generated code can't name 'Hidden', which is file-local, so it won't be found. Remove 'file' from 'Hidden'.");
    }

    [Test]
    [Arguments("""en: "City", nl: null!""", "nl")]
    [Arguments("""en: null!, nl: "Plaats" """, "en")]
    [Arguments(""" "City", null!""", "nl")]
    public async Task WordingThatIsNull_ShouldReportLOC400WithItsLanguage(string arguments, string language)
    {
        // Arrange & Act
        var diagnostic = await SingleCannotRegisterAsync($$"""
            {{Header}}

            public class Address
            {
                [Localized({{arguments}})]
                public string City { get; } = "";
            }
            """);

        // Assert: the language is named, also for an argument without a name.
        diagnostic.GetMessage().ShouldBe($"The text of 'Address.City' can't be registered because its wording for '{language}' is null, so it won't be found. Write a wording for '{language}'.");
    }

    [Test]
    [Arguments("Page_razor.g.cs")]
    [Arguments("Pages/Index_cshtml.g.cs")]
    public async Task MemberMarkedInRazorOutput_ShouldReportLOC400(string outputPath)
    {
        // Arrange: what Razor writes for a class in the @code of a component. The attribute compiles there, and the
        // source generator that writes the texts never saw it, because no generator sees what another writes.
        const string razorOutput = """
            // <auto-generated/>
            namespace Shop.Domain;

            public partial class Page
            {
                public class Model
                {
                    [Localized(en: "City", nl: "Plaats")]
                    public string City { get; set; } = "";
                }
            }
            """;

        // Act
        var diagnostic = await SingleCannotRegisterAsync(Header, (outputPath, razorOutput));

        // Assert: at the attribute, with where to move the class.
        diagnostic.GetMessage().ShouldBe("The text of 'Model.City' can't be registered because it's written in markup, which the source generator can't read, so it won't be found. Move 'Model' to a C# file.");
        At(diagnostic).ShouldBe("Localized(en: \"City\", nl: \"Plaats\")");
    }

    [Test]
    public async Task MemberMarkedInOtherGeneratedCode_ShouldReportNothing()
    {
        // Arrange: whoever generated this is the only one who could fix it.
        const string generated = """
            // <auto-generated/>
            namespace Shop.Domain;

            public class Generated
            {
                [Localized(en: "City", nl: null!)]
                public string City { get; set; } = "";
            }
            """;

        // Act
        var diagnostics = await AnalyzeAsync(Header, ("Generated.g.cs", generated));

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task MarkedIndexer_ShouldReportLOC400()
    {
        // Arrange & Act: the compiler takes the attribute on an indexer, which is a property to it.
        var diagnostic = await SingleCannotRegisterAsync($$"""
            {{Header}}

            public class Lines
            {
                [Localized(en: "Line", nl: "Regel")]
                public string this[int index] => "";
            }
            """);

        // Assert
        diagnostic.GetMessage().ShouldBe("The text of 'this[]' can't be registered because an indexer has no name to find it by, so it won't be found. Remove the attribute.");
    }

    [Test]
    [Arguments("""[field: Localized(en: "City", nl: "Plaats")] public string City { get; } = "";""", "Write the attribute without 'field:'")]
    public async Task AttributeOnTheFieldBehindAProperty_ShouldReportLOC400(string member, string advice)
    {
        // Arrange & Act: 'field:' puts the attribute on the field the compiler keeps the property's value in.
        var diagnostic = await SingleCannotRegisterAsync($$"""
            {{Header}}

            public class Address
            {
                {{member}}
            }
            """);

        // Assert
        diagnostic.GetMessage().ShouldBe($"The text of 'City' can't be registered because the attribute is on the field behind the property, so it won't be found. {advice}.");
    }

    [Test]
    public async Task EnumWithMarkedAndUnmarkedMembers_ShouldReportLOC503ForEachUnmarkedOne()
    {
        // Arrange & Act: two members added later, without a text.
        var diagnostics = await AnalyzeAsync($$"""
            {{Header}}

            public enum ParcelStatus
            {
                [Localized(en: "Assigned", nl: "Toegewezen")]
                Assigned,

                Lost,

                [Localized(en: "Picked", nl: "Gepickt")]
                Picked,

                Returned = 9,
            }
            """);

        // Assert: at the name of each, as a warning, with the attribute to write.
        diagnostics.Length.ShouldBe(2);
        diagnostics.ShouldAllBe(static diagnostic => diagnostic.Id == "LOC503" && diagnostic.Severity == DiagnosticSeverity.Warning);
        diagnostics.Select(At).ShouldBe(["Lost", "Returned"]);
        diagnostics[0].GetMessage().ShouldBe("'ParcelStatus.Lost' has no text, but other members of 'ParcelStatus' have one, so users would see 'Lost' as written. Mark it [Localized(en: \"...\", nl: \"...\")].");
    }

    [Test]
    public async Task UnmarkedEnumMemberWithValueOfMarkedOne_ShouldReportNothing()
    {
        // Arrange & Act: GetText() finds a text by the value, so Default and Initial show the text of Active. Flags
        // that are combined have a value of their own, and are reported.
        var diagnostics = await AnalyzeAsync($$"""
            {{Header}}

            [System.Flags]
            public enum Access
            {
                Default = Read,

                [Localized(en: "Read", nl: "Lezen")]
                Read = 1,

                [Localized(en: "Write", nl: "Schrijven")]
                Write = 2,

                Initial = 1,
                ReadWrite = Read | Write,
            }
            """);

        // Assert
        diagnostics.Select(At).ShouldBe(["ReadWrite"]);
    }

    [Test]
    public async Task EnumMemberWhoseAttributeDoesNotCompile_ShouldCountAsMarked()
    {
        // Arrange & Act: an attribute that leaves a language out, which the compiler reports (CS7036).
        var diagnostics = await AnalyzeAsync($$"""
            {{Header}}

            public enum ParcelStatus
            {
                [Localized(en: "Assigned", nl: "Toegewezen")]
                Assigned,

                [Localized(en: "Picked")]
                Picked,
            }
            """);

        // Assert: no second message about a member that is marked.
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task EnumWithoutMarkedMember_ShouldReportNothing()
    {
        // Arrange & Act: an enum with a text of its own only, whose values may never be shown, and an enum without
        // any text.
        var diagnostics = await AnalyzeAsync($$"""
            {{Header}}

            [Localized(en: "Status", nl: "Status")]
            public enum PackageStatus
            {
                Assigned,
                Picked,
            }

            public enum Untranslated
            {
                One,
                Two,
            }
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task UnmarkedMemberWithThePragma_ShouldBeLeftAlone()
    {
        // Arrange & Act: a member that is never shown, which its author says so.
        var diagnostics = await AnalyzeAsync($$"""
            {{Header}}

            public enum ParcelStatus
            {
                [Localized(en: "Assigned", nl: "Toegewezen")]
                Assigned,

            #pragma warning disable LOC503
                Internal,
            #pragma warning restore LOC503
            }
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task EnumMarkedWithAnAttributeTheCodeBringsItself_ShouldReportNothing()
    {
        // Arrange & Act: an attribute named Localized in the code's own namespace, which is not the generated one.
        var diagnostics = await AnalyzeAsync($$"""
            {{Header}}

            [System.AttributeUsage(System.AttributeTargets.All)]
            public sealed class LocalizedAttribute(string en, string nl) : System.Attribute
            {
                public string En { get; } = en;
                public string Nl { get; } = nl;
            }

            public enum ParcelStatus
            {
                [Localized(en: "Assigned", nl: null!)]
                Assigned,

                Lost,
            }
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task AssemblyWithoutLanguages_ShouldReportNothing()
    {
        // Arrange & Act: without languages there's no attribute, which LOC100 reports.
        var diagnostics = await AnalyzeAsync("""
            namespace Shop.Domain;

            public enum ParcelStatus
            {
                [Localized(en: "Assigned", nl: "Toegewezen")]
                Assigned,

                Lost,
            }
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("LocalizableText.GetTextByMember<Person>(nameof(Person.Name))")]
    [Arguments("LocalizableText.GetTextByMember(typeof(Person), \"Name\")")]
    [Arguments("LocalizableText.GetTextByMember<Customer>(nameof(Customer.Name))")] // From the property it overrides
    [Arguments("LocalizableText.GetTextByMember<Page<int>>(nameof(Page<int>.Items))")]
    [Arguments("LocalizableText.GetTextByMember(typeof(Page<>), \"Items\")")]
    [Arguments("LocalizableText.GetTextByMember<Status>(nameof(Status.Open))")]
    [Arguments("LocalizableText.GetTextOfType<Person>()")]
    [Arguments("LocalizableText.GetTextOfType(typeof(Person))")]
    public async Task LookupOfMemberOrTypeWithText_ShouldReportNothing(string call)
    {
        // Arrange & Act
        var diagnostics = await AnalyzeAsync(Lookup(call));

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("LocalizableText.GetTextByMember(type, member)")]                   // Known at run time alone
    [Arguments("LocalizableText.GetTextByMember(type, \"Email\")")]
    [Arguments("LocalizableText.GetTextByMember<Person>(member)")]
    [Arguments("LocalizableText.GetTextByMember(property)")]
    [Arguments("LocalizableText.GetTextOfType(type)")]
    [Arguments("LocalizableText.GetTextByMember<System.Uri>(nameof(System.Uri.Host))")] // System.Private.Uri uses no Idioma, but is in the next test
    public async Task LookupTheBuildCantCheck_ShouldReportNothing(string call)
    {
        // Arrange: what a form does with the property of a field it shows, where null means no label.
        var source = Lookup(call);

        // Act
        var diagnostics = await AnalyzeAsync(source);

        // Assert: Uri lies in an assembly without Idioma, so only that one is reported.
        if (call.Contains("System.Uri", StringComparison.Ordinal))
            diagnostics.ShouldHaveSingleItem().Id.ShouldBe("LOC504");
        else
            diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("LocalizableText.GetTextByMember<Person>(nameof(Person.Email))", "nameof(Person.Email)", "'Person.Email' has no text, so GetTextByMember gives null. Mark 'Email' [Localized(en: \"...\", nl: \"...\")].")]
    [Arguments("LocalizableText.GetTextByMember(typeof(Person), \"Email\")", "\"Email\"", "'Person.Email' has no text, so GetTextByMember gives null. Mark 'Email' [Localized(en: \"...\", nl: \"...\")].")]
    [Arguments("LocalizableText.GetTextByMember<Customer>(nameof(Customer.Email))", "nameof(Customer.Email)", "'Customer.Email' has no text, so GetTextByMember gives null. Mark 'Person.Email' [Localized(en: \"...\", nl: \"...\")].")]
    [Arguments("LocalizableText.GetTextByMember<Customer>(nameof(Customer.Phone))", "nameof(Customer.Phone)", "'Customer.Phone' has no text, so GetTextByMember gives null. Mark 'Phone' [Localized(en: \"...\", nl: \"...\")].")]
    [Arguments("LocalizableText.GetTextByMember<Person>(\"Nmae\")", "\"Nmae\"", "'Person.Nmae' has no text, so GetTextByMember gives null. 'Person' has no property or field of that name. Name the member with nameof, which the compiler checks.")]
    [Arguments("LocalizableText.GetTextOfType<Customer>()", "GetTextOfType<Customer>", "'Customer' has no text, so GetTextOfType gives null. Mark 'Customer' [Localized(en: \"...\", nl: \"...\")].")]
    [Arguments("LocalizableText.GetTextOfType(typeof(Customer))", "GetTextOfType", "'Customer' has no text, so GetTextOfType gives null. Mark 'Customer' [Localized(en: \"...\", nl: \"...\")].")]
    public async Task LookupOfMemberOrTypeWithoutText_ShouldReportLOC504(string call, string at, string message)
    {
        // Arrange & Act: a type's own text isn't inherited, so Customer has none.
        var diagnostic = (await AnalyzeAsync(Lookup(call))).ShouldHaveSingleItem();

        // Assert
        diagnostic.Id.ShouldBe(LocalizedMemberAnalyzer.NoTextToFindDiagnosticId);
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldBe(message);
        At(diagnostic).ShouldBe(at);
    }

    [Test]
    [Arguments("LocalizableText.GetTextOfType<System.DayOfWeek>()", "GetTextOfType<System.DayOfWeek>", "'DayOfWeek' has no text, so GetTextOfType gives null.")]
    [Arguments("LocalizableText.GetTextByMember<System.DayOfWeek>(nameof(System.DayOfWeek.Monday))", "nameof(System.DayOfWeek.Monday)", "'DayOfWeek.Monday' has no text, so GetTextByMember gives null.")]
    public async Task LookupOfTypeOfAssemblyWithoutIdioma_ShouldReportLOC504(string call, string at, string start)
    {
        // Arrange & Act: an assembly that doesn't reference the package has no texts on its members.
        var diagnostic = (await AnalyzeAsync(Lookup(call))).ShouldHaveSingleItem();

        // Assert: the assembly as the compilation references it, which is System.Runtime in a build.
        diagnostic.Id.ShouldBe(LocalizedMemberAnalyzer.NoTextToFindDiagnosticId);
        diagnostic.GetMessage().ShouldStartWith($"{start} 'DayOfWeek' is a type of '");
        diagnostic.GetMessage().ShouldEndWith("', which doesn't use Idioma, so it has no texts written as attributes.");
        At(diagnostic).ShouldBe(at);
    }

    [Test]
    public async Task LookupOfTypeParameter_ShouldReportNothing()
    {
        // Arrange & Act: the type comes from the caller.
        var diagnostics = await AnalyzeAsync($$"""
            {{Header}}

            public static class Labels
            {
                public static LocalizableText? Of<T>() => LocalizableText.GetTextByMember<T>("Name");

                public static LocalizableText? OfType<T>() => LocalizableText.GetTextOfType<T>();
            }
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task LookupOfTypeOfLibraryThatUsesIdioma_ShouldReportNothing()
    {
        // Arrange: the attribute isn't kept in the library's assembly, so the build can't see which member has a text.
        var library = GeneratorTestHelper.CompileLibraryWithGenerators("LookupLibrary", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace LookupLibrary;

            public class Address
            {
                [Localized(en: "City", nl: "Plaats")]
                public string City { get; set; } = "";

                public string Street { get; set; } = "";
            }
            """);

        // Act
        var diagnostics = await GeneratorTestHelper.AnalyzeAsync(new LocalizedMemberAnalyzer(), $$"""
            {{Header}}

            public class Delivery : LookupLibrary.Address;

            public static class Labels
            {
                public static LocalizableText? City => LocalizableText.GetTextByMember<LookupLibrary.Address>(nameof(LookupLibrary.Address.City));
                public static LocalizableText? Street => LocalizableText.GetTextByMember<LookupLibrary.Address>(nameof(LookupLibrary.Address.Street));
                public static LocalizableText? Inherited => LocalizableText.GetTextByMember<Delivery>(nameof(Delivery.City));
            }
            """, [library]);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task LookupWithoutText_ShouldBeQuietUnderPragma()
    {
        // Arrange & Act: a call that wants null on purpose.
        var diagnostics = await AnalyzeAsync($$"""
            {{Header}}

            public class Address
            {
                public string Street { get; set; } = "";
            }

            public static class Labels
            {
            #pragma warning disable LOC504 // No label yet
                public static LocalizableText? Street => LocalizableText.GetTextByMember<Address>(nameof(Address.Street));
            #pragma warning restore LOC504
            }
            """);

        // Assert
        diagnostics.ShouldAllBe(diagnostic => diagnostic.IsSuppressed);
    }
}
