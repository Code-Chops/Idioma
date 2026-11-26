namespace CodeChops.Idioma;

/// <summary>
/// Tells whether two cultures format values alike, so that a text whose values are to be formatted for one of them
/// needn't switch the thread to it where the thread has the other.
/// </summary>
/// <remarks>
/// <para>
/// A server that sets the culture of each request, as ASP.NET Core's request localization does, gives the thread a
/// culture object of its own: a read-only copy, made for each request, of the culture in its options. A
/// <see cref="LocalizationRegistry.CurrentUICultureProvider"/> may give another object of that name, such as one of
/// <see cref="CultureInfo.GetCultureInfo(string)"/>. Two such objects format alike, unless one of them was changed or
/// takes the settings of the machine's user. So they're compared by what a method reads to format a value: the format
/// of numbers, that of dates and the list separator.
/// </para>
/// <para>
/// A culture of a derived class is alike only to itself. It may format through its own
/// <see cref="CultureInfo.GetFormat(Type)"/>, which the properties compared here don't show.
/// A culture whose calendar is of a class outside .NET is alike only to itself too, because that calendar may count
/// dates its own way.
/// </para>
/// <para>
/// A thread keeps its last few answers, because the texts of a request ask about the same two cultures each time. So a
/// culture whose format is changed after it's in use may keep the answer from before. A culture is set up before it's
/// used, so that doesn't happen in practice.
/// </para>
/// </remarks>
// Before this class, the check compared references. So a provider that gives another object of the thread's culture
// sent every text with parameters down the slower path, which enters a scope. Such a text took 86 ns. It takes 28 ns
// with this class. One whose provider gives the thread's own object takes 13 ns (measured 2026-11-26).
// CultureInfo.Equals can't stand in for this class, because it compares the name and the CompareInfo, not the formats.
// A check of UseUserOverride can't either. Request localization's culture takes the settings of the machine's user and
// GetCultureInfo's doesn't, so that check would always fail. IsReadOnly can't tell, because the read-only copy request
// localization makes keeps any change made to the culture in its options. The answers are kept, because the
// properties that give names and group sizes copy their arrays. Comparing them on every text allocates some 20 arrays.
internal static class CultureFormats
{
    private const int Kept = 4;

    [ThreadStatic]
    private static Answer?[]? Answers;

    [ThreadStatic]
    private static int NextAnswer;

    /// <summary>
    /// Whether values are formatted alike for two cultures: the same object, or two <see cref="CultureInfo"/> objects of
    /// one name whose formats of numbers and dates and whose list separators are equal.
    /// </summary>
    public static bool AreAlike(CultureInfo first, CultureInfo second)
    {
        if (ReferenceEquals(first, second))
            return true;

        if (first.GetType() != typeof(CultureInfo) || second.GetType() != typeof(CultureInfo))
            return false;

        if (!String.Equals(first.Name, second.Name, StringComparison.Ordinal))
            return false;

        var answers = Answers ??= new Answer?[Kept];

        foreach (var answer in answers)
            if (answer is not null && ReferenceEquals(answer.First, first) && ReferenceEquals(answer.Second, second))
                return answer.AreAlike;

        // Such a calendar may come from a plugin, which a kept answer would keep loaded. So its answer isn't kept.
        if (!IsOfDotNet(first.DateTimeFormat.Calendar) || !IsOfDotNet(second.DateTimeFormat.Calendar))
            return false;

        var areAlike = NumberFormatsAreAlike(first.NumberFormat, second.NumberFormat)
                       && DateFormatsAreAlike(first.DateTimeFormat, second.DateTimeFormat)
                       && String.Equals(first.TextInfo.ListSeparator, second.TextInfo.ListSeparator, StringComparison.Ordinal);

        answers[NextAnswer] = new Answer(first, second, areAlike);
        NextAnswer = (NextAnswer + 1) % Kept;

        return areAlike;
    }

    private static bool NumberFormatsAreAlike(NumberFormatInfo first, NumberFormatInfo second)
    {
        if (ReferenceEquals(first, second))
            return true;

        var areAlike = first.CurrencyDecimalDigits == second.CurrencyDecimalDigits
                       && String.Equals(first.CurrencyDecimalSeparator, second.CurrencyDecimalSeparator, StringComparison.Ordinal)
                       && String.Equals(first.CurrencyGroupSeparator, second.CurrencyGroupSeparator, StringComparison.Ordinal)
                       && first.CurrencyGroupSizes.AsSpan().SequenceEqual(second.CurrencyGroupSizes)
                       && first.CurrencyNegativePattern == second.CurrencyNegativePattern
                       && first.CurrencyPositivePattern == second.CurrencyPositivePattern
                       && String.Equals(first.CurrencySymbol, second.CurrencySymbol, StringComparison.Ordinal)
                       && first.DigitSubstitution == second.DigitSubstitution
                       && String.Equals(first.NaNSymbol, second.NaNSymbol, StringComparison.Ordinal)
                       && first.NativeDigits.AsSpan().SequenceEqual(second.NativeDigits)
                       && String.Equals(first.NegativeInfinitySymbol, second.NegativeInfinitySymbol, StringComparison.Ordinal)
                       && String.Equals(first.NegativeSign, second.NegativeSign, StringComparison.Ordinal)
                       && first.NumberDecimalDigits == second.NumberDecimalDigits
                       && String.Equals(first.NumberDecimalSeparator, second.NumberDecimalSeparator, StringComparison.Ordinal)
                       && String.Equals(first.NumberGroupSeparator, second.NumberGroupSeparator, StringComparison.Ordinal)
                       && first.NumberGroupSizes.AsSpan().SequenceEqual(second.NumberGroupSizes)
                       && first.NumberNegativePattern == second.NumberNegativePattern
                       && first.PercentDecimalDigits == second.PercentDecimalDigits
                       && String.Equals(first.PercentDecimalSeparator, second.PercentDecimalSeparator, StringComparison.Ordinal)
                       && String.Equals(first.PercentGroupSeparator, second.PercentGroupSeparator, StringComparison.Ordinal)
                       && first.PercentGroupSizes.AsSpan().SequenceEqual(second.PercentGroupSizes)
                       && first.PercentNegativePattern == second.PercentNegativePattern
                       && first.PercentPositivePattern == second.PercentPositivePattern
                       && String.Equals(first.PercentSymbol, second.PercentSymbol, StringComparison.Ordinal)
                       && String.Equals(first.PerMilleSymbol, second.PerMilleSymbol, StringComparison.Ordinal)
                       && String.Equals(first.PositiveInfinitySymbol, second.PositiveInfinitySymbol, StringComparison.Ordinal)
                       && String.Equals(first.PositiveSign, second.PositiveSign, StringComparison.Ordinal);

        return areAlike;
    }

    private static bool DateFormatsAreAlike(DateTimeFormatInfo first, DateTimeFormatInfo second)
    {
        if (ReferenceEquals(first, second))
            return true;

        var areAlike = CalendarsAreAlike(first.Calendar, second.Calendar)
                       && first.CalendarWeekRule == second.CalendarWeekRule
                       && first.FirstDayOfWeek == second.FirstDayOfWeek
                       && String.Equals(first.AMDesignator, second.AMDesignator, StringComparison.Ordinal)
                       && String.Equals(first.PMDesignator, second.PMDesignator, StringComparison.Ordinal)
                       && String.Equals(first.DateSeparator, second.DateSeparator, StringComparison.Ordinal)
                       && String.Equals(first.TimeSeparator, second.TimeSeparator, StringComparison.Ordinal)
                       && String.Equals(first.FullDateTimePattern, second.FullDateTimePattern, StringComparison.Ordinal)
                       && String.Equals(first.LongDatePattern, second.LongDatePattern, StringComparison.Ordinal)
                       && String.Equals(first.LongTimePattern, second.LongTimePattern, StringComparison.Ordinal)
                       && String.Equals(first.MonthDayPattern, second.MonthDayPattern, StringComparison.Ordinal)
                       && String.Equals(first.ShortDatePattern, second.ShortDatePattern, StringComparison.Ordinal)
                       && String.Equals(first.ShortTimePattern, second.ShortTimePattern, StringComparison.Ordinal)
                       && String.Equals(first.YearMonthPattern, second.YearMonthPattern, StringComparison.Ordinal)
                       && first.AbbreviatedDayNames.AsSpan().SequenceEqual(second.AbbreviatedDayNames)
                       && first.AbbreviatedMonthGenitiveNames.AsSpan().SequenceEqual(second.AbbreviatedMonthGenitiveNames)
                       && first.AbbreviatedMonthNames.AsSpan().SequenceEqual(second.AbbreviatedMonthNames)
                       && first.DayNames.AsSpan().SequenceEqual(second.DayNames)
                       && first.MonthGenitiveNames.AsSpan().SequenceEqual(second.MonthGenitiveNames)
                       && first.MonthNames.AsSpan().SequenceEqual(second.MonthNames)
                       && first.ShortestDayNames.AsSpan().SequenceEqual(second.ShortestDayNames);

        return areAlike;
    }

    // A calendar's settings change the date it shows, though the names and patterns stay the same. HijriAdjustment moves
    // the day. CalendarType picks the names of a Gregorian calendar where the culture offers more than one, as on
    // Windows. ICU offers only Localized.
    private static bool CalendarsAreAlike(Calendar first, Calendar second)
    {
        if (ReferenceEquals(first, second))
            return true;

        if (first.GetType() != second.GetType())
            return false;

        var areAlike = first.TwoDigitYearMax == second.TwoDigitYearMax
                       && first switch
                       {
                           GregorianCalendar gregorian => gregorian.CalendarType == ((GregorianCalendar)second).CalendarType,
                           HijriCalendar hijri => hijri.HijriAdjustment == ((HijriCalendar)second).HijriAdjustment,
                           _ => true,
                       };

        return areAlike;
    }

    private static bool IsOfDotNet(Calendar calendar) => calendar.GetType().Assembly == typeof(Calendar).Assembly;

    /// <summary>
    /// An answer a thread keeps, for two cultures as objects.
    /// </summary>
    private sealed class Answer(CultureInfo first, CultureInfo second, bool areAlike)
    {
        public CultureInfo First { get; } = first;
        public CultureInfo Second { get; } = second;
        public bool AreAlike { get; } = areAlike;
    }
}
