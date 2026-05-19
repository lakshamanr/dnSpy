# Changelog

All notable changes to the dnSpy.DeepSearch extension are documented here.

---

## [0.4.0] — 2026-05-19

### Added
- **Persist search options** — scope checkboxes, match mode, case-sensitive, source, folder path, and subfolder toggle are saved across sessions via `IDeepSearchSettings.SaveOptions/LoadOptions`.
- **Result count label** — `ResultSummary` shown in the status footer: `N result(s) in M assembl(ies)` after every search.
- **Copy to clipboard** (context menu) — right-click any result row: Copy Name (`Namespace.Type.Member`), Copy Assembly Path, Copy All Results (formatted text).
- **Export results** — Export… button writes all results as CSV or plain text (UTF-8) via `SaveFileDialog`.
- **Namespace filter** — NS filter TextBox above the tree; filters displayed results by namespace on the client side without re-running the search.
- **Custom attribute search** — `SearchScope.Attributes` checkbox; finds types/methods decorated with a specific attribute (e.g. `[Obsolete]`, `[DllImport]`). Result kind `A`.
- **Return type / parameter type filter** — Type filter TextBox narrows Methods results to those whose return type or any parameter type contains the filter string.
- **Determinate progress bar** — switches from indeterminate (target collection) to value-bound once the total assembly count is known.
- **Base type / interface search** — `SearchScope.Inheritance` checkbox; finds types by base class name or implemented interface name.
- **Find callers** (cross-reference) — right-click a `[M]` result → Find Callers; scans `call`/`callvirt`/`newobj` operands across all source assemblies for the selected method's full name.
- **IL opcode search** — `SearchScope.IL Opcodes` checkbox + Opcode TextBox; finds all methods containing a specific IL instruction (e.g. `ldsfld`, `call`, `newobj`). Result kind `I`.
- **Result diff / snapshot** — Snapshot button saves current result keys; Diff button shows only results added since the snapshot (prefixed `[NEW]`).
- **NuGet / zip archive scanning** — `FolderScanner.EnumerateZipPeEntries` reads `.nupkg` and `.zip` files; DLLs are loaded from raw bytes (no temp files created).
- **Unit tests** — `NewFeaturesTests.cs` covers all new scopes (Attributes, Inheritance, ILOpcodes, TypeFilter, DiffKey stability) and `FolderScanner.SafeEnumeratePeFiles`.
- `ResultKind.Attribute` (label `A`) and `ResultKind.ILInstruction` (label `I`) added.
- `DeepSearchResult.DiffKey` — stable composite key used for snapshot comparison.
- `FolderScanner.cs` — extracted file-system helpers from `DeepSearchService` so the test project can link them without WPF/MEF dependencies.

### Fixed
- `DeepSearchEngineTests.StatusEvents_FireForEachAssembly` updated to account for the "Collecting…" status message that precedes per-assembly scanning messages.

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
