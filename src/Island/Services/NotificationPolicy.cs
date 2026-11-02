using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Island.Services;

/// <summary>Quiet hours and Windows Focus Assist / Do Not Disturb state.</summary>
public static class NotificationPolicy
{
    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out int state);

    private const int QunsRunningD3dFullScreen = 3;
    private const int QunsPresentationMode = 4;
    private const int QunsQuietTime = 6;

    public static bool IsDoNotDisturb()
    {
        try
        {
            if (SHQueryUserNotificationState(out var state) != 0) return false;
            return state is QunsRunningD3dFullScreen or QunsPresentationMode or QunsQuietTime;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsQuietHours(bool enabled, string? from, string? to, DateTime now)
    {
        if (!enabled) return false;
        if (!TimeOnly.TryParse(from, CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)) return false;
        if (!TimeOnly.TryParse(to, CultureInfo.InvariantCulture, DateTimeStyles.None, out var end)) return false;
        if (start == end) return false;

        var current = TimeOnly.FromDateTime(now);
        return start < end
            ? current >= start && current < end
            : current >= start || current < end;
    }

    /// <summary>True when the notification should be silent (still shown).</summary>
    public static bool ShouldSilence(AppSettings settings, DateTime now)
        => IsDoNotDisturb() || IsQuietHours(settings.QuietHours, settings.QuietFrom, settings.QuietTo, now);
}
