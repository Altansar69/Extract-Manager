using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace ExtractManager;

public static partial class SevenZipManager
{
    private static readonly string SSevenZipExePath = Path.Combine(AppContext.BaseDirectory, "7za.exe");
    private static readonly HttpClient SHttpClient = new();

    [GeneratedRegex(@"7z(\d+)-")]
    private static partial Regex SevenZipVersionRegex();

    public static async Task<SevenZipReleaseInfo> GetLatestReleaseInfoAsync()
    {
        // Avoid duplicate UA values
        if (SHttpClient.DefaultRequestHeaders.UserAgent.Count == 0)
            SHttpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ExtractManager", "1.0"));

        var response = await SHttpClient.GetStringAsync("https://api.github.com/repos/ip7z/7zip/releases/latest");

        using var jsonDoc = JsonDocument.Parse(response);
        var root = jsonDoc.RootElement;
        var assets = root.GetProperty("assets");

        foreach (var asset in assets.EnumerateArray())
        {
            var fileName = asset.GetProperty("name").GetString();
            if (fileName != null && fileName.EndsWith("-extra.7z"))
            {
                var downloadUrl = asset.GetProperty("browser_download_url").GetString();
                var versionMatch = SevenZipVersionRegex().Match(fileName);
                if (versionMatch.Success)
                {
                    var versionStr = versionMatch.Groups[1].Value;
                    var version = $"{versionStr[..2]}.{versionStr[2..]}";
                    return new SevenZipReleaseInfo(downloadUrl, version);
                }
            }
        }

        throw new Exception("Could not find the '-extra.7z' asset in the latest GitHub release.");
    }

    public static async Task DownloadAndExtractAsync(string downloadUrl)
    {
        var tempArchivePath = Path.GetTempFileName();
        try
        {
            var fileBytes = await SHttpClient.GetByteArrayAsync(downloadUrl);
            await File.WriteAllBytesAsync(tempArchivePath, fileBytes);

            using var archive = ArchiveFactory.Open(tempArchivePath);
            // Collect all 7za.exe candidates (could be at root or under arch folders)
            var candidates = archive.Entries
                .Where(e => e is { IsDirectory: false, Key: not null } &&
                            string.Equals(Path.GetFileName(e.Key), "7za.exe", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (candidates.Count == 0)
                throw new FileNotFoundException("Could not find '7za.exe' inside the downloaded archive.");

            // Normalize path separators
            static string N(string? s)
            {
                return (s ?? string.Empty).Replace('\\', '/');
            }

            static bool HasSeg(string? p, string seg)
            {
                var s = N(p);
                return s.StartsWith(seg + "/", StringComparison.OrdinalIgnoreCase) ||
                       s.Contains("/" + seg + "/", StringComparison.OrdinalIgnoreCase);
            }

            // Choose preferred candidate based on OS architecture
            var arch = RuntimeInformation.OSArchitecture;
            var preferred = arch switch
            {
                Architecture.X64 => candidates.FirstOrDefault(e => HasSeg(e.Key, "x64")),
                Architecture.Arm64 =>
                    candidates.FirstOrDefault(e => HasSeg(e.Key, "arm64") || HasSeg(e.Key, "aarch64")),
                Architecture.X86 => candidates.FirstOrDefault(e =>
                    HasSeg(e.Key, "x86") ||
                    (!HasSeg(e.Key, "x64") && !HasSeg(e.Key, "arm64") && !HasSeg(e.Key, "aarch64"))),
                _ => null
            };

            var exeEntry = preferred ??
                           // If no direct match, prefer x64 as a general default on 64-bit OS
                           (Environment.Is64BitOperatingSystem
                               ? candidates.FirstOrDefault(e => HasSeg(e.Key, "x64"))
                               : null) ??
                           candidates.First();

            exeEntry.WriteToFile(SSevenZipExePath, new ExtractionOptions { Overwrite = true });
            AppLogger.Log($"'7za.exe' extracted from '{exeEntry.Key}' for OS arch {arch}.");
        }
        finally
        {
            if (File.Exists(tempArchivePath)) File.Delete(tempArchivePath);
        }
    }

    public static async Task<bool> EnsureSevenZipIsAvailableAsync()
    {
        try
        {
            if (File.Exists(SSevenZipExePath))
            {
                AppLogger.Log("7-Zip already available.");
                return true;
            }

            AppLogger.Log("7-Zip not found. Downloading latest version...");
            var (downloadUrl, _) = await GetLatestReleaseInfoAsync();
            if (string.IsNullOrEmpty(downloadUrl))
            {
                AppLogger.Log("Could not get 7-Zip download URL.");
                return false;
            }

            await DownloadAndExtractAsync(downloadUrl);
            AppLogger.Log("7-Zip is ready.");
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Log($"Failed to ensure 7-Zip is available: {ex.Message}");
            return false;
        }
    }
}

public record struct SevenZipReleaseInfo(string? DownloadUrl, string Version)
{
    public static implicit operator (string? downloadUrl, string version)(SevenZipReleaseInfo value)
    {
        return (value.DownloadUrl, value.Version);
    }

    public static implicit operator SevenZipReleaseInfo((string? downloadUrl, string version) value)
    {
        return new SevenZipReleaseInfo(value.downloadUrl, value.version);
    }
}