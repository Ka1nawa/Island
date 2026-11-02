using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;
using Island.Core;
using Island.Services;

namespace Island.ViewModels;

public sealed class MediaViewModel : ObservableObject
{
    private readonly MediaService _media;
    private readonly LyricsService _lyrics;
    private readonly DispatcherTimer _seekTimer;
    private IReadOnlyList<LyricLine> _lyricLines = Array.Empty<LyricLine>();
    private string _lyricsKey = "";
    private int _lyricIndex = -1;

    private double _position;
    private bool _suppress;
    private DateTime _seekGuard = DateTime.MinValue;
    private double _pendingSeek;

    public MediaViewModel(MediaService media, LyricsService lyrics)
    {
        _media = media;
        _lyrics = lyrics;
        _seekTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(280) };
        _seekTimer.Tick += async (_, _) =>
        {
            _seekTimer.Stop();
            await _media.SeekAsync(_pendingSeek);
        };
    }

    public bool HasMedia { get; private set; }
    public bool HasArtwork => Artwork is not null;
    public bool IsPlaying { get; private set; }
    public string Title { get; private set; } = "";
    public string Artist { get; private set; } = "";
    public string AppName { get; private set; } = "";
    public ImageSource? Artwork { get; private set; }
    public Color AccentColor { get; private set; } = Colors.Transparent;
    public double Duration { get; private set; }

    public string DurationText => FormatTime(Duration);
    public string PositionText => FormatTime(Position);
    public bool HasDuration => Duration >= 1;
    public bool HasAppName => !string.IsNullOrWhiteSpace(AppName);

    private bool _lyricsMode;
    public bool IsLyricsMode
    {
        get => _lyricsMode;
        set
        {
            if (!SetProperty(ref _lyricsMode, value)) return;
            if (value) _ = LoadLyricsAsync();
            OnPropertyChanged(nameof(ShowVisualizer));
        }
    }

    private bool _visualizerEnabled = true;
    public bool VisualizerEnabled
    {
        get => _visualizerEnabled;
        set
        {
            if (!SetProperty(ref _visualizerEnabled, value)) return;
            OnPropertyChanged(nameof(ShowVisualizer));
        }
    }

    public bool ShowVisualizer => !IsLyricsMode && VisualizerEnabled;

    private string _currentLyric = "";
    public string CurrentLyricText
    {
        get => _currentLyric;
        private set => SetProperty(ref _currentLyric, value);
    }

    private string _nextLyric = "";
    public string NextLyricText
    {
        get => _nextLyric;
        private set => SetProperty(ref _nextLyric, value);
    }

    private string _lyricsStatus = "";
    public string LyricsStatusText
    {
        get => _lyricsStatus;
        private set => SetProperty(ref _lyricsStatus, value);
    }

    public double Position
    {
        get => _position;
        set
        {
            if (Math.Abs(_position - value) < 0.01) return;
            _position = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PositionText));

            if (!_suppress) ScheduleSeek(value);
        }
    }

    public void Update()
    {
        var media = _media;

        if (HasMedia != media.HasSession)
        {
            HasMedia = media.HasSession;
            OnPropertyChanged(nameof(HasMedia));
        }

        if (IsPlaying != media.IsPlaying)
        {
            IsPlaying = media.IsPlaying;
            OnPropertyChanged(nameof(IsPlaying));
        }

        if (Title != media.Title)
        {
            Title = media.Title;
            OnPropertyChanged(nameof(Title));
            InvalidateLyrics();
        }

        if (Artist != media.Artist)
        {
            Artist = media.Artist;
            OnPropertyChanged(nameof(Artist));
            InvalidateLyrics();
        }

        if (AppName != media.AppName)
        {
            AppName = media.AppName;
            OnPropertyChanged(nameof(AppName));
            OnPropertyChanged(nameof(HasAppName));
        }

        if (!ReferenceEquals(Artwork, media.Artwork))
        {
            Artwork = media.Artwork;
            OnPropertyChanged(nameof(Artwork));
            OnPropertyChanged(nameof(HasArtwork));
            ComputeAccent();
        }

        if (Math.Abs(Duration - media.Duration) > 0.25)
        {
            Duration = media.Duration;
            OnPropertyChanged(nameof(Duration));
            OnPropertyChanged(nameof(DurationText));
            OnPropertyChanged(nameof(HasDuration));
        }

        UpdateLyricsPosition(Position);

        if (!HasMedia)
        {
            if (_position == 0) return;
            _suppress = true;
            Position = 0;
            _suppress = false;
            return;
        }

        if (DateTime.UtcNow > _seekGuard)
        {
            _suppress = true;
            Position = media.CurrentPosition;
            _suppress = false;
        }
    }

    private void ComputeAccent()
    {
        AccentColor = ComputeAccentColor(Artwork);
        OnPropertyChanged(nameof(AccentColor));
    }

    public static Color ComputeAccentColor(ImageSource? image)
    {
        var color = Colors.Transparent;

        if (image is System.Windows.Media.Imaging.BitmapSource bitmap && bitmap.PixelWidth > 0 && bitmap.PixelHeight > 0)
        {
            try
            {
                const int size = 24;
                var scale = new System.Windows.Media.ScaleTransform(
                    size / (double)bitmap.PixelWidth, size / (double)bitmap.PixelHeight);
                var scaled = new System.Windows.Media.Imaging.TransformedBitmap(bitmap, scale);
                var converted = new System.Windows.Media.Imaging.FormatConvertedBitmap(
                    scaled, System.Windows.Media.PixelFormats.Bgra32, null, 0);

                var stride = size * 4;
                var pixels = new byte[stride * size];
                converted.CopyPixels(pixels, stride, 0);

                double r = 0, g = 0, b = 0, weight = 0;
                for (var i = 0; i < pixels.Length; i += 4)
                {
                    var blue = pixels[i];
                    var green = pixels[i + 1];
                    var red = pixels[i + 2];
                    var alpha = pixels[i + 3];
                    if (alpha < 40) continue;

                    var max = Math.Max(red, Math.Max(green, blue));
                    var min = Math.Min(red, Math.Min(green, blue));
                    var saturation = max == 0 ? 0 : (max - min) / (double)max;
                    var w = 0.2 + saturation;

                    r += red * w;
                    g += green * w;
                    b += blue * w;
                    weight += w;
                }

                if (weight > 1)
                    color = Color.FromRgb((byte)(r / weight), (byte)(g / weight), (byte)(b / weight));
            }
            catch
            {
                color = Colors.Transparent;
            }
        }

        return color;
    }

    private void InvalidateLyrics()
    {
        _lyricsKey = "";
        _lyricLines = Array.Empty<LyricLine>();
        _lyricIndex = -1;
        CurrentLyricText = "";
        NextLyricText = "";
        LyricsStatusText = IsLyricsMode ? L.T("LyricsLoading") : "";
        if (IsLyricsMode) _ = LoadLyricsAsync();
    }

    public async Task LoadLyricsAsync()
    {
        var key = Title + "|" + Artist + "|" + (int)Math.Round(Duration);
        if (key == _lyricsKey && _lyricLines.Count > 0) return;
        if (string.IsNullOrWhiteSpace(Title)) return;

        _lyricsKey = key;
        LyricsStatusText = L.T("LyricsLoading");
        CurrentLyricText = "";
        NextLyricText = "";

        var result = await _lyrics.GetAsync(Artist, Title, Duration, default);
        if (_lyricsKey != key) return;

        _lyricLines = result.Lines;
        _lyricIndex = -1;

        if (_lyricLines.Count == 0)
        {
            LyricsStatusText = L.T("LyricsNone");
            return;
        }

        LyricsStatusText = result.Synced ? "" : L.T("LyricsPlain");
        UpdateLyricsPosition(Position);
    }

    private void UpdateLyricsPosition(double seconds)
    {
        if (_lyricLines.Count == 0) return;

        if (_lyricLines.Count > 0 && _lyricLines[0].Time < 0)
        {
            if (CurrentLyricText.Length == 0)
            {
                CurrentLyricText = _lyricLines[0].Text;
                NextLyricText = _lyricLines.Count > 1 ? _lyricLines[1].Text : "";
            }
            return;
        }

        var index = -1;
        for (var i = 0; i < _lyricLines.Count; i++)
        {
            if (_lyricLines[i].Time <= seconds + 0.2) index = i;
            else break;
        }

        if (index == _lyricIndex) return;
        _lyricIndex = index;

        CurrentLyricText = index >= 0 ? _lyricLines[index].Text : "";
        NextLyricText = index + 1 < _lyricLines.Count ? _lyricLines[index + 1].Text : "";
    }

    private void ScheduleSeek(double value)
    {
        _pendingSeek = value;
        _seekGuard = DateTime.UtcNow.AddSeconds(1.4);
        _seekTimer.Stop();
        _seekTimer.Start();
    }

    public async Task TogglePlayPauseAsync() => await _media.TogglePlayPauseAsync();
    public async Task NextAsync() => await _media.NextAsync();
    public async Task PreviousAsync() => await _media.PreviousAsync();

    private static string FormatTime(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) seconds = 0;
        var span = TimeSpan.FromSeconds(seconds);
        return span.TotalHours >= 1
            ? $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}"
            : $"{span.Minutes}:{span.Seconds:00}";
    }
}