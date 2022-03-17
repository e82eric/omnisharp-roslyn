using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.ILSpy;
using ICSharpCode.ILSpy.Analyzers;
using ICSharpCode.ILSpy.Analyzers.Builtin;

namespace IlSpy.Analyzer.Extraction;

public class IlSpyMethodImplementationFinder
{
    public IEnumerable<IlSpyMetadataSource> Run(string fileName, string typeName, string methodName, IReadOnlyList<string> parameters, string projectName)
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

            var methods = findType.GetMembers().Where(m =>
            {
                if (m.Name != methodName)
                {
                    return false;
                }

                var method = m as IMethod;

                if(method != null)
                {
                    if (method.Parameters.Count != parameters.Count)
                    {
                        return false;
                    }

                    for (int i = 0; i < parameters.Count; i++)
                    {
                        if (method.Parameters[i].Type.ReflectionName != parameters[i])
                        {
                            return false;
                        }
                    }
                }

                return true;
            });

            foreach (var method in methods)
            {
                var methodSymbol = method as IMethod;

                var typeUsedByAnalyzer = new MethodImplementedByAnalyzer();

                var implementations = typeUsedByAnalyzer.Analyze(methodSymbol, cont1).ToList();

                foreach (var implementation in implementations)
                {
                    AddTypeDefinitionToResult((ITypeDefinition)implementation.DeclaringType, implementation, projectName, result, implementation, cont1);
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

    private static void AddTypeDefinitionToResult(ITypeDefinition symbol, IMethod method, string projectName,
        IList<IlSpyMetadataSource> result, IEntity typeToSearchFor, AnalyzerContext context)
    {
        var parentType = FindContainingType(symbol);

        if (symbol.ParentModule.AssemblyName != projectName)
        {
            var finder = new MethodFinder();
            var usage = finder.Find(method, parentType.MetadataToken, parentType);

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
        return
            $"$metadata$/Project/{Folderize(projectName)}/Assembly/{Folderize(topLevelSymbol.Compilation.MainModule.AssemblyName)}/Symbol/{Folderize(GetTypeDisplayString(topLevelSymbol))}.cs"
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    }

    private static string GetTypeDisplayString(ITypeDefinition symbol)
    {
        return symbol.Name;
    }

    private static string Folderize(string path) => string.Join("/", path.Split('.'));
}

