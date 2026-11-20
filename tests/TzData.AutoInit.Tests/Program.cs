using System;
using System.IO;
using TzData;

internal static class Program
{
    private static int Main()
    {
        if (OperatingSystem.IsWindows())
        {
            Check(!TzDataRuntime.IsSupported, "Windows must report IsSupported=false");
            Console.WriteLine("PASS  AutoInit Windows no-op");
            return 0;
        }

        // No explicit Configure() call here: the AutoInit package's module initializer must
        // have run before Main.
        string tzdir = Environment.GetEnvironmentVariable("TZDIR") ?? string.Empty;
        Check(tzdir.Length > 0, "the module initializer should have set TZDIR");
        Check(Directory.Exists(tzdir), $"TZDIR should exist: {tzdir}");
        Check(File.Exists(Path.Combine(tzdir, "zone.tab")), "zone.tab should be present");

        TimeZoneInfo kyiv = TimeZoneInfo.FindSystemTimeZoneById("Europe/Kyiv");
        Check(kyiv.BaseUtcOffset != TimeSpan.Zero, "Europe/Kyiv should have a non-zero offset");

        int zones = TimeZoneInfo.GetSystemTimeZones().Count;
        Check(zones > 100, $"GetSystemTimeZones() should enumerate the database (got {zones})");

        Console.WriteLine($"PASS  AutoInit: TZDIR={tzdir}, {zones} zones");
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
