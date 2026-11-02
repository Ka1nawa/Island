using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Island.Services;
using Island.ViewModels;

namespace Island.Views;

public partial class PlansPage : UserControl
{
    private NoteViewModel? _note;
    private ReminderViewModel? _reminder;
    private readonly System.Windows.Threading.DispatcherTimer _autosave;

    public PlansPage()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateAddHint();

        _autosave = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _autosave.Tick += (_, _) => AutosaveNote();
    }

    private MainViewModel Vm => (MainViewModel)DataContext;

    public event Action<NoteViewModel>? NoteSaved;

    public bool IsEditing => NoteEditor.Visibility == Visibility.Visible || ReminderEditor.Visibility == Visibility.Visible;

    public bool CloseEditorIfOpen()
    {
        if (!IsEditing) return false;
        CloseEditor();
        return true;
    }

    private void PlansTab_Checked(object sender, RoutedEventArgs e)
    {
        if (NotesHost is null || RemindersHost is null) return;
        if (IsEditing) CloseEditor();

        var notes = NotesTab.IsChecked == true;
        NotesHost.Visibility = notes ? Visibility.Visible : Visibility.Collapsed;
        RemindersHost.Visibility = notes ? Visibility.Collapsed : Visibility.Visible;
        UpdateAddHint();
    }

    private void UpdateAddHint()
    {
        if (AddButton is null) return;
        AddButton.ToolTip = NotesTab.IsChecked == true ? L.T("AddNote") : L.T("AddReminder");
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (NotesTab.IsChecked == true)
            OpenNoteEditor(Vm.Plans.AddNote());
        else
            OpenReminderEditor(Vm.Plans.AddReminder());
    }

    private void Note_Click(object sender, MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is NoteViewModel note)
            OpenNoteEditor(note);
    }

    private void Reminder_Click(object sender, MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is ReminderViewModel reminder)
            OpenReminderEditor(reminder);
    }

    private void OpenNoteEditor(NoteViewModel note)
    {
        _note = note;
        NoteTitleBox.Text = note.Model.Title;
        NoteBodyBox.Text = note.Model.Body;
        Color0.IsChecked = note.Model.Color == 0;
        Color1.IsChecked = note.Model.Color == 1;
        Color2.IsChecked = note.Model.Color == 2;
        Color3.IsChecked = note.Model.Color == 3;

        var hasReminder = DateTime.TryParse(note.Model.ReminderAt, CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var at);
        NotePinCheck.IsChecked = note.Model.Pinned;
        NoteReminderCheck.IsChecked = hasReminder;
        NoteReminderDate.Text = (hasReminder ? at : DateTime.Today.AddDays(1)).ToString("dd.MM.yyyy");
        NoteReminderTime.Text = (hasReminder ? at : DateTime.Today.AddHours(9)).ToString("HH:mm");
        UpdateNoteReminderFields();

        ShowEditor(NoteEditor);
        _autosave.Start();
    }

    private void UpdateNoteReminderFields()
    {
        var enabled = NoteReminderCheck.IsChecked == true;
        NoteReminderDate.IsEnabled = enabled;
        NoteReminderTime.IsEnabled = enabled;
        NoteReminderDate.Opacity = enabled ? 1 : 0.4;
        NoteReminderTime.Opacity = enabled ? 1 : 0.4;
    }

    private void NoteReminder_Toggled(object sender, RoutedEventArgs e) => UpdateNoteReminderFields();

    private void ApplyNoteFields()
    {
        if (_note is null) return;

        _note.Model.Title = NoteTitleBox.Text.Trim();
        _note.Model.Body = NoteBodyBox.Text.Trim();
        _note.Model.Color = Color1.IsChecked == true ? 1 : Color2.IsChecked == true ? 2 : Color3.IsChecked == true ? 3 : 0;
        _note.Model.Pinned = NotePinCheck.IsChecked == true;
        _note.Model.Updated = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);

        if (NoteReminderCheck.IsChecked == true &&
            TryParseDateTime(NoteReminderDate.Text, NoteReminderTime.Text, out var at))
            _note.Model.ReminderAt = at.ToString("o", CultureInfo.InvariantCulture);
        else
            _note.Model.ReminderAt = "";
    }

    private void AutosaveNote()
    {
        if (_note is null || NoteEditor.Visibility != Visibility.Visible) return;
        if (string.IsNullOrWhiteSpace(NoteTitleBox.Text) && string.IsNullOrWhiteSpace(NoteBodyBox.Text)) return;

        ApplyNoteFields();
        _note.Refresh();
        Vm.Plans.Save();
        NoteSaved?.Invoke(_note);
    }

    private void Checklist_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is ChecklistItem item)
            item.Toggle();
    }

    private void ExportNotes_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "JSON (*.json)|*.json",
            FileName = "island-notes.json"
        };

        if (dialog.ShowDialog() == true) Vm.Plans.ExportNotes(dialog.FileName);
    }

    private void ImportNotes_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "JSON (*.json)|*.json" };
        if (dialog.ShowDialog() == true) Vm.Plans.ImportNotes(dialog.FileName);
    }

    private void SaveNote_Click(object sender, RoutedEventArgs e)
    {
        if (_note is null) return;

        ApplyNoteFields();

        if (string.IsNullOrWhiteSpace(_note.Model.Title) &&
            string.IsNullOrWhiteSpace(_note.Model.Body) &&
            string.IsNullOrWhiteSpace(_note.Model.ReminderAt))
        {
            Vm.Plans.DeleteNote(_note);
        }
        else
        {
            _note.Refresh();
            Vm.Plans.Save();
            NoteSaved?.Invoke(_note);
        }

        HideEditor();
    }

    private void DeleteNote_Click(object sender, RoutedEventArgs e)
    {
        if (_note is null) return;
        Vm.Plans.DeleteNote(_note);
        HideEditor();
    }

    private void OpenReminderEditor(ReminderViewModel reminder)
    {
        _reminder = reminder;
        QuickReminderBox.Text = "";
        QuickPreview.Text = "";
        ReminderTitleBox.Text = reminder.Model.Title;
        ReminderTimeBox.Text = reminder.TimeText;
        ReminderDateBox.Text = string.IsNullOrWhiteSpace(reminder.Model.Date)
            ? DateTime.Today.AddDays(1).ToString("dd.MM.yyyy")
            : reminder.Model.Date;

        switch (reminder.Model.Repeat)
        {
            case "Once": RepeatOnce.IsChecked = true; break;
            case "Hourly": RepeatHourly.IsChecked = true; break;
            case "Weekly": RepeatWeekly.IsChecked = true; break;
            default: RepeatDaily.IsChecked = true; break;
        }

        SetWeekdays(reminder.Model.Weekdays);
        UpdateReminderFields();
        ShowEditor(ReminderEditor);
    }

    private void QuickReminder_Changed(object sender, TextChangedEventArgs e)
    {
        var parsed = NaturalTimeParser.Parse(QuickReminderBox.Text, DateTime.Now);
        QuickPreview.Text = parsed.Ok ? parsed.Preview : "";
    }

    private void QuickReminder_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        ApplyQuick();
        e.Handled = true;
    }

    private void QuickApply_Click(object sender, RoutedEventArgs e) => ApplyQuick();

    private void ApplyQuick()
    {
        var parsed = NaturalTimeParser.Parse(QuickReminderBox.Text, DateTime.Now);
        if (!parsed.Ok) return;

        ApplyParsed(parsed);
        if (string.IsNullOrWhiteSpace(ReminderTitleBox.Text))
            ReminderTitleBox.Text = L.T("ReminderDue");
        QuickPreview.Text = parsed.Preview;
    }

    private void ApplyParsed(ParsedReminder parsed)
    {
        if (ReminderLogic.TryParseTime(parsed.Time, out var time))
            ReminderTimeBox.Text = time.ToString(@"hh\:mm", CultureInfo.InvariantCulture);

        switch (parsed.Repeat)
        {
            case "Once": RepeatOnce.IsChecked = true; break;
            case "Hourly": RepeatHourly.IsChecked = true; break;
            case "Weekly": RepeatWeekly.IsChecked = true; break;
            default: RepeatDaily.IsChecked = true; break;
        }

        if (parsed.Weekdays != 0) SetWeekdays(parsed.Weekdays);
        if (parsed.Repeat == "Once" && !string.IsNullOrWhiteSpace(parsed.Date))
            ReminderDateBox.Text = parsed.Date;

        UpdateReminderFields();
    }

    private void Preset_Click(object sender, RoutedEventArgs e)
    {
        var tag = (sender as FrameworkElement)?.Tag as string;
        var now = DateTime.Now;

        ParsedReminder parsed;
        string title;

        switch (tag)
        {
            case "water":
                parsed = new ParsedReminder { Ok = true, Repeat = "Hourly", Time = now.ToString("HH:mm", CultureInfo.InvariantCulture) };
                title = L.T("PresetWater");
                break;
            case "break":
                parsed = new ParsedReminder { Ok = true, Repeat = "Hourly", Time = now.ToString("HH:mm", CultureInfo.InvariantCulture) };
                title = L.T("PresetBreak");
                break;
            case "duolingo":
                parsed = new ParsedReminder { Ok = true, Repeat = "Daily", Time = "19:00" };
                title = L.T("PresetDuolingo");
                break;
            default:
                ApplyQuick();
                return;
        }

        ApplyParsed(parsed);
        ReminderTitleBox.Text = title;
    }

    private void Repeat_Checked(object sender, RoutedEventArgs e)
    {
        if (WeekdayRow is null) return;
        UpdateReminderFields();
    }

    private void UpdateReminderFields()
    {
        if (WeekdayRow is null) return;

        var once = RepeatOnce.IsChecked == true;
        var weekly = RepeatWeekly.IsChecked == true;

        WeekdayRow.Visibility = weekly ? Visibility.Visible : Visibility.Collapsed;
        DateLabel.Visibility = once ? Visibility.Visible : Visibility.Collapsed;
        ReminderDateBox.Visibility = once ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SetWeekdays(int mask)
    {
        foreach (var toggle in new[] { DayMon, DayTue, DayWed, DayThu, DayFri, DaySat, DaySun })
        {
            var flag = int.Parse((string)toggle.Tag, CultureInfo.InvariantCulture);
            toggle.IsChecked = (mask & flag) != 0;
        }
    }

    private int ReadWeekdays()
    {
        var mask = 0;
        foreach (var toggle in new[] { DayMon, DayTue, DayWed, DayThu, DayFri, DaySat, DaySun })
        {
            if (toggle.IsChecked == true) mask |= int.Parse((string)toggle.Tag, CultureInfo.InvariantCulture);
        }
        return mask == 0 ? 1 : mask;
    }

    private void SaveReminder_Click(object sender, RoutedEventArgs e)
    {
        if (_reminder is null) return;

        var title = ReminderTitleBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            Vm.Plans.DeleteReminder(_reminder);
            HideEditor();
            return;
        }

        _reminder.Model.Title = title;

        if (ReminderLogic.TryParseTime(ReminderTimeBox.Text, out var time))
            _reminder.Model.Time = time.ToString(@"hh\:mm", CultureInfo.InvariantCulture);

        _reminder.Model.Repeat = RepeatOnce.IsChecked == true ? "Once"
            : RepeatHourly.IsChecked == true ? "Hourly"
            : RepeatWeekly.IsChecked == true ? "Weekly" : "Daily";

        _reminder.Model.Weekdays = ReadWeekdays();
        _reminder.Model.Date = ReminderLogic.TryParseDate(ReminderDateBox.Text, out var date)
            ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : DateTime.Today.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        _reminder.Model.LastFired = "";
        _reminder.Refresh();
        Vm.Plans.Save();
        HideEditor();
    }

    private void DeleteReminder_Click(object sender, RoutedEventArgs e)
    {
        if (_reminder is null) return;
        Vm.Plans.DeleteReminder(_reminder);
        HideEditor();
    }

    private void CloseEditor_Click(object sender, RoutedEventArgs e) => CloseEditor();

    private void CloseEditor()
    {
        if (_note is not null)
        {
            var empty = string.IsNullOrWhiteSpace(_note.Model.Title) &&
                        string.IsNullOrWhiteSpace(_note.Model.Body) &&
                        string.IsNullOrWhiteSpace(_note.Model.ReminderAt);
            if (empty) Vm.Plans.DeleteNote(_note);
        }

        if (_reminder is not null && string.IsNullOrWhiteSpace(_reminder.Model.Title))
        {
            Vm.Plans.DeleteReminder(_reminder);
        }

        HideEditor();
    }

    private void ShowEditor(FrameworkElement editor)
    {
        editor.Visibility = Visibility.Visible;
        editor.Opacity = 0;
        var animation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        editor.BeginAnimation(OpacityProperty, animation);
    }

    private void HideEditor()
    {
        _autosave.Stop();
        foreach (var editor in new FrameworkElement[] { NoteEditor, ReminderEditor })
        {
            if (editor.Visibility != Visibility.Visible) continue;

            editor.BeginAnimation(OpacityProperty, null);
            var animation = new DoubleAnimation(editor.Opacity, 0, TimeSpan.FromMilliseconds(120));
            var target = editor;
            animation.Completed += (_, _) =>
            {
                target.BeginAnimation(OpacityProperty, null);
                target.Opacity = 0;
                target.Visibility = Visibility.Collapsed;
            };
            editor.BeginAnimation(OpacityProperty, animation);
        }

        _note = null;
        _reminder = null;
    }

    private static bool TryParseDateTime(string? dateText, string? timeText, out DateTime result)
    {
        result = DateTime.Today.AddDays(1).AddHours(9);
        if (!ReminderLogic.TryParseTime(timeText, out var time)) return false;

        if (ReminderLogic.TryParseDate(dateText, out var date))
        {
            result = date.Date + time;
            return true;
        }

        return false;
    }
}