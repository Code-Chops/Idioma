using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Resources;
using BenchmarkDotNet.Attributes;
using CodeChops.Idioma;

namespace Benchmarks;

/// <summary>
/// The label of a property found and shown once, for a Dutch user, by <c>[Display]</c> and by <c>[Localized]</c>.
/// </summary>
/// <remarks>
/// <c>[Display]</c> is the attribute .NET itself has for a label. It isn't part of Idioma. It's what Idioma is
/// compared with here, as <c>IStringLocalizer</c> is in <see cref="TextLookup"/>. Its wording comes from the same
/// resource file. The other rows are the ways <see cref="TextLookup"/> doesn't time: a text found by a property the
/// code holds, the text of a type, and the text of a value of an enum.
/// </remarks>
[MemoryDiagnoser]
[HideColumns("Median", "RatioSD", "Alloc Ratio")]
[SimpleJob(launchCount: 1, warmupCount: 5, iterationCount: 15)]
public class LabelLookup
{
    private static readonly PropertyInfo Checkout = typeof(LabelledOrder).GetProperty(nameof(LabelledOrder.Checkout))!;

    // Kept, as a framework that shows forms keeps what it read of a model.
    private static readonly DisplayAttribute CheckoutDisplay = Checkout.GetCustomAttribute<DisplayAttribute>()!;

    // Fields rather than constants, so that no approach is worked out at compile time.
    private readonly ParcelStatus parcelStatus = ParcelStatus.Picked;
    private readonly OrderState orderState = OrderState.Shipped;

    [GlobalSetup]
    public void Setup()
    {
        Culture.Use("nl");
        Verify();
    }

    // [Display] with its attribute kept is the baseline: the least the approach of .NET itself costs.

    [Benchmark(Baseline = true)]
    public string? Display_AttributeKept() => CheckoutDisplay.GetName();

    [Benchmark]
    public string? Display_ReadFromProperty() => Checkout.GetCustomAttribute<DisplayAttribute>()!.GetName();

    [Benchmark]
    public string Localized_PropertyByTypeAndName() => LocalizableText.GetTextByMember<LabelledOrder>(nameof(LabelledOrder.Checkout))!;

    [Benchmark]
    public string Localized_PropertyByPropertyInfo() => LocalizableText.GetTextByMember(Checkout)!;

    [Benchmark]
    public string Localized_Type() => LocalizableText.GetTextOfType<LabelledOrder>()!;

    [Benchmark]
    public string Localized_EnumValue() => this.parcelStatus.GetText();

    [Benchmark]
    public string SetOfEnum_EnumValue() => this.orderState.GetText();

    /// <summary>
    /// Throws when an approach shows other wording than expected for a Dutch user, so that no approach is timed doing
    /// less work than the others.
    /// </summary>
    public static void Verify()
    {
        var benchmark = new LabelLookup();

        (string Approach, string? Shown, string Expected)[] shown =
        [
            (nameof(Display_AttributeKept), benchmark.Display_AttributeKept(), "Afrekenen"),
            (nameof(Display_ReadFromProperty), benchmark.Display_ReadFromProperty(), "Afrekenen"),
            (nameof(Localized_PropertyByTypeAndName), benchmark.Localized_PropertyByTypeAndName(), "Afrekenen"),
            (nameof(Localized_PropertyByPropertyInfo), benchmark.Localized_PropertyByPropertyInfo(), "Afrekenen"),
            (nameof(Localized_Type), benchmark.Localized_Type(), "Bestelling"),
            (nameof(Localized_EnumValue), benchmark.Localized_EnumValue(), "Gepickt"),
            (nameof(SetOfEnum_EnumValue), benchmark.SetOfEnum_EnumValue(), "Verzonden"),
        ];

        foreach (var (approach, wording, expected) in shown)
            if (!string.Equals(wording, expected, StringComparison.Ordinal))
                throw new InvalidOperationException($"{approach} shows \"{wording}\" for a Dutch user, where \"{expected}\" is expected.");
    }
}

/// <summary>
/// The wording <c>[Display]</c> reads: static properties that read the shop's resource file, as the class resx
/// generates has them. It's the file <c>IStringLocalizer&lt;Shop&gt;</c> reads too.
/// </summary>
public static class DisplayLabels
{
    private static readonly ResourceManager Resources = new(typeof(Shop));

    public static string Checkout => Resources.GetString(nameof(Checkout), CultureInfo.CurrentUICulture)!;
}

// A model with the same label written both ways: for .NET's own [Display], and for Idioma's [Localized].
[Localized(en: "Order", nl: "Bestelling", enGb: "Order")]
public class LabelledOrder
{
    [Display(Name = nameof(DisplayLabels.Checkout), ResourceType = typeof(DisplayLabels))]
    [Localized(en: "Checkout", nl: "Afrekenen", enGb: "Checkout")]
    public string Checkout { get; set; } = "";
}

public enum ParcelStatus
{
    [Localized(en: "Assigned", nl: "Toegewezen", enGb: "Assigned")]
    Assigned,

    [Localized(en: "Picked", nl: "Gepickt", enGb: "Picked")]
    Picked,
}

public enum OrderState { Pending, Shipped }

public class OrderStateLocalizationEn : ILocalizationSet<OrderState>, IOrderStateLocalization
{
    public string Pending => "Pending";
    public string Shipped => "Shipped";
}

public class OrderStateLocalizationEnGb : OrderStateLocalizationEn;

public class OrderStateLocalizationNl : ILocalizationSet<OrderState>, IOrderStateLocalization
{
    public string Pending => "In behandeling";
    public string Shipped => "Verzonden";
}
