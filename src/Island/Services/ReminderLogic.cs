using System;
using System.Globalization;
using System.Linq;
using Island.Services;

namespace Island.Services;

public static class ReminderLogic
{
    public static bool TryParseTime(string? text, out TimeSpan time)
    {
        time = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (!TimeOnly.TryParse(text.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) &&
            !TimeOnly.TryParse(text.Trim(), CultureInfo.GetCultureInfo("ru-RU"), DateTimeStyles.None, out parsed))
            return false;
        time = parsed.ToTimeSpan();
        return true;
    }

    public static bool TryParseDate(string? text, out DateTime date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var formats = new[] { "yyyy-MM-dd", "dd.MM.yyyy", "d.M.yyyy" };
        return DateTime.TryParseExact(text.Trim(), formats, CultureInfo.InvariantCulture,
                   DateTimeStyles.None, out date) ||
               DateTime.TryParse(text.Trim(), CultureInfo.GetCultureInfo("ru-RU"), DateTimeStyles.None, out date);
    }

    public static DateTime? LastOccurrence(ReminderItem reminder, DateTime now)
    {
        if (!reminder.Enabled) return null;
        if (!TryParseTime(reminder.Time, out var time)) return null;

        if (!string.IsNullOrWhiteSpace(reminder.SnoozeUntil) &&
            DateTime.TryParse(reminder.SnoozeUntil, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var snooze) &&
            snooze <= now && snooze > now.AddHours(-4))
        {
            return snooze;
        }

        switch (reminder.Repeat)
        {
            case "Hourly":
            {
                var candidate = now.Date + new TimeSpan(now.Hour, time.Minutes, 0);
                if (candidate > now) candidate = candidate.AddHours(-1);
                return candidate;
            }
            case "Daily":
            {
                var candidate = now.Date + time;
                if (candidate > now) candidate = candidate.AddDays(-1);
                return candidate;
            }
            case "Weekly":
            {
                for (var i = 0; i <= 7; i++)
                {
                    var day = now.Date.AddDays(-i);
                    var candidate = day + time;
                    if (candidate > now) continue;
                    if ((reminder.Weekdays & WeekdayFlag(day.DayOfWeek)) == 0) continue;
                    return candidate;
                }
                return null;
            }
            default:
            {
                if (!TryParseDate(reminder.Date, out var date)) return null;
                var candidate = date.Date + time;
                return candidate <= now ? candidate : null;
            }
        }
    }

    public static int WeekdayFlag(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => 1,
        DayOfWeek.Tuesday => 2,
        DayOfWeek.Wednesday => 4,
        DayOfWeek.Thursday => 8,
        DayOfWeek.Friday => 16,
        DayOfWeek.Saturday => 32,
        _ => 64
    };

    public static string WeekdayShort(DayOfWeek day) => L.T(day switch
    {
        DayOfWeek.Monday => "WeekdayMon",
        DayOfWeek.Tuesday => "WeekdayTue",
        DayOfWeek.Wednesday => "WeekdayWed",
        DayOfWeek.Thursday => "WeekdayThu",
        DayOfWeek.Friday => "WeekdayFri",
        DayOfWeek.Saturday => "WeekdaySat",
        _ => "WeekdaySun"
    });

    public static string Describe(ReminderItem reminder)
    {
        TryParseTime(reminder.Time, out var time);
        var clock = DateTime.Today.Add(time).ToString("HH:mm", CultureInfo.InvariantCulture);

        return reminder.Repeat switch
        {
            "Hourly" => L.F("EveryHourAt", clock.Substring(3)),
            "Daily" => L.F("EveryDayAt", clock),
            "Weekly" => L.F("WeeklyOn", WeekdaysText(reminder.Weekdays)),
            _ => string.IsNullOrWhiteSpace(reminder.Date) ? clock : L.F("OnceAt", reminder.Date + " " + clock)
        };
    }

    public static string WeekdaysText(int mask)
    {
        var days = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday }
            .Where(d => (mask & WeekdayFlag(d)) != 0)
            .Select(WeekdayShort);
        return string.Join(" ", days);
    }
}
