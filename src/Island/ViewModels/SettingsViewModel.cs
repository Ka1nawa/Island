using System;
using Island.Core;
using Island.Services;

namespace Island.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _settings;

    public SettingsViewModel(AppSettings settings)
    {
        _settings = settings;
        _language = settings.Language;
        _dockSide = settings.DockSide;
        _density = Math.Clamp(settings.Density, 30, 100);
        _visualizerEnabled = settings.VisualizerEnabled;
    }

    public event Action? DockChanged;
    public event Action? MonitorChanged;
    public event Action? DensityChanged;
    public event Action? VisualizerChanged;
    public event Action? LiveGlassChanged;
    public event Action? CalendarChanged;
    public event Action? TimeZoneChanged;

    private string _language;
    public string Language
    {
        get => _language;
        set
        {
            if (!SetProperty(ref _language, value)) return;
            _settings.Language = value;
            SettingsStore.Save(_settings);
            L.Apply(value);
        }
    }

    private string _dockSide;
    public string DockSide
    {
        get => _dockSide;
        set
        {
            if (!SetProperty(ref _dockSide, value)) return;
            _settings.DockSide = value;
            SettingsStore.Save(_settings);
            DockChanged?.Invoke();
        }
    }

    public string Theme
    {
        get => _settings.Theme;
        set
        {
            var normalized = value == "light" ? "light" : value == "auto" ? "auto" : "dark";
            if (_settings.Theme == normalized) return;
            _settings.Theme = normalized;
            SettingsStore.Save(_settings);
            OnPropertyChanged();
            ThemeChanged?.Invoke();
        }
    }

    public string MonitorId
    {
        get => _settings.MonitorId;
        set
        {
            if (_settings.MonitorId == value) return;
            _settings.MonitorId = value;
            SettingsStore.Save(_settings);
            OnPropertyChanged();
            MonitorChanged?.Invoke();
        }
    }

    private double _density;
    public double Density
    {
        get => _density;
        set
        {
            var clamped = Math.Clamp(value, 30, 100);
            if (Math.Abs(_density - clamped) < 0.1) return;
            _density = clamped;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DensityText));
            _settings.Density = clamped;
            SettingsStore.Save(_settings);
            DensityChanged?.Invoke();
        }
    }

    public string DensityText => $"{(int)Math.Round(_density)}%";

    public bool AutoStart
    {
        get => _settings.AutoStart;
        set
        {
            if (_settings.AutoStart == value) return;
            _settings.AutoStart = value;
            AutoStartService.Apply(value);
            SettingsStore.Save(_settings);
            OnPropertyChanged();
        }
    }

    public string TimeZoneId
    {
        get => _settings.TimeZoneId;
        set
        {
            if (_settings.TimeZoneId == value) return;
            _settings.TimeZoneId = value;
            SettingsStore.Save(_settings);
            OnPropertyChanged();
            TimeZoneChanged?.Invoke();
        }
    }

    public bool QuietHours
    {
        get => _settings.QuietHours;
        set
        {
            if (_settings.QuietHours == value) return;
            _settings.QuietHours = value;
            SettingsStore.Save(_settings);
            OnPropertyChanged();
        }
    }

    public string QuietFrom
    {
        get => _settings.QuietFrom;
        set
        {
            if (_settings.QuietFrom == value) return;
            _settings.QuietFrom = value;
            SettingsStore.Save(_settings);
            OnPropertyChanged();
        }
    }

    public string QuietTo
    {
        get => _settings.QuietTo;
        set
        {
            if (_settings.QuietTo == value) return;
            _settings.QuietTo = value;
            SettingsStore.Save(_settings);
            OnPropertyChanged();
        }
    }

    public event Action? AccentChanged;
    public event Action? ThemeChanged;

    public bool AccentFromArtwork
    {
        get => _settings.AccentFromArtwork;
        set
        {
            if (_settings.AccentFromArtwork == value) return;
            _settings.AccentFromArtwork = value;
            SettingsStore.Save(_settings);
            OnPropertyChanged();
            AccentChanged?.Invoke();
        }
    }

    public double AnimationSpeed
    {
        get => _settings.AnimationSpeed;
        set
        {
            var clamped = Math.Clamp(value, 0.5, 2.0);
            if (Math.Abs(_settings.AnimationSpeed - clamped) < 0.001) return;
            _settings.AnimationSpeed = clamped;
            SettingsStore.Save(_settings);
            OnPropertyChanged();
            OnPropertyChanged(nameof(AnimationSpeedText));
        }
    }

    public string AnimationSpeedText => _settings.AnimationSpeed.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "×";

    public bool ReduceMotion
    {
        get => _settings.ReduceMotion;
        set
        {
            if (_settings.ReduceMotion == value) return;
            _settings.ReduceMotion = value;
            SettingsStore.Save(_settings);
            OnPropertyChanged();
        }
    }

    public string NotesSyncFolder
    {
        get => _settings.NotesSyncFolder;
        set
        {
            if (_settings.NotesSyncFolder == value) return;
            _settings.NotesSyncFolder = value;
            SettingsStore.Save(_settings);
            OnPropertyChanged();
        }
    }

    public string CalendarUrl
    {
        get => _settings.CalendarUrl;
        set
        {
            if (_settings.CalendarUrl == value) return;
            _settings.CalendarUrl = value;
            SettingsStore.Save(_settings);
            OnPropertyChanged();
            CalendarChanged?.Invoke();
        }
    }

    public string UpdateRepo
    {
        get => _settings.UpdateRepo;
        set
        {
            if (_settings.UpdateRepo == value) return;
            _settings.UpdateRepo = value;
            SettingsStore.Save(_settings);
            OnPropertyChanged();
        }
    }

    public string NotificationSound
    {
        get => _settings.NotificationSound;
        set
        {
            if (_settings.NotificationSound == value) return;
            _settings.NotificationSound = value;
            SettingsStore.Save(_settings);
            OnPropertyChanged();
        }
    }

    public bool LiveGlass
    {
        get => _settings.LiveGlass;
        set
        {
            if (_settings.LiveGlass == value) return;
            _settings.LiveGlass = value;
            SettingsStore.Save(_settings);
            OnPropertyChanged();
            LiveGlassChanged?.Invoke();
        }
    }

    private bool _visualizerEnabled;
    public bool VisualizerEnabled
    {
        get => _visualizerEnabled;
        set
        {
            if (!SetProperty(ref _visualizerEnabled, value)) return;
            _settings.VisualizerEnabled = value;
            SettingsStore.Save(_settings);
            VisualizerChanged?.Invoke();
        }
    }
}