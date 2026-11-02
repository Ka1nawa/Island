using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Media;
using Island.Core;
using Island.Services;

namespace Island.ViewModels;

public sealed class NoteViewModel : ObservableObject
{
    private static readonly Color[] Tints =
    {
        Color.FromRgb(0xF5, 0xC5, 0x42),
        Color.FromRgb(0xFF, 0x6B, 0x9A),
        Color.FromRgb(0x59, 0xA8, 0xFF),
        Color.FromRgb(0x32, 0xD7, 0x4B)
    };

    private readonly Action _save;

    public NoteViewModel(NoteItem model, Action save)
    {
        Model = model;
        _save = save;
        RebuildChecklist();
    }

    public NoteItem Model { get; }
    public ObservableCollection<ChecklistItem> Checklist { get; } = new();
    public bool HasChecklist => Checklist.Count > 0;

    public string Title => string.IsNullOrWhiteSpace(Model.Title) ? L.T("Notes") : Model.Title;
    public string Body => Model.Body;
    public bool HasBody => !string.IsNullOrWhiteSpace(Model.Body);
    public bool HasReminder => !string.IsNullOrWhiteSpace(Model.ReminderAt);

    public string ReminderText
    {
        get
        {
            if (!DateTime.TryParse(Model.ReminderAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at))
                return "";
            return at.ToString("d MMM, HH:mm", L.Culture);
        }
    }

    public Brush Accent => new SolidColorBrush(Tints[Math.Clamp(Model.Color, 0, 3)]);

    public Brush CardBackground
    {
        get
        {
            var tint = Tints[Math.Clamp(Model.Color, 0, 3)];
            var brush = new LinearGradientBrush(
                Color.FromArgb(38, tint.R, tint.G, tint.B),
                Color.FromArgb(10, 255, 255, 255), new System.Windows.Point(0, 0), new System.Windows.Point(0, 1));
            brush.Freeze();
            return brush;
        }
    }

    public void Refresh()
    {
        RebuildChecklist();
        OnPropertyChanged(string.Empty);
        OnPropertyChanged(nameof(HasChecklist));
    }

    private void RebuildChecklist()
    {
        Checklist.Clear();
        var lines = (Model.Body ?? "").Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var match = System.Text.RegularExpressions.Regex.Match(lines[i], @"^\s*-\s*\[( |x|X)\]\s*(.*)$");
            if (!match.Success) continue;

            Checklist.Add(new ChecklistItem(this, i, match.Groups[2].Value.Trim(),
                match.Groups[1].Value.Trim().Equals("x", StringComparison.OrdinalIgnoreCase)));
        }
    }

    public void ToggleChecklist(ChecklistItem item)
    {
        var lines = (Model.Body ?? "").Replace("\r\n", "\n").Split('\n').ToList();
        if (item.Line < 0 || item.Line >= lines.Count) return;

        lines[item.Line] = "- [" + (item.IsDone ? " " : "x") + "] " + item.Text;
        Model.Body = string.Join("\n", lines);
        Model.Updated = DateTime.Now.ToString("o");
        _save();
        Refresh();
    }
}

public sealed class ChecklistItem : ObservableObject
{
    private readonly NoteViewModel _owner;

    public ChecklistItem(NoteViewModel owner, int line, string text, bool done)
    {
        _owner = owner;
        Line = line;
        Text = text;
        IsDone = done;
    }

    public int Line { get; }
    public string Text { get; }

    public bool IsDone
    {
        get => _isDone;
        set => SetProperty(ref _isDone, value);
    }
    private bool _isDone;

    public void Toggle() => _owner.ToggleChecklist(this);
}

public sealed class ReminderViewModel : ObservableObject
{
    private readonly Action _save;

    public ReminderViewModel(ReminderItem model, Action save)
    {
        Model = model;
        _save = save;
    }

    public ReminderItem Model { get; }

    public string Title => string.IsNullOrWhiteSpace(Model.Title) ? L.T("ReminderDue") : Model.Title;

    public string TimeText
    {
        get
        {
            ReminderLogic.TryParseTime(Model.Time, out var time);
            return DateTime.Today.Add(time).ToString("HH:mm", CultureInfo.InvariantCulture);
        }
    }

    public string RepeatText => ReminderLogic.Describe(Model);

    public bool Enabled
    {
        get => Model.Enabled;
        set
        {
            if (Model.Enabled == value) return;
            Model.Enabled = value;
            OnPropertyChanged();
            _save();
        }
    }

    public void Refresh() => OnPropertyChanged(string.Empty);
}

public sealed class PlansViewModel : ObservableObject
{
    private readonly AppSettings _settings;

    public PlansViewModel(AppSettings settings)
    {
        _settings = settings;
        Reload();
    }

    public ObservableCollection<NoteViewModel> Notes { get; } = new();
    public ObservableCollection<ReminderViewModel> Reminders { get; } = new();

    public bool HasNotes => Notes.Count > 0;
    public bool HasReminders => Reminders.Count > 0;

    public void Reload()
    {
        Notes.Clear();
        foreach (var note in _settings.Notes) Notes.Add(new NoteViewModel(note, Save));

        Reminders.Clear();
        foreach (var reminder in _settings.Reminders) Reminders.Add(new ReminderViewModel(reminder, Save));

        OnPropertyChanged(nameof(HasNotes));
        OnPropertyChanged(nameof(HasReminders));
    }

    public NoteViewModel AddNote()
    {
        var item = new NoteItem
        {
            Title = "",
            Body = "",
            Color = Notes.Count % 4,
            Updated = DateTime.Now.ToString("o")
        };

        _settings.Notes.Insert(0, item);
        var vm = new NoteViewModel(item, Save);
        Notes.Insert(0, vm);
        Save();
        OnPropertyChanged(nameof(HasNotes));
        return vm;
    }

    public void DeleteNote(NoteViewModel note)
    {
        _settings.Notes.Remove(note.Model);
        Notes.Remove(note);
        Save();
        OnPropertyChanged(nameof(HasNotes));
    }

    public ReminderViewModel AddReminder()
    {
        var now = DateTime.Now.AddMinutes(5);
        var item = new ReminderItem
        {
            Title = "",
            Time = now.ToString("HH:mm", CultureInfo.InvariantCulture),
            Repeat = "Daily",
            Enabled = true
        };

        _settings.Reminders.Add(item);
        var vm = new ReminderViewModel(item, Save);
        Reminders.Add(vm);
        Save();
        OnPropertyChanged(nameof(HasReminders));
        return vm;
    }

    public void DeleteReminder(ReminderViewModel reminder)
    {
        _settings.Reminders.Remove(reminder.Model);
        Reminders.Remove(reminder);
        Save();
        OnPropertyChanged(nameof(HasReminders));
    }

    public void Save()
    {
        SettingsStore.Save(_settings);
        NotesSync.SyncToFolder(_settings);
    }

    public void ExportNotes(string path) => NotesSync.ExportFile(path, _settings.Notes);

    public int ImportNotes(string path)
    {
        var merged = NotesSync.MergeInto(_settings, NotesSync.ImportFile(path));
        Reload();
        return merged;
    }
}
