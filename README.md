# TzData.Runtime

App-local **IANA time zone data** for .NET on Linux and macOS.

.NET's `TimeZoneInfo` reads TZif files from `TZDIR` (default `/usr/share/zoneinfo`). Slim images
such as Alpine, Chiseled and Distroless do not ship tzdata, so
`TimeZoneInfo.FindSystemTimeZoneById("Europe/Kyiv")` throws `TimeZoneNotFoundException`, while
`TimeZoneInfo.Local` silently falls back to UTC. ICU packages do **not** fix this: ICU data and
.NET's `TimeZoneInfo` are separate.

This package ships the compiled TZif tree (`Europe/Kyiv`, `Etc/UTC`, ... plus `zone.tab`) and sets
`TZDIR` explicitly:

```xml
<ItemGroup>
  <PackageReference Include="TzData.Runtime" Version="1.0.0" />
</ItemGroup>
```

```csharp
using TzData;

// Call once, as early as possible (before the first TimeZoneInfo use).
TzDataRuntime.Configure(defaultTimeZoneId: "Asia/Shanghai");

var kyiv = TimeZoneInfo.FindSystemTimeZoneById("Europe/Kyiv");
Console.WriteLine(TimeZoneInfo.Local.Id);   // Asia/Shanghai
```

The `tzdata` folder is copied next to the application by `buildTransitive` targets (output and
publish). No system tzdata, root access or container changes are required.

## API

```csharp
public static class TzDataRuntime
{
    public static string DataVersion { get; }   // e.g. "2026f"
    public static string DataDirectory { get; } // <app>/tzdata
    public static bool IsSupported { get; }     // false on Windows
    public static bool IsConfigured { get; }    // TZDIR is set
    public static bool Configure(
        string? directory = null,
        string? defaultTimeZoneId = null,
        bool overwrite = false);
}
```

- `Configure()` sets `TZDIR` only when it is not already set; pass `overwrite: true` to replace it.
- `defaultTimeZoneId` sets `TZ` only when `TZ` is not already set. Without it, `Local` stays on
  `/etc/localtime` or falls back to UTC.
- `TimeZoneInfo.ClearCachedData()` runs after configuration, so call it before the first use for
  best results.
- `directory` can point at a custom location (for example a read-only mount).
- Windows uses NLS/registry; `IsSupported` is `false` and `Configure()` returns `false`.

## Automatic setup (optional)

Reference the companion package to run `Configure()` from a module initializer, with no code:

```xml
<PackageReference Include="TzData.Runtime.AutoInit" Version="1.0.0" />
```

See [README-AUTOINIT.md](README-AUTOINIT.md) for the exact guarantees and how to disable it.

## Containers

```dockerfile
# Nothing to install: the app carries tzdata and sets TZDIR in code.
COPY --from=build /app/publish /app
WORKDIR /app
ENTRYPOINT ["./MyApp"]
```

If you prefer the environment instead of code:

```dockerfile
ENV TZDIR=/app/tzdata
ENV TZ=Asia/Shanghai
```

`TZDIR` is process-wide and inherited by child processes; it never modifies the host or
`/usr/share/zoneinfo`.

## Platform behavior

| Platform | Behavior |
|---|---|
| Linux (glibc/musl) | `TZDIR` + app-local TZif; works on Alpine/Chiseled/Distroless |
| macOS | .NET reads `TZDIR`; native frameworks keep using the system database |
| Windows | No-op (NLS/registry); package still installs but does not change behavior |

## Data and versioning

- Package `1.0.0` bundles IANA tzdata **2026f** (`TzDataRuntime.DataVersion`).
- Package version and tzdata version are independent: tzdata updates ship as `1.0.1`, `1.0.2`, ...
- `GetSystemTimeZones()` enumerates `zone.tab`; if you trim the data folder, keep `zone.tab` for
  full enumeration and the individual TZif files for lookups.

## Building

```bash
# Regenerate tzdata/ from IANA (requires zic + curl + sha512sum)
./scripts/build-tzdata.sh 2026f

# Verify the committed data matches the release
./scripts/build-tzdata.sh 2026f --verify

# Pack
dotnet pack src/TzData.Runtime/TzData.Runtime.csproj -c Release -o nupkg
dotnet pack src/TzData.Runtime.AutoInit/TzData.Runtime.AutoInit.csproj -c Release -o nupkg
```

## License

- Managed code and build scripts: Apache-2.0 (see `LICENSE`).
- Bundled IANA tzdata: public domain (see `LICENSE-TZDATA`).
