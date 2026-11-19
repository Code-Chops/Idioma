using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="TextParameters"/>: where the syntax of a text says which parameter a name stands for, it says
/// what the compiler says, so that the generators bind nothing for the texts written the usual way.
/// </summary>
public sealed class TextParametersTests
{
    #region Fixture
    /// <summary>
    /// Each name in the body of a method that one of its parameters carries, with the parameter the syntax gives it
    /// (<see cref="TextParameters.FindParameterBySyntax"/>) and the one the compiler binds it to.
    /// </summary>
    private static List<(string Name, int? BySyntax, int? Bound)> ReadNames(string method)
    {
        var (tree, model) = GeneratorTestHelper.Compile($$"""
            using System.Linq;

            namespace TestApp;

            public enum Color { Red, Green }

            public sealed class ShopLocalizationEn
            {
                public string name = "A field";

                {{method}}

                private static string Twice(string value) => value + value;
            }
            """);

        var declaration = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        var symbol = model.GetDeclaredSymbol(declaration)!;
        var body = (SyntaxNode?)declaration.ExpressionBody?.Expression ?? declaration.Body!;
        HashSet<string>? redeclared = null;
        var names = new List<(string Name, int? BySyntax, int? Bound)>();

        foreach (var identifier in body.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
        {
            if (!symbol.Parameters.Any(parameter => String.Equals(parameter.Name, identifier.Identifier.ValueText, StringComparison.Ordinal)))
                continue;

            var bound = model.GetSymbolInfo(identifier).Symbol is IParameterSymbol parameter && SymbolEqualityComparer.Default.Equals(parameter.ContainingSymbol, symbol)
                ? parameter.Ordinal
                : (int?)null;

            names.Add((identifier.Identifier.ValueText, TextParameters.FindParameterBySyntax(identifier, symbol, body, ref redeclared), bound));
        }

        return names;
    }
    #endregion

    [Test]
    [Arguments("public string Greeting(string name) => $\"Hello, {name}!\";")]
    [Arguments("public string Greeting(string name) { return $\"Hello, {name}!\"; }")]
    [Arguments("public string Total(decimal amount) => $\"Total: {amount:N2}\";")]
    [Arguments("public string Items(int count, string shop) => count is 1 ? $\"1 item at {shop}\" : $\"{count} items at {shop}\";")]
    [Arguments("public string Items(int count) => count switch { 1 => \"One\", _ when count % 10 is >= 2 and <= 4 => $\"{count} few\", _ => $\"{count} many\" };")]
    [Arguments("public string Items(int count) => count > 1 ? \"Several\" : \"One\";")]
    [Arguments("public string Greeting(string name) => Twice(name);")]
    [Arguments("public string Greeting(string name) => \"Hello, \" + name.Trim() + (name);")]
    [Arguments("public string Greeting(string name) => name;")]
    [Arguments("public string Greeting(bool formal, string name) => !formal ? name : $\"Dear {name}\";")]
    public void NameReadAsValue_ShouldBeTheParameterFromItsSyntax(string method)
    {
        // Arrange & Act
        var names = ReadNames(method);

        // Assert: every name is told from the syntax, and is the parameter the compiler binds it to.
        names.ShouldNotBeEmpty();
        names.ShouldAllBe(name => name.BySyntax != null && name.BySyntax == name.Bound);
    }

    [Test]
    [Arguments("public string Greeting(string name) => string.Join(\", \", new[] { \"a\" }.Select(name => name));")]
    [Arguments("public string Greeting(object value, string name) => value is string name ? name : \"None\";")]
    [Arguments("public string Greeting(string name) => this.name;")]
    [Arguments("public string Greeting(string name) => Twice(value: name) + new { name = 1 }.name;")]
    [Arguments("public string Greeting(Color Color) => Color.Red.ToString() + Color;")]
    [Arguments("public string Greeting(string name, string name) => $\"Hello, {name}!\";")]
    [Arguments("public string Greeting(string name) { if (name.Length > 3) return \"Long\"; var copy = name; return copy; }")]
    [Arguments("public string Greeting(string name) => name is name ? \"Same\" : nameof(name);")]
    public void NameThatMayStandForSomethingElse_ShouldNeverBeToldWrongFromItsSyntax(string method)
    {
        // Arrange & Act: a name a lambda or a pattern declares anew, the name of a member or of an argument, a
        // parameter named like its type, and code that does not compile.
        var names = ReadNames(method);

        // Assert: where the syntax tells at all, it tells what the compiler binds.
        names.ShouldNotBeEmpty();
        names.ShouldAllBe(name => name.BySyntax == null || name.BySyntax == name.Bound);
    }
}
