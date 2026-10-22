# dnSpy MSI installers

Builds Windows Installer (`.msi`) packages for dnSpy, one per architecture.

| Package | Installs to | Notes |
|---|---|---|
| `dnSpy-x64-6.1.8.msi` | `C:\Program Files\dnSpy` | 64-bit, `ProgramFiles64Folder` |
| `dnSpy-x86-6.1.8.msi` | `C:\Program Files (x86)\dnSpy` | 32-bit, `ProgramFilesFolder` |

The two packages are **separate products** with different `UpgradeCode`s, so
installing one never removes or upgrades the other. Both are per-machine
installs and both are self-contained (the .NET 5 runtime ships inside the
package), so there is no .NET prerequisite.

## Prerequisites

* **WiX Toolset v3** (LGPL) — default location `C:\Tools\WiX3`, override with
  `-WixDir`. The v3 toolset is used deliberately: WiX v4+ requires accepting
  the OSMF (Open Source Maintenance Fee) EULA, v3 does not.
* **.NET 5 SDK** — the publish step needs it. `build.ps1` produces the
  publish output that the MSI is built from.

## Building

```powershell
# 1. Produce the publish output (must run first, per architecture)
.\build.ps1 net-x64
.\build.ps1 net-x86

# 2. Build the MSIs
.\Build\Installer\build-msi.ps1 -OutputDir C:\dnSpy-msi
```

`build-msi.ps1` harvests the publish directory with `heat` and links it with
`candle`/`light`. It expects each publish directory to already have the
layout that `build.ps1` creates:

```
publish\
  dnSpy.exe            <- apphost, patched to load bin\dnSpy.dll
  dnSpy.Console.exe    <- apphost, patched to load bin\dnSpy.Console.dll
  bin\                 <- managed assemblies, extensions, runtime, debug\core
```

`release.ps1` runs the MSI build automatically after the zip packaging.

## Important: the publish directory must be restructured first

`build.ps1` moves the published payload into a `bin\` subdirectory and patches
the apphosts with `AppHostPatcher` so they load the managed assembly from
`bin\`. `build-msi.ps1` verifies this layout and fails with a clear message
if `bin\` is missing. Running `dotnet publish` directly produces a flat
directory that is **not** installable as-is.

## Extensions included

All nine MEF extensions are part of the payload:

`dnSpy.Analyzer`, `dnSpy.AsmEditor`, `dnSpy.BamlDecompiler`,
`dnSpy.Debugger`, `dnSpy.Debugger.DotNet.CorDebug`, `dnSpy.Debugger.DotNet.Mono`,
`dnSpy.Decompiler.ILSpy`, `dnSpy.DeepSearch`, `dnSpy.Scripting.Roslyn`.

The `debug\core\{x86,x64}\dbgshim.dll` files are staged by the
`CopyDotNetDebugFiles_Publish` target in
`dnSpy.Debugger.DotNet.CorDebug.csproj`. Without them the debugger fails at
runtime with `Couldn't find dbgshim.dll` when attaching to a CoreCLR process.
