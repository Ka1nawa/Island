using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Island.Services;

namespace Island;

public partial class ToastWindow : Window
{
    private readonly DispatcherTimer _autoClose;
    private readonly Action<int>? _onSnooze;
    private readonly Color _accent;
    private bool _closing;

    public ToastWindow(string title, string message, string glyph, Color accent, Action<int>? onSnooze)
    {
        InitializeComponent();

        _onSnooze = onSnooze;
        _accent = accent;

        TitleText.Text = title;
        MessageText.Text = message;
        IconGlyph.Text = glyph;
        IconHost.Background = new SolidColorBrush(Color.FromArgb(0x3A, accent.R, accent.G, accent.B));
        IconGlyph.Foreground = new SolidColorBrush(accent);
        DoneText.Text = L.T("Done");
        Snooze5Text.Text = "5";
        Snooze10Text.Text = "10";
        Snooze30Text.Text = "30";
        Snooze5Button.ToolTip = L.F("SnoozeFor", 5);
        Snooze10Button.ToolTip = L.F("SnoozeFor", 10);
        Snooze30Button.ToolTip = L.F("SnoozeFor", 30);

        if (onSnooze is null)
        {
            Snooze5Button.Visibility = Visibility.Collapsed;
            Snooze10Button.Visibility = Visibility.Collapsed;
            Snooze30Button.Visibility = Visibility.Collapsed;
        }

        _autoClose = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
        _autoClose.Tick += (_, _) => CloseAnimated();

        Loaded += OnLoaded;
        MouseEnter += (_, _) => _autoClose.Stop();
        MouseLeave += (_, _) => { if (!_closing) _autoClose.Start(); };

        SourceInitialized += OnSourceInitialized;
    }

    public void PlaceNear(Rect island)
    {
        var work = SystemParameters.WorkArea;
        Left = Math.Clamp(island.X + island.Width / 2 - Width / 2, work.Left + 10, work.Right - Width - 10);
        Top = island.Bottom + 10;
        if (Top + Height > work.Bottom - 10) Top = island.Top - Height - 10;
        if (Top < work.Top + 10) Top = work.Top + 10;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var style = GetWindowLong(handle, -20);
        SetWindowLong(handle, -20, style | 0x08000000 | 0x00000080);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateGlass();
        PositionGlass();

        Opacity = 0;
        var transform = new TranslateTransform(0, -10);
        Card.RenderTransform = transform;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });
        transform.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(-10, 0, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });

        var glow = new DoubleAnimation(344, 0, TimeSpan.FromSeconds(12));
        Progress.BeginAnimation(WidthProperty, glow);
        _autoClose.Start();
    }

    private void UpdateGlass()
    {
        GlassImage.Source = GlassService.Backdrop;
        GlassImage.Width = GlassService.MonitorWidth;
        GlassImage.Height = GlassService.MonitorHeight;
        TintLayer.Background = new SolidColorBrush(Color.FromArgb(0xE2, 10, 10, 14));
        GlassHost.SizeChanged += (_, _) => UpdateClip();

        GlassHost.Clip = new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight), 20, 20);
    }

    private void UpdateClip()
    {
        GlassHost.Clip = new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight), 20, 20);
    }

    private void PositionGlass()
    {
        var source = PresentationSource.FromVisual(this);
        var scale = source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
        var point = PointToScreen(new Point(0, 0));
        System.Windows.Controls.Canvas.SetLeft(GlassImage, -point.X / scale.M11);
        System.Windows.Controls.Canvas.SetTop(GlassImage, -point.Y / scale.M22);
    }

    private void Done_Click(object sender, RoutedEventArgs e) => CloseAnimated();

    private void Snooze5_Click(object sender, RoutedEventArgs e) => Snooze(5);

    private void Snooze10_Click(object sender, RoutedEventArgs e) => Snooze(10);

    private void Snooze30_Click(object sender, RoutedEventArgs e) => Snooze(30);

    private void Snooze(int minutes)
    {
        _onSnooze?.Invoke(minutes);
        CloseAnimated();
    }

    private void CloseAnimated()
    {
        if (_closing) return;
        _closing = true;
        _autoClose.Stop();

        var animation = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(160));
        animation.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, animation);
    }
}