using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.ILSpy;
using ICSharpCode.ILSpy.Analyzers;
using ICSharpCode.ILSpy.Analyzers.Builtin;

namespace IlSpy.Analyzer.Extraction
{
    public class IlSpyBaseTypeUsageFinder
    {
        public IEnumerable<IlSpyMetadataSource> Run(string fileName, string typeName, string projectName)
        {
            var result = new List<IlSpyMetadataSource>();

            try
            {
                var cont1 = new AnalyzerContext();
                var assemblyList = new AssemblyList(new List<string>() { fileName });
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

                foreach (var type in types)
                {
                    AddTypeDefinitionToResult((ITypeDefinition)type, projectName, result, (ITypeDefinition)findType, cont1);
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
