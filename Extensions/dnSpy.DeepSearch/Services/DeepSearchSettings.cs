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
using dnSpy.Contracts.Settings;

namespace dnSpy.DeepSearch.Services {
	public interface IDeepSearchSettings {
		/// <summary>Returns persisted history terms, oldest-at-end order (same as in-memory list).</summary>
		IReadOnlyList<string> LoadHistory();
		/// <summary>Overwrites the persisted history with the current in-memory list.</summary>
		void SaveHistory(IEnumerable<string> history);
	}

	[Export(typeof(IDeepSearchSettings))]
	sealed class DeepSearchSettings : IDeepSearchSettings {
		// Unique GUID for this extension's settings section in the dnSpy settings file
		static readonly Guid SETTINGS_GUID = new Guid("B7C1D2E3-F4A5-6789-BCDE-F01234567890");
		const string SECTION_ITEM = "SearchHistory";
		const string ATTR_TERM    = "Term";

		readonly ISettingsSection _root;

		[ImportingConstructor]
		public DeepSearchSettings(ISettingsService settingsService) =>
			_root = settingsService.GetOrCreateSection(SETTINGS_GUID);

		public IReadOnlyList<string> LoadHistory() {
			var list = new List<string>();
			foreach (var sect in _root.SectionsWithName(SECTION_ITEM)) {
				var term = sect.Attribute<string>(ATTR_TERM);
				if (!string.IsNullOrWhiteSpace(term))
					list.Add(term);
			}
			return list;
		}

		public void SaveHistory(IEnumerable<string> history) {
			// Remove all existing items then write the current list in order
			foreach (var sect in _root.SectionsWithName(SECTION_ITEM))
				_root.RemoveSection(sect);
			foreach (var term in history) {
				var sect = _root.CreateSection(SECTION_ITEM);
				sect.Attribute(ATTR_TERM, term);
			}
		}
	}
}
