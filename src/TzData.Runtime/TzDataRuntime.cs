using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace TzData;

/// <summary>
/// Configures app-local IANA time zone data for <see cref="TimeZoneInfo"/> on Linux and macOS.
/// </summary>
/// <remarks>
/// .NET reads TZif files from <c>TZDIR</c> (default <c>/usr/share/zoneinfo</c>) on Unix.
/// Calling <see cref="Configure"/> points <c>TZDIR</c> at the <c>tzdata</c> folder copied next to
/// the application by the <c>TzData.Runtime</c> package. Windows uses the NLS/registry database,
/// so all methods are no-ops there (see <see cref="IsSupported"/>).
/// </remarks>
public static class TzDataRuntime
{
    private const string TzDirEnvironmentVariable = "TZDIR";
    private const string TzEnvironmentVariable = "TZ";

    private static readonly object Sync = new();
    private static string? _dataDirectory;
    private static bool _configured;

    /// <summary>
    /// The IANA tzdata release compiled into this package (for example <c>2026f</c>).
    /// </summary>
    public static string DataVersion { get; } =
        typeof(TzDataRuntime).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => string.Equals(a.Key, "TzDataVersion", StringComparison.Ordinal))
            ?.Value ?? "unknown";

    /// <summary>
    /// <see langword="true"/> on Linux and macOS; <see langword="false"/> on Windows.
    /// </summary>
    public static bool IsSupported => !OperatingSystem.IsWindows();

    /// <summary>
    /// The default data folder: <c>tzdata</c> next to the application executable.
    /// </summary>
    public static string DataDirectory => _dataDirectory ??= Path.Combine(AppContext.BaseDirectory, "tzdata");

    /// <summary>
    /// <see langword="true"/> when <c>TZDIR</c> is set (by this API or by the environment).
    /// </summary>
    public static bool IsConfigured => _configured || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(TzDirEnvironmentVariable));

    /// <summary>
    /// Points <c>TZDIR</c> at the app-local tzdata folder and optionally sets <c>TZ</c>.
    /// </summary>
    /// <param name="directory">
    /// tzdata folder to use; <see langword="null"/> uses <see cref="DataDirectory"/>.
    /// </param>
    /// <param name="defaultTimeZoneId">
    /// Optional IANA zone ID (for example <c>Asia/Shanghai</c>) used for <c>TZ</c> only when
    /// <c>TZ</c> is not already set. This makes <see cref="TimeZoneInfo.Local"/> use that zone.
    /// </param>
    /// <param name="overwrite">
    /// When <see langword="true"/>, replaces an existing <c>TZDIR</c>/<c>TZ</c>. The default
    /// respects values already provided by the environment.
    /// </param>
    /// <returns><see langword="true"/> when configured; <see langword="false"/> on Windows.</returns>
    /// <exception cref="DirectoryNotFoundException">The directory does not contain tzdata.</exception>
    /// <exception cref="TimeZoneNotFoundException"><paramref name="defaultTimeZoneId"/> is not present in the data.</exception>
    public static bool Configure(string? directory = null, string? defaultTimeZoneId = null, bool overwrite = false)
    {
        if (!IsSupported)
        {
            return false;
        }

        lock (Sync)
        {
            string currentTzDir = Environment.GetEnvironmentVariable(TzDirEnvironmentVariable) ?? string.Empty;
            bool useOwnData = string.IsNullOrEmpty(currentTzDir) || overwrite;

            string targetDirectory;
            if (useOwnData)
            {
                targetDirectory = Path.GetFullPath(directory ?? DataDirectory);
                ValidateDataDirectory(targetDirectory);
                Environment.SetEnvironmentVariable(TzDirEnvironmentVariable, targetDirectory);
            }
            else
            {
                targetDirectory = currentTzDir;
            }

            if (!string.IsNullOrEmpty(defaultTimeZoneId))
            {
                string currentTz = Environment.GetEnvironmentVariable(TzEnvironmentVariable) ?? string.Empty;
                if (string.IsNullOrEmpty(currentTz) || overwrite)
                {
                    Environment.SetEnvironmentVariable(TzEnvironmentVariable, defaultTimeZoneId);
                }
            }

            // Drop the per-process caches so Local and GetSystemTimeZones() see the new data.
            TimeZoneInfo.ClearCachedData();

            if (!string.IsNullOrEmpty(defaultTimeZoneId))
            {
                // Fail fast with the standard exception when the requested zone is not in the data.
                _ = TimeZoneInfo.FindSystemTimeZoneById(defaultTimeZoneId);
            }

            _dataDirectory = targetDirectory;
            _configured = true;
            return true;
        }
    }

    private static void ValidateDataDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException(
                $"The tzdata directory '{directory}' was not found. Publish the app with the TzData.Runtime package, or pass a valid directory to Configure().");
        }

        string zoneTab = Path.Combine(directory, "zone.tab");
        string utc = Path.Combine(directory, "Etc", "UTC");
        if (!File.Exists(zoneTab) && !File.Exists(utc))
        {
            throw new DirectoryNotFoundException(
                $"The directory '{directory}' does not look like tzdata (missing zone.tab and Etc/UTC).");
        }
    }
}
