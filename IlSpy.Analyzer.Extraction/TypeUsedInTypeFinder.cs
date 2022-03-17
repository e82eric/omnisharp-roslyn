using System.Collections.Generic;
using System.Reflection.Metadata;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.ILSpy.Analyzers;
using ICSharpCode.ILSpy.Analyzers.Builtin;

namespace IlSpy.Analyzer.Extraction;

public class TypeUsedInTypeFinder
{
    public IEnumerable<IEntity> Find(ITypeDefinition symbol, EntityHandle typeEntityHandle, AnalyzerContext context)
    {
        var _cachingDecompiler = DecompilerFactory.Get(symbol.ParentModule.PEFile.FileName);

        var syntaxTree = _cachingDecompiler.Run(symbol);

        var result = new List<IEntity>();
        Find(syntaxTree, typeEntityHandle, result);

        return result;
    }

    public IEnumerable<UsageAsTextLocation> Find2(ITypeDefinition symbol, EntityHandle typeEntityHandle, AnalyzerContext context)
    {
        var fileName = symbol.Compilation.MainModule.PEFile.FileName;
        var _cachingDecompiler = DecompilerFactory.Get(fileName);
        var syntaxTree = _cachingDecompiler.Run(symbol);

        var result = new List<UsageAsTextLocation>();
        Find2(syntaxTree, typeEntityHandle, result);

        return result;
    }

    private void Find(AstNode node, EntityHandle entityHandleToSearchFor, IList<IEntity> found)
    {
        if (node.Role == Roles.BaseType)
        {
            var symbol = node.GetSymbol();

            var entity = symbol as IEntity;
            if (entity != null)
            {
                if (entity.MetadataToken == entityHandleToSearchFor)
                {
                    found.Add(entity);
                }
            }
        }

        foreach (var child in node.Children)
        {
            Find(child, entityHandleToSearchFor, found);
        }
    }

    private void Find2(AstNode node, EntityHandle entityHandleToSearchFor, IList<UsageAsTextLocation> found)
    {
        if (node.Role == Roles.BaseType)
        {
            var symbol = node.GetSymbol();

            var entity = symbol as IEntity;
            if (entity != null)
            {
                if (entity.MetadataToken == entityHandleToSearchFor)
                {
                    var usage = new UsageAsTextLocation()
                    {
                        TypeEntityHandle = entityHandleToSearchFor,
                        StartLocation = node.StartLocation,
                        EndLocation = node.EndLocation
                    };
                    found.Add(usage);
                }
            }
        }

        foreach (var child in node.Children)
        {
            Find2(child, entityHandleToSearchFor, found);
        }
    }
}
