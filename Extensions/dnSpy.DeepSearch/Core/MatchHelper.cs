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
using System.Text.RegularExpressions;

namespace dnSpy.DeepSearch.Core {
	public static class MatchHelper {
		/// <summary>
		/// Returns true if <paramref name="input"/> matches <paramref name="pattern"/> according to
		/// <paramref name="mode"/>. Returns false when either argument is null/empty.
		/// </summary>
		public static bool IsMatch(string? input, string? pattern, MatchMode mode, bool caseSensitive) {
			if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(pattern))
				return false;

			switch (mode) {
			case MatchMode.Substring:
				return input.Contains(pattern, caseSensitive
					? StringComparison.Ordinal
					: StringComparison.OrdinalIgnoreCase);

			case MatchMode.Wildcard:
				return IsWildcardMatch(input, pattern, caseSensitive);

			case MatchMode.Regex:
				return IsRegexMatch(input, pattern, caseSensitive);

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
			try {
				var options = RegexOptions.Singleline;
				if (!caseSensitive)
					options |= RegexOptions.IgnoreCase;
				return Regex.IsMatch(input, pattern, options, TimeSpan.FromSeconds(1));
			}
			catch (ArgumentException) {
				// Invalid regex — treat as no match rather than crashing
				return false;
			}
			catch (RegexMatchTimeoutException) {
				return false;
			}
		}
	}
}
