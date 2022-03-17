using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.ILSpy;
using ICSharpCode.ILSpy.Analyzers;
using ICSharpCode.ILSpy.Analyzers.Builtin;

namespace IlSpy.Analyzer.Extraction;

public class IlSpyLocalUsagesFinder
{
    public IEnumerable<IlSpyMetadataSource> Run(string fileName, string typeName, int line, int column, string projectName)
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
            var typeDefinition = findType as ITypeDefinition;

            var typeUseInMethodFinder = new LocalUsageInMethodBodyFinder();
            var foundUses = typeUseInMethodFinder.Find(typeDefinition, line, column);

            foreach (var foundUse in foundUses)
            {
                var rootType = TypeSystemHelper.FindContainingType(typeDefinition);

                var decompileFileName = TypeSystemHelper.GetFilePathForExternalSymbol(projectName, rootType);
                //var fileName = GetFilePathForExternalSymbol(projectName, parentType);
                var metadataSource = new IlSpyMetadataSource()
                {
                    AssemblyName = typeDefinition.ParentModule.AssemblyName,
                    FileName = decompileFileName,
                    Column = foundUse.StartLocation.Column,
                    Language = null,
                    Line = foundUse.StartLocation.Line,
                    MemberName = foundUse.Statement,
                    ProjectName = projectName,
                    SourceLine = foundUse.Statement.ToString().Replace("\r\n", ""),
                    SourceText = $"{foundUse.Statement} {foundUse.Statement.ToString().Replace("\r\n", "")}",
                    TypeName = typeDefinition.DeclaringType.ReflectionName,
                    StatementLine = foundUse.StartLocation.Line,
                    StartColumn = foundUse.StartLocation.Column,
                    EndColumn = foundUse.EndLocation.Column,
                    ContainingTypeFullName = typeDefinition.ReflectionName,
                    ContainingTypeToken = EntityHandleExposer.Get(typeDefinition),
                    AssemblyFilePath = typeDefinition.Compilation.MainModule.PEFile.FileName
                    //Token = usage.Token
                };

                result.Add(metadataSource);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }

        return result;
    }
}
