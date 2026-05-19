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
using System.Diagnostics;
using System.IO;
using System.IO.Compression;

namespace dnSpy.DeepSearch.Core {
	/// <summary>File-system helpers for enumerating PE files and zip archives to scan.</summary>
	internal static class FolderScanner {
		// Manual recursion so per-directory UnauthorizedAccessException doesn't abort the scan.
		public static IEnumerable<string> SafeEnumeratePeFiles(string folder, bool recurse) {
			string[]? dlls = null, exes = null;
			try { dlls = Directory.GetFiles(folder, "*.dll"); } catch (Exception ex) { Debug.WriteLine($"[DeepSearch] GetFiles *.dll failed in {folder}: {ex.Message}"); }
			try { exes = Directory.GetFiles(folder, "*.exe"); } catch (Exception ex) { Debug.WriteLine($"[DeepSearch] GetFiles *.exe failed in {folder}: {ex.Message}"); }
			if (dlls != null) foreach (var f in dlls) yield return f;
			if (exes != null) foreach (var f in exes) yield return f;

			if (!recurse) yield break;

			string[]? subdirs = null;
			try { subdirs = Directory.GetDirectories(folder); } catch (Exception ex) { Debug.WriteLine($"[DeepSearch] GetDirectories failed in {folder}: {ex.Message}"); }
			if (subdirs is null) yield break;

			foreach (var sub in subdirs)
				foreach (var f in SafeEnumeratePeFiles(sub, recurse: true))
					yield return f;
		}

		// Yields (rawBytes, virtualPath, displayName) for every .dll/.exe inside .zip / .nupkg archives.
		// virtualPath uses the "archive!entry" notation so deduplication and display work correctly.
		public static IEnumerable<(byte[] bytes, string virtualPath, string displayName)> EnumerateZipPeEntries(string folder, bool recurse) {
			foreach (var archive in EnumerateArchives(folder))
				foreach (var entry in EntriesFromArchive(archive))
					yield return entry;

			if (!recurse) yield break;

			string[]? subdirs = null;
			try { subdirs = Directory.GetDirectories(folder); } catch { }
			if (subdirs is null) yield break;

			foreach (var sub in subdirs)
				foreach (var entry in EnumerateZipPeEntries(sub, recurse: true))
					yield return entry;
		}

		static IEnumerable<string> EnumerateArchives(string folder) {
			string[]? nupkgs = null, zips = null;
			try { nupkgs = Directory.GetFiles(folder, "*.nupkg"); } catch { }
			try { zips   = Directory.GetFiles(folder, "*.zip");   } catch { }
			if (nupkgs != null) foreach (var f in nupkgs) yield return f;
			if (zips   != null) foreach (var f in zips)   yield return f;
		}

		static IEnumerable<(byte[], string, string)> EntriesFromArchive(string archivePath) {
			ZipArchive? zip = null;
			try { zip = ZipFile.OpenRead(archivePath); }
			catch { yield break; }

			using (zip) {
				foreach (var entry in zip.Entries) {
					var name = entry.Name;
					if (!name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
					    !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
						continue;

					byte[]? bytes = null;
					try {
						using var stream = entry.Open();
						bytes = new byte[(int)entry.Length];
						int offset = 0;
						while (offset < bytes.Length) {
							int n = stream.Read(bytes, offset, bytes.Length - offset);
							if (n == 0) break;
							offset += n;
						}
					}
					catch { continue; }

					var virtualPath  = $"{archivePath}!{entry.FullName}";
					var displayName  = $"{name}  ({Path.GetFileName(archivePath)})";
					yield return (bytes, virtualPath, displayName);
				}
			}
		}
	}
}
