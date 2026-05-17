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
using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Input;
using dnlib.DotNet;
using dnSpy.Contracts.Controls;
using dnSpy.Contracts.Decompiler;
using dnSpy.Contracts.Documents;
using dnSpy.Contracts.Documents.Tabs;
using dnSpy.Contracts.MVVM;
using dnSpy.Contracts.ToolWindows;
using dnSpy.Contracts.ToolWindows.App;
using dnSpy.DeepSearch.Core;
using dnSpy.DeepSearch.Services;
using dnSpy.DeepSearch.UI.ViewModels;

namespace dnSpy.DeepSearch.UI {
	[Export(typeof(IToolWindowContentProvider))]
	sealed class DeepSearchToolWindowContentProvider : IToolWindowContentProvider {
		readonly Lazy<IDeepSearchService> _searchService;
		readonly Lazy<IDocumentTabService> _tabService;
		readonly Lazy<IDsDocumentService> _documentService;
		readonly Lazy<IPickDirectory> _pickDirectory;
		readonly Lazy<IDeepSearchSettings> _deepSearchSettings;

		DeepSearchToolWindowContent? _content;
		DeepSearchToolWindowContent Content =>
			_content ??= new DeepSearchToolWindowContent(_searchService, _tabService, _documentService, _pickDirectory, _deepSearchSettings);

		[ImportingConstructor]
		public DeepSearchToolWindowContentProvider(
			Lazy<IDeepSearchService> searchService,
			Lazy<IDocumentTabService> tabService,
			Lazy<IDsDocumentService> documentService,
			Lazy<IPickDirectory> pickDirectory,
			Lazy<IDeepSearchSettings> deepSearchSettings) {
			_searchService      = searchService;
			_tabService         = tabService;
			_documentService    = documentService;
			_pickDirectory      = pickDirectory;
			_deepSearchSettings = deepSearchSettings;
		}

		public IEnumerable<ToolWindowContentInfo> ContentInfos {
			get { yield return new ToolWindowContentInfo(DeepSearchToolWindowContent.THE_GUID, DeepSearchToolWindowContent.DEFAULT_LOCATION, AppToolWindowConstants.DEFAULT_CONTENT_ORDER_TOP_SEARCH + 1, false); }
		}

		public ToolWindowContent? GetOrCreate(Guid guid) {
			if (guid == DeepSearchToolWindowContent.THE_GUID)
				return Content;
			return null;
		}
	}

	sealed class DeepSearchToolWindowContent : ToolWindowContent, IFocusable {
		public static readonly Guid THE_GUID = new Guid("A1B2C3D4-E5F6-7890-ABCD-EF1234567890");
		public const AppToolWindowLocation DEFAULT_LOCATION = AppToolWindowLocation.DefaultHorizontal;

		public override Guid Guid    => THE_GUID;
		public override string Title => "Deep Search";

		public override object? UIObject      => _control;
		public override IInputElement? FocusedElement => _control.SearchBox;
		public override FrameworkElement? ZoomElement  => _control;

		public bool CanFocus => true;
		public void Focus()  => _control.FocusSearchBox();

		readonly DeepSearchControl _control;
		readonly DeepSearchViewModel _vm;
		readonly Lazy<IDocumentTabService> _tabService;
		readonly Lazy<IDsDocumentService> _documentService;

		public DeepSearchToolWindowContent(
			Lazy<IDeepSearchService> searchService,
			Lazy<IDocumentTabService> tabService,
			Lazy<IDsDocumentService> documentService,
			Lazy<IPickDirectory> pickDirectory,
			Lazy<IDeepSearchSettings> deepSearchSettings) {
			_tabService      = tabService;
			_documentService = documentService;

			_vm = new DeepSearchViewModel(searchService.Value, pickDirectory.Value, deepSearchSettings.Value);
			_control = new DeepSearchControl { DataContext = _vm };
			_control.NavigateRequested += (s, e) => NavigateToSelectedResult();
		}

		void NavigateToSelectedResult() {
			var result = _vm.SelectedResult?.Result;
			if (result is null)
				return;

			// If the DLL is not yet loaded, load it first then navigate
			if (result.TokenProvider is MethodDef method) {
				EnsureLoaded(result.AssemblyPath);
				_tabService.Value.FollowReference(method, false, true, args => {
					// For string literal hits, scroll to the IL offset
					if (!args.HasMovedCaret && args.Success && result.ILOffset is uint offset)
						args.HasMovedCaret = TryGoToILOffset(args.Tab, method, offset);
				});
			}
			else if (result.TokenProvider is IMDTokenProvider token) {
				EnsureLoaded(result.AssemblyPath);
				_tabService.Value.FollowReference(token);
			}
		}

		void EnsureLoaded(string assemblyPath) {
			if (string.IsNullOrEmpty(assemblyPath))
				return;
			// TryGetOrCreate adds the document to the list if not already present
			_documentService.Value.TryGetOrCreate(DsDocumentInfo.CreateDocument(assemblyPath), isAutoLoaded: true);
		}

		static bool TryGoToILOffset(IDocumentTab tab, MethodDef method, uint ilOffset) {
			var docViewer = tab.TryGetDocumentViewer();
			if (docViewer is null)
				return false;
			var debugSvc = docViewer.GetMethodDebugService();
			var stmt = debugSvc.FindByCodeOffset(method, ilOffset);
			if (stmt is null)
				return false;
			docViewer.MoveCaretToPosition(stmt.Value.Statement.TextSpan.Start);
			return true;
		}
	}
}
