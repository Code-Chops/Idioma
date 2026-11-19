using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="ConstantWording"/> and <see cref="MethodWording"/>: a wording they read from the syntax alone
/// is the wording the compiler gives, so that the generators bind nothing for the texts written the usual way.
/// </summary>
public sealed class MethodWordingTests
{
    #region Fixture
    private const string Quotes = "\"\"\"";

    /// <summary>
    /// A set whose one text returns <paramref name="member"/>, next to a constant and a type a wording can name.
    /// </summary>
    private static (MemberDeclarationSyntax Member, SemanticModel Model) Compile(string member)
    {
        var (tree, model) = GeneratorTestHelper.Compile($$"""
            using System.Linq;

            namespace TestApp;

            public enum Color { Red, Green }

            public static class Words
            {
                public const string Hello = "Hello";
                public const string name = "name";
            }

            public sealed class ShopLocalizationEn
            {
                {{member}}
            }
            """);

        var declaration = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Last().Members.Single();

        return (declaration, model);
    }

    private static ExpressionSyntax Returned(MemberDeclarationSyntax member)
    {
        var returned = member switch
        {
            PropertyDeclarationSyntax property => property.ExpressionBody!.Expression,
            MethodDeclarationSyntax method => method.ExpressionBody!.Expression,
            _ => throw new ArgumentOutOfRangeException(nameof(member)),
        };

        return returned;
    }
    #endregion

    [Test]
    [Arguments("\"Checkout\"", "Checkout")]
    [Arguments("\"\"", "")]
    [Arguments("\"A \\\"quoted\\\" word, a tab\\t, and \\u00e9\"", "A \"quoted\" word, a tab\t, and \u00e9")]
    [Arguments("@\"A \"\"quoted\"\" word and a \\ backslash\"", "A \"quoted\" word and a \\ backslash")]
    [Arguments(Quotes + "A \"quoted\" word" + Quotes, "A \"quoted\" word")]
    [Arguments(Quotes + "\n        Two lines,\n          the second indented\n        " + Quotes, "Two lines,\n  the second indented")]
    public void WordingWrittenAsString_ShouldBeReadFromItsSyntaxAsTheCompilerReadsIt(string expression, string expected)
    {
        // Arrange
        var (member, model) = Compile($"public string Checkout => {expression};");

        // Act
        var written = ConstantWording.ReadString(Returned(member));
        var read = ConstantWording.Read((PropertyDeclarationSyntax)member, model, CancellationToken.None);

        // Assert: the syntax gives what the compiler gives, so nothing has to be bound for it.
        var constant = model.GetConstantValue(Returned(member));
        constant.HasValue.ShouldBeTrue();
        written.ShouldBe(expected);
        written.ShouldBe((string?)constant.Value);
        read.ShouldBe(expected);
    }

    [Test]
    [Arguments("\"Check\" + \"out\"", "Checkout")]
    [Arguments("Words.Hello", "Hello")]
    [Arguments("$\"{Words.Hello}, you\"", "Hello, you")]
    [Arguments("nameof(Checkout)", "Checkout")]
    [Arguments("(\"Checkout\")", "Checkout")]
    [Arguments("null!", null)]
    [Arguments("default!", null)]
    [Arguments("System.Environment.NewLine", null)]
    public void WordingNotWrittenAsString_ShouldBeLeftToTheCompiler(string expression, string? expected)
    {
        // Arrange
        var (member, model) = Compile($"public string Checkout => {expression};");

        // Act
        var written = ConstantWording.ReadString(Returned(member));
        var read = ConstantWording.Read((PropertyDeclarationSyntax)member, model, CancellationToken.None);

        // Assert
        written.ShouldBeNull();
        read.ShouldBe(expected);
    }

    [Test]
    [Arguments("string name", "$\"Hello, {name}!\"", "Hello, {name}!")]
    [Arguments("decimal amount", "$\"Total: {amount:N2}\"", "Total: {amount:N2}")]
    [Arguments("string name", "$\"Hello, {name.Trim()}!\"", "Hello, {name.Trim()}!")]
    [Arguments("string name", "$\"Hello, {(name)}!\"", "Hello, {(name)}!")]
    [Arguments("int count", "count is 1 ? \"1 item\" : $\"{count} items\"", null)]
    [Arguments("int count", "count > 1 ? \"Several\" : \"One\"", null)]
    [Arguments("int count", "count switch { 1 => \"One\", _ => \"Many\" }", null)]
    [Arguments("string name", "\"Hello, \" + name", null)]
    [Arguments("string? name", "name ?? \"you\"", null)]
    [Arguments("string name", "name", null)]
    [Arguments("string name", "name.ToUpperInvariant()", null)]
    [Arguments("bool formal", "!formal ? \"Hi\" : \"Good day\"", null)]
    public void WordingThatTakesValue_ShouldBeKnownForNoConstantFromItsSyntax(string parameter, string expression, string? expected)
    {
        // Arrange
        var (member, model) = Compile($"public string Greeting({parameter}) => {expression};");
        var method = (MethodDeclarationSyntax)member;

        // Act
        var takesValue = MethodWording.TakesValue(Returned(member), method);
        var wording = MethodWording.Read(method, model, CancellationToken.None);

        // Assert: the compiler agrees that it is no constant, so it need not be asked.
        takesValue.ShouldBeTrue();
        model.GetConstantValue(Returned(member)).HasValue.ShouldBeFalse();
        wording.ShouldBe(expected);
    }

    [Test]
    [Arguments("string name", "\"Hello\"", "Hello")]
    [Arguments("string name", "\"Hel\" + \"lo\"", "Hello")]
    [Arguments("string name", "nameof(name)", "name")]
    [Arguments("string name", "$\"{nameof(name)}: {Words.Hello}\"", "name: Hello")]
    [Arguments("string name", "Words.name", "name")]
    [Arguments("string name", "$\"{Words.name}\"", "name")]
    [Arguments("string name", "true ? \"Hello\" : \"Hi\"", "Hello")]
    [Arguments("Color Color", "$\"{nameof(Color.Red)}\"", "Red")]
    [Arguments("string name", "string.Concat(new[] { \"a\" }.Select(name => name))", null)]
    [Arguments("string name", "Words.Hello is string name ? name : \"None\"", null)]
    [Arguments("string name, string name", "$\"Hello, {name}!\"", "Hello, {name}!")]
    public void WordingThatMayBeConstant_ShouldBeLeftToTheCompiler(string parameters, string expression, string? expected)
    {
        // Arrange: a constant, a name that stands for something else than the parameter, or code that does not
        // compile.
        var (member, model) = Compile($"public string Greeting({parameters}) => {expression};");
        var method = (MethodDeclarationSyntax)member;

        // Act
        var takesValue = MethodWording.TakesValue(Returned(member), method);
        var wording = MethodWording.Read(method, model, CancellationToken.None);

        // Assert
        takesValue.ShouldBeFalse();
        wording.ShouldBe(expected);
    }
}
