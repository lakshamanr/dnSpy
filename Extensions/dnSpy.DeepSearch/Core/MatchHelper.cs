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
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace dnSpy.DeepSearch.Core {
	public static class MatchHelper {
		static readonly ConcurrentDictionary<(string pattern, bool caseSensitive), Regex?> _regexCache = new();

		/// <summary>
		/// Returns true if <paramref name="input"/> matches <paramref name="pattern"/> according to
		/// <paramref name="mode"/>. Returns false when either argument is null/empty.
		/// </summary>
		public static bool IsMatch(string? input, string? pattern, MatchMode mode, bool caseSensitive) {
			if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(pattern))
				return false;

			// After the null/empty guard above, input and pattern are non-null.
			var nonNullInput   = input!;
			var nonNullPattern = pattern!;

			switch (mode) {
			case MatchMode.Substring:
				return nonNullInput.IndexOf(nonNullPattern, caseSensitive
					? StringComparison.Ordinal
					: StringComparison.OrdinalIgnoreCase) >= 0;

			case MatchMode.Wildcard:
				return IsWildcardMatch(nonNullInput, nonNullPattern, caseSensitive);

			case MatchMode.Regex:
				return IsRegexMatch(nonNullInput, nonNullPattern, caseSensitive);

			default:
				return false;
			}
		}

		static bool IsWildcardMatch(string input, string pattern, bool caseSensitive) {
			// Translate wildcard (* and ?) to a regex, then match
			var regexPattern = "^" + Regex.Escape(pattern)
				.Replace(@"\*", ".*")
				.Replace(@"\?", ".") + "$";
			return IsRegexMatch(input, regexPattern, caseSensitive);
		}

		static bool IsRegexMatch(string input, string pattern, bool caseSensitive) {
			var regex = GetOrCreateRegex(pattern, caseSensitive);
			if (regex is null)
				return false;

			try {
				return regex.IsMatch(input);
			}
			catch (RegexMatchTimeoutException) {
				return false;
			}
		}

		static Regex? GetOrCreateRegex(string pattern, bool caseSensitive) {
			if (_regexCache.Count > 256)
				_regexCache.Clear();

			return _regexCache.GetOrAdd((pattern, caseSensitive), key => {
				try {
					var options = RegexOptions.Singleline;
					if (!key.caseSensitive)
						options |= RegexOptions.IgnoreCase;
					return new Regex(key.pattern, options, TimeSpan.FromSeconds(1));
				}
				catch (ArgumentException) {
					return null;
				}
			});
		}
	}
}
