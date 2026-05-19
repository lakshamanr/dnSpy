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

namespace dnSpy.DeepSearch.Core {
	public enum MatchMode {
		Substring,
		Wildcard,
		Regex,
	}

	[Flags]
	public enum SearchScope {
		None        = 0,
		Types       = 1 << 0,
		Methods     = 1 << 1,
		Fields      = 1 << 2,
		Strings     = 1 << 3,
		Attributes  = 1 << 4,  // custom attributes on types/methods
		Inheritance = 1 << 5,  // base types and implemented interfaces
		ILOpcodes   = 1 << 6,  // IL instruction opcode matching
		All         = Types | Methods | Fields | Strings | Attributes | Inheritance | ILOpcodes,
	}

	public enum DllSource {
		LoadedAssemblies,
		Folder,
		Both,
		/// <summary>Modules currently loaded in the attached debug process via DbgManager.</summary>
		AttachedProcess,
	}

	public sealed class DeepSearchOptions {
		public string SearchTerm         { get; set; } = string.Empty;
		public MatchMode MatchMode       { get; set; } = MatchMode.Substring;
		public bool CaseSensitive        { get; set; }
		public SearchScope Scope         { get; set; } = SearchScope.All;
		public DllSource Source          { get; set; } = DllSource.LoadedAssemblies;
		public string FolderPath         { get; set; } = string.Empty;
		public bool SearchSubfolders     { get; set; } = true;

		/// <summary>Narrows Methods results: only methods whose return type or a parameter type contains this string.</summary>
		public string TypeFilter         { get; set; } = string.Empty;

		/// <summary>When ILOpcodes scope is active, the opcode name to match (e.g. "ldsfld", "call").</summary>
		public string OpcodeFilter       { get; set; } = string.Empty;

		/// <summary>Find-callers mode: ignores SearchTerm; scans call/callvirt operands for CallerTargetFullName.</summary>
		public bool FindCallers          { get; set; }
		public string CallerTargetFullName { get; set; } = string.Empty;
	}
}
