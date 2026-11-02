using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace Island.Services;

public sealed class AudioAppSnapshot
{
    public string Key { get; init; } = "";
    public string Name { get; init; } = "";
    public string ExecutablePath { get; init; } = "";
    public float Volume { get; init; }
    public bool Muted { get; init; }
    public bool IsSystem { get; init; }
    public bool IsActive { get; init; }
    public ImageSource? Icon { get; init; }
}

public sealed class AudioSnapshot
{
    public float MasterVolume { get; init; }
    public bool MasterMuted { get; init; }
    public IReadOnlyList<AudioAppSnapshot> Apps { get; init; } = Array.Empty<AudioAppSnapshot>();
}

public sealed class AudioDevice
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public bool IsDefault { get; init; }
}

public sealed class AudioService : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();
    private MMDevice? _device;
    private string? _deviceId;

    private static readonly Dictionary<string, ImageSource?> IconCache = new(StringComparer.OrdinalIgnoreCase);

    public AudioSnapshot Snapshot()
    {
        var apps = new List<AudioAppSnapshot>();
        float master = 0.5f;
        var muted = false;

        try
        {
            var device = EnsureDevice();
            if (device is null) return new AudioSnapshot { MasterVolume = master, MasterMuted = muted };

            try
            {
                master = device.AudioEndpointVolume.MasterVolumeLevelScalar;
                muted = device.AudioEndpointVolume.Mute;
            }
            catch { }

            var manager = device.AudioSessionManager;
            manager.RefreshSessions();
            var sessions = manager.Sessions;

            var byPid = new Dictionary<uint, AudioAppSnapshot>();

            for (var i = 0; i < sessions.Count; i++)
            {
                AudioSessionControl? session = null;
                try
                {
                    session = sessions[i];
                    if (session is null) continue;
                    if (session.State == AudioSessionState.AudioSessionStateExpired) continue;

                    var system = session.IsSystemSoundsSession;
                    var pid = system ? 0u : session.GetProcessID;
                    if (!system && pid == 0) continue;

                    var key = system ? "system-sounds" : "pid:" + pid;
                    if (!system && byPid.ContainsKey(pid)) continue;

                    var active = session.State == AudioSessionState.AudioSessionStateActive;
                    var displayName = "";
                    try { displayName = session.DisplayName ?? ""; } catch { }

                    var processPath = system ? "" : TryGetProcessPath(pid);
                    var name = ResolveName(system, displayName, processPath, pid);
                    var icon = system ? null : TryGetIcon(processPath);

                    var volume = 1f;
                    var isMuted = false;
                    try
                    {
                        if (session.SimpleAudioVolume is not null)
                        {
                            volume = session.SimpleAudioVolume.Volume;
                            isMuted = session.SimpleAudioVolume.Mute;
                        }
                    }
                    catch { }

                    var app = new AudioAppSnapshot
                    {
                        Key = key,
                        Name = name,
                        ExecutablePath = processPath,
                        Volume = volume,
                        Muted = isMuted,
                        IsSystem = system,
                        IsActive = active,
                        Icon = icon
                    };

                    if (system) apps.Add(app);
                    else byPid[pid] = app;
                }
                catch
                {
                    // session disappeared mid-enumeration - ignore it
                }
            }

            apps.AddRange(byPid.Values);
        }
        catch
        {
            // no audio endpoints at all
        }

        var ordered = apps
            .OrderByDescending(a => a.IsActive)
            .ThenBy(a => a.IsSystem ? 0 : 1)
            .ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new AudioSnapshot { MasterVolume = master, MasterMuted = muted, Apps = ordered };
    }

    [ComImport, Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
    private class PolicyConfigClient
    {
    }

    [ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat(string deviceName, IntPtr format);
        [PreserveSig] int GetDeviceFormat(string deviceName, bool isDefault, IntPtr format);
        [PreserveSig] int ResetDeviceFormat(string deviceName);
        [PreserveSig] int SetDeviceFormat(string deviceName, IntPtr endpointFormat, IntPtr mixFormat);
        [PreserveSig] int GetProcessingPeriod(string deviceName, bool isDefault, IntPtr defaultPeriod, IntPtr minimumPeriod);
        [PreserveSig] int SetProcessingPeriod(string deviceName, IntPtr period);
        [PreserveSig] int GetShareMode(string deviceName, IntPtr mode);
        [PreserveSig] int SetShareMode(string deviceName, IntPtr mode);
        [PreserveSig] int GetPropertyValue(string deviceName, bool fxStore, IntPtr key, IntPtr value);
        [PreserveSig] int SetPropertyValue(string deviceName, bool fxStore, IntPtr key, IntPtr value);
        [PreserveSig] int SetDefaultEndpoint(string deviceName, int role);
        [PreserveSig] int SetEndpointVisibility(string deviceName, bool visible);
    }

    public IReadOnlyList<AudioDevice> GetRenderDevices()
    {
        var result = new List<AudioDevice>();

        try
        {
            var defaultId = "";
            try
            {
                using var current = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                defaultId = current.ID;
            }
            catch
            {
            }

            foreach (var device in _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                try
                {
                    result.Add(new AudioDevice
                    {
                        Id = device.ID,
                        Name = string.IsNullOrWhiteSpace(device.FriendlyName) ? device.DeviceFriendlyName : device.FriendlyName,
                        IsDefault = device.ID == defaultId
                    });
                }
                finally
                {
                    device.Dispose();
                }
            }
        }
        catch
        {
        }

        return result;
    }

    public bool SetDefaultDevice(string deviceId)
    {
        try
        {
            var policy = (IPolicyConfig)new PolicyConfigClient();
            var ok = policy.SetDefaultEndpoint(deviceId, 0) == 0;
            policy.SetDefaultEndpoint(deviceId, 1);
            policy.SetDefaultEndpoint(deviceId, 2);

            _device?.Dispose();
            _device = null;
            _deviceId = null;
            return ok;
        }
        catch
        {
            return false;
        }
    }

    public VolumePreset CapturePreset(string name)
    {
        var snapshot = Snapshot();
        return new VolumePreset
        {
            Name = name,
            Master = snapshot.MasterVolume,
            MasterMuted = snapshot.MasterMuted,
            Apps = snapshot.Apps.Select(app => new VolumePresetApp
            {
                Name = app.Name,
                Volume = app.Volume,
                Muted = app.Muted
            }).ToList()
        };
    }

    public void ApplyPreset(VolumePreset preset)
    {
        SetMasterVolume(preset.Master);
        SetMasterMute(preset.MasterMuted);

        var snapshot = Snapshot();
        foreach (var saved in preset.Apps)
        {
            var match = snapshot.Apps.FirstOrDefault(app =>
                string.Equals(app.Name, saved.Name, StringComparison.OrdinalIgnoreCase));
            if (match is null) continue;

            SetAppVolume(match.Key, saved.Volume);
            SetAppMute(match.Key, saved.Muted);
        }
    }

    public void SetMasterVolume(float value)
    {
        try
        {
            var device = EnsureDevice();
            if (device is null) return;
            device.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(value, 0f, 1f);
        }
        catch { }
    }

    public void SetMasterMute(bool muted)
    {
        try
        {
            var device = EnsureDevice();
            if (device is null) return;
            device.AudioEndpointVolume.Mute = muted;
        }
        catch { }
    }

    public void SetAppVolume(string key, float value)
    {
        try
        {
            var device = EnsureDevice();
            if (device is null) return;
            var manager = device.AudioSessionManager;
            manager.RefreshSessions();
            var sessions = manager.Sessions;

            for (var i = 0; i < sessions.Count; i++)
            {
                try
                {
                    var session = sessions[i];
                    if (session is null) continue;
                    if (SessionKey(session) != key) continue;
                    if (session.SimpleAudioVolume is null) continue;
                    session.SimpleAudioVolume.Volume = Math.Clamp(value, 0f, 1f);
                    return;
                }
                catch { }
            }
        }
        catch { }
    }

    public void SetAppMute(string key, bool muted)
    {
        try
        {
            var device = EnsureDevice();
            if (device is null) return;
            var manager = device.AudioSessionManager;
            manager.RefreshSessions();
            var sessions = manager.Sessions;

            for (var i = 0; i < sessions.Count; i++)
            {
                try
                {
                    var session = sessions[i];
                    if (session is null) continue;
                    if (SessionKey(session) != key) continue;
                    if (session.SimpleAudioVolume is null) continue;
                    session.SimpleAudioVolume.Mute = muted;
                    return;
                }
                catch { }
            }
        }
        catch { }
    }

    private static string SessionKey(AudioSessionControl session)
    {
        try
        {
            if (session.IsSystemSoundsSession) return "system-sounds";
            var pid = session.GetProcessID;
            return pid == 0 ? "unknown" : "pid:" + pid;
        }
        catch
        {
            return "unknown";
        }
    }

    private MMDevice? EnsureDevice()
    {
        try
        {
            var current = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            if (_device is null || _deviceId != current.ID)
            {
                _device?.Dispose();
                _device = current;
                _deviceId = current.ID;
            }
            else
            {
                current.Dispose();
            }

            return _device;
        }
        catch
        {
            return null;
        }
    }

    private static string TryGetProcessPath(uint pid)
    {
        try
        {
            using var process = Process.GetProcessById((int)pid);
            return process.MainModule?.FileName ?? "";
        }
        catch
        {
            return "";
        }
    }

    private static string ResolveName(bool system, string displayName, string processPath, uint pid)
    {
        if (system) return L.T("SystemSounds");

        var fileName = string.IsNullOrEmpty(processPath) ? "" : Path.GetFileNameWithoutExtension(processPath);

        // Prefer the name the app publishes for itself, unless it is just "chrome.exe".
        if (!string.IsNullOrWhiteSpace(displayName) &&
            !displayName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return displayName.Trim();
        }

        if (!string.IsNullOrEmpty(processPath))
        {
            try
            {
                var description = FileVersionInfo.GetVersionInfo(processPath).FileDescription;
                if (!string.IsNullOrWhiteSpace(description) && description != "Package")
                    return description.Trim();
            }
            catch { }

            if (!string.IsNullOrEmpty(fileName))
                return char.ToUpperInvariant(fileName[0]) + fileName[1..];
        }

        if (!string.IsNullOrWhiteSpace(displayName)) return displayName.Trim();
        return L.T("AppFallback") + " " + pid;
    }

    private static ImageSource? TryGetIcon(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        lock (IconCache)
        {
            if (IconCache.TryGetValue(path, out var cached)) return cached;
        }

        ImageSource? image = null;
        try
        {
            if (File.Exists(path))
            {
                using var icon = Icon.ExtractAssociatedIcon(path);
                if (icon is not null)
                {
                    var source = Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    source.Freeze();
                    image = source;
                }
            }
        }
        catch
        {
            image = null;
        }

        lock (IconCache) IconCache[path] = image;
        return image;
    }

    public void Dispose()
    {
        _device?.Dispose();
        _enumerator.Dispose();
    }
}