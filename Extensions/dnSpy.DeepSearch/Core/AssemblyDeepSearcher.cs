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

using System.Collections.Generic;
using System.Threading;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace dnSpy.DeepSearch.Core {
	/// <summary>
	/// Searches a single <see cref="ModuleDef"/> for members matching <see cref="DeepSearchOptions"/>.
	/// All work is done synchronously so the caller controls threading and cancellation granularity.
	/// </summary>
	internal static class AssemblyDeepSearcher {
		public static IEnumerable<DeepSearchResult> Search(
			ModuleDef module,
			string assemblyPath,
			string assemblyName,
			DeepSearchOptions options,
			CancellationToken cancellationToken) {

			foreach (var type in EnumerateAllTypes(module)) {
				cancellationToken.ThrowIfCancellationRequested();

				var typeNs   = type.Namespace?.String ?? string.Empty;
				var typeName = type.Name?.String      ?? string.Empty;
				var declName = type.DeclaringType?.Name?.String ?? string.Empty;

				// ── Types ──────────────────────────────────────────────────────────
				if (options.Scope.HasFlag(SearchScope.Types)) {
					if (MatchHelper.IsMatch(typeName, options.SearchTerm, options.MatchMode, options.CaseSensitive)) {
						yield return new DeepSearchResult(
							ResultKind.Type,
							typeName,
							declName,
							typeNs,
							assemblyPath,
							assemblyName,
							type);
					}
				}

				// ── Fields ─────────────────────────────────────────────────────────
				if (options.Scope.HasFlag(SearchScope.Fields)) {
					foreach (var field in type.Fields) {
						if (MatchHelper.IsMatch(field.Name, options.SearchTerm, options.MatchMode, options.CaseSensitive))
							yield return new DeepSearchResult(ResultKind.Field, field.Name, typeName, typeNs, assemblyPath, assemblyName, field);
					}

					foreach (var prop in type.Properties) {
						if (MatchHelper.IsMatch(prop.Name, options.SearchTerm, options.MatchMode, options.CaseSensitive))
							yield return new DeepSearchResult(ResultKind.Property, prop.Name, typeName, typeNs, assemblyPath, assemblyName, prop);
					}

					foreach (var evt in type.Events) {
						if (MatchHelper.IsMatch(evt.Name, options.SearchTerm, options.MatchMode, options.CaseSensitive))
							yield return new DeepSearchResult(ResultKind.Event, evt.Name, typeName, typeNs, assemblyPath, assemblyName, evt);
					}
				}

				// ── Methods + String literals ──────────────────────────────────────
				foreach (var method in type.Methods) {
					cancellationToken.ThrowIfCancellationRequested();

					if (options.Scope.HasFlag(SearchScope.Methods)) {
						if (MatchHelper.IsMatch(method.Name, options.SearchTerm, options.MatchMode, options.CaseSensitive))
							yield return new DeepSearchResult(ResultKind.Method, method.Name, typeName, typeNs, assemblyPath, assemblyName, method);
					}

					if (options.Scope.HasFlag(SearchScope.Strings) && method.HasBody) {
						foreach (var instr in method.Body.Instructions) {
							// ldstr pushes a literal string onto the stack
							if (instr.OpCode == OpCodes.Ldstr && instr.Operand is string strValue) {
								if (MatchHelper.IsMatch(strValue, options.SearchTerm, options.MatchMode, options.CaseSensitive)) {
									yield return new DeepSearchResult(
										ResultKind.StringLiteral,
										method.Name,
										typeName,
										typeNs,
										assemblyPath,
										assemblyName,
										method,
										instr.Offset,
										strValue);
								}
							}
						}
					}
				}
			}
		}

		/// <summary>
		/// BFS walk over all types including nested types.
		/// </summary>
		static IEnumerable<TypeDef> EnumerateAllTypes(ModuleDef module) {
			var queue = new Queue<TypeDef>();
			foreach (var t in module.Types)
				queue.Enqueue(t);

			while (queue.Count > 0) {
				var type = queue.Dequeue();
				yield return type;
				foreach (var nested in type.NestedTypes)
					queue.Enqueue(nested);
			}
		}
	}
}
