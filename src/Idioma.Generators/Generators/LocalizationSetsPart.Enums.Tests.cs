using CodeChops.Idioma.Generators;
using CodeChops.Idioma.Tests.TestHelpers;

namespace CodeChops.Idioma.Tests;

public sealed partial class LocalizationSetsPartTests
{
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
        text.ShouldContain("public static string Caption => GetLocalizationSet<IOrderStatusLocalization>(0, 4, out var culture)?.Caption");
        text.ShouldContain("public static string Shipped => GetLocalizationSet<IOrderStatusLocalization>(3, 4, out var culture)?.Shipped");
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
    [Arguments("[System.Obsolete(\"Use Shipped.\", error: true)]", "", "")]
    [Arguments("[System.Diagnostics.CodeAnalysis.Experimental(\"SHOP001\")]", "", "")]
    [Arguments("[System.Obsolete(\"Use Shipped.\", DiagnosticId = \"SHOP002\")]", "", "")]
    [Arguments("", "[System.Diagnostics.CodeAnalysis.Experimental(\"SHOP001\")]", "")]
    [Arguments("", "[System.Obsolete(\"Use the new status.\", DiagnosticId = \"SHOP002\")]", "")]
    [Arguments("", "", "[System.Diagnostics.CodeAnalysis.Experimental(\"SHOP001\")]")]
    [Arguments("", "", "[System.Obsolete(\"Use an amount.\", DiagnosticId = \"SHOP002\")]")]
    public void EnumMemberEnumOrParameterTypeWithDiagnosticOfItsOwn_LeavesNothingInGeneratedCode(string onMember, string onEnum, string onParameterType)
    {
        // Arrange: the generated code names the enum, each of its members, and the type of each parameter. Naming them
        // reports a diagnostic of its author's choosing, which a build treats as an error (Experimental, and Obsolete
        // as an error) or stops at when it treats warnings as errors (Obsolete). The pragma of the sets' file doesn't
        // reach a generated file.
        var source = $$"""
            #pragma warning disable SHOP001, SHOP002
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            {{onEnum}}
            public enum OrderStatus { Pending, {{onMember}} Sent, Shipped }

            {{onParameterType}}
            public sealed record Money(decimal Amount);

            public sealed class OrderStatusLocalizationEn : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
            {
                public string Pending => "Pending";
                public string Sent => "Sent";
                public string Shipped => "Shipped";
            }

            public sealed class OrderStatusLocalizationNl : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
            {
                public string Pending => "In behandeling";
                public string Sent => "Verstuurd";
                public string Shipped => "Verzonden";
            }

            public sealed class ShopLocalizationEn : ILocalizationSet, IShopLocalization
            {
                public string Total(Money money, System.Collections.Generic.List<Money>[] earlier) => $"Total: {money.Amount}, after {earlier.Length}";
            }

            public sealed class ShopLocalizationNl : ILocalizationSet, IShopLocalization
            {
                public string Total(Money money, System.Collections.Generic.List<Money>[] earlier) => $"Totaal: {money.Amount}, na {earlier.Length}";
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
    public void EnumMemberMarkedObsoleteAsError_ShouldGiveItsTextByItsValue()
    {
        // Arrange: generated code can't name the member, whose value stored data may still hold.
        var source = $$"""
            {{Header}}

            public enum OrderStatus
            {
                Pending,
                Shipped,
                [System.Obsolete("Use Shipped.", error: true)]
                Sent = -1,
            }

            public sealed class OrderStatusLocalizationEn : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
            {
                public string Pending => "Pending";
                public string Sent => "Sent";
                public string Shipped => "Shipped";
            }

            public sealed class OrderStatusLocalizationNl : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
            {
                public string Pending => "In behandeling";
                public string Sent => "Verstuurd";
                public string Shipped => "Verzonden";
            }
            """;

        // Act
        var text = ContractText(Run(source));
        var errors = GeneratorTestHelper.CompileWithGenerators(source);

        // Assert: the parentheses keep the negative value whole.
        errors.ShouldBeEmpty();
        text.ShouldContain("        (OrderStatus)(-1) => OrderStatusLocalization.Sent,");
        text.ShouldContain("        OrderStatus.Shipped => OrderStatusLocalization.Shipped,");
    }

    [Test]
    public void TextOfDefaultSetThatIsNoMember_ShouldBeReportedNextToTheErrorOfTheSetThatLacksIt()
    {
        // Arrange: Delivered is no member of OrderStatus, and the contract has it, so the Dutch set doesn't compile. A
        // build drops an analyzer's diagnostics next to that error, so the generator reports it.
        const string source = """
            using CodeChops.Idioma;

            [assembly: SupportedLanguages("en", "nl")]

            namespace TestApp;

            public enum OrderStatus { Pending }

            public sealed class OrderStatusLocalizationEn : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
            {
                public string Pending => "Pending";
                public string Delivered => "Delivered";
            }

            public sealed class OrderStatusLocalizationNl : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
            {
                public string Pending => "In behandeling";
            }
            """;

        // Act
        var errors = GeneratorTestHelper.CompileWithGenerators(source);
        var diagnostic = SingleDiagnostic(Run(source), TextOutsideContractAnalyzer.TextOutsideEnumDiagnosticId);

        // Assert: at the text, with the advice the analyzer gives in every other set.
        errors.Select(static error => error.Id).ShouldBe(["CS0535"]);
        diagnostic.Severity.ShouldBe(Microsoft.CodeAnalysis.DiagnosticSeverity.Warning);
        diagnostic.GetMessage().ShouldBe("'OrderStatusLocalizationEn.Delivered' isn't a member of 'OrderStatus', so GetText() never gives it. Add 'Delivered' to 'OrderStatus', move it to a set of its own, or mark it [ExcludeFromLocalizationDiscovery] if it isn't a text.");
        diagnostic.Location.GetLineSpan().StartLinePosition.Line.ShouldBe(11);
    }

    [Test]
    public void SetOfEnumOfAnotherNamespaceOrInsideAClass_ShouldCompileWithoutWarningsAndWithResolvedCrefs()
    {
        // Arrange: an enum of another namespace, and an internal enum inside a class, with a member named like a
        // keyword. Texts named like the enum, like the parameter of the generated method, and like what that method
        // calls stand before those names inside the generated class.
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
                public static class Shipping
                {
                    internal enum Speed { @default, Express }
                }

                public sealed class OrderStatusLocalizationEn : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
                {
                    public string Pending => "Pending";
                    public string Shipped => "Shipped";
            #pragma warning disable LOC305 // Texts that aren't members, kept on purpose
                    public string OrderStatus => "Order status";
                    public string value => "Value";
                    public string ResolveWording => "Unknown";
                    public string TextsByValue => "Texts by value";
                    public string System => "System";
            #pragma warning restore LOC305
                }

                public sealed class SpeedLocalizationEn : ILocalizationSet<Shipping.Speed>, ISpeedLocalization
                {
                    public string @default => "Standard";
                    public string Express => "Express";
                }

                public static class Usage
                {
                    public static string Status => OrderStatusLocalization.GetTextByValue(OrderStatus.Shipped);
                    internal static string Speed => SpeedLocalization.GetTextByValue(Shipping.Speed.@default);
                    public static string StatusOfValue => OrderStatus.Shipped.GetText();
                    internal static string SpeedOfValue => Shipping.Speed.@default.GetText();
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
        contracts.ShouldContain(static contract => contract.Contains("public static string GetTextByValue(Domain.Orders.OrderStatus value)"));
        contracts.ShouldContain(static contract => contract.Contains("internal static string GetTextByValue(Shipping.Speed value)"));
        contracts.ShouldContain(static contract => contract.Contains("extension(Domain.Orders.OrderStatus value)") && contract.Contains("        public string GetText() =>"));
        contracts.ShouldContain(static contract => contract.Contains("extension(Shipping.Speed value)") && contract.Contains("        internal string GetText() =>"));
    }

    [Test]
    public void MethodNamedAfterEnumMember_ShouldReportLOC400AndLeaveItsTextAProperty()
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
        var diagnostic = SingleDiagnostic(result, LocalizationSetsPart.UnsupportedTextDiagnosticId);
        diagnostic.GetMessage().ShouldContain("'OrderStatusLocalizationEn.Shipped' is a public method that returns a string");
        diagnostic.GetMessage().ShouldContain("it has the name of 'OrderStatus.Shipped', and the text of an enum member is a property, because it takes no values");
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).ShouldBe("Shipped");

        var text = ContractText(result);
        text.ShouldContain("    string Shipped { get; }");
        text.ShouldNotContain("string Shipped(int parcels)");
    }

    [Test]
    public void TextNamedGetTextByValueInSetOfEnum_ShouldReportLOC300AndCompileOtherwise()
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
        error.Id.ShouldBe("LOC300");
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
        ContractText(Run(source)).ShouldContain("public static string GetTextByValue => GetLocalizationSet<IShopLocalization>(");
    }

    [Test]
    public void TextNamedGetTextInSetOfEnum_ShouldReportLOC300AndCompileOtherwise()
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
        error.Id.ShouldBe("LOC300");
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
        ContractText(Run(source)).ShouldContain("public static string GetText => GetLocalizationSet<IShopLocalization>(");
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
        var diagnostic = SingleDiagnostic(result, LocalizationSetsPart.MissingLanguageDiagnosticId);
        diagnostic.GetMessage().ShouldContain($"Add 'class OrderStatusLocalizationNl : {expectedInterface}, IOrderStatusLocalization'.");
        diagnostic.Properties[LocalizationSetsPart.SuggestedDeclarationProperty].ShouldBe($"public class OrderStatusLocalizationNl : {expectedInterface}, IOrderStatusLocalization");
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
        var diagnostic = SingleDiagnostic(result, LocalizationSetsPart.MissingContractDiagnosticId);
        diagnostic.GetMessage().ShouldContain("'OrderStatusLocalizationNl' is a set for 'nl' but doesn't implement 'IOrderStatusLocalization'");
        diagnostic.Properties[LocalizationSetsPart.SuggestedNameProperty].ShouldBe("IOrderStatusLocalization");
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
                public static string Size => OptionLocalization.GetTextByValue(TestApp.Size.Unknown);
                public static string Colour => OptionLocalization.GetTextByValue(TestApp.Colour.Unknown);
                public static string SizeOfValue => TestApp.Size.Unknown.GetText();
                public static string ColourOfValue => TestApp.Colour.Unknown.GetText();
            }
            """;

        // Act
        var errors = GeneratorTestHelper.CompileWithGenerators(source);
        var text = ContractText(Run(source));

        // Assert
        errors.ShouldBeEmpty();
        text.ShouldContain("public static string GetTextByValue(Colour value) => TextsByValue<IOptionLocalization>.Get(value);");
        text.ShouldContain("    internal static string Get(Colour value) => value switch");
        text.ShouldContain("public static string GetTextByValue(Size value) => TextsByValue<IOptionLocalization>.Get(value);");
        text.ShouldContain("    extension(Colour value)");
        text.ShouldContain("    extension(Size value)");
        text.ShouldContain("    internal static string Get(Size value) => value switch");
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
        text.ShouldContain("public static string GetTextByValue(System.MidpointRounding value) => TextsByValue<IMidpointRoundingLocalization>.Get(value);");
        text.ShouldContain("    internal static string Get(System.MidpointRounding value) => value switch");
        text.ShouldContain("        System.MidpointRounding.ToEven => MidpointRoundingLocalization.ToEven,");
        text.ShouldContain("        _ => value.ToString(),");
    }
}
