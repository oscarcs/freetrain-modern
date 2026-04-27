namespace FreeTrain.Modern;

public enum ModernSeason : byte
{
    Spring = 0,
    Summer = 1,
    Autumn = 2,
    Winter = 3
}

public enum ModernDayNight : byte
{
    DayTime = 0,
    Night = 1
}

public enum ModernTextLanguage
{
    English,
    Japanese
}

public sealed record ModernWorldClock(int Year, int Month, int Day, int Hour, int Minute)
{
    public const long MinuteLength = 1;
    public const long HourLength = MinuteLength * 60;
    public const long DayLength = HourLength * 24;
    public const long YearLength = DayLength * 365;

    private static readonly int[] DaysOfMonth = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
    private static readonly string[] EnglishDayOfWeek = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
    private static readonly string[] JapaneseDayOfWeek = { "日", "月", "火", "水", "木", "金", "土" };

    public static long StartMinute => DaysOfMonth.Take(3).Sum() * DayLength + 8 * HourLength;
    public static ModernWorldClock Default => FromAbsoluteMinute(StartMinute);

    public long AbsoluteMinute => ToAbsoluteMinute(Year, Month, Day, Hour, Minute);
    public long TotalMinutes => AbsoluteMinute - StartMinute;
    public int DayOfWeek => (int)((AbsoluteMinute / DayLength) % 7);
    public ModernDayNight DayOrNight => Hour is >= 6 and < 18 ? ModernDayNight.DayTime : ModernDayNight.Night;
    public ModernSeason Season => (ModernSeason)(((Month + 9) % 12) / 3);
    public bool IsWeekend => DayOfWeek is 0 or 6;

    public bool IsHoliday =>
        Month == 1 && Day is 1 or 2 or 3 or 15
        || Month == 2 && Day == 11
        || Month == 3 && Day == 21
        || Month == 4 && Day == 29
        || Month == 5 && Day is 3 or 4 or 5
        || Month == 7 && Day == 20
        || Month == 9 && Day is 15 or 23
        || Month == 10 && Day == 10
        || Month == 11 && Day is 3 or 23
        || Month == 12 && Day is 23 or 30 or 31;

    public bool IsVacation =>
        Month == 1 && Day < 6
        || Month == 3 && Day > 24
        || Month == 4 && Day < 6
        || Month == 7 && Day > 24
        || Month == 8
        || Month == 12 && Day > 24;

    public ModernWorldClock AdvanceMinutes(long minutes)
    {
        return FromAbsoluteMinute(AbsoluteMinute + minutes);
    }

    public string Format(ModernTextLanguage language)
    {
        return language == ModernTextLanguage.Japanese
            ? $"{Year}年{Month}月{Day}日({JapaneseDayOfWeek[DayOfWeek]}) {Hour,2:d}時{Minute / 10:d}0分"
            : $"Year {Year}, {Month}/{Day} ({EnglishDayOfWeek[DayOfWeek]}) {Hour:00}:{Minute:00}";
    }

    public static ModernWorldClock FromAbsoluteMinute(long absoluteMinute)
    {
        absoluteMinute = Math.Max(0, absoluteMinute);
        int year = (int)(absoluteMinute / YearLength) + 1;
        long dayOfYear = absoluteMinute / DayLength % 365;
        int month = 1;
        foreach (int days in DaysOfMonth)
        {
            if (dayOfYear < days)
            {
                break;
            }

            dayOfYear -= days;
            month++;
        }

        int day = (int)dayOfYear + 1;
        int hour = (int)(absoluteMinute / HourLength % 24);
        int minute = (int)(absoluteMinute / MinuteLength % 60);
        return new ModernWorldClock(year, month, day, hour, minute);
    }

    private static long ToAbsoluteMinute(int year, int month, int day, int hour, int minute)
    {
        int clampedYear = Math.Max(1, year);
        int clampedMonth = Math.Clamp(month, 1, 12);
        int maxDay = DaysOfMonth[clampedMonth - 1];
        int clampedDay = Math.Clamp(day, 1, maxDay);
        int clampedHour = Math.Clamp(hour, 0, 23);
        int clampedMinute = Math.Clamp(minute, 0, 59);

        long days = (clampedYear - 1) * 365L;
        for (int i = 0; i < clampedMonth - 1; i++)
        {
            days += DaysOfMonth[i];
        }

        days += clampedDay - 1;
        return days * DayLength + clampedHour * HourLength + clampedMinute;
    }
}
