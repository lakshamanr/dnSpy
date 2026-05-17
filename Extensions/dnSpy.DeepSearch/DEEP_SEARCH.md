# Deep Search — Developer & User Guide

## What it does

Deep Search extends dnSpy's built-in search so it can scan **DLL and EXE files directly** — not only files that have already been decompiled and loaded into the document tree.

When you search for a class, method, field, or string literal, Deep Search:

1. Opens each assembly with dnlib (zero decompilation overhead for most searches).
2. Walks every type, method, field, property, event, and `ldstr` IL instruction.
3. Streams matching results into a collapsible tree as each DLL is finished.
4. Lets you double-click any result to auto-load the assembly and jump directly to the matching member.

---

## Opening the panel

| Method | Detail |
|---|---|
| **Keyboard** | `Ctrl + Shift + D` |
| **Menu** | View → Deep Search |

The panel docks in the horizontal tool window group (same row as the built-in Search panel).

---

## Search options

### Match mode

| Mode | Behaviour | Example pattern |
|---|---|---|
| **Substring** | The pattern appears anywhere in the name (case-insensitive by default) | `Service` matches `UserService`, `ServiceBase` |
| **Wildcard** | `*` = any characters, `?` = one character | `Get*By*` matches `GetUserById` |
| **Regex** | Full .NET regular expression | `^Get\w+ById$` |

Enable **Case-sensitive** to make any mode respect letter case.

### Scope

| Checkbox | What is searched |
|---|---|
| **Types** | Class, struct, interface, enum, delegate names |
| **Methods** | Method, constructor, accessor, operator names |
| **Fields** | Field, property, event names |
| **Strings** | String literals embedded inside method bodies (`ldstr` IL opcode) |

> **Performance note:** enabling *Strings* requires visiting every IL instruction in every method body. On a folder with hundreds of DLLs this is the slowest scope — consider combining it with a narrow search term or searching loaded assemblies only.

### Source

| Option | Which DLLs are scanned |
|---|---|
| **Loaded assemblies** | Every assembly currently open in the dnSpy document tree |
| **Folder** | All `.dll` and `.exe` files in the chosen folder |
| **Both** | Union of the above (duplicates are skipped automatically) |

When *Folder* or *Both* is selected, a **Browse…** button appears. Tick **Subfolders** to recurse into subdirectories.

---

## Reading results

Results are displayed in a collapsible tree:

```
▼ MyAssembly.dll  (3 matches)
    [T]  MyService                   MyApp.Services
    [M]  GetUserById()               MyApp.Services.MyService
    [S]  "hello world"  in  Init()   MyApp.Services.MyService
▼ AnotherLib.dll  (1 match)
    [F]  _retryCount                 AnotherLib.Core.Processor
```

**Kind labels:**

| Label | Member kind |
|---|---|
| `T` | Type / class |
| `M` | Method |
| `F` | Field |
| `P` | Property |
| `E` | Event |
| `S` | String literal |

---

## Navigating to a result

**Double-click** a result row (or press **Enter** when a row is focused) to:

1. Load the assembly into the document tree if it is not already loaded.
2. Decompile and open the containing type in a new tab.
3. Move the caret to the matching member — or, for string literal hits, to the exact `ldstr` IL instruction.

---

## Search history

The search box remembers your last 20 search terms. Click the dropdown arrow to revisit a previous term.

---

## Cancelling a search

Click **Cancel** while a search is running. The results collected so far remain visible. Starting a new search always begins from scratch.

---

## Developer guide

### Project layout

```
Extensions/dnSpy.DeepSearch/
├── Core/                       # Pure logic — no WPF, no MEF
│   ├── DeepSearchOptions.cs    # Options model
│   ├── DeepSearchResult.cs     # Single match model
│   ├── DeepSearchResultGroup.cs
│   ├── MatchHelper.cs          # Substring / wildcard / regex
│   ├── AssemblyDeepSearcher.cs # Scans one ModuleDef, yields results
│   └── DeepSearchEngine.cs     # Orchestrates N assemblies on a Task
├── Services/
│   ├── IDeepSearchService.cs   # MEF service interface
│   └── DeepSearchService.cs    # Collects targets, bridges engine ↔ UI
├── UI/
│   ├── DeepSearchToolWindowContent.cs  # MEF tool window provider
│   ├── DeepSearchControl.xaml          # Main XAML
│   ├── DeepSearchControl.xaml.cs       # Code-behind (navigation events)
│   └── ViewModels/
│       ├── DeepSearchViewModel.cs       # Root VM
│       ├── DeepSearchResultGroupVM.cs   # DLL group VM
│       └── DeepSearchResultVM.cs        # Single result VM
└── Plugin/
    └── DeepSearchPlugin.cs     # [ExportAutoLoaded] — keyboard shortcut, menu item
```

### Adding a new result kind

1. Add a value to `ResultKind` in `DeepSearchResult.cs`.
2. Update `KindLabel` in the same file.
3. Add the scanning logic to `AssemblyDeepSearcher.Search()`.
4. No changes to the UI are required — the tree template is data-driven.

### Adding a new scope flag

1. Add a flag to `SearchScope` in `DeepSearchOptions.cs`.
2. Add the corresponding `CheckBox` to `DeepSearchControl.xaml` bound to a new VM property.
3. Update `DeepSearchViewModel.BuildOptions()` to include the flag.
4. Handle it in `AssemblyDeepSearcher.Search()`.

### Running unit tests

```
cd Tests/dnSpy.DeepSearch.Tests
dotnet test
```

Tests compile against .NET 4.8 and cover `MatchHelper`, `AssemblyDeepSearcher`, and `DeepSearchEngine` without any WPF or MEF dependency.
