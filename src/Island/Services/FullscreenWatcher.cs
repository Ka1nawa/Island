using System;
using System.Runtime.InteropServices;

namespace Island.Services;

/// <summary>Detects exclusive fullscreen / presentation mode via the shell notification state.</summary>
public static class FullscreenWatcher
{
    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out int state);

    public static bool IsFullscreen()
    {
        try
        {
            if (SHQueryUserNotificationState(out var state) != 0) return false;
            return state == 3 || state == 4; // D3D fullscreen, presentation mode
        }
        catch
        {
            return false;
        }
    }
}
