---
name: tzdata-runtime-linux
description: >-
  Use TzData.Runtime.Linux to give .NET apps their own IANA time zone database on Linux
  (Alpine, Chiseled, Distroless) and fix "TimeZoneNotFoundException" or a Local time zone that
  silently becomes UTC. Covers TZDIR, TZ precedence, the optional AutoInit package and the
  build-time default zone. 为 Linux 上的 .NET 应用随包携带 IANA tzdata：修复
  TimeZoneNotFoundException / Local 变成 UTC，说明 TZDIR、TZ 优先级、AutoInit 与构建期默认时区。
license: Apache-2.0
metadata:
  authors: DotNet-AGPL
  package: TzData.Runtime.Linux
---

# TzData.Runtime.Linux — app-local time zone data for .NET on Linux

.NET's `TimeZoneInfo` reads TZif files from `TZDIR` (default `/usr/share/zoneinfo`) on Linux.
Slim images (Alpine, Chiseled, Distroless) often ship no tzdata. ICU packages do not help: ICU
data and .NET's `TimeZoneInfo` are separate databases.

This package ships the compiled tzdata next to the app and points `TZDIR` at it.

## When to use

- `TimeZoneInfo.FindSystemTimeZoneById("Europe/Kyiv")` throws `TimeZoneNotFoundException`.
- `TimeZoneInfo.Local.Id` is `UTC` in a container even though the host/region is not UTC.
- The app must not depend on `/usr/share/zoneinfo` or install `tzdata`.
- Linux only. macOS already ships tzdata; Windows uses NLS/registry (both are no-ops here).

## Setup (explicit)

```xml
<ItemGroup>
  <PackageReference Include="TzData.Runtime.Linux" Version="1.0.1" />
</ItemGroup>
```

```csharp
using TzData.Runtime.Linux;

// Call once, before the first TimeZoneInfo use.
TzDataRuntime.Configure(defaultTimeZoneId: "Asia/Shanghai");

var kyiv = TimeZoneInfo.FindSystemTimeZoneById("Europe/Kyiv");
```

- Sets `TZDIR` to `<app>/tzdata` only when it is not already set (`overwrite: true` replaces it).
- `defaultTimeZoneId` sets `TZ` only when `TZ` is empty; existing environment wins.
- Clears the `TimeZoneInfo` cache after configuring.

## Setup (automatic, optional)

```xml
<ItemGroup>
  <PackageReference Include="TzData.Runtime.Linux.AutoInit" Version="1.0.1" />
</ItemGroup>
```

Optional build-time local zone (higher priority than `TZ`):

```xml
<PropertyGroup>
  <TzDataRuntimeAutoInitTimeZone>Asia/Shanghai</TzDataRuntimeAutoInitTimeZone>
</PropertyGroup>
```

- Injects a module initializer only into `Exe`/`WinExe` projects.
- Precedence: **build-time default > `TZ` env > `/etc/localtime` > UTC**.
- `TZDATA_DISABLE_DEFAULT_TZ=1` keeps the environment `TZ`.
- `<TzDataRuntimeAutoInit>false</TzDataRuntimeAutoInit>` disables injection.

## Rules and pitfalls

1. `TZ` alone is not enough: the TZif file must exist under `TZDIR`; otherwise .NET silently falls
   back to UTC.
2. `FindSystemTimeZoneById` throws when the file is missing; `Local` does **not** throw.
3. `TZDIR` is a process environment variable, inherited by child processes. It never modifies
   `/usr/share/zoneinfo` or other processes.
4. If something already used `TimeZoneInfo` before configuration, call
   `TimeZoneInfo.ClearCachedData()` (the API does it) or configure at startup.
5. `TZDIR` support requires .NET 8+.
6. macOS/Windows: `IsSupported` is `false`; `Configure()` returns `false` and changes nothing.

## Verify

```csharp
var kyiv = TimeZoneInfo.FindSystemTimeZoneById("Europe/Kyiv");
var summer = kyiv.GetUtcOffset(new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc));
var winter = kyiv.GetUtcOffset(new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc));
Console.WriteLine($"{summer} / {winter}");                       // 03:00 / 02:00
Console.WriteLine(TimeZoneInfo.GetSystemTimeZones().Count);      // > 100
Console.WriteLine(TimeZoneInfo.Local.Id);                        // configured zone
```

In a container, confirm the app folder contains `tzdata/zone.tab` and `tzdata/Europe/Kyiv`.

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `TimeZoneNotFoundException` | TZDIR does not point at the data, or the zone file is missing. |
| `Local.Id` is `UTC` | No `TZ` and no `/etc/localtime`, or `TZ` is a relative ID without a matching TZif. |
| `GetSystemTimeZones()` only has UTC | `zone.tab` is missing from the data folder. |
| Build-time zone ignored | `TZDATA_DISABLE_DEFAULT_TZ=1` is set, or the zone is not present in the data. |
| Settings seem ignored | Another component cached `TimeZoneInfo`; configure earlier or clear the cache. |

## 中文速查

- **适用**：Linux 精简镜像缺 tzdata，`FindSystemTimeZoneById` 抛异常、`Local` 变 UTC。
- **显式**：`TzDataRuntime.Configure(defaultTimeZoneId: "Asia/Shanghai")`，尽早调用。
- **自动**：引用 `TzData.Runtime.Linux.AutoInit`；可选
  `TzDataRuntimeAutoInitTimeZone`，优先级 **构建期默认 > TZ > /etc/localtime > UTC**；
  `TZDATA_DISABLE_DEFAULT_TZ=1` 回退到环境 TZ。
- **铁律**：只设 `TZ` 不够，必须有 TZDIR 下的 TZif；`TZDIR` 是进程级；.NET 8+ 支持。
- **平台**：仅 Linux；macOS 自带 tzdata，Windows 用 NLS，二者 no-op。
- **排错**：确认发布目录有 `tzdata/zone.tab` 和对应时区文件。

## References

- NuGet: <https://www.nuget.org/packages/TzData.Runtime.Linux>
- Repo README: `README.md`, `README-AUTOINIT.md`
