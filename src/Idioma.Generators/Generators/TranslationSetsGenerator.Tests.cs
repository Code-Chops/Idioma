using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="TranslationSetsGenerator"/>: a partial <see cref="ITranslations"/> class named after the default
/// language yields its contract, the partial declaration implementing it, and the static class holding the texts, with
/// crefs to every translation; every language of <c>[assembly: SupportedLanguages]</c> must have a set, in its own
/// assembly and in the libraries it references.
/// </summary>
public sealed class TranslationSetsGeneratorTests
{
    #region Fixture
    private const string Header = """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace TestApp;
        """;

    private const string DutchSet = """
        public sealed class ShopTranslationsNL : IShopTranslations
        {
            public string Checkout => "Afrekenen";
            public string Total => "Totaal";
        }
        """;

    private const string ShopSets = $$"""
        {{Header}}

        public sealed partial class ShopTranslationsEN : ITranslations
        {
            /// <summary>
            /// The caption of the checkout button.
            /// </summary>
            public string Checkout => "Checkout";
            public string Total => "Total";
        }

        {{DutchSet}}
        """;

    /// <summary>
    /// A library in English and Dutch, with a public contract an app could add a language to.
    /// </summary>
    private static MetadataReference GridLibrary { get; } = GeneratorTestHelper.CompileLibraryWithGenerators("GridLibrary", """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace GridLibrary;

        public partial class GridTranslationsEN : ITranslations
        {
            public virtual string First => "First";
        }

        public sealed class GridTranslationsNL : IGridTranslations
        {
            public string First => "Eerste";
        }
        """);

    /// <summary>
    /// A library in English and Dutch with only inline texts, so nothing an app could add a language to.
    /// </summary>
    private static MetadataReference InlineLibrary { get; } = GeneratorTestHelper.CompileLibraryWithGenerators("InlineLibrary", """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace InlineLibrary;

        public static class InlineTexts
        {
            public static LocalizableText Orders { get; } = LocalizableText.Translated(en: "Orders", nl: "Bestellingen");
        }
        """);

    /// <summary>
    /// Two default-language sets of one name in two namespaces, so two contracts named <c>IShopTranslations</c>, and
    /// neither has a Dutch set yet.
    /// </summary>
    private const string TwoShops = """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace Shop.Checkout
        {
            public partial class ShopTranslationsEN : ITranslations
            {
                public string Pay => "Pay";
            }
        }

        namespace Shop.Admin
        {
            public partial class ShopTranslationsEN : ITranslations
            {
                public string Delete => "Delete";
            }
        }
        """;

    private static GeneratorDriverRunResult Run(string source, params MetadataReference[] references)
    {
        return GeneratorTestHelper.Run(new TranslationSetsGenerator(), source, references);
    }

    private static string RecordedSets(GeneratorDriverRunResult result)
    {
        var run = result.Results.ShouldHaveSingleItem();
        run.Exception.ShouldBeNull();

        return run.GeneratedSources.Single(source => source.HintName is "TranslationSets.g.cs").SourceText.ToString();
    }

    private static string ContractText(GeneratorDriverRunResult result)
    {
        var run = result.Results.ShouldHaveSingleItem();
        run.Exception.ShouldBeNull();

        return run.GeneratedSources.Single(source => source.HintName.EndsWith(".Translations.g.cs", StringComparison.Ordinal)).SourceText.ToString();
    }

    private static string[] DiagnosticIds(GeneratorDriverRunResult result)
    {
        var run = result.Results.ShouldHaveSingleItem();
        run.Exception.ShouldBeNull();

        return run.Diagnostics.Select(diagnostic => diagnostic.Id).ToArray();
    }

    private static Diagnostic SingleDiagnostic(GeneratorDriverRunResult result, string id)
    {
        var run = result.Results.ShouldHaveSingleItem();
        run.Exception.ShouldBeNull();

        var diagnostic = run.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(id);

        return diagnostic;
    }
    #endregion

    [Test]
    public Task DefaultSet_ShouldEmitContractPartialDeclarationAndTexts()
    {
        // Arrange & Act
        var result = Run(ShopSets);

        // Assert
        var run = result.Results.ShouldHaveSingleItem();
        run.Diagnostics.ShouldBeEmpty();
        run.GeneratedSources.Select(source => source.HintName).ShouldBe(["TestApp.ShopTranslationsEN.Translations.g.cs", "TranslationSets.g.cs"]);

        return Verify(ContractText(result));
    }

    [Test]
    public void GeneratedSource_ShouldNotMentionFormosa()
    {
        // Arrange & Act
        var text = ContractText(Run(ShopSets));

        // Assert: neither a name nor a license marker; the package stands on its own.
        text.ShouldNotContain("Formosa");
        text.ShouldNotContain("Obsolete");
    }

    [Test]
    public void GeneratedMembers_ShouldInheritDocCommentOfProperty()
    {
        // Arrange & Act
        var text = ContractText(Run(ShopSets));

        // Assert: both the contract member and the text point at the property, whose doc comment is the single source.
        text.Split('\n').Count(line => line.Contains("/// <inheritdoc cref=\"ShopTranslationsEN.Checkout\"/>", StringComparison.Ordinal)).ShouldBe(2);
        text.ShouldNotContain("/// Checkout");
    }

    [Test]
    public void GeneratedMembers_WithoutDocComment_ShouldBeDocumentedWithDefaultWording()
    {
        // Arrange & Act: Total has no doc comment, so its English wording documents both generated members.
        var text = ContractText(Run(ShopSets));

        // Assert
        text.Split('\n').Count(line => String.Equals(line.Trim(), "/// Total", StringComparison.Ordinal)).ShouldBe(2);
        text.ShouldNotContain("inheritdoc cref=\"ShopTranslationsEN.Total\"");
    }

    [Test]
    public void GeneratedMembers_ShouldLinkToEveryTranslation()
    {
        // Arrange & Act
        var text = ContractText(Run(ShopSets));

        // Assert: on both the contract member and the text, for each language in the order of the attribute.
        foreach (var (language, set) in new[] { ("en", "ShopTranslationsEN"), ("nl", "ShopTranslationsNL") })
        {
            foreach (var member in new[] { "Checkout", "Total" })
            {
                var line = $"/// <item><description><see cref=\"{set}.{member}\">{language}</see></description></item>";
                text.Split('\n').Count(candidate => candidate.Contains(line, StringComparison.Ordinal)).ShouldBe(2);
            }
        }
    }

    [Test]
    public void CrefsToEveryTranslation_ShouldResolve()
    {
        // Arrange & Act: a build that writes a documentation file checks each cref against the real members.
        var unresolved = GeneratorTestHelper.GetUnresolvedDocReferences(ShopSets);

        // Assert
        unresolved.ShouldBeEmpty();
    }

    [Test]
    public void TextsAndNamespaceNamedLikeKeywords_ShouldCompileWithResolvedCrefs()
    {
        // Arrange: texts declared as @event and @class, one documented so the generated members inherit its comment, in
        // a namespace with a keyword part.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp.@event;

            public partial class AgendaTranslationsEN : ITranslations
            {
                /// <summary>
                /// The title of an event.
                /// </summary>
                public virtual string @event => "Event";

                public virtual string @class => "Class";
            }

            public class AgendaTranslationsNL : IAgendaTranslations
            {
                public virtual string @event => "Evenement";
                public virtual string @class => "Klas";
            }
            """;

        // Act
        var errors = GeneratorTestHelper.CompileWithGenerators(source);
        var unresolved = GeneratorTestHelper.GetUnresolvedDocReferences(source);

        // Assert
        errors.ShouldBeEmpty();
        unresolved.ShouldBeEmpty();
    }

    [Test]
    public void UnresolvedCref_ShouldBeNoticed()
    {
        // Arrange & Act: what makes the test above worth anything.
        var unresolved = GeneratorTestHelper.GetUnresolvedDocReferences("""
            /// <summary>
            /// <see cref="Missing"/>
            /// </summary>
            public sealed class Documented;
            """);

        // Assert
        unresolved.ShouldHaveSingleItem().Id.ShouldBe("CS1574");
    }

    [Test]
    public void SetImplementingTextExplicitly_ShouldBeLinkedAsSet()
    {
        // Arrange: an explicit implementation has no name a cref could use.
        var source = $$"""
            {{Header}}

            public sealed partial class ShopTranslationsEN : ITranslations
            {
                public string Checkout => "Checkout";
            }

            public sealed class ShopTranslationsNL : IShopTranslations
            {
                string IShopTranslations.Checkout => "Afrekenen";
            }
            """;

        // Act
        var text = ContractText(Run(source));
        var unresolved = GeneratorTestHelper.GetUnresolvedDocReferences(source);

        // Assert
        text.ShouldContain("<see cref=\"ShopTranslationsNL\">nl</see>");
        unresolved.ShouldBeEmpty();
    }

    [Test]
    public void SetInheritingText_ShouldBeLinkedAtItsMember()
    {
        // Arrange & Act: the text is declared by a base class the set inherits it from.
        var text = ContractText(Run($$"""
            {{Header}}

            public sealed partial class ShopTranslationsEN : ITranslations
            {
                public string Checkout => "Checkout";
            }

            public abstract class ShopWordingBase
            {
                public string Checkout => "Afrekenen";
            }

            public sealed class ShopTranslationsNL : ShopWordingBase, IShopTranslations;
            """));

        // Assert
        text.ShouldContain("<see cref=\"ShopTranslationsNL.Checkout\">nl</see>");
    }

    [Test]
    public void DefaultWording_ShouldBeReadFromGettersAndConstantsAndEscapedForXml()
    {
        // Arrange: a getter returning a literal, a const, an initializer, a plain-comment (not doc) property, and markup
        // characters.
        var source = $$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public sealed partial class ShopTranslationsEN : ITranslations
            {
                private const string Wording = "Pay now";

                public string Checkout { get { return "Go to <checkout> & pay"; } }
                public string Pay => Wording;
                public string Total { get; } = "Total due";
                // A plain comment is no doc comment.
                public string Cancel => "Cancel";
                public string Dynamic => System.DateTime.Now.ToString();
            }
            """;

        // Act
        var text = ContractText(Run(source));

        // Assert
        text.ShouldContain("/// Go to &lt;checkout&gt; &amp; pay");
        text.ShouldContain("/// Pay now");
        text.ShouldContain("/// Total due");
        text.ShouldContain("/// Cancel");
        text.ShouldContain("/// The <c>Dynamic</c> text of <see cref=\"ShopTranslationsEN\"/>.");
        text.ShouldNotContain("inheritdoc");
    }

    [Test]
    public void Contract_ShouldRecordPlaceholdersOfEveryConstantDefaultWordingInValidFormat()
    {
        // Arrange: placeholders out of order and repeated, none, in an initializer, escaped braces, a lone brace, and
        // wording that is no constant.
        var source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public sealed partial class ShopTranslationsEN : ITranslations
            {
                public string Summary => "{1} of {0} ({1,5:N0})";
                public string Checkout => "Checkout";
                public string Greeting { get; } = "Hello, {0}!";
                public string Braces => "{{0}} stays";
                public string Lone => "Use { here";
                public string Dynamic => System.DateTime.Now.ToString();
            }
            """;

        // Act
        var text = ContractText(Run(source));

        // Assert: the contract records each valid constant wording's placeholders right above its member, and nothing
        // for the last two, which nothing can be held to.
        text.ShouldContain("    [CodeChops.Idioma.Internals.FormatPlaceholders(0, 1)]\r\n    string Summary { get; }".ReplaceLineEndings());
        text.ShouldContain("    [CodeChops.Idioma.Internals.FormatPlaceholders()]\r\n    string Checkout { get; }".ReplaceLineEndings());
        text.ShouldContain("    [CodeChops.Idioma.Internals.FormatPlaceholders(0)]\r\n    string Greeting { get; }".ReplaceLineEndings());
        text.ShouldContain("    [CodeChops.Idioma.Internals.FormatPlaceholders()]\r\n    string Braces { get; }".ReplaceLineEndings());
        text.ShouldNotContain("]\r\n    string Lone { get; }".ReplaceLineEndings());
        text.ShouldNotContain("]\r\n    string Dynamic { get; }".ReplaceLineEndings());
        GeneratorTestHelper.CompileWithGenerators(source).ShouldBeEmpty();
    }

    [Test]
    [Arguments("\\r\\n")]
    [Arguments("\\r")]
    [Arguments("\\n")]
    [Arguments("\\u0085")]
    [Arguments("\\u2028")]
    [Arguments("\\u2029")]
    public void DefaultWordingOverSeveralLines_ShouldStayInsideDocComment(string lineBreak)
    {
        // Arrange: every line break C# knows ends a /// line, so the rest of the wording would become code.
        var source = $$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public sealed partial class ShopTranslationsEN : ITranslations
            {
                public string Address => "Street{{lineBreak}}City";
            }
            """;

        // Act
        var errors = GeneratorTestHelper.CompileWithGenerators(source);
        var text = ContractText(Run(source));

        // Assert
        errors.ShouldBeEmpty();
        text.ShouldContain("    /// Street" + Environment.NewLine + "    /// City");
    }

    [Test]
    public void BaseName_ShouldDropLanguageSuffix()
    {
        // Arrange & Act & Assert: a two-letter language takes an upper-case suffix, and a longer one any letter case
        // that starts with a capital, which marks where the suffix starts: the "es" of Messages is no part of EsAR.
        TranslationsDiscovery.DeriveBaseName("ShopTranslationsEN", "en").ShouldBe("ShopTranslations");
        TranslationsDiscovery.DeriveBaseName("ShopTextsEN", "en").ShouldBe("ShopTexts");
        TranslationsDiscovery.DeriveBaseName("ShopTranslationsZhHans", "zh-Hans").ShouldBe("ShopTranslations");
        TranslationsDiscovery.DeriveBaseName("ShopTranslationsFIL", "fil").ShouldBe("ShopTranslations");
        TranslationsDiscovery.DeriveBaseName("MessagesEsAR", "es-AR").ShouldBe("Messages");
        TranslationsDiscovery.DeriveBaseName("MessagesAR", "es-AR").ShouldBeNull();
        TranslationsDiscovery.DeriveBaseName("ShopTranslationsfil", "fil").ShouldBeNull();
        TranslationsDiscovery.DeriveBaseName("ShopEn", "en").ShouldBeNull();
        TranslationsDiscovery.DeriveBaseName("ShopTranslations", "en").ShouldBeNull();
        TranslationsDiscovery.DeriveBaseName("EN", "en").ShouldBeNull();
    }

    [Test]
    [Arguments("ShopTextsEN", "IShopTexts", "ShopTexts", "ShopTexts.")]
    [Arguments("TranslationsEN", "ITranslations", "Translations", "Translations.")]
    public void Names_ShouldDeriveFromClassName(string className, string contract, string texts, string keyPrefix)
    {
        // Arrange & Act: the key stem drops a trailing "Translations", keeping it when nothing else is left.
        var text = ContractText(Run($$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public sealed partial class {{className}} : CodeChops.Idioma.ITranslations
            {
                public string Checkout => "Checkout";
            }
            """));

        // Assert
        text.ShouldContain($"public interface {contract} ");
        text.ShouldContain($"public static partial class {texts}");
        text.ShouldContain($"public const string ResourceKeyPrefix = \"{keyPrefix}\";");
    }

    [Test]
    public void DefaultLanguageWithLongCode_ShouldWork()
    {
        // Arrange & Act
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("zh-Hans", "en")]

            namespace TestApp;

            public sealed partial class ShopTranslationsZhHans : ITranslations
            {
                string ITranslations.LanguageCode => "zh-Hans";
                public string Checkout => "结账";
            }

            public sealed class ShopTranslationsEN : IShopTranslations
            {
                public string Checkout => "Checkout";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
        ContractText(result).ShouldContain("<see cref=\"ShopTranslationsZhHans.Checkout\">zh-Hans</see>");
    }

    [Test]
    public void OnlyPublicInstanceStringPropertiesAreTexts()
    {
        // Arrange: LanguageCode is the ITranslations member, a virtual property is a text (so a set can derive and
        // override), an inherited property belongs to the base (it is not redeclared), a property without a public
        // getter could not implement the contract member, and the rest are not texts by shape.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public class ShopWordingBase
            {
                public string Inherited => "Inherited";
            }

            public partial class ShopTranslationsEN : ShopWordingBase, ITranslations
            {
                public string LanguageCode => "en";
                public string Checkout => "Checkout";
                public virtual string Total => "Total";
                internal string Hidden => "Hidden";
                public string Secret { private get; set; } = "Secret";
                public static string Shared => "Shared";
                public int Count => 1;
                public string this[int index] => "";
                public string Field = "Field";
                public string Method() => "Method";
            }
            """;

        // Act
        var text = ContractText(Run(source));

        // Assert
        text.ShouldContain("    string Checkout { get; }");
        text.ShouldContain("    string Total { get; }");
        foreach (var name in new[] { "LanguageCode", "Inherited", "Hidden", "Secret", "Shared", "Count", "Field", "Method" })
            text.ShouldNotContain($"string {name} {{ get; }}", customMessage: name);
    }

    [Test]
    public void InternalSet_ShouldEmitInternalContractAndTextsClass()
    {
        // Arrange: the generated types are the rest of the set's contract, so they are no more visible than the set.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            internal sealed partial class ShopTranslationsEN : ITranslations
            {
                public string Checkout => "Checkout";
            }
            """;

        // Act
        var result = Run(source);

        // Assert: no other assembly could implement it, so it is not recorded for them.
        var text = ContractText(result);
        text.ShouldContain("internal interface IShopTranslations : CodeChops.Idioma.ITranslations");
        text.ShouldContain("internal static partial class ShopTranslations");
        text.ShouldNotContain("public interface");
        text.ShouldNotContain("public static partial class");
        result.Results.ShouldHaveSingleItem().GeneratedSources.ShouldNotContain(source => source.HintName == "TranslationSets.g.cs");
    }

    [Test]
    public void RecordSet_ShouldEmitPartialRecordDeclaration()
    {
        // Arrange & Act
        var text = ContractText(Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public sealed partial record ShopTranslationsEN : ITranslations
            {
                public string Checkout => "Checkout";
            }
            """));

        // Assert
        text.ShouldContain("partial record ShopTranslationsEN : IShopTranslations;");
    }

    [Test]
    public void SetInGlobalNamespace_ShouldEmitNoNamespaceDeclaration()
    {
        // Arrange & Act
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            public sealed partial class ShopTranslationsEN : ITranslations
            {
                public string Checkout => "Checkout";
            }
            """);

        // Assert
        var generated = result.Results.ShouldHaveSingleItem().GeneratedSources;
        var text = generated.Single(source => source.HintName is "ShopTranslationsEN.Translations.g.cs").SourceText.ToString();
        text.ShouldNotContain("namespace ");
        text.ShouldContain(" DefaultSet<ShopTranslationsEN>.Instance);");
        text.ShouldContain("internal static TSet Instance { get; } = new();");
        generated.Single(source => source.HintName is "TranslationSets.g.cs").SourceText.ToString().ShouldContain("TranslationSetAttribute(\"IShopTranslations\", \"en\")");
    }

    [Test]
    public void SetWithoutTexts_ShouldEmitEmptyContractAndTextsClass()
    {
        // Arrange & Act
        var text = ContractText(Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public sealed partial class ShopTranslationsEN : ITranslations;
            """));

        // Assert
        text.ShouldContain("public interface IShopTranslations");
        text.ShouldContain("public static partial class ShopTranslations");
        text.ShouldNotContain("CreateResourceText<");
    }

    [Test]
    public Task SetsOfPublicContract_ShouldBeRecordedForOtherAssemblies()
    {
        // Arrange & Act
        var text = RecordedSets(Run(ShopSets));

        // Assert
        return Verify(text);
    }

    [Test]
    public void GeneratedCode_ShouldCompile()
    {
        // Arrange & Act
        var errors = GeneratorTestHelper.CompileWithGenerators(ShopSets);

        // Assert
        errors.ShouldBeEmpty();
    }

    [Test]
    public void TextsNamedLikeTheGeneratedHelpers_ShouldCompile()
    {
        // Arrange & Act: the set behind the defaults is named with a type argument, which no text stands before, and it
        // is file-local, so a type of that name elsewhere, with or without a type parameter, cannot take its place.
        var errors = GeneratorTestHelper.CompileWithGenerators("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public sealed partial class OptionTranslationsEN : ITranslations
            {
                public string Default => "Default";
                public string DefaultSet => "Default set";
                public string Instance => "Instance";
                public string KeyPrefix => "Prefix";
            }

            public sealed class OptionTranslationsNL : IOptionTranslations
            {
                public string Default => "Standaard";
                public string DefaultSet => "Standaardset";
                public string Instance => "Exemplaar";
                public string KeyPrefix => "Voorvoegsel";
            }

            public static class DefaultSet;

            public static class DefaultSet<T>;
            """);

        // Assert
        errors.ShouldBeEmpty();
    }

    [Test]
    [Arguments("namespace Checkout;", "Checkout")]
    [Arguments("namespace Orders.Texts;", "Orders")]
    [Arguments("", "DefaultSet")]
    [Arguments("namespace TestApp;", "CodeChops")]
    [Arguments("namespace TestApp;", "CreateResourceText")]
    public void TextNamedLikeNameGeneratedClassWrites_ShouldCompileWithResolvedCrefs(string @namespace, string text)
    {
        // Arrange: inside the generated class a text stands before every namespace and type of its name. So the class
        // names the set behind the defaults without its namespace and with a type argument, which neither a text named
        // like the namespace nor one named DefaultSet stands before, and it calls what creates a text by its name alone,
        // without the package's namespace, which a text named CodeChops would stand before.
        var source = $$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            {{@namespace}}

            public sealed partial class ShopTranslationsEN : ITranslations
            {
                public string {{text}} => "Text";
                public string Other => "Other";
            }

            public sealed class ShopTranslationsNL : IShopTranslations
            {
                public string {{text}} => "Tekst";
                public string Other => "Ander";
            }
            """;

        // Act
        var errors = GeneratorTestHelper.CompileWithGenerators(source);
        var unresolved = GeneratorTestHelper.GetUnresolvedDocReferences(source);

        // Assert
        errors.ShouldBeEmpty();
        unresolved.ShouldBeEmpty();
    }

    [Test]
    [Arguments("Idioma.Demo", "")]
    [Arguments("Generated.Texts", "")]
    [Arguments("Internals.Texts", "")]
    [Arguments("Shop", "namespace Shop { public sealed class Shop; }")]
    public void NamespaceOrClassStandingBeforeFullNameOfSet_ShouldCompileWithResolvedCrefs(string @namespace, string other)
    {
        // Arrange: where the full name of a set is looked for from the wrong place. Inside the registrar's namespace,
        // Idioma.Demo.ShopTranslationsEN is looked for in CodeChops.Idioma, so the registrar names the sets outside it,
        // at the top level of its file. Inside the namespace Shop, a class Shop stands before the namespace, so the file
        // of the contract names the sets of its own namespace without it. A set and an inline text, so that all three
        // generators write their code.
        var source = $$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            {{other}}

            namespace {{@namespace}}
            {
                public partial class ShopTranslationsEN : ITranslations
                {
                    public virtual string Cart => "Cart";
                }

                public class ShopTranslationsNL : IShopTranslations
                {
                    public virtual string Cart => "Winkelwagen";
                }

                public static class InlineTexts
                {
                    public static LocalizableText Orders { get; } = LocalizableText.Translated(en: "Orders", nl: "Bestellingen");
                }
            }
            """;

        // Act
        var errors = GeneratorTestHelper.CompileWithGenerators(source);
        var unresolved = GeneratorTestHelper.GetUnresolvedDocReferences(source);

        // Assert
        errors.ShouldBeEmpty();
        unresolved.ShouldBeEmpty();
    }

    [Test]
    [Arguments("ResourceKeyPrefix")]
    [Arguments("OptionTranslations")]
    public void TextNamedLikeMemberOfGeneratedClass_ShouldReportLOC014AndCompileOtherwise(string name)
    {
        // Arrange & Act: the contract keeps the text, so the sets still compile; only the generated text is left out.
        var errors = GeneratorTestHelper.CompileWithGenerators($$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public sealed partial class OptionTranslationsEN : ITranslations
            {
                public string {{name}} => "Text";
                public string Other => "Other";
            }

            public sealed class OptionTranslationsNL : IOptionTranslations
            {
                public string {{name}} => "Tekst";
                public string Other => "Ander";
            }
            """);

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Id.ShouldBe(TranslationSetsGenerator.ReservedTextNameDiagnosticId);
        error.Id.ShouldBe("LOC014");
        error.GetMessage().ShouldContain($"'OptionTranslationsEN' has a text named '{name}'");
        error.GetMessage().ShouldContain($"'OptionTranslations.{name}' is not generated");
    }

    [Test]
    public void SetWithoutDeclaredLanguages_ShouldReportLOC001AndEmitNoContract()
    {
        // Arrange & Act
        var result = Run("""
            using CodeChops.Idioma;

            namespace TestApp;

            public sealed partial class ShopTranslationsEN : ITranslations
            {
                public string Checkout => "Checkout";
            }
            """);

        // Assert
        var diagnostic = SingleDiagnostic(result, TranslationSetsGenerator.NoLanguagesDeclaredDiagnosticId);
        diagnostic.Id.ShouldBe("LOC001");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("'ShopTranslationsEN'");
        diagnostic.GetMessage().ShouldContain("[assembly: SupportedLanguages(\"en\", \"nl\")]");
        diagnostic.Location.GetLineSpan().StartLinePosition.Line.ShouldBe(4);
        result.Results.ShouldHaveSingleItem().GeneratedSources.ShouldBeEmpty();
    }

    [Test]
    public void AppWithoutDeclaredLanguages_ShouldBeFreeToAddToLibrary()
    {
        // Arrange & Act: rewording a text, and adding a language, in an app that declares nothing.
        var result = Run("""
            namespace TestApp;

            public sealed class MyGridTranslationsEN : GridLibrary.GridTranslationsEN
            {
                public override string First => "Start";
            }

            public sealed class GridTranslationsDE : GridLibrary.IGridTranslations
            {
                public string First => "Erste";
            }
            """, GridLibrary);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    [Arguments("public sealed class ShopTranslationsEN : ITranslations { public string Checkout => \"Checkout\"; }", "it is not partial")]
    [Arguments("public sealed partial class ShopTranslations : ITranslations { public string LanguageCode => \"en\"; public string Checkout => \"Checkout\"; }", "its name does not end in EN")]
    [Arguments("public abstract partial class ShopTranslationsEN : ITranslations { public string Checkout => \"Checkout\"; }", "it is abstract")]
    [Arguments("public sealed partial class ShopTranslationsEN<T> : ITranslations { public string Checkout => \"Checkout\"; }", "it is generic")]
    [Arguments("file sealed partial class ShopTranslationsEN : ITranslations { public string Checkout => \"Checkout\"; }", "it is file-local")]
    [Arguments("public static partial class Container { public sealed partial class ShopTranslationsEN : ITranslations { public string Checkout => \"Checkout\"; } }", "it is nested in 'Container'")]
    [Arguments("public sealed partial class ShopTranslationsEN(string shopName) : ITranslations { public string Checkout => shopName; }", "it has no parameterless constructor")]
    [Arguments("public sealed partial class ShopTranslationsEN : ITranslations { private ShopTranslationsEN() { } public string Checkout => \"Checkout\"; }", "it has no parameterless constructor")]
    public void DefaultSetGeneratorCannotExtend_ShouldReportLOC002AndEmitNothing(string declaration, string expectedReason)
    {
        // Arrange
        var source = $$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            {{declaration}}
            """;

        // Act
        var result = Run(source);

        // Assert
        var diagnostic = SingleDiagnostic(result, TranslationSetsGenerator.CannotExtendDiagnosticId);
        diagnostic.Id.ShouldBe("LOC002");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("'ShopTranslations");
        diagnostic.GetMessage().ShouldContain(expectedReason);
        result.Results.ShouldHaveSingleItem().GeneratedSources.ShouldBeEmpty();
    }

    [Test]
    public void DefaultSetWithoutParameterlessConstructor_ShouldReportLOC002AsItsOnlyError()
    {
        // Arrange & Act: the generated texts create the set for their default wording, as the registrar creates it to
        // register. With all three generators at work, as in a build.
        var errors = GeneratorTestHelper.CompileWithGenerators("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public sealed partial class ShopTranslationsEN(string shopName) : ITranslations
            {
                public string Checkout => $"Pay at {shopName}";
            }
            """);

        // Assert: one error, at the set, and none in code the generators wrote (CS7036 on a 'new()' nobody typed).
        var error = errors.ShouldHaveSingleItem();
        error.Id.ShouldBe("LOC002");
        error.GetMessage().ShouldContain("it has no parameterless constructor");
        error.GetMessage().ShouldContain("with a parameterless constructor");
    }

    [Test]
    [Arguments("public abstract class ShopTranslationsEN : ITranslations { public string Checkout => \"Checkout\"; }")]
    [Arguments("file sealed class ShopTranslationsEN : ITranslations { public string Checkout => \"Checkout\"; }")]
    [Arguments("public static class Container { public sealed class ShopTranslationsEN : ITranslations { public string Checkout => \"Checkout\"; } }")]
    [Arguments("public static partial class Container { [ExcludeFromTranslationDiscovery] public sealed partial class ShopTranslationsEN : ITranslations { public string Checkout => \"Checkout\"; } }")]
    [Arguments("public static partial class Container { public sealed partial class ShopTranslationsNL : ITranslations { public string Checkout => \"Afrekenen\"; } }")]
    [Arguments("[ExcludeFromTranslationDiscovery] public sealed partial class ShopTranslationsEN : ITranslations { public string Checkout => \"Checkout\"; }")]
    public void SetTakingNoPart_ShouldReportNothingAndEmitNothing(string declaration)
    {
        // Arrange & Act: registered by hand, if at all, so it has no contract to generate. Nothing marks it as one: it
        // is not partial (a test fixture), excluded, or in another language than the default.
        var result = Run($$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            {{declaration}}
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
        result.Results.ShouldHaveSingleItem().GeneratedSources.ShouldBeEmpty();
    }

    [Test]
    public void ExcludedSet_ShouldCountAsSetOfItsLanguage()
    {
        // Arrange & Act: the fix LOC003 names for a set that needs a constructor argument; registering it is up to the
        // app.
        var result = Run($$"""
            {{Header}}

            public sealed partial class ShopTranslationsEN : ITranslations
            {
                public string Checkout => "Checkout";
            }

            [ExcludeFromTranslationDiscovery]
            public sealed class ShopTranslationsNL(string checkout) : IShopTranslations
            {
                public string Checkout => checkout;
            }
            """);

        // Assert: no LOC004, the language is recorded for the assemblies referencing this one, and the generated text
        // links to its wording.
        DiagnosticIds(result).ShouldBeEmpty();
        RecordedSets(result).ShouldContain("TranslationSetAttribute(\"TestApp.IShopTranslations\", \"nl\")");
        ContractText(result).ShouldContain("<see cref=\"ShopTranslationsNL.Checkout\">nl</see>");
    }

    [Test]
    public void ExcludedSetNamedAfterUnlistedLanguage_ShouldNotCountForListedLanguage()
    {
        // Arrange & Act: this set serves be, not the listed nl-BE, so the language stays missing, and LOC005 says what
        // the set serves instead, excluded or not.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl-BE")]

            namespace TestApp;

            public sealed partial class ShopTranslationsEN : ITranslations
            {
                public string Checkout => "Checkout";
            }

            [ExcludeFromTranslationDiscovery]
            public sealed class ShopTranslationsBE : IShopTranslations
            {
                public string Checkout => "Afrekenen";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBe(["LOC004", "LOC005"]);
    }

    [Test]
    public void ExcludedSetNamedAfterListedLongCode_ShouldCountForThatLanguage()
    {
        // Arrange & Act: registered by hand, it serves nl-BE, as the generated registrar records that code for it too.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl-BE")]

            namespace TestApp;

            public sealed partial class ShopTranslationsEN : ITranslations
            {
                public string Checkout => "Checkout";
            }

            [ExcludeFromTranslationDiscovery]
            public sealed class ShopTranslationsNlBE : IShopTranslations
            {
                public string Checkout => "Afrekenen";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
        RecordedSets(result).ShouldContain("TranslationSetAttribute(\"TestApp.IShopTranslations\", \"nl-BE\")");
    }

    [Test]
    [Arguments("public class GiftTranslationsNL : IShopTranslations { public virtual string Checkout => \"Afrekenen\"; public virtual string Total => \"Totaal\"; } [ExcludeFromTranslationDiscovery] public sealed class GiftTranslationsEN : GiftTranslationsNL;")]
    [Arguments("[ExcludeFromTranslationDiscovery] public sealed class CartTranslationsNL : ITranslations { public string Checkout => \"Afrekenen\"; }")]
    public void ExcludedSet_ShouldReportNothingAboutItself(string declaration)
    {
        // Arrange & Act: a set deriving from a set of another language (LOC006), and one listing ITranslations in
        // another language than the default (LOC007), were either not excluded. A language not listed is still LOC005.
        var result = Run($$"""
            {{ShopSets}}

            {{declaration}}
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void LanguageWithoutSet_ShouldReportLOC004AndStillEmit()
    {
        // Arrange & Act
        var result = Run($$"""
            {{Header}}

            public sealed partial class ShopTranslationsEN : ITranslations
            {
                public string Checkout => "Checkout";
            }
            """);

        // Assert: the error names the language and the fix, at the set; the contract still comes, so it stays the only
        // error.
        var diagnostic = SingleDiagnostic(result, TranslationSetsGenerator.MissingLanguageDiagnosticId);
        diagnostic.Id.ShouldBe("LOC004");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("'ShopTranslations' has no translation into 'nl'");
        diagnostic.GetMessage().ShouldContain("Add 'class ShopTranslationsNL : IShopTranslations'.");
        diagnostic.Location.GetLineSpan().StartLinePosition.Line.ShouldBe(6);
        ContractText(result).ShouldContain("public interface IShopTranslations");
    }

    [Test]
    public void LanguageWithLongCodeWithoutSet_ShouldNameSetAfterCode()
    {
        // Arrange & Act
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl-BE")]

            namespace TestApp;

            public sealed partial class ShopTranslationsEN : ITranslations
            {
                public string Checkout => "Checkout";
            }
            """);

        // Assert: the name alone gives a set nl-BE, as the assembly declares it.
        var diagnostic = SingleDiagnostic(result, TranslationSetsGenerator.MissingLanguageDiagnosticId);
        diagnostic.GetMessage().ShouldContain("Add 'class ShopTranslationsNlBE : IShopTranslations'.");
    }

    [Test]
    public void HandWrittenContractMissingLanguage_ShouldReportLOC004AtAttribute()
    {
        // Arrange & Act
        var result = Run($$"""
            {{Header}}

            public interface IGridTranslations : ITranslations
            {
                string First { get; }
            }

            public sealed class GridTranslationsEN : IGridTranslations
            {
                public string First => "First";
            }
            """);

        // Assert
        var diagnostic = SingleDiagnostic(result, TranslationSetsGenerator.MissingLanguageDiagnosticId);
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("'TestApp.IGridTranslations' has no translation into 'nl'");
        diagnostic.GetMessage().ShouldContain("Add 'class GridTranslationsNL : IGridTranslations'.");
        diagnostic.Location.GetLineSpan().StartLinePosition.Line.ShouldBe(2);
    }

    [Test]
    public void HandWrittenContractInEveryLanguage_ShouldReportNothing()
    {
        // Arrange & Act: Dutch through a set of a derived contract, which serves the base contract too.
        var result = Run($$"""
            {{Header}}

            internal interface IGridTranslations : ITranslations
            {
                string First { get; }
            }

            internal interface IPagedGridTranslations : IGridTranslations
            {
                string Next { get; }
            }

            internal sealed class GridTranslationsEN : IPagedGridTranslations
            {
                public string First => "First";
                public string Next => "Next";
            }

            internal sealed class GridTranslationsNL : IPagedGridTranslations
            {
                public string First => "Eerste";
                public string Next => "Volgende";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void HandWrittenContractWithExcludedSet_ShouldCountItAsSetOfItsLanguage()
    {
        // Arrange & Act
        var result = Run($$"""
            {{Header}}

            public interface IGridTranslations : ITranslations
            {
                string First { get; }
            }

            public sealed class GridTranslationsEN : IGridTranslations
            {
                public string First => "First";
            }

            [ExcludeFromTranslationDiscovery]
            public sealed class GridTranslationsNL(string first) : IGridTranslations
            {
                public string First => first;
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    [Arguments("public", "partial ")]
    [Arguments("public", "")]
    [Arguments("internal", "")]
    public void SetsListingITranslationsBesideHandWrittenContract_ShouldReportNothingAndEmitNoContract(string accessibility, string partial)
    {
        // Arrange & Act: listing ITranslations again adds nothing to a set of a contract written by hand. The English set
        // is no default-language set to generate a second contract from (LOC002, or a duplicate IGridTranslations once
        // partial), and the Dutch set does implement its contract (LOC007).
        var result = Run($$"""
            {{Header}}

            {{accessibility}} interface IGridTranslations : ITranslations
            {
                string Next { get; }
            }

            {{accessibility}} {{partial}}class GridTranslationsEN : IGridTranslations, ITranslations
            {
                public string Next => "Next";
            }

            {{accessibility}} sealed class GridTranslationsNL : IGridTranslations, ITranslations
            {
                public string Next => "Volgende";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
        result.Results.ShouldHaveSingleItem().GeneratedSources.ShouldNotContain(source => source.HintName.EndsWith(".Translations.g.cs", StringComparison.Ordinal));
    }

    [Test]
    public void SetsListingITranslationsBesideHandWrittenContractWithoutDeclaredLanguages_ShouldReportNothing()
    {
        // Arrange & Act: they start no contract, so they need no languages declared (LOC001).
        var result = Run("""
            using CodeChops.Idioma;

            namespace TestApp;

            public interface IGridTranslations : ITranslations
            {
                string Next { get; }
            }

            public sealed class GridTranslationsEN : IGridTranslations, ITranslations
            {
                public string Next => "Next";
            }

            public sealed class GridTranslationsNL : IGridTranslations, ITranslations
            {
                public string Next => "Volgende";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void HandWrittenContractOfSetWithUnreadableLanguage_ShouldBeSkipped()
    {
        // Arrange & Act: the build cannot tell which language the second set serves, so it does not guess.
        var result = Run($$"""
            {{Header}}

            public interface IGridTranslations : ITranslations
            {
                string First { get; }
            }

            public sealed class GridTranslationsEN : IGridTranslations
            {
                public string First => "First";
            }

            public sealed class GridTranslations : IGridTranslations
            {
                private static string Language { get; } = "nl";

                public string LanguageCode => Language;
                public string First => "Eerste";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void EveryLanguageMissing_ShouldReportEach()
    {
        // Arrange & Act
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]

            namespace TestApp;

            public sealed partial class ShopTranslationsEN : ITranslations;
            """);

        // Assert
        DiagnosticIds(result).ShouldBe(["LOC004", "LOC004"]);
    }

    [Test]
    public void ContractsOfOneNameInTwoNamespaces_ShouldEachNeedTheirOwnLanguages()
    {
        // Arrange & Act: the Dutch set implements the checkout's contract, which leaves the admin's without Dutch.
        var result = Run($$"""
            {{TwoShops}}

            namespace Shop.Checkout
            {
                public sealed class ShopTranslationsNL : IShopTranslations
                {
                    public string Pay => "Betalen";
                }
            }
            """);

        // Assert
        SingleDiagnostic(result, TranslationSetsGenerator.MissingLanguageDiagnosticId);
        var recorded = RecordedSets(result);
        recorded.ShouldContain("TranslationSetAttribute(\"Shop.Checkout.IShopTranslations\", \"nl\")");
        recorded.ShouldNotContain("TranslationSetAttribute(\"Shop.Admin.IShopTranslations\", \"nl\")");
        var admin = result.Results.Single().GeneratedSources.Single(source => source.HintName is "Shop.Admin.ShopTranslationsEN.Translations.g.cs");
        admin.SourceText.ToString().ShouldNotContain("ShopTranslationsNL");
    }

    [Test]
    [Arguments("namespace Shop.Checkout;\npublic sealed class ShopTranslationsNL : IShopTranslations;", "Shop.Checkout")]
    [Arguments("namespace Shop.Admin.Back;\npublic sealed class ShopTranslationsNL : IShopTranslations;", "Shop.Admin")]
    [Arguments("using Shop.Admin;\nnamespace Shop.Checkout;\npublic sealed class ShopTranslationsNL : IShopTranslations;", "Shop.Checkout")]
    [Arguments("using Shop.Admin;\nnamespace Other;\npublic sealed class ShopTranslationsNL : IShopTranslations;", "Shop.Admin")]
    [Arguments("namespace Other\n{\n    using Shop.Admin;\n    public sealed class ShopTranslationsNL : IShopTranslations;\n}", "Shop.Admin")]
    [Arguments("namespace Other;\npublic sealed class ShopTranslationsNL : Shop.Admin.IShopTranslations;", "Shop.Admin")]
    [Arguments("namespace Shop;\npublic sealed class ShopTranslationsNL : Admin.IShopTranslations;", "Shop.Admin")]
    [Arguments("namespace Other;\npublic sealed class ShopTranslationsNL : Shop.Admin.IShopTranslations;", "Shop.Admin")]
    public void SetImplementingGeneratedContract_ShouldCountForContractTheCompilerBinds(string dutchSet, string expectedNamespace)
    {
        // Arrange: the contract does not exist yet while the generator runs; a type of the containing namespaces wins
        // over one a using imports.
        var otherNamespace = expectedNamespace is "Shop.Admin" ? "Shop.Checkout" : "Shop.Admin";

        // Act
        var result = GeneratorTestHelper.RunOverFiles(new TranslationSetsGenerator(), TwoShops, dutchSet);

        // Assert
        var recorded = RecordedSets(result);
        recorded.ShouldContain($"TranslationSetAttribute(\"{expectedNamespace}.IShopTranslations\", \"nl\")");
        recorded.ShouldNotContain($"TranslationSetAttribute(\"{otherNamespace}.IShopTranslations\", \"nl\")");
        DiagnosticIds(result).ShouldBe(["LOC004"]);
    }

    [Test]
    public void SetImplementingGeneratedContractThroughGlobalUsing_ShouldCount()
    {
        // Arrange & Act: the global using lives in a file of its own, as an SDK's implicit usings do.
        var result = GeneratorTestHelper.RunOverFiles(
            new TranslationSetsGenerator(),
            TwoShops,
            "global using Shop.Admin;",
            "namespace Other;\npublic sealed class ShopTranslationsNL : IShopTranslations;");

        // Assert
        var recorded = RecordedSets(result);
        recorded.ShouldContain("TranslationSetAttribute(\"Shop.Admin.IShopTranslations\", \"nl\")");
        recorded.ShouldNotContain("TranslationSetAttribute(\"Shop.Checkout.IShopTranslations\", \"nl\")");
    }

    [Test]
    public void TextMissingFromSet_ShouldNotCompile()
    {
        // Arrange & Act
        var errors = GeneratorTestHelper.CompileWithGenerators($$"""
            {{Header}}

            public sealed partial class ShopTranslationsEN : ITranslations
            {
                public string Checkout => "Checkout";
                public string Total => "Total";
            }

            public sealed class ShopTranslationsNL : IShopTranslations
            {
                public string Checkout => "Afrekenen";
            }
            """);

        // Assert
        errors.ShouldHaveSingleItem().Id.ShouldBe("CS0535");
    }

    [Test]
    public void RegionalLanguage_ShouldBeServedBySetOfItsLanguage()
    {
        // Arrange & Act: at run time nl-BE resolves through nl.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl-BE")]

            namespace TestApp;

            public sealed partial class ShopTranslationsEN : ITranslations;

            public sealed class ShopTranslationsNL : IShopTranslations;
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    [Arguments("")]
    [Arguments("[ExcludeFromTranslationDiscovery]")]
    public void SetForUnlistedLanguage_ShouldReportLOC005(string attribute)
    {
        // Arrange & Act: excluded or not, as an application shows a culture outside its list in its default language,
        // so registering the set by hand would not make it show.
        var result = Run($$"""
            {{ShopSets}}

            {{attribute}}
            public sealed class ShopTranslationsDE : IShopTranslations
            {
                public string Checkout => "Zur Kasse";
                public string Total => "Summe";
            }
            """);

        // Assert
        var diagnostic = SingleDiagnostic(result, TranslationSetsGenerator.UnlistedLanguageDiagnosticId);
        diagnostic.Id.ShouldBe("LOC005");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("'ShopTranslationsDE' translates into 'de'");
        diagnostic.GetMessage().ShouldContain("Add the language to the attribute, or remove the set.");
    }

    [Test]
    public void ClassDerivingFromTypeOfOtherGenerator_ShouldReportNothing()
    {
        // Arrange & Act: OrderConfigurationBase stands for a type another generator writes (Formosa writes one per
        // model for strict configurations), which binds to nothing in the compilation the generators see. The class is
        // no set, so its name ending in 'on' is no language.
        var result = Run($$"""
            {{ShopSets}}

            public sealed class OrderConfiguration : OrderConfigurationBase;
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void SetNamedAfterListedLongCode_ShouldServeIt()
    {
        // Arrange & Act: the name gives nl-BE, as the assembly declares it, and zh-Hans in any letter case.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl-BE", "zh-Hans")]

            namespace TestApp;

            public sealed partial class ShopTranslationsEN : ITranslations
            {
                public string Checkout => "Checkout";
            }

            public sealed class ShopTranslationsNlBE : IShopTranslations
            {
                public string Checkout => "Afrekenen";
            }

            public sealed class ShopTranslationsZHHANS : IShopTranslations
            {
                public string Checkout => "Jiezhang";
            }
            """);

        // Assert: no LOC004 or LOC005, each code recorded for the assemblies referencing this one, and linked.
        DiagnosticIds(result).ShouldBeEmpty();
        RecordedSets(result).ShouldContain("TranslationSetAttribute(\"TestApp.IShopTranslations\", \"nl-BE\")");
        RecordedSets(result).ShouldContain("TranslationSetAttribute(\"TestApp.IShopTranslations\", \"zh-Hans\")");
        ContractText(result).ShouldContain("ShopTranslationsNlBE.Checkout\">nl-BE</see>");
    }

    [Test]
    public void SetNamedAfterTwoLetterCode_ShouldNotBeTakenForLongerCodeItsNameHappensToEndIn()
    {
        // Arrange & Act: "MessagesAR" ends in the letters of EsAR, the suffix of es-AR, but not at a capital: its "es"
        // belongs to "Messages", and the set is Arabic. The Spanish set serves es-AR.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "ar", "es", "es-AR")]

            namespace TestApp;

            public sealed partial class MessagesEN : ITranslations
            {
                public string Save => "Save";
            }

            public sealed class MessagesAR : IMessages
            {
                public string Save => "Hifz";
            }

            public sealed class MessagesES : IMessages
            {
                public string Save => "Guardar";
            }
            """);

        // Assert: no LOC004 asking for the Arabic set that is there, and that set recorded as Arabic.
        DiagnosticIds(result).ShouldBeEmpty();
        var recorded = RecordedSets(result);
        recorded.ShouldContain("TranslationSetAttribute(\"TestApp.IMessages\", \"ar\")");
        recorded.ShouldNotContain("TranslationSetAttribute(\"TestApp.IMessages\", \"es-AR\")");
    }

    [Test]
    public void SetNamedAfterUnlistedLongCode_ShouldReportLOC005NamingBothWays()
    {
        // Arrange & Act: nl-BE is not declared, so the last two letters count, and the set serves 'be'.
        var result = Run($$"""
            {{Header}}

            public sealed partial class ShopTranslationsEN : ITranslations
            {
                public string Checkout => "Checkout";
            }

            {{DutchSet}}

            public sealed class ShopTranslationsNlBE : IShopTranslations
            {
                public string Checkout => "Afrekenen";
            }
            """);

        // Assert: list the code, or implement it.
        var diagnostic = SingleDiagnostic(result, TranslationSetsGenerator.UnlistedLanguageDiagnosticId);
        diagnostic.GetMessage().ShouldContain("'ShopTranslationsNlBE' translates into 'be'");
        diagnostic.GetMessage().ShouldContain("If it is meant for 'nl-BE', list 'nl-BE' in the attribute");
        diagnostic.GetMessage().ShouldContain("implement 'string ITranslations.LanguageCode => \"nl-BE\";'");
    }

    [Test]
    public void SetDerivedFromLibrarySetNamedAfterLongCode_ShouldServeThatCode()
    {
        // Arrange: the library declares nl-BE, so its registrar records nl-BE for its set; the app declares only nl.
        var library = GeneratorTestHelper.CompileLibraryWithGenerators("RegionalLibrary", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl-BE")]

            namespace RegionalLibrary;

            public partial class ShopTranslationsEN : ITranslations
            {
                public virtual string Checkout => "Checkout";
            }

            public class ShopTranslationsNlBE : IShopTranslations
            {
                public virtual string Checkout => "Afrekenen";
            }
            """);

        // Act: a rewording whose own name the app's languages give nothing longer than 'be'.
        var result = Run($$"""
            {{Header}}

            public sealed class MyShopTranslationsNlBE : RegionalLibrary.ShopTranslationsNlBE
            {
                public override string Checkout => "Betalen";
            }
            """, library);

        // Assert: it inherits nl-BE, which nl serves, as at run time; no LOC005 for 'be'.
        DiagnosticIds(result).ShouldNotContain("LOC005", StringComparer.Ordinal);
        RecordedSets(result).ShouldContain("TranslationSetAttribute(\"RegionalLibrary.IShopTranslations\", \"nl-BE\")");
    }

    [Test]
    public void DefaultSetNamedAfterLongCode_ShouldGenerateContractThatCompiles()
    {
        // Arrange & Act: a regional default language, with no LanguageCode written by hand.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("nl-BE", "en")]

            namespace TestApp;

            public partial class ShopTranslationsNlBE : ITranslations
            {
                public string Checkout => "Afrekenen";
            }

            public sealed class ShopTranslationsEN : IShopTranslations
            {
                public string Checkout => "Checkout";
            }
            """;

        // Assert
        DiagnosticIds(Run(source)).ShouldBeEmpty();
        GeneratorTestHelper.CompileWithGenerators(source).ShouldBeEmpty();
    }

    [Test]
    public void SetOfLongCodeImplementingLanguageCode_ShouldReportNothing()
    {
        // Arrange & Act
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl-BE")]

            namespace TestApp;

            public sealed partial class ShopTranslationsEN : ITranslations
            {
                public string Checkout => "Checkout";
            }

            public sealed class ShopTranslationsNlBE : IShopTranslations
            {
                string ITranslations.LanguageCode => "nl-BE";
                public string Checkout => "Afrekenen";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void RegionalSetOfListedLanguage_ShouldBeAllowed()
    {
        // Arrange & Act: a regional set on top of its language keeps every other text in that language.
        var result = Run($$"""
            {{Header}}

            public partial class ShopTranslationsEN : ITranslations
            {
                public virtual string Checkout => "Checkout";
            }

            public sealed class ShopTranslationsNL : IShopTranslations
            {
                public string Checkout => "Afrekenen";
            }

            public sealed class ShopTranslationsGB : ShopTranslationsEN, ITranslations
            {
                string ITranslations.LanguageCode => "en-GB";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void RegionalSetOfNonDefaultLanguage_ShouldBeAllowed()
    {
        // Arrange & Act: the Dutch set implements the contract generated here, so the Belgian set only reaches it through
        // its base. It used to be reported as a set implementing no contract (LOC007).
        var result = Run($$"""
            {{Header}}

            public partial class ShopTranslationsEN : ITranslations
            {
                public virtual string Checkout => "Checkout";
            }

            public class ShopTranslationsNL : IShopTranslations
            {
                public virtual string Checkout => "Afrekenen";
            }

            public sealed class ShopTranslationsNlBE : ShopTranslationsNL, ITranslations
            {
                string ITranslations.LanguageCode => "nl-BE";
                public override string Checkout => "Betalen";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void ListedRegionalLanguageServedBySetDerivedFromDefaultSet_ShouldReportNothing()
    {
        // Arrange & Act: deriving from the default set implements its contract. It used to count as no set of en-GB
        // (LOC004).
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "en-GB")]

            namespace TestApp;

            public partial class ShopTranslationsEN : ITranslations
            {
                public virtual string Colour => "Color";
            }

            public sealed class ShopTranslationsGB : ShopTranslationsEN, ITranslations
            {
                string ITranslations.LanguageCode => "en-GB";
                public override string Colour => "Colour";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
        ContractText(result).ShouldContain("<see cref=\"ShopTranslationsGB.Colour\">en-GB</see>");
    }

    [Test]
    public void ListedRegionOfDefaultLanguage_ShouldBeServedByDefaultSetOfEveryContract()
    {
        // Arrange & Act: en serves en-GB as nl serves nl-BE, so only the contract with British wording needs a British
        // set. Every other contract used to report en-GB as missing (LOC004).
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "en-GB")]

            namespace TestApp;

            public partial class ShopTranslationsEN : ITranslations
            {
                public virtual string Cart => "Cart";
            }

            public class ShopTranslationsNL : IShopTranslations
            {
                public string Cart => "Winkelwagen";
            }

            public sealed class ShopTranslationsEnGB : ShopTranslationsEN
            {
                public override string Cart => "Basket";
            }

            public partial class GridTranslationsEN : ITranslations
            {
                public string Rows => "Rows";
            }

            public class GridTranslationsNL : IGridTranslations
            {
                public string Rows => "Rijen";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void SetsDerivedFromAbstractBaseOfGeneratedContract_ShouldCountForTheirLanguage()
    {
        // Arrange & Act: only the abstract base names the contract generated here. The Dutch set used to be missed,
        // so the build asked for the class it already had (LOC004).
        var result = Run($$"""
            {{Header}}

            public partial class ShopTranslationsEN : ITranslations
            {
                public string Checkout => "Checkout";
            }

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
        DiagnosticIds(result).ShouldBeEmpty();
        ContractText(result).ShouldContain("<see cref=\"ShopTranslationsNL.Checkout\">nl</see>");
    }

    [Test]
    public void SetDerivedFromOtherLanguageOfGeneratedContract_ShouldReportLOC006Only()
    {
        // Arrange & Act: the Dutch set implements the contract generated here, so the German set only reaches it through
        // its base; deriving still leaves its texts in Dutch.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]

            namespace TestApp;

            public partial class ShopTranslationsEN : ITranslations
            {
                public virtual string Checkout => "Checkout";
            }

            public class ShopTranslationsNL : IShopTranslations
            {
                public virtual string Checkout => "Afrekenen";
            }

            public sealed class ShopTranslationsDE : ShopTranslationsNL
            {
            }
            """);

        // Assert
        SingleDiagnostic(result, TranslationSetsGenerator.DerivedFromOtherLanguageDiagnosticId);
    }

    [Test]
    public void SetDerivedFromOtherLanguageWithoutDeclaredLanguages_ShouldReportLOC006()
    {
        // Arrange & Act: an app rewording a library needs no languages of its own, and this set's name makes it Tsonga.
        // It used to register under ts without a word, so its rewording never showed.
        var result = Run("""
            namespace TestApp;

            public sealed class MyGridTexts : GridLibrary.GridTranslationsEN
            {
                public override string First => "Begin";
            }
            """, GridLibrary);

        // Assert
        var diagnostic = SingleDiagnostic(result, TranslationSetsGenerator.DerivedFromOtherLanguageDiagnosticId);
        diagnostic.GetMessage().ShouldContain("'MyGridTexts' translates into 'ts' but derives from 'GridTranslationsEN'");
        diagnostic.GetMessage().ShouldContain("To reword 'GridTranslationsEN', give the set its language");
    }

    [Test]
    public void SetListingITranslationsAgainWithoutDeclaredLanguages_ShouldReportNothing()
    {
        // Arrange & Act: listing ITranslations again is how a set deriving from another implements LanguageCode, so it
        // does not start a contract that would need languages (LOC001).
        var result = Run("""
            using CodeChops.Idioma;

            namespace TestApp;

            public sealed class MyGridTexts : GridLibrary.GridTranslationsEN, ITranslations
            {
                public string LanguageCode => "en";
                public override string First => "Begin";
            }
            """, GridLibrary);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void SetWithLanguageCodeImplementingNothing_ShouldReportNoLanguageErrorOfItsOwn()
    {
        // Arrange & Act: the property implements nothing (LOC015, reported by the registration), and the language checks
        // go by what it says rather than adding LOC005 for the language its name gives (sh).
        var result = Run($$"""
            {{Header}}

            public partial class ShopTranslationsEN : ITranslations
            {
                public virtual string Checkout => "Checkout";
            }

            {{DutchSet}}

            public sealed class ShopTranslationsBritish : ShopTranslationsEN
            {
                public string LanguageCode => "en-GB";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void SetDerivedFromOtherLanguage_ShouldReportLOC006Only()
    {
        // Arrange & Act: compiles, with every text it does not override left in English.
        var result = Run($$"""
            {{Header}}

            public partial class ShopTranslationsEN : ITranslations
            {
                public virtual string Checkout => "Checkout";
                public virtual string Total => "Total";
            }

            public sealed class ShopTranslationsNL : ShopTranslationsEN
            {
                public override string Checkout => "Afrekenen";
            }
            """);

        // Assert: this already says what to do about Dutch, so no LOC004 on top.
        var diagnostic = SingleDiagnostic(result, TranslationSetsGenerator.DerivedFromOtherLanguageDiagnosticId);
        diagnostic.Id.ShouldBe("LOC006");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("'ShopTranslationsNL' translates into 'nl' but derives from 'ShopTranslationsEN'");
        diagnostic.GetMessage().ShouldContain("stays in 'en'");
    }

    [Test]
    public void SetDerivedFromOtherLanguageOfLibrary_ShouldReportLOC006()
    {
        // Arrange & Act
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]

            namespace TestApp;

            public sealed class GridTranslationsDE : GridLibrary.GridTranslationsEN;
            """, GridLibrary);

        // Assert
        SingleDiagnostic(result, TranslationSetsGenerator.DerivedFromOtherLanguageDiagnosticId);
    }

    [Test]
    public void SetRewordingItsOwnLanguage_ShouldBeAllowed()
    {
        // Arrange & Act
        var result = Run($$"""
            {{Header}}

            public partial class ShopTranslationsEN : ITranslations
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
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void SetListingITranslationsInOtherLanguage_ShouldReportLOC007Only()
    {
        // Arrange & Act: it compiles, and no text would ever read it.
        var result = Run($$"""
            {{Header}}

            public sealed partial class ShopTranslationsEN : ITranslations
            {
                public string Checkout => "Checkout";
            }

            public sealed class ShopTranslationsNL : ITranslations
            {
                public string Checkout => "Afrekenen";
            }
            """);

        // Assert
        var diagnostic = SingleDiagnostic(result, TranslationSetsGenerator.MissingContractDiagnosticId);
        diagnostic.Id.ShouldBe("LOC007");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("Implement 'IShopTranslations', generated from 'ShopTranslationsEN'");
    }

    [Test]
    public void LibraryMissingLanguage_ShouldReportLOC008AtAttribute()
    {
        // Arrange & Act
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]

            namespace TestApp;
            """, GridLibrary);

        // Assert
        var diagnostic = SingleDiagnostic(result, TranslationSetsGenerator.LibraryMissingLanguageDiagnosticId);
        diagnostic.Id.ShouldBe("LOC008");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("Nothing translates 'GridLibrary.IGridTranslations' into 'de'");
        diagnostic.GetMessage().ShouldContain("'class GridTranslationsDE : GridLibrary.IGridTranslations'");
        diagnostic.Location.GetLineSpan().StartLinePosition.Line.ShouldBe(2);
    }

    [Test]
    public void LibraryLanguageAddedByApp_ShouldReportNothing()
    {
        // Arrange & Act
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]

            namespace TestApp;

            public sealed class GridTranslationsDE : GridLibrary.IGridTranslations
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

            [ExcludeFromTranslationDiscovery]
            public sealed class GridTranslationsDE(string first) : GridLibrary.IGridTranslations
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

            public partial class ListTranslationsEN : ITranslations
            {
                public virtual string Empty => "Nothing yet";
            }

            [ExcludeFromTranslationDiscovery]
            public sealed class ListTranslationsNL(string empty) : IListTranslations
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

        // Assert: no LOC008 asking the app for the Dutch set the library has.
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void LibraryLanguageAddedByOtherLibrary_ShouldReportNothing()
    {
        // Arrange: a translations package an app only has to reference.
        var german = GeneratorTestHelper.CompileLibraryWithGenerators("GridLibrary.German", """
            namespace GridLibrary.German;

            public sealed class GridTranslationsDE : GridLibrary.IGridTranslations
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
    public void LibraryWithInlineTextsMissingLanguage_ShouldWarnWithLOC009()
    {
        // Arrange & Act: nothing the app could add, so a warning rather than an error it cannot fix.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]

            namespace TestApp;
            """, InlineLibrary);

        // Assert
        var diagnostic = SingleDiagnostic(result, TranslationSetsGenerator.LibraryCannotBeTranslatedDiagnosticId);
        diagnostic.Id.ShouldBe("LOC009");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().ShouldContain("'InlineLibrary' writes texts inline that do not exist in 'de'");
    }

    [Test]
    public void LibraryWithSetsAndInlineTextsMissingLanguage_ShouldReportBoth()
    {
        // Arrange: the app can add German to the contract (LOC008), but not to the inline text (LOC009).
        var library = GeneratorTestHelper.CompileLibraryWithGenerators("MixedLibrary", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace MixedLibrary;

            public partial class MixedTranslationsEN : ITranslations
            {
                public virtual string First => "First";
            }

            public sealed class MixedTranslationsNL : IMixedTranslations
            {
                public string First => "Eerste";
            }

            public static class MixedTexts
            {
                public static LocalizableText Orders { get; } = LocalizableText.Translated(en: "Orders", nl: "Bestellingen");
            }
            """);

        // Act
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]

            namespace TestApp;
            """, library);

        // Assert
        DiagnosticIds(result).ShouldBe(["LOC008", "LOC009"]);
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

    [Test]
    public void ReparsedSet_ShouldStillEmitAcrossEditWithoutThrowing()
    {
        // Arrange & Act: the same driver runs over a re-parsed copy of the set (the IDE edit scenario).
        var result = GeneratorTestHelper.RunAcrossEdit(new TranslationSetsGenerator(), ShopSets);

        // Assert
        ContractText(result).ShouldContain("public static partial class ShopTranslations");
    }

    [Test]
    public void UnrelatedEdit_ShouldLeaveGeneratorFullyCached()
    {
        // Arrange & Act: editing a file that declares no set must not re-run the output steps.
        var reasons = GeneratorTestHelper.GetOutputReasonsAfterUnrelatedEdit(new TranslationSetsGenerator(), ShopSets);

        // Assert
        reasons.ShouldNotBeEmpty();
        reasons.ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }

    [Test]
    public void EditInFileOfSet_ShouldReportInItsNewTreeAndRewriteNoSource()
    {
        // Arrange & Act: a contract without its Dutch set, reported at the default-language set, and an edit in that
        // file which moves nothing.
        var (run, edited) = GeneratorTestHelper.RunAcrossEditInSameFile(new TranslationSetsGenerator(), $$"""
            {{Header}}

            public sealed partial class ShopTranslationsEN : ITranslations
            {
                public string Checkout => "Checkout";
            }
            """);

        // Assert: the diagnostic lies in the tree the file has now, where a pragma of that file applies to it, and the
        // contract and the recorded sets are not written again.
        var diagnostic = run.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC004");
        diagnostic.Location.SourceTree.ShouldBeSameAs(edited);
        var reasons = GeneratorTestHelper.GetSourceOutputReasons(run);
        reasons.Length.ShouldBe(2);
        reasons.ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }
}
