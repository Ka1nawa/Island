using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Island.Services;
using Island.ViewModels;
using WinForms = System.Windows.Forms;

namespace Island;

public partial class MainWindow : Window
{
    private const double CollapsedWidth = 240;
    private const double CollapsedHeight = 44;
    private const double ExpandedWidth = 448;
    private const double ExpandedHeight = 424;
    private const double WindowWidth = 472;
    private const double WindowHeight = 448;

    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x00000080;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);


    private const int WmHotkey = 0x0312;
    private const int HotkeyId = 0x4A17;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint VkSpace = 0x20;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int WmDpiChanged = 0x02E0;
    private const int WmDisplayChange = 0x007E;
    private const int WmSettingChange = 0x001A;
    private const uint SwpNoActivate = 0x0010;
    private static readonly IntPtr HwndTopmost = new(-1);

    private readonly AppSettings _settings;
    private readonly AudioService _audio = new();
    private readonly MediaService _media = new();
    private readonly LyricsService _lyrics = new();
    private readonly WeatherService _weather = new();
    private readonly SpectrumService _spectrum = new();
    private readonly TimerService _timers = new();
    private readonly CalendarService _calendar = new();
    private readonly HardwareMonitor _hardware = new();
    private readonly HashSet<string> _meetingToasts = new();
    private readonly Dictionary<string, StickyWindow> _stickies = new();
    private readonly ReminderService _reminders;
    private readonly MainViewModel _vm;
    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _hoverTimer;
    private readonly Queue<(string Title, string Message, string Glyph, Color Accent, Action<int>? Snooze)> _toastQueue = new();

    private ToastWindow? _toast;
    private bool _animating;
    private DateTime _animStart;
    private double _visualW = CollapsedWidth;
    private double _visualH = CollapsedHeight;
    private double _visualR = 22;
    private double _fromW, _fromH, _fromR, _toW, _toH, _toR;
    private TimeZoneInfo _zone = TimeZoneInfo.Local;
    private WinForms.NotifyIcon? _tray;
    private WinForms.ContextMenuStrip? _trayMenu;
    private IntPtr _trayHandle;
    private System.Drawing.Icon? _appIcon;

    private HwndSource? _hwndSource;
    private IntPtr _hwnd;
    private bool _hotkeyRegistered;
    private DispatcherTimer? _displayTimer;
    private DispatcherTimer? _liveGlassTimer;
    private bool _liveGlassActive;
    private bool _hiddenForFullscreen;
    private bool _updateCheckRunning;
    private DateTime _lastUpdateCheck = DateTime.MinValue;
    private UpdateInfo? _pendingUpdate;
    private string _pendingUpdateFile = "";
    private DisplayMonitor _monitor = DisplayService.GetPrimary();

    private bool _expanded;
    private bool _ready;
    private bool _mediaBusy;
    private bool _eqRunning;
    private bool _menuOpen;
    private bool _collapseAfterMenu;
    private bool _overlayOpen;
    private string _overlay = "";
    private bool _aboutFromSettings;
    private int _tick;
    private DateTime _weatherLoaded = DateTime.MinValue;
    private double _radius = 22;

    public MainWindow()
    {
        _settings = SettingsStore.Load();
        NotesSync.MergeFromFolder(_settings);
        Theme.Apply(_settings.Theme);
        L.Apply(_settings.Language);

        InitializeComponent();

        _reminders = new ReminderService(_settings);
        _vm = new MainViewModel(
            new MediaViewModel(_media, _lyrics),
            new SoundViewModel(_audio, _settings),
            new WeatherViewModel(_weather, _settings),
            new SettingsViewModel(_settings),
            new PlansViewModel(_settings),
            new SystemViewModel(_hardware));

        DataContext = _vm;
        MenuPopup.PlacementTarget = MenuButton;
        RefreshZone();

        if (_settings.AutoStart) AutoStartService.Apply(true);
        MediaView.VisualizerControl.Source = _spectrum;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += OnTick;

        _hoverTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
        _hoverTimer.Tick += OnHoverTimer;

        _displayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _displayTimer.Tick += (_, _) =>
        {
            _displayTimer!.Stop();
            OnDisplayConfigurationChanged();
        };

        _liveGlassTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(70) };
        _liveGlassTimer.Tick += OnLiveGlassTick;

        _vm.Settings.DockChanged += OnDockChanged;
        _vm.Settings.MonitorChanged += OnDockChanged;
        _vm.Settings.TimeZoneChanged += OnTimeZoneChanged;
        _vm.Settings.DensityChanged += UpdateTint;
        _vm.Settings.AccentChanged += UpdateTint;
        _vm.Settings.ThemeChanged += OnThemeChanged;
        _vm.Media.PropertyChanged += OnMediaPropertyChanged;
        _vm.Settings.VisualizerChanged += UpdateVisualizer;
        _vm.Settings.LiveGlassChanged += OnLiveGlassChanged;
        _vm.Settings.CalendarChanged += OnCalendarChanged;
        CommandView.NoteRequested += OnQuickNote;
        CommandView.ReminderRequested += OnQuickReminder;
        CommandView.TimerRequested += OnQuickTimer;
        PlansView.NoteSaved += OnNoteSaved;
        _timers.RemainingChanged += OnTimerRemaining;
        _timers.Finished += OnTimerFinished;
        L.LanguageChanged += OnLanguageChanged;
        SettingsView.AboutRequested += OpenAbout;

        _reminders.ReminderDue += OnReminderDue;
        _reminders.NoteDue += OnNoteDue;

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        Closing += OnClosing;
        Deactivated += OnDeactivated;
        PreviewKeyDown += OnPreviewKeyDown;
        Shell.SizeChanged += (_, _) => UpdateGlass();

        ApplyDock();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        _hwnd = handle;
        var style = GetWindowLong(handle, GwlExStyle);
        SetWindowLong(handle, GwlExStyle, style | WsExToolWindow);

        _hwndSource = HwndSource.FromHwnd(handle);
        _hwndSource?.AddHook(WndProc);

        LiveGlassService.SetExcluded(handle, _settings.LiveGlass && !_settings.ShowInCapture);

        _hotkeyRegistered = RegisterHotKey(handle, HotkeyId, ModControl | ModAlt, VkSpace);

        ApplyDock();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WmHotkey:
                Dispatcher.BeginInvoke(new Action(ToggleCommandBar));
                handled = true;
                break;
            case WmDpiChanged:
            case WmDisplayChange:
            case WmSettingChange:
                _displayTimer?.Stop();
                _displayTimer?.Start();
                break;
        }

        return IntPtr.Zero;
    }

    private void OnDisplayConfigurationChanged()
    {
        if (_settings.Theme == "auto")
        {
            Theme.Apply("auto");
            UpdateTint();
        }

        ApplyDock();
        if (!_ready) return;

        GlassService.Initialize(_monitor);
        UpdateGlass();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        GlassService.Initialize(_monitor);
        GlassImage.Source = GlassService.Backdrop;
        UpdateTint();
        UpdateVisualizer();

        _ready = true;
        SelectTab(_settings.SelectedTab, save: false);
        SettingsView.SyncFromSettings();

        _radius = 22;
        UpdateGlassClip();
        UpdateGlass();
        PlayIntro();

        _timer.Start();
        _reminders.Start();
        CreateTray();

        _ = RefreshMediaAsync();
        RefreshAudio();
        _vm.Sound.RefreshDevices();
        _ = LoadWeatherAsync();
        _ = RefreshCalendarAsync();

        foreach (var note in _settings.Notes.Where(n => n.Pinned).ToList()) ShowSticky(note);

        if (_settings.AutoCheckUpdates)
        {
            _lastUpdateCheck = DateTime.Now;
            _ = AutoCheckUpdatesAsync();
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _timer.Stop();
        _hoverTimer.Stop();
        _displayTimer?.Stop();
        _liveGlassTimer?.Stop();
        if (_hotkeyRegistered) UnregisterHotKey(_hwnd, HotkeyId);
        _hwndSource?.RemoveHook(WndProc);
        CompositionTarget.Rendering -= OnAnimationFrame;
        _tray?.Dispose();
        if (_trayHandle != IntPtr.Zero) DestroyIcon(_trayHandle);
        _reminders.Dispose();
        foreach (var sticky in _stickies.Values.ToList()) sticky.Close();
        _stickies.Clear();
        _hardware.Dispose();
        _timers.Dispose();
        _spectrum.Dispose();
        _audio.Dispose();
        SettingsStore.Save(_settings);
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (_menuOpen)
        {
            _collapseAfterMenu = true;
            return;
        }

        if (_overlayOpen) return;
        if (_expanded) Collapse();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;

        if (_overlayOpen)
        {
            CloseOverlay();
            e.Handled = true;
            return;
        }

        if (_vm.Weather.IsSearching)
        {
            _vm.Weather.CancelSearch();
            e.Handled = true;
            return;
        }

        if (PlansView.Visibility == Visibility.Visible && PlansView.CloseEditorIfOpen())
        {
            e.Handled = true;
            return;
        }

        Collapse();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _tick++;
        var now = NowInZone();
        _vm.Clock = now.ToString("HH:mm", L.Culture);
        _vm.DateLine = L.DateLine(now);
        _vm.Media.Update();
        UpdateEqualizer();

        var mediaEvery = _expanded ? 4 : 16;
        if (_tick % mediaEvery == 0) _ = RefreshMediaAsync();
        if (_expanded && _tick % 8 == 0) RefreshAudio();
        if (_expanded && _tick % 120 == 0) UpdateWeatherIfStale();
        if (_tick % 4 == 0) UpdateMeetingHint();
        if (_tick % 4 == 0) UpdateFullscreenState();
        if (_settings.AutoCheckUpdates && !_updateCheckRunning &&
            DateTime.Now - _lastUpdateCheck > TimeSpan.FromHours(6)) _ = AutoCheckUpdatesAsync();
        if (_expanded && _vm.SelectedTab == 4 && _tick % 4 == 0) _vm.System.Update();
        if (_tick % 3600 == 0) _ = RefreshCalendarAsync();
    }

    private async Task RefreshMediaAsync()
    {
        if (_mediaBusy) return;
        _mediaBusy = true;
        try
        {
            await _media.RefreshAsync();
            _vm.Media.Update();
        }
        catch
        {
        }
        finally
        {
            _mediaBusy = false;
        }
    }

    private void RefreshAudio()
    {
        try
        {
            _vm.Sound.Apply(_audio.Snapshot());
        }
        catch
        {
        }
    }

    private async Task LoadWeatherAsync()
    {
        _weatherLoaded = DateTime.UtcNow;
        await _vm.Weather.LoadAsync();
    }

    private void UpdateWeatherIfStale()
    {
        if (DateTime.UtcNow - _weatherLoaded < TimeSpan.FromMinutes(10)) return;
        _ = LoadWeatherAsync();
    }

    private void UpdateEqualizer()
    {
        var shouldRun = _vm.Media.IsPlaying && _vm.Media.HasMedia;
        if (shouldRun == _eqRunning) return;
        _eqRunning = shouldRun;

        var bars = new[] { EqBar1, EqBar2, EqBar3 };
        var durations = new[] { 430, 560, 370 };

        for (var i = 0; i < bars.Length; i++)
        {
            var scale = (ScaleTransform)bars[i].RenderTransform;
            if (shouldRun)
            {
                var animation = new DoubleAnimation(0.3, 1.0, TimeSpan.FromMilliseconds(durations[i]))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
            }
            else
            {
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                scale.ScaleY = 0.35;
            }
        }
    }

    private void SelectTab(int index, bool save = true)
    {
        index = Math.Clamp(index, 0, 4);
        _vm.SelectedTab = index;

        MediaView.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        SoundView.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        WeatherView.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
        PlansView.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;
        SystemView.Visibility = index == 4 ? Visibility.Visible : Visibility.Collapsed;

        TabMedia.IsChecked = index == 0;
        TabSound.IsChecked = index == 1;
        TabWeather.IsChecked = index == 2;
        TabPlans.IsChecked = index == 3;
        TabSystem.IsChecked = index == 4;

        if (index == 1)
        {
            RefreshAudio();
            _vm.Sound.RefreshDevices();
        }
        if (index == 2) UpdateWeatherIfStale();
        if (index == 4) _vm.System.Update();
        UpdateSpectrum();

        if (save)
        {
            _settings.SelectedTab = index;
            SettingsStore.Save(_settings);
        }
    }

    private void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (!_ready || sender is not RadioButton button) return;
        if (button.Tag is not string tag || !int.TryParse(tag, out var index)) return;
        SelectTab(index);
    }

    private void Shell_MouseEnter(object sender, MouseEventArgs e)
    {
        _hoverTimer.Stop();
        if (!_expanded) Expand(activate: false);
    }

    private void Shell_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_expanded || _overlayOpen || _menuOpen) return;
        _hoverTimer.Stop();
        _hoverTimer.Start();
    }

    private void OnHoverTimer(object? sender, EventArgs e)
    {
        _hoverTimer.Stop();
        if (!_expanded || _overlayOpen || _menuOpen) return;
        if (Mouse.LeftButton == MouseButtonState.Pressed)
        {
            _hoverTimer.Start();
            return;
        }

        var focused = Keyboard.FocusedElement as DependencyObject;
        if (focused is not null && Shell.IsAncestorOf(focused))
        {
            _hoverTimer.Start();
            return;
        }

        Collapse();
    }

    private void Expand(bool activate)
    {
        if (_expanded) return;
        _expanded = true;

        CollapsedView.IsHitTestVisible = false;
        ExpandedView.IsHitTestVisible = true;
        ExpandedView.Visibility = Visibility.Visible;

        FadeTo(CollapsedView, 0, Motion(90));
        FadeTo(ExpandedView, 1, Motion(200), Motion(70));
        AnimateShell(ExpandedWidth, ExpandedHeight, 30);

        RefreshAudio();
        _vm.Sound.RefreshDevices();
        UpdateWeatherIfStale();
        _ = RefreshMediaAsync();
        UpdateSpectrum();

        if (_settings.LiveGlass && !_settings.ShowInCapture) StartLiveGlass();

        if (activate) Activate();
    }

    private void Collapse()
    {
        if (!_expanded) return;
        _expanded = false;
        _hoverTimer.Stop();

        CloseOverlay();
        ExpandedView.IsHitTestVisible = false;
        FadeTo(ExpandedView, 0, Motion(110));
        FadeTo(CollapsedView, 1, Motion(150), Motion(70));
        AnimateShell(CollapsedWidth, CollapsedHeight, 22);
        CollapsedView.IsHitTestVisible = true;
        UpdateSpectrum();
        StopLiveGlass();
        RestoreWallpaperGlass();

        if (MenuPopup.IsOpen) MenuPopup.IsOpen = false;
    }

    private void AnimateShell(double width, double height, double radius)
    {
        _fromW = _visualW;
        _fromH = _visualH;
        _fromR = _visualR;
        _toW = width;
        _toH = height;
        _toR = radius;
        _animStart = DateTime.UtcNow;

        if (_animating) return;
        _animating = true;
        CompositionTarget.Rendering += OnAnimationFrame;
    }

    private void OnAnimationFrame(object? sender, EventArgs e)
    {
        var duration = Math.Max(80.0, 340.0 * AnimationFactor);
        var progress = Math.Clamp((DateTime.UtcNow - _animStart).TotalMilliseconds / duration, 0, 1);
        var eased = 1 - Math.Pow(1 - progress, 3);

        var width = _fromW + (_toW - _fromW) * eased;
        var height = _fromH + (_toH - _fromH) * eased;
        var radius = _fromR + (_toR - _fromR) * eased;

        Shell.Width = width;
        Shell.Height = height;
        Shell.CornerRadius = new CornerRadius(radius);
        _visualW = width;
        _visualH = height;
        _visualR = radius;

        UpdateGlassClip(radius);
        UpdateGlassOffset();

        if (progress < 1) return;

        CompositionTarget.Rendering -= OnAnimationFrame;
        _animating = false;
        _visualW = Shell.Width = _toW;
        _visualH = Shell.Height = _toH;
        _visualR = _toR;
        Shell.CornerRadius = new CornerRadius(_toR);
        UpdateGlassClip(_toR);
        UpdateGlassOffset();
    }

    private void UpdateGlassClip(double radius = double.NaN)
    {
        if (double.IsNaN(radius)) radius = _radius;
        if (GlassHost.ActualWidth <= 0 || GlassHost.ActualHeight <= 0) return;

        GlassHost.Clip = new RectangleGeometry(
            new Rect(0, 0, GlassHost.ActualWidth, GlassHost.ActualHeight), radius, radius);
    }

    private void UpdateGlass()
    {
        if (_liveGlassActive) return;
        if (GlassService.Backdrop is null) return;

        GlassImage.Source = GlassService.Backdrop;
        GlassImage.Width = GlassService.MonitorWidth;
        GlassImage.Height = GlassService.MonitorHeight;
        UpdateGlassOffset();
        UpdateGlassClip();
    }

    private void UpdateGlassOffset()
    {
        try
        {
            var source = PresentationSource.FromVisual(this);
            var scale = source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
            var point = Shell.PointToScreen(new Point(0, 0));
            GlassOffset.X = (GlassService.OriginX - point.X) / scale.M11;
            GlassOffset.Y = (GlassService.OriginY - point.Y) / scale.M22;
        }
        catch
        {
        }
    }

    private void OnThemeChanged()
    {
        Theme.Apply(_settings.Theme);
        UpdateTint();
        UpdateGlass();
    }

    private void OnMediaPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MediaViewModel.AccentColor)) UpdateTint();
    }

    private void UpdateTint()
    {
        var density = Math.Clamp(_settings.Density, 30, 100);
        var alpha = 0.28 + (density - 30) / 70.0 * 0.57;
        var light = Theme.Current == "light";
        TintLayer.Background = light
            ? new SolidColorBrush(Color.FromArgb((byte)(alpha * 210), 246, 246, 249))
            : new SolidColorBrush(Color.FromArgb((byte)(alpha * 255), 7, 7, 11));

        var accent = _settings.AccentFromArtwork ? _vm.Media.AccentColor : Colors.Transparent;
        if (accent.A > 0)
        {
            AccentLayer.Background = new SolidColorBrush(Color.FromArgb(44, accent.R, accent.G, accent.B));
            var eq = new SolidColorBrush(Color.FromArgb(255, accent.R, accent.G, accent.B));
            EqBar1.Fill = eq;
            EqBar2.Fill = eq;
            EqBar3.Fill = eq;
        }
        else
        {
            AccentLayer.Background = Brushes.Transparent;
            var eq = new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x9A));
            EqBar1.Fill = eq;
            EqBar2.Fill = eq;
            EqBar3.Fill = eq;
        }
    }

    private double AnimationFactor => _settings.ReduceMotion
        ? 0.35
        : 1.0 / Math.Clamp(_settings.AnimationSpeed, 0.5, 2.0);

    private void UpdateVisualizer()
    {
        _vm.Media.VisualizerEnabled = _settings.VisualizerEnabled;
        UpdateSpectrum();
        MediaView.VisualizerControl.Source = _spectrum;
    }

    private void UpdateSpectrum()
    {
        if (_settings.VisualizerEnabled && _expanded && _vm.SelectedTab == 0 && !_vm.Media.IsLyricsMode && !OnBattery())
            _spectrum.Start();
        else
            _spectrum.Dispose();
    }

    private static bool OnBattery()
    {
        try
        {
            return WinForms.SystemInformation.PowerStatus.PowerLineStatus == WinForms.PowerLineStatus.Offline;
        }
        catch
        {
            return false;
        }
    }

    private void StartLiveGlass()
    {
        if (_liveGlassActive) return;
        if (_settings.ShowInCapture) return;
        if (OnBattery()) return;
        _liveGlassActive = true;
        _liveGlassTimer!.Start();
        OnLiveGlassTick(null, EventArgs.Empty);
    }

    private void StopLiveGlass()
    {
        _liveGlassTimer?.Stop();
        _liveGlassActive = false;
    }

    private void RestoreWallpaperGlass()
    {
        _liveGlassActive = false;
        UpdateGlass();
    }

    private void UpdateFullscreenState()
    {
        if (!_settings.HideInFullscreen)
        {
            if (_hiddenForFullscreen) RestoreFromFullscreen();
            return;
        }

        var fullscreen = FullscreenWatcher.IsFullscreen();
        if (fullscreen && !_hiddenForFullscreen)
        {
            _hiddenForFullscreen = true;
            Collapse();
            Topmost = false;
            Hide();
        }
        else if (!fullscreen && _hiddenForFullscreen)
        {
            RestoreFromFullscreen();
        }
    }

    private void RestoreFromFullscreen()
    {
        _hiddenForFullscreen = false;
        Show();
        Topmost = true;
        ApplyDock();
    }

    private void OnLiveGlassChanged()
    {
        var handle = new WindowInteropHelper(this).Handle;
        LiveGlassService.SetExcluded(handle, _settings.LiveGlass && !_settings.ShowInCapture);

        if (_settings.LiveGlass && !_settings.ShowInCapture && _expanded)
        {
            StartLiveGlass();
            return;
        }

        StopLiveGlass();
        RestoreWallpaperGlass();
    }

    private void OnLiveGlassTick(object? sender, EventArgs e)
    {
        if (!_expanded || !_settings.LiveGlass || _settings.ShowInCapture) return;
        if (OnBattery())
        {
            StopLiveGlass();
            RestoreWallpaperGlass();
            return;
        }
        if (Shell.ActualWidth < 4 || Shell.ActualHeight < 4) return;
        try
        {
            var source = PresentationSource.FromVisual(this);
            var scale = source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
            var point = Shell.PointToScreen(new Point(0, 0));
            var width = (int)Math.Round(Shell.ActualWidth * scale.M11);
            var height = (int)Math.Round(Shell.ActualHeight * scale.M22);

            var frame = LiveGlassService.Grab(
                (int)Math.Round(point.X), (int)Math.Round(point.Y), width, height);

            if (frame is null)
            {
                StopLiveGlass();
                RestoreWallpaperGlass();
                return;
            }

            GlassImage.Source = frame;
            GlassImage.Width = Shell.ActualWidth;
            GlassImage.Height = Shell.ActualHeight;
            GlassOffset.X = 0;
            GlassOffset.Y = 0;
            UpdateGlassClip(_visualR);
        }
        catch
        {
            StopLiveGlass();
            RestoreWallpaperGlass();
        }
    }

    private void ApplyDock()
    {
        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        _monitor = DisplayService.ResolveMonitor(_settings.MonitorId, handle);
        var work = DisplayService.GetUsableWorkArea(_monitor);
        var scale = _monitor.Scale;

        var side = _settings.DockSide;
        var left = side.Contains("Left");
        var right = side.Contains("Right");
        var bottom = side.StartsWith("Bottom", StringComparison.Ordinal);

        Shell.HorizontalAlignment = right ? HorizontalAlignment.Right : left ? HorizontalAlignment.Left : HorizontalAlignment.Center;
        Shell.VerticalAlignment = bottom ? VerticalAlignment.Bottom : VerticalAlignment.Top;
        Shell.Margin = new Thickness(0);

        CollapsedView.HorizontalAlignment = Shell.HorizontalAlignment;
        CollapsedView.VerticalAlignment = Shell.VerticalAlignment;
        ExpandedView.HorizontalAlignment = Shell.HorizontalAlignment;
        ExpandedView.VerticalAlignment = Shell.VerticalAlignment;

        var panelWidth = ExpandedWidth * scale;
        var panelHeight = ExpandedHeight * scale;

        var panelLeft = left
            ? work.Left + 14 * scale
            : right
                ? work.Right - 14 * scale - panelWidth
                : work.Left + (work.Width - panelWidth) / 2.0;
        var panelTop = bottom
            ? work.Bottom - 12 * scale - panelHeight
            : work.Top + 10 * scale;

        panelLeft = Clamp(panelLeft, work.Left, work.Right - panelWidth);
        panelTop = Clamp(panelTop, work.Top, work.Bottom - panelHeight);

        var padLeft = left ? 0 : right ? 24 * scale : 12 * scale;
        var padTop = bottom ? 24 * scale : 0;

        var windowWidth = WindowWidth * scale;
        var windowHeight = WindowHeight * scale;
        var windowX = Clamp(panelLeft - padLeft, _monitor.Left, _monitor.Right - windowWidth);
        var windowY = Clamp(panelTop - padTop, _monitor.Top, _monitor.Bottom - windowHeight);

        if (handle != IntPtr.Zero)
        {
            SetWindowPos(handle, HwndTopmost,
                (int)Math.Round(windowX), (int)Math.Round(windowY),
                (int)Math.Round(windowWidth), (int)Math.Round(windowHeight),
                SwpNoActivate);
        }
        else
        {
            Left = windowX / scale;
            Top = windowY / scale;
        }

        UpdateGlass();
    }

    private static double Clamp(double value, double min, double max) =>
        max < min ? min : Math.Clamp(value, min, max);

    private void OnDockChanged()
    {
        ApplyDock();
        GlassService.Initialize(_monitor);
        UpdateGlass();
    }

    private void RefreshZone()
    {
        _zone = TimeZoneInfo.Local;
        if (string.IsNullOrWhiteSpace(_settings.TimeZoneId)) return;

        try
        {
            _zone = TimeZoneInfo.FindSystemTimeZoneById(_settings.TimeZoneId);
        }
        catch
        {
            _zone = TimeZoneInfo.Local;
        }
    }

    private DateTime NowInZone() => TimeZoneInfo.ConvertTime(DateTimeOffset.Now, _zone).DateTime;

    private void OnTimeZoneChanged()
    {
        RefreshZone();
        var now = NowInZone();
        _vm.Clock = now.ToString("HH:mm", L.Culture);
        _vm.DateLine = L.DateLine(now);
    }

    private static string AppVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version is null ? "0.0.1" : version.ToString(3);
    }

    private void CreateTray()
    {
        try
        {
            _appIcon = BuildAppIcon(out var handle);
            _trayHandle = handle;

            _tray = new WinForms.NotifyIcon
            {
                Icon = _appIcon,
                Visible = true,
                Text = L.T("AppName") + " " + AppVersion()
            };

            _tray.DoubleClick += (_, _) => ShowFromTray();
            RebuildTrayMenu();
        }
        catch
        {
            _tray = null;
        }
    }

    private static System.Drawing.Icon BuildAppIcon(out IntPtr handle)
    {
        using var bitmap = new System.Drawing.Bitmap(32, 32);
        using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.Clear(System.Drawing.Color.Transparent);

            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            var rect = new System.Drawing.RectangleF(2, 8, 28, 16);
            const float radius = 8f;
            path.AddArc(rect.X, rect.Y, radius, radius, 180, 90);
            path.AddArc(rect.Right - radius, rect.Y, radius, radius, 270, 90);
            path.AddArc(rect.Right - radius, rect.Bottom - radius, radius, radius, 0, 90);
            path.AddArc(rect.X, rect.Bottom - radius, radius, radius, 90, 90);
            path.CloseFigure();

            using var fill = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(255, 12, 12, 18));
            graphics.FillPath(fill, path);
            using var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(110, 255, 255, 255), 1f);
            graphics.DrawPath(pen, path);
            using var dot = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(255, 242, 242, 248));
            graphics.FillEllipse(dot, 7, 13, 6, 6);
        }

        handle = bitmap.GetHicon();
        return System.Drawing.Icon.FromHandle(handle);
    }

    private void RebuildTrayMenu()
    {
        if (_tray is null) return;

        _trayMenu?.Dispose();
        _trayMenu = new WinForms.ContextMenuStrip();

        var open = new WinForms.ToolStripMenuItem(L.T("TrayOpen"), null, (_, _) => ShowFromTray());
        open.Font = new System.Drawing.Font(open.Font, System.Drawing.FontStyle.Bold);
        _trayMenu.Items.Add(open);
        _trayMenu.Items.Add(L.T("MenuSettings"), null, (_, _) =>
        {
            ShowFromTray();
            OpenOverlay("settings");
        });
        _trayMenu.Items.Add(L.T("MenuAbout"), null, (_, _) =>
        {
            ShowFromTray();
            OpenOverlay("about");
        });
        _trayMenu.Items.Add(new WinForms.ToolStripSeparator());

        var autoStart = new WinForms.ToolStripMenuItem(L.T("AutoStart")) { Checked = _settings.AutoStart };
        autoStart.Click += (_, _) =>
        {
            _vm.Settings.AutoStart = !_vm.Settings.AutoStart;
            autoStart.Checked = _vm.Settings.AutoStart;
        };
        _trayMenu.Items.Add(autoStart);
        _trayMenu.Items.Add(new WinForms.ToolStripSeparator());
        _trayMenu.Items.Add(L.T("TrayExit"), null, (_, _) => Close());

        _tray.ContextMenuStrip = _trayMenu;
        _tray.Text = L.T("AppName") + " " + AppVersion();
    }

    private void ShowFromTray()
    {
        Show();
        Activate();
        Expand(activate: true);
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    private void OnLanguageChanged()
    {
        var now = NowInZone();
        _vm.DateLine = L.DateLine(now);
        _vm.Weather.RefreshLanguage();
        _vm.Sound.RefreshLanguage();

        PlansView.CloseEditorIfOpen();
        _vm.Plans.Reload();

        SettingsView.SyncFromSettings();
        AboutView.RefreshLanguage();
        RebuildTrayMenu();
    }

    private int Motion(int milliseconds) =>
        Math.Max(40, (int)Math.Round(milliseconds * AnimationFactor));

    private void FadeTo(UIElement element, double target, int milliseconds, int delay = 0)
    {
        var from = element.Opacity;
        if (Math.Abs(from - target) < 0.01 && element.Visibility == Visibility.Visible) return;

        element.BeginAnimation(OpacityProperty, null);
        element.Opacity = target;
        if (target > 0) element.Visibility = Visibility.Visible;

        var animation = new DoubleAnimation(from, target, TimeSpan.FromMilliseconds(milliseconds))
        {
            BeginTime = TimeSpan.FromMilliseconds(delay),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        };

        animation.Completed += (_, _) =>
        {
            element.BeginAnimation(OpacityProperty, null);
            element.Opacity = target;
            if (target <= 0.01) element.Visibility = Visibility.Collapsed;
        };

        element.BeginAnimation(OpacityProperty, animation);
    }

    private void PlayIntro()
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var transform = (TranslateTransform)Shell.RenderTransform;

        transform.Y = -10;
        transform.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(-10, 0, TimeSpan.FromMilliseconds(460))
            {
                BeginTime = TimeSpan.FromMilliseconds(100),
                EasingFunction = ease
            });

        Shell.Opacity = 0;
        Shell.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(420))
            {
                BeginTime = TimeSpan.FromMilliseconds(100),
                EasingFunction = ease
            });
    }

    private void Shell_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_expanded) Expand(activate: true);
    }

    private void Collapse_Click(object sender, RoutedEventArgs e) => Collapse();

    private void WeatherChip_Click(object sender, MouseButtonEventArgs e)
    {
        Expand(activate: true);
        SelectTab(2);
        e.Handled = true;
    }

    private void OpenMenu_Click(object sender, RoutedEventArgs e)
    {
        _menuOpen = true;
        MenuPopup.HorizontalOffset = MenuButton.ActualWidth - 184;
        MenuPopup.VerticalOffset = 4;
        MenuPopup.IsOpen = true;
    }

    private void MenuPopup_Closed(object? sender, EventArgs e)
    {
        _menuOpen = false;
        if (!_collapseAfterMenu) return;
        _collapseAfterMenu = false;
        if (_expanded) Collapse();
    }

    private async Task AutoCheckUpdatesAsync()
    {
        if (_updateCheckRunning || string.IsNullOrWhiteSpace(_settings.UpdateRepo)) return;

        _updateCheckRunning = true;
        _lastUpdateCheck = DateTime.Now;
        try
        {
            var current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 1);
            var info = await UpdateService.CheckAsync(_settings.UpdateRepo, current);
            if (!info.Ok || !info.Newer) return;
            if (string.Equals(info.Tag, _settings.LastNotifiedUpdate, StringComparison.OrdinalIgnoreCase)) return;

            _settings.LastNotifiedUpdate = info.Tag;
            SettingsStore.Save(_settings);

            var asset = UpdateService.PickAsset(info);
            if (asset is not null && !string.IsNullOrWhiteSpace(info.Sha256))
            {
                var destination = System.IO.Path.Combine(System.IO.Path.GetTempPath(), asset.Name);
                var (ok, file, _) = await UpdateService.DownloadAsync(asset, destination, info.Sha256, default);
                if (ok)
                {
                    _pendingUpdate = info;
                    _pendingUpdateFile = file;
                    ShowInfoToast(L.T("MenuUpdate"), L.F("UpdateReady", info.Tag), "\uE895", Color.FromRgb(0x4F, 0xA8, 0xFF));
                    return;
                }
            }

            ShowInfoToast(L.T("MenuUpdate"), L.F("UpdateAvailable", info.Tag), "\uE895", Color.FromRgb(0x4F, 0xA8, 0xFF));
        }
        catch
        {
            // silent: background check must never disturb the user
        }
        finally
        {
            _updateCheckRunning = false;
        }
    }

    private bool _updating;

    private async void MenuUpdate_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        if (_updating) return;

        if (_pendingUpdate is not null && !string.IsNullOrWhiteSpace(_pendingUpdateFile) &&
            System.IO.File.Exists(_pendingUpdateFile))
        {
            var file = System.IO.Path.GetFileName(_pendingUpdateFile);
            var prompt = MessageBox.Show(this, L.F("UpdateRun", file), L.T("MenuUpdate"),
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (prompt == MessageBoxResult.Yes)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_pendingUpdateFile)
                {
                    UseShellExecute = true
                });
                Close();
            }
            return;
        }

        if (string.IsNullOrWhiteSpace(_settings.UpdateRepo))
        {
            ShowInfoToast(L.T("AppName"), L.T("UpdateNotConfigured"), "\uE895", Color.FromRgb(0x8E, 0x8E, 0x9A), null);
            return;
        }

        _updating = true;
        try
        {
            var current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 1);
            var info = await UpdateService.CheckAsync(_settings.UpdateRepo, current);

            if (!info.Ok)
            {
                ShowInfoToast(L.T("MenuUpdate"), L.T("UpdateFailed"), "\uE895", Color.FromRgb(0xFF, 0x6B, 0x5E), null);
                return;
            }

            if (!info.Newer)
            {
                ShowInfoToast(L.T("MenuUpdate"), L.T("UpdateLatest"), "\uE895", Color.FromRgb(0x4F, 0xA8, 0xFF), null);
                return;
            }

            var asset = UpdateService.PickAsset(info);
            if (asset is null || string.IsNullOrWhiteSpace(info.Sha256))
            {
                ShowInfoToast(L.T("MenuUpdate"), L.T("UpdateNoHash"), "\uE895", Color.FromRgb(0xFF, 0x9F, 0x0A), null);
                return;
            }

            var confirm = MessageBox.Show(this, L.F("UpdateConfirm", info.Tag, asset.Name), L.T("MenuUpdate"),
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            ShowInfoToast(L.T("MenuUpdate"), L.F("UpdateDownloading", info.Tag), "\uE895", Color.FromRgb(0x4F, 0xA8, 0xFF), null);

            var destination = System.IO.Path.Combine(System.IO.Path.GetTempPath(), asset.Name);
            var (ok, file, error) = await UpdateService.DownloadAsync(asset, destination, info.Sha256, default);

            if (!ok)
            {
                var message = error == "hash-mismatch" ? L.T("UpdateHashFailed") : L.T("UpdateDownloadFailed");
                ShowInfoToast(L.T("MenuUpdate"), message, "\uE895", Color.FromRgb(0xFF, 0x6B, 0x5E), null);
                return;
            }

            var run = MessageBox.Show(this, L.F("UpdateRun", asset.Name), L.T("MenuUpdate"),
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (run != MessageBoxResult.Yes) return;

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(file) { UseShellExecute = true });
            Close();
        }
        catch
        {
            ShowInfoToast(L.T("MenuUpdate"), L.T("UpdateFailed"), "\uE895", Color.FromRgb(0xFF, 0x6B, 0x5E), null);
        }
        finally
        {
            _updating = false;
        }
    }

    private void MenuRefresh_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        _ = LoadWeatherAsync();
    }

    private void MenuCollapse_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        Collapse();
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        Close();
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e) => OpenOverlay("settings");

    private void OnNoteSaved(NoteViewModel note)
    {
        if (note.Model.Pinned) ShowSticky(note.Model);
        else CloseSticky(note.Model.Id);
    }

    private void ShowSticky(NoteItem note)
    {
        if (_stickies.ContainsKey(note.Id)) return;

        var sticky = new StickyWindow(note, OnStickyClosed);
        sticky.Show();
        _stickies[note.Id] = sticky;
    }

    private void CloseSticky(string id)
    {
        if (!_stickies.TryGetValue(id, out var sticky)) return;
        _stickies.Remove(id);
        sticky.Close();
    }

    private void OnStickyClosed(StickyWindow sticky, bool unpin)
    {
        foreach (var pair in _stickies.ToList())
        {
            if (pair.Value != sticky) continue;
            _stickies.Remove(pair.Key);

            var note = _settings.Notes.FirstOrDefault(n => n.Id == pair.Key);
            if (note is null) break;

            note.PinX = sticky.Left;
            note.PinY = sticky.Top;
            if (unpin) note.Pinned = false;
            SettingsStore.Save(_settings);
            if (unpin) _vm.Plans.Reload();
            break;
        }
    }

    private void OnCalendarChanged() => _ = RefreshCalendarAsync();

    private async Task RefreshCalendarAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.CalendarUrl))
        {
            _settings.CalendarCache.Clear();
            UpdateMeetingHint();
            return;
        }

        try
        {
            var events = await _calendar.FetchAsync(_settings.CalendarUrl,
                DateTime.Now.AddHours(-2), DateTime.Now.AddDays(7), default);
            _settings.CalendarCache = events;
            _settings.CalendarFetchedAt = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);
            SettingsStore.Save(_settings);
        }
        catch
        {
            // offline: keep the cached events
        }

        UpdateMeetingHint();
    }

    private void UpdateMeetingHint()
    {
        var now = DateTime.Now;
        CalendarEvent? next = null;
        var nextStart = DateTime.MaxValue;

        foreach (var item in _settings.CalendarCache)
        {
            if (!DateTime.TryParse(item.Start, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var start)) continue;
            if (start < now.AddMinutes(-1) || start >= nextStart) continue;
            next = item;
            nextStart = start;
        }

        if (next is null)
        {
            _vm.MeetingHint = "";
            return;
        }

        var minutes = (int)Math.Round((nextStart - now).TotalMinutes);
        _vm.MeetingHint = minutes <= 60
            ? minutes <= 15
                ? L.F("MeetingSoon", Math.Max(1, minutes), next.Summary)
                : L.F("MeetingAt", nextStart.ToString("HH:mm", CultureInfo.InvariantCulture), next.Summary)
            : "";

        if (minutes is > 0 and <= 15)
        {
            var key = next.Uid + "|" + nextStart.Ticks;
            if (_meetingToasts.Add(key))
                ShowInfoToast(L.T("Calendar"), L.F("MeetingSoon", minutes, next.Summary), "\uE787",
                    Color.FromRgb(0x8F, 0xB8, 0xFF), null);
        }
    }

    private void OnQuickTimer(int minutes)
    {
        _timers.Start(TimeSpan.FromMinutes(minutes));
        CloseOverlay();
        Collapse();
    }

    private void OnTimerRemaining(TimeSpan remaining)
    {
        var active = _timers.IsRunning && remaining > TimeSpan.Zero;
        _vm.HasTimer = active;
        _vm.TimerText = active
            ? ((int)remaining.TotalMinutes).ToString("00", CultureInfo.InvariantCulture) + ":" +
              remaining.Seconds.ToString("00", CultureInfo.InvariantCulture)
            : "";
    }

    private void OnTimerFinished()
    {
        var minutes = (int)Math.Round(_timers.Total.TotalMinutes);
        _vm.HasTimer = false;
        _vm.TimerText = "";
        ShowToast(L.T("Timer"), L.F("TimerFinished", minutes), "\uE916", Color.FromRgb(0xFF, 0xB8, 0x6B), null);
    }

    private void TimerChip_Click(object sender, MouseButtonEventArgs e)
    {
        _timers.Stop();
        _vm.HasTimer = false;
        _vm.TimerText = "";
    }

    private void MenuQuick_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        OpenCommandBar();
    }

    private void OpenCommandBar()
    {
        if (!_expanded) Expand(activate: true);
        OpenOverlay("command");
    }

    private void ToggleCommandBar()
    {
        if (_overlayOpen && _overlay == "command")
        {
            CloseOverlay();
            Collapse();
            return;
        }

        OpenCommandBar();
    }

    private void OnQuickNote(string text)
    {
        var note = _vm.Plans.AddNote();
        note.Model.Title = text;
        _vm.Plans.Save();
        note.Refresh();

        CloseOverlay();
        SelectTab(3);
    }

    private void OnQuickReminder(string text, ParsedReminder parsed)
    {
        var reminder = _vm.Plans.AddReminder();
        reminder.Model.Title = text;
        reminder.Model.Repeat = parsed.Repeat;
        reminder.Model.Time = parsed.Time;
        reminder.Model.Weekdays = parsed.Weekdays;
        if (!string.IsNullOrWhiteSpace(parsed.Date)) reminder.Model.Date = parsed.Date;
        reminder.Model.Enabled = true;
        reminder.Model.LastFired = "";
        reminder.Model.SnoozeUntil = "";

        _vm.Plans.Save();
        reminder.Refresh();

        CloseOverlay();
        SelectTab(3);
    }

    private void MenuSettings_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        OpenOverlay("settings");
    }

    private void MenuAbout_Click(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        OpenOverlay("about");
    }

    private void OpenAbout()
    {
        _aboutFromSettings = _overlay == "settings";
        if (!_overlayOpen || _overlay != "about") OpenOverlay("about");
    }

    private void OpenOverlay(string name)
    {
        if (!_expanded) Expand(activate: true);

        _overlay = name;
        _overlayOpen = true;

        TabsBorder.Visibility = Visibility.Collapsed;
        PagesHost.Visibility = Visibility.Collapsed;
        OverlayHost.Visibility = Visibility.Visible;
        SettingsView.Visibility = name == "settings" ? Visibility.Visible : Visibility.Collapsed;
        AboutView.Visibility = name == "about" ? Visibility.Visible : Visibility.Collapsed;
        CommandView.Visibility = name == "command" ? Visibility.Visible : Visibility.Collapsed;

        BackButton.Visibility = Visibility.Visible;
        SettingsButton.Visibility = Visibility.Collapsed;
        MenuButton.Visibility = Visibility.Collapsed;
        CollapseButton.Visibility = Visibility.Collapsed;

        if (name == "settings")
        {
            SettingsView.SyncFromSettings();
        }
        else if (name == "command")
        {
            CommandView.Reset();
            Dispatcher.BeginInvoke(new Action(() => CommandView.FocusInput()), DispatcherPriority.Input);
        }
        else
        {
            AboutView.RefreshLanguage();
        }

        Activate();
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_overlay == "about" && _aboutFromSettings)
        {
            _aboutFromSettings = false;
            OpenOverlay("settings");
            return;
        }

        CloseOverlay();
    }

    private void CloseOverlay()
    {
        if (!_overlayOpen) return;

        _overlayOpen = false;
        _overlay = "";
        OverlayHost.Visibility = Visibility.Collapsed;
        SettingsView.Visibility = Visibility.Collapsed;
        AboutView.Visibility = Visibility.Collapsed;
        CommandView.Visibility = Visibility.Collapsed;

        TabsBorder.Visibility = Visibility.Visible;
        PagesHost.Visibility = Visibility.Visible;
        BackButton.Visibility = Visibility.Collapsed;
        SettingsButton.Visibility = Visibility.Visible;
        MenuButton.Visibility = Visibility.Visible;
        CollapseButton.Visibility = Visibility.Visible;
    }

    private Rect IslandScreenRect()
    {
        try
        {
            var source = PresentationSource.FromVisual(this);
            var scale = source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
            var point = Shell.PointToScreen(new Point(0, 0));
            return new Rect(point.X / scale.M11, point.Y / scale.M22, Shell.ActualWidth, Shell.ActualHeight);
        }
        catch
        {
            return new Rect(0, 0, 1, 1);
        }
    }

    private void OnReminderDue(ReminderItem reminder)
    {
        SettingsStore.Save(_settings);
        var title = string.IsNullOrWhiteSpace(reminder.Title) ? L.T("ReminderDue") : reminder.Title;
        var message = ReminderLogic.Describe(reminder);

        ShowToast(title, message, "\uEA8F", Color.FromRgb(0x4F, 0xA8, 0xFF), minutes =>
        {
            reminder.SnoozeUntil = DateTime.Now.AddMinutes(minutes).ToString("o", CultureInfo.InvariantCulture);
            reminder.LastFired = "";
            SettingsStore.Save(_settings);
        });
    }

    private void OnNoteDue(NoteItem note)
    {
        SettingsStore.Save(_settings);
        var title = string.IsNullOrWhiteSpace(note.Title) ? L.T("NoteDue") : note.Title;
        var color = note.Color switch
        {
            1 => Color.FromRgb(0xFF, 0x6B, 0x9A),
            2 => Color.FromRgb(0x59, 0xA8, 0xFF),
            3 => Color.FromRgb(0x32, 0xD7, 0x4B),
            _ => Color.FromRgb(0xF5, 0xC5, 0x42)
        };

        ShowToast(title, note.Body, "\uE70B", color, minutes =>
        {
            note.ReminderAt = DateTime.Now.AddMinutes(minutes).ToString("o", CultureInfo.InvariantCulture);
            SettingsStore.Save(_settings);
        });
    }

    /// <summary>Informational toast: real Windows toast when available, otherwise the custom card.</summary>
    private void ShowInfoToast(string title, string message, string glyph, Color accent, Action<int>? snooze = null)
    {
        if (ToastService.TryShow(title, message)) return;
        ShowToast(title, message, glyph, accent, snooze);
    }

    private void ShowToast(string title, string message, string glyph, Color accent, Action<int>? snooze)
    {
        if (_toast is not null)
        {
            _toastQueue.Enqueue((title, message, glyph, accent, snooze));
            return;
        }

        if (!NotificationPolicy.ShouldSilence(_settings, DateTime.Now))
            NotificationSoundService.Play(_settings.NotificationSound);

        var toast = new ToastWindow(title, message, glyph, accent, snooze);
        toast.PlaceNear(IslandScreenRect());
        toast.Closed += (_, _) =>
        {
            _toast = null;
            if (_toastQueue.Count > 0)
            {
                var next = _toastQueue.Dequeue();
                ShowToast(next.Title, next.Message, next.Glyph, next.Accent, next.Snooze);
            }
        };

        _toast = toast;
        toast.Show();
    }
}