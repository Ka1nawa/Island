using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Island.Services;

public sealed class ParsedReminder
{
    public bool Ok { get; init; }
    public string Time { get; init; } = "";
    public string Repeat { get; init; } = "Once";
    public int Weekdays { get; init; }
    public string Date { get; init; } = "";
    public string Preview { get; init; } = "";
}

/// <summary>
/// Understands Russian and English schedule phrases:
/// "через 20 минут", "каждый час", "каждый день в 9:30", "в среду в 15:00",
/// "завтра в 10", "in 20 minutes", "every monday at 9".
/// </summary>
public static class NaturalTimeParser
{
    private static readonly string[] RuWeekdays =
    {
        "воскресень", "понедельник", "вторник", "сред", "четверг", "пятниц", "суббот"
    };

    private static readonly string[] EnWeekdays =
    {
        "sunday", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday"
    };

    public static ParsedReminder Parse(string? input, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(input)) return new ParsedReminder();

        var raw = input.Trim();
        var text = raw.ToLowerInvariant().Replace('ё', 'е');
        text = Regex.Replace(text, @"\s+", " ");

        var parsed = ParseRelative(text, now) ?? ParseHourly(text, now) ?? ParseDaily(text, now) ??
                     ParseWeekly(text, now) ?? ParseDayRelative(text, now) ?? ParseBareTime(text, now);

        return parsed ?? new ParsedReminder();
    }

    private static ParsedReminder? ParseRelative(string text, DateTime now)
    {
        Match match;

        if (Regex.IsMatch(text, @"через\s+полчаса|in\s+half\s+an\s+hour"))
            return Once(now.AddMinutes(30), now, L.F("QuickInMinutes", 30, Clock(now.AddMinutes(30))));

        if (Regex.IsMatch(text, @"через\s+час(\s|$)|in\s+an\s+hour|in\s+one\s+hour"))
            return Once(now.AddHours(1), now, L.F("QuickInHours", 1, Clock(now.AddHours(1))));

        match = Regex.Match(text, @"через\s+(\d{1,3})\s*(мин(?:ут[аы]?)?|ч(?:ас(?:а|ов)?)?)");
        if (match.Success)
        {
            var amount = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            if (amount <= 0 || amount > 1000) return null;
            var unit = match.Groups[2].Value;
            var target = unit.StartsWith("мин", StringComparison.Ordinal)
                ? now.AddMinutes(amount)
                : now.AddHours(amount);
            var key = unit.StartsWith("мин", StringComparison.Ordinal) ? "QuickInMinutes" : "QuickInHours";
            return Once(target, now, L.F(key, amount, Clock(target)));
        }

        match = Regex.Match(text, @"in\s+(\d{1,3})\s*(minutes?|mins?|hours?|hrs?|h)");
        if (match.Success)
        {
            var amount = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            if (amount <= 0 || amount > 1000) return null;
            var isMinutes = match.Groups[2].Value.StartsWith("min", StringComparison.Ordinal);
            var target = isMinutes ? now.AddMinutes(amount) : now.AddHours(amount);
            return Once(target, now, L.F(isMinutes ? "QuickInMinutes" : "QuickInHours", amount, Clock(target)));
        }

        return null;
    }

    private static ParsedReminder? ParseHourly(string text, DateTime now)
    {
        if (!Regex.IsMatch(text, @"кажд(ый|ые)\s+час|ежечасно|every\s+hour|hourly")) return null;

        var time = Clock(now);
        return new ParsedReminder
        {
            Ok = true,
            Repeat = "Hourly",
            Time = time,
            Preview = L.F("QuickHourly", time.Substring(3))
        };
    }

    private static ParsedReminder? ParseDaily(string text, DateTime now)
    {
        if (!Regex.IsMatch(text, @"каждый\s+день|ежедневно|daily|every\s+day")) return null;
        if (!TryTime(text, out var hour, out var minute)) return null;

        var time = Format(hour, minute);
        return new ParsedReminder
        {
            Ok = true,
            Repeat = "Daily",
            Time = time,
            Preview = L.F("EveryDayAt", time)
        };
    }

    private static ParsedReminder? ParseWeekly(string text, DateTime now)
    {
        if (Regex.IsMatch(text, @"по\s+будням|weekdays")) 
        {
            var time = TryTime(text, out var h, out var m) ? Format(h, m) : "10:00";
            return new ParsedReminder
            {
                Ok = true,
                Repeat = "Weekly",
                Weekdays = 31,
                Time = time,
                Preview = L.F("QuickWeekly", ReminderLogic.WeekdaysText(31), time)
            };
        }

        var dayIndex = FindWeekday(text);
        if (dayIndex < 0) return null;

        var timeText = TryTime(text, out var hour, out var minute) ? Format(hour, minute) : "10:00";
        var mask = ReminderLogic.WeekdayFlag((DayOfWeek)dayIndex);
        return new ParsedReminder
        {
            Ok = true,
            Repeat = "Weekly",
            Weekdays = mask,
            Time = timeText,
            Preview = L.F("QuickWeekly", ReminderLogic.WeekdaysText(mask), timeText)
        };
    }

    private static ParsedReminder? ParseDayRelative(string text, DateTime now)
    {
        var tomorrow = Regex.IsMatch(text, @"завтра|tomorrow");
        var today = Regex.IsMatch(text, @"сегодня|today");
        if (!tomorrow && !today) return null;
        if (!TryTime(text, out var hour, out var minute)) return null;

        var date = now.Date.AddDays(tomorrow ? 1 : 0);
        var target = date + new TimeSpan(hour, minute, 0);
        return Once(target, now, L.F("OnceAt", target.ToString("dd.MM HH:mm", CultureInfo.InvariantCulture)));
    }

    private static ParsedReminder? ParseBareTime(string text, DateTime now)
    {
        if (!TryTime(text, out var hour, out var minute)) return null;

        var target = now.Date + new TimeSpan(hour, minute, 0);
        if (target <= now) target = target.AddDays(1);
        return Once(target, now, L.F("OnceAt", target.ToString("dd.MM HH:mm", CultureInfo.InvariantCulture)));
    }

    /// <summary>Removes the schedule phrase so "выпить воды через 20 минут" yields "выпить воды".</summary>
    public static string StripSchedule(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";

        var result = text;
        result = Regex.Replace(result, @"через\s+\d{1,3}\s*(мин(?:ут[аы]?)?|ч(?:ас(?:а|ов)?)?)", " ", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"через\s+полчаса|через\s+час", " ", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"кажд(ый|ые)\s+час|ежечасно|every\s+hour|hourly", " ", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"каждый\s+день|ежедневно|daily|every\s+day", " ", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"(?:в|по|on)\s+(?:понедельник\w*|вторник\w*|сред\w*|четверг\w*|пятниц\w*|суббот\w*|воскресень\w*|пн|вт|ср|чт|пт|сб|вс|sunday|monday|tuesday|wednesday|thursday|friday|saturday)", " ", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\b(?:завтра|сегодня|tomorrow|today)\b", " ", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"(?:в|at|к)\s+\d{1,2}(?:[:.]\d{2})?", " ", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\b\d{1,2}[:.]\d{2}\b", " ", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\b(?:in|on|at|every|через|каждый|каждую|по)\b", " ", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\s+", " ").Trim(' ', ',', '.', ';', '-', '—', ':');
        return result;
    }

    private static ParsedReminder Once(DateTime target, DateTime now, string preview) => new()
    {
        Ok = true,
        Repeat = "Once",
        Date = target.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        Time = target.ToString("HH:mm", CultureInfo.InvariantCulture),
        Preview = preview
    };

    private static bool TryTime(string text, out int hour, out int minute)
    {
        hour = 0;
        minute = 0;

        var match = Regex.Match(text, @"(?:в|at|к)\s+(\d{1,2})(?:[:.](\d{2}))?");
        if (!match.Success) match = Regex.Match(text, @"(?:^|\s)(\d{1,2})[:.](\d{2})(?:\s|$)");
        if (!match.Success) match = Regex.Match(text, @"(?:^|\s)(\d{1,2})(?:\s|$)");
        if (!match.Success) return false;

        hour = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        minute = match.Groups[2].Success ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : 0;
        return hour is >= 0 and <= 23 && minute is >= 0 and <= 59;
    }

    private static int FindWeekday(string text)
    {
        for (var i = 0; i < EnWeekdays.Length; i++)
        {
            if (Regex.IsMatch(text, @"\b" + EnWeekdays[i] + @"\b")) return i;
        }

        for (var i = 0; i < RuWeekdays.Length; i++)
        {
            if (text.Contains(RuWeekdays[i], StringComparison.Ordinal)) return i;
        }

        if (Regex.IsMatch(text, @"\bпн\b")) return 1;
        if (Regex.IsMatch(text, @"\bвт\b")) return 2;
        if (Regex.IsMatch(text, @"\bср\b")) return 3;
        if (Regex.IsMatch(text, @"\bчт\b")) return 4;
        if (Regex.IsMatch(text, @"\bпт\b")) return 5;
        if (Regex.IsMatch(text, @"\bсб\b")) return 6;
        if (Regex.IsMatch(text, @"\bвс\b")) return 0;

        return -1;
    }

    private static string Format(int hour, int minute) =>
        new TimeSpan(hour, minute, 0).ToString(@"hh\:mm", CultureInfo.InvariantCulture);

    private static string Clock(DateTime value) => value.ToString("HH:mm", CultureInfo.InvariantCulture);
}
