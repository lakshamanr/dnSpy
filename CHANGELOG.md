# Changelog

All notable changes to the dnSpy.DeepSearch extension are documented here.

---

## [0.3.0] — 2026-05-19

### Added
- **Attached-process source mode** — new `DllSource.AttachedProcess` option searches all non-dynamic, non-in-memory modules currently loaded in the active debug session via `DbgManager`. Appears as an "Attached process" radio button in the UI.
- `DbgManager` optional MEF import in `DeepSearchService` (`[Import(AllowDefault = true)]`) so the extension loads correctly whether or not a debugger session is active.
- Project reference to `dnSpy.Contracts.Debugger.csproj` in the extension csproj.
- `Debug.WriteLine` tracing throughout `DeepSearchEngine` and `DeepSearchService` for easier diagnosis in Debug builds.

### Fixed
- **UI thread hang** — target collection (file I/O + `ModuleDefMD.Load`) was running on the UI thread during folder scans, freezing the application. Entire target materialization is now inside `Task.Run`.
- **`UnauthorizedAccessException` on System32/protected folders** — replaced `Directory.EnumerateFiles(..., AllDirectories)` with `SafeEnumeratePeFiles`, which recurses manually and swallows per-directory access errors rather than aborting the whole scan.
- **In-memory module deduplication bug** — modules with an empty path (dynamic/in-memory assemblies) were all collapsed to a single entry in the `seen` `HashSet`. Empty-path modules are now deduplicated by name instead.
- **Background thread stall on status updates** — `OnStatusChanged` used `Dispatcher.Invoke` (blocking), which stalled the search thread on every status-line update. Switched to `Dispatcher.BeginInvoke`.

---

## [0.2.0] — 2026-05-17

### Added
- **Persistent search history** — last 20 search terms are saved to the dnSpy settings file via `ISettingsService` and restored on next launch. Implemented in `DeepSearchSettings`.
- **release.ps1** — automated build and packaging script: restores git submodules, runs `build.ps1 -Release`, and zips the output directory.
- dnSpy.DeepSearch added to `dnSpy.sln` so it is included in solution-wide builds and IDE navigation.

### Fixed
- **`XamlParseException` on startup** — DeepSearch WPF styles were defined at the wrong resource scope. Moved to `UserControl.Resources` in `DeepSearchControl.xaml`.
- `release.ps1` false exit-code 1 caused by submodule restore output being misinterpreted as an error; suppressed with `$LASTEXITCODE` reset.

---

## [0.1.0] — 2026-05-17

### Added
- **Initial DeepSearch extension** — searches `.dll` and `.exe` files for types, methods, fields, properties, events, and string literals without needing to decompile first.
- Three match modes: Substring (default), Wildcard (`*`/`?`), Regex.
- Case-sensitive toggle.
- Four scope flags: Types, Methods, Fields, Strings.
- Three source modes: Loaded assemblies, Folder (with subfolder toggle), Both.
- Results stream into a collapsible tree grouped by assembly as each DLL finishes scanning.
- Double-click navigation: loads the assembly and jumps to the matched member in a new tab.
- `Ctrl + Alt + D` keyboard shortcut and View menu item.
- Cancellation support — results so far remain visible after cancel.
- Search term history dropdown (in-memory, 20 items).
