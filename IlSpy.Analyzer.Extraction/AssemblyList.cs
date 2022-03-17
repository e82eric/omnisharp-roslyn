// Copyright (c) 2011 AlphaSierraPapa for the SharpDevelop Team
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
//using System.Windows.Threading;
using System.Xml.Linq;

namespace ICSharpCode.ILSpy
{
    /// <summary>
    /// A list of assemblies.
    /// </summary>
    public sealed class AssemblyList
    {
        readonly object lockObj = new object();

        /// <summary>
        /// The assemblies in this list.
        /// Needs locking for multi-threaded access!
        /// Write accesses are allowed on the GUI thread only (but still need locking!)
        /// </summary>
        /// <remarks>
        /// Technically read accesses need locking when done on non-GUI threads... but whenever possible, use the
        /// thread-safe <see cref="GetAssemblies()"/> method.
        /// </remarks>
        readonly ObservableCollection<LoadedAssembly> assemblies = new ObservableCollection<LoadedAssembly>();

        /// <summary>
        /// Assembly lookup by filename.
        /// Usually byFilename.Values == assemblies; but when an assembly is loaded by a background thread,
        /// that assembly is added to byFilename immediately, and to assemblies only later on the main thread.
        /// </summary>
        readonly Dictionary<string, LoadedAssembly> byFilename = new Dictionary<string, LoadedAssembly>(StringComparer.OrdinalIgnoreCase);

        public AssemblyList(IEnumerable<string> paths)
        {
            foreach (var asm in paths)
            {
                OpenAssembly((string)asm);
            }
        }

        /// <summary>
		/// Gets the loaded assemblies. This method is thread-safe.
		/// </summary>
		public LoadedAssembly[] GetAssemblies()
        {
            lock (lockObj)
            {
                return assemblies.ToArray();
            }
        }

        /// <summary>
        /// Gets all loaded assemblies recursively, including assemblies found in bundles or packages.
        /// </summary>
        public async Task<IList<LoadedAssembly>> GetAllAssemblies()
        {
            var assemblies = GetAssemblies();
            var results = new List<LoadedAssembly>(assemblies.Length);

            foreach (var asm in assemblies)
            {
                LoadedAssembly.LoadResult result;
                try
                {
                    result = await asm.GetLoadResultAsync();
                }
                catch
                {
                    results.Add(asm);
                    continue;
                }
                //if (result.Package != null)
                //{
                //    AddDescendants(result.Package.RootFolder);
                //}
                if (result.PEFile != null)
                {
                    results.Add(asm);
                }
            }

            return results;
        }

        /// <summary>
        /// Find an assembly that was previously opened.
        /// </summary>
        public LoadedAssembly? FindAssembly(string file)
        {
            file = Path.GetFullPath(file);
            lock (lockObj)
            {
                if (byFilename.TryGetValue(file, out var asm))
                    return asm;
            }
            return null;
        }

        public LoadedAssembly OpenAssembly(string file, bool isAutoLoaded = false)
        {
            file = Path.GetFullPath(file);
            return OpenAssembly(file, () =>
            {
                var newAsm = new LoadedAssembly(this, file);
                newAsm.IsAutoLoaded = isAutoLoaded;
                return newAsm;
            });
        }

        LoadedAssembly OpenAssembly(string file, Func<LoadedAssembly> load)
        {
            LoadedAssembly asm;
            lock (lockObj)
            {
                if (byFilename.TryGetValue(file, out asm))
                    return asm;
                asm = load();
                Debug.Assert(asm.FileName == file);
                byFilename.Add(asm.FileName, asm);

                assemblies.Add(asm);
            }

            return asm;
        }
    }
}
