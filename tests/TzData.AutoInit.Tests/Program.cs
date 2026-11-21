using System;
using System.IO;
using TzData.Runtime.Linux;

internal static class Program
{
    private static int Main()
    {
        if (OperatingSystem.IsWindows())
        {
            Check(!TzDataRuntime.IsSupported, "Windows must report IsSupported=false");
            Check(!TzDataRuntime.Configure(), "Configure() must return false on Windows");
            Console.WriteLine("PASS  Windows no-op");
            return 0;
        }

        // No explicit Configure() call here: the AutoInit package's module initializer must
        // have run before Main (TZDIR and the build-time default TZ).
        string tzdir = Environment.GetEnvironmentVariable("TZDIR") ?? string.Empty;
        Check(tzdir.Length > 0, "the module initializer should have set TZDIR");
        Check(Directory.Exists(tzdir), $"TZDIR should exist: {tzdir}");
        Check(File.Exists(Path.Combine(tzdir, "zone.tab")), "zone.tab should be present");

        string? environmentTz = Environment.GetEnvironmentVariable("TZ");
        bool defaultTimeZoneDisabled = Environment.GetEnvironmentVariable("TZDATA_DISABLE_DEFAULT_TZ") == "1";

        if (defaultTimeZoneDisabled)
        {
            string expected = string.IsNullOrEmpty(environmentTz) ? TimeZoneInfo.Local.Id : environmentTz;
            Check(TimeZoneInfo.Local.Id == expected,
                $"with TZDATA_DISABLE_DEFAULT_TZ=1, Local should follow TZ (expected {expected}, got {TimeZoneInfo.Local.Id})");
        }
        else
        {
            Check(TimeZoneInfo.Local.Id == "Asia/Shanghai",
                $"build-time default should win over TZ (expected Asia/Shanghai, got {TimeZoneInfo.Local.Id})");
        }

        TimeZoneInfo kyiv = TimeZoneInfo.FindSystemTimeZoneById("Europe/Kyiv");
        Check(kyiv.BaseUtcOffset != TimeSpan.Zero, "Europe/Kyiv should have a non-zero offset");

        int zones = TimeZoneInfo.GetSystemTimeZones().Count;
        Check(zones > 100, $"GetSystemTimeZones() should enumerate the database (got {zones})");

        Console.WriteLine($"PASS  AutoInit: TZDIR={tzdir}, Local={TimeZoneInfo.Local.Id}, {zones} zones");
        return 0;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
