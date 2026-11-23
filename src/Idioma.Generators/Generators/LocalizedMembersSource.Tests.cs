using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="LocalizedMembersSource"/>, as <see cref="LocalizationRegistrationPart"/> writes it: a
/// member marked <c>[Localized]</c> gets a text written as code, with its full name as its key, the assembly gets one
/// function that finds a member's text, and an enum with a marked member gets <c>GetText()</c>.
/// </summary>
public sealed class LocalizedMembersSourceTests
{
    #region Fixture
    private const string Header = """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace Shop.Domain;
        """;

    private static GeneratorDriverRunResult Run(string source) => GeneratorTestHelper.Run(LocalizationParts.Registration(), source);

    /// <summary>
    /// The registrar, or <see langword="null"/> when none is written.
    /// </summary>
    private static string? Registrar(GeneratorDriverRunResult result)
    {
        var run = result.Results.ShouldHaveSingleItem();
        run.Exception.ShouldBeNull();

        return run.GeneratedSources.SingleOrDefault(source => source.HintName is "LocalizationRegistrar.g.cs").SourceText?.ToString();
    }

    /// <summary>
    /// The file with the texts of the members, or <see langword="null"/> when none is written.
    /// </summary>
    private static string? Texts(GeneratorDriverRunResult result)
    {
        var run = result.Results.ShouldHaveSingleItem();
        run.Exception.ShouldBeNull();

        return run.GeneratedSources.SingleOrDefault(source => source.HintName is "LocalizedMembers.g.cs").SourceText?.ToString();
    }

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

    #endregion

    [Test]
    public Task MembersOfClassAndEnum_ShouldEmitTextPerMemberAndFunctionThatFindsThem()
    {
        // Arrange & Act
        var text = Texts(Run($$"""
            {{Header}}

            public class Address
            {
                [Localized(en: "City", nl: "Plaats")]
                public string City { get; } = "";

                [Localized(en: "Postal code", nl: "Postcode")]
                public string PostalCode { get; } = "";

                public string Unmarked { get; } = "";
            }

            [Localized(en: "Status", nl: "Status")]
            public enum PackageStatus
            {
                [Localized(en: "Assigned", nl: "Toegewezen")]
                Assigned,

                [Localized(en: "Picked", nl: "Gepickt")]
                Picked,

                Unmarked,
            }
            """))!;

        // Assert
        return Verify(text);
    }

    [Test]
    public void EveryKindOfMember_ShouldCompileWithoutWarningsAndWithResolvedCrefs()
    {
        // Arrange: a property, a field, two fields of one declaration, the property of a record's parameter, a
        // struct, an interface, a type inside another, a type with type parameters, an enum inside a class, an enum
        // in the global namespace, an internal enum, and names that are keywords.
        const string source = """
            #nullable enable
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "en-GB")]

            [Localized(en: "Size", nl: "Maat", enGb: "Size")]
            public enum Size
            {
                [Localized(en: "Small", nl: "Klein", enGb: "Small")]
                Small,
            }

            namespace Shop.Domain
            {
                [Localized(en: "Address", nl: "Adres", enGb: "Address")]
                public class Address
                {
                    [Localized(en: "City", nl: "Plaats", enGb: "City")]
                    public string City { get; } = "";

                    [Localized(en: "Color", nl: "Kleur", enGb: "Colour")]
                    public string Colour = "";

                    [Localized(en: "Width or height", nl: "Breedte of hoogte", enGb: "Width or height")]
                    public int Width, Height;

                    [Localized(en: "Class", nl: "Klasse", enGb: "Class")]
                    public string @class { get; } = "";
                }

                public record Line([property: Localized(en: "Quantity", nl: "Aantal", enGb: "Quantity")] int Quantity);

                public struct Money
                {
                    [Localized(en: "Amount", nl: "Bedrag", enGb: "Amount")]
                    public decimal Amount { get; set; }
                }

                public interface IHasName
                {
                    [Localized(en: "Name", nl: "Naam", enGb: "Name")]
                    string Name { get; }
                }

                public class Order
                {
                    public class Line
                    {
                        [Localized(en: "Article", nl: "Artikel", enGb: "Article")]
                        public string Article { get; } = "";
                    }

                    public enum Status
                    {
                        [Localized(en: "Open", nl: "Open", enGb: "Open")]
                        Open,

                        [Localized(en: "Default", nl: "Standaard", enGb: "Default")]
                        @default,
                    }
                }

                public class Page<T>
                {
                    [Localized(en: "Items", nl: "Regels", enGb: "Items")]
                    public T[] Items { get; } = [];

                    public enum Kind
                    {
                        [Localized(en: "First", nl: "Eerste", enGb: "First")]
                        First,
                    }
                }

                internal enum Speed
                {
                    [Localized(en: "Fast", nl: "Snel", enGb: "Fast")]
                    Fast,

                    [Localized(en: "Quick", nl: "Vlot", enGb: "Quick")]
                    Quick = Fast,
                }

                public static class Usage
                {
                    public static string Small => Size.Small.GetText();
                    public static string Open => Order.Status.Open.GetText();
                    internal static string Fast => Speed.Quick.GetText();
                    public static string? City => LocalizableText.GetTextByMember<Address>(nameof(Address.City));

                    // As docs/attributes.md shows it: the label of a property, or else its name.
                    public static string Label(System.Reflection.PropertyInfo property) => LocalizableText.GetTextByMember(property) ?? property.Name;
                }
            }
            """;

        // Act
        var errors = GeneratorTestHelper.CompileWithGenerators(source);
        var warnings = GeneratorTestHelper.GetWarningsWithGenerators(source);
        var unresolved = GeneratorTestHelper.GetUnresolvedDocReferences(source);
        var text = Texts(Run(source))!;

        // Assert: a type inside another is named with dots, and a type with type parameters by its definition. An
        // enum inside such a type gets no extension method, and its texts are still found. Each text is its wording
        // per language, in the order of the assembly's languages.
        errors.ShouldBeEmpty();
        warnings.ShouldBeEmpty();
        unresolved.ShouldBeEmpty();
        text.ShouldContain("if (type == typeof(Shop.Domain.Order.Line))");
        text.ShouldContain("if (type == typeof(Shop.Domain.Page<>))");
        text.ShouldContain("if (type == typeof(Shop.Domain.Page<>.Kind))");
        text.ShouldContain("internal static readonly string[] Languages = [\"en\", \"nl\", \"en-GB\"];");
        text.ShouldContain("internal static readonly string[] Text0 = [\"Items\", \"Regels\", \"Items\"];");
        text.ShouldContain(" = [\"Article\", \"Artikel\", \"Article\"];");
        text.ShouldContain("internal static readonly string[] OwnText = [\"Address\", \"Adres\", \"Address\"];");
        text.ShouldContain("internal static readonly string[] OwnText = [\"Size\", \"Maat\", \"Size\"];");
        text.ShouldContain(" = [\"Quantity\", \"Aantal\", \"Quantity\"];");
        text.ShouldContain(" = [\"Color\", \"Kleur\", \"Colour\"];");
        text.ShouldNotContain("CreateMemberText");
        text.ShouldContain("\"Width\" =>");
        text.ShouldContain("\"Height\" =>");
        text.ShouldContain("\"class\" =>");
        text.ShouldContain("public static class SizeLocalizedExtensions");
        text.ShouldContain("public static class Order_StatusLocalizedExtensions");
        text.ShouldContain("Shop.Domain.Order.Status.@default =>");
        text.ShouldContain("internal static class SpeedLocalizedExtensions");
        text.ShouldNotContain("KindLocalizedExtensions");

        // Of two members of one value, the first gives the value its text.
        text.ShouldContain("Shop.Domain.Speed.Fast =>");
        text.ShouldNotContain("Shop.Domain.Speed.Quick =>");
    }

    [Test]
    public void AssemblyWithoutMarkedMember_ShouldEmitNothing()
    {
        // Arrange & Act
        var text = Texts(Run($$"""
            {{Header}}

            public class Address
            {
                public string City { get; } = "";
            }
            """));

        // Assert
        text.ShouldBeNull();
    }

    [Test]
    public void AssemblyWithoutLanguages_ShouldEmitNothing()
    {
        // Arrange & Act: without languages there's no attribute either.
        var text = Texts(Run("""
            namespace Shop.Domain;

            public class Address
            {
                [Localized(en: "City", nl: "Plaats")]
                public string City { get; } = "";
            }
            """));

        // Assert
        text.ShouldBeNull();
    }

    [Test]
    public void AttributeOfThatNameTheCodeBringsItself_ShouldBeLeftAlone()
    {
        // Arrange & Act: an attribute named Localized in a namespace of the code's own binds, so it isn't the
        // generated one.
        var text = Texts(Run($$"""
            {{Header}}

            public sealed class LocalizedAttribute(string en, string nl) : System.Attribute
            {
                public string En { get; } = en;
                public string Nl { get; } = nl;
            }

            public class Address
            {
                [Localized(en: "City", nl: "Plaats")]
                public string City { get; } = "";
            }
            """));

        // Assert
        text.ShouldBeNull();
    }

    [Test]
    public void AttributeThatDoesNotCompile_ShouldGetNoTextAndNoMessageOfItsOwn()
    {
        // Arrange: a language left out, a language the assembly doesn't list, and a wording that is no constant. The
        // compiler reports each, so the generator adds nothing.
        var source = $$"""
            {{Header}}

            public class Address
            {
                public static string Wording => "City";

                [Localized(en: "City")]
                public string City { get; } = "";

                [Localized(en: "Street", nl: "Straat", de: "Straße")]
                public string Street { get; } = "";

                [Localized(en: Wording, nl: "Naam")]
                public string Name { get; } = "";

                [Localized(en: "Country", nl: "Land")]
                public string Country { get; } = "";
            }
            """;

        // Act
        var diagnostics = GeneratorTestHelper.GetGeneratorDiagnostics(source);
        var errors = GeneratorTestHelper.CompileWithGenerators(source);
        var text = Texts(Run(source))!;

        // Assert
        diagnostics.ShouldBeEmpty();
        errors.Select(static error => error.Id).ShouldBe(["CS7036", "CS1739", "CS0182"], ignoreOrder: true);
        text.ShouldContain("\"Country\" =>");
        text.ShouldNotContain("\"City\" =>");
        text.ShouldNotContain("\"Street\" =>");
        text.ShouldNotContain("\"Name\" =>");
    }

    [Test]
    public void WordingWrittenAsConstant_ShouldBeReadAsItsValue()
    {
        // Arrange & Act: a constant, and two put together.
        var text = Texts(Run($$"""
            {{Header}}

            public class Address
            {
                private const string Dutch = "Plaats";

                [Localized(en: "Ci" + "ty", nl: Dutch)]
                public string City { get; } = "";

                [Localized(en: "He said \"hi\"", nl: "Hij zei \"hoi\"")]
                public string Quote { get; } = "";
            }
            """))!;

        // Assert: a quote is escaped as C# writes it.
        text.ShouldContain("internal static readonly string[] Text0 = [\"City\", \"Plaats\"];");
        text.ShouldContain("""internal static readonly string[] Text1 = ["He said \"hi\"", "Hij zei \"hoi\""];""");
    }

    [Test]
    public void UnnamedArguments_ShouldTakeTheLanguagesInOrder()
    {
        // Arrange & Act: LOC501 reports the missing names. The text is what the compiler makes of the arguments.
        var text = Texts(Run($$"""
            {{Header}}

            public class Address
            {
                [Localized("City", "Plaats")]
                public string City { get; } = "";
            }
            """))!;

        // Assert
        text.ShouldContain("internal static readonly string[] Text0 = [\"City\", \"Plaats\"];");
    }

    [Test]
    [Arguments("private")]
    [Arguments("protected")]
    [Arguments("private protected")]
    [Arguments("file")]
    public void MemberOfTypeGeneratedCodeCannotName_ShouldGetNoTextAndStillCompile(string accessibility)
    {
        // Arrange: a type inside another that generated code at the top level can't name, or a file-local one.
        var hidden = $$"""
            {{(accessibility is "file" ? "file" : accessibility)}} class Hidden
            {
                [Localized(en: "Article", nl: "Artikel")]
                public string Article { get; } = "";
            }
            """;

        var source = $$"""
            {{Header}}

            public class Order
            {
                {{(accessibility is "file" ? "" : hidden)}}

                [Localized(en: "Number", nl: "Nummer")]
                public int Number { get; }
            }

            {{(accessibility is "file" ? hidden : "")}}
            """;

        // Act
        var text = Texts(Run(source))!;

        // Assert: the other members of the assembly keep their texts. LocalizedMemberAnalyzer reports the member.
        text.ShouldContain("\"Number\" =>");
        text.ShouldNotContain("Article");
        GeneratorTestHelper.CompileWithGenerators(source).ShouldBeEmpty();
    }

    [Test]
    [Arguments("[System.Diagnostics.CodeAnalysis.Experimental(\"SHOP001\")]", "", "")]
    [Arguments("", "[System.Diagnostics.CodeAnalysis.Experimental(\"SHOP001\")]", "")]
    [Arguments("", "", "[System.Diagnostics.CodeAnalysis.Experimental(\"SHOP001\")]")]
    [Arguments("[System.Obsolete(\"Use the new order.\", DiagnosticId = \"SHOP002\")]", "", "")]
    [Arguments("", "", "[System.Obsolete(\"Use the new status.\", DiagnosticId = \"SHOP002\")]")]
    public void TypeOrEnumMemberWithDiagnosticOfItsOwn_LeavesNothingInGeneratedCode(string onClass, string onEnum, string onEnumMember)
    {
        // Arrange: naming the type or the member reports a diagnostic of its author's choosing, which a build treats as
        // an error (Experimental) or stops at when it treats warnings as errors (Obsolete).
        var source = $$"""
            {{Header}}

            {{onClass}}
            public class Order
            {
                [Localized(en: "Number", nl: "Nummer")]
                public int Number { get; }
            }

            {{onEnum}}
            public enum OrderStatus
            {
                {{onEnumMember}}
                [Localized(en: "Pending", nl: "In behandeling")]
                Pending,
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
    public void WordingThatIsNull_ShouldGetNoText()
    {
        // Arrange & Act: LocalizedMemberAnalyzer reports it.
        var text = Texts(Run($$"""
            {{Header}}

            public class Address
            {
                [Localized(en: "City", nl: null!)]
                public string City { get; } = "";
            }
            """));

        // Assert
        text.ShouldBeNull();
    }

    [Test]
    [Arguments("using Tooling;")]
    [Arguments("global using Tooling;")]
    public void AttributeOfThatNameBehindAUsingOfTheFile_ShouldGetItsTextBecauseTheGeneratedOneWins(string usingAtTop)
    {
        // Arrange: the code has an attribute of that name in another namespace, as an app has that moves from one of
        // its own. C# looks in the global namespace, where the generated attribute lies, before it looks in the usings
        // of the file. So the build binds the member's attribute to the generated one.
        var source = $$"""
            {{usingAtTop}}
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace Tooling
            {
                public sealed class LocalizedAttribute(string en, string nl) : System.Attribute
                {
                    public string En { get; } = en;
                    public string Nl { get; } = nl;
                }
            }

            namespace Shop.Domain
            {
                public class Address
                {
                    [Localized(en: "City", nl: "Plaats")]
                    public string City { get; } = "";
                }
            }
            """;

        // Act
        var text = Texts(Run(source));

        // Assert
        text.ShouldNotBeNull().ShouldContain("internal static readonly string[] Text0 = [\"City\", \"Plaats\"];");
        GeneratorTestHelper.CompileWithGenerators(source).ShouldBeEmpty();
    }

    [Test]
    [Arguments("namespace Shop.Domain { using Tooling; public class Address { [Localized(en: \"City\", nl: \"Plaats\")] public string City { get; } = \"\"; } }")]
    [Arguments("namespace Tooling.Domain { public class Address { [Localized(en: \"City\", nl: \"Plaats\")] public string City { get; } = \"\"; } }")]
    [Arguments("namespace Shop.Domain { using LocalizedAttribute = Tooling.LocalizedAttribute; public class Address { [Localized(en: \"City\", nl: \"Plaats\")] public string City { get; } = \"\"; } }")]
    public void AttributeOfThatNameInReachBeforeTheGlobalNamespace_ShouldBeLeftAlone(string usage)
    {
        // Arrange: behind a using written inside the namespace, in a namespace around the member, or behind an alias
        // there. C# finds each before the global namespace, so the build binds the member's attribute to the code's own.
        var source = $$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace Tooling
            {
                public sealed class LocalizedAttribute(string en, string nl) : System.Attribute
                {
                    public string En { get; } = en;
                    public string Nl { get; } = nl;
                }
            }

            {{usage}}
            """;

        // Act
        var text = Texts(Run(source));

        // Assert
        text.ShouldBeNull();
        GeneratorTestHelper.CompileWithGenerators(source).ShouldBeEmpty();
    }

    [Test]
    public void AssemblyWithMarkedMember_ShouldBeMarkedAsWritingInlineTexts()
    {
        // Arrange: a text on a member carries its wording itself, as an inline text does, so no app can add a language
        // to it. An assembly that also calls LocalizableText.Inline is marked twice, by two generated files.
        var source = $$"""
            {{Header}}

            public class Address
            {
                [Localized(en: "City", nl: "Plaats")]
                public string City { get; } = "";

                public static string Title => LocalizableText.Inline(en: "Address", nl: "Adres");
            }
            """;

        // Act
        var text = Texts(Run(source))!;

        // Assert
        text.ShouldContain("[assembly: CodeChops.Idioma.Internals.InlineTextsAttribute]");
        GeneratorTestHelper.CompileWithGenerators(source).ShouldBeEmpty();
        GeneratorTestHelper.GetWarningsWithGenerators(source).ShouldBeEmpty();
    }

    [Test]
    public void Registrar_ShouldBePublicAndRecordTheMemberTexts()
    {
        // Arrange & Act: a library with nothing but a marked member.
        var registrar = Registrar(Run($$"""
            {{Header}}

            public class Address
            {
                [Localized(en: "City", nl: "Plaats")]
                public string City { get; } = "";
            }
            """))!;

        // Assert: public, so that an app that references the library registers it before any of its code runs. A
        // member's text is asked for by its type, which an app can hold before that.
        registrar.ShouldContain("public static class LocalizationRegistrar_TestAssembly");
        registrar.ShouldContain("            LocalizedMembers_TestAssembly.Register();");
        registrar.ShouldContain("/// Records the texts of this assembly's members marked <c>[Localized]</c> too.");
    }

    [Test]
    public void AppReferencingLibraryWithMarkedMembers_ShouldCallItsRegistrar()
    {
        // Arrange: a library with nothing but a marked member.
        var library = GeneratorTestHelper.CompileLibraryWithGenerators("MemberLibrary", $$"""
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
        var result = GeneratorTestHelper.RunAsApplication(LocalizationParts.Registration(), "namespace TestApp;", library);
        var registrar = result.Results.ShouldHaveSingleItem().GeneratedSources.ShouldHaveSingleItem().SourceText.ToString();

        // Assert
        registrar.ShouldContain("RegisterReferenced(\"MemberLibrary\", static () => CodeChops.Idioma.Generated.LocalizationRegistrar_MemberLibrary.Register());");
    }

    [Test]
    [NotInParallel(nameof(LocalizationRegistry))]
    public void TextOfMemberOfALibrary_ShouldBeFoundBeforeAnyCodeOfTheLibraryRan()
    {
        // Arrange: an app that references a library with a marked member, as a form reads the label of a property of
        // a model before it creates one.
        var library = GeneratorTestHelper.EmitLibraryWithGenerators("EarlyMemberLibrary", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace EarlyMemberLibrary;

            public class Address
            {
                [Localized(en: "City", nl: "Plaats")]
                public string City { get; } = "";
            }
            """);
        var app = GeneratorTestHelper.EmitLibraryWithGenerators("EarlyMemberApp", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace EarlyMemberApp;

            public static class Anchor;
            """, MetadataReference.CreateFromImage(library));
        var context = new InMemoryLoadContext(new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["EarlyMemberApp"] = app,
            ["EarlyMemberLibrary"] = library,
        });

        // Act: the app's code first runs, which registers the library it references. Nothing of the library is used.
        var appAssembly = context.LoadFromAssemblyName(new AssemblyName("EarlyMemberApp"));
        RuntimeHelpers.RunModuleConstructor(appAssembly.ManifestModule.ModuleHandle);
        var address = context.LoadFromAssemblyName(new AssemblyName("EarlyMemberLibrary")).GetType("EarlyMemberLibrary.Address")!;

        // Assert
        using var _ = new CulturesScope("nl-NL");
        LocalizableText.GetTextByMember(address.GetProperty("City")!).ShouldBe("Plaats");
    }

    [Test]
    public void EnumWithMarkedMembersAndASet_ShouldGetOneGetText()
    {
        // Arrange: a set of the enum gives it GetText, and so would its marked members, which made every call
        // ambiguous (CS0121).
        const string source = $$"""
            {{Header}}

            public enum ParcelStatus
            {
                [Localized(en: "Picked", nl: "Gepickt")]
                Picked,

                [Localized(en: "Delivered", nl: "Bezorgd")]
                Delivered,
            }

            public sealed class ParcelStatusLocalizationEn : ILocalizationSet<ParcelStatus>, IParcelStatusLocalization
            {
                public string Picked => "Picked up";
                public string Delivered => "Delivered";
            }

            public sealed class ParcelStatusLocalizationNl : ILocalizationSet<ParcelStatus>, IParcelStatusLocalization
            {
                public string Picked => "Opgehaald";
                public string Delivered => "Bezorgd";
            }

            public static class Labels
            {
                public static string Of(ParcelStatus status) => status.GetText();
            }
            """;

        // Act
        var errors = GeneratorTestHelper.CompileWithGenerators(source);
        var texts = Texts(Run(source));

        // Assert: the set's, and the member texts are still recorded for GetTextByMember.
        errors.ShouldBeEmpty();
        texts.ShouldNotBeNull().ShouldNotContain("ParcelStatusLocalizedExtensions");
        texts.ShouldContain("\"Gepickt\"");
    }

    [Test]
    public void MemberInKeywordNamespace_ShouldBeFoundByItsNameWithoutTheAt()
    {
        // Arrange & Act
        var texts = Texts(Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace @class.@event;

            public class Model
            {
                [Localized(en: "Base", nl: "Basis")]
                public string @base { get; } = "";
            }
            """));

        // Assert: the type is named as code names it, and the member by its name, without the @.
        texts.ShouldNotBeNull().ShouldContain("if (type == typeof(@class.@event.Model))");
        texts.ShouldContain("\"base\" =>");
        texts.ShouldNotContain("\"@base\"");
    }

    [Test]
    public void EditInFileOfMarkedMember_ShouldRewriteNoSource()
    {
        // Arrange & Act: an edit in the file of a marked member, which gives its attribute a new location.
        var (run, _) = GeneratorTestHelper.RunAcrossEditInSameFile(LocalizationParts.Registration(), $$"""
            {{Header}}

            public class Address
            {
                [Localized(en: "City", nl: "Plaats")]
                public string City { get; } = "";
            }
            """);

        // Assert: neither the texts nor the registrar, because what is read of a member holds no location.
        var reasons = GeneratorTestHelper.GetSourceOutputReasons(run);
        reasons.Length.ShouldBe(2);
        reasons.ShouldAllBe(static reason => reason == IncrementalStepRunReason.Cached);
    }
}
