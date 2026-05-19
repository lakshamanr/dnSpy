/*
    Copyright (C) 2024 dnSpy Contributors

    This file is part of dnSpy

    dnSpy is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    dnSpy is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with dnSpy.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using dnlib.DotNet;
using dnSpy.Contracts.Debugger;
using dnSpy.Contracts.Documents;
using dnSpy.DeepSearch.Core;

namespace dnSpy.DeepSearch.Services {
	[Export(typeof(IDeepSearchService))]
	sealed class DeepSearchService : IDeepSearchService {
		readonly IDsDocumentService _documentService;
		readonly DeepSearchEngine _engine;
		readonly DbgManager? _dbgManager;

		public bool IsSearching => _engine.IsRunning;

		public event EventHandler<DeepSearchGroupFoundEventArgs>? GroupFound;
		public event EventHandler<DeepSearchCompletedEventArgs>? SearchCompleted;
		public event EventHandler<string>? StatusChanged;

		[ImportingConstructor]
		public DeepSearchService(IDsDocumentService documentService, [Import(AllowDefault = true)] DbgManager? dbgManager = null) {
			_documentService = documentService;
			_dbgManager      = dbgManager;
			_engine = new DeepSearchEngine();
			_engine.GroupFound      += (s, e) => GroupFound?.Invoke(this, e);
			_engine.SearchCompleted += (s, e) => SearchCompleted?.Invoke(this, e);
			_engine.StatusChanged   += (s, e) => StatusChanged?.Invoke(this, e);
		}

		public void StartSearch(DeepSearchOptions options) {
			if (_engine.IsRunning)
				return;
			_engine.Start(CollectTargets(options), options);
		}

		public void CancelSearch() => _engine.Cancel();

		IEnumerable<(ModuleDef module, string path, string name)> CollectTargets(DeepSearchOptions options) {
			var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			// ── Loaded assemblies ─────────────────────────────────────────────
			if (options.Source == DllSource.LoadedAssemblies || options.Source == DllSource.Both) {
				foreach (var doc in _documentService.GetDocuments()) {
					var mod = doc.ModuleDef;
					if (mod is null) continue;
					var path = doc.Filename ?? string.Empty;
					if (!string.IsNullOrEmpty(path) && !seen.Add(path))
						continue;
					var displayName = string.IsNullOrEmpty(path)
						? (mod.Assembly?.Name.String ?? mod.Name.String ?? "<unknown>")
						: Path.GetFileName(path);
					yield return (mod, path, displayName);
				}
			}

			// ── Attached debug process ────────────────────────────────────────
			if (options.Source == DllSource.AttachedProcess) {
				foreach (var item in GetAttachedProcessModules(seen))
					yield return item;
			}

			// ── Folder (plain PE files) ───────────────────────────────────────
			if (options.Source == DllSource.Folder || options.Source == DllSource.Both) {
				if (!string.IsNullOrWhiteSpace(options.FolderPath) && Directory.Exists(options.FolderPath)) {
					bool recurse = options.SearchSubfolders;
					Debug.WriteLine($"[DeepSearch] Enumerating folder: {options.FolderPath}, recurse={recurse}");

					foreach (var file in FolderScanner.SafeEnumeratePeFiles(options.FolderPath, recurse)) {
						if (!seen.Add(file)) continue;
						ModuleDef? mod = null;
						try {
							mod = ModuleDefMD.Load(file, new ModuleCreationOptions { TryToLoadPdbFromDisk = false });
						}
						catch {
							Debug.WriteLine($"[DeepSearch] Skipped non-.NET file: {file}");
							continue;
						}
						yield return (mod, file, Path.GetFileName(file));
					}

					// ── NuGet / zip archives ──────────────────────────────────
					foreach (var (bytes, virtualPath, displayName) in FolderScanner.EnumerateZipPeEntries(options.FolderPath, recurse)) {
						if (!seen.Add(virtualPath)) continue;
						ModuleDef? mod = null;
						try {
							mod = ModuleDefMD.Load(bytes, new ModuleCreationOptions { TryToLoadPdbFromDisk = false });
						}
						catch {
							Debug.WriteLine($"[DeepSearch] Skipped non-.NET zip entry: {virtualPath}");
							continue;
						}
						yield return (mod, virtualPath, displayName);
					}
				}
			}
		}

		IEnumerable<(ModuleDef module, string path, string name)> GetAttachedProcessModules(HashSet<string> seen) {
			var dbgMgr = _dbgManager;
			if (dbgMgr is null) {
				Debug.WriteLine("[DeepSearch] No debugger active — AttachedProcess source yielded nothing.");
				yield break;
			}

			int skippedDynamic = 0;
			foreach (var process in dbgMgr.Processes) {
				foreach (var runtime in process.Runtimes) {
					foreach (var module in runtime.Modules) {
						if (module.IsDynamic || module.IsInMemory) {
							skippedDynamic++;
							continue;
						}
						var filename = module.Filename;
						if (string.IsNullOrEmpty(filename)) continue;
						if (!seen.Add(filename)) continue;
						ModuleDef? mod = null;
						try {
							mod = ModuleDefMD.Load(filename, new ModuleCreationOptions { TryToLoadPdbFromDisk = false });
						}
						catch {
							Debug.WriteLine($"[DeepSearch] Skipped process module: {filename}");
							continue;
						}
						yield return (mod, filename, Path.GetFileName(filename));
					}
				}
			}

			if (skippedDynamic > 0)
				Debug.WriteLine($"[DeepSearch] Skipped {skippedDynamic} dynamic/in-memory process module(s) — live metadata access not supported.");
		}
	}
}
