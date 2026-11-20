using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="LocalizationSetsGenerator"/>: a partial <see cref="ILocalizationSet"/> class named after the default
/// language yields its contract, the partial declaration implementing it, and the static class holding the texts, with
/// crefs to every translation; every language of <c>[assembly: SupportedLanguages]</c> must have a set, in its own
/// assembly and in the libraries it references.
/// </summary>
public sealed class LocalizationSetsGeneratorTests
{
    #region Fixture
    private const string Header = """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace TestApp;
        """;

    private const string DutchSet = """
        public sealed class ShopLocalizationNl : CodeChops.Idioma.ILocalizationSet, IShopLocalization
        {
            public string Checkout => "Afrekenen";
            public string Total => "Totaal";
        }
        """;

    private const string ShopSets = $$"""
        {{Header}}

        public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
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

        public class GridLocalizationEn : ILocalizationSet, IGridLocalization
        {
            public virtual string First => "First";
        }

        public sealed class GridLocalizationNl : ILocalizationSet, IGridLocalization
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
    /// Two default-language sets of one name in two namespaces, so two contracts named <c>IShopLocalization</c>, and
    /// neither has a Dutch set yet.
    /// </summary>
    private const string TwoShops = """
        using CodeChops.Idioma;

        [assembly: SupportedLanguages("en", "nl")]

        namespace Shop.Checkout
        {
            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Pay => "Pay";
            }
        }

        namespace Shop.Admin
        {
            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Delete => "Delete";
            }
        }
        """;

    private static GeneratorDriverRunResult Run(string source, params MetadataReference[] references)
    {
        return GeneratorTestHelper.Run(new LocalizationSetsGenerator(), source, references);
    }

    private static string RecordedSets(GeneratorDriverRunResult result)
    {
        var run = result.Results.ShouldHaveSingleItem();
        run.Exception.ShouldBeNull();

        return run.GeneratedSources.Single(source => source.HintName is "LocalizationSets.g.cs").SourceText.ToString();
    }

    private static string ContractText(GeneratorDriverRunResult result)
    {
        var run = result.Results.ShouldHaveSingleItem();
        run.Exception.ShouldBeNull();

        return run.GeneratedSources.Single(source => source.HintName.EndsWith(".Localization.g.cs", StringComparison.Ordinal)).SourceText.ToString();
    }

    /// <summary>
    /// The values the contract records as shown on one of its methods (<c>[ShownValue]</c>), in the order it writes them.
    /// </summary>
    private static string[] ShownValues(string contractText, string method)
    {
        const string prefix = "    [CodeChops.Idioma.Internals.ShownValue(\"";
        const string suffix = "\")]";

        var lines = contractText.Split('\n').Select(static line => line.TrimEnd('\r')).ToArray();
        var index = Array.FindIndex(lines, line => line.StartsWith($"    string {method}(", StringComparison.Ordinal));
        index.ShouldBeGreaterThanOrEqualTo(0, customMessage: method);

        var shown = new List<string>();

        for (var i = index - 1; i >= 0 && lines[i].StartsWith(prefix, StringComparison.Ordinal) && lines[i].EndsWith(suffix, StringComparison.Ordinal); i--)
            shown.Insert(0, lines[i][prefix.Length..^suffix.Length]);

        return [.. shown];
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
    public void Members_ShouldFollowTheOrderTheyAreWrittenIn()
    {
        // Arrange & Act: not alphabetical, so an order by name would fail this.
        var text = ContractText(Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Zeta => "Zeta";
                public string Alpha => "Alpha";
                public string Mid(int count) => $"Mid {count}";
            }
            """));

        // Assert
        text.IndexOf("string Zeta", StringComparison.Ordinal).ShouldBeLessThan(text.IndexOf("string Alpha", StringComparison.Ordinal));
        text.IndexOf("string Alpha", StringComparison.Ordinal).ShouldBeLessThan(text.IndexOf("string Mid", StringComparison.Ordinal));
    }

    [Test]
    public Task DefaultSet_ShouldEmitContractAndTexts()
    {
        // Arrange & Act
        var result = Run(ShopSets);

        // Assert
        var run = result.Results.ShouldHaveSingleItem();
        run.Diagnostics.ShouldBeEmpty();
        run.GeneratedSources.Select(source => source.HintName).ShouldBe(["TestApp.ShopLocalizationEn.Localization.g.cs", "LocalizationSets.g.cs"]);

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
        text.Split('\n').Count(line => line.Contains("/// <inheritdoc cref=\"ShopLocalizationEn.Checkout\"/>", StringComparison.Ordinal)).ShouldBe(2);
        text.ShouldNotContain("/// Checkout");
    }

    [Test]
    public void GeneratedMembers_WithoutDocComment_ShouldBeDocumentedWithDefaultWording()
    {
        // Arrange & Act: Total has no doc comment, so its English wording documents both generated members.
        var text = ContractText(Run(ShopSets));

        // Assert
        text.Split('\n').Count(line => String.Equals(line.Trim(), "/// Total", StringComparison.Ordinal)).ShouldBe(2);
        text.ShouldNotContain("inheritdoc cref=\"ShopLocalizationEn.Total\"");
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
                     ("en", "ShopLocalizationEn", "Checkout", "Checkout"), ("en", "ShopLocalizationEn", "Total", "Total"),
                     ("nl", "ShopLocalizationNl", "Checkout", "Afrekenen"), ("nl", "ShopLocalizationNl", "Total", "Totaal"),
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

            public class AgendaLocalizationEn : ILocalizationSet, IAgendaLocalization
            {
                /// <summary>
                /// The title of an event.
                /// </summary>
                public virtual string @event => "Event";

                public virtual string @class => "Class";
            }

            public class AgendaLocalizationNl : ILocalizationSet, IAgendaLocalization
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
    public void TextWithParametersNamedLikeContextualKeywords_ShouldCompile()
    {
        // Arrange: scoped is a keyword only in front of a parameter, where the generated code lists parameters by name.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public class ScopeLocalizationEn : ILocalizationSet, IScopeLocalization
            {
                public string Scope(string scoped) => $"Scope: {scoped}";
                public string Pair(string scoped, int var) => $"{scoped} {var}";
            }

            public static class Usage
            {
                public static LocalizableText Text { get; } = ScopeLocalization.Scope(scoped: "all");
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
        const string source = $$"""
            {{Header}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                string IShopLocalization.Checkout => "Afrekenen";
            }
            """;

        // Act
        var text = ContractText(Run(source));
        var unresolved = GeneratorTestHelper.GetUnresolvedDocReferences(source);

        // Assert
        text.ShouldContain("<see cref=\"ShopLocalizationNl\">nl</see>: Afrekenen</description>");
        unresolved.ShouldBeEmpty();
    }

    [Test]
    public void SetInheritingText_ShouldBeLinkedAtClassDeclaringIt()
    {
        // Arrange: the text is declared by a base class the set inherits it from. A cref does not find it through the
        // set, which only inherits it (CS1574).
        const string source = $$"""
            {{Header}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            public abstract class ShopWordingBase
            {
                public string Checkout => "Afrekenen";
            }

            public sealed class ShopLocalizationNl : ShopWordingBase, ILocalizationSet, IShopLocalization;
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

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Cart => "Cart";
                public virtual string Checkout => "Checkout";
            }

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public virtual string Cart => "Winkelwagen";
                public virtual string Checkout => "Afrekenen";
            }

            public class ShopLocalizationEnGb : ShopLocalizationEn
            {
                public override string Cart => "Basket";
            }
            """;

        // Act
        var text = ContractText(Run(source));
        var unresolved = GeneratorTestHelper.GetUnresolvedDocReferences(source);

        // Assert: its own wording at the set, and the rest where British users get it from.
        text.ShouldContain("<see cref=\"ShopLocalizationEnGb.Cart\">en-GB</see>: Basket</description>");
        text.ShouldContain("<see cref=\"ShopLocalizationEn.Checkout\">en-GB</see>: Checkout (inherited from <see cref=\"ShopLocalizationEn\"/>)</description>");
        text.ShouldNotContain("ShopLocalizationEnGb.Checkout");
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
                public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
                {
                    public string Checkout => "Checkout";
                }

                public sealed class ShopLocalizationNl : {{baseType}}, ILocalizationSet, IShopLocalization;
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
        const string source = $$"""
            {{Header}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            public abstract class ShopWordingBase
            {
                public string Checkout => "Kassa";
            }

            public sealed class ShopLocalizationNl : ShopWordingBase, ILocalizationSet, IShopLocalization
            {
                string IShopLocalization.Checkout => "Afrekenen";
            }
            """;

        // Act
        var text = ContractText(Run(source));
        var unresolved = GeneratorTestHelper.GetUnresolvedDocReferences(source);

        // Assert
        text.ShouldContain("<see cref=\"ShopLocalizationNl\">nl</see>: Afrekenen</description>");
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

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Address => "Street\nCity";
                public string Terms => "{{longWording}}";
                public string Total => "<b>Total</b> & more";
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
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
        text.ShouldContain("<see cref=\"ShopLocalizationEn.Address\">en</see>: Street…</description>");
        text.ShouldContain($"<see cref=\"ShopLocalizationEn.Terms\">en</see>: {new string('a', 79)}b…</description>");
        text.ShouldContain("<see cref=\"ShopLocalizationEn.Total\">en</see>: &lt;b&gt;Total&lt;/b&gt; &amp; more</description>");
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
                public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
                {
                    public virtual string Checkout => "Checkout";
                    public virtual string Total => "Total";
                }

                public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
                {
                    public string Checkout => "Afrekenen";
                    public string Total => "Totaal";
                }

                [ExcludeFromLocalizationDiscovery]
                public sealed class UnregisteredShopLocalizationNl : ILocalizationSet, IShopLocalization
                {
                    public string Checkout => "Betalen";
                    public string Total => "Totaal";
                }
            }

            namespace Shared.Wording
            {
                public sealed class RewordedShopLocalizationEn : TestApp.ShopLocalizationEn
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
        text.ShouldContain("<see cref=\"ShopLocalizationEn.Checkout\">en</see>: Checkout (<see cref=\"ShopLocalizationEn\"/>)</description>");
        text.ShouldContain("<see cref=\"Namespace_Shared_Wording.RewordedShopLocalizationEn.Checkout\">en</see>: Pay (<see cref=\"Namespace_Shared_Wording.RewordedShopLocalizationEn\"/>)</description>");
        text.ShouldContain("<see cref=\"ShopLocalizationEn.Total\">en</see>: Total (<see cref=\"Namespace_Shared_Wording.RewordedShopLocalizationEn\"/>, inherited from <see cref=\"ShopLocalizationEn\"/>)</description>");
        text.ShouldContain("<see cref=\"ShopLocalizationNl.Checkout\">nl</see>: Afrekenen (<see cref=\"ShopLocalizationNl\"/>)</description>");
        text.ShouldContain("<see cref=\"UnregisteredShopLocalizationNl.Checkout\">nl</see>: Betalen (<see cref=\"UnregisteredShopLocalizationNl\"/>, excluded from discovery)</description>");
        errors.ShouldBeEmpty();
        unresolved.ShouldBeEmpty();
    }

    [Test]
    public void WordingThatIsNoConstantOrEmpty_ShouldLeaveLinkAlone()
    {
        // Arrange
        const string source = $$"""
            {{Header}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
                public string Total => "Total";
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Checkout => System.DateTime.Now.ToString();
                public string Total => "";
            }
            """;

        // Act
        var text = ContractText(Run(source));

        // Assert
        text.ShouldContain("<see cref=\"ShopLocalizationNl.Checkout\">nl</see></description>");
        text.ShouldContain("<see cref=\"ShopLocalizationNl.Total\">nl</see></description>");
    }

    [Test]
    public void DefaultWording_ShouldBeReadFromGettersAndConstantsAndEscapedForXml()
    {
        // Arrange: a getter returning a literal, a const, an initializer, a plain-comment (not doc) property, and markup
        // characters.
        const string source = $$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
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
        text.ShouldContain("/// The <c>Dynamic</c> text of <see cref=\"ShopLocalizationEn\"/>.");
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

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
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
        // Arrange & Act & Assert: the suffix is the language in PascalCase, and counts in any letter case that starts
        // with a capital, which marks where the suffix starts: the "es" of Messages is no part of EsAr.
        LocalizationDiscovery.DeriveBaseName("ShopLocalizationEn", "en").ShouldBe("ShopLocalization");
        LocalizationDiscovery.DeriveBaseName("ShopLocalizationEN", "en").ShouldBe("ShopLocalization");
        LocalizationDiscovery.DeriveBaseName("ShopTextsEn", "en").ShouldBe("ShopTexts");
        LocalizationDiscovery.DeriveBaseName("ShopEn", "en").ShouldBe("Shop");
        LocalizationDiscovery.DeriveBaseName("ShopLocalizationZhHans", "zh-Hans").ShouldBe("ShopLocalization");
        LocalizationDiscovery.DeriveBaseName("ShopLocalizationFIL", "fil").ShouldBe("ShopLocalization");
        LocalizationDiscovery.DeriveBaseName("MessagesEsAr", "es-AR").ShouldBe("Messages");
        LocalizationDiscovery.DeriveBaseName("MessagesEsAR", "es-AR").ShouldBe("Messages");
        LocalizationDiscovery.DeriveBaseName("MessagesAr", "es-AR").ShouldBeNull();
        LocalizationDiscovery.DeriveBaseName("ShopLocalizationfil", "fil").ShouldBeNull();
        LocalizationDiscovery.DeriveBaseName("ShopLocalizationen", "en").ShouldBeNull();
        LocalizationDiscovery.DeriveBaseName("Screen", "en").ShouldBeNull();
        LocalizationDiscovery.DeriveBaseName("ShopLocalization", "en").ShouldBeNull();
        LocalizationDiscovery.DeriveBaseName("En", "en").ShouldBeNull();
    }

    [Test]
    [Arguments("ShopLocalizationEn", "IShopLocalization", "ShopLocalization", "TestApp.ShopLocalization.")]
    [Arguments("ShopTextsEn", "IShopTexts", "ShopTexts", "TestApp.ShopTexts.")]
    [Arguments("LocalizationEn", "ILocalization", "Localization", "TestApp.Localization.")]
    [Arguments("LocalizationSetEn", "ILocalizationSet", "LocalizationSet", "TestApp.LocalizationSet.")]
    public void Names_ShouldDeriveFromClassName(string className, string contract, string texts, string keyPrefix)
    {
        // Arrange & Act: a key starts with the full name of the generated class, as that class is named, a trailing
        // "Localization" included. The last set gets a contract named as the library's own interface is, so each set
        // lists its contract by its full name.
        var text = ContractText(Run($$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public sealed class {{className}} : CodeChops.Idioma.ILocalizationSet, TestApp.{{contract}}
            {
                public string Checkout => "Checkout";
            }
            """));

        // Assert
        text.ShouldContain($"public interface {contract}{Environment.NewLine}");
        text.ShouldContain($"public static partial class {texts}");
        text.ShouldContain($"CreateSetText<{contract}>(\"{keyPrefix}Checkout\",");
    }

    [Test]
    [Arguments("namespace TestApp;", "TestApp.ShopLocalization.Checkout")]
    [Arguments("namespace TestApp.Orders;", "TestApp.Orders.ShopLocalization.Checkout")]
    [Arguments("namespace TestApp.@event;", "TestApp.event.ShopLocalization.Checkout")]
    [Arguments("", "ShopLocalization.Checkout")]
    public void Key_ShouldBeFullNameOfGeneratedText(string @namespace, string key)
    {
        // Arrange & Act: a key is a name and not code, so a namespace part named like a keyword comes without its @,
        // and a set in the global namespace has a key without a namespace.
        var text = ContractText(Run($$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            {{@namespace}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }
            """));

        // Assert
        text.ShouldContain($"CreateSetText<IShopLocalization>(\"{key}\",");
    }

    [Test]
    public void TextsOfOneNameInSetsOfOneName_ShouldGetKeysOfTheirOwn()
    {
        // Arrange & Act: two sets of one name in two namespaces, and a third whose name differs from theirs only in a
        // trailing "Localization", each with a text named Checkout.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp.Orders
            {
                public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
                {
                    public string Checkout => "Place order";
                }

                public sealed class ShopEn : ILocalizationSet, IShop
                {
                    public string Checkout => "To the shop";
                }
            }

            namespace TestApp.Billing
            {
                public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
                {
                    public string Checkout => "Pay invoice";
                }
            }
            """);

        // Assert: a resource file can then provide the wording of one of them, and not of all three at once.
        var generated = result.Results.ShouldHaveSingleItem().GeneratedSources;
        string Contract(string set) => generated.Single(source => String.Equals(source.HintName, $"{set}.Localization.g.cs", StringComparison.Ordinal)).SourceText.ToString();

        Contract("TestApp.Orders.ShopLocalizationEn").ShouldContain("(\"TestApp.Orders.ShopLocalization.Checkout\",");
        Contract("TestApp.Orders.ShopEn").ShouldContain("(\"TestApp.Orders.Shop.Checkout\",");
        Contract("TestApp.Billing.ShopLocalizationEn").ShouldContain("(\"TestApp.Billing.ShopLocalization.Checkout\",");
    }

    [Test]
    public void DefaultLanguageWithLongCode_ShouldWork()
    {
        // Arrange & Act
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("zh-Hans", "en")]

            namespace TestApp;

            public sealed class ShopLocalizationZhHans : ILocalizationSet, IShopLocalization
            {
                string ILocalizationSet.LocalizationLanguageCode => "zh-Hans";
                public string Checkout => "结账";
            }

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
        ContractText(result).ShouldContain("<see cref=\"ShopLocalizationZhHans.Checkout\">zh-Hans</see>");
    }

    [Test]
    public void OnlyPublicInstanceStringPropertiesAreTexts()
    {
        // Arrange: LocalizationLanguageCode is the ILocalizationSet member, a virtual property is a text (so a set can derive and
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

            public class ShopLocalizationEn : ShopWordingBase, ILocalizationSet, IShopLocalization
            {
                public string LocalizationLanguageCode => "en";
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
        foreach (var name in new[] { "LocalizationLanguageCode", "Inherited", "Hidden", "Secret", "Shared", "Count", "Field", "Method" })
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

            internal sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }
            """;

        // Act
        var result = Run(source);

        // Assert: no other assembly could implement it, so it is not recorded for them.
        var text = ContractText(result);
        text.ShouldContain($"internal interface IShopLocalization{Environment.NewLine}");
        text.ShouldContain("internal static partial class ShopLocalization");
        text.ShouldNotContain("public interface");
        text.ShouldNotContain("public static partial class");
        result.Results.ShouldHaveSingleItem().GeneratedSources.ShouldNotContain(source => source.HintName == "LocalizationSets.g.cs");
    }

    [Test]
    public void Contract_ShouldBeMarkedAndNotDeriveFromILocalizationSet()
    {
        // Arrange & Act
        var text = ContractText(Run(ShopSets));

        // Assert: a set lists ILocalizationSet itself, so neither of the two it lists is redundant.
        text.ShouldContain($"[CodeChops.Idioma.Internals.LocalizationContract]{Environment.NewLine}public interface IShopLocalization{Environment.NewLine}");
        text.ShouldNotContain("IShopLocalization :");
        text.ShouldNotContain("partial class ShopLocalizationEn");
    }

    [Test]
    public void RecordSet_ShouldGetContractAndTexts()
    {
        // Arrange
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public sealed record ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }
            """;

        // Act
        var text = ContractText(Run(source));

        // Assert: the record lists its contract itself, so nothing is declared for it.
        text.ShouldContain("public interface IShopLocalization");
        text.ShouldContain("public static partial class ShopLocalization");
        text.ShouldNotContain("partial record");
        GeneratorTestHelper.CompileWithGenerators(source).ShouldBeEmpty();
    }

    [Test]
    public void SetInGlobalNamespace_ShouldEmitNoNamespaceDeclaration()
    {
        // Arrange & Act
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }
            """);

        // Assert
        var generated = result.Results.ShouldHaveSingleItem().GeneratedSources;
        var text = generated.Single(source => source.HintName is "ShopLocalizationEn.Localization.g.cs").SourceText.ToString();
        text.ShouldNotContain("namespace ");
        text.ShouldContain(" DefaultSet<ShopLocalizationEn>.Instance);");
        text.ShouldContain("internal static TSet Instance { get; } = new();");
        generated.Single(source => source.HintName is "LocalizationSets.g.cs").SourceText.ToString().ShouldContain("LocalizationSetAttribute(\"IShopLocalization\", \"en\")");
    }

    [Test]
    public void SetWithoutTexts_ShouldEmitEmptyContractAndTextsClass()
    {
        // Arrange & Act
        var text = ContractText(Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization;
            """));

        // Assert
        text.ShouldContain("public interface IShopLocalization");
        text.ShouldContain("public static partial class ShopLocalization");
        text.ShouldNotContain("CreateSetText<");
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
        // is file-local, so a type of that name elsewhere, with or without a type parameter, can't take its place.
        var errors = GeneratorTestHelper.CompileWithGenerators("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public sealed class OptionLocalizationEn : ILocalizationSet, IOptionLocalization
            {
                public string Default => "Default";
                public string DefaultSet => "Default set";
                public string Instance => "Instance";
                public string KeyPrefix => "Prefix";
            }

            public sealed class OptionLocalizationNl : ILocalizationSet, IOptionLocalization
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
    [Arguments("namespace TestApp;", "CreateSetText")]
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

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string {{text}} => "Text";
                public string Other => "Other";
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
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
        // Idioma.Demo.ShopLocalizationEn is looked for in CodeChops.Idioma, so the registrar names the sets outside it,
        // at the top level of its file. Inside the namespace Shop, a class Shop stands before the namespace, so the file
        // of the contract names the sets of its own namespace without it. A set and an inline text, so that all three
        // generators write their code.
        var source = $$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            {{other}}

            namespace {{@namespace}}
            {
                public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
                {
                    public virtual string Cart => "Cart";
                }

                public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
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
    [Arguments("OptionLocalization")]
    public void TextNamedLikeGeneratedClass_ShouldReportLOC205AndCompileOtherwise(string name)
    {
        // Arrange & Act: the contract keeps the text, so the sets still compile; only the generated text is left out.
        var errors = GeneratorTestHelper.CompileWithGenerators($$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public sealed class OptionLocalizationEn : ILocalizationSet, IOptionLocalization
            {
                public string {{name}} => "Text";
                public string Other => "Other";
            }

            public sealed class OptionLocalizationNl : ILocalizationSet, IOptionLocalization
            {
                public string {{name}} => "Tekst";
                public string Other => "Ander";
            }
            """);

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Id.ShouldBe(LocalizationSetsGenerator.ReservedTextNameDiagnosticId);
        error.Id.ShouldBe("LOC205");
        error.GetMessage().ShouldContain($"'OptionLocalizationEn' has a text named '{name}'");
        error.GetMessage().ShouldContain($"'OptionLocalization.{name}' isn't generated");

        // At the text of the default-language set, which is what to rename.
        error.Location.SourceTree!.GetText().ToString(error.Location.SourceSpan).ShouldBe(name);
        error.Location.GetLineSpan().StartLinePosition.Line.ShouldBe(8);
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

            public sealed class FilterLocalizationEn : ILocalizationSet, IFilterLocalization
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

            public sealed class FilterLocalizationNl : ILocalizationSet, IFilterLocalization
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
        text.ShouldContain("public static new CodeChops.Idioma.LocalizableText Equals { get; }");
        text.ShouldContain("public static CodeChops.Idioma.LocalizableText Contains { get; }");
        warnings.ShouldBeEmpty();
        errors.ShouldBeEmpty();
    }

    [Test]
    public void SetDerivingFromSetOfProjectReferencedAsCompilation_ShouldNotStopTheGenerator()
    {
        // Arrange: an IDE hands a generator a project of the same solution as its compilation, where a build hands over
        // the assembly. The LocalizationLanguageCode the library's set implements then has syntax, in a tree of that other
        // compilation, which this one has no semantic model for.
        var library = GeneratorTestHelper.CompileProjectWithGenerators("RegionalLibrary", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace RegionalLibrary;

            public class GridLocalizationEn : ILocalizationSet, IGridLocalization
            {
                public virtual string Colour => "Color";
            }

            public class GridLocalizationNl : ILocalizationSet, IGridLocalization
            {
                public virtual string Colour => "Kleur";
            }

            public class GridLocalizationEnGb : GridLocalizationEn, ILocalizationSet
            {
                string ILocalizationSet.LocalizationLanguageCode => "en-GB";

                public override string Colour => "Colour";
            }
            """);

        // Act
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public sealed class MyGridLocalizationEnGb : RegionalLibrary.GridLocalizationEnGb
            {
                public override string Colour => "Hue";
            }
            """, library);

        // Assert: as in a build, where the language of such a set can't be read, so nothing is held against it.
        result.Results.ShouldHaveSingleItem().Exception.ShouldBeNull();
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void SetWithoutDeclaredLanguages_ShouldReportLOC100AndEmitNoContract()
    {
        // Arrange & Act
        var result = Run("""
            using CodeChops.Idioma;

            namespace TestApp;

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }
            """);

        // Assert
        var diagnostic = SingleDiagnostic(result, LocalizationSetsGenerator.NoLanguagesDeclaredDiagnosticId);
        diagnostic.Id.ShouldBe("LOC100");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("'ShopLocalizationEn'");
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

            public sealed class MyGridLocalizationEn : GridLibrary.GridLocalizationEn
            {
                public override string First => "Start";
            }

            public sealed class GridLocalizationDe : CodeChops.Idioma.ILocalizationSet, GridLibrary.IGridLocalization
            {
                public string First => "Erste";
            }
            """, GridLibrary);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

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
        var diagnostic = SingleDiagnostic(result, LocalizationSetsGenerator.CannotExtendDiagnosticId);
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
        error.GetMessage().ShouldContain("has a public parameterless constructor");
    }

    [Test]
    [Arguments("public abstract class ShopLocalizationEn : ILocalizationSet { public string Checkout => \"Checkout\"; }")]
    [Arguments("file sealed class ShopLocalizationEn : ILocalizationSet { public string Checkout => \"Checkout\"; }")]
    [Arguments("public static class Container { public sealed class ShopLocalizationEn : ILocalizationSet { public string Checkout => \"Checkout\"; } }")]
    [Arguments("public static partial class Container { [ExcludeFromLocalizationDiscovery] public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization { public string Checkout => \"Checkout\"; } }")]
    [Arguments("public static partial class Container { public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization { public string Checkout => \"Afrekenen\"; } }")]
    [Arguments("[ExcludeFromLocalizationDiscovery] public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization { public string Checkout => \"Checkout\"; }")]
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
        // Arrange & Act: the fix LOC400 names for a set that needs a constructor argument; registering it is up to the
        // app.
        var result = Run($$"""
            {{Header}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            [ExcludeFromLocalizationDiscovery]
            public sealed class ShopLocalizationNl(string checkout) : ILocalizationSet, IShopLocalization
            {
                public string Checkout => checkout;
            }
            """);

        // Assert: no LOC201, the language is recorded for the assemblies referencing this one, and the generated text
        // links to its wording.
        DiagnosticIds(result).ShouldBeEmpty();
        RecordedSets(result).ShouldContain("LocalizationSetAttribute(\"TestApp.IShopLocalization\", \"nl\")");
        ContractText(result).ShouldContain("<see cref=\"ShopLocalizationNl.Checkout\">nl</see>");
    }

    [Test]
    public void ExcludedSetNamedAfterUnlistedLanguage_ShouldNotCountForListedLanguage()
    {
        // Arrange & Act: this set serves be, not the listed nl-BE, so the language stays missing, and LOC202 says what
        // the set serves instead, excluded or not.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl-BE")]

            namespace TestApp;

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            [ExcludeFromLocalizationDiscovery]
            public sealed class ShopLocalizationBe : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBe(["LOC201", "LOC202"]);
    }

    [Test]
    public void ExcludedSetNamedAfterListedLongCode_ShouldCountForThatLanguage()
    {
        // Arrange & Act: registered by hand, it serves nl-BE, as the generated registrar records that code for it too.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl-BE")]

            namespace TestApp;

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            [ExcludeFromLocalizationDiscovery]
            public sealed class ShopLocalizationNlBe : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
        RecordedSets(result).ShouldContain("LocalizationSetAttribute(\"TestApp.IShopLocalization\", \"nl-BE\")");
    }

    [Test]
    [Arguments("public class GiftLocalizationNl : ILocalizationSet, IShopLocalization { public virtual string Checkout => \"Afrekenen\"; public virtual string Total => \"Totaal\"; } [ExcludeFromLocalizationDiscovery] public sealed class GiftLocalizationEn : GiftLocalizationNl;")]
    [Arguments("[ExcludeFromLocalizationDiscovery] public sealed class CartLocalizationNl : ILocalizationSet { public string Checkout => \"Afrekenen\"; }")]
    public void ExcludedSet_ShouldReportNothingAboutItself(string declaration)
    {
        // Arrange & Act: a set deriving from a set of another language (LOC203), and one listing ILocalizationSet in
        // another language than the default (LOC204), were either not excluded. A language not listed is still LOC202.
        var result = Run($"""
            {ShopSets}

            {declaration}
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void LanguageWithoutSet_ShouldReportLOC201AndStillEmit()
    {
        // Arrange & Act
        var result = Run($$"""
            {{Header}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }
            """);

        // Assert: the error names the language and the fix, at the set; the contract still comes, so it stays the only
        // error.
        var diagnostic = SingleDiagnostic(result, LocalizationSetsGenerator.MissingLanguageDiagnosticId);
        diagnostic.Id.ShouldBe("LOC201");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("'ShopLocalization' has no set for 'nl'");
        diagnostic.GetMessage().ShouldContain("Add 'class ShopLocalizationNl : ILocalizationSet, IShopLocalization'.");
        diagnostic.Location.GetLineSpan().StartLinePosition.Line.ShouldBe(6);
        ContractText(result).ShouldContain("public interface IShopLocalization");
    }

    [Test]
    public void LanguageWithLongCodeWithoutSet_ShouldNameSetAfterCode()
    {
        // Arrange & Act
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl-BE")]

            namespace TestApp;

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }
            """);

        // Assert: the name alone gives a set nl-BE, as the assembly declares it.
        var diagnostic = SingleDiagnostic(result, LocalizationSetsGenerator.MissingLanguageDiagnosticId);
        diagnostic.GetMessage().ShouldContain("Add 'class ShopLocalizationNlBe : ILocalizationSet, IShopLocalization'.");
    }

    [Test]
    public void HandWrittenContractMissingLanguage_ShouldReportLOC201AtAttribute()
    {
        // Arrange & Act
        var result = Run($$"""
            {{Header}}

            public interface IGridLocalization : ILocalizationSet
            {
                string First { get; }
            }

            public sealed class GridLocalizationEn : ILocalizationSet, IGridLocalization
            {
                public string First => "First";
            }
            """);

        // Assert
        var diagnostic = SingleDiagnostic(result, LocalizationSetsGenerator.MissingLanguageDiagnosticId);
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("'TestApp.IGridLocalization' has no set for 'nl'");
        diagnostic.GetMessage().ShouldContain("Add 'class GridLocalizationNl : ILocalizationSet, IGridLocalization'.");
        diagnostic.Location.GetLineSpan().StartLinePosition.Line.ShouldBe(2);
    }

    [Test]
    public void HandWrittenContractInEveryLanguage_ShouldReportNothing()
    {
        // Arrange & Act: Dutch through a set of a derived contract, which serves the base contract too.
        var result = Run($$"""
            {{Header}}

            internal interface IGridLocalization : ILocalizationSet
            {
                string First { get; }
            }

            internal interface IPagedGridLocalization : IGridLocalization
            {
                string Next { get; }
            }

            internal sealed class GridLocalizationEn : ILocalizationSet, IPagedGridLocalization
            {
                public string First => "First";
                public string Next => "Next";
            }

            internal sealed class GridLocalizationNl : ILocalizationSet, IPagedGridLocalization
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

            public interface IGridLocalization : ILocalizationSet
            {
                string First { get; }
            }

            public sealed class GridLocalizationEn : ILocalizationSet, IGridLocalization
            {
                public string First => "First";
            }

            [ExcludeFromLocalizationDiscovery]
            public sealed class GridLocalizationNl(string first) : ILocalizationSet, IGridLocalization
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
    public void SetsListingILocalizationSetBesideHandWrittenContract_ShouldReportNothingAndEmitNoContract(string accessibility, string partial)
    {
        // Arrange & Act: listing ILocalizationSet again adds nothing to a set of a contract written by hand. The English set
        // is no default-language set to generate a second contract from (LOC200, or a duplicate IGridLocalization once
        // partial), and the Dutch set does implement its contract (LOC204).
        var result = Run($$"""
            {{Header}}

            {{accessibility}} interface IGridLocalization : ILocalizationSet
            {
                string Next { get; }
            }

            {{accessibility}} {{partial}}class GridLocalizationEn : IGridLocalization, ILocalizationSet
            {
                public string Next => "Next";
            }

            {{accessibility}} sealed class GridLocalizationNl : IGridLocalization, ILocalizationSet
            {
                public string Next => "Volgende";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
        result.Results.ShouldHaveSingleItem().GeneratedSources.ShouldNotContain(source => source.HintName.EndsWith(".Localization.g.cs", StringComparison.Ordinal));
    }

    [Test]
    public void SetsListingILocalizationSetBesideHandWrittenContractWithoutDeclaredLanguages_ShouldReportNothing()
    {
        // Arrange & Act: they start no contract, so they need no languages declared (LOC100).
        var result = Run("""
            using CodeChops.Idioma;

            namespace TestApp;

            public interface IGridLocalization : ILocalizationSet
            {
                string Next { get; }
            }

            public sealed class GridLocalizationEn : IGridLocalization, ILocalizationSet
            {
                public string Next => "Next";
            }

            public sealed class GridLocalizationNl : IGridLocalization, ILocalizationSet
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
        // Arrange & Act: the build can't tell which language the second set serves, so it does not guess.
        var result = Run($$"""
            {{Header}}

            public interface IGridLocalization : ILocalizationSet
            {
                string First { get; }
            }

            public sealed class GridLocalizationEn : ILocalizationSet, IGridLocalization
            {
                public string First => "First";
            }

            public sealed class GridLocalization : ILocalizationSet, IGridLocalization
            {
                private static string Language { get; } = "nl";

                public string LocalizationLanguageCode => Language;
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

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization;
            """);

        // Assert
        DiagnosticIds(result).ShouldBe(["LOC201", "LOC201"]);
    }

    [Test]
    public void ContractsOfOneNameInTwoNamespaces_ShouldEachNeedTheirOwnLanguages()
    {
        // Arrange & Act: the Dutch set implements the checkout's contract, which leaves the admin's without Dutch.
        var result = Run($$"""
            {{TwoShops}}

            namespace Shop.Checkout
            {
                public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
                {
                    public string Pay => "Betalen";
                }
            }
            """);

        // Assert
        SingleDiagnostic(result, LocalizationSetsGenerator.MissingLanguageDiagnosticId);
        var recorded = RecordedSets(result);
        recorded.ShouldContain("LocalizationSetAttribute(\"Shop.Checkout.IShopLocalization\", \"nl\")");
        recorded.ShouldNotContain("LocalizationSetAttribute(\"Shop.Admin.IShopLocalization\", \"nl\")");
        var admin = result.Results.Single().GeneratedSources.Single(source => source.HintName is "Shop.Admin.ShopLocalizationEn.Localization.g.cs");
        admin.SourceText.ToString().ShouldNotContain("ShopLocalizationNl");
    }

    [Test]
    [Arguments("namespace Shop.Checkout;\npublic sealed class ShopLocalizationNl : CodeChops.Idioma.ILocalizationSet, IShopLocalization;", "Shop.Checkout")]
    [Arguments("namespace Shop.Admin.Back;\npublic sealed class ShopLocalizationNl : CodeChops.Idioma.ILocalizationSet, IShopLocalization;", "Shop.Admin")]
    [Arguments("using Shop.Admin;\nnamespace Shop.Checkout;\npublic sealed class ShopLocalizationNl : CodeChops.Idioma.ILocalizationSet, IShopLocalization;", "Shop.Checkout")]
    [Arguments("using Shop.Admin;\nnamespace Other;\npublic sealed class ShopLocalizationNl : CodeChops.Idioma.ILocalizationSet, IShopLocalization;", "Shop.Admin")]
    [Arguments("namespace Other\n{\n    using Shop.Admin;\n    public sealed class ShopLocalizationNl : CodeChops.Idioma.ILocalizationSet, IShopLocalization;\n}", "Shop.Admin")]
    [Arguments("namespace Other;\npublic sealed class ShopLocalizationNl : CodeChops.Idioma.ILocalizationSet, Shop.Admin.IShopLocalization;", "Shop.Admin")]
    [Arguments("namespace Shop;\npublic sealed class ShopLocalizationNl : CodeChops.Idioma.ILocalizationSet, Admin.IShopLocalization;", "Shop.Admin")]
    [Arguments("namespace Other;\npublic sealed class ShopLocalizationNl : CodeChops.Idioma.ILocalizationSet, Shop.Admin.IShopLocalization;", "Shop.Admin")]
    public void SetImplementingGeneratedContract_ShouldCountForContractTheCompilerBinds(string dutchSet, string expectedNamespace)
    {
        // Arrange: the contract does not exist yet while the generator runs; a type of the containing namespaces wins
        // over one a using imports.
        var otherNamespace = expectedNamespace is "Shop.Admin" ? "Shop.Checkout" : "Shop.Admin";

        // Act
        var result = GeneratorTestHelper.RunOverFiles(new LocalizationSetsGenerator(), TwoShops, dutchSet);

        // Assert
        var recorded = RecordedSets(result);
        recorded.ShouldContain($"LocalizationSetAttribute(\"{expectedNamespace}.IShopLocalization\", \"nl\")");
        recorded.ShouldNotContain($"LocalizationSetAttribute(\"{otherNamespace}.IShopLocalization\", \"nl\")");
        DiagnosticIds(result).ShouldBe(["LOC201"]);
    }

    [Test]
    public void SetImplementingGeneratedContractThroughGlobalUsing_ShouldCount()
    {
        // Arrange & Act: the global using lives in a file of its own, as an SDK's implicit usings do.
        var result = GeneratorTestHelper.RunOverFiles(
            new LocalizationSetsGenerator(),
            TwoShops,
            "global using Shop.Admin;",
            "namespace Other;\npublic sealed class ShopLocalizationNl : CodeChops.Idioma.ILocalizationSet, IShopLocalization;");

        // Assert
        var recorded = RecordedSets(result);
        recorded.ShouldContain("LocalizationSetAttribute(\"Shop.Admin.IShopLocalization\", \"nl\")");
        recorded.ShouldNotContain("LocalizationSetAttribute(\"Shop.Checkout.IShopLocalization\", \"nl\")");
    }

    [Test]
    public void TextMissingFromSet_ShouldNotCompile()
    {
        // Arrange & Act
        var errors = GeneratorTestHelper.CompileWithGenerators($$"""
            {{Header}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
                public string Total => "Total";
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
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

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Cart => "Cart";
            }

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public virtual string Cart => "Winkelwagen";
            }
            """);

        // Assert: the set to add derives from the English one, so that it holds only the texts that differ.
        var diagnostic = SingleDiagnostic(result, LocalizationSetsGenerator.MissingLanguageDiagnosticId);
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldBe("'ShopLocalization' has no set for 'en-GB', which [assembly: SupportedLanguages] requires. Add 'class ShopLocalizationEnGb : ShopLocalizationEn', which keeps the wording of 'en' for every text it doesn't override. A class without ILocalizationSet doesn't count as a set, and neither does a nested, generic, or file-local one.");
    }

    [Test]
    [Arguments("public class ShopLocalizationEnGb : ShopLocalizationEn;")]
    [Arguments("public class ShopLocalizationEnGb : ShopLocalizationEn { public override string Cart => \"Basket\"; }")]
    [Arguments("public class ShopLocalizationEnGb : ILocalizationSet, IShopLocalization { public string Cart => \"Basket\"; }")]
    [Arguments("public class ShopLocalizationBritish : ShopLocalizationEn, ILocalizationSet { string ILocalizationSet.LocalizationLanguageCode => \"en-GB\"; }")]
    public void ListedRegionWithSetOfItsOwn_ShouldReportNothing(string british)
    {
        // Arrange & Act: a set that derives from the English one and adds nothing will do, as will one that implements
        // the contract itself, or one that gives its language in LocalizationLanguageCode.
        var result = Run($$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "en-GB")]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Cart => "Cart";
            }

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public virtual string Cart => "Winkelwagen";
            }

            {{british}}
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    [Arguments("\"nl\", \"en\", \"en-GB\"", "public sealed class ShopLocalizationEn", "Add 'class ShopLocalizationEnGb : ILocalizationSet, IShopLocalization'. A class without ILocalizationSet doesn't count as a set, and neither does a nested, generic, or file-local one.")]
    [Arguments("\"nl\", \"en\", \"en-GB\"", "public record ShopLocalizationEn", "Add 'record ShopLocalizationEnGb : ShopLocalizationEn', which keeps the wording of 'en' for every text it doesn't override. A class without ILocalizationSet doesn't count as a set, and neither does a nested, generic, or file-local one.")]
    [Arguments("\"nl\", \"en-GB\"", "public class ShopLocalizationEn", "Add 'class ShopLocalizationEnGb : ShopLocalizationEn', which keeps the wording of 'en' for every text it doesn't override. A class without ILocalizationSet doesn't count as a set, and neither does a nested, generic, or file-local one.")]
    public void ListedRegionWithoutSet_ShouldBeToldWhichSetToAdd(string languages, string english, string advice)
    {
        // Arrange & Act: here the English set is not the default one. A sealed set has nothing deriving from it, a record
        // only a record, and a set of a language that is not listed itself still gives the region its wording.
        var result = Run($$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages({{languages}})]

            namespace TestApp;

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public virtual string Cart => "Winkelwagen";
            }

            {{english}} : ILocalizationSet, IShopLocalization
            {
                public virtual string Cart => "Cart";
            }
            """);

        // Assert
        var message = SingleDiagnostic(result, LocalizationSetsGenerator.MissingLanguageDiagnosticId).GetMessage();
        message.ShouldContain("has no set for 'en-GB'");
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

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Cart => "Cart";
            }

            public class ShopLocalizationZh : ILocalizationSet, IShopLocalization
            {
                public virtual string Cart => "购物车";
            }

            public class ShopLocalizationZhHans : ShopLocalizationZh;
            """);

        // Assert
        var message = SingleDiagnostic(result, LocalizationSetsGenerator.MissingLanguageDiagnosticId).GetMessage();
        message.ShouldEndWith("Add 'class ShopLocalizationZhHansCn : ShopLocalizationZhHans', which keeps the wording of 'zh-Hans' for every text it doesn't override. A class without ILocalizationSet doesn't count as a set, and neither does a nested, generic, or file-local one.");
    }

    [Test]
    public void HandWrittenContractMissingListedRegion_ShouldReportLOC201()
    {
        // Arrange & Act: a contract written by hand is held to its languages as a generated one is.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "en-GB")]

            namespace TestApp;

            public interface IGridLocalization : ILocalizationSet
            {
                string First { get; }
            }

            public class GridLocalizationEn : ILocalizationSet, IGridLocalization
            {
                public virtual string First => "First";
            }

            public class GridLocalizationNl : ILocalizationSet, IGridLocalization
            {
                public virtual string First => "Eerste";
            }
            """);

        // Assert
        var message = SingleDiagnostic(result, LocalizationSetsGenerator.MissingLanguageDiagnosticId).GetMessage();
        message.ShouldContain("'TestApp.IGridLocalization' has no set for 'en-GB'");
        message.ShouldEndWith("Add 'class GridLocalizationEnGb : GridLocalizationEn', which keeps the wording of 'en' for every text it doesn't override. A class without ILocalizationSet doesn't count as a set, and neither does a nested, generic, or file-local one.");
    }

    [Test]
    [Arguments("")]
    [Arguments("[ExcludeFromLocalizationDiscovery]")]
    public void SetForUnlistedLanguage_ShouldReportLOC202(string attribute)
    {
        // Arrange & Act: excluded or not, as an application shows a culture outside its list in its default language,
        // so registering the set by hand would not make it show.
        var result = Run($$"""
            {{ShopSets}}

            {{attribute}}
            public sealed class ShopLocalizationDe : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Zur Kasse";
                public string Total => "Summe";
            }
            """);

        // Assert
        var diagnostic = SingleDiagnostic(result, LocalizationSetsGenerator.UnlistedLanguageDiagnosticId);
        diagnostic.Id.ShouldBe("LOC202");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("'ShopLocalizationDe' is a set for 'de'");
        diagnostic.GetMessage().ShouldContain("Add the language to the attribute, or remove the set.");
    }

    [Test]
    public void ClassDerivingFromTypeOfOtherGenerator_ShouldReportNothing()
    {
        // Arrange & Act: OrderConfigurationBase stands for a type another generator writes, which binds to nothing in
        // the compilation the generators see. The class is no set, so its name ending in 'on' is no language.
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

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            public sealed class ShopLocalizationNlBe : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }

            public sealed class ShopLocalizationZHHANS : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Jiezhang";
            }
            """);

        // Assert: no LOC201 or LOC202, each code recorded for the assemblies referencing this one, and linked.
        DiagnosticIds(result).ShouldBeEmpty();
        RecordedSets(result).ShouldContain("LocalizationSetAttribute(\"TestApp.IShopLocalization\", \"nl-BE\")");
        RecordedSets(result).ShouldContain("LocalizationSetAttribute(\"TestApp.IShopLocalization\", \"zh-Hans\")");
        ContractText(result).ShouldContain("ShopLocalizationNlBe.Checkout\">nl-BE</see>");
    }

    [Test]
    public void SetNamedAfterTwoLetterCode_ShouldNotBeTakenForLongerCodeItsNameHappensToEndIn()
    {
        // Arrange & Act: "MessagesAr" ends in the letters of EsAr, the suffix of es-AR, but not at a capital: its "es"
        // belongs to "Messages", and the set is Arabic. The Argentinian set is the one named MessagesEsAr.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "ar", "es", "es-AR")]

            namespace TestApp;

            public sealed class MessagesEn : ILocalizationSet, IMessages
            {
                public string Save => "Save";
            }

            public sealed class MessagesAr : ILocalizationSet, IMessages
            {
                public string Save => "Hifz";
            }

            public class MessagesEs : ILocalizationSet, IMessages
            {
                public virtual string Save => "Guardar";
            }

            public sealed class MessagesEsAr : MessagesEs;
            """);

        // Assert: no LOC201 asking for the Arabic set that is there, and each set recorded and linked under its own
        // language.
        DiagnosticIds(result).ShouldBeEmpty();
        var recorded = RecordedSets(result);
        recorded.ShouldContain("LocalizationSetAttribute(\"TestApp.IMessages\", \"ar\")");
        recorded.ShouldContain("LocalizationSetAttribute(\"TestApp.IMessages\", \"es-AR\")");
        var contract = ContractText(result);
        contract.ShouldContain("MessagesAr.Save\">ar</see>");
        contract.ShouldContain("MessagesEs.Save\">es-AR</see>");
    }

    [Test]
    public void SetNamedAfterUnlistedLongCode_ShouldReportLOC202NamingBothWays()
    {
        // Arrange & Act: nl-BE is not declared, so the last two letters count, and the set serves 'be'.
        var result = Run($$"""
            {{Header}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            {{DutchSet}}

            public sealed class ShopLocalizationNlBe : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """);

        // Assert: list the code, or implement it.
        var diagnostic = SingleDiagnostic(result, LocalizationSetsGenerator.UnlistedLanguageDiagnosticId);
        diagnostic.GetMessage().ShouldContain("'ShopLocalizationNlBe' is a set for 'be'");
        diagnostic.GetMessage().ShouldContain("If it's meant for 'nl-BE', list 'nl-BE' in the attribute");
        diagnostic.GetMessage().ShouldContain("implement 'string ILocalizationSet.LocalizationLanguageCode => \"nl-BE\";'");
    }

    [Test]
    [Arguments("AdminUiNl")]
    [Arguments("MyAppNl")]
    public void SetWhoseNameEndsInWordAndUnlistedLanguage_ShouldReportLOC202WithoutGuessingLongerCode(string name)
    {
        // Arrange & Act: Ui and App aren't languages the attribute lists, so the name ends in nl alone.
        var result = Run($$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "de")]

            namespace TestApp;

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            public sealed class ShopLocalizationDe : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Zur Kasse";
            }

            public sealed class {{name}} : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """);

        // Assert
        var diagnostic = SingleDiagnostic(result, LocalizationSetsGenerator.UnlistedLanguageDiagnosticId);
        diagnostic.GetMessage().ShouldBe($"'{name}' is a set for 'nl', which [assembly: SupportedLanguages] doesn't list. Add the language to the attribute, or remove the set.");
    }

    [Test]
    public void SetDerivedFromLibrarySetNamedAfterLongCode_ShouldServeThatCode()
    {
        // Arrange: the library declares nl-BE, so its registrar records nl-BE for its set; the app declares only nl.
        var library = GeneratorTestHelper.CompileLibraryWithGenerators("RegionalLibrary", """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl-BE")]

            namespace RegionalLibrary;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            public class ShopLocalizationNlBe : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Afrekenen";
            }
            """);

        // Act: a rewording whose own name the app's languages give nothing longer than 'be'.
        var result = Run($$"""
            {{Header}}

            public sealed class MyShopLocalizationNlBe : RegionalLibrary.ShopLocalizationNlBe
            {
                public override string Checkout => "Betalen";
            }
            """, library);

        // Assert: it inherits nl-BE, which nl serves, as at run time; no LOC202 for 'be'.
        DiagnosticIds(result).ShouldNotContain("LOC202", StringComparer.Ordinal);
        RecordedSets(result).ShouldContain("LocalizationSetAttribute(\"RegionalLibrary.IShopLocalization\", \"nl-BE\")");
    }

    [Test]
    public void DefaultSetNamedAfterLongCode_ShouldGenerateContractThatCompiles()
    {
        // Arrange & Act: a regional default language, with no LocalizationLanguageCode written by hand.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("nl-BE", "en")]

            namespace TestApp;

            public class ShopLocalizationNlBe : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
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

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            public sealed class ShopLocalizationNlBe : ILocalizationSet, IShopLocalization
            {
                string ILocalizationSet.LocalizationLanguageCode => "nl-BE";
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

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }

            public sealed class ShopLocalizationGb : ShopLocalizationEn, ILocalizationSet
            {
                string ILocalizationSet.LocalizationLanguageCode => "en-GB";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void RegionalSetOfNonDefaultLanguage_ShouldBeAllowed()
    {
        // Arrange & Act: the Dutch set implements the contract generated here, so the Belgian set only reaches it through
        // its base. It used to be reported as a set implementing no contract (LOC204).
        var result = Run($$"""
            {{Header}}

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Afrekenen";
            }

            public sealed class ShopLocalizationNlBe : ShopLocalizationNl, ILocalizationSet
            {
                string ILocalizationSet.LocalizationLanguageCode => "nl-BE";
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
        // (LOC201).
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "en-GB")]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Colour => "Color";
            }

            public sealed class ShopLocalizationGb : ShopLocalizationEn, ILocalizationSet
            {
                string ILocalizationSet.LocalizationLanguageCode => "en-GB";
                public override string Colour => "Colour";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
        ContractText(result).ShouldContain("<see cref=\"ShopLocalizationGb.Colour\">en-GB</see>");
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

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Cart => "Cart";
            }

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Cart => "Winkelwagen";
            }

            public sealed class ShopLocalizationEnGb : ShopLocalizationEn
            {
                public override string Cart => "Basket";
            }

            public class GridLocalizationEn : ILocalizationSet, IGridLocalization
            {
                public string Rows => "Rows";
            }

            public class GridLocalizationNl : ILocalizationSet, IGridLocalization
            {
                public string Rows => "Rijen";
            }
            """);

        // Assert: only the contract without a British set is reported, with the set that derives from its English one.
        var message = SingleDiagnostic(result, LocalizationSetsGenerator.MissingLanguageDiagnosticId).GetMessage();
        message.ShouldContain("'GridLocalization' has no set for 'en-GB'");
        message.ShouldEndWith("Add 'class GridLocalizationEnGb : GridLocalizationEn', which keeps the wording of 'en' for every text it doesn't override. A class without ILocalizationSet doesn't count as a set, and neither does a nested, generic, or file-local one.");
    }

    [Test]
    public void SetsDerivedFromAbstractBaseOfGeneratedContract_ShouldCountForTheirLanguage()
    {
        // Arrange & Act: only the abstract base names the contract generated here. The Dutch set used to be missed,
        // so the build asked for the class it already had (LOC201).
        var result = Run($$"""
            {{Header}}

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            public abstract class ShopLocalizationBase : ILocalizationSet, IShopLocalization
            {
                public abstract string Checkout { get; }
            }

            public sealed class ShopLocalizationNl : ShopLocalizationBase
            {
                public override string Checkout => "Afrekenen";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
        ContractText(result).ShouldContain("<see cref=\"ShopLocalizationNl.Checkout\">nl</see>");
    }

    [Test]
    public void SetDerivedFromOtherLanguageOfGeneratedContract_ShouldReportLOC203Only()
    {
        // Arrange & Act: the Dutch set implements the contract generated here, so the German set only reaches it through
        // its base; deriving still leaves its texts in Dutch.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Afrekenen";
            }

            public sealed class ShopLocalizationDe : ShopLocalizationNl
            {
            }
            """);

        // Assert
        SingleDiagnostic(result, LocalizationSetsGenerator.DerivedFromOtherLanguageDiagnosticId);
    }

    [Test]
    public void SetDerivedFromOtherLanguageWithoutDeclaredLanguages_ShouldReportLOC203()
    {
        // Arrange & Act: an app rewording a library needs no languages of its own, and this set's name makes it Dutch.
        // Without a word, it would register under nl, and every text it doesn't override would stay English there.
        var result = Run("""
            namespace TestApp;

            public sealed class MyGridTextsNl : GridLibrary.GridLocalizationEn
            {
                public override string First => "Begin";
            }
            """, GridLibrary);

        // Assert
        var diagnostic = SingleDiagnostic(result, LocalizationSetsGenerator.DerivedFromOtherLanguageDiagnosticId);
        diagnostic.GetMessage().ShouldContain("'MyGridTextsNl' is a set for 'nl' but derives from 'GridLocalizationEn'");
        diagnostic.GetMessage().ShouldContain("To reword 'GridLocalizationEn', give 'MyGridTextsNl' the language");
    }

    [Test]
    public void SetListingILocalizationSetAgainWithoutDeclaredLanguages_ShouldReportNothing()
    {
        // Arrange & Act: listing ILocalizationSet again is how a set deriving from another implements LocalizationLanguageCode, so
        // it does not start a contract that would need languages (LOC100).
        var result = Run("""
            using CodeChops.Idioma;

            namespace TestApp;

            public sealed class MyGridTexts : GridLibrary.GridLocalizationEn, ILocalizationSet
            {
                public string LocalizationLanguageCode => "en";
                public override string First => "Begin";
            }
            """, GridLibrary);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void SetWithLanguageCodeImplementingNothing_ShouldReportNoLanguageErrorOfItsOwn()
    {
        // Arrange & Act: the property implements nothing (LOC401, reported by the registration), and the language checks
        // go by what it says rather than adding LOC202 for the language its name gives (sh).
        var result = Run($$"""
            {{Header}}

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            {{DutchSet}}

            public sealed class ShopLocalizationBritish : ShopLocalizationEn
            {
                public string LocalizationLanguageCode => "en-GB";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void SetDerivedFromOtherLanguage_ShouldReportLOC203Only()
    {
        // Arrange & Act: compiles, with every text it does not override left in English.
        var result = Run($$"""
            {{Header}}

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
                public virtual string Total => "Total";
            }

            public sealed class ShopLocalizationNl : ShopLocalizationEn
            {
                public override string Checkout => "Afrekenen";
            }
            """);

        // Assert: this already says what to do about Dutch, so no LOC201 on top.
        var diagnostic = SingleDiagnostic(result, LocalizationSetsGenerator.DerivedFromOtherLanguageDiagnosticId);
        diagnostic.Id.ShouldBe("LOC203");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("'ShopLocalizationNl' is a set for 'nl' but derives from 'ShopLocalizationEn'");
        diagnostic.GetMessage().ShouldContain("stays in 'en'");
    }

    [Test]
    public void SetDerivedFromOtherLanguageOfLibrary_ShouldReportLOC203()
    {
        // Arrange & Act
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]

            namespace TestApp;

            public sealed class GridLocalizationDe : GridLibrary.GridLocalizationEn;
            """, GridLibrary);

        // Assert
        SingleDiagnostic(result, LocalizationSetsGenerator.DerivedFromOtherLanguageDiagnosticId);
    }

    [Test]
    public void SetRewordingItsOwnLanguage_ShouldBeAllowed()
    {
        // Arrange & Act
        var result = Run($$"""
            {{Header}}

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }

            public sealed class MyShopLocalizationEn : ShopLocalizationEn
            {
                public override string Checkout => "Pay";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
    }

    [Test]
    public void DefaultSetWithoutItsContract_ShouldReportLOC204AndStillGenerateIt()
    {
        // Arrange & Act: the shape a default-language set had while the generator declared its contract for it.
        var result = Run($$"""
            {{Header}}

            public partial class ShopLocalizationEn : ILocalizationSet
            {
                public string Checkout => "Checkout";
                public string Total => "Total";
            }

            {{DutchSet}}
            """);

        // Assert: the other languages keep compiling against the contract meanwhile.
        var diagnostic = SingleDiagnostic(result, LocalizationSetsGenerator.MissingContractDiagnosticId);
        diagnostic.GetMessage().ShouldBe("'ShopLocalizationEn' is a set for 'en' but doesn't implement 'IShopLocalization', so no text reads it. Add 'IShopLocalization', the contract generated from 'ShopLocalizationEn', next to ILocalizationSet.");
        diagnostic.Properties[LocalizationSetsGenerator.SuggestedNameProperty].ShouldBe("IShopLocalization");
        ContractText(result).ShouldContain("public interface IShopLocalization");
        ContractText(result).ShouldNotContain("partial class ShopLocalizationEn");
    }

    [Test]
    public void ClassImplementingContractWithoutILocalizationSet_ShouldNotCountAsSet()
    {
        // Arrange & Act: the shape every other language's set had while the contract derived from ILocalizationSet.
        var result = Run($$"""
            {{Header}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            public sealed class ShopLocalizationNl : IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """);

        // Assert: it isn't the Dutch set, so Dutch is missing, and the message says why the class doesn't count.
        var message = SingleDiagnostic(result, LocalizationSetsGenerator.MissingLanguageDiagnosticId).GetMessage();
        message.ShouldContain("Add 'class ShopLocalizationNl : ILocalizationSet, IShopLocalization'.");
        message.ShouldEndWith("A class without ILocalizationSet doesn't count as a set, and neither does a nested, generic, or file-local one.");
    }

    [Test]
    [Arguments("ShopLocalizationFake")]   // Its name ends in 'ke', a language that isn't listed
    [Arguments("ShopLocalizationScreen")] // Its name ends in 'en', the default language
    public void TestDoubleImplementingContract_ShouldNotCountAsSet(string name)
    {
        // Arrange & Act
        var result = Run($$"""
            {{Header}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }

            public sealed class {{name}} : IShopLocalization
            {
                public string Checkout => "Test";
            }
            """);

        // Assert: no language is read from its name, and neither the texts nor the recorded sets name it.
        DiagnosticIds(result).ShouldBeEmpty();
        ContractText(result).ShouldNotContain(name);
        RecordedSets(result).ShouldNotContain(name);
    }

    [Test]
    public void ClassImplementingLibraryContractWithoutILocalizationSet_ShouldReportLOC600()
    {
        // Arrange & Act: the library's contract is marked, and doesn't derive from ILocalizationSet.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]

            namespace TestApp;

            public sealed class GridLocalizationDe : GridLibrary.IGridLocalization
            {
                public string First => "Erste";
            }
            """, GridLibrary);

        // Assert: it isn't the German set, so the library lacks German.
        SingleDiagnostic(result, LocalizationSetsGenerator.LibraryMissingLanguageDiagnosticId).GetMessage().ShouldContain("Add 'class GridLocalizationDe : ILocalizationSet, GridLibrary.IGridLocalization'.");
    }

    [Test]
    public void SetListingILocalizationSetInOtherLanguage_ShouldReportLOC204Only()
    {
        // Arrange & Act: it compiles, and no text would ever read it.
        var result = Run($$"""
            {{Header}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }

            public sealed class ShopLocalizationNl : ILocalizationSet
            {
                public string Checkout => "Afrekenen";
            }
            """);

        // Assert
        var diagnostic = SingleDiagnostic(result, LocalizationSetsGenerator.MissingContractDiagnosticId);
        diagnostic.Id.ShouldBe("LOC204");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("Add 'IShopLocalization', the contract generated from 'ShopLocalizationEn', next to ILocalizationSet.");
    }

    [Test]
    public void LibraryMissingLanguage_ShouldReportLOC600AtAttribute()
    {
        // Arrange & Act
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]

            namespace TestApp;
            """, GridLibrary);

        // Assert
        var diagnostic = SingleDiagnostic(result, LocalizationSetsGenerator.LibraryMissingLanguageDiagnosticId);
        diagnostic.Id.ShouldBe("LOC600");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("No set serves 'GridLibrary.IGridLocalization' in 'de'");
        diagnostic.GetMessage().ShouldContain("'class GridLocalizationDe : ILocalizationSet, GridLibrary.IGridLocalization'");
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

        // Assert: no LOC600 asking the app for the Dutch set the library has.
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
    public void LibraryWithInlineTextsMissingLanguage_ShouldWarnWithLOC601()
    {
        // Arrange & Act: nothing the app could add, so a warning rather than an error it can't fix.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de")]

            namespace TestApp;
            """, InlineLibrary);

        // Assert
        var diagnostic = SingleDiagnostic(result, LocalizationSetsGenerator.LibraryCannotBeTranslatedDiagnosticId);
        diagnostic.Id.ShouldBe("LOC601");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().ShouldContain("'InlineLibrary' has inline texts without 'de'");
    }

    [Test]
    public void LibraryWithTextsOnMembersMissingLanguage_ShouldWarnWithLOC601()
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
        var diagnostic = SingleDiagnostic(result, LocalizationSetsGenerator.LibraryCannotBeTranslatedDiagnosticId);
        diagnostic.Id.ShouldBe("LOC601");
        diagnostic.GetMessage().ShouldContain("'MemberLibrary' has inline texts without 'de'");
    }

    [Test]
    public void LibraryWithSetsAndInlineTextsMissingLanguage_ShouldReportBoth()
    {
        // Arrange: the app can add German to the contract (LOC600), but not to the inline text (LOC601).
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
        DiagnosticIds(result).ShouldBe(["LOC600", "LOC601"]);
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
        // Arrange: inside namespace Shop the name Shop is the class, so Shop.Dutch.ShopLocalizationNl names nothing
        // there (CS1574), which stops a build that writes a documentation file and treats warnings as errors.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace Shop
            {
                public sealed class Shop;

                public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
                {
                    public virtual string Checkout => "Checkout";
                }
            }

            namespace Shop.Dutch
            {
                public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
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
        text.ShouldContain("<see cref=\"Namespace_Shop_Dutch.ShopLocalizationNl.Checkout\">nl</see>");
        text.ShouldContain("<see cref=\"ShopLocalizationEn.Checkout\">en</see>");
        errors.ShouldBeEmpty();
        unresolved.ShouldBeEmpty();
    }

    [Test]
    public void SetsInOneNamespace_ShouldGetNoAliasButThoseOfTheFramework()
    {
        // Arrange & Act
        var text = ContractText(Run(ShopSets));
        var aliases = text.Split('\n').Where(static line => line.StartsWith("using Namespace_", StringComparison.Ordinal)).Select(static line => line.TrimEnd()).ToArray();

        // Assert: the namespaces of what the method that gives a text by its key names, and none for the sets.
        aliases.ShouldBe(["using Namespace_System = System;", "using Namespace_System_Collections_Generic = System.Collections.Generic;"]);
    }

    [Test]
    public void SetInNamespaceWithPartNamedSystem_ShouldLeaveNoUnresolvedCref()
    {
        // Arrange: inside Acme.System.Shop the name System is Acme.System, where no Globalization lies.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace Acme.System.Shop;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
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

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
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
        // Arrange: what a LocalizationLanguageCode holds while it is being typed. The contract must still be written: without it
        // every IShopLocalization of the project stops compiling.
        var source = $$"""
            {{ShopSets}}

            public class ShopLocalizationBritish : ShopLocalizationEn, ILocalizationSet
            {
                string ILocalizationSet.LocalizationLanguageCode => {{languageCode}};
            }
            """;

        // Act
        var result = Run(source);

        // Assert: nothing about its language from here; LOC402 says what is wrong with the code.
        DiagnosticIds(result).ShouldBeEmpty();
        ContractText(result).ShouldContain("interface IShopLocalization");
        GeneratorTestHelper.GetGeneratorDiagnostics(source).Select(diagnostic => diagnostic.Id).ShouldBe(["LOC402"]);
    }

    [Test]
    [Arguments("\"e\", \"nl\"")]
    [Arguments("\"en_US\", \"nl\"")]
    [Arguments("null, \"nl\"")]
    [Arguments("\"e\"")]
    public void InvalidDefaultLanguage_ShouldNotMakeTheNextLanguageTheDefault(string languages)
    {
        // Arrange: the first language is wrong, which LOC101 reports. With the next one as the default, the English set
        // would be reported as unlisted, no contract would be written, and every use of it would stop compiling.
        var source = ShopSets.Replace("\"en\", \"nl\"", languages, StringComparison.Ordinal);

        // Act
        var result = Run(source);
        var errors = GeneratorTestHelper.CompileWithGenerators(source);

        // Assert: the contract is still written, from the set that starts it, and LOC101 stays the only error.
        DiagnosticIds(result).ShouldBeEmpty();
        ContractText(result).ShouldContain("interface IShopLocalization");
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

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
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
        run.GeneratedSources.Count(generated => generated.HintName.EndsWith(".Localization.g.cs", StringComparison.Ordinal)).ShouldBe(1);
        errors.ShouldHaveSingleItem().Id.ShouldBe("LOC101");
    }

    [Test]
    [Arguments("public required string Checkout { get; init; }", "it has a required member ('Checkout'), which generated code can't set")]
    [Arguments("internal ShopLocalizationEn() { } public string Checkout => \"Checkout\";", "it has no public parameterless constructor")]
    [Arguments("public ShopLocalizationEn(string shop = \"\") { } public string Checkout => \"Checkout\";", "it has no public parameterless constructor")]
    [Arguments("private ShopLocalizationEn() { } public string Checkout => \"Checkout\";", "it has no parameterless constructor")]
    [Arguments("public ShopLocalizationEn(string shop) { } public string Checkout => \"Checkout\";", "it has no parameterless constructor")]
    public void DefaultSetGeneratedCodeCannotCreate_ShouldReportLOC200AsItsOnlyError(string members, string expectedReason)
    {
        // Arrange & Act: the generated texts create the set through DefaultSet<TSet>, which takes a public
        // parameterless constructor (CS0310) and no member left to set (CS9040). With every generator at work.
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
        error.GetMessage().ShouldStartWith($"'OrderStatusEn' is the default-language set of a contract, but its generated class would be named 'OrderStatus', like {expectedType}. Rename the set or that type.");
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
        var diagnostic = SingleDiagnostic(result, LocalizationSetsGenerator.CannotExtendDiagnosticId);
        diagnostic.GetMessage().ShouldStartWith("'ShopLocalizationEn' is the default-language set of a contract, but 'ShopLocalizationEN' is the default-language set of 'IShopLocalization' already, and a contract has only one. The generator can't use it, so it writes no texts for it.");
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).ShouldBe("ShopLocalizationEn");
        diagnostic.Properties.ShouldNotContainKey(LocalizationSetsGenerator.SuggestedNameProperty);
        DiagnosticIds(result).ShouldBe(["LOC200"]);
        ContractText(result).ShouldContain("DefaultSet<ShopLocalizationEN>.Instance");
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
        var diagnostic = SingleDiagnostic(result, LocalizationSetsGenerator.CannotExtendDiagnosticId);
        diagnostic.Properties[LocalizationSetsGenerator.SuggestedNameProperty].ShouldBe(expectedName);
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
        var diagnostic = SingleDiagnostic(result, LocalizationSetsGenerator.CannotExtendDiagnosticId);
        diagnostic.GetMessage().ShouldContain("its name doesn't end in En, the suffix of the default language");
        diagnostic.Properties[LocalizationSetsGenerator.SuggestedNameProperty].ShouldBe(expectedName);
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
        DiagnosticIds(result).Contains(LocalizationSetsGenerator.CannotExtendDiagnosticId, StringComparer.Ordinal).ShouldBeFalse();
    }

    [Test]
    [Arguments("private static readonly string Code = \"nl\"; public string LocalizationLanguageCode => Code;")]
    [Arguments("public string LocalizationLanguageCode => System.Globalization.CultureInfo.GetCultureInfo(\"nl\").Name;")]
    public void SetOfGeneratedContractWithUnreadableLanguage_ShouldNotBeReportedAsMissing(string languageCode)
    {
        // Arrange & Act: the build can't tell which language the set serves, so it may be the Dutch one, and asking
        // for a Dutch set would ask for the set that exists.
        var result = Run($$"""
            {{Header}}

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            public class ShopWording : ILocalizationSet, IShopLocalization
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
        // is what LOC400 advises for a set that takes constructor arguments.
        var result = Run($$"""
            {{Header}}

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            [ExcludeFromLocalizationDiscovery]
            public class ShopWording(string language) : ILocalizationSet, IShopLocalization
            {
                public string LocalizationLanguageCode => language;
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

            public class GridLocalizationEn : ILocalizationSet, IGridLocalization
            {
                public virtual string First => "First";
            }

            public class GridWording : ILocalizationSet, IGridLocalization
            {
                private static readonly string Code = "nl";

                public string LocalizationLanguageCode => Code;
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

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            public class ShopWording : ILocalizationSet, IShopLocalization
            {
                private static readonly string Code = "nl";

                public string LocalizationLanguageCode => Code;
                public virtual string Checkout => "Afrekenen";
            }
            """));

        // Assert: under what is no culture name, so that a build that does not know it takes it for no language.
        recorded.ShouldContain("""LocalizationSetAttribute("TestApp.IShopLocalization", "en")""");
        recorded.ShouldContain("""LocalizationSetAttribute("TestApp.IShopLocalization", "*")""");
    }

    [Test]
    public void SetImplementingLanguageCodeOfAnotherInterface_ShouldServeLanguageOfItsName()
    {
        // Arrange & Act: the explicit implementation is not that of ILocalizationSet.LocalizationLanguageCode, which the set leaves
        // to its name. Taken for it, the set would count as German: unlisted, and the Dutch one missing.
        var result = Run($$"""
            {{Header}}

            public interface IHasCode
            {
                string LocalizationLanguageCode { get; }
            }

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization, IHasCode
            {
                string IHasCode.LocalizationLanguageCode => "de";

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
        // set of its own language, and nothing is left in another one (LOC203).
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "zh-Hant", "zh-TW")]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "Checkout";
            }

            public class ShopLocalizationZhHant : ILocalizationSet, IShopLocalization
            {
                public virtual string Checkout => "結帳";
            }

            public class ShopLocalizationZhTw : ShopLocalizationZhHant;
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

            public class GridLocalizationEn : ILocalizationSet, IGridLocalization
            {
                public virtual string First => "First";
            }

            public class GridLocalizationZhHans : ILocalizationSet, IGridLocalization
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
        var result = GeneratorTestHelper.RunAcrossEdit(new LocalizationSetsGenerator(), ShopSets);

        // Assert
        ContractText(result).ShouldContain("public static partial class ShopLocalization");
    }

    [Test]
    public void UnrelatedEdit_ShouldLeaveGeneratorFullyCached()
    {
        // Arrange & Act: editing a file that declares no set must not re-run the output steps.
        var reasons = GeneratorTestHelper.GetOutputReasonsAfterUnrelatedEdit(new LocalizationSetsGenerator(), ShopSets);

        // Assert
        reasons.ShouldNotBeEmpty();
        reasons.ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }

    [Test]
    public void Discovery_ShouldReadTheTextsOfAContractAndNotWhatTheRegistrarRecords()
    {
        // Arrange & Act: discovery reads every set again on every edit, in any file, so it reads what this generator
        // writes from and leaves the rest to the generator that uses it.
        var discovery = GeneratorTestHelper.GetStepValue<Discovery>(new LocalizationSetsGenerator(), LocalizationDiscovery.TrackingName, """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "en-GB")]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Cart => "Cart";
                public virtual string Greeting(string name) => $"Hello, {name}!";
            }

            public sealed class ShopLocalizationEnGb : ShopLocalizationEn
            {
                public override string Cart => "Basket";
            }
            """);

        // Assert
        var english = discovery.Sets.Single(static set => set.Name is "ShopLocalizationEn");
        var british = discovery.Sets.Single(static set => set.Name is "ShopLocalizationEnGb");
        english.Texts.Select(static text => (text.Name, text.DefaultText)).ShouldBe([("Cart", "Cart"), ("Greeting", "Hello, {name}!")]);
        english.Wordings.Select(static wording => wording.Wording).ShouldBe(["Cart", "Hello, {name}!"]);
        british.Wordings.Select(static wording => wording.Wording).ShouldBe(["Basket", "Hello, {name}!"]);
        british.DeclaredTexts.ShouldBe(["Cart"]);
        british.InheritedTexts.Select(static text => text.Name).ShouldBe(["Greeting"]);
        british.OwnTexts.ShouldBeEmpty();
    }

    [Test]
    public void EditInFileOfSet_ShouldReportInItsNewTreeAndRewriteNoSource()
    {
        // Arrange & Act: a contract without its Dutch set, reported at the default-language set, and an edit in that
        // file which moves nothing.
        var (run, edited) = GeneratorTestHelper.RunAcrossEditInSameFile(new LocalizationSetsGenerator(), $$"""
            {{Header}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
            }
            """);

        // Assert: the diagnostic lies in the tree the file has now, where a pragma of that file applies to it, and the
        // contract and the recorded sets are not written again.
        var diagnostic = run.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("LOC201");
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

        public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
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

        public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
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

        // Assert: the contract repeats the method, which is what holds every language to its parameters, and records
        // on it the values its wording shows. The class creates the text on every call, with the values it is called
        // with.
        text.ShouldContain("    string Greeting(string name);");
        text.ShouldContain("    string ItemsInCart(int count, string shop);");
        ShownValues(text, "Greeting").ShouldBe(["name"]);
        ShownValues(text, "ItemsInCart").ShouldBe(["count", "shop"]);
        text.ShouldContain("LocalizableText Greeting(string name) =>");
        text.ShouldContain("""public static CodeChops.Idioma.LocalizableText Greeting(string name) => CreateSetText<IShopLocalization, string>("TestApp.ShopLocalization.Greeting", static (set, name) => set.Greeting(name), DefaultSet<ShopLocalizationEn>.Instance, name);""");
        text.ShouldContain("""public static CodeChops.Idioma.LocalizableText ItemsInCart(int count, string shop) => CreateSetText<IShopLocalization, int, string>("TestApp.ShopLocalization.ItemsInCart", static (set, count, shop) => set.ItemsInCart(count, shop), DefaultSet<ShopLocalizationEn>.Instance, count, shop);""");
    }

    [Test]
    public void TextThatTakesValues_ShouldBeDocumentedLikeAnyText()
    {
        // Arrange & Act
        var text = ContractText(Run(GreetingSets));

        // Assert: the doc comment of the method is inherited, parameters included, and the wording of each language
        // stands next to its link with each value in braces. A method that chooses between wordings shows none.
        text.Split('\n').Count(line => line.Contains("/// <inheritdoc cref=\"ShopLocalizationEn.Greeting\"/>", StringComparison.Ordinal)).ShouldBe(2);
        text.Split('\n').Count(line => line.Contains("<see cref=\"ShopLocalizationEn.Greeting\">en</see>: Hello, {name}!</description>", StringComparison.Ordinal)).ShouldBe(2);
        text.Split('\n').Count(line => line.Contains("<see cref=\"ShopLocalizationNl.Greeting\">nl</see>: Hallo, {name}!</description>", StringComparison.Ordinal)).ShouldBe(2);
        text.Split('\n').Count(line => line.Contains("/// The <c>ItemsInCart</c> text of <see cref=\"ShopLocalizationEn\"/>.", StringComparison.Ordinal)).ShouldBe(2);
        text.Split('\n').Count(line => line.Contains("<see cref=\"ShopLocalizationNl.ItemsInCart\">nl</see></description>", StringComparison.Ordinal)).ShouldBe(2);
    }

    [Test]
    public void OnlyPublicInstanceMethodsReturningStringWithParametersAreTexts()
    {
        // Arrange: a virtual method is a text (so a set can derive and override), an inherited method belongs to the
        // base (it is not redeclared), and the rest are not texts by shape.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public class ShopWordingBase
            {
                public string Inherited(string name) => name;
            }

            public class ShopLocalizationEn : ShopWordingBase, ILocalizationSet, IShopLocalization
            {
                public string Greeting(string name) => $"Hello, {name}!";
                public virtual string Farewell(string name) => $"Goodbye, {name}!";
                public string LocalizationLanguageCode(int unused) => "en";
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
        text.ShouldContain("    string Greeting(string name);");
        text.ShouldContain("    string Farewell(string name);");
        ShownValues(text, "Greeting").ShouldBe(["name"]);
        ShownValues(text, "Farewell").ShouldBe(["name"]);
        foreach (var name in new[] { "Inherited", "LocalizationLanguageCode", "Hidden", "Secret", "Shared", "Count", "Nothing", "Reference" })
            text.ShouldNotContain($" {name}(", customMessage: name);
    }

    [Test]
    [Arguments("public string Greeting<T>(T name) => $\"Hello, {name}!\";", "it's generic")]
    [Arguments("public string Greeting(string name) => name; private string Greeting(string name, bool formal) => name;", "its set has another method of that name")]
    [Arguments("public string Greeting(ref string name) => name;", "its parameter 'name' is passed by reference")]
    [Arguments("public string Greeting(in int count) => \"\";", "its parameter 'count' is passed by reference")]
    [Arguments("public string Greeting(out string name) => name = \"\";", "its parameter 'name' is passed by reference")]
    [Arguments("public string Greeting(params string[] names) => \"\";", "its parameter 'names' is a params parameter")]
    [Arguments("public string Greeting(string name = \"you\") => name;", "its parameter 'name' has a default value")]
    [Arguments("public string Greeting(System.ReadOnlySpan<char> name) => name.ToString();", "its parameter 'name' is of a type a text can't keep")]
    [Arguments("public unsafe string Greeting(int* count) => \"\";", "its parameter 'count' is of a type a text can't keep")]
    [Arguments("public string Greeting(dynamic name) => \"\";", "its parameter 'name' is dynamic")]
    [Arguments("public string Greeting(int a, int b, int c, int d, int e, int f, int g, int h, int i) => \"\";", "it takes 9 values, and a text takes at most 8")]
    public void MethodThatCannotBeAText_ShouldReportLOC300AtItAndEmitNoTextForIt(string method, string expectedReason)
    {
        // Arrange
        var source = $$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
                {{method}}
            }
            """;

        // Act
        var result = Run(source);

        // Assert: at the method, which is what to change. The other texts are generated all the same.
        var diagnostic = SingleDiagnostic(result, LocalizationSetsGenerator.UnsupportedTextDiagnosticId);
        diagnostic.Id.ShouldBe("LOC300");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("'ShopLocalizationEn.Greeting' is a public method that returns a string");
        diagnostic.GetMessage().ShouldContain(expectedReason);
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).ShouldBe("Greeting");

        var text = ContractText(result);
        text.ShouldContain("    string Checkout { get; }");
        text.ShouldNotContain("Greeting");
    }

    [Test]
    public void TextWrittenAsMethodWithoutParameters_ShouldReportLOC300SayingToWriteAProperty()
    {
        // Arrange: a text without values is a property. Written as a method it is no text, and what calls it as one
        // would only be told that nothing has its name (CS0117). The Dutch set starts no contract, so its method is
        // for the compiler.
        const string source = $$"""
            {{Header}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
                public string Greeting() => "Hello";
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
                public string Greeting() => "Hallo";
            }
            """;

        // Act
        var result = Run(source);
        var errors = GeneratorTestHelper.CompileWithGenerators(source);

        // Assert: at the method, and the other texts are generated all the same.
        var diagnostic = SingleDiagnostic(result, LocalizationSetsGenerator.UnsupportedTextDiagnosticId);
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldBe("'ShopLocalizationEn.Greeting' is a public method that returns a string, which makes it a text with parameters, but it takes no parameters, so write it as a property. No text is generated for it. Fix the method. If it isn't a text, make it internal or private, or mark it [ExcludeFromLocalizationDiscovery].");
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).ShouldBe("Greeting");
        DiagnosticIds(result).ShouldBe(["LOC300"]);
        ContractText(result).ShouldContain("    string Checkout { get; }");
        ContractText(result).ShouldNotContain("Greeting");
        errors.Select(static error => error.Id).ShouldBe(["LOC300"]);
    }

    [Test]
    [Arguments("", "public override string ToString() => \"Shop\";")]
    [Arguments("", "public new string ToString() => \"Shop\";")]
    [Arguments("", "public new string Label() => \"Shop\";")]
    [Arguments("", "[ExcludeFromLocalizationDiscovery] public string Describe() => \"Shop\";")]
    [Arguments(", IDescribed", "public string Describe() => \"Shop\";")]
    [Arguments(", IDescribed", "string IDescribed.Describe() => \"Shop\";")]
    [Arguments("", "public static string Describe() => \"Shop\";")]
    [Arguments("", "internal string Describe() => \"Shop\";")]
    [Arguments("", "public string Describe<T>() => typeof(T).Name;")]
    [Arguments("", "public object Describe() => \"Shop\";")]
    public void MethodWithoutParametersThatPlainlyIsNoText_ShouldReportNothing(string baseType, string method)
    {
        // Arrange: an override, a method that hides one of a base class, a member marked as no text, the
        // implementation of an interface, and a method that is no text by its shape.
        var source = $$"""
            {{Header}}

            public interface IDescribed
            {
                string Describe();
            }

            public class Labelled
            {
                public string Label() => "Label";
            }

            public sealed class ShopLocalizationEn : Labelled, ILocalizationSet, IShopLocalization{{baseType}}
            {
                public string Checkout => "Checkout";
                {{method}}
            }

            {{DutchSet.Replace("public string Total => \"Totaal\";", "", StringComparison.Ordinal)}}
            """;

        // Act
        var result = Run(source);
        var errors = GeneratorTestHelper.CompileWithGenerators(source);

        // Assert
        DiagnosticIds(result).ShouldBeEmpty();
        errors.ShouldBeEmpty();
        ContractText(result).ShouldNotContain("Describe");
    }

    [Test]
    public void TwoPublicMethodsOfOneName_ShouldEachReportLOC300()
    {
        // Arrange & Act: a text is told from the others by its name, which is its key, so neither can be one.
        var result = Run($$"""
            {{Header}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Greeting(string name) => $"Hello, {name}!";
                public string Greeting(string name, string title) => $"Hello, {title} {name}!";
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization;
            """);

        // Assert
        DiagnosticIds(result).ShouldBe(["LOC300", "LOC300"]);
        ContractText(result).ShouldNotContain("Greeting");
    }

    [Test]
    public void MethodThatCannotBeAText_ShouldBeNoPartOfTheContract()
    {
        // Arrange & Act: LOC300 is the only error, so the Dutch set, which has no such method, implements the contract.
        var errors = GeneratorTestHelper.CompileWithGenerators($$"""
            {{Header}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
                public string Greeting(string name = "you") => $"Hello, {name}!";
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Afrekenen";
            }
            """);

        // Assert
        errors.ShouldHaveSingleItem().Id.ShouldBe("LOC300");
    }

    [Test]
    public void SetWithoutTextThatTakesValues_ShouldNotCompile()
    {
        // Arrange & Act: the contract demands the method of every language, as it demands every property.
        var errors = GeneratorTestHelper.CompileWithGenerators($$"""
            {{Header}}

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Greeting(string name) => $"Hello, {name}!";
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization;
            """);

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Id.ShouldBe("CS0535");
        error.GetMessage().ShouldContain("IShopLocalization.Greeting(string)");
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

        const string source = $$"""
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

                public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
                {
                    {{methods}}
                }

                public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
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
        text.ShouldContain("LocalizableText @event(string @class, int @int) => CreateSetText<IShopLocalization, string, int>(\"TestApp.ShopLocalization.event\", static (set, @class, @int) => set.@event(@class, @int), DefaultSet<ShopLocalizationEn>.Instance, @class, @int);");
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

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Greeting(string name, string[] titles) => $"Hello, {name}!";
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
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
        // Arrange: parameters named like the set the generated method names itself, like the helpers it calls, and
        // like another text; and texts named like those helpers and like the namespace of a parameter's type.
        const string methods = """
            public string Paired(string set, string set_) => set + set_;
            public string Valued(string values, int values_) => values + values_;
            public string CreateSetText(string DefaultSet, string Instance) => DefaultSet + Instance;
            public string DefaultSet(string CreateSetText) => CreateSetText;
            public string Name => "Name";
            public string Named(string Name) => Name;
            public string System(DateOnly date) => $"{date}";
            public string CodeChops(string value) => value;
            """;

        const string source = $$"""
            using System;
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public sealed class OptionLocalizationEn : ILocalizationSet, IOptionLocalization
            {
                {{methods}}
            }

            public sealed class OptionLocalizationNl : ILocalizationSet, IOptionLocalization
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
        text.ShouldContain("static (set__, set, set_) => set__.Paired(set, set_)");
        text.ShouldContain("static (set, values, values_) => set.Valued(values, values_)");
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

                public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
                {
                    public string Dear({{parameterType}} value) => $"Dear {value}";
                }

                public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
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
    [Arguments("using MyApp.Domain;", "", "OrderId")]
    [Arguments("using MyApp.Domain;", "", "OrderId?")]
    [Arguments("using System.Collections.Generic;\nusing MyApp.Domain;", "", "IReadOnlyList<OrderId>")]
    [Arguments("", "using Domain;", "OrderId")]
    [Arguments("using Id = MyApp.Domain.OrderId;", "", "Id")]
    [Arguments("using static MyApp.Domain.Ids;", "", "CustomerId")]
    [Arguments("", "", "MyApp.Domain.OrderId")]
    [Arguments("", "", "LocalId")]
    public void ParameterOfTypeAnotherGeneratorWrites_ShouldCompile(string usingsOfFile, string usingsOfNamespace, string parameterType)
    {
        // Arrange: a type another source generator writes binds to nothing while this one runs, so the contract names
        // it by the words the set writes it in. Those name it only next to the using directives of the set.
        var source = $$"""
            {{usingsOfFile}}
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace MyApp.Localization
            {
                {{usingsOfNamespace}}

                public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
                {
                    public string Shipped({{parameterType}} id) => $"Order {id} is on its way";
                }

                public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
                {
                    public string Shipped({{parameterType}} id) => $"Bestelling {id} is onderweg";
                }
            }
            """;

        const string written = """
            namespace MyApp.Domain
            {
                public readonly record struct OrderId(int Value);

                public static partial class Ids
                {
                    public readonly record struct CustomerId(int Value);
                }
            }

            namespace MyApp.Localization
            {
                public readonly record struct LocalId(int Value);
            }
            """;

        // Act
        var errors = GeneratorTestHelper.CompileWithGeneratorsNextToAnother(source, written);

        // Assert
        errors.ShouldBeEmpty();
    }

    [Test]
    public void UsingDirectivesOfSet_ShouldBeRepeatedOnlyForTypeThatBindsToNothing()
    {
        // Arrange: Money binds, so the contract names it by its own name and needs no directive of the set. OrderId is
        // written by another generator.
        const string source = """
            using System.Text;
            using MyApp.Domain;
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace MyApp.Domain
            {
                public readonly record struct Money(decimal Amount);
            }

            namespace MyApp.Localization
            {
                public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
                {
                    public string Total({{type}} value) => $"Total: {value}";
                }
            }
            """;

        // Act
        var bound = ContractText(Run(source.Replace("{{type}}", "Money", StringComparison.Ordinal)));
        var unbound = ContractText(Run(source.Replace("{{type}}", "OrderId", StringComparison.Ordinal)));

        // Assert
        bound.ShouldNotContain("using MyApp.Domain;");
        bound.ShouldNotContain("using System.Text;");
        bound.ShouldContain("string Total(Namespace_MyApp_Domain.Money value);");
        unbound.ShouldContain("using System.Text;");
        unbound.ShouldContain("using CodeChops.Idioma;");
        unbound.IndexOf("using MyApp.Domain;", StringComparison.Ordinal).ShouldBeInRange(0, unbound.IndexOf("namespace MyApp.Localization;", StringComparison.Ordinal));
        unbound.ShouldContain("string Total(OrderId value);");
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

        const string source = $$"""
            #nullable enable
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public sealed class FilterLocalizationEn : ILocalizationSet, IFilterLocalization
            {
                {{methods}}
            }

            public sealed class FilterLocalizationNl : ILocalizationSet, IFilterLocalization
            {
                {{methods}}
            }
            """;

        // Act
        var text = ContractText(Run(source));
        var warnings = GeneratorTestHelper.GetWarningsWithGenerators(source);
        var errors = GeneratorTestHelper.CompileWithGenerators(source);

        // Assert
        text.ShouldContain("public static new CodeChops.Idioma.LocalizableText Equals(object? other)");
        text.ShouldContain("public static new CodeChops.Idioma.LocalizableText ReferenceEquals(object? left, object? right)");
        text.ShouldContain("public static CodeChops.Idioma.LocalizableText ToString(string format)");
        text.ShouldContain("public static CodeChops.Idioma.LocalizableText GetHashCode(int seed)");
        warnings.ShouldBeEmpty();
        errors.ShouldBeEmpty();
    }

    [Test]
    public void TextThatTakesValuesNamedLikeGeneratedClass_ShouldReportLOC205AndCompileOtherwise()
    {
        // Arrange & Act: the contract keeps the text, so the sets still compile; only the generated method is left out.
        var errors = GeneratorTestHelper.CompileWithGenerators("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public sealed class OptionLocalizationEn : ILocalizationSet, IOptionLocalization
            {
                public string OptionLocalization(string name) => $"Options of {name}";
            }

            public sealed class OptionLocalizationNl : ILocalizationSet, IOptionLocalization
            {
                public string OptionLocalization(string name) => $"Opties van {name}";
            }
            """);

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Id.ShouldBe("LOC205");
        error.GetMessage().ShouldContain("'OptionLocalizationEn' has a text named 'OptionLocalization'");
    }

    [Test]
    public void SetOfOnlyTextsThatTakeValues_ShouldShowOneWithItsValuesLeftOutAsItsExample()
    {
        // Arrange & Act
        var text = ContractText(Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Greeting(string name) => $"Hello, {name}!";
            }
            """));

        // Assert: in markup a text that takes values does not stand by its name alone.
        text.ShouldContain("<c>@ShopLocalization.Greeting(...)</c>");
    }

    [Test]
    public void TextThatTakesValuesInheritedOrImplementedExplicitly_ShouldBeLinkedWithResolvedCref()
    {
        // Arrange: a British set that leaves the text as the English set words it, which a cref names on the English
        // set, a Dutch set that implements it explicitly, which has no name to link to, and a German set with a helper
        // of the text's name, which a cref can't tell from the text.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl", "de", "en-GB")]

            namespace TestApp;

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public virtual string Greeting(string name) => $"Hello, {name}!";
            }

            public sealed class ShopLocalizationEnGb : ShopLocalizationEn;

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                string IShopLocalization.Greeting(string name) => $"Hallo, {name}!";
            }

            public sealed class ShopLocalizationDe : ILocalizationSet, IShopLocalization
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
        text.ShouldContain("<see cref=\"ShopLocalizationEn.Greeting\">en-GB</see>: Hello, {name}! (inherited from <see cref=\"ShopLocalizationEn\"/>)</description>");
        text.ShouldContain("<see cref=\"ShopLocalizationNl\">nl</see>: Hallo, {name}!</description>");
        text.ShouldContain("<see cref=\"ShopLocalizationDe\">de</see></description>");
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

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
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
        text.ShouldContain("<see cref=\"ShopLocalizationEn.Total\">en</see>: Total: {amount:N2}</description>");
        text.ShouldContain("<see cref=\"ShopLocalizationEn.Greeting\">en</see>: Hello, {name}!</description>");
        text.ShouldContain("<see cref=\"ShopLocalizationEn.Raw\">en</see>: Hello, {name}, and {braces}!</description>");
        text.ShouldContain("<see cref=\"ShopLocalizationEn.Braces\">en</see>: {{name}} &amp; &lt;b&gt;</description>");
        text.ShouldContain("<see cref=\"ShopLocalizationEn.Constant\">en</see>: Hello</description>");
        text.ShouldContain("<see cref=\"ShopLocalizationEn.Chosen\">en</see></description>");
        text.ShouldContain("<see cref=\"ShopLocalizationEn.Statements\">en</see></description>");
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

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
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

        // Assert: on the method of the contract, which is what another language implements, and not on its parameters,
        // which an IDE copies into the set it writes. Nor on the method of the class.
        ShownValues(text, "Shown").ShouldBe(["name"]);
        ShownValues(text, "ShownInOneWording").ShouldBe(["count"]);
        ShownValues(text, "HandedOn").ShouldBe(["name"]);
        ShownValues(text, "InStatements").ShouldBe(["name"]);
        ShownValues(text, "InCondition").ShouldBeEmpty();
        ShownValues(text, "InSwitch").ShouldBeEmpty();
        ShownValues(text, "InPattern").ShouldBeEmpty();
        ShownValues(text, "InIf").ShouldBeEmpty();
        ShownValues(text, "InComparison").ShouldBeEmpty();
        ShownValues(text, "Unused").ShouldBeEmpty();
        ShownValues(text, "Shadowed").ShouldBeEmpty();
        ShownValues(text, "Mixed").ShouldBe(["name"]);
        text.ShouldContain("    string Mixed(Salutation salutation, string name);");
        text.ShouldContain("LocalizableText Shown(string name) =>");
        text.ShouldNotContain("ShownValue] ");
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

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Total(decimal amount) => $"Total: {Money(amount)}";

                [ExcludeFromLocalizationDiscovery]
                public string Pattern => "C";

                [ExcludeFromLocalizationDiscovery]
                public string Money(decimal amount) => amount.ToString(Pattern);

                [ExcludeFromLocalizationDiscovery]
                public string Generic<T>(T value) => $"{value}";

                [ExcludeFromLocalizationDiscovery]
                public string ShopLocalization(string value) => value;
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
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
        text.ShouldContain("    string Total(decimal amount);");
        ShownValues(text, "Total").ShouldBe(["amount"]);
        foreach (var member in new[] { " Pattern {", " Money(", " Generic", " ShopLocalization(" })
            text.ShouldNotContain(member, customMessage: member);
    }

    [Test]
    [Arguments("[ExcludeFromLocalizationDiscovery]", false)]
    [Arguments("", true)]
    public void OverrideOfPublicMemberOfBaseClass_ShouldBeTextUnlessMarkedAsExcluded(string attribute, bool isText)
    {
        // Arrange: an override can't be private or static, so only the attribute keeps it from being a text. The Dutch
        // set doesn't implement it.
        var source = $$"""
            {{Header}}

            public abstract class PriceFormatter
            {
                public abstract string Money(decimal amount);
            }

            public sealed class ShopLocalizationEn : PriceFormatter, ILocalizationSet, IShopLocalization
            {
                public string Total(decimal amount) => $"Total: {Money(amount)}";

                {{attribute}}
                public override string Money(decimal amount) => amount.ToString("C");
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Total(decimal amount) => $"Totaal: {amount:C}";
            }
            """;

        // Act
        var result = Run(source);
        var text = ContractText(result);
        var errors = GeneratorTestHelper.CompileWithGenerators(source);

        // Assert: unmarked, it's a text of the contract, so the Dutch set doesn't compile without it.
        string[] expectedErrors = isText ? ["CS0535"] : [];
        DiagnosticIds(result).ShouldBeEmpty();
        text.Contains("    string Money(decimal amount);", StringComparison.Ordinal).ShouldBe(isText);
        errors.Select(static error => error.Id).ShouldBe(expectedErrors);
    }

    [Test]
    public void RecordSetWithTextThatTakesValues_ShouldCompile()
    {
        // Arrange & Act: a record writes methods of its own, among which a ToString without parameters and an Equals
        // that returns no string. Neither is a text, and neither stands in the way of one.
        var errors = GeneratorTestHelper.CompileWithGenerators($$"""
            {{Header}}

            public sealed record ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Greeting(string name) => $"Hello, {name}!";
            }

            public sealed record ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Greeting(string name) => $"Hallo, {name}!";
            }
            """);

        // Assert
        errors.ShouldBeEmpty();
    }
    #endregion

    #region Texts of an enum
    /// <summary>
    /// An enum with a set per language that holds a text for each of its members.
    /// </summary>
    private const string OrderStatusSets = $$"""
        {{Header}}

        public enum OrderStatus
        {
            Pending,
            Shipped,
        }

        public sealed class OrderStatusLocalizationEn : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
        {
            public string Pending => "Pending";

            /// <summary>
            /// The order is on its way.
            /// </summary>
            public string Shipped => "Shipped";
        }

        public sealed class OrderStatusLocalizationNl : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
        {
            public string Pending => "In behandeling";
            public string Shipped => "Verzonden";
        }
        """;

    [Test]
    public Task SetOfEnum_ShouldEmitTextPerMemberAndMethodThatGivesTextOfValue()
    {
        // Arrange & Act
        var result = Run(OrderStatusSets);

        // Assert
        var run = result.Results.ShouldHaveSingleItem();
        run.Diagnostics.ShouldBeEmpty();

        return Verify(ContractText(result));
    }

    [Test]
    public void SetOfEnum_ShouldCompileWithoutWarningsAndWithResolvedCrefs()
    {
        // Arrange & Act
        var errors = GeneratorTestHelper.CompileWithGenerators(OrderStatusSets);
        var warnings = GeneratorTestHelper.GetWarningsWithGenerators(OrderStatusSets);
        var unresolved = GeneratorTestHelper.GetUnresolvedDocReferences(OrderStatusSets);

        // Assert
        errors.ShouldBeEmpty();
        warnings.ShouldBeEmpty();
        unresolved.ShouldBeEmpty();
    }

    [Test]
    [Arguments("public sealed class OrderStatusLocalizationNl : ILocalizationSet<OrderStatus>, IOrderStatusLocalization")]
    [Arguments("public sealed class OrderStatusLocalizationNl : ILocalizationSet, IOrderStatusLocalization")]
    public void EnumMemberWithoutText_ShouldNotCompileInAnyLanguage(string dutchSet)
    {
        // Arrange & Act: neither set has a text for Shipped. The contract comes from the enum's members, so it asks
        // both for it, whichever of the two interfaces the Dutch set lists.
        var source = $$"""
            {{Header}}

            public enum OrderStatus { Pending, Shipped }

            public sealed class OrderStatusLocalizationEn : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
            {
                public string Pending => "Pending";
            }

            {{dutchSet}}
            {
                public string Pending => "In behandeling";
            }
            """;

        var result = Run(source);
        var errors = GeneratorTestHelper.CompileWithGenerators(source);

        // Assert: after the texts that are written, and without a word from the generator.
        DiagnosticIds(result).ShouldBeEmpty();
        ContractText(result).ShouldContain("    string Shipped { get; }");
        errors.Select(static error => error.Id).ShouldBe(["CS0535", "CS0535"]);
        errors.ShouldAllBe(static error => error.GetMessage().Contains("IOrderStatusLocalization.Shipped"));
    }

    [Test]
    public void SetOfEnum_ShouldKeepItsTextsInTheOrderTheyAreWrittenAndAddTheRestInTheOrderOfTheEnum()
    {
        // Arrange & Act: a text that is no member, a member, and two members the set has no text for.
        var text = ContractText(Run($$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public enum OrderStatus { Pending, Shipped, Delivered }

            public sealed class OrderStatusLocalizationEn : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
            {
                public string Caption => "Order status";
                public string Delivered => "Delivered";
            }
            """));

        // Assert
        string[] members = ["string Caption { get; }", "string Delivered { get; }", "string Pending { get; }", "string Shipped { get; }"];
        members.Select(member => text.IndexOf(member, StringComparison.Ordinal)).ShouldBeInOrder();
        text.ShouldContain("public static CodeChops.Idioma.LocalizableText Caption { get; }");
        text.ShouldContain("\"TestApp.OrderStatusLocalization.Shipped\"");
    }

    [Test]
    public void EnumMembersOfOneValue_ShouldEachHaveATextAndTheFirstGiveTheTextOfTheValue()
    {
        // Arrange
        var source = $$"""
            {{Header}}

            public enum OrderStatus { Pending, Waiting = Pending, Shipped }

            public sealed class OrderStatusLocalizationEn : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
            {
                public string Pending => "Pending";
                public string Waiting => "Waiting";
                public string Shipped => "Shipped";
            }

            public sealed class OrderStatusLocalizationNl : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
            {
                public string Pending => "In behandeling";
                public string Waiting => "Wachtend";
                public string Shipped => "Verzonden";
            }
            """;

        // Act
        var text = ContractText(Run(source));
        var errors = GeneratorTestHelper.CompileWithGenerators(source);

        // Assert: a switch takes a value once.
        errors.ShouldBeEmpty();
        text.ShouldContain("    string Waiting { get; }");
        text.ShouldContain("        OrderStatus.Pending => OrderStatusLocalization.Pending,");
        text.ShouldNotContain("OrderStatus.Waiting =>");
    }

    [Test]
    public void SetOfEnumOfAnotherNamespaceOrInsideAClass_ShouldCompileWithoutWarningsAndWithResolvedCrefs()
    {
        // Arrange: an enum of another namespace, behind a class of the set's namespace named like the start of that
        // namespace, and an internal enum inside a class, with a member named like a keyword. Texts named like the
        // enum, like the parameter of the generated method, and like what that method calls stand before those
        // names inside the generated class.
        const string source = """
            using CodeChops.Idioma;
            using Domain.Orders;

            [assembly: SupportedLanguages("en")]

            namespace Domain.Orders
            {
                public enum OrderStatus { Pending, Shipped }
            }

            namespace TestApp
            {
                public static class Domain;

                public static class Shipping
                {
                    internal enum Speed { @default, Express }
                }

                public sealed class OrderStatusLocalizationEn : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
                {
                    public string Pending => "Pending";
                    public string Shipped => "Shipped";
                    public string OrderStatus => "Order status";
                    public string value => "Value";
                    public string CreateUndefinedEnumText => "Unknown";
                    public string TextsByValue => "Texts by value";
                    public string System => "System";
                }

                public sealed class SpeedLocalizationEn : ILocalizationSet<Shipping.Speed>, ISpeedLocalization
                {
                    public string @default => "Standard";
                    public string Express => "Express";
                }

                public static class Usage
                {
                    public static LocalizableText Status { get; } = OrderStatusLocalization.GetTextByValue(OrderStatus.Shipped);
                    internal static LocalizableText Speed { get; } = SpeedLocalization.GetTextByValue(Shipping.Speed.@default);
                    public static LocalizableText StatusOfValue { get; } = OrderStatus.Shipped.GetText();
                    internal static LocalizableText SpeedOfValue { get; } = Shipping.Speed.@default.GetText();
                }
            }
            """;

        // Act
        var errors = GeneratorTestHelper.CompileWithGenerators(source);
        var warnings = GeneratorTestHelper.GetWarningsWithGenerators(source);
        var unresolved = GeneratorTestHelper.GetUnresolvedDocReferences(source);
        var contracts = Run(source).Results.ShouldHaveSingleItem().GeneratedSources.Select(static generated => generated.SourceText.ToString()).ToArray();

        // Assert: the method is as accessible as its enum, and so is the extension method on a value.
        errors.ShouldBeEmpty();
        warnings.ShouldBeEmpty();
        unresolved.ShouldBeEmpty();
        contracts.ShouldContain(static contract => contract.Contains("public static CodeChops.Idioma.LocalizableText GetTextByValue(Namespace_Domain_Orders.OrderStatus value)"));
        contracts.ShouldContain(static contract => contract.Contains("internal static CodeChops.Idioma.LocalizableText GetTextByValue(Shipping.Speed value)"));
        contracts.ShouldContain(static contract => contract.Contains("extension(Namespace_Domain_Orders.OrderStatus value)") && contract.Contains("        public CodeChops.Idioma.LocalizableText GetText() =>"));
        contracts.ShouldContain(static contract => contract.Contains("extension(Shipping.Speed value)") && contract.Contains("        internal CodeChops.Idioma.LocalizableText GetText() =>"));
    }

    [Test]
    public void MethodNamedAfterEnumMember_ShouldReportLOC300AndLeaveItsTextAProperty()
    {
        // Arrange & Act
        var result = Run($$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public enum OrderStatus { Pending, Shipped }

            public sealed class OrderStatusLocalizationEn : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
            {
                public string Pending => "Pending";
                public string Shipped(int parcels) => $"Shipped in {parcels} parcels";
            }
            """);

        // Assert: at the method. The contract still asks for the member's text as a property.
        var diagnostic = SingleDiagnostic(result, LocalizationSetsGenerator.UnsupportedTextDiagnosticId);
        diagnostic.GetMessage().ShouldContain("'OrderStatusLocalizationEn.Shipped' is a public method that returns a string");
        diagnostic.GetMessage().ShouldContain("it has the name of 'OrderStatus.Shipped', and the text of an enum member is a property, because it takes no values");
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).ShouldBe("Shipped");

        var text = ContractText(result);
        text.ShouldContain("    string Shipped { get; }");
        text.ShouldNotContain("string Shipped(int parcels)");
    }

    [Test]
    public void TextNamedGetTextByValueInSetOfEnum_ShouldReportLOC205AndCompileOtherwise()
    {
        // Arrange & Act: the contract keeps the text, so the sets still compile; only the generated text is left out.
        var errors = GeneratorTestHelper.CompileWithGenerators($$"""
            {{Header}}

            public enum OrderStatus { Pending }

            public sealed class OrderStatusLocalizationEn : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
            {
                public string Pending => "Pending";
                public string GetTextByValue => "of";
            }

            public sealed class OrderStatusLocalizationNl : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
            {
                public string Pending => "In behandeling";
                public string GetTextByValue => "van";
            }
            """);

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Id.ShouldBe("LOC205");
        error.GetMessage().ShouldBe("'OrderStatusLocalizationEn' has a text named 'GetTextByValue', like the generated method 'OrderStatusLocalization.GetTextByValue', which gives the text of an enum value. The text 'OrderStatusLocalization.GetTextByValue' isn't generated. Rename the text.");
        error.Location.SourceTree!.GetText().ToString(error.Location.SourceSpan).ShouldBe("GetTextByValue");
    }

    [Test]
    public void TextNamedGetTextByValueInSetWithoutEnum_ShouldBeATextLikeAnyOther()
    {
        // Arrange & Act
        var source = $$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string GetTextByValue => "of";
            }
            """;

        var errors = GeneratorTestHelper.CompileWithGenerators(source);

        // Assert
        errors.ShouldBeEmpty();
        ContractText(Run(source)).ShouldContain("public static CodeChops.Idioma.LocalizableText GetTextByValue { get; }");
    }

    [Test]
    public void TextNamedGetTextInSetOfEnum_ShouldReportLOC205AndCompileOtherwise()
    {
        // Arrange & Act: the generated class gets an extension method of that name, which is a method of the class.
        var errors = GeneratorTestHelper.CompileWithGenerators($$"""
            {{Header}}

            public enum OrderStatus { Pending }

            public sealed class OrderStatusLocalizationEn : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
            {
                public string Pending => "Pending";
                public string GetText => "Get";
            }

            public sealed class OrderStatusLocalizationNl : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
            {
                public string Pending => "In behandeling";
                public string GetText => "Haal op";
            }
            """);

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Id.ShouldBe("LOC205");
        error.GetMessage().ShouldBe("'OrderStatusLocalizationEn' has a text named 'GetText', like the generated extension method 'GetText', which gives an enum value its text. The text 'OrderStatusLocalization.GetText' isn't generated. Rename the text.");
        error.Location.SourceTree!.GetText().ToString(error.Location.SourceSpan).ShouldBe("GetText");
    }

    [Test]
    public void TextNamedGetTextInSetWithoutEnum_ShouldBeATextLikeAnyOther()
    {
        // Arrange & Act
        var source = $$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string GetText => "Get";
            }
            """;

        var errors = GeneratorTestHelper.CompileWithGenerators(source);

        // Assert
        errors.ShouldBeEmpty();
        ContractText(Run(source)).ShouldContain("public static CodeChops.Idioma.LocalizableText GetText { get; }");
    }

    [Test]
    [Arguments("TestApp", "ILocalizationSet<OrderStatus>")]
    [Arguments("Domain.Orders", "ILocalizationSet<Domain.Orders.OrderStatus>")]
    public void LanguageWithoutSetOfEnum_ShouldBeToldToAddSetThatListsTheEnum(string enumNamespace, string expectedInterface)
    {
        // Arrange & Act
        var result = Run($$"""
            using CodeChops.Idioma;
            using {{enumNamespace}};

            [assembly: SupportedLanguages("en", "nl")]

            namespace {{enumNamespace}}
            {
                public enum OrderStatus { Pending }
            }

            namespace TestApp
            {
                public sealed class OrderStatusLocalizationEn : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
                {
                    public string Pending => "Pending";
                }
            }
            """);

        // Assert: the set to add lists what the default-language set lists, in the message and in what the IDE writes.
        var diagnostic = SingleDiagnostic(result, LocalizationSetsGenerator.MissingLanguageDiagnosticId);
        diagnostic.GetMessage().ShouldContain($"Add 'class OrderStatusLocalizationNl : {expectedInterface}, IOrderStatusLocalization'.");
        diagnostic.Properties[LocalizationSetsGenerator.SuggestedDeclarationProperty].ShouldBe($"public class OrderStatusLocalizationNl : {expectedInterface}, IOrderStatusLocalization");
    }

    [Test]
    public void SetOfEnumInOtherLanguageWithoutContract_ShouldReportLOC204Only()
    {
        // Arrange & Act
        var result = Run($$"""
            {{Header}}

            public enum OrderStatus { Pending }

            public sealed class OrderStatusLocalizationEn : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
            {
                public string Pending => "Pending";
            }

            public sealed class OrderStatusLocalizationNl : ILocalizationSet<OrderStatus>
            {
                public string Pending => "In behandeling";
            }
            """);

        // Assert: a set like any other, which the generic interface is no contract of.
        var diagnostic = SingleDiagnostic(result, LocalizationSetsGenerator.MissingContractDiagnosticId);
        diagnostic.GetMessage().ShouldContain("'OrderStatusLocalizationNl' is a set for 'nl' but doesn't implement 'IOrderStatusLocalization'");
        diagnostic.Properties[LocalizationSetsGenerator.SuggestedNameProperty].ShouldBe("IOrderStatusLocalization");
    }

    [Test]
    public void RegionalSetOfEnum_ShouldDeriveFromSetOfItsLanguageAndOverrideWhatDiffers()
    {
        // Arrange & Act
        var errors = GeneratorTestHelper.CompileWithGenerators("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "en-GB")]

            namespace TestApp;

            public enum Colour { Grey }

            public class ColourLocalizationEn : ILocalizationSet<Colour>, IColourLocalization
            {
                public virtual string Grey => "Gray";
            }

            public sealed class ColourLocalizationEnGb : ColourLocalizationEn
            {
                public override string Grey => "Grey";
            }
            """);

        // Assert
        errors.ShouldBeEmpty();
    }

    [Test]
    public void SetOfTwoEnums_ShouldGetAMethodForEachAndOneTextForMembersOfOneName()
    {
        // Arrange
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public enum Size { Small, Unknown }

            public enum Colour { Red, Unknown }

            public sealed class OptionLocalizationEn : ILocalizationSet<Size>, ILocalizationSet<Colour>, IOptionLocalization
            {
                public string Small => "Small";
                public string Red => "Red";
                public string Unknown => "Unknown";
            }

            public static class Usage
            {
                public static LocalizableText Size { get; } = OptionLocalization.GetTextByValue(TestApp.Size.Unknown);
                public static LocalizableText Colour { get; } = OptionLocalization.GetTextByValue(TestApp.Colour.Unknown);
                public static LocalizableText SizeOfValue { get; } = TestApp.Size.Unknown.GetText();
                public static LocalizableText ColourOfValue { get; } = TestApp.Colour.Unknown.GetText();
            }
            """;

        // Act
        var errors = GeneratorTestHelper.CompileWithGenerators(source);
        var text = ContractText(Run(source));

        // Assert
        errors.ShouldBeEmpty();
        text.ShouldContain("public static CodeChops.Idioma.LocalizableText GetTextByValue(Colour value) => TextsByValue<IOptionLocalization>.Get(value);");
        text.ShouldContain("    internal static CodeChops.Idioma.LocalizableText Get(Colour value) => value switch");
        text.ShouldContain("public static CodeChops.Idioma.LocalizableText GetTextByValue(Size value) => TextsByValue<IOptionLocalization>.Get(value);");
        text.ShouldContain("    extension(Colour value)");
        text.ShouldContain("    extension(Size value)");
        text.ShouldContain("    internal static CodeChops.Idioma.LocalizableText Get(Size value) => value switch");
        text.ShouldContain("        Colour.Unknown => OptionLocalization.Unknown,");
        text.ShouldContain("        Size.Unknown => OptionLocalization.Unknown,");
        text.Split("    string Unknown { get; }").Length.ShouldBe(2);
    }

    [Test]
    public void SetOfEnumOfAReferencedAssembly_ShouldHaveATextForEveryMember()
    {
        // Arrange & Act: an enum of the framework, read from metadata.
        var source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public sealed class MidpointRoundingLocalizationEn : ILocalizationSet<System.MidpointRounding>, IMidpointRoundingLocalization
            {
                public string ToEven => "To even";
                public string AwayFromZero => "Away from zero";
                public string ToZero => "Toward zero";
                public string ToNegativeInfinity => "Down";
                public string ToPositiveInfinity => "Up";
            }
            """;

        var errors = GeneratorTestHelper.CompileWithGenerators(source);
        var text = ContractText(Run(source));

        // Assert
        errors.ShouldBeEmpty();
        text.ShouldContain("public static CodeChops.Idioma.LocalizableText GetTextByValue(Namespace_System.MidpointRounding value) => TextsByValue<IMidpointRoundingLocalization>.Get(value);");
        text.ShouldContain("    internal static CodeChops.Idioma.LocalizableText Get(Namespace_System.MidpointRounding value) => value switch");
        text.ShouldContain("        Namespace_System.MidpointRounding.ToEven => MidpointRoundingLocalization.ToEven,");
        text.ShouldContain("        _ => CreateUndefinedEnumText<Namespace_System.MidpointRounding>(value),");
    }
    #endregion

    #region Texts by key
    [Test]
    public void GeneratedClass_ShouldGetMethodThatGivesTextByItsKey()
    {
        // Arrange & Act
        var text = ContractText(Run(GreetingSets));

        // Assert: an arm per text, by its name. A text that takes values takes each from the values passed.
        text.ShouldContain("public static CodeChops.Idioma.LocalizableText GetTextByKey(string key, params Namespace_System.ReadOnlySpan<object?> values) => key switch");
        text.ShouldContain("        \"Checkout\" => GetKeyedText<IShopLocalization>(ShopLocalization.Checkout, key, values),");
        text.ShouldContain("        \"ItemsInCart\" => ShopLocalization.ItemsInCart(GetKeyedValue<int>(key, values, 0, 2, \"count\"), GetKeyedValue<string>(key, values, 1, 2, \"shop\")),");
        text.ShouldContain("        _ => throw CreateUnknownKeyException<IShopLocalization>(key, \"ShopLocalization\"),");
    }

    [Test]
    public void MethodThatGivesTextByKey_ShouldCompileWhateverTheTextsAreNamedOrTake()
    {
        // Arrange: texts named like the parameters of the method, like what it declares and calls, and like a
        // keyword, in a namespace with a part named System. One takes a value of a type of another namespace, behind
        // a class named like the start of that namespace, and one a value that may be null.
        const string source = """
            #nullable enable
            using CodeChops.Idioma;
            using Domain.Orders;

            [assembly: SupportedLanguages("en")]

            namespace Domain.Orders
            {
                public enum OrderStatus { Pending }
            }

            namespace TestApp.System
            {
                public static class Domain;

                public sealed class OptionLocalizationEn : ILocalizationSet, IOptionLocalization
                {
                    public string key => "Key";
                                        public string values => "Values";
                    public string text => "Text";
                    public string @event => "Event";
                    public string GetKeyedText => "Keyed text";
                    public string GetKeyedValue => "Keyed value";
                    public string CreateUnknownKeyException => "Unknown";
                    public string Status(OrderStatus status, string? note) => $"{status}: {note}";
                }

                public static class Usage
                {
                    public static LocalizableText Key { get; } = OptionLocalization.GetTextByKey("key");
                    public static LocalizableText Event { get; } = OptionLocalization.GetTextByKey("event");
                    public static LocalizableText Status { get; } = OptionLocalization.GetTextByKey("Status", OrderStatus.Pending, null);
                }
            }
            """;

        // Act
        var errors = GeneratorTestHelper.CompileWithGenerators(source);
        var warnings = GeneratorTestHelper.GetWarningsWithGenerators(source);
        var unresolved = GeneratorTestHelper.GetUnresolvedDocReferences(source);

        // Assert
        errors.ShouldBeEmpty();
        warnings.ShouldBeEmpty();
        unresolved.ShouldBeEmpty();
    }

    [Test]
    public void SetWithoutTexts_ShouldGetMethodThatKnowsNoKey()
    {
        // Arrange & Act
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization;
            """;

        var errors = GeneratorTestHelper.CompileWithGenerators(source);

        // Assert
        errors.ShouldBeEmpty();
        ContractText(Run(source)).ShouldContain("        _ => throw CreateUnknownKeyException<IShopLocalization>(key, \"ShopLocalization\"),");
    }

    [Test]
    [Arguments("public string GetTextByKey => \"Get\";")]
    [Arguments("public string GetTextByKey(string what) => $\"Get {what}\";")]
    public void TextNamedGetTextByKey_ShouldReportLOC205AndCompileOtherwise(string text)
    {
        // Arrange & Act: the contract keeps the text, so the set still compiles; only the generated text is left out.
        var errors = GeneratorTestHelper.CompileWithGenerators($$"""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Checkout => "Checkout";
                {{text}}
            }
            """);

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Id.ShouldBe("LOC205");
        error.GetMessage().ShouldBe("'ShopLocalizationEn' has a text named 'GetTextByKey', like the generated method 'ShopLocalization.GetTextByKey', which gives a text by its key. The text 'ShopLocalization.GetTextByKey' isn't generated. Rename the text.");
        error.Location.SourceTree!.GetText().ToString(error.Location.SourceSpan).ShouldBe("GetTextByKey");
    }

    [Test]
    public void DefaultSetGeneratedCodeCannotCreate_ShouldGetNoMethodThatGivesTextByKey()
    {
        // Arrange & Act: without the texts, there is nothing a key could name.
        var result = Run("""
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en")]

            namespace TestApp;

            public sealed class ShopLocalizationEn(string shop) : ILocalizationSet, IShopLocalization
            {
                public string Checkout => $"Checkout at {shop}";
            }
            """);

        // Assert
        DiagnosticIds(result).ShouldBe([LocalizationSetsGenerator.CannotExtendDiagnosticId]);
        ContractText(result).ShouldNotContain(" GetTextByKey(string key");
    }
    #endregion
}
