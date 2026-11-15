using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="TranslationSetsGenerator"/>: a partial <see cref="ITranslationSet"/> class named after the default
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

        public sealed partial class ShopTranslationsEN : ITranslationSet
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

        public partial class GridTranslationsEN : ITranslationSet
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
            public static LocalizableText Orders { get; } = LocalizableText.Inline(en: "Orders", nl: "Bestellingen");
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
            public partial class ShopTranslationsEN : ITranslationSet
            {
                public string Pay => "Pay";
            }
        }

        namespace Shop.Admin
        {
            public partial class ShopTranslationsEN : ITranslationSet
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

        // Assert: on both the contract member and the text, for each language in the order of the attribute, with its
        // wording next to the link.
        foreach (var (language, set, member, wording) in new[]
                 {
                     ("en", "ShopTranslationsEN", "Checkout", "Checkout"), ("en", "ShopTranslationsEN", "Total", "Total"),
                     ("nl", "ShopTranslationsNL", "Checkout", "Afrekenen"), ("nl", "ShopTranslationsNL", "Total", "Totaal"),
                 })
        {
            var line = $"/// <item><description><see cref=\"{set}.{member}\">{language}</see>: {wording}</description></item>";
            text.Split('\n').Count(candidate => candidate.Contains(line, StringComparison.Ordinal)).ShouldBe(2);
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

            public partial class AgendaTranslationsEN : ITranslationSet
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

            public sealed partial class ShopTranslationsEN : ITranslationSet
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
        text.ShouldContain("<see cref=\"ShopTranslationsNL\">nl</see>: Afrekenen</description>");
        unresolved.ShouldBeEmpty();
    }

    [Test]
    public void SetInheritingText_ShouldBeLinkedAtClassDeclaringIt()
    {
        // Arrange: the text is declared by a base class the set inherits it from. A cref does not find it through the
        // set, which only inherits it (CS1574).
        var source = $$"""
            {{Header}}

            public sealed partial class ShopTranslationsEN : ITranslationSet
            {
                public string Checkout => "Checkout";
            }

            public abstract class ShopWordingBase
            {
                public string Checkout => "Afrekenen";
            }

            public sealed class ShopTranslationsNL : ShopWordingBase, IShopTranslations;
            """;

        // Act
        var text = ContractText(Run(source));
        var unresolved = GeneratorTestHelper.GetUnresolvedDocReferences(source);

        // Assert
        text.ShouldContain("<see cref=\"ShopWordingBase.Checkout\">nl</see>: Afrekenen (inherited from <see cref=\"ShopWordingBase\"/>)</description>");
        unresolved.ShouldBeEmpty();
    }

    [Test]
    public void RegionalSetLeavingTextAsItsLanguageWordsIt_ShouldBeLinkedAtSetOfItsLanguage()
    {
        // Arrange: a British set with wording of its own for one text, as the README writes it.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "en-GB")]

            namespace TestApp;

            public partial class ShopTranslationsEN : ITranslationSet
            {
                public virtual string Cart => "Cart";
                public virtual string Checkout => "Checkout";
            }

            public class ShopTranslationsNL : IShopTranslations
            {
                public virtual string Cart => "Winkelwagen";
                public virtual string Checkout => "Afrekenen";
            }

            public class ShopTranslationsEnGB : ShopTranslationsEN
            {
                public override string Cart => "Basket";
            }
            """;

        // Act
        var text = ContractText(Run(source));
        var unresolved = GeneratorTestHelper.GetUnresolvedDocReferences(source);

        // Assert: its own wording at the set, and the rest where British users get it from.
        text.ShouldContain("<see cref=\"ShopTranslationsEnGB.Cart\">en-GB</see>: Basket</description>");
        text.ShouldContain("<see cref=\"ShopTranslationsEN.Checkout\">en-GB</see>: Checkout (inherited from <see cref=\"ShopTranslationsEN\"/>)</description>");
        text.ShouldNotContain("ShopTranslationsEnGB.Checkout");
        unresolved.ShouldBeEmpty();
    }

    [Test]
    [Arguments("namespace TestApp { public abstract class WordingBase<TMarker> { public string Checkout => \"Afrekenen\"; } }", "WordingBase<int>", "WordingBase{TMarker}.Checkout")]
    [Arguments("namespace TestApp { public static class Wordings { public abstract class Dutch { public string Checkout => \"Afrekenen\"; } } }", "Wordings.Dutch", "Wordings.Dutch.Checkout")]
    [Arguments("namespace Shared.Wording { public abstract class DutchBase { public string Checkout => \"Afrekenen\"; } }", "Shared.Wording.DutchBase", "Namespace_Shared_Wording.DutchBase.Checkout")]
    [Arguments("namespace Shared { public abstract class Pair<TFirst, TSecond> { public string Checkout => \"Afrekenen\"; } }", "Shared.Pair<int, string>", "Namespace_Shared.Pair{TFirst, TSecond}.Checkout")]
    public void SetInheritingTextFromGenericOrNestedClassOrOtherNamespace_ShouldBeLinkedWithResolvedCref(string declaration, string baseType, string expectedCref)
    {
        // Arrange: a cref names a generic class with its type parameters in braces, a nested one through its container,
        // and a class of another namespace through the alias the file gives that namespace.
        var source = $$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            {{declaration}}

            namespace TestApp
            {
                public sealed partial class ShopTranslationsEN : ITranslationSet
                {
                    public string Checkout => "Checkout";
                }

                public sealed class ShopTranslationsNL : {{baseType}}, IShopTranslations;
            }
            """;

        // Act
        var text = ContractText(Run(source));
        var errors = GeneratorTestHelper.CompileWithGenerators(source);
        var unresolved = GeneratorTestHelper.GetUnresolvedDocReferences(source);

        // Assert
        text.ShouldContain($"<see cref=\"{expectedCref}\">nl</see>");
        errors.ShouldBeEmpty();
        unresolved.ShouldBeEmpty();
    }

    [Test]
    public void SetImplementingExplicitlyTextItAlsoInherits_ShouldBeLinkedAsSet()
    {
        // Arrange: the explicit implementation is the wording, not the property of the same name the set inherits.
        var source = $$"""
            {{Header}}

            public sealed partial class ShopTranslationsEN : ITranslationSet
            {
                public string Checkout => "Checkout";
            }

            public abstract class ShopWordingBase
            {
                public string Checkout => "Kassa";
            }

            public sealed class ShopTranslationsNL : ShopWordingBase, IShopTranslations
            {
                string IShopTranslations.Checkout => "Afrekenen";
            }
            """;

        // Act
        var text = ContractText(Run(source));
        var unresolved = GeneratorTestHelper.GetUnresolvedDocReferences(source);

        // Assert
        text.ShouldContain("<see cref=\"ShopTranslationsNL\">nl</see>: Afrekenen</description>");
        text.ShouldNotContain("ShopWordingBase.Checkout");
        text.ShouldNotContain("Kassa");
        unresolved.ShouldBeEmpty();
    }

    [Test]
    public void WordingNextToLink_ShouldBeOneEscapedLineOfAtMost80Characters()
    {
        // Arrange: a wording over two lines, one longer than 80 characters, and one with markup characters.
        var longWording = new string('a', 79) + "bc";
        var source = $$"""
            {{Header}}

            public sealed partial class ShopTranslationsEN : ITranslationSet
            {
                public string Address => "Street\nCity";
                public string Terms => "{{longWording}}";
                public string Total => "<b>Total</b> & more";
            }

            public sealed class ShopTranslationsNL : IShopTranslations
            {
                public string Address => "Straat";
                public string Terms => "Voorwaarden";
                public string Total => "Totaal";
            }
            """;

        // Act
        var errors = GeneratorTestHelper.CompileWithGenerators(source);
        var text = ContractText(Run(source));

        // Assert
        errors.ShouldBeEmpty();
        text.ShouldContain("<see cref=\"ShopTranslationsEN.Address\">en</see>: Street…</description>");
        text.ShouldContain($"<see cref=\"ShopTranslationsEN.Terms\">en</see>: {new string('a', 79)}b…</description>");
        text.ShouldContain("<see cref=\"ShopTranslationsEN.Total\">en</see>: &lt;b&gt;Total&lt;/b&gt; &amp; more</description>");
    }

    [Test]
    public void SetsOfOneLanguage_ShouldEachBeNamedNextToTheirWording()
    {
        // Arrange: a set rewording its own language, in another namespace, and a set excluded from discovery.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp
            {
                public partial class ShopTranslationsEN : ITranslationSet
                {
                    public virtual string Checkout => "Checkout";
                    public virtual string Total => "Total";
                }

                public sealed class ShopTranslationsNL : IShopTranslations
                {
                    public string Checkout => "Afrekenen";
                    public string Total => "Totaal";
                }

                [ExcludeFromTranslationDiscovery]
                public sealed class UnregisteredShopTranslationsNL : IShopTranslations
                {
                    public string Checkout => "Betalen";
                    public string Total => "Totaal";
                }
            }

            namespace Shared.Wording
            {
                public sealed class RewordedShopTranslationsEN : TestApp.ShopTranslationsEN
                {
                    public override string Checkout => "Pay";
                }
            }
            """;

        // Act
        var text = ContractText(Run(source));
        var errors = GeneratorTestHelper.CompileWithGenerators(source);
        var unresolved = GeneratorTestHelper.GetUnresolvedDocReferences(source);

        // Assert
        text.ShouldContain("<see cref=\"ShopTranslationsEN.Checkout\">en</see>: Checkout (<see cref=\"ShopTranslationsEN\"/>)</description>");
        text.ShouldContain("<see cref=\"Namespace_Shared_Wording.RewordedShopTranslationsEN.Checkout\">en</see>: Pay (<see cref=\"Namespace_Shared_Wording.RewordedShopTranslationsEN\"/>)</description>");
        text.ShouldContain("<see cref=\"ShopTranslationsEN.Total\">en</see>: Total (<see cref=\"Namespace_Shared_Wording.RewordedShopTranslationsEN\"/>, inherited from <see cref=\"ShopTranslationsEN\"/>)</description>");
        text.ShouldContain("<see cref=\"ShopTranslationsNL.Checkout\">nl</see>: Afrekenen (<see cref=\"ShopTranslationsNL\"/>)</description>");
        text.ShouldContain("<see cref=\"UnregisteredShopTranslationsNL.Checkout\">nl</see>: Betalen (<see cref=\"UnregisteredShopTranslationsNL\"/>, excluded from discovery)</description>");
        errors.ShouldBeEmpty();
        unresolved.ShouldBeEmpty();
    }

    [Test]
    public void WordingThatIsNoConstantOrEmpty_ShouldLeaveLinkAlone()
    {
        // Arrange
        var source = $$"""
            {{Header}}

            public sealed partial class ShopTranslationsEN : ITranslationSet
            {
                public string Checkout => "Checkout";
                public string Total => "Total";
            }

            public sealed class ShopTranslationsNL : IShopTranslations
            {
                public string Checkout => System.DateTime.Now.ToString();
                public string Total => "";
            }
            """;

        // Act
        var text = ContractText(Run(source));

        // Assert
        text.ShouldContain("<see cref=\"ShopTranslationsNL.Checkout\">nl</see></description>");
        text.ShouldContain("<see cref=\"ShopTranslationsNL.Total\">nl</see></description>");
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

            public sealed partial class ShopTranslationsEN : ITranslationSet
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

            public sealed partial class ShopTranslationsEN : ITranslationSet
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
    [Arguments("ShopTranslationsEN", "IShopTranslations", "ShopTranslations", "TestApp.ShopTranslations.")]
    [Arguments("ShopTextsEN", "IShopTexts", "ShopTexts", "TestApp.ShopTexts.")]
    [Arguments("TranslationsEN", "ITranslations", "Translations", "TestApp.Translations.")]
    [Arguments("TranslationSetEN", "ITranslationSet", "TranslationSet", "TestApp.TranslationSet.")]
    public void Names_ShouldDeriveFromClassName(string className, string contract, string texts, string keyPrefix)
    {
        // Arrange & Act: a key starts with the full name of the generated class, as that class is named, a trailing
        // "Translations" included. The last set gets a contract named as the library's own interface is, which it
        // therefore lists by its full name.
        var text = ContractText(Run($$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public sealed partial class {{className}} : CodeChops.Idioma.ITranslationSet
            {
                public string Checkout => "Checkout";
            }
            """));

        // Assert
        text.ShouldContain($"public interface {contract} ");
        text.ShouldContain($"public static partial class {texts}");
        text.ShouldContain($"CreateResourceText<{contract}>(\"{keyPrefix}Checkout\",");
    }

    [Test]
    [Arguments("namespace TestApp;", "TestApp.ShopTranslations.Checkout")]
    [Arguments("namespace TestApp.Orders;", "TestApp.Orders.ShopTranslations.Checkout")]
    [Arguments("namespace TestApp.@event;", "TestApp.event.ShopTranslations.Checkout")]
    [Arguments("", "ShopTranslations.Checkout")]
    public void Key_ShouldBeFullNameOfGeneratedText(string @namespace, string key)
    {
        // Arrange & Act: a key is a name and not code, so a namespace part named like a keyword comes without its @,
        // and a set in the global namespace has a key without a namespace.
        var text = ContractText(Run($$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            {{@namespace}}

            public sealed partial class ShopTranslationsEN : ITranslationSet
            {
                public string Checkout => "Checkout";
            }
            """));

        // Assert
        text.ShouldContain($"CreateResourceText<IShopTranslations>(\"{key}\",");
    }

    [Test]
    public void TextsOfOneNameInSetsOfOneName_ShouldGetKeysOfTheirOwn()
    {
        // Arrange & Act: two sets of one name in two namespaces, and a third whose name differs from theirs only in a
        // trailing "Translations", each with a text named Checkout.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp.Orders
            {
                public sealed partial class ShopTranslationsEN : ITranslationSet
                {
                    public string Checkout => "Place order";
                }

                public sealed partial class ShopEN : ITranslationSet
                {
                    public string Checkout => "To the shop";
                }
            }

            namespace TestApp.Billing
            {
                public sealed partial class ShopTranslationsEN : ITranslationSet
                {
                    public string Checkout => "Pay invoice";
                }
            }
            """);

        // Assert: a resource file can then provide the wording of one of them, and not of all three at once.
        var generated = result.Results.ShouldHaveSingleItem().GeneratedSources;
        string Contract(string set) => generated.Single(source => String.Equals(source.HintName, $"{set}.Translations.g.cs", StringComparison.Ordinal)).SourceText.ToString();

        Contract("TestApp.Orders.ShopTranslationsEN").ShouldContain("(\"TestApp.Orders.ShopTranslations.Checkout\",");
        Contract("TestApp.Orders.ShopEN").ShouldContain("(\"TestApp.Orders.Shop.Checkout\",");
        Contract("TestApp.Billing.ShopTranslationsEN").ShouldContain("(\"TestApp.Billing.ShopTranslations.Checkout\",");
    }

    [Test]
    public void DefaultLanguageWithLongCode_ShouldWork()
    {
        // Arrange & Act
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("zh-Hans", "en")]

            namespace TestApp;

            public sealed partial class ShopTranslationsZhHans : ITranslationSet
            {
                string ITranslationSet.LanguageCode => "zh-Hans";
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
        // Arrange: LanguageCode is the ITranslationSet member, a virtual property is a text (so a set can derive and
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

            public partial class ShopTranslationsEN : ShopWordingBase, ITranslationSet
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

            internal sealed partial class ShopTranslationsEN : ITranslationSet
            {
                public string Checkout => "Checkout";
            }
            """;

        // Act
        var result = Run(source);

        // Assert: no other assembly could implement it, so it is not recorded for them.
        var text = ContractText(result);
        text.ShouldContain("internal interface IShopTranslations : CodeChops.Idioma.ITranslationSet");
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

            public sealed partial record ShopTranslationsEN : ITranslationSet
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

            public sealed partial class ShopTranslationsEN : ITranslationSet
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

            public sealed partial class ShopTranslationsEN : ITranslationSet;
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

            public sealed partial class OptionTranslationsEN : ITranslationSet
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
    [Arguments("namespace TestApp;", "ResourceKeyPrefix")]
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

            public sealed partial class ShopTranslationsEN : ITranslationSet
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
                public partial class ShopTranslationsEN : ITranslationSet
                {
                    public virtual string Cart => "Cart";
                }

                public class ShopTranslationsNL : IShopTranslations
                {
                    public virtual string Cart => "Winkelwagen";
                }

                public static class InlineTexts
                {
                    public static LocalizableText Orders { get; } = LocalizableText.Inline(en: "Orders", nl: "Bestellingen");
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
    [Arguments("OptionTranslations")]
    public void TextNamedLikeGeneratedClass_ShouldReportLOC014AndCompileOtherwise(string name)
    {
        // Arrange & Act: the contract keeps the text, so the sets still compile; only the generated text is left out.
        var errors = GeneratorTestHelper.CompileWithGenerators($$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public sealed partial class OptionTranslationsEN : ITranslationSet
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
    public void TextNamedLikeMemberOfObject_ShouldCompileWithoutWarnings()
    {
        // Arrange: a text named like a member every class has, as a filter's "Equals" is. The generated class hides
        // that member as the set does, which without the new modifier is a warning (CS0108) in generated code. Finalize
        // hides nothing, and neither does any other text, where the modifier would be a warning itself (CS0109).
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public sealed partial class FilterTranslationsEN : ITranslationSet
            {
                public new string Equals => "Equals";
                public new string GetHashCode => "Hash code";
                public new string GetType => "Type";
                public new string MemberwiseClone => "Copy";
                public new string ReferenceEquals => "Is the same as";
                public new string ToString => "As text";
                public string Finalize => "Finalize";
                public string Contains => "Contains";
            }

            public sealed class FilterTranslationsNL : IFilterTranslations
            {
                public new string Equals => "Is gelijk aan";
                public new string GetHashCode => "Hashcode";
                public new string GetType => "Type";
                public new string MemberwiseClone => "Kopie";
                public new string ReferenceEquals => "Is hetzelfde als";
                public new string ToString => "Als tekst";
                public string Finalize => "Afronden";
                public string Contains => "Bevat";
            }
            """;

        // Act
        var text = ContractText(Run(source));
        var warnings = GeneratorTestHelper.GetWarningsWithGenerators(source);
        var errors = GeneratorTestHelper.CompileWithGenerators(source);

        // Assert
        text.ShouldContain("public static new CodeChops.Idioma.ResourceText Equals { get; }");
        text.ShouldContain("public static CodeChops.Idioma.ResourceText Contains { get; }");
        warnings.ShouldBeEmpty();
        errors.ShouldBeEmpty();
    }

    [Test]
    public void SetDerivingFromSetOfProjectReferencedAsCompilation_ShouldNotStopTheGenerator()
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
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public sealed class MyGridTranslationsEnGB : RegionalLibrary.GridTranslationsEnGB
            {
                public override string Colour => "Hue";
            }
            """, library);

        // Assert: as in a build, where the language of such a set cannot be read, so nothing is held against it.
        result.Results.ShouldHaveSingleItem().Exception.ShouldBeNull();
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void SetWithoutDeclaredLanguages_ShouldReportLOC001AndEmitNoContract()
    {
        // Arrange & Act
        var result = Run("""
            using CodeChops.Idioma;

            namespace TestApp;

            public sealed partial class ShopTranslationsEN : ITranslationSet
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
    [Arguments("public sealed class ShopTranslationsEN : ITranslationSet { public string Checkout => \"Checkout\"; }", "it is not partial")]
    [Arguments("public sealed partial class ShopTranslations : ITranslationSet { public string LanguageCode => \"en\"; public string Checkout => \"Checkout\"; }", "its name does not end in EN")]
    [Arguments("public abstract partial class ShopTranslationsEN : ITranslationSet { public string Checkout => \"Checkout\"; }", "it is abstract")]
    [Arguments("public sealed partial class ShopTranslationsEN<T> : ITranslationSet { public string Checkout => \"Checkout\"; }", "it is generic")]
    [Arguments("file sealed partial class ShopTranslationsEN : ITranslationSet { public string Checkout => \"Checkout\"; }", "it is file-local")]
    [Arguments("public static partial class Container { public sealed partial class ShopTranslationsEN : ITranslationSet { public string Checkout => \"Checkout\"; } }", "it is nested in 'Container'")]
    [Arguments("public sealed partial class ShopTranslationsEN(string shopName) : ITranslationSet { public string Checkout => shopName; }", "it has no parameterless constructor")]
    [Arguments("public sealed partial class ShopTranslationsEN : ITranslationSet { private ShopTranslationsEN() { } public string Checkout => \"Checkout\"; }", "it has no parameterless constructor")]
    public void DefaultSetGeneratorCannotExtend_ShouldReportLOC002AndEmitNothing(string declaration, string expectedReason)
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

            public sealed partial class ShopTranslationsEN(string shopName) : ITranslationSet
            {
                public string Checkout => $"Pay at {shopName}";
            }
            """);

        // Assert: one error, at the set, and none in code the generators wrote (CS7036 on a 'new()' nobody typed).
        var error = errors.ShouldHaveSingleItem();
        error.Id.ShouldBe("LOC002");
        error.GetMessage().ShouldContain("it has no parameterless constructor");
        error.GetMessage().ShouldContain("with a public parameterless constructor");
    }

    [Test]
    [Arguments("public abstract class ShopTranslationsEN : ITranslationSet { public string Checkout => \"Checkout\"; }")]
    [Arguments("file sealed class ShopTranslationsEN : ITranslationSet { public string Checkout => \"Checkout\"; }")]
    [Arguments("public static class Container { public sealed class ShopTranslationsEN : ITranslationSet { public string Checkout => \"Checkout\"; } }")]
    [Arguments("public static partial class Container { [ExcludeFromTranslationDiscovery] public sealed partial class ShopTranslationsEN : ITranslationSet { public string Checkout => \"Checkout\"; } }")]
    [Arguments("public static partial class Container { public sealed partial class ShopTranslationsNL : ITranslationSet { public string Checkout => \"Afrekenen\"; } }")]
    [Arguments("[ExcludeFromTranslationDiscovery] public sealed partial class ShopTranslationsEN : ITranslationSet { public string Checkout => \"Checkout\"; }")]
    public void SetTakingNoPart_ShouldReportNothingAndEmitNothing(string declaration)
    {
        // Arrange & Act: registered by hand, if at all, so it has no contract to generate. Nothing marks it as one: it
        // is not partial (a test fixture), excluded, or in another language than the default.
        var result = Run($"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            {declaration}
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

            public sealed partial class ShopTranslationsEN : ITranslationSet
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

            public sealed partial class ShopTranslationsEN : ITranslationSet
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

            public sealed partial class ShopTranslationsEN : ITranslationSet
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
    [Arguments("[ExcludeFromTranslationDiscovery] public sealed class CartTranslationsNL : ITranslationSet { public string Checkout => \"Afrekenen\"; }")]
    public void ExcludedSet_ShouldReportNothingAboutItself(string declaration)
    {
        // Arrange & Act: a set deriving from a set of another language (LOC006), and one listing ITranslationSet in
        // another language than the default (LOC007), were either not excluded. A language not listed is still LOC005.
        var result = Run($"""
            {ShopSets}

            {declaration}
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

            public sealed partial class ShopTranslationsEN : ITranslationSet
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

            public sealed partial class ShopTranslationsEN : ITranslationSet
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

            public interface IGridTranslations : ITranslationSet
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

            internal interface IGridTranslations : ITranslationSet
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

            public interface IGridTranslations : ITranslationSet
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
    public void SetsListingITranslationSetBesideHandWrittenContract_ShouldReportNothingAndEmitNoContract(string accessibility, string partial)
    {
        // Arrange & Act: listing ITranslationSet again adds nothing to a set of a contract written by hand. The English set
        // is no default-language set to generate a second contract from (LOC002, or a duplicate IGridTranslations once
        // partial), and the Dutch set does implement its contract (LOC007).
        var result = Run($$"""
            {{Header}}

            {{accessibility}} interface IGridTranslations : ITranslationSet
            {
                string Next { get; }
            }

            {{accessibility}} {{partial}}class GridTranslationsEN : IGridTranslations, ITranslationSet
            {
                public string Next => "Next";
            }

            {{accessibility}} sealed class GridTranslationsNL : IGridTranslations, ITranslationSet
            {
                public string Next => "Volgende";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
        result.Results.ShouldHaveSingleItem().GeneratedSources.ShouldNotContain(source => source.HintName.EndsWith(".Translations.g.cs", StringComparison.Ordinal));
    }

    [Test]
    public void SetsListingITranslationSetBesideHandWrittenContractWithoutDeclaredLanguages_ShouldReportNothing()
    {
        // Arrange & Act: they start no contract, so they need no languages declared (LOC001).
        var result = Run("""
            using CodeChops.Idioma;

            namespace TestApp;

            public interface IGridTranslations : ITranslationSet
            {
                string Next { get; }
            }

            public sealed class GridTranslationsEN : IGridTranslations, ITranslationSet
            {
                public string Next => "Next";
            }

            public sealed class GridTranslationsNL : IGridTranslations, ITranslationSet
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

            public interface IGridTranslations : ITranslationSet
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

            public sealed partial class ShopTranslationsEN : ITranslationSet;
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

            public sealed partial class ShopTranslationsEN : ITranslationSet
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
    public void ListedRegion_ShouldNeedSetOfItsOwn()
    {
        // Arrange & Act: at run time en-GB resolves through en. Listing it says it has wording of its own, as every
        // inline text takes one, so the English set does not stand in for it.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "en-GB")]

            namespace TestApp;

            public partial class ShopTranslationsEN : ITranslationSet
            {
                public virtual string Cart => "Cart";
            }

            public class ShopTranslationsNL : IShopTranslations
            {
                public virtual string Cart => "Winkelwagen";
            }
            """);

        // Assert: the set to add derives from the English one, so that it holds only the texts that differ.
        var diagnostic = SingleDiagnostic(result, TranslationSetsGenerator.MissingLanguageDiagnosticId);
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldBe("'ShopTranslations' has no translation into 'en-GB', which [assembly: SupportedLanguages] requires. Add 'class ShopTranslationsEnGB : ShopTranslationsEN', which keeps the wording of 'en' for every text it does not override. A nested, generic, or file-local class does not count as a set.");
    }

    [Test]
    [Arguments("public class ShopTranslationsEnGB : ShopTranslationsEN;")]
    [Arguments("public class ShopTranslationsEnGB : ShopTranslationsEN { public override string Cart => \"Basket\"; }")]
    [Arguments("public class ShopTranslationsEnGB : IShopTranslations { public string Cart => \"Basket\"; }")]
    [Arguments("public class ShopTranslationsBritish : ShopTranslationsEN, ITranslationSet { string ITranslationSet.LanguageCode => \"en-GB\"; }")]
    public void ListedRegionWithSetOfItsOwn_ShouldReportNothing(string british)
    {
        // Arrange & Act: a set that derives from the English one and adds nothing will do, as will one that implements
        // the contract itself, or one that gives its language in LanguageCode.
        var result = Run($$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "en-GB")]

            namespace TestApp;

            public partial class ShopTranslationsEN : ITranslationSet
            {
                public virtual string Cart => "Cart";
            }

            public class ShopTranslationsNL : IShopTranslations
            {
                public virtual string Cart => "Winkelwagen";
            }

            {{british}}
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    [Arguments("\"nl\", \"en\", \"en-GB\"", "public sealed class ShopTranslationsEN", "Add 'class ShopTranslationsEnGB : IShopTranslations'. A nested, generic, or file-local class does not count as a set.")]
    [Arguments("\"nl\", \"en\", \"en-GB\"", "public record ShopTranslationsEN", "Add 'record ShopTranslationsEnGB : ShopTranslationsEN', which keeps the wording of 'en' for every text it does not override. A nested, generic, or file-local class does not count as a set.")]
    [Arguments("\"nl\", \"en-GB\"", "public class ShopTranslationsEN", "Add 'class ShopTranslationsEnGB : ShopTranslationsEN', which keeps the wording of 'en' for every text it does not override. A nested, generic, or file-local class does not count as a set.")]
    public void ListedRegionWithoutSet_ShouldBeToldWhichSetToAdd(string languages, string english, string advice)
    {
        // Arrange & Act: here the English set is not the default one. A sealed set has nothing deriving from it, a record
        // only a record, and a set of a language that is not listed itself still gives the region its wording.
        var result = Run($$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages({{languages}})]

            namespace TestApp;

            public partial class ShopTranslationsNL : ITranslationSet
            {
                public virtual string Cart => "Winkelwagen";
            }

            {{english}} : IShopTranslations
            {
                public virtual string Cart => "Cart";
            }
            """);

        // Assert
        var message = SingleDiagnostic(result, TranslationSetsGenerator.MissingLanguageDiagnosticId).GetMessage();
        message.ShouldContain("has no translation into 'en-GB'");
        message.ShouldEndWith(advice);
    }

    [Test]
    public void ListedRegionOfRegion_ShouldBeToldToDeriveFromNearestSet()
    {
        // Arrange & Act: zh-Hans-CN has both the set of zh and the set of zh-Hans above it.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "zh", "zh-Hans", "zh-Hans-CN")]

            namespace TestApp;

            public partial class ShopTranslationsEN : ITranslationSet
            {
                public virtual string Cart => "Cart";
            }

            public class ShopTranslationsZH : IShopTranslations
            {
                public virtual string Cart => "购物车";
            }

            public class ShopTranslationsZhHans : ShopTranslationsZH;
            """);

        // Assert
        var message = SingleDiagnostic(result, TranslationSetsGenerator.MissingLanguageDiagnosticId).GetMessage();
        message.ShouldEndWith("Add 'class ShopTranslationsZhHansCN : ShopTranslationsZhHans', which keeps the wording of 'zh-Hans' for every text it does not override. A nested, generic, or file-local class does not count as a set.");
    }

    [Test]
    public void HandWrittenContractMissingListedRegion_ShouldReportLOC004()
    {
        // Arrange & Act: a contract written by hand is held to its languages as a generated one is.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "en-GB")]

            namespace TestApp;

            public interface IGridTranslations : ITranslationSet
            {
                string First { get; }
            }

            public class GridTranslationsEN : IGridTranslations
            {
                public virtual string First => "First";
            }

            public class GridTranslationsNL : IGridTranslations
            {
                public virtual string First => "Eerste";
            }
            """);

        // Assert
        var message = SingleDiagnostic(result, TranslationSetsGenerator.MissingLanguageDiagnosticId).GetMessage();
        message.ShouldContain("'TestApp.IGridTranslations' has no translation into 'en-GB'");
        message.ShouldEndWith("Add 'class GridTranslationsEnGB : GridTranslationsEN', which keeps the wording of 'en' for every text it does not override. A nested, generic, or file-local class does not count as a set.");
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

            public sealed partial class ShopTranslationsEN : ITranslationSet
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
        // belongs to "Messages", and the set is Arabic. The Argentinian set is the one named MessagesEsAR.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "ar", "es", "es-AR")]

            namespace TestApp;

            public sealed partial class MessagesEN : ITranslationSet
            {
                public string Save => "Save";
            }

            public sealed class MessagesAR : IMessages
            {
                public string Save => "Hifz";
            }

            public class MessagesES : IMessages
            {
                public virtual string Save => "Guardar";
            }

            public sealed class MessagesEsAR : MessagesES;
            """);

        // Assert: no LOC004 asking for the Arabic set that is there, and each set recorded and linked under its own
        // language.
        DiagnosticIds(result).ShouldBeEmpty();
        var recorded = RecordedSets(result);
        recorded.ShouldContain("TranslationSetAttribute(\"TestApp.IMessages\", \"ar\")");
        recorded.ShouldContain("TranslationSetAttribute(\"TestApp.IMessages\", \"es-AR\")");
        var contract = ContractText(result);
        contract.ShouldContain("MessagesAR.Save\">ar</see>");
        contract.ShouldContain("MessagesES.Save\">es-AR</see>");
    }

    [Test]
    public void SetNamedAfterUnlistedLongCode_ShouldReportLOC005NamingBothWays()
    {
        // Arrange & Act: nl-BE is not declared, so the last two letters count, and the set serves 'be'.
        var result = Run($$"""
            {{Header}}

            public sealed partial class ShopTranslationsEN : ITranslationSet
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
        diagnostic.GetMessage().ShouldContain("implement 'string ITranslationSet.LanguageCode => \"nl-BE\";'");
    }

    [Test]
    public void SetDerivedFromLibrarySetNamedAfterLongCode_ShouldServeThatCode()
    {
        // Arrange: the library declares nl-BE, so its registrar records nl-BE for its set; the app declares only nl.
        var library = GeneratorTestHelper.CompileLibraryWithGenerators("RegionalLibrary", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl-BE")]

            namespace RegionalLibrary;

            public partial class ShopTranslationsEN : ITranslationSet
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

            public partial class ShopTranslationsNlBE : ITranslationSet
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

            public sealed partial class ShopTranslationsEN : ITranslationSet
            {
                public string Checkout => "Checkout";
            }

            public sealed class ShopTranslationsNlBE : IShopTranslations
            {
                string ITranslationSet.LanguageCode => "nl-BE";
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

            public partial class ShopTranslationsEN : ITranslationSet
            {
                public virtual string Checkout => "Checkout";
            }

            public sealed class ShopTranslationsNL : IShopTranslations
            {
                public string Checkout => "Afrekenen";
            }

            public sealed class ShopTranslationsGB : ShopTranslationsEN, ITranslationSet
            {
                string ITranslationSet.LanguageCode => "en-GB";
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

            public partial class ShopTranslationsEN : ITranslationSet
            {
                public virtual string Checkout => "Checkout";
            }

            public class ShopTranslationsNL : IShopTranslations
            {
                public virtual string Checkout => "Afrekenen";
            }

            public sealed class ShopTranslationsNlBE : ShopTranslationsNL, ITranslationSet
            {
                string ITranslationSet.LanguageCode => "nl-BE";
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

            public partial class ShopTranslationsEN : ITranslationSet
            {
                public virtual string Colour => "Color";
            }

            public sealed class ShopTranslationsGB : ShopTranslationsEN, ITranslationSet
            {
                string ITranslationSet.LanguageCode => "en-GB";
                public override string Colour => "Colour";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
        ContractText(result).ShouldContain("<see cref=\"ShopTranslationsGB.Colour\">en-GB</see>");
    }

    [Test]
    public void ListedRegionOfDefaultLanguage_ShouldNeedSetInEveryContract()
    {
        // Arrange & Act: en-GB is listed, so every contract has a British set, also the one with nothing British about
        // it. The set of the default language does not stand in for it.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "en-GB")]

            namespace TestApp;

            public partial class ShopTranslationsEN : ITranslationSet
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

            public partial class GridTranslationsEN : ITranslationSet
            {
                public string Rows => "Rows";
            }

            public class GridTranslationsNL : IGridTranslations
            {
                public string Rows => "Rijen";
            }
            """);

        // Assert: only the contract without a British set is reported, with the set that derives from its English one.
        var message = SingleDiagnostic(result, TranslationSetsGenerator.MissingLanguageDiagnosticId).GetMessage();
        message.ShouldContain("'GridTranslations' has no translation into 'en-GB'");
        message.ShouldEndWith("Add 'class GridTranslationsEnGB : GridTranslationsEN', which keeps the wording of 'en' for every text it does not override. A nested, generic, or file-local class does not count as a set.");
    }

    [Test]
    public void SetsDerivedFromAbstractBaseOfGeneratedContract_ShouldCountForTheirLanguage()
    {
        // Arrange & Act: only the abstract base names the contract generated here. The Dutch set used to be missed,
        // so the build asked for the class it already had (LOC004).
        var result = Run($$"""
            {{Header}}

            public partial class ShopTranslationsEN : ITranslationSet
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

            public partial class ShopTranslationsEN : ITranslationSet
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
    public void SetListingITranslationSetAgainWithoutDeclaredLanguages_ShouldReportNothing()
    {
        // Arrange & Act: listing ITranslationSet again is how a set deriving from another implements LanguageCode, so
        // it does not start a contract that would need languages (LOC001).
        var result = Run("""
            using CodeChops.Idioma;

            namespace TestApp;

            public sealed class MyGridTexts : GridLibrary.GridTranslationsEN, ITranslationSet
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

            public partial class ShopTranslationsEN : ITranslationSet
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

            public partial class ShopTranslationsEN : ITranslationSet
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
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void SetListingITranslationSetInOtherLanguage_ShouldReportLOC007Only()
    {
        // Arrange & Act: it compiles, and no text would ever read it.
        var result = Run($$"""
            {{Header}}

            public sealed partial class ShopTranslationsEN : ITranslationSet
            {
                public string Checkout => "Checkout";
            }

            public sealed class ShopTranslationsNL : ITranslationSet
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

            public partial class ListTranslationsEN : ITranslationSet
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

            public partial class MixedTranslationsEN : ITranslationSet
            {
                public virtual string First => "First";
            }

            public sealed class MixedTranslationsNL : IMixedTranslations
            {
                public string First => "Eerste";
            }

            public static class MixedTexts
            {
                public static LocalizableText Orders { get; } = LocalizableText.Inline(en: "Orders", nl: "Bestellingen");
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
    public void SetInOtherNamespaceBehindClassNamedLikeThatNamespace_ShouldBeLinkedWithResolvedCref()
    {
        // Arrange: inside namespace Shop the name Shop is the class, so Shop.Dutch.ShopTranslationsNL names nothing
        // there (CS1574), which stops a build that writes a documentation file and treats warnings as errors.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace Shop
            {
                public sealed class Shop;

                public partial class ShopTranslationsEN : ITranslationSet
                {
                    public virtual string Checkout => "Checkout";
                }
            }

            namespace Shop.Dutch
            {
                public class ShopTranslationsNL : IShopTranslations
                {
                    public virtual string Checkout => "Afrekenen";
                }
            }
            """;

        // Act
        var text = ContractText(Run(source));
        var errors = GeneratorTestHelper.CompileWithGenerators(source);
        var unresolved = GeneratorTestHelper.GetUnresolvedDocReferences(source);

        // Assert: the namespace is named once, at the top of the file, where nothing of the contract's namespace hides
        // it, and the set is reached through that alias.
        text.ShouldContain("using Namespace_Shop_Dutch = Shop.Dutch;");
        text.ShouldContain("<see cref=\"Namespace_Shop_Dutch.ShopTranslationsNL.Checkout\">nl</see>");
        text.ShouldContain("<see cref=\"ShopTranslationsEN.Checkout\">en</see>");
        errors.ShouldBeEmpty();
        unresolved.ShouldBeEmpty();
    }

    [Test]
    public void SetsInOneNamespace_ShouldGetNoAlias()
    {
        // Arrange & Act
        var text = ContractText(Run(ShopSets));

        // Assert
        text.ShouldNotContain("Namespace_");
    }

    [Test]
    public void SetInNamespaceWithPartNamedSystem_ShouldLeaveNoUnresolvedCref()
    {
        // Arrange: inside Acme.System.Shop the name System is Acme.System, where no Globalization lies.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace Acme.System.Shop;

            public partial class ShopTranslationsEN : ITranslationSet
            {
                public virtual string Checkout => "Checkout";
            }

            public class ShopTranslationsNL : IShopTranslations
            {
                public virtual string Checkout => "Afrekenen";
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
    public void DefaultWordingWithCharacterXmlDoesNotAllow_ShouldStayValidDocComment()
    {
        // Arrange: a control character and a form feed, neither of which a doc comment may hold, also as an entity
        // (CS1570), and half of a surrogate pair.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public sealed partial class ShopTranslationsEN : ITranslationSet
            {
                public string Checkout => "Check\u0001out";
                public string Page => "Page\fbreak";
                public string Half => "Half \uD83D";
                public string Whole => "Whole 😀";
            }
            """;

        // Act
        var text = ContractText(Run(source));
        var errors = GeneratorTestHelper.CompileWithGenerators(source);
        var invalid = GeneratorTestHelper.GetUnresolvedDocReferences(source);

        // Assert: shown as C# writes it, and a pair that is whole stays as it is.
        text.ShouldContain(@"/// Check\u0001out");
        text.ShouldContain(@"/// Page\u000Cbreak");
        text.ShouldContain(@"/// Half \uD83D");
        text.ShouldContain("/// Whole 😀");
        errors.ShouldBeEmpty();
        invalid.ShouldBeEmpty();
    }

    [Test]
    [Arguments("\"\"")]
    [Arguments("\"-\"")]
    [Arguments("\"en\\nx\"")]
    public void SetWithLanguageCodeThatIsNoCultureName_ShouldNotStopTheGeneratorOrBeHeldToALanguage(string languageCode)
    {
        // Arrange: what a LanguageCode holds while it is being typed. The contract must still be written: without it
        // every IShopTranslations of the project stops compiling.
        var source = $$"""
            {{ShopSets}}

            public class ShopTranslationsBritish : ShopTranslationsEN, ITranslationSet
            {
                string ITranslationSet.LanguageCode => {{languageCode}};
            }
            """;

        // Act
        var result = Run(source);

        // Assert: nothing about its language from here; LOC018 says what is wrong with the code.
        DiagnosticIds(result).ShouldBeEmpty();
        ContractText(result).ShouldContain("interface IShopTranslations");
        GeneratorTestHelper.GetGeneratorDiagnostics(source).Select(diagnostic => diagnostic.Id).ShouldBe(["LOC018"]);
    }

    [Test]
    [Arguments("\"e\", \"nl\"")]
    [Arguments("\"en_US\", \"nl\"")]
    [Arguments("null, \"nl\"")]
    [Arguments("\"e\"")]
    public void InvalidDefaultLanguage_ShouldNotMakeTheNextLanguageTheDefault(string languages)
    {
        // Arrange: the first language is wrong, which LOC010 reports. With the next one as the default, the English set
        // would be reported as unlisted, no contract would be written, and every use of it would stop compiling.
        var source = ShopSets.Replace("\"en\", \"nl\"", languages, StringComparison.Ordinal);

        // Act
        var result = Run(source);
        var errors = GeneratorTestHelper.CompileWithGenerators(source);

        // Assert: the contract is still written, from the set that starts it, and LOC010 stays the only error.
        DiagnosticIds(result).ShouldBeEmpty();
        ContractText(result).ShouldContain("interface IShopTranslations");
        errors.ShouldHaveSingleItem().Id.ShouldBe(LocalizableTextFactoryGenerator.InvalidLanguagesDiagnosticId);
    }

    [Test]
    public void InvalidDefaultLanguage_ShouldWriteContractOfTwoSetsStartingItOnce()
    {
        // Arrange: without a default language to tell them apart, the Dutch set starts the contract as the English
        // one does. Written twice it would not compile.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("e", "nl")]

            namespace TestApp;

            public sealed partial class ShopTranslationsEN : ITranslationSet
            {
                public string Checkout => "Checkout";
            }

            public sealed partial class ShopTranslationsNL : ITranslationSet
            {
                public string Checkout => "Afrekenen";
            }
            """;

        // Act
        var run = Run(source).Results.ShouldHaveSingleItem();
        var errors = GeneratorTestHelper.CompileWithGenerators(source);

        // Assert
        run.GeneratedSources.Count(generated => generated.HintName.EndsWith(".Translations.g.cs", StringComparison.Ordinal)).ShouldBe(1);
        errors.ShouldHaveSingleItem().Id.ShouldBe("LOC010");
    }

    [Test]
    [Arguments("public required string Checkout { get; init; }", "it has a required member ('Checkout'), which generated code cannot set")]
    [Arguments("internal ShopTranslationsEN() { } public string Checkout => \"Checkout\";", "it has no public parameterless constructor")]
    [Arguments("public ShopTranslationsEN(string shop = \"\") { } public string Checkout => \"Checkout\";", "it has no public parameterless constructor")]
    public void DefaultSetGeneratedCodeCannotCreate_ShouldReportLOC002AsItsOnlyError(string members, string expectedReason)
    {
        // Arrange & Act: the generated texts create the set through DefaultSet<TSet>, which takes a public
        // parameterless constructor (CS0310) and no member left to set (CS9040). With every generator at work.
        var errors = GeneratorTestHelper.CompileWithGenerators($$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public sealed partial class ShopTranslationsEN : ITranslationSet
            {
                {{members}}
            }
            """);

        // Assert: one error, at the set, and none in code the generators wrote.
        var error = errors.ShouldHaveSingleItem();
        error.Id.ShouldBe("LOC002");
        error.GetMessage().ShouldContain(expectedReason);
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

            public sealed partial class ShopTranslationsEN : ITranslationSet
            {
                [SetsRequiredMembers]
                public ShopTranslationsEN()
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
    [Arguments("ShopTranslationsEn", "ShopTranslationsEN")]
    [Arguments("ShopTranslationsen", "ShopTranslationsEN")]
    [Arguments("ShopTexts", "ShopTextsEN")]
    public void DefaultSetNotNamedAfterItsLanguage_ShouldBeToldTheNameThatEndsInIt(string name, string expectedName)
    {
        // Arrange & Act: a name that ends in its language in another letter case gets the suffix in place of that
        // ending, not after it (ShopTranslationsEnEN, whose generated class would be ShopTranslationsEn).
        var result = Run($$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public sealed partial class {{name}} : ITranslationSet
            {
                public string LanguageCode => "en";
                public string Checkout => "Checkout";
            }
            """);

        // Assert
        var diagnostic = SingleDiagnostic(result, TranslationSetsGenerator.CannotExtendDiagnosticId);
        diagnostic.Properties[TranslationSetsGenerator.SuggestedNameProperty].ShouldBe(expectedName);
    }

    [Test]
    [Arguments("private static readonly string Code = \"nl\"; public string LanguageCode => Code;")]
    [Arguments("public string LanguageCode => System.Globalization.CultureInfo.GetCultureInfo(\"nl\").Name;")]
    public void SetOfGeneratedContractWithUnreadableLanguage_ShouldNotBeReportedAsMissing(string languageCode)
    {
        // Arrange & Act: the build cannot tell which language the set serves, so it may be the Dutch one, and asking
        // for a Dutch set would ask for the set that exists.
        var result = Run($$"""
            {{Header}}

            public partial class ShopTranslationsEN : ITranslationSet
            {
                public virtual string Checkout => "Checkout";
            }

            public class ShopWording : IShopTranslations
            {
                {{languageCode}}
                public virtual string Checkout => "Afrekenen";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void ExcludedSetOfGeneratedContractWithUnreadableLanguage_ShouldNotBeReportedAsMissing()
    {
        // Arrange & Act: one class for every other language, created with its language and registered by hand, which
        // is what LOC003 advises for a set that takes constructor arguments.
        var result = Run($$"""
            {{Header}}

            public partial class ShopTranslationsEN : ITranslationSet
            {
                public virtual string Checkout => "Checkout";
            }

            [ExcludeFromTranslationDiscovery]
            public class ShopWording(string language) : IShopTranslations
            {
                public string LanguageCode => language;
                public virtual string Checkout => "Afrekenen";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void LibrarySetWithUnreadableLanguage_ShouldNotBeReportedAsMissingInApp()
    {
        // Arrange: the library's build could not read the language of its second set, and records the set as one of a
        // language unknown, so that the app's build does not ask for a set the library has.
        var library = GeneratorTestHelper.CompileLibraryWithGenerators("WordingLibrary", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace WordingLibrary;

            public partial class GridTranslationsEN : ITranslationSet
            {
                public virtual string First => "First";
            }

            public class GridWording : IGridTranslations
            {
                private static readonly string Code = "nl";

                public string LanguageCode => Code;
                public virtual string First => "Eerste";
            }
            """);

        // Act
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]
            """, library);

        // Assert: neither Dutch nor German, as the set may be of either.
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void SetWithUnreadableLanguage_ShouldBeRecordedForOtherAssembliesAsOfLanguageUnknown()
    {
        // Arrange & Act
        var recorded = RecordedSets(Run($$"""
            {{Header}}

            public partial class ShopTranslationsEN : ITranslationSet
            {
                public virtual string Checkout => "Checkout";
            }

            public class ShopWording : IShopTranslations
            {
                private static readonly string Code = "nl";

                public string LanguageCode => Code;
                public virtual string Checkout => "Afrekenen";
            }
            """));

        // Assert: under what is no culture name, so that a build that does not know it takes it for no language.
        recorded.ShouldContain("""TranslationSetAttribute("TestApp.IShopTranslations", "en")""");
        recorded.ShouldContain("""TranslationSetAttribute("TestApp.IShopTranslations", "*")""");
    }

    [Test]
    public void SetImplementingLanguageCodeOfAnotherInterface_ShouldServeLanguageOfItsName()
    {
        // Arrange & Act: the explicit implementation is not that of ITranslationSet.LanguageCode, which the set leaves
        // to its name. Taken for it, the set would count as German: unlisted, and the Dutch one missing.
        var result = Run($$"""
            {{Header}}

            public interface IHasCode
            {
                string LanguageCode { get; }
            }

            public partial class ShopTranslationsEN : ITranslationSet
            {
                public virtual string Checkout => "Checkout";
            }

            public class ShopTranslationsNL : IShopTranslations, IHasCode
            {
                string IHasCode.LanguageCode => "de";

                public virtual string Checkout => "Afrekenen";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    [Arguments("zh-Hant", "zh-TW", true)]
    [Arguments("zh-Hant", "zh-HK", true)]
    [Arguments("zh-Hant", "zh-MO", true)]
    [Arguments("zh-Hans", "zh-CN", true)]
    [Arguments("zh-Hans", "zh-SG", true)]
    [Arguments("ZH-hans", "zh-cn", true)]
    [Arguments("zh", "zh-TW", true)]
    [Arguments("zh-Hans", "zh-TW", false)]
    [Arguments("zh-Hant", "zh-CN", false)]
    [Arguments("zh-TW", "zh-Hant", false)]
    [Arguments("en", "en-GB", true)]
    [Arguments("en-GB", "en", false)]
    [Arguments("en", "zh-TW", false)]
    public void Language_ShouldServeCulturesThatResolveThroughItAtRunTime(string provided, string requested, bool expected)
    {
        // Arrange & Act: a culture resolves through its parents, which for a Chinese region written without its script
        // is the script (zh-TW under zh-Hant), and no start of its name.
        var serves = SupportedLanguages.Serves(provided, requested);

        // Assert
        serves.ShouldBe(expected);
    }

    [Test]
    public void ChineseRegionDerivedFromSetOfItsScript_ShouldBeAllowed()
    {
        // Arrange & Act: users in Taiwan get the Traditional Chinese wording at run time, so the set derives from a
        // set of its own language, and nothing is left in another one (LOC006).
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "zh-Hant", "zh-TW")]

            namespace TestApp;

            public partial class ShopTranslationsEN : ITranslationSet
            {
                public virtual string Checkout => "Checkout";
            }

            public class ShopTranslationsZhHant : IShopTranslations
            {
                public virtual string Checkout => "結帳";
            }

            public class ShopTranslationsZhTW : ShopTranslationsZhHant;
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void LibraryServingChineseRegionThroughItsScript_ShouldReportNothing()
    {
        // Arrange
        var library = GeneratorTestHelper.CompileLibraryWithGenerators("ChineseLibrary", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "zh-Hans")]

            namespace ChineseLibrary;

            public partial class GridTranslationsEN : ITranslationSet
            {
                public virtual string First => "First";
            }

            public class GridTranslationsZhHans : IGridTranslations
            {
                public virtual string First => "第一";
            }
            """);

        // Act: users in China get the library's Simplified Chinese set at run time.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "zh-CN")]
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

            public sealed partial class ShopTranslationsEN : ITranslationSet
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

    #region Texts that take values
    /// <summary>
    /// A set with a text that takes one value, documented, and one that takes two and chooses between two wordings.
    /// </summary>
    private const string GreetingSets = $$"""
        {{Header}}

        public sealed partial class ShopTranslationsEN : ITranslationSet
        {
            public string Checkout => "Checkout";

            /// <summary>
            /// Greets a customer.
            /// </summary>
            /// <param name="name">The name of the customer.</param>
            public string Greeting(string name) => $"Hello, {name}!";

            public string ItemsInCart(int count, string shop) => count is 1
                ? $"1 item in your cart at {shop}"
                : $"{count} items in your cart at {shop}";
        }

        public sealed class ShopTranslationsNL : IShopTranslations
        {
            public string Checkout => "Afrekenen";
            public string Greeting(string name) => $"Hallo, {name}!";

            public string ItemsInCart(int count, string shop) => count is 1
                ? $"1 artikel in je winkelwagen bij {shop}"
                : $"{count} artikelen in je winkelwagen bij {shop}";
        }
        """;

    [Test]
    public Task SetWithTextsThatTakeValues_ShouldEmitMethodsInContractAndTextsClass()
    {
        // Arrange & Act
        var result = Run(GreetingSets);

        // Assert
        var run = result.Results.ShouldHaveSingleItem();
        run.Diagnostics.ShouldBeEmpty();

        return Verify(ContractText(result));
    }

    [Test]
    public void TextsThatTakeValues_ShouldCompileWithoutWarningsAndWithResolvedCrefs()
    {
        // Arrange & Act
        var errors = GeneratorTestHelper.CompileWithGenerators(GreetingSets);
        var warnings = GeneratorTestHelper.GetWarningsWithGenerators(GreetingSets);
        var unresolved = GeneratorTestHelper.GetUnresolvedDocReferences(GreetingSets);

        // Assert
        errors.ShouldBeEmpty();
        warnings.ShouldBeEmpty();
        unresolved.ShouldBeEmpty();
    }

    [Test]
    public void TextThatTakesValues_ShouldBeAMethodOfContractAndTextsClassKeyedByItsName()
    {
        // Arrange & Act
        var text = ContractText(Run(GreetingSets));

        // Assert: the contract repeats the method, which is what holds every language to its parameters, and marks
        // the values its wording shows. The class creates the text on every call, with the values it is called with.
        text.ShouldContain("    string Greeting([CodeChops.Idioma.Internals.ShownValue] string name);");
        text.ShouldContain("    string ItemsInCart([CodeChops.Idioma.Internals.ShownValue] int count, [CodeChops.Idioma.Internals.ShownValue] string shop);");
        text.ShouldContain("ResourceText Greeting(string name) =>");
        text.ShouldContain("""public static CodeChops.Idioma.ResourceText Greeting(string name) => CreateResourceText<IShopTranslations>("TestApp.ShopTranslations.Greeting", static (translations, values) => ((IShopTranslations)translations).Greeting((string)values[0]!), DefaultSet<ShopTranslationsEN>.Instance, [name]);""");
        text.ShouldContain("""public static CodeChops.Idioma.ResourceText ItemsInCart(int count, string shop) => CreateResourceText<IShopTranslations>("TestApp.ShopTranslations.ItemsInCart", static (translations, values) => ((IShopTranslations)translations).ItemsInCart((int)values[0]!, (string)values[1]!), DefaultSet<ShopTranslationsEN>.Instance, [count, shop]);""");
    }

    [Test]
    public void TextThatTakesValues_ShouldBeDocumentedLikeAnyText()
    {
        // Arrange & Act
        var text = ContractText(Run(GreetingSets));

        // Assert: the doc comment of the method is inherited, parameters included, and the wording of each language
        // stands next to its link with each value in braces. A method that chooses between wordings shows none.
        text.Split('\n').Count(line => line.Contains("/// <inheritdoc cref=\"ShopTranslationsEN.Greeting\"/>", StringComparison.Ordinal)).ShouldBe(2);
        text.Split('\n').Count(line => line.Contains("<see cref=\"ShopTranslationsEN.Greeting\">en</see>: Hello, {name}!</description>", StringComparison.Ordinal)).ShouldBe(2);
        text.Split('\n').Count(line => line.Contains("<see cref=\"ShopTranslationsNL.Greeting\">nl</see>: Hallo, {name}!</description>", StringComparison.Ordinal)).ShouldBe(2);
        text.Split('\n').Count(line => line.Contains("/// The <c>ItemsInCart</c> text of <see cref=\"ShopTranslationsEN\"/>.", StringComparison.Ordinal)).ShouldBe(2);
        text.Split('\n').Count(line => line.Contains("<see cref=\"ShopTranslationsNL.ItemsInCart\">nl</see></description>", StringComparison.Ordinal)).ShouldBe(2);
    }

    [Test]
    public void OnlyPublicInstanceMethodsReturningStringWithParametersAreTexts()
    {
        // Arrange: a virtual method is a text (so a set can derive and override), an inherited method belongs to the
        // base (it is not redeclared), a method without parameters stays what it was before texts took values, a
        // helper, and the rest are not texts by shape.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public class ShopWordingBase
            {
                public string Inherited(string name) => name;
            }

            public partial class ShopTranslationsEN : ShopWordingBase, ITranslationSet
            {
                public string Greeting(string name) => $"Hello, {name}!";
                public virtual string Farewell(string name) => $"Goodbye, {name}!";
                public string Parameterless() => "Method";
                public string LanguageCode(int unused) => "en";
                internal string Hidden(string name) => name;
                private string Secret(string name) => name;
                public static string Shared(string name) => name;
                public int Count(string name) => name.Length;
                public void Nothing(string name) { }
                public ref readonly string Reference(string[] names) => ref names[0];
            }
            """;

        // Act
        var result = Run(source);
        var text = ContractText(result);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
        text.ShouldContain("    string Greeting([CodeChops.Idioma.Internals.ShownValue] string name);");
        text.ShouldContain("    string Farewell([CodeChops.Idioma.Internals.ShownValue] string name);");
        foreach (var name in new[] { "Inherited", "Parameterless", "LanguageCode", "Hidden", "Secret", "Shared", "Count", "Nothing", "Reference" })
            text.ShouldNotContain($" {name}(", customMessage: name);
    }

    [Test]
    [Arguments("public string Greeting<T>(T name) => $\"Hello, {name}!\";", "it is generic")]
    [Arguments("public string Greeting(string name) => name; private string Greeting(string name, bool formal) => name;", "its set has another method of that name")]
    [Arguments("public string Greeting(ref string name) => name;", "its parameter 'name' is passed by reference")]
    [Arguments("public string Greeting(in int count) => \"\";", "its parameter 'count' is passed by reference")]
    [Arguments("public string Greeting(out string name) => name = \"\";", "its parameter 'name' is passed by reference")]
    [Arguments("public string Greeting(params string[] names) => \"\";", "its parameter 'names' is a params parameter")]
    [Arguments("public string Greeting(string name = \"you\") => name;", "its parameter 'name' has a default value")]
    [Arguments("public string Greeting(System.ReadOnlySpan<char> name) => name.ToString();", "its parameter 'name' is of a type a text cannot keep")]
    [Arguments("public unsafe string Greeting(int* count) => \"\";", "its parameter 'count' is of a type a text cannot keep")]
    [Arguments("public string Greeting(dynamic name) => \"\";", "its parameter 'name' is dynamic")]
    public void MethodThatCannotBeAText_ShouldReportLOC019AtItAndEmitNoTextForIt(string method, string expectedReason)
    {
        // Arrange
        var source = $$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public sealed partial class ShopTranslationsEN : ITranslationSet
            {
                public string Checkout => "Checkout";
                {{method}}
            }
            """;

        // Act
        var result = Run(source);

        // Assert: at the method, which is what to change. The other texts are generated all the same.
        var diagnostic = SingleDiagnostic(result, TranslationSetsGenerator.UnsupportedTextDiagnosticId);
        diagnostic.Id.ShouldBe("LOC019");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("'ShopTranslationsEN.Greeting' is a public method that returns a string");
        diagnostic.GetMessage().ShouldContain(expectedReason);
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).ShouldBe("Greeting");

        var text = ContractText(result);
        text.ShouldContain("    string Checkout { get; }");
        text.ShouldNotContain("Greeting");
    }

    [Test]
    public void TwoPublicMethodsOfOneName_ShouldEachReportLOC019()
    {
        // Arrange & Act: a text is told from the others by its name, which is its key, so neither can be one.
        var result = Run($$"""
            {{Header}}

            public sealed partial class ShopTranslationsEN : ITranslationSet
            {
                public string Greeting(string name) => $"Hello, {name}!";
                public string Greeting(string name, string title) => $"Hello, {title} {name}!";
            }

            public sealed class ShopTranslationsNL : IShopTranslations;
            """);

        // Assert
        DiagnosticIds(result).ShouldBe(["LOC019", "LOC019"]);
        ContractText(result).ShouldNotContain("Greeting");
    }

    [Test]
    public void MethodThatCannotBeAText_ShouldBeNoPartOfTheContract()
    {
        // Arrange & Act: LOC019 is the only error, so the Dutch set, which has no such method, implements the contract.
        var errors = GeneratorTestHelper.CompileWithGenerators($$"""
            {{Header}}

            public sealed partial class ShopTranslationsEN : ITranslationSet
            {
                public string Checkout => "Checkout";
                public string Greeting(string name = "you") => $"Hello, {name}!";
            }

            public sealed class ShopTranslationsNL : IShopTranslations
            {
                public string Checkout => "Afrekenen";
            }
            """);

        // Assert
        errors.ShouldHaveSingleItem().Id.ShouldBe("LOC019");
    }

    [Test]
    public void SetWithoutTextThatTakesValues_ShouldNotCompile()
    {
        // Arrange & Act: the contract demands the method of every language, as it demands every property.
        var errors = GeneratorTestHelper.CompileWithGenerators($$"""
            {{Header}}

            public sealed partial class ShopTranslationsEN : ITranslationSet
            {
                public string Greeting(string name) => $"Hello, {name}!";
            }

            public sealed class ShopTranslationsNL : IShopTranslations;
            """);

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Id.ShouldBe("CS0535");
        error.GetMessage().ShouldContain("IShopTranslations.Greeting(string)");
    }

    [Test]
    public void ParametersOfEveryKindOfType_ShouldBeDeclaredAsTheSetDeclaresThem()
    {
        // Arrange: keywords, types of the set's own namespace, of another, of the global one, and of the framework,
        // with what may be null marked as the set marks it, arrays, tuples with and without names, a delegate, and
        // names that are keywords. The Dutch set declares each as the English one does, so a parameter the contract
        // declared in another way would not compile, or warn about what may be null.
        const string methods = """
            public string Keywords(string? name, int count, decimal price, object? value, nint handle) => "";
            public string OwnNamespace(Size size, Size? optional) => "";
            public string OtherNamespace(Salutation salutation, Order.Line line, Box<Order?>.Lid? lid) => "";
            public string GlobalNamespace(GlobalKind kind) => "";
            public string Framework(DateOnly date, DateTime? time, IReadOnlyList<string?> names, Dictionary<string, List<int>>? counts) => "";
            public string Arrays(string[] names, int[,] grid, string?[]? optional, int[][] jagged, int[]?[] rows) => "";
            public string Tuples((string Name, int Count) pair, (int, string?) unnamed) => "";
            public string Delegates(Func<int, string> format) => "";
            public string @event(string @class, int @int) => "";
            """;

        var source = $$"""
            #nullable enable
            using System;
            using System.Collections.Generic;
            using CodeChops.Idioma;
            using TestApp.Models;

            [assembly: SupportedLanguages("en", "nl")]

            public enum GlobalKind { One }

            namespace TestApp.Models
            {
                public enum Salutation { Unknown, Madam, Sir }
                public sealed class Order { public sealed class Line; }
                public sealed class Box<T> { public sealed class Lid; }
            }

            namespace TestApp
            {
                public enum Size { Small }

                public sealed partial class ShopTranslationsEN : ITranslationSet
                {
                    {{methods}}
                }

                public sealed class ShopTranslationsNL : IShopTranslations
                {
                    {{methods}}
                }
            }
            """;

        // Act
        var text = ContractText(Run(source));
        var errors = GeneratorTestHelper.CompileWithGenerators(source);
        var warnings = GeneratorTestHelper.GetWarningsWithGenerators(source);

        // Assert: a type of another namespace is reached through the alias of that namespace, as a set of another
        // namespace is.
        errors.ShouldBeEmpty();
        warnings.ShouldBeEmpty();
        text.ShouldContain("using Namespace_System = System;");
        text.ShouldContain("using Namespace_System_Collections_Generic = System.Collections.Generic;");
        text.ShouldContain("using Namespace_TestApp_Models = TestApp.Models;");
        text.ShouldContain("    string Keywords(string? name, int count, decimal price, object? value, nint handle);");
        text.ShouldContain("    string OwnNamespace(Size size, Size? optional);");
        text.ShouldContain("    string OtherNamespace(Namespace_TestApp_Models.Salutation salutation, Namespace_TestApp_Models.Order.Line line, Namespace_TestApp_Models.Box<Namespace_TestApp_Models.Order?>.Lid? lid);");
        text.ShouldContain("    string GlobalNamespace(GlobalKind kind);");
        text.ShouldContain("    string Framework(Namespace_System.DateOnly date, Namespace_System.DateTime? time, Namespace_System_Collections_Generic.IReadOnlyList<string?> names, Namespace_System_Collections_Generic.Dictionary<string, Namespace_System_Collections_Generic.List<int>>? counts);");
        text.ShouldContain("    string Arrays(string[] names, int[,] grid, string?[]? optional, int[][] jagged, int[]?[] rows);");
        text.ShouldContain("    string Tuples((string Name, int Count) pair, (int, string?) unnamed);");
        text.ShouldContain("    string Delegates(Namespace_System.Func<int, string> format);");
        text.ShouldContain("    string @event(string @class, int @int);");
        text.ShouldContain("ResourceText @event(string @class, int @int) => CreateResourceText<IShopTranslations>(\"TestApp.ShopTranslations.event\", static (translations, values) => ((IShopTranslations)translations).@event((string)values[0]!, (int)values[1]!), DefaultSet<ShopTranslationsEN>.Instance, [@class, @int]);");
    }

    [Test]
    public void ParameterTypesOfSetWithoutNullableAnnotations_ShouldCompileWithoutWarnings()
    {
        // Arrange: a set in a file that does not say what may be null. The generated file does, and must not take the
        // set's parameters for ones that are never null.
        const string source = """
            #nullable disable
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public sealed partial class ShopTranslationsEN : ITranslationSet
            {
                public string Greeting(string name, string[] titles) => $"Hello, {name}!";
            }

            public sealed class ShopTranslationsNL : IShopTranslations
            {
                public string Greeting(string name, string[] titles) => $"Hallo, {name}!";
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
    public void NamesStandingBeforeWhatGeneratedMethodWrites_ShouldCompile()
    {
        // Arrange: parameters named like the set and the values the generated method names itself, like the helpers
        // it calls, and like another text; and texts named like those helpers and like the namespace of a parameter's
        // type.
        const string methods = """
            public string Translated(string translations, string translations_) => translations + translations_;
            public string Valued(string values, int values_) => values + values_;
            public string CreateResourceText(string DefaultSet, string Instance) => DefaultSet + Instance;
            public string DefaultSet(string CreateResourceText) => CreateResourceText;
            public string Name => "Name";
            public string Named(string Name) => Name;
            public string System(DateOnly date) => $"{date}";
            public string CodeChops(string value) => value;
            """;

        var source = $$"""
            using System;
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public sealed partial class OptionTranslationsEN : ITranslationSet
            {
                {{methods}}
            }

            public sealed class OptionTranslationsNL : IOptionTranslations
            {
                {{methods}}
            }
            """;

        // Act
        var text = ContractText(Run(source));
        var errors = GeneratorTestHelper.CompileWithGenerators(source);
        var unresolved = GeneratorTestHelper.GetUnresolvedDocReferences(source);

        // Assert
        errors.ShouldBeEmpty();
        unresolved.ShouldBeEmpty();
        text.ShouldContain("static (translations__, values) => ((IOptionTranslations)translations__).Translated((string)values[0]!, (string)values[1]!)");
        text.ShouldContain("static (translations, values__) => ((IOptionTranslations)translations).Valued((string)values__[0]!, (int)values__[1]!)");
    }

    [Test]
    [Arguments("public static class System;", "DateOnly", "using System;")]
    [Arguments("public static class TestApp;", "Salutation", "using TestApp.Models;")]
    public void ClassStandingBeforeNamespaceOfParameterType_ShouldCompile(string other, string parameterType, string import)
    {
        // Arrange: inside the set's namespace a class carries the name the namespace of the parameter's type starts
        // with, so that the type's full name would name nothing there.
        var source = $$"""
            {{import}}
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp.Models
            {
                public enum Salutation { Unknown, Madam, Sir }
            }

            namespace TestApp
            {
                {{other}}

                public sealed partial class ShopTranslationsEN : ITranslationSet
                {
                    public string Dear({{parameterType}} value) => $"Dear {value}";
                }

                public sealed class ShopTranslationsNL : IShopTranslations
                {
                    public string Dear({{parameterType}} value) => $"Beste {value}";
                }
            }
            """;

        // Act
        var errors = GeneratorTestHelper.CompileWithGenerators(source);

        // Assert
        errors.ShouldBeEmpty();
    }

    [Test]
    public void TextThatTakesWhatMemberOfObjectTakes_ShouldCompileWithoutWarnings()
    {
        // Arrange: only a method that takes what a member of every class takes hides that member in the generated
        // class, and says so with the new modifier. A method of that name with other parameters stands next to it,
        // where the modifier would be a warning itself (CS0109).
        const string methods = """
            public new string Equals(object? other) => $"Equals {other}";
            public new string ReferenceEquals(object? left, object? right) => $"{left} is {right}";
            public string ToString(string format) => format;
            public string GetHashCode(int seed) => $"{seed}";
            """;

        var source = $$"""
            #nullable enable
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public sealed partial class FilterTranslationsEN : ITranslationSet
            {
                {{methods}}
            }

            public sealed class FilterTranslationsNL : IFilterTranslations
            {
                {{methods}}
            }
            """;

        // Act
        var text = ContractText(Run(source));
        var warnings = GeneratorTestHelper.GetWarningsWithGenerators(source);
        var errors = GeneratorTestHelper.CompileWithGenerators(source);

        // Assert
        text.ShouldContain("public static new CodeChops.Idioma.ResourceText Equals(object? other)");
        text.ShouldContain("public static new CodeChops.Idioma.ResourceText ReferenceEquals(object? left, object? right)");
        text.ShouldContain("public static CodeChops.Idioma.ResourceText ToString(string format)");
        text.ShouldContain("public static CodeChops.Idioma.ResourceText GetHashCode(int seed)");
        warnings.ShouldBeEmpty();
        errors.ShouldBeEmpty();
    }

    [Test]
    public void TextThatTakesValuesNamedLikeGeneratedClass_ShouldReportLOC014AndCompileOtherwise()
    {
        // Arrange & Act: the contract keeps the text, so the sets still compile; only the generated method is left out.
        var errors = GeneratorTestHelper.CompileWithGenerators("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public sealed partial class OptionTranslationsEN : ITranslationSet
            {
                public string OptionTranslations(string name) => $"Options of {name}";
            }

            public sealed class OptionTranslationsNL : IOptionTranslations
            {
                public string OptionTranslations(string name) => $"Opties van {name}";
            }
            """);

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Id.ShouldBe("LOC014");
        error.GetMessage().ShouldContain("'OptionTranslationsEN' has a text named 'OptionTranslations'");
    }

    [Test]
    public void SetOfOnlyTextsThatTakeValues_ShouldShowOneWithItsValuesLeftOutAsItsExample()
    {
        // Arrange & Act
        var text = ContractText(Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public sealed partial class ShopTranslationsEN : ITranslationSet
            {
                public string Greeting(string name) => $"Hello, {name}!";
            }
            """));

        // Assert: in markup a text that takes values does not stand by its name alone.
        text.ShouldContain("<c>@ShopTranslations.Greeting(...)</c>");
    }

    [Test]
    public void TextThatTakesValuesInheritedOrImplementedExplicitly_ShouldBeLinkedWithResolvedCref()
    {
        // Arrange: a British set that leaves the text as the English set words it, which a cref names on the English
        // set, a Dutch set that implements it explicitly, which has no name to link to, and a German set with a helper
        // of the text's name, which a cref cannot tell from the text.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de", "en-GB")]

            namespace TestApp;

            public partial class ShopTranslationsEN : ITranslationSet
            {
                public virtual string Greeting(string name) => $"Hello, {name}!";
            }

            public sealed class ShopTranslationsEnGB : ShopTranslationsEN;

            public sealed class ShopTranslationsNL : IShopTranslations
            {
                string IShopTranslations.Greeting(string name) => $"Hallo, {name}!";
            }

            public sealed class ShopTranslationsDE : IShopTranslations
            {
                public string Greeting(string name) => Greeting(name, formal: false);
                private static string Greeting(string name, bool formal) => formal ? $"Guten Tag, {name}!" : $"Hallo, {name}!";
            }
            """;

        // Act
        var result = Run(source);
        var text = ContractText(result);
        var errors = GeneratorTestHelper.CompileWithGenerators(source);
        var unresolved = GeneratorTestHelper.GetUnresolvedDocReferences(source);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
        errors.ShouldBeEmpty();
        unresolved.ShouldBeEmpty();
        text.ShouldContain("<see cref=\"ShopTranslationsEN.Greeting\">en-GB</see>: Hello, {name}! (inherited from <see cref=\"ShopTranslationsEN\"/>)</description>");
        text.ShouldContain("<see cref=\"ShopTranslationsNL\">nl</see>: Hallo, {name}!</description>");
        text.ShouldContain("<see cref=\"ShopTranslationsDE\">de</see></description>");
    }

    [Test]
    public void WordingOfTextThatTakesValues_ShouldShowEachValueInBracesAsItIsWritten()
    {
        // Arrange: a value with a format, a method with a body, a raw string that writes a value between two braces,
        // a brace that is text, a constant, and a method that chooses between wordings, which has no one wording.
        const string source = """"
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public sealed partial class ShopTranslationsEN : ITranslationSet
            {
                public string Total(decimal amount) => $"Total: {amount:N2}";
                public string Greeting(string name) { return $"Hello, {name}!"; }
                public string Raw(string name) => $$"""Hello, {{name}}, and {braces}!""";
                public string Braces(string name) => $"{{{name}}} & <b>";
                public string Constant(string name) => "Hello";
                public string Chosen(int count) => count is 1 ? "One" : "Many";
                public string Statements(int count) { var text = $"{count}"; return text; }
            }
            """";

        // Act
        var text = ContractText(Run(source));
        var unresolved = GeneratorTestHelper.GetUnresolvedDocReferences(source);

        // Assert
        unresolved.ShouldBeEmpty();
        text.ShouldContain("<see cref=\"ShopTranslationsEN.Total\">en</see>: Total: {amount:N2}</description>");
        text.ShouldContain("<see cref=\"ShopTranslationsEN.Greeting\">en</see>: Hello, {name}!</description>");
        text.ShouldContain("<see cref=\"ShopTranslationsEN.Raw\">en</see>: Hello, {name}, and {braces}!</description>");
        text.ShouldContain("<see cref=\"ShopTranslationsEN.Braces\">en</see>: {{name}} &amp; &lt;b&gt;</description>");
        text.ShouldContain("<see cref=\"ShopTranslationsEN.Constant\">en</see>: Hello</description>");
        text.ShouldContain("<see cref=\"ShopTranslationsEN.Chosen\">en</see></description>");
        text.ShouldContain("<see cref=\"ShopTranslationsEN.Statements\">en</see></description>");
    }

    [Test]
    public void ValuesTheDefaultWordingShows_ShouldBeMarkedInContract()
    {
        // Arrange: a value in the wording, a value handed on, and a value in a body with statements are shown. A value
        // that stands only where a wording is chosen is not: in a condition, in what a switch switches on, in a
        // pattern, in a when clause, or in a comparison. Nor is a value the method never uses, or a name that stands
        // for something else than the parameter.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public enum Salutation { Unknown, Madam, Sir }

            public sealed partial class ShopTranslationsEN : ITranslationSet
            {
                public string Shown(string name) => $"Hello, {name}!";
                public string ShownInOneWording(int count) => count is 1 ? "1 item" : $"{count} items";
                public string HandedOn(string name) => Twice(name);
                public string InStatements(string name) { var greeting = "Hello, " + name; return greeting; }
                public string InCondition(int count) => count > 1 ? "Several" : "One";
                public string InSwitch(Salutation salutation) => salutation switch { Salutation.Madam => "Madam", _ => "Customer" };
                public string InPattern(object value, int limit) => value switch { int number when number > limit => "More", _ => "Less" };
                public string InIf(bool formal) { if (formal) return "Good day"; return "Hi"; }
                public string InComparison(int count) { var several = count != 1; return several ? "Several" : "One"; }
                public string Unused(string name) => "Hello!";
                public string Shadowed(string name) => string.Join(", ", new[] { "a" }.Select(name => name));
                public string Mixed(Salutation salutation, string name) => salutation is Salutation.Madam ? $"Dear Mrs {name}" : $"Dear {name}";

                private static string Twice(string value) => value + value;
            }
            """;

        // Act
        var text = ContractText(Run(source));

        // Assert: in the contract, which is what another language implements, and not on the method of the class.
        const string shown = "[CodeChops.Idioma.Internals.ShownValue] ";
        text.ShouldContain($"    string Shown({shown}string name);");
        text.ShouldContain($"    string ShownInOneWording({shown}int count);");
        text.ShouldContain($"    string HandedOn({shown}string name);");
        text.ShouldContain($"    string InStatements({shown}string name);");
        text.ShouldContain("    string InCondition(int count);");
        text.ShouldContain("    string InSwitch(Salutation salutation);");
        text.ShouldContain("    string InPattern(object value, int limit);");
        text.ShouldContain("    string InIf(bool formal);");
        text.ShouldContain("    string InComparison(int count);");
        text.ShouldContain("    string Unused(string name);");
        text.ShouldContain("    string Shadowed(string name);");
        text.ShouldContain($"    string Mixed(Salutation salutation, {shown}string name);");
        text.ShouldContain("ResourceText Shown(string name) =>");
    }

    [Test]
    public void MemberMarkedAsExcluded_ShouldBeNoText()
    {
        // Arrange: a property and a method that have to be public and are no texts, a method that could not be a text
        // anyway, and one named like the generated class. The Dutch set has none of them.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public sealed partial class ShopTranslationsEN : ITranslationSet
            {
                public string Total(decimal amount) => $"Total: {Money(amount)}";

                [ExcludeFromTranslationDiscovery]
                public string Pattern => "C";

                [ExcludeFromTranslationDiscovery]
                public string Money(decimal amount) => amount.ToString(Pattern);

                [ExcludeFromTranslationDiscovery]
                public string Generic<T>(T value) => $"{value}";

                [ExcludeFromTranslationDiscovery]
                public string ShopTranslations(string value) => value;
            }

            public sealed class ShopTranslationsNL : IShopTranslations
            {
                public string Total(decimal amount) => $"Totaal: {amount:C}";
            }
            """;

        // Act
        var result = Run(source);
        var text = ContractText(result);
        var errors = GeneratorTestHelper.CompileWithGenerators(source);

        // Assert: nothing is reported about them, the contract and the class hold the one text, and the Dutch set,
        // which has only that one, compiles.
        DiagnosticIds(result).ShouldBeEmpty();
        errors.ShouldBeEmpty();
        text.ShouldContain("    string Total([CodeChops.Idioma.Internals.ShownValue] decimal amount);");
        foreach (var member in new[] { " Pattern {", " Money(", " Generic", " ShopTranslations(" })
            text.ShouldNotContain(member, customMessage: member);
    }

    [Test]
    public void RecordSetWithTextThatTakesValues_ShouldCompile()
    {
        // Arrange & Act: a record writes methods of its own, among which a ToString without parameters and an Equals
        // that returns no string. Neither is a text, and neither stands in the way of one.
        var errors = GeneratorTestHelper.CompileWithGenerators($$"""
            {{Header}}

            public sealed partial record ShopTranslationsEN : ITranslationSet
            {
                public string Greeting(string name) => $"Hello, {name}!";
            }

            public sealed record ShopTranslationsNL : IShopTranslations
            {
                public string Greeting(string name) => $"Hallo, {name}!";
            }
            """);

        // Assert
        errors.ShouldBeEmpty();
    }
    #endregion
}
