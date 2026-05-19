# DeepSearch feature development prompt

You are helping implement or improve the **dnSpy.DeepSearch** extension, a custom plugin that searches .NET assemblies (DLL/EXE) for types, methods, fields, and string literals.

## Context loaded automatically

- Architecture: `Extensions/dnSpy.DeepSearch/` — see `CLAUDE.md` for the full layout.
- Feature backlog: `ROADMAP.md` — phased plan with implementation notes per feature.
- Changelog: `CHANGELOG.md` — what has already shipped.

## Task

$ARGUMENTS

## Rules to follow

1. **Core layer is WPF-free and MEF-free.** `Core/` files may only reference `dnlib` and the BCL. No `System.Windows`, no `[Import]`, no `Application.Current`.
2. **All file I/O and `ModuleDefMD.Load` calls run inside `Task.Run`.** Never call them on the UI thread.
3. **UI updates go through `Dispatcher.BeginInvoke`** (non-blocking), not `Invoke`.
4. **New scope flags** follow the pattern in `SearchScope` (`[Flags]` enum) + checkbox in XAML + VM bool + `BuildOptions()` + `AssemblyDeepSearcher.Search()` handler.
5. **New source modes** follow the pattern in `DllSource` + radio button + VM bool + `IsFolderVisible` update + `DeepSearchService.GetTargets()` branch.
6. **New result kinds** only need a `ResultKind` value + `KindLabel` case + scanning logic. The result tree is data-driven — no XAML template changes.
7. **Settings persistence** — use `IDeepSearchSettings` / `DeepSearchSettings` backed by `ISettingsSection`. Do not write to files directly.
8. **MEF optional imports** use `[Import(AllowDefault = true)]` and are nullable.
9. No comments unless the WHY is non-obvious. No docstrings on obvious members.
10. After implementing, update `CHANGELOG.md` under an `[Unreleased]` heading and note the feature in `ROADMAP.md` as done.

## Key files to read before implementing

| What you need | File |
|---|---|
| Scanning logic | `Core/AssemblyDeepSearcher.cs` |
| Options model | `Core/DeepSearchOptions.cs` |
| Result model | `Core/DeepSearchResult.cs` |
| Engine orchestration | `Core/DeepSearchEngine.cs` |
| Target collection | `Services/DeepSearchService.cs` |
| ViewModel | `UI/ViewModels/DeepSearchViewModel.cs` |
| XAML panel | `UI/DeepSearchControl.xaml` |

## Verification checklist

- [ ] Core layer compiles without WPF references
- [ ] New option round-trips through `BuildOptions()` → `DeepSearchOptions` → searcher
- [ ] Heavy work is not on the UI thread (no `Dispatcher.Invoke` wrapping file I/O)
- [ ] Cancellation token is checked inside any new scanning loop
- [ ] `CHANGELOG.md` updated
