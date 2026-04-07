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
            { "Accept", "application/vnd.github+json" }
        }
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
                return new UpdateInfo(currentVersion, latestVersion, "", false);
            }

            if (latest <= current)
            {
                logger.LogInformation("[Update] Already on latest version");
                return new UpdateInfo(currentVersion, latestVersion, "", false);
            }

            var expectedAsset = GetExpectedAssetName();
            var downloadUrl = "";

            foreach (var asset in root.GetProperty("assets").EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString();
                if (name == expectedAsset)
                {
                    downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                    break;
                }
            }

            if (string.IsNullOrEmpty(downloadUrl))
            {
                logger.LogWarning("[Update] No asset found for RID {Rid} (expected: {Asset})",
                    RuntimeInformation.RuntimeIdentifier, expectedAsset);
                return new UpdateInfo(currentVersion, latestVersion, "", false);
            }

            logger.LogInformation("[Update] Update available: {Latest} (asset: {Asset})", latestVersion, expectedAsset);
            return new UpdateInfo(currentVersion, latestVersion, downloadUrl, true);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "[Update] Network error checking for updates");
            return new UpdateInfo(currentVersion, "", "", false);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "[Update] Failed to parse GitHub API response");
            return new UpdateInfo(currentVersion, "", "", false);
        }
        catch (TaskCanceledException ex)
        {
            logger.LogWarning(ex, "[Update] Update check timed out");
            return new UpdateInfo(currentVersion, "", "", false);
        }
    }

    public async Task<string> ApplyUpdateAsync(UpdateInfo update)
    {
        if (!update.IsUpdateAvailable || string.IsNullOrEmpty(update.DownloadUrl))
            throw new UpdateException("No update available.");

        if (!Uri.TryCreate(update.DownloadUrl, UriKind.Absolute, out var downloadUri) ||
            downloadUri.Scheme != Uri.UriSchemeHttps ||
            !downloadUri.Host.EndsWith("github.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new UpdateException("Download URL is not a valid GitHub HTTPS URL.",
                ReleasePageUrl);
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "ytpt-update-" + Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);

        try
        {
            var assetName = GetExpectedAssetName();
            var archivePath = Path.Combine(tempDir, assetName);
            logger.LogInformation("[Update] Downloading {Url}", update.DownloadUrl);

            using var downloadCts = new CancellationTokenSource(DownloadTimeout);
            var response = await Http.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, downloadCts.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using (var fileStream = File.Create(archivePath))
            {
                await response.Content.CopyToAsync(fileStream, downloadCts.Token).ConfigureAwait(false);
            }

            if (new FileInfo(archivePath).Length == 0)
                throw new UpdateException("Downloaded file is empty. Try again or download manually.",
                    ReleasePageUrl);

            var binaryName = OperatingSystem.IsWindows() ? "ytpt.exe" : "ytpt";
            var extractedBinaryPath = Path.Combine(tempDir, binaryName);

            if (OperatingSystem.IsWindows())
            {
                using var archive = ZipFile.OpenRead(archivePath);
                var fullTargetPath = Path.GetFullPath(tempDir);
                foreach (var entry in archive.Entries)
                {
                    var destinationPath = Path.GetFullPath(Path.Combine(tempDir, entry.FullName));
                    if (!destinationPath.StartsWith(fullTargetPath + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                        throw new UpdateException("Archive contains path traversal.", ReleasePageUrl);

                    // Create directory if needed
                    var dir = Path.GetDirectoryName(destinationPath);
                    if (dir != null) Directory.CreateDirectory(dir);

                    if (!string.IsNullOrEmpty(entry.Name)) // Skip directories
                        entry.ExtractToFile(destinationPath, overwrite: true);
                }
            }
            else
            {
                await using var gzipStream = new GZipStream(File.OpenRead(archivePath), CompressionMode.Decompress);
                await TarFile.ExtractToDirectoryAsync(gzipStream, tempDir, overwriteFiles: true).ConfigureAwait(false);

                // Verify extracted binary is within temp directory
                var fullTarget = Path.GetFullPath(tempDir);
                var fullBinary = Path.GetFullPath(extractedBinaryPath);
                if (!fullBinary.StartsWith(fullTarget + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    throw new UpdateException("Extracted binary path is outside temp directory.", ReleasePageUrl);
            }

            if (!File.Exists(extractedBinaryPath))
                throw new UpdateException(
                    $"Archive did not contain expected binary '{binaryName}'.",
                    ReleasePageUrl);

            var currentBinaryPath = Environment.ProcessPath
                ?? throw new UpdateException("Could not determine current binary path.");

            return await binaryUpdater.ApplyAsync(extractedBinaryPath, currentBinaryPath).ConfigureAwait(false);
        }
        catch (UpdateException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new UpdateException(
                $"Failed to download update: {ex.Message}",
                ReleasePageUrl);
        }
        catch (IOException ex)
        {
            throw new UpdateException($"Failed to extract update archive: {ex.Message}");
        }
        finally
        {
            // On Windows, the update script runs after the process exits and needs
            // the temp directory. On Unix, we can clean up immediately.
            if (!OperatingSystem.IsWindows())
            {
                try { Directory.Delete(tempDir, recursive: true); }
                catch { /* non-fatal */ }
            }
        }
    }
}
