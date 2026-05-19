# CLAUDE.md — dnSpy + DeepSearch extension

## What this repo is

This is a fork of [dnSpy](https://github.com/dnSpy/dnSpy) (a .NET assembly decompiler/debugger) with a custom **DeepSearch** extension added under `Extensions/dnSpy.DeepSearch/`. The upstream dnSpy code is untouched; all custom work lives in that extension folder and in build tooling at the repo root.

## Build

```powershell
# Full release build + zip packaging
.\release.ps1

# Debug build only (faster iteration)
dotnet build dnSpy.sln -c Debug

# Single extension project
dotnet build Extensions/dnSpy.DeepSearch/dnSpy.DeepSearch.csproj -c Debug
```

`release.ps1` restores submodules, runs `build.ps1 -Release`, and zips the output. On a clean clone always run `git submodule update --init --recursive` first.

Target frameworks: `net5.0-windows` (release), `net48` (debug fallback). Both are listed in the csproj `<TargetFrameworks>`.

## DeepSearch extension layout

```
Extensions/dnSpy.DeepSearch/
├── Core/                        # Pure logic — no WPF, no MEF
│   ├── DeepSearchOptions.cs     # SearchScope, DllSource, MatchMode, options model
│   ├── DeepSearchResult.cs      # ResultKind enum + single match model
│   ├── DeepSearchResultGroup.cs # Grouped results per assembly
│   ├── MatchHelper.cs           # Substring / wildcard / regex matching
│   ├── AssemblyDeepSearcher.cs  # Scans one ModuleDef, yields DeepSearchResult
│   └── DeepSearchEngine.cs      # Orchestrates N assemblies; fires events to UI
├── Services/
│   ├── IDeepSearchService.cs    # MEF service interface consumed by the VM
│   ├── DeepSearchService.cs     # Collects search targets; bridges engine ↔ UI
│   └── DeepSearchSettings.cs    # Persists search history via ISettingsService
├── UI/
│   ├── DeepSearchToolWindowContent.cs  # MEF tool window registration
│   ├── DeepSearchControl.xaml          # Main WPF panel
│   ├── DeepSearchControl.xaml.cs       # Code-behind (double-click navigation)
│   └── ViewModels/
│       ├── DeepSearchViewModel.cs       # Root VM — commands, options, result list
│       ├── DeepSearchResultGroupVM.cs   # Per-assembly group VM
│       └── DeepSearchResultVM.cs        # Single result row VM
└── Plugin/
    └── DeepSearchPlugin.cs      # [ExportAutoLoaded] — Ctrl+Alt+D shortcut, View menu item
```

## Adding features — quick reference

### New result kind (e.g. Custom Attribute)
1. Add value to `ResultKind` in `DeepSearchResult.cs` + update `KindLabel`.
2. Add scanning logic to `AssemblyDeepSearcher.Search()`.
3. No UI changes needed — result rows are data-driven.

### New scope flag (e.g. Attributes)
1. Add flag to `SearchScope` in `DeepSearchOptions.cs`.
2. Add a `CheckBox` to `DeepSearchControl.xaml` bound to a new VM bool property.
3. Include the flag in `DeepSearchViewModel.BuildOptions()`.
4. Handle it in `AssemblyDeepSearcher.Search()`.

### New source mode (e.g. NuGet)
1. Add value to `DllSource` in `DeepSearchOptions.cs`.
2. Add a `RadioButton` to the source `WrapPanel` in `DeepSearchControl.xaml`.
3. Add the VM bool property + update `IsFolderVisible` logic in `DeepSearchViewModel`.
4. Implement target enumeration in `DeepSearchService.GetTargets()`.

### New settings key
Extend `IDeepSearchSettings` + `DeepSearchSettings` to store/restore the value via `ISettingsSection`.

## Coding conventions

- No comments unless the WHY is non-obvious. No docstrings on obvious methods.
- WPF bindings use MVVM strictly — no code-behind logic beyond event routing.
- All heavy work (file I/O, `ModuleDefMD.Load`) runs on background threads via `Task.Run`. UI updates go through `Dispatcher.BeginInvoke` (non-blocking).
- `DeepSearchService.GetTargets()` is an `IEnumerable` — it is consumed inside `Task.Run` in `DeepSearchEngine`. Do not call it on the UI thread.
- MEF imports that may be absent use `[Import(AllowDefault = true)]` and are nullable.
- `SafeEnumeratePeFiles` in `DeepSearchService` must stay the folder enumeration method — it handles `UnauthorizedAccessException` per-directory without aborting the whole scan.

## Key shortcuts

| Action | Binding |
|---|---|
| Open Deep Search panel | `Ctrl + Alt + D` |
| Start search | `Enter` in search box |
| Cancel search | `Cancel` button |
| Navigate to result | Double-click row |

## Roadmap

See `ROADMAP.md` for the phased implementation plan.

## Changelog

See `CHANGELOG.md` for version history.
