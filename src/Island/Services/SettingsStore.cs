using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Island.Services;

public sealed class AppSettings
{
    public string CityName { get; set; } = "Берлин";
    public string CityCountry { get; set; } = "Германия";
    public double Latitude { get; set; } = 52.52;
    public double Longitude { get; set; } = 13.405;
    public string TimeZone { get; set; } = "Europe/Berlin";
    public int SelectedTab { get; set; }
    public WeatherCache? Weather { get; set; }

    public string Language { get; set; } = "ru";
    public string Theme { get; set; } = "dark";
    public string DockSide { get; set; } = "TopCenter";
    public string MonitorId { get; set; } = "primary";
    public double Density { get; set; } = 62;
    public bool VisualizerEnabled { get; set; } = true;
    public bool LiveGlass { get; set; } = true;
    public bool ReduceMotion { get; set; }
    public double AnimationSpeed { get; set; } = 1.0;
    public bool AccentFromArtwork { get; set; } = true;
    public bool AutoStart { get; set; }
    public string TimeZoneId { get; set; } = "";
    public string NotificationSound { get; set; } = "soft";
    public bool QuietHours { get; set; }
    public string QuietFrom { get; set; } = "23:00";
    public string QuietTo { get; set; } = "08:00";
    public string UpdateRepo { get; set; } = "";
    public string CalendarUrl { get; set; } = "";
    public string NotesSyncFolder { get; set; } = "";
    public string CalendarFetchedAt { get; set; } = "";
    public List<CalendarEvent> CalendarCache { get; set; } = new();
    public List<CityEntry> Cities { get; set; } = new();
    public List<VolumePreset> VolumePresets { get; set; } = new();
    public List<ReminderItem> Reminders { get; set; } = new();
    public List<NoteItem> Notes { get; set; } = new();
}

public sealed class CalendarEvent
{
    public string Uid { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Start { get; set; } = "";
    public string End { get; set; } = "";
    public bool AllDay { get; set; }
}

public sealed class VolumePreset
{
    public string Name { get; set; } = "";
    public float Master { get; set; } = 0.5f;
    public bool MasterMuted { get; set; }
    public List<VolumePresetApp> Apps { get; set; } = new();
}

public sealed class VolumePresetApp
{
    public string Name { get; set; } = "";
    public float Volume { get; set; } = 1f;
    public bool Muted { get; set; }
}

public sealed class CityEntry
{
    public string Name { get; set; } = "";
    public string Country { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string TimeZone { get; set; } = "auto";
}

public sealed class ReminderItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public string Time { get; set; } = "10:00";
    public string Repeat { get; set; } = "Daily";
    public int Weekdays { get; set; } = 0;
    public string Date { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public string LastFired { get; set; } = "";
    public string SnoozeUntil { get; set; } = "";
}

public sealed class NoteItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public int Color { get; set; }
    public string ReminderAt { get; set; } = "";
    public string Created { get; set; } = DateTime.Now.ToString("o");
    public string Updated { get; set; } = DateTime.Now.ToString("o");
    public bool Pinned { get; set; }
    public double PinX { get; set; }
    public double PinY { get; set; }
}

public sealed class WeatherCache
{
    public double Temperature { get; set; }
    public double FeelsLike { get; set; }
    public double High { get; set; }
    public double Low { get; set; }
    public int Code { get; set; }
    public int Humidity { get; set; }
    public double Wind { get; set; }
    public string SavedAt { get; set; } = "";
    public List<DailyCache> Daily { get; set; } = new();
}

public sealed class DailyCache
{
    public string Date { get; set; } = "";
    public int Code { get; set; }
    public double Max { get; set; }
    public double Min { get; set; }
    public int Precip { get; set; }
}

public static class SettingsStore
{
    /// <summary>
    /// Data directory. Defaults to %APPDATA%\Island; ISLAND_DATA_DIR overrides it
    /// (portable mode and sandboxed testing).
    /// </summary>
    public static string Dir { get; } = ResolveDir();

    private static readonly string FilePath = Path.Combine(Dir, "settings.json");

    private static string ResolveDir()
    {
        var custom = Environment.GetEnvironmentVariable("ISLAND_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(custom)) return custom;
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Island");
    }

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, Options);
                if (loaded != null)
                {
                    Migrate(loaded);
                    return loaded;
                }
            }
        }
        catch (Exception ex)
        {
            Log("load", ex);
        }

        return new AppSettings();
    }

    /// <summary>Schema migrations: fill fields added after older versions.</summary>
    private static void Migrate(AppSettings settings)
    {
        foreach (var note in settings.Notes)
        {
            if (string.IsNullOrWhiteSpace(note.Updated))
                note.Updated = string.IsNullOrWhiteSpace(note.Created) ? DateTime.Now.ToString("o") : note.Created;
            if (string.IsNullOrWhiteSpace(note.Id))
                note.Id = Guid.NewGuid().ToString("N");
        }

        settings.Notes.RemoveAll(n => string.IsNullOrWhiteSpace(n.Id) && string.IsNullOrWhiteSpace(n.Title) && string.IsNullOrWhiteSpace(n.Body));
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var json = JsonSerializer.Serialize(settings, Options);
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, json);
            if (File.Exists(FilePath)) File.Replace(temp, FilePath, null);
            else File.Move(temp, FilePath);
        }
        catch (Exception ex)
        {
            Log("save", ex);
        }
    }

    private static void Log(string operation, Exception exception)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.AppendAllText(Path.Combine(Dir, "error.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] settings {operation} failed: {exception.GetType().Name}: {exception.Message}\n");
        }
        catch
        {
            // logging must never crash the widget
        }
    }
}