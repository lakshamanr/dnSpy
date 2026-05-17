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

using System.Collections.ObjectModel;
using dnSpy.Contracts.MVVM;

namespace dnSpy.DeepSearch.UI.ViewModels {
	public sealed class DeepSearchResultGroupVM : ViewModelBase {
		public string AssemblyName { get; }
		public string AssemblyPath { get; }
		public ObservableCollection<DeepSearchResultVM> Results { get; } = new();

		public string Header => $"{AssemblyName}  ({Results.Count} match{(Results.Count == 1 ? "" : "es")})";

		bool _isExpanded = true;
		public bool IsExpanded {
			get => _isExpanded;
			set {
				if (_isExpanded != value) {
					_isExpanded = value;
					OnPropertyChanged(nameof(IsExpanded));
				}
			}
		}

		public DeepSearchResultGroupVM(string assemblyName, string assemblyPath) {
			AssemblyName = assemblyName;
			AssemblyPath = assemblyPath;
		}

		public void AddResult(DeepSearchResultVM result) {
			Results.Add(result);
			OnPropertyChanged(nameof(Header));
		}
	}
}
