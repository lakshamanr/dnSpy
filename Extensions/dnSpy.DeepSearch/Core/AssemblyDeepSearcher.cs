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
			ModuleDef module, string assemblyPath, string assemblyName,
			DeepSearchOptions options, CancellationToken cancellationToken) {

			return options.FindCallers
				? FindCallers(module, assemblyPath, assemblyName, options, cancellationToken)
				: SearchMembers(module, assemblyPath, assemblyName, options, cancellationToken);
		}

		// ── Normal member search ───────────────────────────────────────────────

		static IEnumerable<DeepSearchResult> SearchMembers(
			ModuleDef module, string assemblyPath, string assemblyName,
			DeepSearchOptions options, CancellationToken ct) {

			bool hasTypeFilter = !string.IsNullOrWhiteSpace(options.TypeFilter);
			bool hasOpcodeFilter = options.Scope.HasFlag(SearchScope.ILOpcodes) &&
			                       !string.IsNullOrWhiteSpace(options.OpcodeFilter);

			foreach (var type in EnumerateAllTypes(module)) {
				ct.ThrowIfCancellationRequested();

				var typeNs   = type.Namespace?.String ?? string.Empty;
				var typeName = type.Name?.String      ?? string.Empty;
				var declName = type.DeclaringType?.Name?.String ?? string.Empty;

				// ── Types ──────────────────────────────────────────────────────
				if (options.Scope.HasFlag(SearchScope.Types)) {
					if (IsMatch(typeName, options))
						yield return new DeepSearchResult(ResultKind.Type, typeName, declName, typeNs, assemblyPath, assemblyName, type);
				}

				// ── Inheritance: base type / interfaces ────────────────────────
				if (options.Scope.HasFlag(SearchScope.Inheritance)) {
					var baseName = type.BaseType?.Name?.String ?? string.Empty;
					if (!string.IsNullOrEmpty(baseName) && IsMatch(baseName, options))
						yield return new DeepSearchResult(ResultKind.Type, typeName, declName, typeNs, assemblyPath, assemblyName, type, matchedValue: $"extends {baseName}");

					foreach (var iface in type.Interfaces) {
						var ifaceName = iface.Interface?.Name?.String ?? string.Empty;
						if (!string.IsNullOrEmpty(ifaceName) && IsMatch(ifaceName, options))
							yield return new DeepSearchResult(ResultKind.Type, typeName, declName, typeNs, assemblyPath, assemblyName, type, matchedValue: $"implements {ifaceName}");
					}
				}

				// ── Custom attributes on types ─────────────────────────────────
				if (options.Scope.HasFlag(SearchScope.Attributes)) {
					foreach (var attr in type.CustomAttributes) {
						var attrName = attr.Constructor?.DeclaringType?.Name?.String ?? string.Empty;
						if (!string.IsNullOrEmpty(attrName) && IsMatch(attrName, options))
							yield return new DeepSearchResult(ResultKind.Attribute, attrName, typeName, typeNs, assemblyPath, assemblyName, type);
					}
				}

				// ── Fields / properties / events ───────────────────────────────
				if (options.Scope.HasFlag(SearchScope.Fields)) {
					foreach (var field in type.Fields)
						if (IsMatch(field.Name, options))
							yield return new DeepSearchResult(ResultKind.Field, field.Name, typeName, typeNs, assemblyPath, assemblyName, field);

					foreach (var prop in type.Properties)
						if (IsMatch(prop.Name, options))
							yield return new DeepSearchResult(ResultKind.Property, prop.Name, typeName, typeNs, assemblyPath, assemblyName, prop);

					foreach (var evt in type.Events)
						if (IsMatch(evt.Name, options))
							yield return new DeepSearchResult(ResultKind.Event, evt.Name, typeName, typeNs, assemblyPath, assemblyName, evt);
				}

				// ── Methods + strings + IL opcodes + attributes on methods ─────
				foreach (var method in type.Methods) {
					ct.ThrowIfCancellationRequested();

					if (options.Scope.HasFlag(SearchScope.Methods)) {
						bool nameMatch = IsMatch(method.Name, options);
						bool typeMatch = !hasTypeFilter || MethodMatchesTypeFilter(method, options.TypeFilter);
						if (nameMatch && typeMatch)
							yield return new DeepSearchResult(ResultKind.Method, method.Name, typeName, typeNs, assemblyPath, assemblyName, method);
					}

					if (options.Scope.HasFlag(SearchScope.Attributes)) {
						foreach (var attr in method.CustomAttributes) {
							var attrName = attr.Constructor?.DeclaringType?.Name?.String ?? string.Empty;
							if (!string.IsNullOrEmpty(attrName) && IsMatch(attrName, options))
								yield return new DeepSearchResult(ResultKind.Attribute, attrName, typeName, typeNs, assemblyPath, assemblyName, method);
						}
					}

					if (!method.HasBody) continue;
					var instructions = method.Body.Instructions;

					if (options.Scope.HasFlag(SearchScope.Strings)) {
						foreach (var instr in instructions) {
							if (instr.OpCode == OpCodes.Ldstr && instr.Operand is string strValue)
								if (IsMatch(strValue, options))
									yield return new DeepSearchResult(ResultKind.StringLiteral, method.Name, typeName, typeNs, assemblyPath, assemblyName, method, instr.Offset, strValue);
						}
					}

					if (hasOpcodeFilter) {
						var opTarget = options.OpcodeFilter.Trim();
						foreach (var instr in instructions) {
							if (instr.OpCode.Name.Equals(opTarget, StringComparison.OrdinalIgnoreCase))
								yield return new DeepSearchResult(ResultKind.ILInstruction, method.Name, typeName, typeNs, assemblyPath, assemblyName, method, instr.Offset, instr.OpCode.Name);
						}
					}
				}
			}
		}

		// ── Find callers (cross-reference) ────────────────────────────────────

		static IEnumerable<DeepSearchResult> FindCallers(
			ModuleDef module, string assemblyPath, string assemblyName,
			DeepSearchOptions options, CancellationToken ct) {

			var target = options.CallerTargetFullName;
			if (string.IsNullOrWhiteSpace(target)) yield break;

			foreach (var type in EnumerateAllTypes(module)) {
				ct.ThrowIfCancellationRequested();

				var typeNs   = type.Namespace?.String ?? string.Empty;
				var typeName = type.Name?.String      ?? string.Empty;

				foreach (var method in type.Methods) {
					ct.ThrowIfCancellationRequested();
					if (!method.HasBody) continue;

					foreach (var instr in method.Body.Instructions) {
						if (instr.OpCode != OpCodes.Call    &&
						    instr.OpCode != OpCodes.Callvirt &&
						    instr.OpCode != OpCodes.Newobj)
							continue;

						if (instr.Operand is IMethodDefOrRef callee) {
							// FullName access can throw on malformed metadata; pull it out before yielding
							// since yield return is not allowed inside a try/catch block (CS1626).
							string? fullName = null;
							string? calleeName = null;
							try { fullName = callee.FullName; calleeName = callee.Name; } catch { }
							if (fullName != null && fullName.Equals(target, StringComparison.Ordinal))
								yield return new DeepSearchResult(ResultKind.Method, method.Name, typeName, typeNs, assemblyPath, assemblyName, method, instr.Offset, $"calls {calleeName}");
						}
					}
				}
			}
		}

		// ── Helpers ───────────────────────────────────────────────────────────

		static bool IsMatch(string value, DeepSearchOptions options) =>
			MatchHelper.IsMatch(value, options.SearchTerm, options.MatchMode, options.CaseSensitive);

		static bool MethodMatchesTypeFilter(MethodDef method, string typeFilter) {
			var comparison = StringComparison.OrdinalIgnoreCase;
			if ((method.ReturnType?.TypeName ?? string.Empty).IndexOf(typeFilter, comparison) >= 0)
				return true;
			foreach (var param in method.Parameters)
				if ((param.Type?.TypeName ?? string.Empty).IndexOf(typeFilter, comparison) >= 0)
					return true;
			return false;
		}

		static IEnumerable<TypeDef> EnumerateAllTypes(ModuleDef module) {
			var queue = new Queue<TypeDef>();
			foreach (var t in module.Types) queue.Enqueue(t);
			while (queue.Count > 0) {
				var type = queue.Dequeue();
				yield return type;
				foreach (var nested in type.NestedTypes) queue.Enqueue(nested);
			}
		}
	}
}
