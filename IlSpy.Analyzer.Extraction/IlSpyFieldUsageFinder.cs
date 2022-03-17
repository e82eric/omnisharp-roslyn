using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.ILSpy;
using ICSharpCode.ILSpy.Analyzers;
using ICSharpCode.ILSpy.Analyzers.Builtin;

namespace IlSpy.Analyzer.Extraction;

public class IlSpyFieldUsagesFinder
{
    public IEnumerable<IlSpyMetadataSource> Run(string fileName, string typeName, string fieldName, string projectName)
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

            var foundMethod = FindField(findType, fieldName);

            var methodUsedByAnalyzer = new FieldAccessAnalyzer(true);
            var typeUsedByResult = methodUsedByAnalyzer.Analyze(foundMethod, cont1).ToList();

            foreach (var methodToSearch in typeUsedByResult)
            {
                var method = methodToSearch as IMethod;
                var rootType = FindContainingType(method);
                if (method != null)
                {
                    var typeUseInMethodFinder = new MethodInMethodBodyFinder();
                    var foundUses = typeUseInMethodFinder.Find(method, foundMethod.MetadataToken, rootType);

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

    private IField FindField(IType type, string fieldName)
    {
        var methods = type.GetFields().Where(m =>
        {
            if (m.Name != fieldName)
            {
                return false;
            }

            return true;
        });

        return methods.FirstOrDefault();
    }
}
