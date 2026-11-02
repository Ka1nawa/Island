using System;
using System.IO;

namespace Island.Services;

/// <summary>Keeps one archived error log so the file cannot grow without bound.</summary>
public static class LogRotation
{
    private const long LimitBytes = 1024 * 1024;

    public static void Rotate()
    {
        try
        {
            var path = Path.Combine(SettingsStore.Dir, "error.log");
            if (!File.Exists(path)) return;
            if (new FileInfo(path).Length < LimitBytes) return;

            var archive = Path.Combine(SettingsStore.Dir, "error.1.log");
            File.Copy(path, archive, true);
            File.WriteAllText(path, "");
        }
        catch
        {
        }
    }
}
