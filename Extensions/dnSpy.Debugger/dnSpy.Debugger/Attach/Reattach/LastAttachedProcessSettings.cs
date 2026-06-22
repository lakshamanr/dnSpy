/*
    Copyright (C) 2014-2024 dnSpy Contributors

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
using System.ComponentModel.Composition;
using dnSpy.Contracts.Debugger;
using dnSpy.Contracts.Settings;

namespace dnSpy.Debugger.Attach.Reattach {
	interface ILastAttachedProcessSettings {
		LastAttachedProcessInfo? Load();
		void Save(LastAttachedProcessInfo? info);
	}

	[Export(typeof(ILastAttachedProcessSettings))]
	sealed class LastAttachedProcessSettings : ILastAttachedProcessSettings {
		static readonly Guid SETTINGS_GUID = new Guid("0E5B6F7A-9C84-4B6E-9E10-5F1FA0C8B3F1");
		const string SECTION_NAME    = "LastAttached";
		const string ATTR_NAME       = "Name";
		const string ATTR_FILENAME   = "Filename";
		const string ATTR_RUNTIMENM  = "RuntimeName";
		const string ATTR_RUNTIMEKIND= "RuntimeKindGuid";
		const string ATTR_RUNTIMEGD  = "RuntimeGuid";
		const string ATTR_PID        = "LastProcessId";
		const string ATTR_ARCH       = "Architecture";
		const string ATTR_OS         = "OperatingSystem";

		readonly ISettingsSection root;

		[ImportingConstructor]
		LastAttachedProcessSettings(ISettingsService settingsService) =>
			root = settingsService.GetOrCreateSection(SETTINGS_GUID);

		public LastAttachedProcessInfo? Load() {
			foreach (var sect in root.SectionsWithName(SECTION_NAME)) {
				var filename = sect.Attribute<string>(ATTR_FILENAME) ?? string.Empty;
				var name     = sect.Attribute<string>(ATTR_NAME)     ?? string.Empty;
				if (string.IsNullOrEmpty(filename) && string.IsNullOrEmpty(name))
					return null;
				return new LastAttachedProcessInfo(
					name,
					filename,
					sect.Attribute<string>(ATTR_RUNTIMENM) ?? string.Empty,
					sect.Attribute<Guid?>(ATTR_RUNTIMEKIND) ?? Guid.Empty,
					sect.Attribute<Guid?>(ATTR_RUNTIMEGD)   ?? Guid.Empty,
					sect.Attribute<int?>(ATTR_PID) ?? 0,
					(DbgArchitecture)(sect.Attribute<int?>(ATTR_ARCH) ?? (int)DbgArchitecture.X86),
					(DbgOperatingSystem)(sect.Attribute<int?>(ATTR_OS) ?? (int)DbgOperatingSystem.Windows));
			}
			return null;
		}

		public void Save(LastAttachedProcessInfo? info) {
			foreach (var sect in root.SectionsWithName(SECTION_NAME))
				root.RemoveSection(sect);
			if (info is null)
				return;
			var s = root.CreateSection(SECTION_NAME);
			s.Attribute(ATTR_NAME,        info.Name);
			s.Attribute(ATTR_FILENAME,    info.Filename);
			s.Attribute(ATTR_RUNTIMENM,   info.RuntimeName);
			s.Attribute(ATTR_RUNTIMEKIND, info.RuntimeKindGuid);
			s.Attribute(ATTR_RUNTIMEGD,   info.RuntimeGuid);
			s.Attribute(ATTR_PID,         info.LastProcessId);
			s.Attribute(ATTR_ARCH,        (int)info.Architecture);
			s.Attribute(ATTR_OS,          (int)info.OperatingSystem);
		}
	}
}
