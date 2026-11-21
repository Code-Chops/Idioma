// Generated: 100 texts a shop shows, the same in each file of this app. Only their size is measured.

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Localization;

namespace Benchmarks;

/// <summary>
/// Shows every text of the resx files through <c>IStringLocalizer</c>.
/// </summary>
public sealed class LocalizerStorePage : ComponentBase
{
    [Inject]
    private IStringLocalizer<Store> Localizer { get; set; } = null!;

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.AddContent(0, this.Localizer["AddToCart"].Value);
        builder.AddContent(1, this.Localizer["RemoveFromCart"].Value);
        builder.AddContent(2, this.Localizer["Checkout"].Value);
        builder.AddContent(3, this.Localizer["ContinueShopping"].Value);
        builder.AddContent(4, this.Localizer["YourCart"].Value);
        builder.AddContent(5, this.Localizer["CartIsEmpty"].Value);
        builder.AddContent(6, this.Localizer["Subtotal"].Value);
        builder.AddContent(7, this.Localizer["Shipping"].Value);
        builder.AddContent(8, this.Localizer["Total"].Value);
        builder.AddContent(9, this.Localizer["FreeShipping"].Value);
        builder.AddContent(10, this.Localizer["PlaceOrder"].Value);
        builder.AddContent(11, this.Localizer["OrderConfirmed"].Value);
        builder.AddContent(12, this.Localizer["OrderNumber"].Value);
        builder.AddContent(13, this.Localizer["TrackOrder"].Value);
        builder.AddContent(14, this.Localizer["Delivery"].Value);
        builder.AddContent(15, this.Localizer["DeliveryAddress"].Value);
        builder.AddContent(16, this.Localizer["BillingAddress"].Value);
        builder.AddContent(17, this.Localizer["SameAsDeliveryAddress"].Value);
        builder.AddContent(18, this.Localizer["FirstName"].Value);
        builder.AddContent(19, this.Localizer["LastName"].Value);
        builder.AddContent(20, this.Localizer["EmailAddress"].Value);
        builder.AddContent(21, this.Localizer["PhoneNumber"].Value);
        builder.AddContent(22, this.Localizer["StreetAndNumber"].Value);
        builder.AddContent(23, this.Localizer["PostalCode"].Value);
        builder.AddContent(24, this.Localizer["City"].Value);
        builder.AddContent(25, this.Localizer["Country"].Value);
        builder.AddContent(26, this.Localizer["Payment"].Value);
        builder.AddContent(27, this.Localizer["PaymentMethod"].Value);
        builder.AddContent(28, this.Localizer["CreditCard"].Value);
        builder.AddContent(29, this.Localizer["PayNow"].Value);
        builder.AddContent(30, this.Localizer["PaymentFailed"].Value);
        builder.AddContent(31, this.Localizer["SignIn"].Value);
        builder.AddContent(32, this.Localizer["SignOut"].Value);
        builder.AddContent(33, this.Localizer["CreateAccount"].Value);
        builder.AddContent(34, this.Localizer["ForgotPassword"].Value);
        builder.AddContent(35, this.Localizer["Password"].Value);
        builder.AddContent(36, this.Localizer["RememberMe"].Value);
        builder.AddContent(37, this.Localizer["MyAccount"].Value);
        builder.AddContent(38, this.Localizer["MyOrders"].Value);
        builder.AddContent(39, this.Localizer["Wishlist"].Value);
        builder.AddContent(40, this.Localizer["AddToWishlist"].Value);
        builder.AddContent(41, this.Localizer["Search"].Value);
        builder.AddContent(42, this.Localizer["SearchProducts"].Value);
        builder.AddContent(43, this.Localizer["NoResults"].Value);
        builder.AddContent(44, this.Localizer["Filter"].Value);
        builder.AddContent(45, this.Localizer["SortBy"].Value);
        builder.AddContent(46, this.Localizer["PriceLowToHigh"].Value);
        builder.AddContent(47, this.Localizer["PriceHighToLow"].Value);
        builder.AddContent(48, this.Localizer["Newest"].Value);
        builder.AddContent(49, this.Localizer["BestSellers"].Value);
        builder.AddContent(50, this.Localizer["InStock"].Value);
        builder.AddContent(51, this.Localizer["OutOfStock"].Value);
        builder.AddContent(52, this.Localizer["OnlyAFewLeft"].Value);
        builder.AddContent(53, this.Localizer["Size"].Value);
        builder.AddContent(54, this.Localizer["Color"].Value);
        builder.AddContent(55, this.Localizer["Quantity"].Value);
        builder.AddContent(56, this.Localizer["ProductDetails"].Value);
        builder.AddContent(57, this.Localizer["Description"].Value);
        builder.AddContent(58, this.Localizer["Reviews"].Value);
        builder.AddContent(59, this.Localizer["WriteReview"].Value);
        builder.AddContent(60, this.Localizer["Rating"].Value);
        builder.AddContent(61, this.Localizer["RecommendedForYou"].Value);
        builder.AddContent(62, this.Localizer["RecentlyViewed"].Value);
        builder.AddContent(63, this.Localizer["Sale"].Value);
        builder.AddContent(64, this.Localizer["NewArrivals"].Value);
        builder.AddContent(65, this.Localizer["Categories"].Value);
        builder.AddContent(66, this.Localizer["AllProducts"].Value);
        builder.AddContent(67, this.Localizer["Home"].Value);
        builder.AddContent(68, this.Localizer["ContactUs"].Value);
        builder.AddContent(69, this.Localizer["CustomerService"].Value);
        builder.AddContent(70, this.Localizer["Returns"].Value);
        builder.AddContent(71, this.Localizer["ReturnPolicy"].Value);
        builder.AddContent(72, this.Localizer["FrequentlyAskedQuestions"].Value);
        builder.AddContent(73, this.Localizer["AboutUs"].Value);
        builder.AddContent(74, this.Localizer["PrivacyPolicy"].Value);
        builder.AddContent(75, this.Localizer["TermsAndConditions"].Value);
        builder.AddContent(76, this.Localizer["CookieNotice"].Value);
        builder.AddContent(77, this.Localizer["AcceptCookies"].Value);
        builder.AddContent(78, this.Localizer["NewsletterSignUp"].Value);
        builder.AddContent(79, this.Localizer["Subscribe"].Value);
        builder.AddContent(80, this.Localizer["Unsubscribe"].Value);
        builder.AddContent(81, this.Localizer["Save"].Value);
        builder.AddContent(82, this.Localizer["Cancel"].Value);
        builder.AddContent(83, this.Localizer["Delete"].Value);
        builder.AddContent(84, this.Localizer["Edit"].Value);
        builder.AddContent(85, this.Localizer["Close"].Value);
        builder.AddContent(86, this.Localizer["Back"].Value);
        builder.AddContent(87, this.Localizer["Next"].Value);
        builder.AddContent(88, this.Localizer["Previous"].Value);
        builder.AddContent(89, this.Localizer["Loading"].Value);
        builder.AddContent(90, this.Localizer["SomethingWentWrong"].Value);
        builder.AddContent(91, this.Localizer["TryAgain"].Value);
        builder.AddContent(92, this.Localizer["ChangesSaved"].Value);
        builder.AddContent(93, this.Localizer["RequiredField"].Value);
        builder.AddContent(94, this.Localizer["InvalidEmailAddress"].Value);
        builder.AddContent(95, this.Localizer["PasswordTooShort"].Value);
        builder.AddContent(96, this.Localizer["Language"].Value);
        builder.AddContent(97, this.Localizer["Currency"].Value);
        builder.AddContent(98, this.Localizer["Notifications"].Value);
        builder.AddContent(99, this.Localizer["Settings"].Value);
    }
}
