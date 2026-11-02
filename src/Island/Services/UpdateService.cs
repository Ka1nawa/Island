using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Island.Services;

public sealed class UpdateAsset
{
    public string Name { get; init; } = "";
    public string Url { get; init; } = "";
    public long Size { get; init; }
}

public sealed class UpdateInfo
{
    public bool Ok { get; init; }
    public bool Newer { get; init; }
    public string Tag { get; init; } = "";
    public string PageUrl { get; init; } = "";
    public string Notes { get; init; } = "";
    public string Sha256 { get; init; } = "";
    public IReadOnlyList<UpdateAsset> Assets { get; init; } = Array.Empty<UpdateAsset>();
    public string Error { get; init; } = "";
}

/// <summary>Checks GitHub releases and downloads an asset only with a published SHA-256.</summary>
public static class UpdateService
{
    public static async Task<UpdateInfo> CheckAsync(string repo, Version current)
    {
        if (string.IsNullOrWhiteSpace(repo)) return new UpdateInfo { Error = "not-configured" };

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Island/" + current);

            var json = await http.GetStringAsync($"https://api.github.com/repos/{repo.Trim()}/releases/latest");
            using var document = JsonDocument.Parse(json);

            var root = document.RootElement;
            var tag = root.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() ?? "" : "";
            var url = root.TryGetProperty("html_url", out var urlElement) ? urlElement.GetString() ?? "" : "";
            var notes = root.TryGetProperty("body", out var bodyElement) ? bodyElement.GetString() ?? "" : "";

            var assets = new List<UpdateAsset>();
            if (root.TryGetProperty("assets", out var assetsElement) && assetsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assetsElement.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? "" : "";
                    var download = asset.TryGetProperty("browser_download_url", out var downloadElement)
                        ? downloadElement.GetString() ?? ""
                        : "";
                    var size = asset.TryGetProperty("size", out var sizeElement) ? sizeElement.GetInt64() : 0;
                    if (name.Length > 0 && download.Length > 0)
                        assets.Add(new UpdateAsset { Name = name, Url = download, Size = size });
                }
            }

            var newer = Version.TryParse(tag.TrimStart('v', 'V'), out var latest) && latest > current;

            return new UpdateInfo
            {
                Ok = true,
                Newer = newer,
                Tag = tag,
                PageUrl = url,
                Notes = notes,
                Sha256 = ParseSha256(notes),
                Assets = assets
            };
        }
        catch (Exception exception)
        {
            return new UpdateInfo { Error = exception.Message };
        }
    }

    /// <summary>Looks for "sha256: &lt;64 hex&gt;" in the release notes.</summary>
    public static string ParseSha256(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return "";
        var match = Regex.Match(notes, @"sha256[:\s=]+([0-9a-fA-F]{64})", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.ToLowerInvariant() : "";
    }

    public static UpdateAsset? PickAsset(UpdateInfo info)
    {
        static bool Safe(UpdateAsset asset) =>
            Regex.IsMatch(asset.Name, @"^[A-Za-z0-9 ._\-]+\.(exe|msi|zip)$", RegexOptions.IgnoreCase);

        return info.Assets.FirstOrDefault(a => Safe(a) && a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
               ?? info.Assets.FirstOrDefault(a => Safe(a) && a.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
               ?? info.Assets.FirstOrDefault(a => Safe(a) && a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
    }

    public static async Task<(bool Ok, string Path, string Error)> DownloadAsync(
        UpdateAsset asset, string destination, string expectedSha, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(expectedSha)) return (false, "", "no-hash");

        destination = ResolveDownloadPath(destination, asset.Name);
        if (destination.Length == 0) return (false, "", "bad-name");

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Island/updater");

            using var response = await http.GetAsync(asset.Url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            await using (var target = File.Create(destination))
            {
                await response.Content.CopyToAsync(target, ct);
            }

            var actual = ComputeSha256(destination);
            if (!string.Equals(actual, expectedSha, StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(destination); } catch { }
                return (false, "", "hash-mismatch");
            }

            return (true, destination, "");
        }
        catch (Exception exception)
        {
            return (false, "", exception.Message);
        }
    }

    /// <summary>Keeps downloads inside the intended directory, whatever the asset name contains.</summary>
    public static string ResolveDownloadPath(string destination, string? assetName)
    {
        var safeName = Path.GetFileName(assetName ?? "");
        if (string.IsNullOrWhiteSpace(safeName) || safeName is "." or "..") return "";

        var directory = Path.GetDirectoryName(destination);
        if (string.IsNullOrWhiteSpace(directory)) directory = Path.GetTempPath();

        return Path.Combine(directory, safeName);
    }

    public static string ComputeSha256(string path)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
    }
}
