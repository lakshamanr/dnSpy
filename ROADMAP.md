# DeepSearch — Phase-wise Implementation Roadmap

Each phase is self-contained and buildable. Later phases depend on earlier ones where noted.

---

## Phase 1 — Developer Quality-of-Life (low effort, high daily value)

**Goal:** Make the tool pleasant to use every day without changing search capability.

### 1.1 Persist search options
- Save/restore scope checkboxes (Types, Methods, Fields, Strings), match mode, case-sensitive, source radio, and subfolder toggle across sessions.
- Extend `IDeepSearchSettings` / `DeepSearchSettings` with `SaveOptions` / `LoadOptions` using `ISettingsSection` attributes (same pattern as history).
- Load in `DeepSearchViewModel` constructor; save on `StartSearch`.

### 1.2 Result count label
- Add a dedicated `TextBlock` below the results tree showing e.g. `3 results in 2 assemblies`.
- `TotalResults` and `ResultGroups.Count` are already tracked in the VM — just expose them in the UI.

### 1.3 Copy to clipboard (context menu)
- Right-click context menu on result rows with:
  - **Copy name** — `Namespace.Type.Member`
  - **Copy assembly path** — full file path
  - **Copy all results** — tab-separated dump of visible results
- Implement as `ContextMenu` in `DeepSearchControl.xaml` bound to VM commands.

### 1.4 Export results to file
- Add an **Export…** button (active after search completes) that opens a `SaveFileDialog` and writes results as plain text or CSV.
- Output columns: Kind, Name, Namespace, ContainingType, AssemblyName, AssemblyPath.

### 1.5 Progress bar
- Replace the status `TextBlock` with a `ProgressBar` + overlaid text.
- `DeepSearchGroupFoundEventArgs` already carries `CurrentDllIndex` and `TotalDllCount` — bind `ProgressBar.Value` to a new `ProgressVM` property.

---

## Phase 2 — Richer Search Capability

**Goal:** Let developers find things they currently can't, without restructuring the engine.

### 2.1 Namespace filter (client-side)
- Add a "Filter namespace" `TextBox` above the results tree.
- Filter is applied to the already-collected `ResultGroups` using a `CollectionViewSource` — no re-search needed.
- Useful when a broad search term matches things in many namespaces.

### 2.2 Custom attribute search (`[Attribute]` scope)
- New `SearchScope.Attributes` flag.
- In `AssemblyDeepSearcher.Search()`, walk `type.CustomAttributes` and `method.CustomAttributes`, match the attribute type name against the search term.
- New `ResultKind.Attribute` with label `A`.
- Lets you instantly find all `[Obsolete]`, `[DllImport]`, `[Serializable]` usages.

### 2.3 Return type / parameter type filter
- Add an optional second search box: "Type contains" (e.g. `HttpClient`).
- In `AssemblyDeepSearcher`, when this filter is set, match method return types (`method.ReturnType.TypeName`) and parameter types.
- New `DeepSearchOptions.TypeFilter` string property.

### 2.4 Dynamic/in-memory module support in AttachedProcess
- `GetAttachedProcessModules` currently skips `IsDynamic` and `IsInMemory` modules.
- For dynamic modules, use `dbgRuntime`'s reflection API to enumerate members directly (no file load needed).
- Requires understanding `DbgRuntime` / `DbgModule` dynamic-metadata APIs.

### 2.5 Base type / interface search
- New scope: **Inherits** — find all types that extend a given base class or implement a given interface.
- In `AssemblyDeepSearcher`, walk `type.BaseType?.Name` and `type.Interfaces`.
- Match against search term.

---

## Phase 3 — Advanced / Power-User Features

**Goal:** Make DeepSearch a first-class reverse-engineering analysis tool.

### 3.1 IL opcode search
- New `SearchScope.ILOpcodes` flag + a second drop-down to select the opcode (or type it).
- Walk all method bodies, match instructions by opcode (and optionally operand text).
- Useful for: finding all `PInvoke` call sites (`call` to extern), all `throw new X`, all `ldsfld` of a specific field.
- Result kind `ResultKind.ILInstruction` with label `I`.

### 3.2 Find all callers (cross-reference)
- Right-click a `[M]` result row → **Find callers** → re-runs a search over all loaded assemblies scanning `call` / `callvirt` operands for that method's token.
- Needs a reference to the target `MethodDef` (already stored as `IMDTokenProvider TokenProvider`).
- Opens results in the same panel (new search, same UI).

### 3.3 Result comparison (diff mode)
- **Snapshot** button saves the current result set.
- After a second search, a **Diff** toggle shows only: added results (green), removed results (red), unchanged (grey).
- Useful for comparing an original DLL against a patched one.

### 3.4 NuGet / zip package scanning
- When source = Folder and a `.nupkg` or `.zip` file is encountered, unzip to a temp directory, scan the DLLs inside, then delete the temp directory.
- Lets you audit a NuGet package without manual extraction.

### 3.5 Unit test project
- `Tests/dnSpy.DeepSearch.Tests` (referenced in `DEEP_SEARCH.md` but not yet created).
- Cover: `MatchHelper` (all modes + edge cases), `AssemblyDeepSearcher` (each result kind), `SafeEnumeratePeFiles` (permission errors), `DeepSearchEngine` cancellation.
- Target `net48` — no WPF or MEF dependency in tests.

---

## Implementation order recommendation

```
Phase 1 (1.1 → 1.5)  — complete before shipping to other developers
Phase 2.1             — namespace filter is low-risk and immediately useful
Phase 2.2             — attribute search is high-value for security analysis
Phase 3.5             — add tests before Phase 3 features to prevent regressions
Phase 2.3, 2.5        — type/inheritance filters
Phase 3.1, 3.2        — IL search and caller lookup (most complex)
Phase 2.4, 3.3, 3.4   — dynamic modules, diff mode, NuGet (nice-to-have)
```
