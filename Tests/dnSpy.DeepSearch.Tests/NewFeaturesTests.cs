/*
    Copyright (C) 2024 dnSpy Contributors

    Tests for features added in v0.4: Attributes, Inheritance, ILOpcodes, TypeFilter,
    FindCallers, FolderScanner.SafeEnumeratePeFiles, and zip scanning.
*/

using System;
using System.IO;
using System.Linq;
using System.Threading;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnSpy.DeepSearch.Core;
using Xunit;

namespace dnSpy.DeepSearch.Tests {
    public class NewFeaturesTests {
        static ModuleDef BuildExtendedModule() {
            var mod = new ModuleDefUser("ExtendedAssembly");
            var asm = new AssemblyDefUser("ExtendedAssembly");
            asm.Modules.Add(mod);
            var corLib  = mod.CorLibTypes;
            var objRef  = corLib.Object.TypeDefOrRef;
            var asmRef  = mod.CorLibTypes.AssemblyRef;

            var iLogger = new TypeDefUser("Ext", "ILogger", null);
            iLogger.Attributes |= TypeAttributes.Interface | TypeAttributes.Abstract;
            mod.Types.Add(iLogger);

            var baseService = new TypeDefUser("Ext", "BaseService", objRef);
            mod.Types.Add(baseService);

            var myType = new TypeDefUser("Ext", "MyController", baseService);
            myType.Interfaces.Add(new InterfaceImplUser(iLogger));
            var obsoleteCtor = new MemberRefUser(mod, ".ctor",
                MethodSig.CreateInstance(corLib.Void),
                new TypeRefUser(mod, "System", "ObsoleteAttribute", asmRef));
            myType.CustomAttributes.Add(new CustomAttribute(obsoleteCtor));
            mod.Types.Add(myType);

            var streamRef = new TypeRefUser(mod, "System.IO", "Stream", asmRef);
            var method = new MethodDefUser("GetData",
                MethodSig.CreateInstance(corLib.String, new ClassSig(streamRef)),
                MethodAttributes.Public);
            myType.Methods.Add(method);
            method.Body = new CilBody();
            method.Body.Instructions.Add(OpCodes.Ldnull.ToInstruction());
            method.Body.Instructions.Add(OpCodes.Ret.ToInstruction());

            return mod;
        }

        static ILookup<ResultKind, DeepSearchResult> RunSearch(ModuleDef mod, DeepSearchOptions opts) =>
            AssemblyDeepSearcher
                .Search(mod, "ext.dll", "ExtendedAssembly", opts, CancellationToken.None)
                .ToLookup(r => r.Kind);

        // Attributes scope
        [Fact]
        public void AttributeScope_FindsObsoleteOnType() {
            var opts = new DeepSearchOptions { SearchTerm = "Obsolete", Scope = SearchScope.Attributes };
            var results = RunSearch(BuildExtendedModule(), opts);
            Assert.NotEmpty(results[ResultKind.Attribute]);
            Assert.Contains(results[ResultKind.Attribute], r => r.Name.Contains("Obsolete"));
        }

        [Fact]
        public void AttributeScope_NothingFoundForUnknownAttribute() {
            var opts = new DeepSearchOptions { SearchTerm = "NonExistentAttr", Scope = SearchScope.Attributes };
            Assert.Empty(RunSearch(BuildExtendedModule(), opts)[ResultKind.Attribute]);
        }

        // Inheritance scope
        [Fact]
        public void InheritanceScope_FindsByBaseClass() {
            var opts = new DeepSearchOptions { SearchTerm = "BaseService", Scope = SearchScope.Inheritance };
            var results = RunSearch(BuildExtendedModule(), opts);
            Assert.Contains(results[ResultKind.Type], r => r.Name == "MyController");
        }

        [Fact]
        public void InheritanceScope_FindsByInterface() {
            var opts = new DeepSearchOptions { SearchTerm = "ILogger", Scope = SearchScope.Inheritance };
            var results = RunSearch(BuildExtendedModule(), opts);
            Assert.Contains(results[ResultKind.Type], r => r.Name == "MyController");
        }

        // ILOpcodes scope
        [Fact]
        public void ILOpcodesScope_FindsLdnull() {
            var opts = new DeepSearchOptions {
                SearchTerm   = "*",
                OpcodeFilter = "ldnull",
                Scope        = SearchScope.ILOpcodes,
                MatchMode    = MatchMode.Wildcard,
            };
            var results = RunSearch(BuildExtendedModule(), opts);
            Assert.NotEmpty(results[ResultKind.ILInstruction]);
        }

        [Fact]
        public void ILOpcodesScope_NoMatchForAbsentOpcode() {
            var opts = new DeepSearchOptions {
                SearchTerm   = "*",
                OpcodeFilter = "call",
                Scope        = SearchScope.ILOpcodes,
                MatchMode    = MatchMode.Wildcard,
            };
            Assert.Empty(RunSearch(BuildExtendedModule(), opts)[ResultKind.ILInstruction]);
        }

        // TypeFilter
        [Fact]
        public void TypeFilter_NarrowsMethodByReturnType() {
            var opts = new DeepSearchOptions {
                SearchTerm = "GetData", Scope = SearchScope.Methods, TypeFilter = "String",
            };
            Assert.Single(RunSearch(BuildExtendedModule(), opts)[ResultKind.Method]);
        }

        [Fact]
        public void TypeFilter_ExcludesMethodWhenNoMatch() {
            var opts = new DeepSearchOptions {
                SearchTerm = "GetData", Scope = SearchScope.Methods, TypeFilter = "XYZNoSuchType",
            };
            Assert.Empty(RunSearch(BuildExtendedModule(), opts)[ResultKind.Method]);
        }

        [Fact]
        public void TypeFilter_Empty_DoesNotFilter() {
            var opts = new DeepSearchOptions {
                SearchTerm = "GetData", Scope = SearchScope.Methods, TypeFilter = "",
            };
            Assert.Single(RunSearch(BuildExtendedModule(), opts)[ResultKind.Method]);
        }

        // DiffKey stability
        [Fact]
        public void DiffKey_IsDeterministicAcrossRuns() {
            var opts = new DeepSearchOptions { SearchTerm = "MyController", Scope = SearchScope.Types };
            string Key() => AssemblyDeepSearcher
                .Search(BuildExtendedModule(), "ext.dll", "ExtendedAssembly", opts, CancellationToken.None)
                .First().DiffKey;
            Assert.Equal(Key(), Key());
        }

        // FolderScanner.SafeEnumeratePeFiles
        [Fact]
        public void SafeEnumeratePeFiles_FindsDllsAndExes() {
            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(dir);
            try {
                File.WriteAllText(Path.Combine(dir, "a.dll"), "dummy");
                File.WriteAllText(Path.Combine(dir, "b.exe"), "dummy");
                File.WriteAllText(Path.Combine(dir, "c.txt"), "dummy");
                var found = FolderScanner.SafeEnumeratePeFiles(dir, recurse: false).ToList();
                Assert.Equal(2, found.Count);
            }
            finally { Directory.Delete(dir, recursive: true); }
        }

        [Fact]
        public void SafeEnumeratePeFiles_RecursesSubdirectories() {
            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var sub = Path.Combine(dir, "sub");
            Directory.CreateDirectory(sub);
            try {
                File.WriteAllText(Path.Combine(dir, "root.dll"), "dummy");
                File.WriteAllText(Path.Combine(sub,  "leaf.dll"), "dummy");
                Assert.Equal(2, FolderScanner.SafeEnumeratePeFiles(dir, recurse: true).Count());
                Assert.Single(FolderScanner.SafeEnumeratePeFiles(dir, recurse: false));
            }
            finally { Directory.Delete(dir, recursive: true); }
        }

        [Fact]
        public void SafeEnumeratePeFiles_NonExistentDir_ReturnsEmpty() {
            var nonExistent = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var result = FolderScanner.SafeEnumeratePeFiles(nonExistent, recurse: false).ToList();
            Assert.Empty(result);
        }
    }
}
