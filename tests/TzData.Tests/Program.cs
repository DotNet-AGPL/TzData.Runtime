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

        // Start from a clean process environment so the assertions are deterministic.
        Environment.SetEnvironmentVariable("TZDIR", null);
        Environment.SetEnvironmentVariable("TZ", null);

        Check(TzDataRuntime.IsSupported, "Unix must report IsSupported=true");
        Check(TzDataRuntime.Configure(defaultTimeZoneId: "Asia/Shanghai"), "Configure() should succeed");

        string tzdir = Environment.GetEnvironmentVariable("TZDIR") ?? string.Empty;
        Check(tzdir.Length > 0, "TZDIR should be set");
        Check(Directory.Exists(tzdir), $"TZDIR should exist: {tzdir}");
        Check(File.Exists(Path.Combine(tzdir, "zone.tab")), "zone.tab should be present");
        Check(TzDataRuntime.IsConfigured, "IsConfigured should be true");
        Check(!string.IsNullOrEmpty(TzDataRuntime.DataVersion), "DataVersion should be reported");

        TimeZoneInfo kyiv = TimeZoneInfo.FindSystemTimeZoneById("Europe/Kyiv");
        TimeSpan summer = kyiv.GetUtcOffset(new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc));
        TimeSpan winter = kyiv.GetUtcOffset(new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc));
        Check(summer != winter, $"Europe/Kyiv should observe DST (summer={summer}, winter={winter})");

        int zones = TimeZoneInfo.GetSystemTimeZones().Count;
        Check(zones > 100, $"GetSystemTimeZones() should enumerate the database (got {zones})");
        string localId = TimeZoneInfo.Local.Id;
        Check(localId == "Asia/Shanghai", $"Local should be Asia/Shanghai (got {localId})");

        // A user-provided TZDIR must win unless overwrite is requested.
        Environment.SetEnvironmentVariable("TZDIR", "/tmp");
        Check(TzDataRuntime.Configure() && Environment.GetEnvironmentVariable("TZDIR") == "/tmp",
            "Configure() must not overwrite an existing TZDIR");

        Console.WriteLine($"PASS  tzdata {TzDataRuntime.DataVersion}: {zones} zones, Kyiv DST {summer} / {winter}, Local={localId}");
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
