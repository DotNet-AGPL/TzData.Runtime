# TzData.Runtime.Linux.AutoInit

Opt-in companion package for [TzData.Runtime.Linux](../README.md). It injects a module initializer
into **executable** projects so `TzDataRuntime.Configure()` runs before `Main` — no startup code
needed. Linux only; macOS/Windows are no-ops.

```xml
<ItemGroup>
  <PackageReference Include="TzData.Runtime.Linux.AutoInit" Version="1.0.1" />
</ItemGroup>
```

## Default local time zone (optional)

```xml
<PropertyGroup>
  <TzDataRuntimeAutoInitTimeZone>Asia/Shanghai</TzDataRuntimeAutoInitTimeZone>
</PropertyGroup>
```

Precedence: **build-time default > `TZ` environment variable > `/etc/localtime` > UTC**.

The value is baked into the app assembly as metadata. At startup the initializer:

1. sets `TZDIR` to `<app>/tzdata` (unless it is already set);
2. validates `TzDataRuntimeAutoInitTimeZone` against that data;
3. overrides `TZ` with the build-time value and clears the `TimeZoneInfo` cache.

If validation fails, the environment `TZ` is left untouched. Leaving the property empty keeps the
current environment behavior.

### Opt-outs

- `TZDATA_DISABLE_DEFAULT_TZ=1` at runtime: ignore the build-time default and keep `TZ`.
- `<TzDataRuntimeAutoInit>false</TzDataRuntimeAutoInit>`: do not inject the initializer at all.
- `<TzDataRuntimeAutoInitTimeZone></TzDataRuntimeAutoInitTimeZone>`: no build-time default.

## Guarantees and limits

- Injected **only** when `OutputType` is `Exe` or `WinExe`. Libraries do not get the initializer,
  because a library may load after the app has already used `TimeZoneInfo`.
- Runs once, synchronously, before `Main`, for the app assembly.
- Exceptions are swallowed: a missing data folder or invalid zone never prevents the app from
  starting.
- Non-Linux platforms: `Configure()` returns `false`; nothing changes.
- The build-time default also applies to child processes because `TZ` is a process environment
  variable.

## How it works

The package ships:

- `buildTransitive/TzData.Runtime.Linux.AutoInit.targets` — adds the injected source for executable
  projects and, when configured, the `TzDataRuntime.DefaultTimeZone` assembly metadata;
- `content/TzDataAutoInit.cs` — the module initializer that calls `Configure()` and applies the
  build-time default.

The package depends on `TzData.Runtime.Linux` so the API and the `tzdata` folder are available.
