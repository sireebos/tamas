using System;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using RestaurantManager.Services.Infrastructure;

namespace RestaurantManager.Services;

/// <summary>
/// Checks for application updates from a remote JSON endpoint.
/// Expected JSON format: { "version": "2.3.0", "download_url": "https://...", "release_notes": "..." }
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// Checks the configured URL for a newer version.
    /// Returns update info if available, null if up-to-date or check fails.
    /// </summary>
    Task<UpdateInfo?> CheckForUpdateAsync(string? updateCheckUrl = null);

    /// <summary>
    /// Gets the current application version.
    /// </summary>
    Version GetCurrentVersion();
}

public class UpdateInfo
{
    public string LatestVersion { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public string ReleaseNotes { get; set; } = "";
}

public class UpdateService : IUpdateService
{
    private readonly HttpClient _httpClient;
    private readonly ILoggerService? _logger;

    // Default URL — can be overridden via BrandingSettings.UpdateCheckUrl
    private const string DefaultUpdateUrl = "https://gist.githubusercontent.com/sireebos/48bb09c778e714e8182597986240d5e9/raw/2c3f82c10c7227183c2eaf0589fc6f51aee3ece4/version.json";

    public UpdateService(ILoggerService? logger = null)
    {
        _logger = logger;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    public Version GetCurrentVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        var ver = asm.GetName().Version;
        return ver ?? new Version(1, 0, 0);
    }

    public async Task<UpdateInfo?> CheckForUpdateAsync(string? updateCheckUrl = null)
    {
        var url = string.IsNullOrWhiteSpace(updateCheckUrl) ? DefaultUpdateUrl : updateCheckUrl;

        try
        {
            _logger?.LogInformation("Checking for updates at {Url}", url);

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogDebug("Update check returned {StatusCode}", (int)response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var versionStr = root.TryGetProperty("version", out var vProp) ? vProp.GetString() : null;
            if (string.IsNullOrWhiteSpace(versionStr))
            {
                _logger?.LogDebug("Update response missing 'version' field");
                return null;
            }

            // Parse the remote version
            if (!Version.TryParse(versionStr, out var remoteVersion))
            {
                _logger?.LogWarning("Invalid version format in update response: {Version}", versionStr);
                return null;
            }

            var currentVersion = GetCurrentVersion();

            // Compare: only major.minor.build (ignore revision)
            var current = new Version(currentVersion.Major, currentVersion.Minor, currentVersion.Build < 0 ? 0 : currentVersion.Build);
            var remote = new Version(remoteVersion.Major, remoteVersion.Minor, remoteVersion.Build < 0 ? 0 : remoteVersion.Build);

            if (remote <= current)
            {
                _logger?.LogInformation("App is up to date (current: {Current}, remote: {Remote})", current, remote);
                return null;
            }

            var downloadUrl = root.TryGetProperty("download_url", out var dProp) ? dProp.GetString() ?? "" : "";
            var releaseNotes = root.TryGetProperty("release_notes", out var rProp) ? rProp.GetString() ?? "" : "";

            _logger?.LogInformation("Update available: {Remote} (current: {Current})", remote, current);

            return new UpdateInfo
            {
                LatestVersion = versionStr,
                DownloadUrl = downloadUrl,
                ReleaseNotes = releaseNotes
            };
        }
        catch (TaskCanceledException)
        {
            _logger?.LogDebug("Update check timed out");
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogWarning(ex, "Update check network error");
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Update check failed");
            return null;
        }
    }
}
