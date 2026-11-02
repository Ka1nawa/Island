using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;
using Island.Core;
using Island.Services;

namespace Island.ViewModels;

public sealed class AudioAppViewModel : ObservableObject
{
    private readonly AudioService _audio;
    private bool _applying;
    private DateTime _touched = DateTime.MinValue;

    public AudioAppViewModel(AudioService audio, AudioAppSnapshot snapshot)
    {
        _audio = audio;
        Key = snapshot.Key;
        ApplyStatic(snapshot);
        _volume = snapshot.Volume * 100;
        _muted = snapshot.Muted;
    }

    public string Key { get; }
    public string Name { get; private set; } = "";
    public ImageSource? Icon { get; private set; }
    public bool HasIcon => Icon is not null;
    public bool IsSystem { get; private set; }
    public bool IsActive { get; private set; }

    public string Initial => string.IsNullOrEmpty(Name) ? "?" : Name[..1].ToUpperInvariant();

    public void RefreshLanguage()
    {
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(MuteGlyph));
    }
    public bool ShowInitial => !IsSystem && Icon is null;
    public bool ShowSpeaker => IsSystem;

    public string Subtitle => IsSystem ? L.T("System") : IsActive ? L.T("NowPlayingSub") : L.T("PausedSub");
    public string MuteGlyph => _muted ? "\uE74F" : "\uE767";

    private double _volume = 100;
    public double Volume
    {
        get => _volume;
        set
        {
            var clamped = Math.Clamp(value, 0, 100);
            if (Math.Abs(_volume - clamped) < 0.01) return;
            _volume = clamped;
            OnPropertyChanged();
            if (_applying) return;
            _touched = DateTime.UtcNow;
            _audio.SetAppVolume(Key, (float)(clamped / 100.0));
        }
    }

    private bool _muted;
    public bool Muted
    {
        get => _muted;
        set
        {
            if (_muted == value) return;
            _muted = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MuteGlyph));
            if (_applying) return;
            _touched = DateTime.UtcNow;
            _audio.SetAppMute(Key, value);
        }
    }

    public void Apply(AudioAppSnapshot snapshot)
    {
        var nameChanged = Name != snapshot.Name;
        var iconChanged = !ReferenceEquals(Icon, snapshot.Icon);
        var activeChanged = IsActive != snapshot.IsActive;

        ApplyStatic(snapshot);

        if (nameChanged)
        {
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Initial));
        }
        if (iconChanged)
        {
            OnPropertyChanged(nameof(Icon));
            OnPropertyChanged(nameof(HasIcon));
            OnPropertyChanged(nameof(ShowInitial));
        }
        if (activeChanged) OnPropertyChanged(nameof(Subtitle));

        if (DateTime.UtcNow - _touched < TimeSpan.FromSeconds(2)) return;

        _applying = true;
        _volume = snapshot.Volume * 100;
        _muted = snapshot.Muted;
        _applying = false;
        OnPropertyChanged(nameof(Volume));
        OnPropertyChanged(nameof(Muted));
        OnPropertyChanged(nameof(MuteGlyph));
    }

    private void ApplyStatic(AudioAppSnapshot snapshot)
    {
        Name = snapshot.Name;
        Icon = snapshot.Icon;
        IsSystem = snapshot.IsSystem;
        IsActive = snapshot.IsActive;
    }
}

public sealed class AudioDeviceViewModel : ObservableObject
{
    public AudioDeviceViewModel(AudioDevice device)
    {
        Id = device.Id;
        Name = device.Name;
        IsDefault = device.IsDefault;
    }

    public string Id { get; }
    public string Name { get; }

    private bool _isDefault;
    public bool IsDefault
    {
        get => _isDefault;
        private set => SetProperty(ref _isDefault, value);
    }

    public string Display => IsDefault ? Name + " ✓" : Name;

    public override string ToString() => Name;
}

public sealed class SoundViewModel : ObservableObject
{
    private readonly AudioService _audio;
    private readonly AppSettings _settings;
    private bool _applying;
    private DateTime _masterTouched = DateTime.MinValue;

    public SoundViewModel(AudioService audio, AppSettings settings)
    {
        _audio = audio;
        _settings = settings;
    }

    public ObservableCollection<AudioAppViewModel> Apps { get; } = new();
    public ObservableCollection<AudioDeviceViewModel> Devices { get; } = new();

    private AudioDeviceViewModel? _selectedDevice;
    public AudioDeviceViewModel? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (!SetProperty(ref _selectedDevice, value)) return;
            if (_applying || value is null) return;
            _audio.SetDefaultDevice(value.Id);
            RefreshDevices();
        }
    }

    public void RefreshDevices()
    {
        var previous = _selectedDevice;
        var devices = _audio.GetRenderDevices();

        _applying = true;
        try
        {
            Devices.Clear();
            foreach (var device in devices) Devices.Add(new AudioDeviceViewModel(device));

            var current = Devices.FirstOrDefault(d => d.IsDefault) ?? Devices.FirstOrDefault();
            _selectedDevice = current;
            OnPropertyChanged(nameof(SelectedDevice));
        }
        finally
        {
            _applying = false;
        }
    }

    public bool HasApps => Apps.Count > 0;

    public string AppCountText => L.AppsCount(Apps.Count);

    private double _masterVolume = 50;
    public double MasterVolume
    {
        get => _masterVolume;
        set
        {
            var clamped = Math.Clamp(value, 0, 100);
            if (Math.Abs(_masterVolume - clamped) < 0.01) return;
            _masterVolume = clamped;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MasterText));
            if (_applying) return;
            _masterTouched = DateTime.UtcNow;
            _audio.SetMasterVolume((float)(clamped / 100.0));
        }
    }

    public string MasterText => $"{(int)Math.Round(_masterVolume)}%";

    private bool _masterMuted;
    public bool MasterMuted
    {
        get => _masterMuted;
        set
        {
            if (_masterMuted == value) return;
            _masterMuted = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MasterGlyph));
            if (_applying) return;
            _masterTouched = DateTime.UtcNow;
            _audio.SetMasterMute(value);
        }
    }

    public string MasterGlyph => _masterMuted ? "\uE74F" : "\uE767";

    public void SavePreset(string key)
    {
        var preset = _audio.CapturePreset(key);
        _settings.VolumePresets.RemoveAll(p => p.Name == key);
        _settings.VolumePresets.Add(preset);
        SettingsStore.Save(_settings);
    }

    public bool ApplyPreset(string key)
    {
        var preset = _settings.VolumePresets.FirstOrDefault(p => p.Name == key);
        if (preset is null) return false;

        _audio.ApplyPreset(preset);
        _masterTouched = DateTime.MinValue;
        return true;
    }

    public void RefreshLanguage()
    {
        OnPropertyChanged(nameof(MasterText));
        OnPropertyChanged(nameof(AppCountText));
        foreach (var app in Apps) app.RefreshLanguage();
    }

    public void Apply(AudioSnapshot snapshot)
    {
        _applying = true;
        try
        {
            if (DateTime.UtcNow - _masterTouched > TimeSpan.FromSeconds(1.5))
            {
                MasterVolume = snapshot.MasterVolume * 100;
                MasterMuted = snapshot.MasterMuted;
            }
        }
        finally
        {
            _applying = false;
        }

        SyncApps(snapshot.Apps);
    }

    private void SyncApps(IReadOnlyList<AudioAppSnapshot> snapshots)
    {
        var byKey = Apps.ToDictionary(a => a.Key, StringComparer.Ordinal);
        var alive = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < snapshots.Count; i++)
        {
            var snapshot = snapshots[i];
            alive.Add(snapshot.Key);

            if (byKey.TryGetValue(snapshot.Key, out var existing))
            {
                existing.Apply(snapshot);
                var current = Apps.IndexOf(existing);
                if (current >= 0 && current != i && i < Apps.Count) Apps.Move(current, i);
            }
            else
            {
                Apps.Insert(Math.Min(i, Apps.Count), new AudioAppViewModel(_audio, snapshot));
            }
        }

        for (var i = Apps.Count - 1; i >= 0; i--)
        {
            if (!alive.Contains(Apps[i].Key)) Apps.RemoveAt(i);
        }

        OnPropertyChanged(nameof(HasApps));
        OnPropertyChanged(nameof(AppCountText));
    }
}