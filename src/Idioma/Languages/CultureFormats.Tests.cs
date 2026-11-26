using System.Reflection;
using System.Runtime.CompilerServices;

namespace CodeChops.Idioma.Tests;

/// <summary>
/// Tests for <see cref="CultureFormats"/>: two culture objects of one name are alike where they format values alike, the
/// settings of their calendars included.
/// </summary>
public sealed class CultureFormatsTests
{
    #region Fixture
    /// <summary>
    /// A Gregorian calendar that shows the day after a date, as a calendar of a class outside .NET can.
    /// </summary>
    private sealed class DayLaterCalendar : GregorianCalendar
    {
        public override int GetDayOfMonth(DateTime time) => base.GetDayOfMonth(time.AddDays(1));
    }

    private static DateTime Date { get; } = new(2026, 11, 26);

    /// <summary>
    /// A Saudi culture object of its own whose dates follow the Hijri calendar, moved by the given number of days.
    /// </summary>
    private static CultureInfo HijriCulture(int adjustment)
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("ar-SA").Clone();
        culture.DateTimeFormat.Calendar = new HijriCalendar { HijriAdjustment = adjustment };
        return culture;
    }

    private static CultureInfo CultureWithCalendar(string name, Calendar calendar)
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo(name).Clone();
        culture.DateTimeFormat.Calendar = calendar;
        return culture;
    }

    private static object FormatOf(CultureInfo culture, Type formatType) => formatType == typeof(NumberFormatInfo) ? culture.NumberFormat
        : formatType == typeof(DateTimeFormatInfo) ? culture.DateTimeFormat
        : formatType == typeof(TextInfo) ? culture.TextInfo
        : throw new ArgumentOutOfRangeException(nameof(formatType), formatType, null);

    /// <summary>
    /// A value the setting accepts, other than the one it has.
    /// </summary>
    private static object OtherValue(PropertyInfo setting, object value) => value switch
    {
        // Patterns and numbers of digits start at 0.
        int number => number is 0 ? 1 : number - 1,
        string text => text + "x",
        int[] groupSizes => groupSizes.SequenceEqual([2]) ? new[] { 4 } : new[] { 2 },
        string[] digits when String.Equals(setting.Name, nameof(NumberFormatInfo.NativeDigits), StringComparison.Ordinal) => Enumerable.Range(0, 10)
            .Select(digit => ((char)((String.Equals(digits[0], "0", StringComparison.Ordinal) ? '٠' : '0') + digit)).ToString())
            .ToArray(),
        string[] names => names.Select((name, index) => index is 0 ? name + "x" : name).ToArray(),
        Enum choice => Enum.GetValues(choice.GetType()).Cast<object>().First(other => !other.Equals(choice)),
        _ => throw new InvalidOperationException($"No other value for {setting.DeclaringType!.Name}.{setting.Name}, a {setting.PropertyType.Name}."),
    };

    /// <summary>
    /// The public settings of a type and of those it derives from, by the type that first declares each.
    /// </summary>
    private static IEnumerable<string> SettingsOf(Type type) => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(setting => setting.SetMethod is { IsPublic: true })
        .Select(setting => $"{setting.GetMethod!.GetBaseDefinition().DeclaringType!.Name}.{setting.Name}");
    #endregion

    [Test]
    public void AreAlike_WithObjectsOfOneName_ShouldBeTrue()
    {
        // Arrange & Act & Assert: neither takes the settings of the machine's user.
        CultureFormats.AreAlike(new CultureInfo("nl-NL", useUserOverride: false), CultureInfo.GetCultureInfo("nl-NL")).ShouldBeTrue();
        CultureFormats.AreAlike(HijriCulture(1), HijriCulture(1)).ShouldBeTrue();
    }

    [Test]
    public void AreAlike_WithAnySettingOfFormatsChanged_ShouldBeFalse()
    {
        // Arrange: the settings are found by reflection, so a setting .NET adds is tested too. The calendar is tested
        // below, because en-US offers only the Gregorian one.
        var american = CultureInfo.GetCultureInfo("en-US");
        var settings = new[] { typeof(NumberFormatInfo), typeof(DateTimeFormatInfo), typeof(TextInfo) }
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(setting => setting.SetMethod is { IsPublic: true } && setting.PropertyType != typeof(Calendar))
            .ToList();
        var missed = new List<string>();

        // Act
        foreach (var setting in settings)
        {
            var changed = (CultureInfo)american.Clone();
            var format = FormatOf(changed, setting.DeclaringType!);
            setting.SetValue(format, OtherValue(setting, setting.GetValue(format)!));

            if (CultureFormats.AreAlike(changed, american))
                missed.Add($"{setting.DeclaringType!.Name}.{setting.Name}");
        }

        // Assert
        settings.Count.ShouldBe(27 + 20 + 1);
        missed.ShouldBeEmpty();
    }

    [Test]
    public void AreAlike_WithAnyPatternsOfSetAllDateTimePatterns_ShouldBeFalse()
    {
        // Arrange
        var american = CultureInfo.GetCultureInfo("en-US");
        var missed = new List<char>();

        // Act: the first pattern becomes the one a format specifier gives.
        foreach (var specifier in "dDtTyY")
        {
            var changed = (CultureInfo)american.Clone();
            changed.DateTimeFormat.SetAllDateTimePatterns([american.DateTimeFormat.GetAllDateTimePatterns(specifier)[0] + " 'x'"], specifier);

            if (CultureFormats.AreAlike(changed, american))
                missed.Add(specifier);
        }

        // Assert
        missed.ShouldBeEmpty();
    }

    [Test]
    public void AreAlike_WithOtherCalendarOrTwoDigitYearMax_ShouldBeFalse()
    {
        // Arrange & Act & Assert: Saudi dates follow the Umm al-Qura calendar, unless the Hijri calendar is set.
        CultureFormats.AreAlike(CultureWithCalendar("ar-SA", new HijriCalendar()), CultureInfo.GetCultureInfo("ar-SA")).ShouldBeFalse();
        CultureFormats.AreAlike(CultureWithCalendar("en-US", new GregorianCalendar { TwoDigitYearMax = 2030 }), CultureInfo.GetCultureInfo("en-US")).ShouldBeFalse();
        CultureFormats.AreAlike(CultureWithCalendar("en-US", new GregorianCalendar()), CultureInfo.GetCultureInfo("en-US")).ShouldBeTrue();
    }

    [Test]
    public void Settings_BesidesThoseOfFormats_ShouldBeTheOnesCultureFormatsCompares()
    {
        // Arrange: a setting or method .NET adds fails this test until CultureFormats compares what it changes. A void
        // method is taken to change something.
        var types = new[] { typeof(CultureInfo), typeof(NumberFormatInfo), typeof(DateTimeFormatInfo), typeof(TextInfo), typeof(CompareInfo) }
            .Concat(typeof(Calendar).Assembly.GetExportedTypes().Where(type => typeof(Calendar).IsAssignableFrom(type)))
            .ToList();

        // Act
        var settings = new[] { typeof(CultureInfo), typeof(TextInfo), typeof(CompareInfo) }
            .Concat(types.Where(type => typeof(Calendar).IsAssignableFrom(type)))
            .SelectMany(SettingsOf)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);
        var methods = types
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(method => !method.IsSpecialName && method.ReturnType == typeof(void))
            .Select(method => $"{method.DeclaringType!.Name}.{method.Name}")
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        // Assert: ClearCachedData resets what the process caches, not the culture it's called on.
        settings.ShouldBe([
            "Calendar.TwoDigitYearMax",
            "CultureInfo.DateTimeFormat",
            "CultureInfo.NumberFormat",
            "GregorianCalendar.CalendarType",
            "HijriCalendar.HijriAdjustment",
            "TextInfo.ListSeparator",
        ]);
        methods.ShouldBe(["CultureInfo.ClearCachedData", "DateTimeFormatInfo.SetAllDateTimePatterns"]);
    }

    [Test]
    public void AreAlike_WithOtherHijriAdjustment_ShouldBeFalse()
    {
        // Arrange: the adjustment moves the day a date shows, though the names and patterns stay the same.
        var unadjusted = HijriCulture(0);
        var adjusted = HijriCulture(1);
        Date.ToString("d MMMM", adjusted).ShouldNotBe(Date.ToString("d MMMM", unadjusted), StringComparer.Ordinal);

        // Act & Assert
        CultureFormats.AreAlike(adjusted, unadjusted).ShouldBeFalse();
    }

    [Test]
    public void AreAlike_WithCalendarOfOwnClass_ShouldBeAlikeOnlyToItself()
    {
        // Arrange
        var calendar = new DayLaterCalendar();
        var first = CultureWithCalendar("en-US", calendar);
        Date.ToString("d", first).ShouldBe("11/27/2026");

        // Act & Assert: another calendar of that class may count otherwise, as may the culture's own.
        CultureFormats.AreAlike(first, first).ShouldBeTrue();
        CultureFormats.AreAlike(first, CultureWithCalendar("en-US", new DayLaterCalendar())).ShouldBeFalse();
        CultureFormats.AreAlike(first, CultureInfo.GetCultureInfo("en-US")).ShouldBeFalse();
        CultureFormats.AreAlike(first, CultureWithCalendar("en-US", calendar)).ShouldBeFalse();
    }

    [Test]
    public void AreAlike_WithCalendarOfOwnClass_ShouldNotKeepCalendar()
    {
        // Arrange & Act: the calendar's class may come from a plugin, which a kept answer would keep loaded.
        var calendar = CompareCultureWithCalendarOfOwnClass();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Assert
        calendar.IsAlive.ShouldBeFalse();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CompareCultureWithCalendarOfOwnClass()
    {
        var calendar = new DayLaterCalendar();
        CultureFormats.AreAlike(CultureWithCalendar("en-US", calendar), CultureWithCalendar("en-US", calendar)).ShouldBeFalse();
        return new WeakReference(calendar);
    }
}
