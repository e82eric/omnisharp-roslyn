using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.Semantics;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.ILSpy.Analyzers.Builtin;

namespace IlSpy.Analyzer.Extraction;

public class TypeInMethodBodyFinder
{
    public IEnumerable<UsageAsTextLocation> Find(IMethod symbol, EntityHandle typeEntityHandle, ITypeDefinition rootType)
    {
        var fileName = symbol.Compilation.MainModule.PEFile.FileName;
        var cachingDecompiler = DecompilerFactory.Get(fileName);
        var result = new List<UsageAsTextLocation>();

        var syntaxTree = cachingDecompiler.Run(rootType);

        var methodBodyNode = FindMethodBody(syntaxTree, symbol.MetadataToken);
        if (methodBodyNode != null)
        {
            Find(methodBodyNode, typeEntityHandle, result);
        }

        return result;
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
                        return method.Body;
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

    private void Find(AstNode node, EntityHandle entityHandleToSearchFor, IList<UsageAsTextLocation> found)
    {
        var symbol = node.GetSymbol();

        var resolveResult2 = node.GetResolveResult();
        var resolveResult = node.Annotations.Where(a => a is ResolveResult).FirstOrDefault() as ResolveResult;
        if (resolveResult != null)
        {
            var resolveTypeDef = resolveResult.Type as ITypeDefinition;
            if (resolveTypeDef != null)
            {
                if (resolveTypeDef.MetadataToken == entityHandleToSearchFor)
                {
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
                }
            }
        }

        foreach (var child in node.Children)
        {
            Find(child, entityHandleToSearchFor, found);
        }
    }
}
