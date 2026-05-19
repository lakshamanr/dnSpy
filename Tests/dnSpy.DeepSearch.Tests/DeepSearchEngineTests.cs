/*
    Copyright (C) 2024 dnSpy Contributors

    Tests for DeepSearchEngine: multi-assembly orchestration, cancellation, streaming.
*/

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnSpy.DeepSearch.Core;
using Xunit;

namespace dnSpy.DeepSearch.Tests {
	public class DeepSearchEngineTests {
		static ModuleDef MakeModule(string typeName, string ns = "Test") {
			var mod = new ModuleDefUser("Asm_" + typeName);
			var asm = new AssemblyDefUser("Asm_" + typeName);
			asm.Modules.Add(mod);
			mod.Types.Add(new TypeDefUser(ns, typeName, mod.CorLibTypes.Object.TypeDefOrRef));
			return mod;
		}

		static DeepSearchOptions TypeOptions(string term) =>
			new DeepSearchOptions { SearchTerm = term, Scope = SearchScope.Types, MatchMode = MatchMode.Substring };

		// ── Basic multi-assembly streaming ────────────────────────────────────
		[Fact]
		public async Task FindsResultsAcrossMultipleAssemblies() {
			var engine = new DeepSearchEngine();
			var groups  = new List<DeepSearchResultGroup>();
			var tcs = new TaskCompletionSource<bool>();

			engine.GroupFound      += (_, e) => groups.Add(e.Group);
			engine.SearchCompleted += (_, e) => tcs.TrySetResult(!e.WasCancelled);

			var targets = new[] {
				(MakeModule("Alpha"), "a.dll", "a"),
				(MakeModule("Beta"),  "b.dll", "b"),
				(MakeModule("Gamma"), "c.dll", "c"),
			};

			engine.Start(targets, TypeOptions("a"));      // matches "Alpha"
			var completed = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));

			Assert.True(completed);
			Assert.Single(groups);
			Assert.Equal("a", groups[0].AssemblyName);
		}

		[Fact]
		public async Task ReturnsGroupPerMatchingAssembly() {
			var engine = new DeepSearchEngine();
			var groups  = new List<DeepSearchResultGroup>();
			var tcs = new TaskCompletionSource<bool>();

			engine.GroupFound      += (_, e) => groups.Add(e.Group);
			engine.SearchCompleted += (_, e) => tcs.TrySetResult(true);

			var targets = new[] {
				(MakeModule("ServiceOne"), "s1.dll", "s1"),
				(MakeModule("ServiceTwo"), "s2.dll", "s2"),
				(MakeModule("Unrelated"),  "u.dll",  "u"),
			};

			engine.Start(targets, TypeOptions("Service"));
			await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));

			Assert.Equal(2, groups.Count);
		}

		// ── Cancellation ──────────────────────────────────────────────────────
		[Fact]
		public async Task CancelMidSearch_ReportsCancelled() {
			var engine = new DeepSearchEngine();
			bool wasCancelled = false;
			var tcs = new TaskCompletionSource<bool>();

			engine.SearchCompleted += (_, e) => {
				wasCancelled = e.WasCancelled;
				tcs.TrySetResult(true);
			};

			// Build 50 modules — gives us time to cancel before finish
			var targets = new List<(ModuleDef, string, string)>();
			for (int i = 0; i < 50; i++)
				targets.Add((MakeModule($"Type{i}"), $"{i}.dll", $"{i}"));

			engine.Start(targets, TypeOptions("Type"));
			engine.Cancel();

			await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
			Assert.True(wasCancelled);
		}

		// ── IsRunning flag ────────────────────────────────────────────────────
		[Fact]
		public async Task IsRunning_TrueWhileSearching_FalseAfter() {
			var engine = new DeepSearchEngine();
			var tcs = new TaskCompletionSource<bool>();
			engine.SearchCompleted += (_, _) => tcs.TrySetResult(true);

			var targets = new[] { (MakeModule("Only"), "o.dll", "o") };
			engine.Start(targets, TypeOptions("Only"));
			Assert.True(engine.IsRunning);

			await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
			Assert.False(engine.IsRunning);
		}

		// ── Start is a no-op when already running ─────────────────────────────
		[Fact]
		public async Task DoubleStart_IsNoOp() {
			var engine = new DeepSearchEngine();
			int completions = 0;
			var tcs = new TaskCompletionSource<bool>();

			engine.SearchCompleted += (_, _) => {
				if (++completions == 1) tcs.TrySetResult(true);
			};

			var targets = new[] { (MakeModule("A"), "a.dll", "a") };
			engine.Start(targets, TypeOptions("A"));
			engine.Start(targets, TypeOptions("A")); // second call must be silently ignored

			await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));

			// Give a moment to ensure no second completion fires
			await Task.Delay(200);
			Assert.Equal(1, completions);
		}

		// ── Status events ─────────────────────────────────────────────────────
		[Fact]
		public async Task StatusEvents_FireForEachAssembly() {
			var engine = new DeepSearchEngine();
			var statuses = new List<string>();
			var tcs = new TaskCompletionSource<bool>();

			engine.StatusChanged   += (_, s) => statuses.Add(s);
			engine.SearchCompleted += (_, _) => tcs.TrySetResult(true);

			var targets = new[] {
				(MakeModule("A"), "a.dll", "a"),
				(MakeModule("B"), "b.dll", "b"),
			};
			engine.Start(targets, TypeOptions("x")); // no matches, but status fires

			await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));

			// Engine fires "Collecting…" + one status per assembly scanned
			var scanStatuses = statuses.FindAll(s => s.StartsWith("Scanning:"));
			Assert.Equal(2, scanStatuses.Count);
		}
	}
}
