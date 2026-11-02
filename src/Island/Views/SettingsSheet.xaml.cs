using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Island.Services;
using Island.ViewModels;

namespace Island.Views;

public sealed class ZoneOption
{
    public string Id { get; init; } = "";
    public string Display { get; init; } = "";
    public override string ToString() => Display;
}

public sealed class SoundOption
{
    public string Id { get; init; } = "";
    public string Display { get; init; } = "";
    public override string ToString() => Display;
}

public sealed class MonitorOption
{
    public string Id { get; init; } = "";
    public string Display { get; init; } = "";
    public override string ToString() => Display;
}

public partial class SettingsSheet : UserControl
{
    private bool _ready;

    public SettingsSheet()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            _ready = true;
            SyncFromSettings();
        };
    }

    public event Action? AboutRequested;

    private MainViewModel Vm => (MainViewModel)DataContext;

    public void SyncFromSettings()
    {
        if (Vm is null) return;

        _ready = false;
        try
        {
            ThemeDark.IsChecked = Vm.Settings.Theme == "dark";
            ThemeLight.IsChecked = Vm.Settings.Theme == "light";
            ThemeAuto.IsChecked = Vm.Settings.Theme == "auto";

            LangRu.IsChecked = Vm.Settings.Language == "ru";
            LangEn.IsChecked = Vm.Settings.Language == "en";

            foreach (var button in new[] { DockTopLeft, DockTopCenter, DockTopRight, DockBottomLeft, DockBottomCenter, DockBottomRight })
                button.IsChecked = (string)button.Tag == Vm.Settings.DockSide;

            var zones = new List<ZoneOption> { new() { Id = "", Display = L.T("SystemTime") } };
            zones.AddRange(TimeZoneInfo.GetSystemTimeZones()
                .OrderBy(z => z.BaseUtcOffset)
                .ThenBy(z => z.DisplayName, StringComparer.CurrentCulture)
                .Select(z => new ZoneOption { Id = z.Id, Display = z.DisplayName }));

            TimeZoneCombo.ItemsSource = zones;

            var currentZoneId = "";
            if (!string.IsNullOrWhiteSpace(Vm.Settings.TimeZoneId))
            {
                try { currentZoneId = TimeZoneInfo.FindSystemTimeZoneById(Vm.Settings.TimeZoneId).Id; }
                catch { currentZoneId = Vm.Settings.TimeZoneId; }
            }

            TimeZoneCombo.SelectedItem = zones.FirstOrDefault(z => z.Id == currentZoneId) ?? zones[0];

            var sounds = new List<SoundOption>
            {
                new() { Id = "none", Display = L.T("SoundNone") },
                new() { Id = "soft", Display = L.T("SoundSoft") },
                new() { Id = "bright", Display = L.T("SoundBright") },
                new() { Id = "system", Display = L.T("SoundSystem") }
            };

            SoundCombo.ItemsSource = sounds;
            SoundCombo.SelectedItem = sounds.FirstOrDefault(s => s.Id == Vm.Settings.NotificationSound) ?? sounds[1];

            var monitors = new List<MonitorOption>
            {
                new() { Id = "primary", Display = L.T("MonitorPrimary") },
                new() { Id = "cursor", Display = L.T("MonitorCursor") }
            };
            monitors.AddRange(DisplayService.GetMonitors()
                .Select(m => new MonitorOption { Id = m.DeviceName, Display = m.DisplayName }));

            MonitorCombo.ItemsSource = monitors;
            MonitorCombo.SelectedItem = monitors.FirstOrDefault(m => m.Id == Vm.Settings.MonitorId) ?? monitors[0];

            AutoStartSwitch.IsChecked = Vm.Settings.AutoStart;
        }
        finally
        {
            _ready = true;
        }
    }

    private void Theme_Checked(object sender, RoutedEventArgs e)
    {
        if (!_ready || Vm is null) return;
        if (((FrameworkElement)sender).Tag is string theme)
            Vm.Settings.Theme = theme;
    }

    private void Lang_Checked(object sender, RoutedEventArgs e)
    {
        if (!_ready || Vm is null) return;
        if (((FrameworkElement)sender).Tag is string language)
            Vm.Settings.Language = language;
    }

    private void Dock_Click(object sender, RoutedEventArgs e)
    {
        if (Vm is null) return;

        var button = (ToggleButton)sender;
        var side = (string)button.Tag;

        foreach (var other in new[] { DockTopLeft, DockTopCenter, DockTopRight, DockBottomLeft, DockBottomCenter, DockBottomRight })
            other.IsChecked = other == button;

        button.IsChecked = true;
        Vm.Settings.DockSide = side;
    }

    private void Monitor_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || Vm is null) return;
        if (MonitorCombo.SelectedItem is MonitorOption monitor)
            Vm.Settings.MonitorId = monitor.Id;
    }

    private void TimeZone_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || Vm is null) return;
        if (TimeZoneCombo.SelectedItem is ZoneOption zone)
            Vm.Settings.TimeZoneId = zone.Id;
    }

    private void Sound_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || Vm is null) return;
        if (SoundCombo.SelectedItem is not SoundOption sound) return;

        var changed = Vm.Settings.NotificationSound != sound.Id;
        Vm.Settings.NotificationSound = sound.Id;
        if (changed) NotificationSoundService.Play(sound.Id);
    }

    private void About_Click(object sender, RoutedEventArgs e) => AboutRequested?.Invoke();
}