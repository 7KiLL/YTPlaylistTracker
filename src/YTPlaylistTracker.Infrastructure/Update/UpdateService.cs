using System.Formats.Tar;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using YTPlaylistTracker.Domain.Interfaces;
using YTPlaylistTracker.Domain.Models;
using YTPlaylistTracker.Infrastructure.Platform;

namespace YTPlaylistTracker.Infrastructure.Update;

public class UpdateService(IBinaryUpdater binaryUpdater, ILogger<UpdateService> logger) : IUpdateService
{
    private const string GitHubApiUrl = "https://api.github.com/repos/7KiLL/YTPlaylistTracker/releases/latest";
    private const string ReleasePageUrl = "https://github.com/7KiLL/YTPlaylistTracker/releases/latest";

    private static readonly HttpClient Http = new()
    {
        Timeout = Timeout.InfiniteTimeSpan,
        DefaultRequestHeaders =
        {
            { "User-Agent", "ytpt-updater" },
            { "Accept", "application/vnd.github+json" },
        },
    };

    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(5);

    public static string GetCurrentVersion()
    {
        var attr = Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        var full = attr?.InformationalVersion ?? "0.0.0";
        var plusIdx = full.IndexOf('+', StringComparison.Ordinal);
        return plusIdx >= 0 ? full[..plusIdx] : full;
    }

    private static string GetExpectedAssetName()
    {
        var rid = RuntimeInformation.RuntimeIdentifier;
        var extension = OperatingSystem.IsWindows() ? "zip" : "tar.gz";
        return $"ytpt-{rid}.{extension}";
    }

    public async Task<UpdateInfo> CheckForUpdateAsync()
    {
        var currentVersion = GetCurrentVersion();
        logger.LogInformation("[Update] Checking for updates (current: {Version})", currentVersion);

        try
        {
            using var checkCts = new CancellationTokenSource(CheckTimeout);
            var json = await Http.GetStringAsync(GitHubApiUrl, checkCts.Token).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tagName = root.GetProperty("tag_name").GetString() ?? "";
            var latestVersion = tagName.TrimStart('v');

            if (!Version.TryParse(currentVersion, out var current) ||
                !Version.TryParse(latestVersion, out var latest))
            {
                logger.LogWarning("[Update] Could not parse versions: current={Current}, latest={Latest}",
                    currentVersion, latestVersion);
                return new UpdateInfo(currentVersion, latestVersion, "", IsUpdateAvailable: false);
            }

            if (latest <= current)
            {
                logger.LogInformation("[Update] Already on latest version");
                return new UpdateInfo(currentVersion, latestVersion, "", IsUpdateAvailable: false);
            }

            var expectedAsset = GetExpectedAssetName();
            var downloadUrl = "";

            foreach (var asset in root.GetProperty("assets").EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString();
                if (string.Equals(name, expectedAsset, StringComparison.Ordinal))
                {
                    downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                    break;
                }
            }

            if (string.IsNullOrEmpty(downloadUrl))
            {
                logger.LogWarning("[Update] No asset found for RID {Rid} (expected: {Asset})",
                    RuntimeInformation.RuntimeIdentifier, expectedAsset);
                return new UpdateInfo(currentVersion, latestVersion, "", IsUpdateAvailable: false);
            }

            logger.LogInformation("[Update] Update available: {Latest} (asset: {Asset})", latestVersion, expectedAsset);
            return new UpdateInfo(currentVersion, latestVersion, downloadUrl, IsUpdateAvailable: true);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "[Update] Network error checking for updates");
            return new UpdateInfo(currentVersion, "", "", IsUpdateAvailable: false);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "[Update] Failed to parse GitHub API response");
            return new UpdateInfo(currentVersion, "", "", IsUpdateAvailable: false);
        }
        catch (TaskCanceledException ex)
        {
            logger.LogWarning(ex, "[Update] Update check timed out");
            return new UpdateInfo(currentVersion, "", "", IsUpdateAvailable: false);
        }
    }

    public async Task<string> ApplyUpdateAsync(UpdateInfo update)
    {
        if (!update.IsUpdateAvailable || string.IsNullOrEmpty(update.DownloadUrl))
            throw new UpdateException("No update available.");

        ValidateDownloadUrl(update.DownloadUrl);

        var tempDir = Path.Combine(Path.GetTempPath(), "ytpt-update-" + Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);

        try
        {
            var assetName = GetExpectedAssetName();
            var archivePath = Path.Combine(tempDir, assetName);
            logger.LogInformation("[Update] Downloading {Url}", update.DownloadUrl);

            using var downloadCts = new CancellationTokenSource(DownloadTimeout);
            await DownloadArchiveAsync(update.DownloadUrl, archivePath, downloadCts.Token).ConfigureAwait(false);

            var binaryName = OperatingSystem.IsWindows() ? "ytpt.exe" : "ytpt";
            var extractedBinaryPath = Path.Combine(tempDir, binaryName);

            await ExtractArchiveAsync(archivePath, tempDir, extractedBinaryPath, downloadCts.Token).ConfigureAwait(false);

            if (!File.Exists(extractedBinaryPath))
                throw new UpdateException(
                    $"Archive did not contain expected binary '{binaryName}'.",
                    ReleasePageUrl);

            var currentBinaryPath = Environment.ProcessPath
                ?? throw new UpdateException("Could not determine current binary path.");

            return await binaryUpdater.ApplyAsync(extractedBinaryPath, currentBinaryPath).ConfigureAwait(false);
        }
        catch (UpdateException) { throw; }
        catch (HttpRequestException ex)
        {
            throw new UpdateException($"Failed to download update: {ex.Message}", ReleasePageUrl);
        }
        catch (IOException ex)
        {
            throw new UpdateException($"Failed to extract update archive: {ex.Message}");
        }
        finally
        {
            if (!OperatingSystem.IsWindows())
            {
                try { Directory.Delete(tempDir, recursive: true); }
                catch { /* non-fatal */ }
            }
        }
    }

    private static void ValidateDownloadUrl(string downloadUrl)
    {
        if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var downloadUri) ||
            !string.Equals(downloadUri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) ||
            !downloadUri.Host.EndsWith("github.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new UpdateException("Download URL is not a valid GitHub HTTPS URL.", ReleasePageUrl);
        }
    }

    private static async Task DownloadArchiveAsync(string url, string archivePath, CancellationToken ct)
    {
        var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var fileStream = File.Create(archivePath);
        await using (fileStream.ConfigureAwait(false))
        {
            await response.Content.CopyToAsync(fileStream, ct).ConfigureAwait(false);
        }

        if (new FileInfo(archivePath).Length == 0)
            throw new UpdateException("Downloaded file is empty. Try again or download manually.", ReleasePageUrl);
    }

    private static async Task ExtractArchiveAsync(string archivePath, string tempDir, string extractedBinaryPath, CancellationToken ct)
    {
        if (OperatingSystem.IsWindows())
        {
            var archive = await ZipFile.OpenReadAsync(archivePath, ct).ConfigureAwait(false);
            await using (archive.ConfigureAwait(false))
            {
                var fullTargetPath = Path.GetFullPath(tempDir);
                foreach (var entry in archive.Entries)
                {
                    var destinationPath = Path.GetFullPath(Path.Combine(tempDir, entry.FullName));
                    if (!destinationPath.StartsWith(fullTargetPath + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                        throw new UpdateException("Archive contains path traversal.", ReleasePageUrl);

                    var dir = Path.GetDirectoryName(destinationPath);
                    if (dir != null) Directory.CreateDirectory(dir);

                    if (!string.IsNullOrEmpty(entry.Name))
                        await entry.ExtractToFileAsync(destinationPath, overwrite: true, cancellationToken: ct);
                }
            }
        }
        else
        {
            var gzipStream = new GZipStream(File.OpenRead(archivePath), CompressionMode.Decompress);
            await using (gzipStream.ConfigureAwait(false))
            {
                await TarFile.ExtractToDirectoryAsync(gzipStream, tempDir, overwriteFiles: true, ct).ConfigureAwait(false);

                var fullTarget = Path.GetFullPath(tempDir);
                var fullBinary = Path.GetFullPath(extractedBinaryPath);
                if (!fullBinary.StartsWith(fullTarget + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    throw new UpdateException("Extracted binary path is outside temp directory.", ReleasePageUrl);
            }
        }
    }
}
