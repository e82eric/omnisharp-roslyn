using System.Reflection.Metadata;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.TypeSystem;
using IlSpy.Analyzer.Extraction;

namespace ICSharpCode.ILSpy.Analyzers.Builtin;

public class MethodFinder
{
    public UsageAsTextLocation Find(IMethod symbol, EntityHandle typeEntityHandle, ITypeDefinition rootType)
    {
        var fileName = symbol.Compilation.MainModule.PEFile.FileName;
        var cachingDecompiler = DecompilerFactory.Get(fileName);

        var syntaxTree = cachingDecompiler.Run(rootType);

        var methodBodyNode = FindMethodBody(syntaxTree, symbol.MetadataToken);

        var usage = new UsageAsTextLocation()
        {
            TypeEntityHandle = typeEntityHandle,
            StartLocation = methodBodyNode.StartLocation,
            EndLocation = methodBodyNode.EndLocation,
            Statement = methodBodyNode.ToString()
        };

        return usage;
    }

    private AstNode FindMethodBody(AstNode node, EntityHandle entityHandle)
    {
        if (node.NodeType == NodeType.Member)
        {
            var symbol = node.GetSymbol();
            var entity = symbol as IEntity;
            if (entity != null)
            {
                if (entity.MetadataToken == entityHandle)
                {
                    var method = node as MethodDeclaration;

                    if (method != null)
                    {
                        return method;
                    }

                    return null;
                }
            }
        }

        if (node.HasChildren)
        {
            foreach (var child in node.Children)
            {
                var method = FindMethodBody(child, entityHandle);
                if (method != null)
                {
                    return method;
                }
            }
        }

        return null;
    }
}
