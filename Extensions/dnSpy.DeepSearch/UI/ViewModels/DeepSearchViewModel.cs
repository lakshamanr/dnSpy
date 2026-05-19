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
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using dnlib.DotNet;
using dnSpy.Contracts.MVVM;
using dnSpy.DeepSearch.Core;
using dnSpy.DeepSearch.Services;
using Microsoft.Win32;

namespace dnSpy.DeepSearch.UI.ViewModels {
	public sealed class DeepSearchViewModel : ViewModelBase {
		readonly IDeepSearchService _searchService;
		readonly IPickDirectory _pickDirectory;
		readonly IDeepSearchSettings _settings;

		const int MaxHistoryItems = 20;

		// ── Search term + history ────────────────────────────────────────────
		public ObservableCollection<string> SearchHistory { get; } = new();

		string _searchTerm = string.Empty;
		public string SearchTerm {
			get => _searchTerm;
			set { if (_searchTerm != value) { _searchTerm = value; OnPropertyChanged(nameof(SearchTerm)); } }
		}

		// ── Type filter (narrows Methods by return/parameter type) ───────────
		string _typeFilter = string.Empty;
		public string TypeFilter {
			get => _typeFilter;
			set { if (_typeFilter != value) { _typeFilter = value; OnPropertyChanged(nameof(TypeFilter)); } }
		}

		// ── Match mode ───────────────────────────────────────────────────────
		bool _isSubstring = true;
		public bool IsSubstring {
			get => _isSubstring;
			set { if (_isSubstring != value) { _isSubstring = value; OnPropertyChanged(nameof(IsSubstring)); } }
		}

		bool _isWildcard;
		public bool IsWildcard {
			get => _isWildcard;
			set { if (_isWildcard != value) { _isWildcard = value; OnPropertyChanged(nameof(IsWildcard)); } }
		}

		bool _isRegex;
		public bool IsRegex {
			get => _isRegex;
			set { if (_isRegex != value) { _isRegex = value; OnPropertyChanged(nameof(IsRegex)); } }
		}

		bool _caseSensitive;
		public bool CaseSensitive {
			get => _caseSensitive;
			set { if (_caseSensitive != value) { _caseSensitive = value; OnPropertyChanged(nameof(CaseSensitive)); } }
		}

		// ── Scope ────────────────────────────────────────────────────────────
		bool _scopeTypes = true;
		public bool ScopeTypes {
			get => _scopeTypes;
			set { if (_scopeTypes != value) { _scopeTypes = value; OnPropertyChanged(nameof(ScopeTypes)); } }
		}

		bool _scopeMethods = true;
		public bool ScopeMethods {
			get => _scopeMethods;
			set { if (_scopeMethods != value) { _scopeMethods = value; OnPropertyChanged(nameof(ScopeMethods)); } }
		}

		bool _scopeFields = true;
		public bool ScopeFields {
			get => _scopeFields;
			set { if (_scopeFields != value) { _scopeFields = value; OnPropertyChanged(nameof(ScopeFields)); } }
		}

		bool _scopeStrings = true;
		public bool ScopeStrings {
			get => _scopeStrings;
			set { if (_scopeStrings != value) { _scopeStrings = value; OnPropertyChanged(nameof(ScopeStrings)); } }
		}

		bool _scopeAttributes;
		public bool ScopeAttributes {
			get => _scopeAttributes;
			set { if (_scopeAttributes != value) { _scopeAttributes = value; OnPropertyChanged(nameof(ScopeAttributes)); } }
		}

		bool _scopeInheritance;
		public bool ScopeInheritance {
			get => _scopeInheritance;
			set { if (_scopeInheritance != value) { _scopeInheritance = value; OnPropertyChanged(nameof(ScopeInheritance)); } }
		}

		bool _scopeILOpcodes;
		public bool ScopeILOpcodes {
			get => _scopeILOpcodes;
			set {
				if (_scopeILOpcodes != value) {
					_scopeILOpcodes = value;
					OnPropertyChanged(nameof(ScopeILOpcodes));
					OnPropertyChanged(nameof(IsOpcodeFilterVisible));
				}
			}
		}

		// ── IL opcode filter (visible only when ScopeILOpcodes is on) ────────
		string _opcodeFilter = string.Empty;
		public string OpcodeFilter {
			get => _opcodeFilter;
			set { if (_opcodeFilter != value) { _opcodeFilter = value; OnPropertyChanged(nameof(OpcodeFilter)); } }
		}

		public bool IsOpcodeFilterVisible => _scopeILOpcodes;

		// ── Source ───────────────────────────────────────────────────────────
		bool _sourceLoaded = true;
		public bool SourceLoaded {
			get => _sourceLoaded;
			set { if (_sourceLoaded != value) { _sourceLoaded = value; OnPropertyChanged(nameof(SourceLoaded)); OnPropertyChanged(nameof(IsFolderVisible)); } }
		}

		bool _sourceFolder;
		public bool SourceFolder {
			get => _sourceFolder;
			set { if (_sourceFolder != value) { _sourceFolder = value; OnPropertyChanged(nameof(SourceFolder)); OnPropertyChanged(nameof(IsFolderVisible)); } }
		}

		bool _sourceBoth;
		public bool SourceBoth {
			get => _sourceBoth;
			set { if (_sourceBoth != value) { _sourceBoth = value; OnPropertyChanged(nameof(SourceBoth)); OnPropertyChanged(nameof(IsFolderVisible)); } }
		}

		bool _sourceAttached;
		public bool SourceAttached {
			get => _sourceAttached;
			set { if (_sourceAttached != value) { _sourceAttached = value; OnPropertyChanged(nameof(SourceAttached)); OnPropertyChanged(nameof(IsFolderVisible)); } }
		}

		public bool IsFolderVisible => SourceFolder || SourceBoth;

		string _folderPath = string.Empty;
		public string FolderPath {
			get => _folderPath;
			set { if (_folderPath != value) { _folderPath = value; OnPropertyChanged(nameof(FolderPath)); } }
		}

		bool _searchSubfolders = true;
		public bool SearchSubfolders {
			get => _searchSubfolders;
			set { if (_searchSubfolders != value) { _searchSubfolders = value; OnPropertyChanged(nameof(SearchSubfolders)); } }
		}

		// ── Namespace filter (client-side, no re-search) ─────────────────────
		string _namespaceFilter = string.Empty;
		public string NamespaceFilter {
			get => _namespaceFilter;
			set {
				if (_namespaceFilter != value) {
					_namespaceFilter = value;
					OnPropertyChanged(nameof(NamespaceFilter));
					OnPropertyChanged(nameof(FilteredResultGroups));
					OnPropertyChanged(nameof(FilteredAssemblyCount));
				}
			}
		}

		// ── Results ──────────────────────────────────────────────────────────
		public ObservableCollection<DeepSearchResultGroupVM> ResultGroups { get; } = new();

		/// <summary>Namespace-filtered and diff-filtered view of ResultGroups, bound by the TreeView.</summary>
		public IEnumerable<DeepSearchResultGroupVM> FilteredResultGroups {
			get {
				IEnumerable<DeepSearchResultGroupVM> source = ResultGroups;

				// Apply namespace filter
				if (!string.IsNullOrWhiteSpace(_namespaceFilter)) {
					var term = _namespaceFilter.Trim();
					var nsFiltered = new List<DeepSearchResultGroupVM>();
					foreach (var group in source) {
						var matching = group.Results
							.Where(r => r.Location.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
							.ToList();
						if (matching.Count == 0) continue;
						var fg = new DeepSearchResultGroupVM(group.AssemblyName, group.AssemblyPath);
						foreach (var r in matching) fg.AddResult(r);
						nsFiltered.Add(fg);
					}
					source = nsFiltered;
				}

				// Apply diff mode — show only results not present in the snapshot
				if (_isDiffMode && _snapshotKeys != null) {
					var diffFiltered = new List<DeepSearchResultGroupVM>();
					foreach (var group in source) {
						var newResults = group.Results
							.Where(r => !_snapshotKeys.Contains(r.Result.DiffKey))
							.ToList();
						if (newResults.Count == 0) continue;
						var dg = new DeepSearchResultGroupVM($"[NEW] {group.AssemblyName}", group.AssemblyPath);
						foreach (var r in newResults) dg.AddResult(r);
						diffFiltered.Add(dg);
					}
					return diffFiltered;
				}

				return source;
			}
		}

		DeepSearchResultVM? _selectedResult;
		public DeepSearchResultVM? SelectedResult {
			get => _selectedResult;
			set { if (_selectedResult != value) { _selectedResult = value; OnPropertyChanged(nameof(SelectedResult)); } }
		}

		DeepSearchResultGroupVM? _selectedGroup;
		public DeepSearchResultGroupVM? SelectedGroup {
			get => _selectedGroup;
			set { if (_selectedGroup != value) { _selectedGroup = value; OnPropertyChanged(nameof(SelectedGroup)); } }
		}

		// ── Status & progress ────────────────────────────────────────────────
		string _statusText = "Ready";
		public string StatusText {
			get => _statusText;
			set { if (_statusText != value) { _statusText = value; OnPropertyChanged(nameof(StatusText)); } }
		}

		int _totalResults;
		public int TotalResults {
			get => _totalResults;
			set { if (_totalResults != value) { _totalResults = value; OnPropertyChanged(nameof(TotalResults)); OnPropertyChanged(nameof(ResultSummary)); OnPropertyChanged(nameof(CanExport)); } }
		}

		int _assemblyCount;
		public int AssemblyCount {
			get => _assemblyCount;
			set { if (_assemblyCount != value) { _assemblyCount = value; OnPropertyChanged(nameof(AssemblyCount)); OnPropertyChanged(nameof(ResultSummary)); } }
		}

		public int FilteredAssemblyCount => FilteredResultGroups.Count();

		int _progressValue;
		public int ProgressValue {
			get => _progressValue;
			set { if (_progressValue != value) { _progressValue = value; OnPropertyChanged(nameof(ProgressValue)); } }
		}

		int _progressMax;
		public int ProgressMax {
			get => _progressMax;
			set {
				if (_progressMax != value) {
					_progressMax = value;
					OnPropertyChanged(nameof(ProgressMax));
					OnPropertyChanged(nameof(IsProgressIndeterminate));
				}
			}
		}

		public bool IsProgressIndeterminate => _progressMax == 0;

		bool _isSearching;
		public bool IsSearching {
			get => _isSearching;
			set {
				if (_isSearching != value) {
					_isSearching = value;
					OnPropertyChanged(nameof(IsSearching));
					OnPropertyChanged(nameof(IsNotSearching));
					OnPropertyChanged(nameof(CanExport));
					OnPropertyChanged(nameof(ResultSummary));
				}
			}
		}

		public bool IsNotSearching => !_isSearching;
		public bool CanExport      => !_isSearching && _totalResults > 0;

		/// <summary>Stable one-line count displayed in the status footer when not searching.</summary>
		public string ResultSummary =>
			_isSearching || _totalResults == 0
				? string.Empty
				: $"{_totalResults} result(s) in {_assemblyCount} assembl{(_assemblyCount == 1 ? "y" : "ies")}";

		// ── Diff / snapshot ──────────────────────────────────────────────────
		HashSet<string>? _snapshotKeys;

		bool _isDiffMode;
		public bool IsDiffMode {
			get => _isDiffMode;
			set {
				if (_isDiffMode != value) {
					_isDiffMode = value;
					OnPropertyChanged(nameof(IsDiffMode));
					OnPropertyChanged(nameof(FilteredResultGroups));
					OnPropertyChanged(nameof(FilteredAssemblyCount));
				}
			}
		}

		// ── Commands ─────────────────────────────────────────────────────────
		public ICommand SearchCommand          { get; }
		public ICommand CancelCommand          { get; }
		public ICommand BrowseFolderCommand    { get; }
		public ICommand ExportCommand          { get; }
		public ICommand SnapshotCommand        { get; }
		public ICommand ToggleDiffCommand      { get; }
		public ICommand CopyNameCommand        { get; }
		public ICommand CopyPathCommand        { get; }
		public ICommand CopyAllResultsCommand  { get; }
		public ICommand FindCallersCommand     { get; }

		public DeepSearchViewModel(IDeepSearchService searchService, IPickDirectory pickDirectory, IDeepSearchSettings settings) {
			_searchService = searchService;
			_pickDirectory = pickDirectory;
			_settings      = settings;

			// Restore history
			foreach (var term in _settings.LoadHistory())
				SearchHistory.Add(term);

			// Restore persisted options
			ApplyOptions(_settings.LoadOptions());

			SearchCommand         = new RelayCommand(_ => StartSearch(),                             _ => IsNotSearching && !string.IsNullOrWhiteSpace(SearchTerm));
			CancelCommand         = new RelayCommand(_ => _searchService.CancelSearch(),             _ => IsSearching);
			BrowseFolderCommand   = new RelayCommand(_ => BrowseFolder());
			ExportCommand         = new RelayCommand(_ => ExportResults(),                           _ => CanExport);
			SnapshotCommand       = new RelayCommand(_ => TakeSnapshot());
			ToggleDiffCommand     = new RelayCommand(_ => ToggleDiff());
			CopyNameCommand       = new RelayCommand(_ => CopyName(),                                _ => _selectedResult != null);
			CopyPathCommand       = new RelayCommand(_ => CopyPath(),                                _ => _selectedResult != null || _selectedGroup != null);
			CopyAllResultsCommand = new RelayCommand(_ => CopyAllResults(),                          _ => _totalResults > 0);
			FindCallersCommand    = new RelayCommand(_ => StartFindCallers(),                        _ => IsNotSearching && _selectedResult?.Result.TokenProvider is MethodDef);

			_searchService.GroupFound      += OnGroupFound;
			_searchService.SearchCompleted += OnSearchCompleted;
			_searchService.StatusChanged   += OnStatusChanged;
		}

		// ── Search start / build options ──────────────────────────────────────

		void StartSearch() {
			if (string.IsNullOrWhiteSpace(SearchTerm)) return;

			SearchHistory.Remove(SearchTerm);
			SearchHistory.Insert(0, SearchTerm);
			while (SearchHistory.Count > MaxHistoryItems)
				SearchHistory.RemoveAt(SearchHistory.Count - 1);

			_settings.SaveHistory(SearchHistory);
			_settings.SaveOptions(BuildOptionsDto());

			ResultGroups.Clear();
			TotalResults  = 0;
			AssemblyCount = 0;
			ProgressValue = 0;
			ProgressMax   = 0;
			IsSearching   = true;
			StatusText    = "Starting search…";

			_searchService.StartSearch(BuildOptions());
		}

		void StartFindCallers() {
			var result = _selectedResult?.Result;
			if (result?.TokenProvider is not MethodDef method) return;

			ResultGroups.Clear();
			TotalResults  = 0;
			AssemblyCount = 0;
			ProgressValue = 0;
			ProgressMax   = 0;
			IsSearching   = true;
			StatusText    = $"Finding callers of {method.Name}…";

			_searchService.StartSearch(new DeepSearchOptions {
				FindCallers          = true,
				CallerTargetFullName = method.FullName,
				SearchTerm           = "*",
				Source               = CurrentSource(),
				FolderPath           = FolderPath,
				SearchSubfolders     = SearchSubfolders,
				Scope                = SearchScope.All,
			});
		}

		DeepSearchOptions BuildOptions() {
			var scope = SearchScope.None;
			if (ScopeTypes)       scope |= SearchScope.Types;
			if (ScopeMethods)     scope |= SearchScope.Methods;
			if (ScopeFields)      scope |= SearchScope.Fields;
			if (ScopeStrings)     scope |= SearchScope.Strings;
			if (ScopeAttributes)  scope |= SearchScope.Attributes;
			if (ScopeInheritance) scope |= SearchScope.Inheritance;
			if (ScopeILOpcodes)   scope |= SearchScope.ILOpcodes;

			var mode = IsWildcard ? MatchMode.Wildcard
			         : IsRegex    ? MatchMode.Regex
			         :              MatchMode.Substring;

			return new DeepSearchOptions {
				SearchTerm       = SearchTerm,
				MatchMode        = mode,
				CaseSensitive    = CaseSensitive,
				Scope            = scope,
				Source           = CurrentSource(),
				FolderPath       = FolderPath,
				SearchSubfolders = SearchSubfolders,
				TypeFilter       = TypeFilter,
				OpcodeFilter     = OpcodeFilter,
			};
		}

		DllSource CurrentSource() =>
			SourceAttached ? DllSource.AttachedProcess
			: SourceLoaded ? DllSource.LoadedAssemblies
			: SourceFolder ? DllSource.Folder
			:                DllSource.Both;

		DeepSearchOptionsDto BuildOptionsDto() => new() {
			ScopeTypes       = ScopeTypes,
			ScopeMethods     = ScopeMethods,
			ScopeFields      = ScopeFields,
			ScopeStrings     = ScopeStrings,
			ScopeAttributes  = ScopeAttributes,
			ScopeInheritance = ScopeInheritance,
			ScopeILOpcodes   = ScopeILOpcodes,
			MatchMode        = IsWildcard ? "Wildcard" : IsRegex ? "Regex" : "Substring",
			CaseSensitive    = CaseSensitive,
			Source           = CurrentSource().ToString(),
			SearchSubfolders = SearchSubfolders,
			FolderPath       = FolderPath,
		};

		void ApplyOptions(DeepSearchOptionsDto dto) {
			ScopeTypes       = dto.ScopeTypes;
			ScopeMethods     = dto.ScopeMethods;
			ScopeFields      = dto.ScopeFields;
			ScopeStrings     = dto.ScopeStrings;
			ScopeAttributes  = dto.ScopeAttributes;
			ScopeInheritance = dto.ScopeInheritance;
			ScopeILOpcodes   = dto.ScopeILOpcodes;
			CaseSensitive    = dto.CaseSensitive;
			FolderPath       = dto.FolderPath;
			SearchSubfolders = dto.SearchSubfolders;

			IsSubstring = dto.MatchMode != "Wildcard" && dto.MatchMode != "Regex";
			IsWildcard  = dto.MatchMode == "Wildcard";
			IsRegex     = dto.MatchMode == "Regex";

			SourceLoaded   = dto.Source == "LoadedAssemblies";
			SourceFolder   = dto.Source == "Folder";
			SourceBoth     = dto.Source == "Both";
			SourceAttached = dto.Source == "AttachedProcess";
			if (!SourceLoaded && !SourceFolder && !SourceBoth && !SourceAttached)
				SourceLoaded = true;
		}

		// ── Event handlers ────────────────────────────────────────────────────

		void OnGroupFound(object? sender, DeepSearchGroupFoundEventArgs e) {
			Application.Current.Dispatcher.Invoke(() => {
				var groupVM = new DeepSearchResultGroupVM(e.Group.AssemblyName, e.Group.AssemblyPath);
				foreach (var result in e.Group.Results) {
					groupVM.AddResult(new DeepSearchResultVM(result));
					TotalResults++;
				}
				ResultGroups.Add(groupVM);
				AssemblyCount = ResultGroups.Count;
				ProgressValue = e.CurrentDllIndex;
				ProgressMax   = e.TotalDllCount;
				OnPropertyChanged(nameof(FilteredResultGroups));
				OnPropertyChanged(nameof(FilteredAssemblyCount));
				StatusText = $"Scanning: {e.CurrentDllPath}  ({e.CurrentDllIndex} of {e.TotalDllCount})  —  {TotalResults} result(s)";
			});
		}

		void OnSearchCompleted(object? sender, DeepSearchCompletedEventArgs e) {
			Application.Current.Dispatcher.Invoke(() => {
				IsSearching   = false;
				ProgressValue = ProgressMax;
				StatusText    = e.WasCancelled
					? $"Search cancelled — {TotalResults} result(s) in {AssemblyCount} assembl{(AssemblyCount == 1 ? "y" : "ies")}"
					: $"Search complete — {TotalResults} result(s) in {AssemblyCount} assembl{(AssemblyCount == 1 ? "y" : "ies")}";
			});
		}

		void OnStatusChanged(object? sender, string status) =>
			Application.Current.Dispatcher.BeginInvoke(new Action(() => StatusText = status));

		// ── Commands implementation ───────────────────────────────────────────

		void BrowseFolder() {
			var chosen = _pickDirectory.GetDirectory(string.IsNullOrWhiteSpace(FolderPath) ? null : FolderPath);
			if (chosen is not null)
				FolderPath = chosen;
		}

		void ExportResults() {
			var dialog = new SaveFileDialog {
				Title      = "Export Deep Search Results",
				Filter     = "CSV files (*.csv)|*.csv|Text files (*.txt)|*.txt|All files (*.*)|*.*",
				DefaultExt = "csv",
				FileName   = "deep_search_results",
			};
			if (dialog.ShowDialog() != true) return;

			bool isCsv = dialog.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
			var sb = new StringBuilder();

			if (isCsv) {
				sb.AppendLine("Kind,Name,Namespace,ContainingType,AssemblyName,AssemblyPath");
				foreach (var group in ResultGroups)
					foreach (var r in group.Results) {
						var res = r.Result;
						sb.AppendLine($"\"{EscapeCsv(res.KindLabel)}\",\"{EscapeCsv(res.Name)}\",\"{EscapeCsv(res.Namespace)}\",\"{EscapeCsv(res.ContainingType)}\",\"{EscapeCsv(res.AssemblyName)}\",\"{EscapeCsv(res.AssemblyPath)}\"");
					}
			}
			else {
				foreach (var group in ResultGroups) {
					sb.AppendLine($"{group.AssemblyName}  ({group.Results.Count} match{(group.Results.Count == 1 ? "" : "es")})");
					foreach (var r in group.Results)
						sb.AppendLine($"  [{r.Result.KindLabel}]  {r.Result.Name}  ({r.Result.Location})");
					sb.AppendLine();
				}
			}

			File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
		}

		static string EscapeCsv(string s) => s.Replace("\"", "\"\"");

		void TakeSnapshot() {
			if (_totalResults == 0) {
				StatusText = "No results to snapshot.";
				return;
			}
			_snapshotKeys = new HashSet<string>(
				ResultGroups.SelectMany(g => g.Results).Select(r => r.Result.DiffKey));
			OnPropertyChanged(nameof(FilteredResultGroups));
			StatusText = $"Snapshot saved — {_snapshotKeys.Count} result(s). Run a new search then toggle Diff.";
		}

		void ToggleDiff() {
			if (_snapshotKeys == null) {
				StatusText = "Take a snapshot first (run a search, then click Snapshot), then run a new search.";
				return;
			}
			IsDiffMode = !IsDiffMode;
		}

		void CopyName() {
			var result = _selectedResult?.Result;
			if (result is null) return;
			var text = result.Kind == ResultKind.StringLiteral
				? result.MatchedValue ?? string.Empty
				: string.IsNullOrEmpty(result.Location)
					? result.Name
					: $"{result.Location}.{result.Name}";
			Clipboard.SetText(text);
		}

		void CopyPath() {
			var path = _selectedResult?.Result.AssemblyPath
			        ?? _selectedGroup?.AssemblyPath
			        ?? string.Empty;
			if (!string.IsNullOrEmpty(path))
				Clipboard.SetText(path);
		}

		void CopyAllResults() {
			var sb = new StringBuilder();
			foreach (var group in ResultGroups) {
				sb.AppendLine($"{group.AssemblyName}  ({group.Results.Count} match{(group.Results.Count == 1 ? "" : "es")})");
				foreach (var r in group.Results)
					sb.AppendLine($"  [{r.Result.KindLabel}]  {r.Result.DisplayName}  ({r.Result.Location})");
				sb.AppendLine();
			}
			if (sb.Length > 0)
				Clipboard.SetText(sb.ToString());
		}
	}
}
