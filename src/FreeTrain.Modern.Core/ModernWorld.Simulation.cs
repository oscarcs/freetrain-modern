namespace FreeTrain.Modern;

public sealed partial class ModernWorld
{
    public void AdvanceClock(long minutes = 1)
    {
        if (minutes <= 0)
        {
            return;
        }

        long previousDay = Clock.AbsoluteMinute / (24 * 60);
        long previousHour = Clock.AbsoluteMinute / 60;
        ModernDayNight previousDayNight = Clock.DayOrNight;
        ModernSeason previousSeason = Clock.Season;
        Clock = Clock.AdvanceMinutes(minutes);
        long currentDay = Clock.AbsoluteMinute / (24 * 60);
        long currentHour = Clock.AbsoluteMinute / 60;
        if (currentHour > previousHour)
        {
            ApplyStationHourlyDecay(currentHour - previousHour);
        }

        if (currentDay > previousDay)
        {
            ApplyStationDailyReset(currentDay - previousDay);
        }

        AdvanceTrains(minutes);
        Publish(ModernWorldChangeKind.Clock, null, "Clock advanced.");

        if (Clock.DayOrNight != previousDayNight || Clock.Season != previousSeason)
        {
            Publish(ModernWorldChangeKind.Reset, null, "Clock changed visual period.");
        }
    }

    public bool Spend(long amount, ModernAccountGenre genre, string description)
    {
        if (amount <= 0)
        {
            return false;
        }

        Account = Account.Spend(amount, genre, Clock, description);
        Publish(ModernWorldChangeKind.Economy, null, description);
        return true;
    }

    public bool Earn(long amount, ModernAccountGenre genre, string description)
    {
        if (amount <= 0)
        {
            return false;
        }

        Account = Account.Earn(amount, genre, Clock, description);
        Publish(ModernWorldChangeKind.Economy, null, description);
        return true;
    }

}
