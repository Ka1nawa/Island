using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Island.Services;

/// <summary>
/// Notes export/import and folder synchronization.
/// Merge rule: the note with the newer Updated wins; unknown ids are added.
/// The file is written atomically and the previous copy is kept as .bak.
/// </summary>
public static class NotesSync
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static void ExportFile(string path, IEnumerable<NoteItem> notes)
    {
        var payload = new NotesFile { Version = 1, Notes = notes.ToList() };
        var json = JsonSerializer.Serialize(payload, Options);
        var temp = path + ".tmp";
        File.WriteAllText(temp, json);

        if (File.Exists(path))
        {
            File.Replace(temp, path, path + ".bak");
        }
        else
        {
            File.Move(temp, path);
        }
    }

    public static List<NoteItem> ImportFile(string path)
    {
        if (!File.Exists(path)) return new List<NoteItem>();
        var json = File.ReadAllText(path);
        var payload = JsonSerializer.Deserialize<NotesFile>(json, Options);
        return payload?.Notes ?? new List<NoteItem>();
    }

    public static int MergeInto(AppSettings settings, IEnumerable<NoteItem> incoming)
    {
        var changed = 0;

        foreach (var note in incoming)
        {
            if (string.IsNullOrWhiteSpace(note.Id)) note.Id = Guid.NewGuid().ToString("N");
            var local = settings.Notes.FirstOrDefault(n => n.Id == note.Id);

            if (local is null)
            {
                settings.Notes.Add(note);
                changed++;
                continue;
            }

            if (Compare(note.Updated, local.Updated) <= 0) continue;

            local.Title = note.Title;
            local.Body = note.Body;
            local.Color = note.Color;
            local.ReminderAt = note.ReminderAt;
            local.Updated = note.Updated;
            changed++;
        }

        if (changed > 0) SettingsStore.Save(settings);
        return changed;
    }

    public static string FolderFile(string folder) => Path.Combine(folder, "island-notes.json");

    public static void SyncToFolder(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.NotesSyncFolder)) return;

        try
        {
            Directory.CreateDirectory(settings.NotesSyncFolder);
            ExportFile(FolderFile(settings.NotesSyncFolder), settings.Notes);
        }
        catch (Exception exception)
        {
            Log("export", exception);
        }
    }

    public static void MergeFromFolder(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.NotesSyncFolder)) return;

        try
        {
            var file = FolderFile(settings.NotesSyncFolder);
            if (File.Exists(file)) MergeInto(settings, ImportFile(file));
        }
        catch (Exception exception)
        {
            Log("merge", exception);
        }
    }

    private static int Compare(string? left, string? right)
    {
        DateTime.TryParse(left, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out var a);
        DateTime.TryParse(right, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out var b);
        return a.CompareTo(b);
    }

    private static void Log(string operation, Exception exception)
    {
        try
        {
            File.AppendAllText(Path.Combine(SettingsStore.Dir, "error.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] notes {operation} failed: {exception.Message}\n");
        }
        catch
        {
        }
    }

    private sealed class NotesFile
    {
        public int Version { get; set; }
        public List<NoteItem> Notes { get; set; } = new();
    }
}
