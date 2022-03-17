using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection.Metadata;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.OutputVisitor;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.ILSpy;
using ICSharpCode.ILSpy.Analyzers;

namespace IlSpy.Analyzer.Extraction;

public class CachingDecompiler
{
    private static readonly ConcurrentDictionary<(string, EntityHandle), Tuple<SyntaxTree, string>> SyntaxTrees = new();

    private int _cacheHits;
    private readonly DecompilerTypeSystem _ts;

    public CachingDecompiler(string fileName)
    {
        _cacheHits = 0;

        var cont1 = new AnalyzerContext();
        var assemblyList = new AssemblyList(new List<string>() { fileName });
        cont1.AssemblyList = assemblyList;
        var findAssembly = assemblyList.FindAssembly(fileName);
        _ts = cont1.GetOrCreateTypeSystem(findAssembly.GetPEFileOrNull());
    }

    public ITypeDefinition FindTypeDefinition(string fullTypeName)
    {
        var findType = _ts.FindType(new FullTypeName(fullTypeName)) as ITypeDefinition;

        return findType;
    }

    public Tuple<SyntaxTree, string> Run2(ITypeDefinition rooTypeDefinition)
    {
        var assemblyFilePath = rooTypeDefinition.ParentModule.PEFile.FileName;
        var rootTypeDefinitionHandle = rooTypeDefinition.MetadataToken;

        if (SyntaxTrees.TryGetValue((assemblyFilePath, rootTypeDefinitionHandle), out var result))
        {
            _cacheHits++;
            Debug.WriteLine($"Cache hits {_cacheHits}");
            Console.WriteLine($"Cache hits {_cacheHits}");
            return result;
        }

        var decompilerSettings = new DecompilerSettings();
        var decompiler = new CSharpDecompiler(_ts, decompilerSettings);

        var stringWriter = new StringWriter();
        var tokenWriter = TokenWriter.CreateWriterThatSetsLocationsInAST(stringWriter, "  ");

        var syntaxTree = decompiler.DecompileTypes(new[] { (TypeDefinitionHandle)rootTypeDefinitionHandle });
        syntaxTree.AcceptVisitor(new CSharpOutputVisitor(tokenWriter, decompilerSettings.CSharpFormattingOptions));

        var source = stringWriter.ToString();
        var tuple = new Tuple<SyntaxTree, string>(syntaxTree, source);
        SyntaxTrees.TryAdd((assemblyFilePath, rootTypeDefinitionHandle), tuple);

        return tuple;
    }

    public SyntaxTree Run(ITypeDefinition rooTypeDefinition)
    {
        var tuplee = Run2(rooTypeDefinition);

        return tuplee.Item1;
    }

    public Tuple<SyntaxTree, string> Run2(string fullTypeName)
    {
        var findType = _ts.FindType(new FullTypeName(fullTypeName)) as ITypeDefinition;

        var result = Run2(findType);

        return result;
    }

    public SyntaxTree Run(string fullTypeName)
    {
        var result = this.Run2(fullTypeName);

        return result.Item1;
    }

    public string RunToString(string fullTypeName)
    {
        var syntaxTree = Run2(fullTypeName);
        return syntaxTree.Item2;
    }

}
