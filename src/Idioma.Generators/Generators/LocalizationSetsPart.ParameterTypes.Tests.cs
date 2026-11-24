using CodeChops.Idioma.Tests.TestHelpers;

namespace CodeChops.Idioma.Tests;

public sealed partial class LocalizationSetsPartTests
{
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

        // Assert: a type of another namespace stands by its full name, as a set of another namespace does.
        errors.ShouldBeEmpty();
        warnings.ShouldBeEmpty();
        text.ShouldNotContain("using TestApp.Models;");
        text.ShouldContain("    string Keywords(string? name, int count, decimal price, object? value, nint handle);");
        text.ShouldContain("    string OwnNamespace(Size size, Size? optional);");
        text.ShouldContain("    string OtherNamespace(TestApp.Models.Salutation salutation, TestApp.Models.Order.Line line, TestApp.Models.Box<TestApp.Models.Order?>.Lid? lid);");
        text.ShouldContain("    string GlobalNamespace(GlobalKind kind);");
        text.ShouldContain("    string Framework(System.DateOnly date, System.DateTime? time, System.Collections.Generic.IReadOnlyList<string?> names, System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<int>>? counts);");
        text.ShouldContain("    string Arrays(string[] names, int[,] grid, string?[]? optional, int[][] jagged, int[]?[] rows);");
        text.ShouldContain("    string Tuples((string Name, int Count) pair, (int, string?) unnamed);");
        text.ShouldContain("    string Delegates(System.Func<int, string> format);");
        text.ShouldContain("    string @event(string @class, int @int);");
        text.ShouldContain("public static string @event(string @class, int @int) => FindSetForValues<IShopLocalization>(");
        text.ShouldContain(")?.@event(@class, @int) ?? WordSetText<IShopLocalization, ShopLocalizationEn, string, int>(");
        text.ShouldContain(", \"event\", static (set, @class, @int) => set.@event(@class, @int), @class, @int);");
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
            public string WordSetText(string FindSet, string FindSetForValues) => FindSet + FindSetForValues;
            public string FindSetForValues(string WordSetText) => WordSetText;
            public string FindSet => "Find";
            public string GetKeyedValue(string GetKeyedText) => GetKeyedText;
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
        // Arrange: Money binds, so the contract names it by its full name and needs no directive of the set. OrderId is
        // written by another generator. The set imports System as the contract's file does.
        const string source = """
            using System;
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
        bound.ShouldContain("string Total(MyApp.Domain.Money value);");
        unbound.ShouldContain("using System.Text;");
        unbound.ShouldContain("using CodeChops.Idioma;");
        unbound.IndexOf("using MyApp.Domain;", StringComparison.Ordinal).ShouldBeInRange(0, unbound.IndexOf("namespace MyApp.Localization;", StringComparison.Ordinal));
        unbound.ShouldContain("string Total(OrderId value);");
        unbound.Split('\n').Count(static line => String.Equals(line.TrimEnd(), "using System;", StringComparison.Ordinal)).ShouldBe(1);
    }
}
