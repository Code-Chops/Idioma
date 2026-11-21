#:package CodeChops.Idioma@*

using System.Globalization;
using CodeChops.Idioma;

[assembly: SupportedLanguages("en", "nl")] // The first is the default

const OrderStatus status = OrderStatus.Shipped;

// The supported languages, and German, which isn't one of them. A German user sees English, the default.
foreach (var culture in LocalizationRegistry.SupportedLanguages.Append("de-DE"))
{
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);

    // status.GetText() is short for OrderStatusLocalization.GetTextByValue(status).
    Console.WriteLine($"{culture}: {OrderStatusLocalization.Caption}: {status.GetText()}");
}

// A set of an enum is a set like any other, with a text for each member of the enum as well.
// Add a member, and it doesn't build until English and Dutch have a text for it.
public enum OrderStatus { Pending, Shipped }

// English: the default. The source generator writes two types from this set:
// - the contract IOrderStatusLocalization, which every set implements.
// - the class OrderStatusLocalization, which shows the texts. It also gives OrderStatus its GetText().
public class OrderStatusLocalizationEn : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
{
    public string Caption => "Order status"; // A text of its own, not a member of the enum
    public string Pending => "Pending";
    public string Shipped => "Shipped";
}

// Dutch
public class OrderStatusLocalizationNl : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
{
    public string Caption => "Bestelstatus";
    public string Pending => "In behandeling";
    public string Shipped => "Verzonden";
}
