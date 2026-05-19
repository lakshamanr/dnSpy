# Deep Search — User Guide

Deep Search is a dnSpy extension that lets you search **any .NET assembly for types, methods, fields, string literals, custom attributes, IL opcodes, and more** — without having to decompile or load the assembly first.

---

## Table of contents

1. [Opening the panel](#1-opening-the-panel)
2. [Your first search](#2-your-first-search)
3. [Search term and history](#3-search-term-and-history)
4. [Type filter](#4-type-filter)
5. [Match modes](#5-match-modes)
6. [Scope — what to search for](#6-scope--what-to-search-for)
7. [IL Opcode filter](#7-il-opcode-filter)
8. [Source — which assemblies to scan](#8-source--which-assemblies-to-scan)
9. [Namespace filter](#9-namespace-filter)
10. [Reading results](#10-reading-results)
11. [Navigating to a result](#11-navigating-to-a-result)
12. [Right-click context menu](#12-right-click-context-menu)
13. [Find Callers — cross-reference](#13-find-callers--cross-reference)
14. [Export results](#14-export-results)
15. [Snapshot and Diff](#15-snapshot-and-diff)
16. [Progress and status bar](#16-progress-and-status-bar)
17. [Tips and performance](#17-tips-and-performance)

---

## 1. Opening the panel

| Method | Steps |
|---|---|
| **Keyboard** | Press `Ctrl + Alt + D` |
| **Menu** | Click **View** → **Deep Search** |

The panel docks in the same row as dnSpy's built-in Search panel.

---

## 2. Your first search

1. Open the panel (`Ctrl + Alt + D`).
2. Type a class or method name in the search box.
3. Press **Enter** or click **Search**.
4. Results stream in as each assembly is scanned. Double-click any row to jump to it.

---

## 3. Search term and history

- The search box is a **combo box** — click the arrow on the right to drop down your last 20 search terms.
- Your history is **saved across sessions** (survives restarting dnSpy).
- All options (scope, mode, source, folder path) are also saved automatically.

---

## 4. Type filter

The **Type filter** box sits below the search box. It is optional and only affects **Methods**.

When filled, a method result is only shown if its **return type or at least one parameter type** contains the filter text.

| Example | Effect |
|---|---|
| Type filter = `HttpClient` | Only methods that accept or return `HttpClient` |
| Type filter = `Task` | Only async methods (return `Task` or `Task<T>`) |
| Type filter = `Stream` | Methods that read/write streams |
| *(empty)* | All methods are shown (no filtering) |

This is useful when the search term matches many methods but you only care about a specific signature.

---

## 5. Match modes

Select one radio button in the **Match** row.

### Substring *(default)*

The pattern appears anywhere in the name. Case-insensitive unless **Case-sensitive** is ticked.

| Pattern | Matches | Does not match |
|---|---|---|
| `Service` | `UserService`, `ServiceBase`, `IService` | `Controller` |
| `get` | `GetUser`, `getById`, `Target` | `Post` |

### Wildcard

`*` matches any number of characters. `?` matches exactly one character.

| Pattern | Matches | Does not match |
|---|---|---|
| `Get*By*` | `GetUserById`, `GetOrderByDate` | `SetUserById` |
| `I*Service` | `IUserService`, `IOrderService` | `UserService` |
| `?ndex` | `Index`, `index` | `OnIndex` |

### Regex

Full **.NET regular expression**. Case-insensitive by default.

| Pattern | What it finds |
|---|---|
| `^Get\w+ById$` | Any method starting with `Get`, ending with `ById` |
| `^I[A-Z]` | Interfaces (names starting with capital I then another capital) |
| `(Service\|Repository)$` | Names ending in `Service` or `Repository` |

> If your regex is invalid it silently matches nothing — no error dialog.

### Case-sensitive

Tick this checkbox to make any of the three modes respect letter case.

---

## 6. Scope — what to search for

Each checkbox controls a category of members to include in the search. You can combine any of them.

| Checkbox | Searches | Result label |
|---|---|---|
| **Types** | Class, struct, interface, enum, delegate names | `T` |
| **Methods** | Method, constructor, accessor, operator names | `M` |
| **Fields** | Field, property, and event names | `F` / `P` / `E` |
| **Strings** | String literals inside method bodies (`ldstr` IL) | `S` |
| **Attributes** | Custom attributes on types and methods | `A` |
| **Inheritance** | Types by their base class or implemented interface | `T` |
| **IL Opcodes** | Methods containing a specific IL instruction | `I` |

### Attributes scope

Find all types or methods decorated with a specific attribute.

- Pattern `Obsolete` → finds every `[Obsolete]` usage
- Pattern `DllImport` → finds every P/Invoke declaration
- Pattern `Authorize` → finds every ASP.NET `[Authorize]` endpoint
- Pattern `Serializable` → finds all serializable classes

The matched result shows the attribute name and the type or method it is on.

### Inheritance scope

Find types by what they extend or implement — without knowing the exact type name.

- Pattern `Controller` → all classes that extend `Controller` or `ControllerBase`
- Pattern `IDisposable` → all types that implement `IDisposable`
- Pattern `Exception` → all custom exception classes

### IL Opcodes scope

Find methods that contain a specific IL instruction. Requires the **Opcode** field to be filled (see [Section 7](#7-il-opcode-filter)).

Common uses:

| What you want to find | Opcode to enter |
|---|---|
| All P/Invoke call sites | `call` |
| All array allocations | `newarr` |
| All `throw` statements | `throw` |
| All static field reads | `ldsfld` |
| All `new` object allocations | `newobj` |

> **Performance:** Strings, Attributes, Inheritance, and IL Opcodes all require reading method bodies. On folders with hundreds of DLLs, combine these scopes with a specific pattern to keep searches fast.

---

## 7. IL Opcode filter

This text box appears below the scope checkboxes **only when IL Opcodes is ticked**.

Type the exact opcode name (case-insensitive):

```
ldsfld
call
callvirt
newobj
throw
newarr
stfld
box
```

The search term still applies — it filters which **methods** appear in results. Leave the search term as `*` (wildcard) to match all methods.

**Example — find every method that throws:**

1. Scope: tick **IL Opcodes**, untick everything else
2. Match mode: **Wildcard**
3. Search term: `*`
4. Opcode: `throw`
5. Click Search

---

## 8. Source — which assemblies to scan

Select one of the four radio buttons in the **Source** row.

### Loaded assemblies

Scans every assembly currently open in the dnSpy document tree. Fastest option — no file I/O beyond what is already loaded.

### Folder

Scans all `.dll` and `.exe` files in the chosen directory.

- Click **Browse…** to pick a folder.
- Tick **Subfolders** to recurse into subdirectories.
- Assemblies that are not valid .NET PEs are silently skipped.

**ZIP and NuGet packages** — Deep Search also scans `.zip` and `.nupkg` files found in the folder. Each .NET DLL inside the archive is loaded from its raw bytes (no extraction to disk). This lets you audit a NuGet package without unpacking it manually.

### Both

Union of *Loaded assemblies* and *Folder*. Duplicate files are automatically deduplicated.

### Attached process

Scans modules currently loaded in the **active debug session** (requires dnSpy's debugger to be running and attached to a process). Useful for searching the exact set of DLLs your target application has loaded.

> Dynamic and in-memory modules (e.g. generated by `AssemblyBuilder`) are skipped in this mode since live metadata access is not yet supported.

---

## 9. Namespace filter

The **NS filter** box sits above the results tree, next to the Export/Snapshot/Diff buttons.

It is a **live, client-side filter** — it narrows what is displayed without running a new search.

| Example | Effect |
|---|---|
| `MyApp.Services` | Only shows results in that exact namespace |
| `Controller` | Shows all results whose namespace contains "Controller" |
| *(empty)* | All results are shown |

The filter is applied to the result's **full location** (`Namespace.ContainingType`).

---

## 10. Reading results

Results are grouped by assembly in a collapsible tree:

```
▼ MyApp.Services.dll  (4 matches)
    [T]  UserService              MyApp.Services
    [M]  GetUserById              MyApp.Services.UserService
    [A]  [Authorize]  on  Index   MyApp.Controllers
    [S]  "hello world"  in  Init  MyApp.Services.UserService

▼ ThirdParty.dll  (1 match)
    [F]  _retryCount              ThirdParty.Core.Processor
```

### Result kind labels

| Label | What was found |
|---|---|
| `T` | Type / class / struct / interface |
| `M` | Method (including constructors and operators) |
| `F` | Field |
| `P` | Property |
| `E` | Event |
| `S` | String literal (`"..."` inside a method body) |
| `A` | Custom attribute (`[Obsolete]`, `[DllImport]`, etc.) |
| `I` | IL instruction (opcode match inside a method body) |

### What the columns mean

```
[M]  GetUserById              MyApp.Services.UserService
 │       │                          │
kind   name               namespace.containing-type
```

For string literals the name column shows the literal value and the method it is in:
```
[S]  "hello world"  in  Init()   MyApp.Services.UserService
```

For attribute results:
```
[A]  [Authorize]  on  Index   MyApp.Controllers.HomeController
```

---

## 11. Navigating to a result

**Double-click** a result row (or select it and press **Enter**) to:

1. Load the assembly into the dnSpy document tree if not already open.
2. Decompile and open the containing type in a new editor tab.
3. Move the caret to the matching member.  
   For string literal hits, the caret lands on the exact `ldstr` IL instruction.

Clicking a **group header** (the assembly row) does not navigate — only leaf result rows navigate.

---

## 12. Right-click context menu

Right-clicking any result row opens a context menu:

| Item | What it copies / does |
|---|---|
| **Copy Name** | Copies `Namespace.ContainingType.MemberName` to the clipboard |
| **Copy Assembly Path** | Copies the full file path of the assembly |
| **Copy All Results** | Copies every visible result as formatted plain text |
| **Find Callers** | Starts a cross-reference search (see below) |

> **Copy All Results** respects the current NS filter — only what is visible is copied.

---

## 13. Find Callers — cross-reference

Find Callers lets you answer *"which methods call this method?"* across all source assemblies.

**How to use it:**

1. Run a normal search and find a `[M]` method result.
2. Right-click the method → **Find Callers**.
3. Deep Search restarts and scans the same source (loaded / folder / both / attached) for `call`, `callvirt`, and `newobj` IL instructions whose operand matches the selected method's full signature.
4. Results are the **calling methods** — each one double-clicks to the call site.

**Example workflow:**

> You found `UserService.GetUserById`. You want to know what calls it.

1. Right-click `[M] GetUserById` → Find Callers
2. Deep Search scans all assemblies
3. Results show every method that calls `GetUserById`
4. Double-click any result to jump to that call site

> Find Callers uses the currently selected **Source** option, so it searches the same set of assemblies as your last search.

---

## 14. Export results

Click **Export…** (toolbar above the results tree) after a search completes.

A **Save File dialog** opens. Two formats are available:

### CSV (`.csv`)

```
Kind,Name,Namespace,ContainingType,AssemblyName,AssemblyPath
"M","GetUserById","MyApp.Services","UserService","MyApp.Services.dll","C:\...\MyApp.Services.dll"
"S","hello world","MyApp.Services","UserService","MyApp.Services.dll","C:\...\MyApp.Services.dll"
```

Import into Excel or any spreadsheet tool for further analysis.

### Plain text (`.txt`)

```
MyApp.Services.dll  (2 matches)
  [M]  GetUserById  (MyApp.Services.UserService)
  [S]  "hello world"  in  Init()  (MyApp.Services.UserService)

ThirdParty.dll  (1 match)
  [F]  _retryCount  (ThirdParty.Core.Processor)
```

Good for sharing with teammates or pasting into bug reports.

> The **NS filter** does **not** affect exports — the full unfiltered result set is always exported.

---

## 15. Snapshot and Diff

Snapshot + Diff lets you compare two search runs — for example, before and after patching an assembly.

### Workflow

**Step 1 — Run a baseline search**

Search in the original (unmodified) assemblies. When the results appear, click **Snapshot**.

The status bar confirms: *"Snapshot saved — N result(s)."*

**Step 2 — Run a second search**

Load the patched assemblies (or switch to the folder with the new build) and run the same search again.

**Step 3 — Click Diff**

Only results that are **new since the snapshot** are shown. Group headers are prefixed with `[NEW]`.

**Step 4 — Click Diff again to toggle off**

Returns to the full result view.

### What Diff shows

| Scenario | Shown in Diff mode |
|---|---|
| Result exists in both runs | Hidden (unchanged) |
| Result is new (not in snapshot) | Shown with `[NEW]` prefix |
| Result was removed (in snapshot, not in current) | Not shown in this version |

### Typical uses

- Compare two builds: search in v1 → snapshot → search in v2 → diff
- Find methods added by a patch
- Verify that a class was removed after refactoring

---

## 16. Progress and status bar

### Status bar (bottom of panel)

During a search:
```
Scanning: MyAssembly.dll  (12 of 47)  —  23 result(s)         [ ████░░░░ ]
```

After completion:
```
Search complete — 23 result(s) in 5 assemblies           23 result(s) in 5 assemblies
```

The **right side count** stays visible at all times so you can glance at the total without reading the full status message.

### Progress bar

- **Spinning** — target collection phase (reading folder / enumerating process modules)
- **Filling** — scanning phase; fills as each assembly is completed

---

## 17. Tips and performance

### Speed tips

| Situation | Recommendation |
|---|---|
| Searching a large folder | Tick only the scopes you need; untick **Strings** and **IL Opcodes** unless necessary |
| Searching System32 or Windows SDK folders | Results appear even if some subdirectories are inaccessible — they are silently skipped |
| Searching a NuGet cache | Use **Folder** source, point to the `.nuget\packages` directory, tick **Subfolders** |
| Only care about one namespace | Use **NS filter** after searching instead of re-running with a narrower term |

### Useful search patterns

| Goal | Search term | Scope | Notes |
|---|---|---|---|
| All controller classes | `Controller` | Types | Common base class suffix |
| All async methods | `*Async` | Methods, Wildcard | .NET naming convention |
| All hardcoded URLs | `http` | Strings | Finds connection strings, endpoints |
| All obsolete APIs | `Obsolete` | Attributes | Finds `[Obsolete]` everywhere |
| All classes extending Exception | `Exception` | Inheritance | Custom exception classes |
| All DllImport declarations | `DllImport` | Attributes | P/Invoke audit |
| All throw sites | `*` | IL Opcodes, opcode=throw | Find all exception throw points |
| All static field reads | `*` | IL Opcodes, opcode=ldsfld | Find global state access |
| All callers of a specific method | (find it first, then right-click) | — | Use **Find Callers** |

### Keyboard shortcuts summary

| Key | Action |
|---|---|
| `Ctrl + Alt + D` | Open Deep Search panel |
| `Enter` (in search box) | Start search |
| `Enter` (on result row) | Navigate to result |
| `Double-click` (result row) | Navigate to result |

---

*Deep Search is an open-source extension for [dnSpy](https://github.com/dnSpy/dnSpy). Report issues or suggestions on the project's GitHub page.*
