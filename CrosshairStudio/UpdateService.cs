using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace CrosshairStudio;

public sealed class UpdateCheckResult
{
    public bool UpdateAvailable { get; init; }
    public string Version { get; init; } = "";
    public string? AssetName { get; init; }
    public string? DownloadUrl { get; init; }
    public string? ChecksumUrl { get; init; }
    public string? Error { get; init; }
}

public static class UpdateService
{
    private const string Owner = "romanpuhach11-ai";
    private const string Repo = "NexCross";

    public static async Task<UpdateCheckResult> CheckAsync()
    {
        try
        {
            using var client = CreateClient();
            using var response = await client.GetAsync(
                $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest");
            if (!response.IsSuccessStatusCode)
            {
                return new UpdateCheckResult
                {
                    Error = $"GitHub Releases недоступний: {(int)response.StatusCode}."
                };
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            var release = await System.Text.Json.JsonSerializer.DeserializeAsync<GitHubRelease>(stream);
            if (release is null || string.IsNullOrWhiteSpace(release.TagName))
                return new UpdateCheckResult { Error = "Реліз не містить номеру версії." };

            var latest = NormalizeVersion(release.TagName);
            var current = NormalizeVersion(
                typeof(UpdateService).Assembly.GetName().Version?.ToString() ?? "1.0.0");
            if (latest <= current)
                return new UpdateCheckResult { UpdateAvailable = false, Version = release.TagName };

            var asset = release.Assets.FirstOrDefault(a =>
                a.Name?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true);
            if (asset is null)
                return new UpdateCheckResult { Error = "У релізі немає portable ZIP-архіву." };

            var checksum = release.Assets.FirstOrDefault(a =>
                string.Equals(a.Name, "checksums.txt", StringComparison.OrdinalIgnoreCase));

            return new UpdateCheckResult
            {
                UpdateAvailable = true,
                Version = release.TagName,
                AssetName = asset.Name,
                DownloadUrl = asset.BrowserDownloadUrl,
                ChecksumUrl = checksum?.BrowserDownloadUrl
            };
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult { Error = $"Не вдалося перевірити оновлення: {ex.Message}" };
        }
    }

    public static async Task<string?> DownloadAsync(UpdateCheckResult update)
    {
        if (string.IsNullOrWhiteSpace(update.DownloadUrl) ||
            string.IsNullOrWhiteSpace(update.AssetName))
            throw new InvalidOperationException("Не вказано файл оновлення.");

        using var client = CreateClient();
        var bytes = await client.GetByteArrayAsync(update.DownloadUrl);
        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "CrosshairStudio-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var zipPath = Path.Combine(tempDirectory, update.AssetName);
        await File.WriteAllBytesAsync(zipPath, bytes);

        if (!string.IsNullOrWhiteSpace(update.ChecksumUrl))
        {
            var checksumText = await client.GetStringAsync(update.ChecksumUrl);
            if (!VerifyChecksum(zipPath, update.AssetName, checksumText))
                throw new InvalidOperationException(
                    "SHA-256 завантаженого архіву не відповідає файлу checksums.txt.");
        }

        return zipPath;
    }

    public static void ApplyPortableUpdate(string zipPath)
    {
        var targetDirectory = AppContext.BaseDirectory;
        var executable = Path.Combine(targetDirectory, "CrosshairStudio.exe");
        var processId = Environment.ProcessId;
        var helperPath = Path.Combine(
            Path.GetTempPath(),
            "CrosshairStudio-update-" + Guid.NewGuid().ToString("N") + ".ps1");

        File.WriteAllText(helperPath, """
$ErrorActionPreference = 'Stop'
$zip = $args[0]
$target = $args[1]
$exe = $args[2]
$processId = [int]$args[3]
$payload = Join-Path ([System.IO.Path]::GetTempPath()) ('CrosshairStudio-payload-' + [guid]::NewGuid())
New-Item -ItemType Directory -Force -Path $payload | Out-Null
try {
    if ($processId -gt 0) {
        $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
        if ($process) { $process.WaitForExit(15000) }
    }
    Expand-Archive -LiteralPath $zip -DestinationPath $payload -Force
    Copy-Item -LiteralPath (Join-Path $payload '*') -Destination $target -Recurse -Force
    Start-Process -FilePath $exe -WorkingDirectory $target
}
finally {
    Remove-Item -LiteralPath $payload -Recurse -Force -ErrorAction SilentlyContinue
}
""");

        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            ArgumentList =
            {
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                helperPath,
                zipPath,
                targetDirectory,
                executable,
                processId.ToString()
            },
            UseShellExecute = false,
            CreateNoWindow = true
        });
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("CrosshairStudio-Updater");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    private static Version NormalizeVersion(string value)
    {
        var clean = value.Trim().TrimStart('v', 'V');
        return Version.TryParse(clean, out var version) ? version : new Version(0, 0, 0);
    }

    private static bool VerifyChecksum(
        string filePath,
        string assetName,
        string checksumText)
    {
        foreach (var line in checksumText.Split(
                     '\n',
                     StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.Trim().Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 2)
                continue;
            if (!string.Equals(fields[^1].TrimStart('*'), assetName,
                    StringComparison.OrdinalIgnoreCase))
                continue;

            using var sha = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var actual = Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
            return string.Equals(fields[0], actual, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("assets")]
        public GitHubAsset[] Assets { get; set; } = Array.Empty<GitHubAsset>();
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
    }
}
