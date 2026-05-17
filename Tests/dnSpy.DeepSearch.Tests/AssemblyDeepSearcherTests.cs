/*
    Copyright (C) 2024 dnSpy Contributors

    Integration tests for AssemblyDeepSearcher using in-memory dnlib modules.
    No WPF, no MEF, no disk I/O.
*/

using System.Linq;
using System.Threading;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnSpy.DeepSearch.Core;
using Xunit;

namespace dnSpy.DeepSearch.Tests {
	public class AssemblyDeepSearcherTests {
		// ── Helpers ───────────────────────────────────────────────────────────

		/// <summary>
		/// Builds a minimal in-memory module with:
		///   namespace Foo { class MyService { string GetName() { return "hello world"; } } }
		/// </summary>
		static ModuleDef BuildTestModule() {
			var mod = new ModuleDefUser("TestAssembly");
			var asm = new AssemblyDefUser("TestAssembly");
			asm.Modules.Add(mod);

			var corLib = mod.CorLibTypes;

			var type = new TypeDefUser("Foo", "MyService", mod.CorLibTypes.Object.TypeDefOrRef);
			mod.Types.Add(type);

			// Method: string GetName() { return "hello world"; }
			var method = new MethodDefUser(
				"GetName",
				MethodSig.CreateInstance(corLib.String),
				MethodAttributes.Public);
			type.Methods.Add(method);

			method.Body = new CilBody();
			method.Body.Instructions.Add(OpCodes.Ldstr.ToInstruction("hello world"));
			method.Body.Instructions.Add(OpCodes.Ret.ToInstruction());

			// Field: int _count
			var field = new FieldDefUser("_count", new FieldSig(corLib.Int32), FieldAttributes.Private);
			type.Fields.Add(field);

			// Property (no backing logic — just the definition)
			var prop = new PropertyDefUser("Count", PropertySig.CreateInstance(corLib.Int32));
			type.Properties.Add(prop);

			return mod;
		}

		static ILookup<ResultKind, DeepSearchResult> RunSearch(DeepSearchOptions options) {
			var module = BuildTestModule();
			var results = AssemblyDeepSearcher
				.Search(module, "test.dll", "TestAssembly", options, CancellationToken.None)
				.ToLookup(r => r.Kind);
			return results;
		}

		// ── Type search ───────────────────────────────────────────────────────
		[Fact]
		public void FindsTypeByName() {
			var opts = new DeepSearchOptions {
				SearchTerm = "MyService",
				Scope = SearchScope.Types,
				MatchMode = MatchMode.Substring,
			};
			var results = RunSearch(opts);
			Assert.Single(results[ResultKind.Type]);
			Assert.Equal("MyService", results[ResultKind.Type].First().Name);
		}

		[Fact]
		public void TypeSearch_DoesNotReturnMethods() {
			var opts = new DeepSearchOptions { SearchTerm = "MyService", Scope = SearchScope.Types };
			var results = RunSearch(opts);
			Assert.Empty(results[ResultKind.Method]);
		}

		// ── Method search ─────────────────────────────────────────────────────
		[Fact]
		public void FindsMethodByName() {
			var opts = new DeepSearchOptions { SearchTerm = "GetName", Scope = SearchScope.Methods };
			var results = RunSearch(opts);
			Assert.Single(results[ResultKind.Method]);
			Assert.Equal("GetName", results[ResultKind.Method].First().Name);
			Assert.Equal("MyService", results[ResultKind.Method].First().ContainingType);
		}

		// ── Field search ──────────────────────────────────────────────────────
		[Fact]
		public void FindsFieldByName() {
			var opts = new DeepSearchOptions { SearchTerm = "_count", Scope = SearchScope.Fields };
			var results = RunSearch(opts);
			Assert.Single(results[ResultKind.Field]);
		}

		[Fact]
		public void FindsPropertyByName() {
			var opts = new DeepSearchOptions { SearchTerm = "Count", Scope = SearchScope.Fields };
			var results = RunSearch(opts);
			Assert.Single(results[ResultKind.Property]);
		}

		// ── String literal search ─────────────────────────────────────────────
		[Fact]
		public void FindsStringLiteral() {
			var opts = new DeepSearchOptions { SearchTerm = "hello", Scope = SearchScope.Strings };
			var results = RunSearch(opts);
			Assert.Single(results[ResultKind.StringLiteral]);
			var r = results[ResultKind.StringLiteral].First();
			Assert.Equal("hello world", r.MatchedValue);
			Assert.Equal("GetName", r.Name); // containing method
		}

		[Fact]
		public void StringSearch_MissWhenTermNotPresent() {
			var opts = new DeepSearchOptions { SearchTerm = "goodbye", Scope = SearchScope.Strings };
			var results = RunSearch(opts);
			Assert.Empty(results[ResultKind.StringLiteral]);
		}

		// ── Scope isolation ───────────────────────────────────────────────────
		[Fact]
		public void ScopeNone_ReturnsNoResults() {
			var opts = new DeepSearchOptions { SearchTerm = "MyService", Scope = SearchScope.None };
			var results = RunSearch(opts);
			Assert.Empty(results[ResultKind.Type]);
			Assert.Empty(results[ResultKind.Method]);
		}

		// ── Cancellation ──────────────────────────────────────────────────────
		[Fact]
		public void Cancellation_StopsSearch() {
			var cts = new CancellationTokenSource();
			cts.Cancel(); // cancel immediately

			var module = BuildTestModule();
			var opts = new DeepSearchOptions { SearchTerm = "x", Scope = SearchScope.All };

			Assert.Throws<System.OperationCanceledException>(() =>
				AssemblyDeepSearcher.Search(module, "test.dll", "TestAssembly", opts, cts.Token).ToList());
		}

		// ── Wildcard + Regex ─────────────────────────────────────────────────
		[Fact]
		public void WildcardMatch_FindsType() {
			var opts = new DeepSearchOptions {
				SearchTerm = "My*",
				Scope = SearchScope.Types,
				MatchMode = MatchMode.Wildcard,
			};
			var results = RunSearch(opts);
			Assert.Single(results[ResultKind.Type]);
		}

		[Fact]
		public void RegexMatch_FindsMethod() {
			var opts = new DeepSearchOptions {
				SearchTerm = @"^Get\w+$",
				Scope = SearchScope.Methods,
				MatchMode = MatchMode.Regex,
			};
			var results = RunSearch(opts);
			Assert.Single(results[ResultKind.Method]);
		}

		// ── Result metadata ───────────────────────────────────────────────────
		[Fact]
		public void TypeResult_HasCorrectNamespace() {
			var opts = new DeepSearchOptions { SearchTerm = "MyService", Scope = SearchScope.Types };
			var results = RunSearch(opts);
			var r = results[ResultKind.Type].First();
			Assert.Equal("Foo", r.Namespace);
			Assert.Equal("TestAssembly", r.AssemblyName);
		}
	}
}
