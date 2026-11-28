using CodeChops.Idioma.Tests.TestHelpers;

namespace CodeChops.Idioma.Tests;

public sealed partial class LocalizationSetsPartTests
{
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

        // Assert: the contract repeats the method, which is what holds every language to its parameters, and marks
        // on it the values its wording shows. The class hands the values it is called with to the set, on every call.
        text.ShouldContain("    string Greeting(string name);");
        text.ShouldContain("    string ItemsInCart(int count, string shop);");
        ShownValues(text, "Greeting").ShouldBe(["name"]);
        ShownValues(text, "ItemsInCart").ShouldBe(["count", "shop"]);
        text.ShouldContain("string Greeting(string name) =>");
        text.ShouldContain("""public static string Greeting(string name) => GetLocalizationSetForValues<IShopLocalization>()?.Greeting(name) ?? ResolveWording<IShopLocalization, ShopLocalizationEn, string>("Greeting", static (set, name) => set.Greeting(name), name);""");
        text.ShouldContain("""public static string ItemsInCart(int count, string shop) => GetLocalizationSetForValues<IShopLocalization>()?.ItemsInCart(count, shop) ?? ResolveWording<IShopLocalization, ShopLocalizationEn, (int, string)>("ItemsInCart", static (set, values) => set.ItemsInCart(values.Item1, values.Item2), (count, shop));""");
    }

    [Test]
    public void ValueShownThroughPatternVariable_ShouldBeMarkedAsShown()
    {
        // Arrange & Act: 'n' and 'number' stand for the count as a whole, so the wording shows it. 'length' stands for a
        // part of the name, and nameof reads no value.
        var text = ContractText(Run($$"""
            {{Header}}

            public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Items(int count) => count switch { 1 => "1 item", var n => $"{n} items" };
                public string Pieces(int count) => count is > 1 and int number ? $"{number} pieces" : "One piece";
                public string Initials(string name) => name switch { { Length: var length } => $"{length} letters" };
                public string Field(string name) => $"The field {nameof(name)}";
            }

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Items(int count) => $"{count} artikelen";
                public string Pieces(int count) => $"{count} stuks";
                public string Initials(string name) => name;
                public string Field(string name) => "Het veld";
            }
            """));

        // Assert
        ShownValues(text, "Items").ShouldBe(["count"]);
        ShownValues(text, "Pieces").ShouldBe(["count"]);
        ShownValues(text, "Initials").ShouldBeEmpty();
        ShownValues(text, "Field").ShouldBeEmpty();
    }

    [Test]
    public void PartialMethodOfDefaultSet_ShouldBeReadWhereItsImplemented()
    {
        // Arrange & Act: the part that declares the method has no body to read the wording from.
        var text = ContractText(Run($$"""
            {{Header}}

            public partial class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public partial string Greeting(string name);
                public partial string Greeting(string name) => $"Hello, {name}!";
            }

            public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Greeting(string name) => $"Hallo, {name}!";
            }
            """));

        // Assert
        ShownValues(text, "Greeting").ShouldBe(["name"]);
        text.ShouldContain("<see cref=\"ShopLocalizationEn.Greeting\">en</see>: Hello, {name}!</description>");
    }

    [Test]
    public void TextThatTakesValues_ShouldBeDocumentedLikeAnyText()
    {
        // Arrange & Act
        var text = ContractText(Run(GreetingSets));

        // Assert: the text inherits the doc comment of the method, parameters included, and the wording of each language
        // stands next to its link with each value in braces. A method that chooses between wordings shows each of them,
        // cut where a line grows too long. The contract member inherits the text's.
        text.Split('\n').Count(line => line.Contains("/// <inheritdoc cref=\"ShopLocalizationEn.Greeting\"/>", StringComparison.Ordinal)).ShouldBe(1);
        text.Split('\n').Count(line => line.Contains("<see cref=\"ShopLocalizationEn.Greeting\">en</see>: Hello, {name}!</description>", StringComparison.Ordinal)).ShouldBe(1);
        text.Split('\n').Count(line => line.Contains("<see cref=\"ShopLocalizationNl.Greeting\">nl</see>: Hallo, {name}!</description>", StringComparison.Ordinal)).ShouldBe(1);
        text.Split('\n').Count(line => line.Contains("/// 1 item in your cart at {shop} / {count} items in your cart at {shop}", StringComparison.Ordinal)).ShouldBe(1);
        text.Split('\n').Count(line => line.Contains("<see cref=\"ShopLocalizationNl.ItemsInCart\">nl</see>: 1 artikel in je winkelwagen bij {shop} / {count} artikelen in je winkelwagen bij…</description>", StringComparison.Ordinal)).ShouldBe(1);
        text.ShouldContain("/// <inheritdoc cref=\"ShopLocalization.Greeting\"/>");
        text.ShouldContain("/// <inheritdoc cref=\"ShopLocalization.ItemsInCart\"/>");
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
        text.ShouldContain("public static new string Equals(object? other)");
        text.ShouldContain("public static new string ReferenceEquals(object? left, object? right)");
        text.ShouldContain("public static string ToString(string format)");
        text.ShouldContain("public static string GetHashCode(int seed)");
        warnings.ShouldBeEmpty();
        errors.ShouldBeEmpty();
    }

    [Test]
    public void TextThatTakesValuesNamedLikeGeneratedClass_ShouldReportLOC300AndCompileOtherwise()
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
        error.Id.ShouldBe("LOC300");
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
        // a brace that is text, a constant, a method that chooses between wordings, which shows each, and a method of
        // statements, which shows none.
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
        text.ShouldContain("<see cref=\"ShopLocalizationEn.Chosen\">en</see>: One / Many</description>");
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
        text.ShouldContain("string Shown(string name) =>");
        text.ShouldNotContain("ShownValue] ");
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
}
