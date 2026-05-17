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

using System.ComponentModel.Composition;
using System.Windows.Input;
using dnSpy.Contracts.Controls;
using dnSpy.Contracts.Extension;
using dnSpy.Contracts.Menus;
using dnSpy.Contracts.MVVM;
using dnSpy.Contracts.ToolWindows.App;
using dnSpy.DeepSearch.UI;

namespace dnSpy.DeepSearch.Plugin {
	/// <summary>
	/// Auto-loaded at startup: registers the Ctrl+Shift+D keyboard shortcut and wires the
	/// command into the main window's command manager.
	/// </summary>
	[ExportAutoLoaded]
	sealed class DeepSearchPluginLoader : IAutoLoaded {
		public static readonly RoutedCommand OpenDeepSearch =
			new RoutedCommand("OpenDeepSearch", typeof(DeepSearchPluginLoader));

		[ImportingConstructor]
		DeepSearchPluginLoader(IWpfCommandService wpfCommandService, IDsToolWindowService toolWindowService) {
			var cmds = wpfCommandService.GetCommands(ControlConstants.GUID_MAINWINDOW);
			cmds.Add(OpenDeepSearch, new RelayCommand(_ => toolWindowService.Show(DeepSearchToolWindowContent.THE_GUID)));
			cmds.Add(OpenDeepSearch, ModifierKeys.Control | ModifierKeys.Shift, Key.D);
		}
	}

	/// <summary>Menu item added to the View → Windows sub-menu.</summary>
	[ExportMenuItem(
		OwnerGuid  = MenuConstants.APP_MENU_VIEW_GUID,
		Header     = "_Deep Search",
		InputGestureText = "Ctrl+Shift+D",
		Group      = MenuConstants.GROUP_APP_MENU_VIEW_WINDOWS,
		Order      = 2100)]
	sealed class DeepSearchViewMenuItem : MenuItemCommand {
		DeepSearchViewMenuItem()
			: base(DeepSearchPluginLoader.OpenDeepSearch) {
		}
	}
}
