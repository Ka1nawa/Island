using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Island.Services;

/// <summary>Minimal but real iCalendar reader: VEVENT, DTSTART/DTEND, TZID/Z, RRULE (DAILY/WEEKLY/MONTHLY), EXDATE, CANCELLED.</summary>
public static class IcsCalendar
{
    public static List<CalendarEvent> Parse(string ics, DateTime from, DateTime to)
    {
        var events = new List<CalendarEvent>();
        if (string.IsNullOrWhiteSpace(ics)) return events;

        foreach (var block in SplitEvents(Unfold(ics)))
        {
            var props = ParseProperties(block);
            if (props.Count == 0) continue;

            var status = Get(props, "STATUS");
            if (string.Equals(status, "CANCELLED", StringComparison.OrdinalIgnoreCase)) continue;

            var start = GetDate(props, "DTSTART");
            if (start is null) continue;

            var end = GetDate(props, "DTEND") ?? start.Value + ParseDuration(Get(props, "DURATION"));
            var allDay = IsDateOnly(props, "DTSTART");
            var summary = Get(props, "SUMMARY") ?? "";
            var uid = Get(props, "UID") ?? summary;

            var exdates = props
                .Where(p => p.Name == "EXDATE")
                .SelectMany(p => p.Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Select(v => TryParse(v.Trim(), null))
                .Where(v => v is not null)
                .Select(v => v!.Value)
                .ToHashSet();

            var rrule = Get(props, "RRULE");
            if (string.IsNullOrWhiteSpace(rrule))
            {
                if (start.Value >= from && start.Value <= to && !exdates.Contains(start.Value))
                    Add(events, uid, summary, start.Value, end, allDay);
                continue;
            }

            foreach (var occurrence in Expand(start.Value, rrule, to))
            {
                if (occurrence < from || occurrence > to) continue;
                if (exdates.Contains(occurrence)) continue;
                Add(events, uid, summary, occurrence, occurrence + (end - start.Value), allDay);
            }
        }

        return events.OrderBy(e => e.Start, StringComparer.Ordinal).ToList();
    }

    private static void Add(List<CalendarEvent> events, string uid, string summary, DateTime start, DateTime end, bool allDay)
    {
        events.Add(new CalendarEvent
        {
            Uid = uid,
            Summary = summary,
            Start = start.ToString("o", CultureInfo.InvariantCulture),
            End = end.ToString("o", CultureInfo.InvariantCulture),
            AllDay = allDay
        });
    }

    private static IEnumerable<DateTime> Expand(DateTime start, string rrule, DateTime to)
    {
        var parts = rrule.Split(';')
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0].ToUpperInvariant(), p => p[1]);

        var freq = parts.TryGetValue("FREQ", out var f) ? f.ToUpperInvariant() : "DAILY";
        var interval = parts.TryGetValue("INTERVAL", out var i) &&
                       int.TryParse(i, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInterval)
            ? Math.Max(1, parsedInterval)
            : 1;

        var count = parts.TryGetValue("COUNT", out var c) &&
                    int.TryParse(c, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedCount)
            ? parsedCount
            : int.MaxValue;

        var until = parts.TryGetValue("UNTIL", out var u) ? TryParse(u, null) : null;
        var byday = parts.TryGetValue("BYDAY", out var days)
            ? ParseByDay(days)
            : new List<DayOfWeek>();

        var produced = 0;
        var guard = 0;

        if (freq == "WEEKLY" && byday.Count > 0)
        {
            var weekStart = start.Date.AddDays(-(((int)start.DayOfWeek + 6) % 7));
            for (var week = 0; week < 520 && guard++ < 5000; week += interval)
            {
                var monday = weekStart.AddDays(7 * week);
                if (monday > to) break;

                foreach (var day in byday.OrderBy(d => ((int)d + 6) % 7))
                {
                    var occurrence = monday.AddDays(((int)day + 6) % 7) + start.TimeOfDay;
                    if (occurrence < start) continue;
                    if (occurrence > to) break;
                    if (until is not null && occurrence > until) break;
                    produced++;
                    yield return occurrence;
                    if (produced >= count) yield break;
                }
            }

            yield break;
        }

        var current = start;
        while (current <= to && guard++ < 5000)
        {
            if (until is not null && current > until) yield break;
            yield return current;
            produced++;
            if (produced >= count) yield break;

            current = freq switch
            {
                "WEEKLY" => current.AddDays(7 * interval),
                "MONTHLY" => current.AddMonths(interval),
                "YEARLY" => current.AddYears(interval),
                _ => current.AddDays(interval)
            };
        }
    }

    private static List<DayOfWeek> ParseByDay(string value)
    {
        var result = new List<DayOfWeek>();
        foreach (var token in value.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var day = token.Trim().ToUpperInvariant() switch
            {
                "MO" => DayOfWeek.Monday,
                "TU" => DayOfWeek.Tuesday,
                "WE" => DayOfWeek.Wednesday,
                "TH" => DayOfWeek.Thursday,
                "FR" => DayOfWeek.Friday,
                "SA" => DayOfWeek.Saturday,
                "SU" => DayOfWeek.Sunday,
                _ => (DayOfWeek?)null
            };
            if (day is not null) result.Add(day.Value);
        }

        return result;
    }

    private static TimeSpan ParseDuration(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return TimeSpan.FromHours(1);

        var match = System.Text.RegularExpressions.Regex.Match(value, @"P(?:T(?:(d+)H)?(?:(d+)M)?)?");
        if (!match.Success) return TimeSpan.FromHours(1);

        var hours = match.Groups[1].Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
        var minutes = match.Groups[2].Success ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : 0;
        return new TimeSpan(hours, minutes, 0);
    }

    private static DateTime? GetDate(List<Property> props, string name)
    {
        var property = props.FirstOrDefault(p => p.Name == name);
        if (property.Name is null) return null;

        return TryParse(property.Value, property.Params.TryGetValue("TZID", out var tz) ? tz : null);
    }

    private static bool IsDateOnly(List<Property> props, string name)
    {
        var property = props.FirstOrDefault(p => p.Name == name);
        return property.Value is { Length: 8 } ||
               (property.Params.TryGetValue("VALUE", out var value) && value.Equals("DATE", StringComparison.OrdinalIgnoreCase));
    }

    private static DateTime? TryParse(string? value, string? tzid)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();

        if (value.Length == 8 &&
            DateTime.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly))
        {
            return DateTime.SpecifyKind(dateOnly, DateTimeKind.Local);
        }

        var utc = value.EndsWith("Z", StringComparison.OrdinalIgnoreCase);
        var text = utc ? value[..^1] : value;

        if (!DateTime.TryParseExact(text, "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed))
        {
            if (!DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)) return null;
        }

        if (utc)
        {
            var utcTime = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            return utcTime.ToLocalTime();
        }

        if (!string.IsNullOrWhiteSpace(tzid))
        {
            var zone = FindZone(tzid);
            if (zone is not null)
            {
                try
                {
                    var unspecified = DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified);
                    return TimeZoneInfo.ConvertTimeToUtc(unspecified, zone).ToLocalTime();
                }
                catch
                {
                }
            }
        }

        return DateTime.SpecifyKind(parsed, DateTimeKind.Local);
    }

    private static TimeZoneInfo? FindZone(string tzid)
    {
        foreach (var candidate in new[] { tzid, tzid.Replace('/', ' '), tzid.Replace(" ", "_") })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(candidate);
            }
            catch
            {
            }
        }

        return null;
    }

    private static List<Property> ParseProperties(List<string> block)
    {
        var result = new List<Property>();
        foreach (var line in block)
        {
            var colon = line.IndexOf(':');
            if (colon <= 0) continue;

            var head = line[..colon];
            var value = line[(colon + 1)..];
            var pieces = head.Split(';');
            var name = pieces[0].ToUpperInvariant();
            var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var piece in pieces.Skip(1))
            {
                var eq = piece.IndexOf('=');
                if (eq > 0) parameters[piece[..eq]] = piece[(eq + 1)..].Trim('"');
            }

            result.Add(new Property(name, parameters, Unescape(value)));
        }

        return result;
    }

    private static string? Get(List<Property> props, string name) =>
        props.FirstOrDefault(p => p.Name == name).Value;

    private static string Unescape(string value) => value
        .Replace("\\n", "\n")
        .Replace("\\,", ",")
        .Replace("\\;", ";")
        .Replace("\\\\", "\\");

    private static string Unfold(string ics)
    {
        var builder = new StringBuilder();
        foreach (var raw in ics.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            if (raw.Length > 0 && (raw[0] == ' ' || raw[0] == '\t') && builder.Length > 0)
            {
                if (builder.Length > 0 && builder[^1] == '\n') builder.Length--;
                builder.Append(raw[1..]);
            }
            else
            {
                builder.Append(raw).Append('\n');
            }
        }

        return builder.ToString();
    }

    private static List<List<string>> SplitEvents(string unfolded)
    {
        var blocks = new List<List<string>>();
        List<string>? current = null;

        foreach (var line in unfolded.Split('\n'))
        {
            var trimmed = line.TrimEnd();
            if (trimmed.Equals("BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                current = new List<string>();
                continue;
            }

            if (trimmed.Equals("END:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                if (current is not null) blocks.Add(current);
                current = null;
                continue;
            }

            current?.Add(trimmed);
        }

        return blocks;
    }

    private readonly record struct Property(string Name, Dictionary<string, string> Params, string Value);
}

public sealed class CalendarService
{
    private static readonly HttpClient Http = Create();

    private static HttpClient Create()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Island/1.0 (Windows widget)");
        return client;
    }

    public async Task<List<CalendarEvent>> FetchAsync(string url, DateTime from, DateTime to, CancellationToken ct)
    {
        var normalized = url.Trim();
        if (normalized.StartsWith("webcal://", StringComparison.OrdinalIgnoreCase))
            normalized = "https://" + normalized["webcal://".Length..];

        var text = await Http.GetStringAsync(normalized, ct);
        return IcsCalendar.Parse(text, from, to);
    }
}
