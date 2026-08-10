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
using System.Windows;
using dnSpy.Contracts.Controls;

namespace dnSpy.Debugger.ToolWindows.QuickWatch {
	sealed partial class QuickWatchWindow : WindowBase {
		readonly Action<string> addToWatch;

		internal QuickWatchWindow(string expression, string value, Action<string> addToWatch) {
			InitializeComponent();
			this.addToWatch = addToWatch ?? throw new ArgumentNullException(nameof(addToWatch));
			ExpressionTextBox.Text = expression;
			ValueTextBox.Text = value;
			ExpressionTextBox.SelectAll();
			ExpressionTextBox.Focus();
		}

		void AddToWatch_Click(object sender, RoutedEventArgs e) {
			var expr = ExpressionTextBox.Text.Trim();
			if (string.IsNullOrEmpty(expr)) return;
			addToWatch(expr);
		}

		void Close_Click(object sender, RoutedEventArgs e) => Close();
	}
}
