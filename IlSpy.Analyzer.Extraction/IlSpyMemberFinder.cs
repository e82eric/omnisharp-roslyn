using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.ILSpy;
using ICSharpCode.ILSpy.Analyzers;
using ICSharpCode.ILSpy.Analyzers.Builtin;

namespace IlSpy.Analyzer.Extraction;

public class IlSpyMemberFinder
{
    public IlSpyMetadataSource Run(string fileName, string typeName, string methodName, IReadOnlyList<string> methodParameterTypes, string projectName)
    {
        try
        {
            var cont1 = new AnalyzerContext();
            var assemblyList = new AssemblyList(new List<string>() { fileName });
            cont1.AssemblyList = assemblyList;
            var findAssembly = assemblyList.FindAssembly(fileName);
            var ts = cont1.GetOrCreateTypeSystem(findAssembly.GetPEFileOrNull());

            var findType = ts.FindType(new FullTypeName(typeName)) as ITypeDefinition;

            var method = FindMethod(findType, methodName, methodParameterTypes);

            var rootType = FindContainingType(findType);

            var finder = new MethodInTypeFinder();
            var foundUse = finder.Find(findType, method.MetadataToken, rootType);

            var decompiledFileName = GetFilePathForExternalSymbol(projectName, rootType);

            var metadataSource = new IlSpyMetadataSource()
            {
                AssemblyName = rootType.ParentModule.AssemblyName,
                FileName = decompiledFileName,
                Column = foundUse.StartLocation.Column,
                Language = null,
                Line = foundUse.StartLocation.Line,
                MemberName = findType.ReflectionName,
                SourceLine = foundUse.Statement.ToString().Replace("\r\n", ""),
                SourceText = $"{findType.ReflectionName} {foundUse.Statement.ToString().Replace("\r\n", "")}",
                TypeName = findType.ReflectionName,
                StatementLine = foundUse.StartLocation.Line,
                StartColumn = foundUse.StartLocation.Column,
                EndColumn = foundUse.EndLocation.Column,
                ContainingTypeFullName = rootType.ReflectionName,
                ContainingTypeToken = EntityHandleExposer.Get(rootType),
                AssemblyFilePath = findType.Compilation.MainModule.PEFile.FileName
                //Token = usage.Token
            };

            return metadataSource;

        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    private static ITypeDefinition FindContainingType(ITypeDefinition symbol)
    {
        if (symbol.DeclaringType == null)
        {
            return symbol;
        }
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

    private IMember FindMethod(IType type, string methodName, IReadOnlyList<string> methodParameterTypes)
    {
        var methods = new List<IMember>();

        foreach (var member in type.GetMembers())
        {
            if (member.Name == methodName)
            {
                var method = member as IMethod;

                if (method != null)
                {
                    if (method.Parameters.Count == methodParameterTypes.Count)
                    {
                        var paramsMatch = true;
                        for (int i = 0; i < methodParameterTypes.Count; i++)
                        {
                            if (method.Parameters[i].Type.ReflectionName != methodParameterTypes[i])
                            {
                                paramsMatch = false;
                            }
                        }

                        if (paramsMatch)
                        {
                            methods.Add(member);
                        }
                    }
                }
            }
        }

        // var methods = type.GetMembers().Where(m =>
        // {
        //     if (m.Name != methodName)
        //     {
        //         return false;
        //     }
        //
        //     var method = m as IMethod;
        //
        //     if(method != null)
        //     {
        //         if (method.Parameters.Count != methodParameterTypes.Count)
        //         {
        //             return false;
        //         }
        //
        //         for (int i = 0; i < methodParameterTypes.Count; i++)
        //         {
        //             if (method.Parameters[i].Type.ReflectionName != methodParameterTypes[i])
        //             {
        //                 return false;
        //             }
        //         }
        //     }
        //     else
        //     {
        //         if (m.Name != methodName)
        //         {
        //             return false;
        //         }
        //     }
        //
        //     return true;
        // });

        if (methods.Count > 1)
        {
            foreach (var method in methods)
            {
                if (method.DeclaringType?.Name == type.Name)
                {
                    return method;
                }
            }
        }

        return (IMember)methods.FirstOrDefault();
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
