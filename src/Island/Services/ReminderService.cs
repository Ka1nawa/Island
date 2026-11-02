using System;
using System.Globalization;
using System.Linq;
using System.Windows.Threading;

namespace Island.Services;

public sealed class ReminderService : IDisposable
{
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _timer;

    public ReminderService(AppSettings settings)
    {
        _settings = settings;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _timer.Tick += (_, _) => Check();
    }

    public event Action<ReminderItem>? ReminderDue;
    public event Action<NoteItem>? NoteDue;

    public void Start() => _timer.Start();

    public void Check()
    {
        var now = DateTime.Now;

        foreach (var reminder in _settings.Reminders.ToList())
        {
            var due = ReminderLogic.LastOccurrence(reminder, now);
            if (due is null) continue;

            var lastFired = ParseDate(reminder.LastFired);
            if (lastFired is not null && lastFired >= due) continue;

            reminder.LastFired = now.ToString("o", CultureInfo.InvariantCulture);
            var snoozed = !string.IsNullOrWhiteSpace(reminder.SnoozeUntil);
            reminder.SnoozeUntil = "";

            if (now - due.Value > TimeSpan.FromMinutes(35) && !snoozed) continue;
            ReminderDue?.Invoke(reminder);
        }

        foreach (var note in _settings.Notes.ToList())
        {
            if (string.IsNullOrWhiteSpace(note.ReminderAt)) continue;
            if (!DateTime.TryParse(note.ReminderAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at)) continue;
            if (at > now) continue;

            note.ReminderAt = "";
            if (now - at < TimeSpan.FromHours(12)) NoteDue?.Invoke(note);
        }
    }

    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;
    }

    public void Dispose() => _timer.Stop();
}
