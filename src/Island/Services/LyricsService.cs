using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Island.Services;

public sealed class LyricLine
{
    public double Time { get; init; }
    public string Text { get; init; } = "";
}

public sealed class LyricsResult
{
    public bool Found { get; init; }
    public bool Synced { get; init; }
    public IReadOnlyList<LyricLine> Lines { get; init; } = Array.Empty<LyricLine>();
}

/// <summary>Lyrics from LRCLIB (free, no key). Returns "not found" instead of inventing text.</summary>
public sealed class LyricsService
{
    private static readonly HttpClient Http = Create();

    private static HttpClient Create()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Island/1.0 (Windows widget)");
        return client;
    }

    public async Task<LyricsResult> GetAsync(string artist, string title, double duration, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(title)) return new LyricsResult();

        try
        {
            var url = "https://lrclib.net/api/get?artist_name=" + Uri.EscapeDataString(artist ?? "") +
                      "&track_name=" + Uri.EscapeDataString(title) +
                      (duration >= 1 ? "&duration=" + ((int)Math.Round(duration)).ToString(CultureInfo.InvariantCulture) : "");

            using var response = await Http.GetAsync(url, ct);
            if (response.StatusCode == HttpStatusCode.NotFound) return new LyricsResult();

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(ct);
            using var document = JsonDocument.Parse(json);

            var synced = document.RootElement.TryGetProperty("syncedLyrics", out var syncedElement)
                ? syncedElement.GetString()
                : null;
            var plain = document.RootElement.TryGetProperty("plainLyrics", out var plainElement)
                ? plainElement.GetString()
                : null;

            if (!string.IsNullOrWhiteSpace(synced))
            {
                var lines = ParseLrc(synced);
                if (lines.Count > 0) return new LyricsResult { Found = true, Synced = true, Lines = lines };
            }

            if (!string.IsNullOrWhiteSpace(plain))
            {
                var lines = plain.Replace("\r\n", "\n").Split('\n')
                    .Select((text, index) => new LyricLine { Time = -1, Text = text.Trim() })
                    .Where(line => line.Text.Length > 0)
                    .ToList();
                if (lines.Count > 0) return new LyricsResult { Found = true, Synced = false, Lines = lines };
            }

            return new LyricsResult();
        }
        catch (OperationCanceledException)
        {
            return new LyricsResult();
        }
        catch
        {
            return new LyricsResult();
        }
    }

    public static List<LyricLine> ParseLrc(string text)
    {
        var lines = new List<LyricLine>();
        var pattern = new Regex(@"\[(\d{1,2}):(\d{2})(?:\.(\d{1,3}))?\]");

        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var matches = pattern.Matches(raw);
            if (matches.Count == 0) continue;

            var content = pattern.Replace(raw, "").Trim();
            if (content.Length == 0) continue;

            foreach (Match match in matches)
            {
                var minutes = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                var seconds = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                var fraction = match.Groups[3].Success
                    ? double.Parse("0." + match.Groups[3].Value, CultureInfo.InvariantCulture)
                    : 0;

                lines.Add(new LyricLine
                {
                    Time = minutes * 60 + seconds + fraction,
                    Text = content
                });
            }
        }

        return lines.OrderBy(line => line.Time).ToList();
    }
}
