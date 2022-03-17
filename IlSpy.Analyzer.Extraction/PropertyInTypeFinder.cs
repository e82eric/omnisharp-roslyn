using System.Reflection.Metadata;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.ILSpy.Analyzers;
using ICSharpCode.ILSpy.Analyzers.Builtin;

namespace IlSpy.Analyzer.Extraction;

public class PropertyInTypeFinder
{
    public UsageAsTextLocation Find(IProperty symbol, EntityHandle? setterEntityHandle, EntityHandle? getterEntityHandle, ITypeDefinition rootType)
    {
        var fileName = symbol.ParentModule.PEFile.FileName;
        var cachingDecompiler = DecompilerFactory.Get(fileName);

        var syntaxTree = cachingDecompiler.Run(rootType);


        var result = Find(syntaxTree, setterEntityHandle, getterEntityHandle, rootType.MetadataToken);

        return result;
    }

    private UsageAsTextLocation Find(AstNode node, EntityHandle? setterEntityHandle, EntityHandle? getterEntityHandle, EntityHandle rootTypeEntityHandle)
    {
        var symbol = node.GetSymbol();

        if (symbol != null)
        {
            if (symbol is IProperty entity)
            {
                if (entity.Setter != null && setterEntityHandle != null)
                {
                    if (entity.Setter.MetadataToken == setterEntityHandle)
                    {
                        var usage = new UsageAsTextLocation()
                        {
                            TypeEntityHandle = rootTypeEntityHandle,
                            StartLocation = node.StartLocation,
                            EndLocation = node.EndLocation,
                            Statement = node.Parent.ToString()
                        };

                        return usage;
                    }
                }

                if (entity.Getter != null && getterEntityHandle != null)
                {
                    if (entity.Getter.MetadataToken == getterEntityHandle)
                    {
                        var usage = new UsageAsTextLocation()
                        {
                            TypeEntityHandle = rootTypeEntityHandle,
                            StartLocation = node.StartLocation,
                            EndLocation = node.EndLocation,
                            Statement = node.Parent.ToString()
                        };

                        return usage;
                    }
                }
            }
        }

        foreach (var child in node.Children)
        {
            var usage = Find(child, setterEntityHandle, getterEntityHandle, rootTypeEntityHandle);
            if (usage != null)
            {
                return usage;
            }
        }

        return null;
    }
}
