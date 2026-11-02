using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace Island.Services;

public sealed class MediaService
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;

    private string _artKey = "";
    private ImageSource? _art;

    private double _position;
    private DateTime _anchor = DateTime.UtcNow;

    public bool HasSession { get; private set; }
    public bool IsPlaying { get; private set; }
    public string Title { get; private set; } = "";
    public string Artist { get; private set; } = "";
    public string AppName { get; private set; } = "";
    public double Duration { get; private set; }
    public ImageSource? Artwork => _art;

    public double CurrentPosition
    {
        get
        {
            if (!HasSession || !IsPlaying || Duration <= 0) return _position;
            var pos = _position + (DateTime.UtcNow - _anchor).TotalSeconds;
            return Math.Clamp(pos, 0, Duration);
        }
    }

    public async Task<bool> RefreshAsync()
    {
        try
        {
            _manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var session = _manager.GetCurrentSession();

            if (session is null)
            {
                ResetSession();
                return true;
            }

            _session = session;
            var info = session.GetPlaybackInfo();
            var playing = info.PlaybackStatus ==
                          GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

            var props = await session.TryGetMediaPropertiesAsync();

            var title = props?.Title?.Trim() ?? "";
            var artist = props?.Artist?.Trim() ?? "";
            var app = FriendlyAppName(session.SourceAppUserModelId);

            if (title.Length == 0 && artist.Length == 0)
            {
                // Some apps expose a session before metadata is ready.
                HasSession = false;
                IsPlaying = playing;
                Title = "";
                Artist = "";
                AppName = app;
                return false;
            }

            Title = title;
            Artist = artist;
            AppName = app;
            HasSession = true;
            IsPlaying = playing;

            var timeline = session.GetTimelineProperties();
            Duration = Math.Max(0, (timeline.EndTime - timeline.StartTime).TotalSeconds);
            _position = Math.Max(0, (timeline.Position - timeline.StartTime).TotalSeconds);
            _anchor = DateTime.UtcNow;

            var key = title + "\u0001" + artist;
            if (key != _artKey)
            {
                _artKey = key;
                _art = await LoadArtAsync(props?.Thumbnail);
            }

            return true;
        }
        catch
        {
            ResetSession();
            return false;
        }
    }

    public void Tick()
    {
        if (HasSession && Duration > 0)
        {
            var pos = CurrentPosition;
            if (IsPlaying) _position = pos;
        }
    }

    public async Task TogglePlayPauseAsync()
    {
        try
        {
            if (_session is not null) await _session.TryTogglePlayPauseAsync();
        }
        catch { }
    }

    public async Task NextAsync()
    {
        try
        {
            if (_session is not null) await _session.TrySkipNextAsync();
        }
        catch { }
    }

    public async Task PreviousAsync()
    {
        try
        {
            if (_session is not null) await _session.TrySkipPreviousAsync();
        }
        catch { }
    }

    public async Task SeekAsync(double seconds)
    {
        try
        {
            if (_session is null) return;
            var target = TimeSpan.FromSeconds(Math.Clamp(seconds, 0, Math.Max(0, Duration)));
            await _session.TryChangePlaybackPositionAsync(target.Ticks);
            _position = target.TotalSeconds;
            _anchor = DateTime.UtcNow;
        }
        catch { }
    }

    private void ResetSession()
    {
        HasSession = false;
        IsPlaying = false;
        Title = "";
        Artist = "";
        AppName = "";
        Duration = 0;
        _position = 0;
        _artKey = "";
        _art = null;
    }

    private static string FriendlyAppName(string? aumid)
    {
        if (string.IsNullOrWhiteSpace(aumid)) return "";
        var value = aumid;

        // Packaged apps look like "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify"
        var bang = value.IndexOf('!');
        if (bang >= 0) value = value[(bang + 1)..];

        var slash = value.LastIndexOfAny(new[] { '\\', '/' });
        if (slash >= 0) value = value[(slash + 1)..];

        if (value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            value = value[..^4];

        if (value.Length == 0) return "";

        // Some apps expose an opaque hash instead of a readable name.
        if (value.Length >= 8 && value.All(Uri.IsHexDigit)) return L.T("MediaSource");

        // "chrome" -> "Chrome", "vlc" -> "VLC" is fine too
        return char.ToUpperInvariant(value[0]) + value[1..];
    }

    private static async Task<ImageSource?> LoadArtAsync(IRandomAccessStreamReference? reference)
    {
        if (reference is null) return null;

        try
        {
            using var stream = await reference.OpenReadAsync();
            if (stream.Size == 0) return null;

            var buffer = new byte[stream.Size];
            using var input = stream.GetInputStreamAt(0);
            using var reader = new DataReader(input);
            await reader.LoadAsync((uint)stream.Size);
            reader.ReadBytes(buffer);

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = new MemoryStream(buffer);
            bitmap.DecodePixelWidth = 256;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }
}