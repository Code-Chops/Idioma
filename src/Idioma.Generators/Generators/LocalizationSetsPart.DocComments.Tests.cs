using CodeChops.Idioma.Tests.TestHelpers;

namespace CodeChops.Idioma.Tests;

public sealed partial class LocalizationSetsPartTests
{
    [Test]
    public void GeneratedMembers_ShouldInheritDocCommentOfProperty()
    {
        // Arrange & Act
        var text = ContractText(Run(ShopSets));

        // Assert: the text points at the property, whose doc comment is the single source, and the contract member at
        // the text.
        text.Split('\n').Count(line => line.Contains("/// <inheritdoc cref=\"ShopLocalizationEn.Checkout\"/>", StringComparison.Ordinal)).ShouldBe(1);
        text.ShouldContain("/// <inheritdoc cref=\"ShopLocalization.Checkout\"/>");
        text.ShouldNotContain("/// Checkout");
    }

    [Test]
    public void GeneratedMembers_WithoutDocComment_ShouldBeDocumentedWithDefaultWording()
    {
        // Arrange & Act: Total has no doc comment, so its English wording documents the text, which the contract member
        // inherits.
        var text = ContractText(Run(ShopSets));

        // Assert
        text.Split('\n').Count(line => String.Equals(line.Trim(), "/// Total", StringComparison.Ordinal)).ShouldBe(1);
        text.ShouldContain("/// <inheritdoc cref=\"ShopLocalization.Total\"/>");
        text.ShouldNotContain("inheritdoc cref=\"ShopLocalizationEn.Total\"");
    }

    [Test]
    [Arguments("OptionLocalization")]
    [Arguments("GetTextByKey")]
    public void TextTheClassDoesntHold_ShouldBeDocumentedInTheContract(string name)
    {
        // Arrange: a text the generated class can't hold for its name (LOC300), next to one it holds. Inside the
        // contract, a text named like the class doesn't hide the class from the cref of the other member.
        var source = $$"""
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
            """;

        // Act
        var text = ContractText(Run(source));
        var unresolved = GeneratorTestHelper.GetUnresolvedDocReferences(source);

        // Assert
        text.ShouldContain($"/// <item><description><see cref=\"OptionLocalizationNl.{name}\">nl</see>: Tekst</description></item>");
        text.ShouldNotContain($"<inheritdoc cref=\"OptionLocalization.{name}\"/>");
        text.ShouldContain("/// <inheritdoc cref=\"OptionLocalization.Other\"/>");
        unresolved.ShouldBeEmpty();
    }

    [Test]
    public void GeneratedMembers_ShouldLinkToEveryTranslation()
    {
        // Arrange & Act
        var text = ContractText(Run(ShopSets));

        // Assert: on the text, for each language in the order of the attribute, with its wording next to the link.
        foreach (var (language, set, member, wording) in new[]
                 {
                     ("en", "ShopLocalizationEn", "Checkout", "Checkout"), ("en", "ShopLocalizationEn", "Total", "Total"),
                     ("nl", "ShopLocalizationNl", "Checkout", "Afrekenen"), ("nl", "ShopLocalizationNl", "Total", "Totaal"),
                 })
        {
            var line = $"/// <item><description><see cref=\"{set}.{member}\">{language}</see>: {wording}</description></item>";
            text.Split('\n').Count(candidate => candidate.Contains(line, StringComparison.Ordinal)).ShouldBe(1);
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
                public static string Text => ScopeLocalization.Scope(scoped: "all");
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
        // Arrange: a British set with wording of its own for one text, as docs/language-codes.md writes it.
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
    [Arguments("namespace Shared.Wording { public abstract class DutchBase { public string Checkout => \"Afrekenen\"; } }", "Shared.Wording.DutchBase", "Shared.Wording.DutchBase.Checkout")]
    [Arguments("namespace Shared { public abstract class Pair<TFirst, TSecond> { public string Checkout => \"Afrekenen\"; } }", "Shared.Pair<int, string>", "Shared.Pair{TFirst, TSecond}.Checkout")]
    public void SetInheritingTextFromGenericOrNestedClassOrOtherNamespace_ShouldBeLinkedWithResolvedCref(string declaration, string baseType, string expectedCref)
    {
        // Arrange: a cref names a generic class with its type parameters in braces, a nested one through its container,
        // and a class of another namespace by its full name.
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
        text.ShouldContain("<see cref=\"Shared.Wording.RewordedShopLocalizationEn.Checkout\">en</see>: Pay (<see cref=\"Shared.Wording.RewordedShopLocalizationEn\"/>)</description>");
        text.ShouldContain("<see cref=\"ShopLocalizationEn.Total\">en</see>: Total (<see cref=\"Shared.Wording.RewordedShopLocalizationEn\"/>, inherited from <see cref=\"ShopLocalizationEn\"/>)</description>");
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
        text.ShouldNotContain("<inheritdoc cref=\"ShopLocalizationEn.");
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
    public void SetInOtherNamespace_ShouldBeLinkedByItsFullName()
    {
        // Arrange
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace Shop
            {
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

        // Assert
        text.ShouldContain("<see cref=\"Shop.Dutch.ShopLocalizationNl.Checkout\">nl</see>");
        text.ShouldContain("<see cref=\"ShopLocalizationEn.Checkout\">en</see>");
        errors.ShouldBeEmpty();
        unresolved.ShouldBeEmpty();
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
}
