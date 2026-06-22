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
using dnSpy.Contracts.Debugger;

namespace dnSpy.Debugger.Attach.Reattach {
	sealed class LastAttachedProcessInfo {
		public string Name { get; }
		public string Filename { get; }
		public string RuntimeName { get; }
		public Guid RuntimeKindGuid { get; }
		public Guid RuntimeGuid { get; }
		public int LastProcessId { get; }
		public DbgArchitecture Architecture { get; }
		public DbgOperatingSystem OperatingSystem { get; }

		public LastAttachedProcessInfo(string name, string filename, string runtimeName, Guid runtimeKindGuid, Guid runtimeGuid, int lastProcessId, DbgArchitecture architecture, DbgOperatingSystem operatingSystem) {
			Name = name ?? string.Empty;
			Filename = filename ?? string.Empty;
			RuntimeName = runtimeName ?? string.Empty;
			RuntimeKindGuid = runtimeKindGuid;
			RuntimeGuid = runtimeGuid;
			LastProcessId = lastProcessId;
			Architecture = architecture;
			OperatingSystem = operatingSystem;
		}

		public string DisplayName {
			get {
				if (!string.IsNullOrEmpty(Name))
					return Name;
				if (!string.IsNullOrEmpty(Filename))
					return Filename;
				return LastProcessId.ToString();
			}
		}
	}
}
