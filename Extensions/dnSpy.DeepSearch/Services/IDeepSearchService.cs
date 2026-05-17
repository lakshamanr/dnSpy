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
using dnSpy.DeepSearch.Core;

namespace dnSpy.DeepSearch.Services {
	public interface IDeepSearchService {
		bool IsSearching { get; }

		/// <summary>Starts a new search with the given options. No-ops if already running.</summary>
		void StartSearch(DeepSearchOptions options);

		/// <summary>Cancels any running search.</summary>
		void CancelSearch();

		event EventHandler<DeepSearchGroupFoundEventArgs>? GroupFound;
		event EventHandler<DeepSearchCompletedEventArgs>? SearchCompleted;

		/// <summary>Fires with a human-readable status line (current DLL + progress).</summary>
		event EventHandler<string>? StatusChanged;
	}
}
