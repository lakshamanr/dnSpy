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

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using dnSpy.DeepSearch.UI.ViewModels;

namespace dnSpy.DeepSearch.UI {
	public partial class DeepSearchControl : UserControl {
		/// <summary>Raised when the user wants to navigate to a result (double-click or Enter).</summary>
		public event RoutedEventHandler? NavigateRequested;

		public DeepSearchControl() => InitializeComponent();

		void OnResultMouseDoubleClick(object sender, MouseButtonEventArgs e) {
			if (GetSelectedResult() is not null)
				NavigateRequested?.Invoke(this, e);
		}

		void OnResultKeyDown(object sender, KeyEventArgs e) {
			if (e.Key == Key.Enter && GetSelectedResult() is not null) {
				NavigateRequested?.Invoke(this, e);
				e.Handled = true;
			}
		}

		DeepSearchResultVM? GetSelectedResult() {
			// Walk up from the TreeViewItem that owns the selected item
			if (ResultsTree.SelectedItem is DeepSearchResultVM result) {
				if (DataContext is DeepSearchViewModel vm)
					vm.SelectedResult = result;
				return result;
			}
			return null;
		}

		public void FocusSearchBox() => SearchBox.Focus();
	}
}
