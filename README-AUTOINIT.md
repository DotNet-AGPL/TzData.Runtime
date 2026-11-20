# TzData.Runtime.AutoInit

Opt-in companion package for [TzData.Runtime](../README.md). It injects a module initializer into
**executable** projects so `TzDataRuntime.Configure()` runs before `Main` — no startup code needed.

```xml
<ItemGroup>
  <PackageReference Include="TzData.Runtime.AutoInit" Version="1.0.0" />
</ItemGroup>
```

The initializer only sets `TZDIR` when it is not already set; it does not set `TZ`. If you need a
non-UTC local zone, call `TzDataRuntime.Configure(defaultTimeZoneId: "...")` yourself, or set `TZ`
in the environment.

## Guarantees and limits

- Injected **only** when `OutputType` is `Exe` or `WinExe`. Libraries do not get the initializer,
  because a library may load after the app has already used `TimeZoneInfo`.
- Runs once, synchronously, before `Main`, for the app assembly.
- Exceptions are swallowed: a missing data folder never prevents the app from starting
  (`TimeZoneInfo` then behaves as if tzdata were absent).
- Windows: `Configure()` returns `false`; nothing changes.

## Disable

```xml
<PropertyGroup>
  <TzDataRuntimeAutoInit>false</TzDataRuntimeAutoInit>
</PropertyGroup>
```

Then call `TzDataRuntime.Configure()` at startup yourself.

## How it works

The package ships:

- `buildTransitive/TzData.Runtime.AutoInit.targets` — adds the injected source only for executable
  projects;
- `content/TzDataAutoInit.cs` — the module initializer that calls `TzDataRuntime.Configure()`.

The package depends on `TzData.Runtime` so the API and the `tzdata` folder are available.
