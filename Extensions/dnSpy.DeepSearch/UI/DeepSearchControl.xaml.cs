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
using System.Windows.Media;
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

		// Ensure the item under the mouse is selected before the context menu opens so that
		// VM commands (CopyName, FindCallers, etc.) operate on the right item.
		void OnResultPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e) {
			var element = e.OriginalSource as DependencyObject;
			while (element != null && element is not TreeViewItem)
				element = VisualTreeHelper.GetParent(element);

			if (element is not TreeViewItem tvi) return;
			tvi.IsSelected = true;

			if (DataContext is not DeepSearchViewModel vm) return;
			if (tvi.DataContext is DeepSearchResultVM rv) {
				vm.SelectedResult = rv;
				vm.SelectedGroup  = null;
			}
			else if (tvi.DataContext is DeepSearchResultGroupVM gv) {
				vm.SelectedGroup  = gv;
				vm.SelectedResult = null;
			}
		}

		DeepSearchResultVM? GetSelectedResult() {
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
