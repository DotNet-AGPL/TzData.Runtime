# TzData.Runtime.Linux

**Linux-only** app-local **IANA time zone data** for .NET.

> Renamed from `TzData.Runtime` 1.0.0. This package targets Linux only; macOS already ships
> tzdata, and Windows uses NLS/registry.

.NET's `TimeZoneInfo` reads TZif files from `TZDIR` (default `/usr/share/zoneinfo`). Slim Linux
images such as Alpine, Chiseled and Distroless do not ship tzdata, so
`TimeZoneInfo.FindSystemTimeZoneById("Europe/Kyiv")` throws `TimeZoneNotFoundException`, while
`TimeZoneInfo.Local` silently falls back to UTC. ICU packages do **not** fix this: ICU data and
.NET's `TimeZoneInfo` are separate.

This package ships the compiled TZif tree (`Europe/Kyiv`, `Etc/UTC`, ... plus `zone.tab`) and sets
`TZDIR` explicitly:

```xml
<ItemGroup>
  <PackageReference Include="TzData.Runtime.Linux" Version="1.0.1" />
</ItemGroup>
```

```csharp
using TzData.Runtime.Linux;

// Call once, as early as possible (before the first TimeZoneInfo use).
TzDataRuntime.Configure(defaultTimeZoneId: "Asia/Shanghai");

var kyiv = TimeZoneInfo.FindSystemTimeZoneById("Europe/Kyiv");
Console.WriteLine(TimeZoneInfo.Local.Id);   // Asia/Shanghai
```

The `tzdata` folder is copied next to the application by `buildTransitive` targets for Linux
targets. No system tzdata, root access or container changes are required.

## API

```csharp
namespace TzData.Runtime.Linux;

public static class TzDataRuntime
{
    public static string DataVersion { get; }   // e.g. "2026f"
    public static string DataDirectory { get; } // <app>/tzdata
    public static bool IsSupported { get; }     // true only on Linux
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
- `TimeZoneInfo.ClearCachedData()` runs after configuration.
- `directory` can point at a custom location (for example a read-only mount).
- On macOS/Windows `IsSupported` is `false` and `Configure()` returns `false` without changing
  the environment.

## Automatic setup (optional)

Reference the companion package to run `Configure()` from a module initializer, with no code:

```xml
<PackageReference Include="TzData.Runtime.Linux.AutoInit" Version="1.0.1" />
```

It can also bake in a default local time zone (applied with **higher priority than `TZ`**):

```xml
<PropertyGroup>
  <TzDataRuntimeAutoInitTimeZone>Asia/Shanghai</TzDataRuntimeAutoInitTimeZone>
</PropertyGroup>
```

See [README-AUTOINIT.md](README-AUTOINIT.md) for the exact guarantees, precedence and opt-outs.

## Agent skill (SKILL.md)

Both packages ship an [Agent Skill](https://agentskills.io/specification) at
`skills/tzdata-runtime-linux/SKILL.md`. Agents can discover it from the restored package:

```bash
dotnet-inspect package TzData.Runtime.Linux -S Skills
dotnet-package-skills install
ls ~/.nuget/packages/tzdata.runtime.linux/*/skills/
```

The skill is frozen inside each `.nupkg` and covers TZDIR/TZ precedence, the AutoInit default zone,
verification snippets and troubleshooting.

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
| macOS | Not supported (system tzdata is always present); `Configure()` is a no-op |
| Windows | Not supported (NLS/registry); `Configure()` is a no-op |

## Data and versioning

- Package `1.0.1` bundles IANA tzdata **2026f** (`TzDataRuntime.DataVersion`).
- Package version and tzdata version are independent: tzdata updates ship as `1.0.2`, `1.0.3`, ...
- The `tzdata/` folder is copied only for Linux RIDs (or portable builds on a Linux host); disable
  with `TzDataRuntimeCopyData=false`.
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
