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

using dnlib.DotNet;

namespace dnSpy.DeepSearch.Core {
	public enum ResultKind {
		Type,
		Method,
		Field,
		Property,
		Event,
		StringLiteral,
		Attribute,      // custom attribute on a type or method
		ILInstruction,  // IL opcode match
	}

	public sealed class DeepSearchResult {
		public ResultKind Kind         { get; }
		public string Name             { get; }
		public string ContainingType   { get; }
		public string Namespace        { get; }
		public string AssemblyPath     { get; }
		public string AssemblyName     { get; }
		public IMDTokenProvider TokenProvider { get; }
		public uint? ILOffset          { get; }
		public string? MatchedValue    { get; }

		public DeepSearchResult(
			ResultKind kind, string name, string containingType,
			string ns, string assemblyPath, string assemblyName,
			IMDTokenProvider tokenProvider,
			uint? ilOffset = null, string? matchedValue = null) {
			Kind           = kind;
			Name           = name;
			ContainingType = containingType;
			Namespace      = ns;
			AssemblyPath   = assemblyPath;
			AssemblyName   = assemblyName;
			TokenProvider  = tokenProvider;
			ILOffset       = ilOffset;
			MatchedValue   = matchedValue;
		}

		public string DisplayName => Kind switch {
			ResultKind.StringLiteral => $"\"{MatchedValue}\"  in  {Name}()",
			ResultKind.Attribute     => $"[{Name}]  on  {ContainingType}",
			ResultKind.ILInstruction => $"{MatchedValue}  in  {Name}()",
			_                        => Name,
		};

		public string Location {
			get {
				if (string.IsNullOrEmpty(ContainingType))
					return Namespace;
				if (string.IsNullOrEmpty(Namespace))
					return ContainingType;
				return $"{Namespace}.{ContainingType}";
			}
		}

		public string KindLabel => Kind switch {
			ResultKind.Type          => "T",
			ResultKind.Method        => "M",
			ResultKind.Field         => "F",
			ResultKind.Property      => "P",
			ResultKind.Event         => "E",
			ResultKind.StringLiteral => "S",
			ResultKind.Attribute     => "A",
			ResultKind.ILInstruction => "I",
			_                        => "?",
		};

		/// <summary>Stable key used for snapshot diff comparison.</summary>
		public string DiffKey => $"{AssemblyName}|{Kind}|{Name}|{Location}";
	}
}
