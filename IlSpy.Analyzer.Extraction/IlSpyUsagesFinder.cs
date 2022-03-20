using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.ILSpy;
using ICSharpCode.ILSpy.Analyzers;
using ICSharpCode.ILSpy.Analyzers.Builtin;

namespace IlSpy.Analyzer.Extraction
{
    public class IlSpyUsagesFinder
    {
        public IEnumerable<IlSpyMetadataSource> Run(string fileName, string typeName, string projectName, IEnumerable<string> otherReferences)
        {
            var result = new List<IlSpyMetadataSource>();

            try
            {
                var cont1 = new AnalyzerContext();
                var enumerable = new List<string>() { fileName };
                enumerable.AddRange(otherReferences);
                var assemblyList = new AssemblyList(enumerable);
                cont1.AssemblyList = assemblyList;
                var findAssembly = assemblyList.FindAssembly(fileName);
                var ts = cont1.GetOrCreateTypeSystem(findAssembly.GetPEFileOrNull());

                var findType = ts.FindType(new FullTypeName(typeName));

                var typeUsedByAnalyzer = new TypeUsedByAnalyzer();

                var typeUsedByResult = typeUsedByAnalyzer.Analyze((ITypeDefinition)findType, true, cont1).ToList();

                var types = typeUsedByResult.Where(r => r.SymbolKind == SymbolKind.TypeDefinition);

                var typeGroups = types.GroupBy(m =>
                {
                    var parentType = FindContainingType((ITypeDefinition)m);
                    return parentType.ReflectionName;
                });

                var sw1 = Stopwatch.StartNew();
                Parallel.ForEach(typeGroups, new ParallelOptions { MaxDegreeOfParallelism = 3 }, groups =>
                {
                    foreach (var type in groups)
                    {
                        AddTypeDefinitionToResult((ITypeDefinition)type, projectName, result, (ITypeDefinition)findType, cont1);
                    }
                });
                sw1.Stop();
                Debug.WriteLine(sw1.Elapsed);

                // foreach (var type in types)
                // {
                //     AddTypeDefinitionToResult((ITypeDefinition)type, projectName, result, (ITypeDefinition)findType, cont1);
                // }

                var methods = typeUsedByResult.Where(r => r.SymbolKind == SymbolKind.Method);

                var groups = methods.GroupBy(m =>
                {
                    var parentType = FindContainingType((IMethod)m);
                    return parentType.ReflectionName;
                });

                var sw = Stopwatch.StartNew();
                Parallel.ForEach(groups, new ParallelOptions { MaxDegreeOfParallelism = 3 }, group =>
                {
                    foreach (var symbol in group)
                    {
                        AddMethodDefinitionToResult((IMethod)symbol, projectName, result, (ITypeDefinition)findType, cont1);
                    }
                });
                sw.Stop();
                Debug.WriteLine(sw.Elapsed);

                // foreach (var method in methods)
                // {
                //     AddMethodDefinitionToResult((IMethod)method, projectName, result, (ITypeDefinition)findType, cont1);
                // }

                var stuffThatNeedsToBeSearched = methods.Where(m =>
                    ((IMethod)m).ReturnType.ReflectionName != findType.ReflectionName &&
                    !((IMethod)m).Parameters.Any(p => p.Type.ReflectionName == findType.ReflectionName));

                foreach (var methodToSearch in stuffThatNeedsToBeSearched)
                {
                    var method = methodToSearch as IMethod;
                    var rootType = FindContainingType(method);
                    if (method != null)
                    {
                        var typeUseInMethodFinder = new TypeInMethodBodyFinder();
                        var foundUses = typeUseInMethodFinder.Find(method, ((ITypeDefinition)findType).MetadataToken, rootType);

                        foreach (var foundUse in foundUses)
                        {
                            var parentType = FindContainingType(method);
                            if (parentType != null)
                            {
                                //var fileName = GetFilePathForExternalSymbol(projectName, parentType);
                                var metadataSource = new IlSpyMetadataSource()
                                {
                                    AssemblyName = parentType.ParentModule.AssemblyName,
                                    FileName = fileName,
                                    Column = foundUse.StartLocation.Column,
                                    Language = null,
                                    Line = foundUse.StartLocation.Line,
                                    MemberName = method.ReflectionName,
                                    ProjectName = projectName,
                                    SourceLine = foundUse.Statement.ToString().Replace("\r\n", ""),
                                    SourceText = $"{method.ReflectionName} {foundUse.Statement.ToString().Replace("\r\n", "")}",
                                    TypeName = method.DeclaringType.ReflectionName,
                                    StatementLine = foundUse.StartLocation.Line,
                                    StartColumn = foundUse.StartLocation.Column,
                                    EndColumn = foundUse.EndLocation.Column,
                                    ContainingTypeFullName = parentType.ReflectionName,
                                    ContainingTypeToken = EntityHandleExposer.Get(parentType),
                                    AssemblyFilePath = method.Compilation.MainModule.PEFile.FileName
                                    //Token = usage.Token
                                };

                                result.Add(metadataSource);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            return result;
        }

        private static void AddTypeDefinitionToResult(ITypeDefinition symbol, string projectName, IList<IlSpyMetadataSource> result, IEntity typeToSearchFor, AnalyzerContext context)
        {
            var parentType = FindContainingType(symbol);

            if (symbol.ParentModule.AssemblyName != projectName)
            {
                var finder = new TypeUsedInTypeFinder();
                var usagesInTypeDefintions = finder.Find2(parentType, typeToSearchFor.MetadataToken, context);

                foreach (var usage in usagesInTypeDefintions)
                {
                    var fileName = GetFilePathForExternalSymbol(projectName, parentType);
                    var metadataSource = new IlSpyMetadataSource
                    {
                        AssemblyName = symbol.ParentModule.AssemblyName,
                        FileName = fileName,
                        Column = usage.StartLocation.Column,
                        Language = null,
                        Line = usage.StartLocation.Line,
                        MemberName = symbol.ReflectionName,
                        ProjectName = projectName,
                        SourceLine = symbol.FullName,
                        SourceText = symbol.ReflectionName,
                        TypeName = symbol.FullName,
                        ContainingTypeToken = EntityHandleExposer.Get(parentType),
                        //Token = EntityHandleExposer.Get(usage),
                        MethodToken = 0,
                        StartColumn = usage.StartLocation.Column,
                        EndColumn = usage.EndLocation.Column,
                        ContainingTypeFullName = parentType.ReflectionName,
                        AssemblyFilePath = symbol.Compilation.MainModule.PEFile.FileName
                    };

                    result.Add(metadataSource);
                }
            }
        }

        private static void AddMethodDefinitionToResult(IMethod symbol, string projectName, IList<IlSpyMetadataSource> result, IEntity typeToSearchFor, AnalyzerContext context)
        {
            var parentType = FindContainingType(symbol);

            if (parentType != null)
            {
                if (symbol.ParentModule.AssemblyName != projectName)
                {
                    var newFinder = new TypeInMethodDefinitionFinder();
                    var rr = newFinder.Find(parentType, symbol, typeToSearchFor.MetadataToken, context);

                    foreach (var r in rr)
                    {
                        var entityHandle = EntityHandleExposer.Get(symbol);
                        var fileName = GetFilePathForExternalSymbol(projectName, parentType);
                        var containingTypeToken = EntityHandleExposer.Get(symbol.DeclaringTypeDefinition);
                        var metadataSource = new IlSpyMetadataSource()
                        {
                            AssemblyName = symbol.ParentModule.AssemblyName,
                            FileName = fileName,
                            Column = r.StartLocation.Column,
                            Language = null,
                            Line = r.StartLocation.Line,
                            MemberName = symbol.ReflectionName,
                            ProjectName = projectName,
                            SourceLine = symbol.FullName,
                            SourceText = symbol.ReflectionName,
                            TypeName = symbol.DeclaringType.FullName,
                            //Token = EntityHandleExposer.Get(r),
                            MethodToken = entityHandle,
                            ContainingTypeToken = containingTypeToken,
                            StartColumn = r.StartLocation.Column,
                            EndColumn = r.EndLocation.Column,
                            ContainingTypeFullName = parentType.ReflectionName,
                            AssemblyFilePath = symbol.Compilation.MainModule.PEFile.FileName
                        };

                        result.Add(metadataSource);
                    }
                }
            }
        }

        private static ITypeDefinition FindContainingType(ITypeDefinition symblol)
        {
            IType result = symblol;

            while (result.DeclaringType != null)
            {
                result = result.DeclaringType;
            }

            if (result is ParameterizedType)
            {
                return null;
            }
            return (ITypeDefinition)result;
        }

        private static ITypeDefinition FindContainingType(IMethod symbol)
        {
            IType result = symbol.DeclaringType;

            while (result.DeclaringType != null)
            {
                result = result.DeclaringType;
            }

            if (result is ParameterizedType)
            {
                return null;
            }

            return (ITypeDefinition)result;
        }

        internal static string GetFilePathForExternalSymbol(string projectName, ITypeDefinition topLevelSymbol)
        {
            return $"$metadata$/Project/{Folderize(projectName)}/Assembly/{Folderize(topLevelSymbol.Compilation.MainModule.AssemblyName)}/Symbol/{Folderize(GetTypeDisplayString(topLevelSymbol))}.cs".Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        }

        private static string GetTypeDisplayString(ITypeDefinition symbol)
        {
            return symbol.Name;
        }

        private static string Folderize(string path) => string.Join("/", path.Split('.'));
    }
}
