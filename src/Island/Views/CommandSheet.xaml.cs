using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Island.Services;

namespace Island.Views;

public partial class CommandSheet : UserControl
{
    public CommandSheet()
    {
        InitializeComponent();
    }

    public event Action<string>? NoteRequested;
    public event Action<string, ParsedReminder>? ReminderRequested;
    public event Action<int>? TimerRequested;

    private ParsedReminder _parsed = new();

    public void Reset()
    {
        InputBox.Text = "";
        _parsed = new ParsedReminder();
        PreviewText.Text = "";
    }

    public void FocusInput()
    {
        InputBox.Focus();
        Keyboard.Focus(InputBox);
        InputBox.CaretIndex = InputBox.Text.Length;
    }

    private void Input_Changed(object sender, TextChangedEventArgs e)
    {
        _parsed = NaturalTimeParser.Parse(InputBox.Text, DateTime.Now);
        var text = InputBox.Text.Trim();
        var timer = ParseTimer(text);

        if (text.Length == 0)
        {
            PreviewText.Text = "";
        }
        else if (timer is not null)
        {
            PreviewText.Text = L.T("Timer") + ": " + timer + " " + L.T("MinutesShort");
        }
        else if (_parsed.Ok)
        {
            PreviewText.Text = L.T("QuickReminder") + ": " + _parsed.Preview;
        }
        else
        {
            PreviewText.Text = L.T("QuickNote") + ": " + text;
        }
    }

    private static int? ParseTimer(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var match = Regex.Match(text.Trim(),
            @"^(?:/)?\s*(?:таймер|timer)\s+(\d{1,3})\s*(?:мин|min|m|м)?$",
            RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            match = Regex.Match(text.Trim(), @"^(\d{1,3})\s*(?:мин|min|m|м)$", RegexOptions.IgnoreCase);
        }
        if (!match.Success) return null;

        var minutes = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        return minutes is > 0 and <= 600 ? minutes : null;
    }

    private void Input_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        Execute();
        e.Handled = true;
    }

    private void Note_Click(object sender, RoutedEventArgs e) => CreateNote();

    private void Reminder_Click(object sender, RoutedEventArgs e) => CreateReminder();

    private void Execute()
    {
        if (string.IsNullOrWhiteSpace(InputBox.Text)) return;

        var timer = ParseTimer(InputBox.Text);
        if (timer is not null)
        {
            TimerRequested?.Invoke(timer.Value);
            return;
        }

        if (_parsed.Ok) CreateReminder();
        else CreateNote();
    }

    private void CreateNote()
    {
        var text = InputBox.Text.Trim();
        if (text.Length == 0) return;
        NoteRequested?.Invoke(text);
    }

    private void CreateReminder()
    {
        var text = InputBox.Text.Trim();
        if (text.Length == 0) return;

        var parsed = NaturalTimeParser.Parse(text, DateTime.Now);
        if (!parsed.Ok) return;

        var title = NaturalTimeParser.StripSchedule(text);
        if (string.IsNullOrWhiteSpace(title)) title = L.T("ReminderDue");
        ReminderRequested?.Invoke(title, parsed);
    }
}
