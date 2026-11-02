using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Island.Services;

namespace Island;

public partial class StickyWindow : Window
{
    private readonly NoteItem _note;
    private readonly Action<StickyWindow, bool> _onClosed;

    public StickyWindow(NoteItem note, Action<StickyWindow, bool> onClosed)
    {
        InitializeComponent();

        _note = note;
        _onClosed = onClosed;

        TitleText.Text = string.IsNullOrWhiteSpace(note.Title) ? L.T("Notes") : note.Title;
        BodyText.Markdown = note.Body;

        var accent = ColorFor(note.Color);
        Header.Background = new SolidColorBrush(Color.FromArgb(0x33, accent.R, accent.G, accent.B));

        if (note.PinX > 0 || note.PinY > 0)
        {
            Left = note.PinX;
            Top = note.PinY;
        }
        else
        {
            var work = SystemParameters.WorkArea;
            Left = work.Right - Width - 28;
            Top = work.Top + 120;
        }

        Closing += (_, _) => _onClosed(this, false);
    }

    private static Color ColorFor(int index) => index switch
    {
        1 => Color.FromRgb(0xFF, 0x6B, 0x9A),
        2 => Color.FromRgb(0x59, 0xA8, 0xFF),
        3 => Color.FromRgb(0x32, 0xD7, 0x4B),
        _ => Color.FromRgb(0xF5, 0xC5, 0x42)
    };

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        try { DragMove(); } catch { }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        _onClosed(this, true);
        Close();
    }
}
