/*
    Copyright (C) 2024 dnSpy Contributors

    Unit tests for MatchHelper — no WPF or MEF dependencies required.
*/

using dnSpy.DeepSearch.Core;
using Xunit;

namespace dnSpy.DeepSearch.Tests {
	public class MatchHelperTests {
		// ── Substring ────────────────────────────────────────────────────────
		[Theory]
		[InlineData("MyClass",       "Class",  true)]
		[InlineData("MyClass",       "my",     true)]   // case-insensitive by default
		[InlineData("MyClass",       "xyz",    false)]
		[InlineData("",              "x",      false)]
		[InlineData("MyClass",       "",       false)]
		public void Substring_Default(string input, string pattern, bool expected) =>
			Assert.Equal(expected, MatchHelper.IsMatch(input, pattern, MatchMode.Substring, caseSensitive: false));

		[Fact]
		public void Substring_CaseSensitive_NoMatch() =>
			Assert.False(MatchHelper.IsMatch("MyClass", "myclass", MatchMode.Substring, caseSensitive: true));

		[Fact]
		public void Substring_CaseSensitive_Match() =>
			Assert.True(MatchHelper.IsMatch("MyClass", "MyClass", MatchMode.Substring, caseSensitive: true));

		// ── Wildcard ─────────────────────────────────────────────────────────
		[Theory]
		[InlineData("MyClass",       "My*",    true)]
		[InlineData("MyClass",       "*Class", true)]
		[InlineData("MyClass",       "*",      true)]
		[InlineData("MyClass",       "My?lass", true)]
		[InlineData("MyClass",       "My?",    false)]  // ? is one char, not suffix wildcard
		[InlineData("GetUserName",   "Get*Name", true)]
		[InlineData("GetUserName",   "Set*",   false)]
		public void Wildcard_Default(string input, string pattern, bool expected) =>
			Assert.Equal(expected, MatchHelper.IsMatch(input, pattern, MatchMode.Wildcard, caseSensitive: false));

		[Fact]
		public void Wildcard_CaseSensitive_NoMatch() =>
			Assert.False(MatchHelper.IsMatch("MyClass", "myclass*", MatchMode.Wildcard, caseSensitive: true));

		// ── Regex ─────────────────────────────────────────────────────────────
		[Theory]
		[InlineData("GetUserById",  @"Get\w+By\w+", true)]
		[InlineData("SetValue",     @"^Get",        false)]
		[InlineData("ProcessData",  @"[Pp]rocess",  true)]
		[InlineData("MyClass",      @"^My.*ss$",    true)]
		public void Regex_Default(string input, string pattern, bool expected) =>
			Assert.Equal(expected, MatchHelper.IsMatch(input, pattern, MatchMode.Regex, caseSensitive: false));

		[Fact]
		public void Regex_Invalid_DoesNotThrow() =>
			// An invalid regex must not throw — should just return false
			Assert.False(MatchHelper.IsMatch("anything", "[invalid(", MatchMode.Regex, caseSensitive: false));

		[Fact]
		public void Regex_CaseSensitive() {
			Assert.True( MatchHelper.IsMatch("MyClass", "MyClass", MatchMode.Regex, caseSensitive: true));
			Assert.False(MatchHelper.IsMatch("MyClass", "myclass", MatchMode.Regex, caseSensitive: true));
		}

		// ── Null / empty guards ───────────────────────────────────────────────
		[Fact]
		public void NullInput_ReturnsFalse() =>
			Assert.False(MatchHelper.IsMatch(null, "pattern", MatchMode.Substring, caseSensitive: false));

		[Fact]
		public void NullPattern_ReturnsFalse() =>
			Assert.False(MatchHelper.IsMatch("input", null, MatchMode.Substring, caseSensitive: false));
	}
}
