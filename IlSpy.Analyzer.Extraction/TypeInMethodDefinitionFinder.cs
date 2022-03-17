using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.Semantics;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.ILSpy.Analyzers;
using ICSharpCode.ILSpy.Analyzers.Builtin;

namespace IlSpy.Analyzer.Extraction;

public class TypeInMethodDefinitionFinder
{
    public IEnumerable<UsageAsTextLocation> Find(ITypeDefinition rootType, IMethod symbol, EntityHandle typeEntityHandle, AnalyzerContext context)
    {
        var fileName = symbol.Compilation.MainModule.PEFile.FileName;
        var cachingDecompiler = DecompilerFactory.Get(fileName);
        var result = new List<UsageAsTextLocation>();

        var syntaxTree = cachingDecompiler.Run(rootType);

        var methodNode = FindMethod(syntaxTree, symbol.MetadataToken);
        if (methodNode != null)
        {
            Find(methodNode, typeEntityHandle, result);
        }

        return result;
    }

    private AstNode FindMethod(AstNode node, EntityHandle entityHandle)
    {
        if (node.NodeType == NodeType.Member)
        {
            var symbol = node.GetSymbol();
            var entity = symbol as IEntity;
            if (entity != null)
            {
                if (entity.MetadataToken == entityHandle)
                {
                    return node;
                }
            }
        }

        if (node.HasChildren)
        {
            foreach (var child in node.Children)
            {
                var method = FindMethod(child, entityHandle);
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

        if (node.Role == Roles.Type || node.Role == Roles.Parameter)
        {
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
                                    EndLocation = node.EndLocation
                                };

                                found.Add(usage);
                            }
                        }
                    }
                }
            }
        }

        if (node.Role != Roles.Body)
        {
            foreach (var child in node.Children)
            {
                Find(child, entityHandleToSearchFor, found);
            }
        }
    }
}
