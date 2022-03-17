using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.ILSpy;
using ICSharpCode.ILSpy.Analyzers;
using ICSharpCode.ILSpy.Analyzers.Builtin;


namespace IlSpy.Analyzer.Extraction
{
    public class IlSpyPropertyUsagesFinder
    {
        public IEnumerable<IlSpyMetadataSource> Run(string fileName, string typeName, string methodName, string projectName)
        {
            var result = new List<IlSpyMetadataSource>();

            try
            {
                var context = new AnalyzerContext();
                var assemblyList = new AssemblyList(new List<string>() { fileName });
                context.AssemblyList = assemblyList;
                var findAssembly = assemblyList.FindAssembly(fileName);
                var ts = context.GetOrCreateTypeSystem(findAssembly.GetPEFileOrNull());

                var findType = ts.FindType(new FullTypeName(typeName));

                var foundProperty = FindProperty(findType, methodName);

                var propertyUsedByAnalyzer = new PropertyUsedByAnalyzer();
                var typeUsedByResult = propertyUsedByAnalyzer.Analyze(foundProperty, context).ToList();

                foreach (var usageToSearch in typeUsedByResult)
                {
                    var method = usageToSearch as IMethod;
                    var rootType = FindContainingType(method);
                    if (method != null)
                    {
                        var typeUseInMethodFinder = new PropertyInMethodBodyFinder();
                        var foundUses = typeUseInMethodFinder.Find(method, foundProperty.Setter?.MetadataToken, foundProperty.Getter?.MetadataToken, rootType, context);

                        foreach (var foundUse in foundUses)
                        {
                            var parentType = FindContainingType(method);
                            if (parentType != null)
                            {
                                var decompileFileName =
                                    TypeSystemHelper.GetFilePathForExternalSymbol(projectName, parentType);
                                //var fileName = GetFilePathForExternalSymbol(projectName, parentType);
                                var metadataSource = new IlSpyMetadataSource()
                                {
                                    AssemblyName = parentType.ParentModule.AssemblyName,
                                    FileName = decompileFileName,
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
                    //
                    // var methodUsesAnalyzer = new MethodUsesAnalyzer();
                    // var localUses = methodUsesAnalyzer.Analyze((ITypeDefinition)findType, methodToSearch, cont1).ToList();
                    //
                    // foreach (var localUse in localUses)
                    // {
                    //     AddLocalsToResult(localUse, projectName, result);
                    // }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            return result;
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

        private IProperty FindProperty(IType type, string methodName)
        {
            var properties = type.GetProperties().Where(m =>
            {
                if (m.Name != methodName)
                {
                    return false;
                }

                return true;
            });

            return (IProperty)properties.FirstOrDefault();
        }
    }
}
