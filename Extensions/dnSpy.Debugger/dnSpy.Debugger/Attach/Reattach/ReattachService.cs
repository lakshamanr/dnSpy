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
using System.IO;
using System.Threading;
using dnSpy.Contracts.App;
using dnSpy.Contracts.Debugger;
using dnSpy.Contracts.Debugger.Attach;
using dnSpy.Debugger.Properties;

namespace dnSpy.Debugger.Attach.Reattach {
	interface IReattachService {
		LastAttachedProcessInfo? Last { get; }
		event EventHandler? LastChanged;

		bool CanReattach { get; }
		void Reattach();

		void Record(AttachProgramOptions options, string? resolvedName, string? resolvedFilename);
	}

	[Export(typeof(IReattachService))]
	sealed class ReattachService : IReattachService {
		readonly ILastAttachedProcessSettings settings;
		readonly Lazy<AttachableProcessesService> attachableProcessesService;
		readonly Lazy<IMessageBoxService> messageBoxService;

		LastAttachedProcessInfo? last;

		public LastAttachedProcessInfo? Last => last;
		public event EventHandler? LastChanged;

		[ImportingConstructor]
		ReattachService(ILastAttachedProcessSettings settings, Lazy<AttachableProcessesService> attachableProcessesService, Lazy<IMessageBoxService> messageBoxService) {
			this.settings = settings;
			this.attachableProcessesService = attachableProcessesService;
			this.messageBoxService = messageBoxService;
			last = settings.Load();
		}

		public bool CanReattach => last is not null && (!string.IsNullOrEmpty(last.Filename) || !string.IsNullOrEmpty(last.Name));

		public void Record(AttachProgramOptions options, string? resolvedName, string? resolvedFilename) {
			if (options is null)
				return;
			// Provider-supplied values win; otherwise use the values the dialog resolved from the live process.
			var name     = options.Name     ?? resolvedName     ?? string.Empty;
			var filename = options.Filename ?? resolvedFilename ?? string.Empty;
			if (string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(filename))
				name = Path.GetFileName(filename);

			Store(new LastAttachedProcessInfo(
				name,
				filename,
				options.RuntimeName ?? string.Empty,
				options.RuntimeKindGuid,
				options.RuntimeGuid,
				options.ProcessId,
				options.Architecture ?? DbgArchitecture.X86,
				options.OperatingSystem ?? DbgOperatingSystem.Windows));
		}

		void Record(AttachableProcess process) =>
			Store(new LastAttachedProcessInfo(
				process.Name ?? string.Empty,
				process.Filename ?? string.Empty,
				process.RuntimeName ?? string.Empty,
				process.RuntimeKindGuid,
				process.RuntimeGuid,
				process.ProcessId,
				process.Architecture,
				process.OperatingSystem));

		void Store(LastAttachedProcessInfo info) {
			last = info;
			try { settings.Save(info); }
			catch { /* persistence failures should never break the debugger */ }
			LastChanged?.Invoke(this, EventArgs.Empty);
		}

		public async void Reattach() {
			var info = last;
			if (info is null) {
				ShowMessage(dnSpy_Debugger_Resources.Reattach_NoLast);
				return;
			}

			// First try to filter by short process name so the enumeration is cheap.
			var processName = !string.IsNullOrEmpty(info.Name)
				? Path.GetFileNameWithoutExtension(info.Name)
				: Path.GetFileNameWithoutExtension(info.Filename);
			string[]? nameFilter = string.IsNullOrEmpty(processName) ? null : new[] { processName + ".*", processName };

			AttachableProcess[] processes;
			try {
				processes = await attachableProcessesService.Value.GetAttachableProcessesAsync(nameFilter, null, null, CancellationToken.None).ConfigureAwait(true);
			}
			catch (Exception ex) {
				ShowMessage(string.Format(dnSpy_Debugger_Resources.Reattach_EnumerateFailed, ex.Message));
				return;
			}

			var match = FindBestMatch(processes, info);
			if (match is null) {
				ShowMessage(string.Format(dnSpy_Debugger_Resources.Reattach_NotFound, info.DisplayName));
				return;
			}

			try {
				match.Attach();
				Record(match);
			}
			catch (Exception ex) {
				ShowMessage(string.Format(dnSpy_Debugger_Resources.Reattach_AttachFailed, ex.Message));
			}
		}

		static AttachableProcess? FindBestMatch(AttachableProcess[] processes, LastAttachedProcessInfo info) {
			if (processes.Length == 0)
				return null;

			AttachableProcess? exactFilenameRuntime = null;
			AttachableProcess? exactFilename = null;
			AttachableProcess? sameNameRuntime = null;
			AttachableProcess? sameName = null;
			foreach (var p in processes) {
				bool sameFile = !string.IsNullOrEmpty(info.Filename) && PathsEqual(p.Filename, info.Filename);
				bool sameRuntime = info.RuntimeKindGuid != Guid.Empty && p.RuntimeKindGuid == info.RuntimeKindGuid;
				bool sameShortName = !string.IsNullOrEmpty(info.Name) && string.Equals(p.Name, info.Name, StringComparison.OrdinalIgnoreCase);

				if (sameFile && sameRuntime) { exactFilenameRuntime = p; break; }
				if (sameFile && exactFilename is null) exactFilename = p;
				if (sameShortName && sameRuntime && sameNameRuntime is null) sameNameRuntime = p;
				if (sameShortName && sameName is null) sameName = p;
			}
			return exactFilenameRuntime ?? exactFilename ?? sameNameRuntime ?? sameName;
		}

		static bool PathsEqual(string a, string b) {
			if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
				return false;
			try {
				return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
			}
			catch {
				return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
			}
		}

		void ShowMessage(string message) => messageBoxService.Value.Show(message);
	}
}
