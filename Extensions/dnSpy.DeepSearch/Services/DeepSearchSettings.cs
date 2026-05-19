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
	/// <summary>Persisted state for the search options panel.</summary>
	public sealed class DeepSearchOptionsDto {
		public bool   ScopeTypes       { get; set; } = true;
		public bool   ScopeMethods     { get; set; } = true;
		public bool   ScopeFields      { get; set; } = true;
		public bool   ScopeStrings     { get; set; } = true;
		public bool   ScopeAttributes  { get; set; }
		public bool   ScopeInheritance { get; set; }
		public bool   ScopeILOpcodes   { get; set; }
		public string MatchMode        { get; set; } = "Substring";
		public bool   CaseSensitive    { get; set; }
		public string Source           { get; set; } = "LoadedAssemblies";
		public bool   SearchSubfolders { get; set; } = true;
		public string FolderPath       { get; set; } = string.Empty;
	}

	public interface IDeepSearchSettings {
		/// <summary>Returns persisted history terms, newest-first order (same as the in-memory list).</summary>
		IReadOnlyList<string> LoadHistory();
		/// <summary>Overwrites the persisted history with the current in-memory list.</summary>
		void SaveHistory(IEnumerable<string> history);

		/// <summary>Returns persisted search-option values, or defaults if nothing was saved yet.</summary>
		DeepSearchOptionsDto LoadOptions();
		/// <summary>Persists the current search-option values.</summary>
		void SaveOptions(DeepSearchOptionsDto options);
	}

	[Export(typeof(IDeepSearchSettings))]
	sealed class DeepSearchSettings : IDeepSearchSettings {
		static readonly Guid SETTINGS_GUID = new Guid("B7C1D2E3-F4A5-6789-BCDE-F01234567890");
		const string SECTION_ITEM    = "SearchHistory";
		const string SECTION_OPTIONS = "SearchOptions";
		const string ATTR_TERM       = "Term";

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
			foreach (var sect in _root.SectionsWithName(SECTION_ITEM))
				_root.RemoveSection(sect);
			foreach (var term in history) {
				var sect = _root.CreateSection(SECTION_ITEM);
				sect.Attribute(ATTR_TERM, term);
			}
		}

		public DeepSearchOptionsDto LoadOptions() {
			var dto = new DeepSearchOptionsDto();
			var sects = _root.SectionsWithName(SECTION_OPTIONS);
			foreach (var sect in sects) {
				dto.ScopeTypes       = sect.Attribute<bool?>("ScopeTypes")       ?? dto.ScopeTypes;
				dto.ScopeMethods     = sect.Attribute<bool?>("ScopeMethods")     ?? dto.ScopeMethods;
				dto.ScopeFields      = sect.Attribute<bool?>("ScopeFields")      ?? dto.ScopeFields;
				dto.ScopeStrings     = sect.Attribute<bool?>("ScopeStrings")     ?? dto.ScopeStrings;
				dto.ScopeAttributes  = sect.Attribute<bool?>("ScopeAttributes")  ?? dto.ScopeAttributes;
				dto.ScopeInheritance = sect.Attribute<bool?>("ScopeInheritance") ?? dto.ScopeInheritance;
				dto.ScopeILOpcodes   = sect.Attribute<bool?>("ScopeILOpcodes")   ?? dto.ScopeILOpcodes;
				dto.MatchMode        = sect.Attribute<string>("MatchMode")        ?? dto.MatchMode;
				dto.CaseSensitive    = sect.Attribute<bool?>("CaseSensitive")    ?? dto.CaseSensitive;
				dto.Source           = sect.Attribute<string>("Source")           ?? dto.Source;
				dto.SearchSubfolders = sect.Attribute<bool?>("SearchSubfolders") ?? dto.SearchSubfolders;
				dto.FolderPath       = sect.Attribute<string>("FolderPath")       ?? dto.FolderPath;
				break;
			}
			return dto;
		}

		public void SaveOptions(DeepSearchOptionsDto dto) {
			foreach (var sect in _root.SectionsWithName(SECTION_OPTIONS))
				_root.RemoveSection(sect);

			var s = _root.CreateSection(SECTION_OPTIONS);
			s.Attribute("ScopeTypes",       dto.ScopeTypes);
			s.Attribute("ScopeMethods",     dto.ScopeMethods);
			s.Attribute("ScopeFields",      dto.ScopeFields);
			s.Attribute("ScopeStrings",     dto.ScopeStrings);
			s.Attribute("ScopeAttributes",  dto.ScopeAttributes);
			s.Attribute("ScopeInheritance", dto.ScopeInheritance);
			s.Attribute("ScopeILOpcodes",   dto.ScopeILOpcodes);
			s.Attribute("MatchMode",        dto.MatchMode);
			s.Attribute("CaseSensitive",    dto.CaseSensitive);
			s.Attribute("Source",           dto.Source);
			s.Attribute("SearchSubfolders", dto.SearchSubfolders);
			s.Attribute("FolderPath",       dto.FolderPath);
		}
	}
}
