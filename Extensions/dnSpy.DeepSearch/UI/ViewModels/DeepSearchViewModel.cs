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
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using dnSpy.Contracts.MVVM;
using dnSpy.DeepSearch.Core;
using dnSpy.DeepSearch.Services;

namespace dnSpy.DeepSearch.UI.ViewModels {
	public sealed class DeepSearchViewModel : ViewModelBase {
		readonly IDeepSearchService _searchService;
		readonly IPickDirectory _pickDirectory;

		const int MaxHistoryItems = 20;

		// ── Search term + history ────────────────────────────────────────────
		public ObservableCollection<string> SearchHistory { get; } = new();

		string _searchTerm = string.Empty;
		public string SearchTerm {
			get => _searchTerm;
			set {
				if (_searchTerm != value) {
					_searchTerm = value;
					OnPropertyChanged(nameof(SearchTerm));
				}
			}
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

		// ── Source ───────────────────────────────────────────────────────────
		bool _sourceLoaded = true;
		public bool SourceLoaded {
			get => _sourceLoaded;
			set {
				if (_sourceLoaded != value) {
					_sourceLoaded = value;
					OnPropertyChanged(nameof(SourceLoaded));
					OnPropertyChanged(nameof(IsFolderVisible));
				}
			}
		}

		bool _sourceFolder;
		public bool SourceFolder {
			get => _sourceFolder;
			set {
				if (_sourceFolder != value) {
					_sourceFolder = value;
					OnPropertyChanged(nameof(SourceFolder));
					OnPropertyChanged(nameof(IsFolderVisible));
				}
			}
		}

		bool _sourceBoth;
		public bool SourceBoth {
			get => _sourceBoth;
			set {
				if (_sourceBoth != value) {
					_sourceBoth = value;
					OnPropertyChanged(nameof(SourceBoth));
					OnPropertyChanged(nameof(IsFolderVisible));
				}
			}
		}

		// Folder row is visible whenever a folder path is needed
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

		// ── Results ──────────────────────────────────────────────────────────
		public ObservableCollection<DeepSearchResultGroupVM> ResultGroups { get; } = new();

		// The result the user last double-clicked; the code-behind uses this for navigation.
		DeepSearchResultVM? _selectedResult;
		public DeepSearchResultVM? SelectedResult {
			get => _selectedResult;
			set { if (_selectedResult != value) { _selectedResult = value; OnPropertyChanged(nameof(SelectedResult)); } }
		}

		// ── Status ───────────────────────────────────────────────────────────
		string _statusText = "Ready";
		public string StatusText {
			get => _statusText;
			set { if (_statusText != value) { _statusText = value; OnPropertyChanged(nameof(StatusText)); } }
		}

		int _totalResults;
		public int TotalResults {
			get => _totalResults;
			set { if (_totalResults != value) { _totalResults = value; OnPropertyChanged(nameof(TotalResults)); } }
		}

		bool _isSearching;
		public bool IsSearching {
			get => _isSearching;
			set {
				if (_isSearching != value) {
					_isSearching = value;
					OnPropertyChanged(nameof(IsSearching));
					OnPropertyChanged(nameof(IsNotSearching));
				}
			}
		}

		public bool IsNotSearching => !_isSearching;

		// ── Commands ─────────────────────────────────────────────────────────
		public ICommand SearchCommand { get; }
		public ICommand CancelCommand { get; }
		public ICommand BrowseFolderCommand { get; }

		public DeepSearchViewModel(IDeepSearchService searchService, IPickDirectory pickDirectory) {
			_searchService = searchService;
			_pickDirectory = pickDirectory;

			SearchCommand      = new RelayCommand(_ => StartSearch(),               _ => IsNotSearching && !string.IsNullOrWhiteSpace(SearchTerm));
			CancelCommand      = new RelayCommand(_ => _searchService.CancelSearch(), _ => IsSearching);
			BrowseFolderCommand = new RelayCommand(_ => BrowseFolder());

			_searchService.GroupFound      += OnGroupFound;
			_searchService.SearchCompleted += OnSearchCompleted;
			_searchService.StatusChanged   += OnStatusChanged;
		}

		void StartSearch() {
			if (string.IsNullOrWhiteSpace(SearchTerm))
				return;

			// Push search term into history (deduplicated, newest first)
			SearchHistory.Remove(SearchTerm);
			SearchHistory.Insert(0, SearchTerm);
			while (SearchHistory.Count > MaxHistoryItems)
				SearchHistory.RemoveAt(SearchHistory.Count - 1);

			ResultGroups.Clear();
			TotalResults = 0;
			IsSearching  = true;
			StatusText   = "Starting search…";

			_searchService.StartSearch(BuildOptions());
		}

		DeepSearchOptions BuildOptions() {
			var scope = SearchScope.None;
			if (ScopeTypes)   scope |= SearchScope.Types;
			if (ScopeMethods) scope |= SearchScope.Methods;
			if (ScopeFields)  scope |= SearchScope.Fields;
			if (ScopeStrings) scope |= SearchScope.Strings;

			var source = SourceLoaded ? DllSource.LoadedAssemblies
			           : SourceFolder ? DllSource.Folder
			           : DllSource.Both;

			var mode = IsWildcard ? MatchMode.Wildcard
			         : IsRegex    ? MatchMode.Regex
			         :              MatchMode.Substring;

			return new DeepSearchOptions {
				SearchTerm      = SearchTerm,
				MatchMode       = mode,
				CaseSensitive   = CaseSensitive,
				Scope           = scope,
				Source          = source,
				FolderPath      = FolderPath,
				SearchSubfolders = SearchSubfolders,
			};
		}

		void OnGroupFound(object? sender, DeepSearchGroupFoundEventArgs e) {
			Application.Current.Dispatcher.Invoke(() => {
				var groupVM = new DeepSearchResultGroupVM(e.Group.AssemblyName, e.Group.AssemblyPath);
				foreach (var result in e.Group.Results) {
					groupVM.AddResult(new DeepSearchResultVM(result));
					TotalResults++;
				}
				ResultGroups.Add(groupVM);
				StatusText = $"Scanning: {e.CurrentDllPath}  ({e.CurrentDllIndex} of {e.TotalDllCount})  —  {TotalResults} result(s)";
			});
		}

		void OnSearchCompleted(object? sender, DeepSearchCompletedEventArgs e) {
			Application.Current.Dispatcher.Invoke(() => {
				IsSearching = false;
				StatusText  = e.WasCancelled
					? $"Search cancelled — {TotalResults} result(s) found"
					: $"Search complete — {TotalResults} result(s) found";
			});
		}

		void OnStatusChanged(object? sender, string status) =>
			Application.Current.Dispatcher.Invoke(() => StatusText = status);

		void BrowseFolder() {
			var chosen = _pickDirectory.GetDirectory(string.IsNullOrWhiteSpace(FolderPath) ? null : FolderPath);
			if (chosen is not null)
				FolderPath = chosen;
		}
	}
}
