using System.Collections.Generic;
using System.Reflection.Metadata;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.ILSpy.Analyzers.Builtin;

namespace IlSpy.Analyzer.Extraction;

public class LocalUsageInMethodBodyFinder
{
    public IEnumerable<UsageAsTextLocation> Find(ITypeDefinition rootContainingTypeSymbol, int line, int column)
    {
        var fileName = rootContainingTypeSymbol.ParentModule.PEFile.FileName;
        var cachingDecompiler = DecompilerFactory.Get(fileName);
        var result = new List<UsageAsTextLocation>();

        var syntaxTree = cachingDecompiler.Run(rootContainingTypeSymbol);

        var localNode = syntaxTree.GetNodeAt(line + 1, column + 1);

        var symbolOfLocal = localNode.GetSymbol();

        if (symbolOfLocal != null)
        {
            var entity = symbolOfLocal as IEntity;
            if (entity != null)
            {
                var method = FindMethod(localNode);

                if (method != null)
                {
                    Find(localNode, entity.MetadataToken, result);
                }
            }
        }

        return result;
    }

    private AstNode FindMethod(AstNode node)
    {
        if (node.NodeType == NodeType.Member)
        {
            return node;
        }

        if (node.Parent != null)
        {
            return null;
        }

        return FindMethod(node.Parent);
    }

    private void Find(AstNode node, EntityHandle entityHandleToSearchFor, IList<UsageAsTextLocation> found)
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
                    EndLocation = node.EndLocation,
                    Statement = node.Parent.ToString()
                };

                found.Add(usage);
            }
        }

        foreach (var child in node.Children)
        {
            Find(child, entityHandleToSearchFor, found);
        }
    }
}
