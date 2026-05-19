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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using dnlib.DotNet;

namespace dnSpy.DeepSearch.Core {
	public sealed class DeepSearchGroupFoundEventArgs : EventArgs {
		public DeepSearchResultGroup Group { get; }
		public int CurrentDllIndex { get; }
		public int TotalDllCount { get; }
		public string CurrentDllPath { get; }

		public DeepSearchGroupFoundEventArgs(DeepSearchResultGroup group, int currentIndex, int total, string path) {
			Group = group;
			CurrentDllIndex = currentIndex;
			TotalDllCount = total;
			CurrentDllPath = path;
		}
	}

	public sealed class DeepSearchCompletedEventArgs : EventArgs {
		public bool WasCancelled { get; }
		public int TotalResults { get; }

		public DeepSearchCompletedEventArgs(bool wasCancelled, int totalResults) {
			WasCancelled = wasCancelled;
			TotalResults = totalResults;
		}
	}

	/// <summary>
	/// Orchestrates scanning multiple assemblies sequentially on a background thread.
	/// Results stream in via <see cref="GroupFound"/> as each assembly finishes.
	/// </summary>
	public sealed class DeepSearchEngine {
		public event EventHandler<DeepSearchGroupFoundEventArgs>? GroupFound;
		public event EventHandler<DeepSearchCompletedEventArgs>? SearchCompleted;

		// Fires with the status line text (current DLL name + progress fraction)
		public event EventHandler<string>? StatusChanged;

		CancellationTokenSource? _cts;

		public bool IsRunning => _cts != null;

		/// <summary>
		/// Starts a background search. No-ops if a search is already running.
		/// </summary>
		/// <param name="targets">Sequence of (module, filePath, displayName) tuples. Enumerated lazily on the background thread.</param>
		/// <param name="options">Search configuration.</param>
		public void Start(IEnumerable<(ModuleDef module, string path, string name)> targets, DeepSearchOptions options) {
			if (_cts != null)
				return;

			_cts = new CancellationTokenSource();
			var token = _cts.Token;

			// FIX: Enumerate targets (file I/O + ModuleDefMD.Load) on the background thread,
			// not the UI thread, to prevent the app from hanging during folder scans.
			Task.Run(() => {
				Debug.WriteLine($"[DeepSearch] Background thread started. Source={options.Source}, Folder={options.FolderPath}");
				StatusChanged?.Invoke(this, "Collecting search targets…");

				List<(ModuleDef module, string path, string name)> targetList;
				try {
					targetList = new List<(ModuleDef module, string path, string name)>(targets);
				}
				catch (OperationCanceledException) {
					Debug.WriteLine("[DeepSearch] Collection cancelled.");
					_cts = null;
					SearchCompleted?.Invoke(this, new DeepSearchCompletedEventArgs(true, 0));
					return;
				}
				catch (Exception ex) {
					Debug.WriteLine($"[DeepSearch] Error collecting targets: {ex}");
					_cts = null;
					StatusChanged?.Invoke(this, $"Error collecting search targets: {ex.Message}");
					SearchCompleted?.Invoke(this, new DeepSearchCompletedEventArgs(false, 0));
					return;
				}

				Debug.WriteLine($"[DeepSearch] Collected {targetList.Count} targets.");
				RunSearch(targetList, options, token);
			}, token);
		}

		/// <summary>
		/// Cancels the running search. The next call to <see cref="Start"/> will begin fresh.
		/// </summary>
		public void Cancel() {
			_cts?.Cancel();
			_cts = null;
		}

		void RunSearch(List<(ModuleDef module, string path, string name)> targets, DeepSearchOptions options, CancellationToken token) {
			int totalResults = 0;
			bool cancelled = false;

			try {
				for (int i = 0; i < targets.Count; i++) {
					token.ThrowIfCancellationRequested();

					var (module, path, name) = targets[i];
					Debug.WriteLine($"[DeepSearch] Scanning [{i + 1}/{targets.Count}]: {name}");
					StatusChanged?.Invoke(this, $"Scanning: {name}  ({i + 1} of {targets.Count})");

					var results = new List<DeepSearchResult>();
					try {
						foreach (var result in AssemblyDeepSearcher.Search(module, path, name, options, token))
							results.Add(result);
					}
					catch (OperationCanceledException) {
						throw;
					}
					catch (Exception ex) {
						// Unreadable / obfuscated assembly — skip silently
						Debug.WriteLine($"[DeepSearch] Skipping {name}: {ex.GetType().Name}: {ex.Message}");
					}

					if (results.Count > 0) {
						totalResults += results.Count;
						var group = new DeepSearchResultGroup(path, name, results);
						GroupFound?.Invoke(this, new DeepSearchGroupFoundEventArgs(group, i + 1, targets.Count, path));
					}
				}
			}
			catch (OperationCanceledException) {
				cancelled = true;
				Debug.WriteLine("[DeepSearch] Search cancelled.");
			}
			finally {
				_cts = null;
				Debug.WriteLine($"[DeepSearch] Search finished. Cancelled={cancelled}, Results={totalResults}");
				SearchCompleted?.Invoke(this, new DeepSearchCompletedEventArgs(cancelled, totalResults));
			}
		}
	}
}
