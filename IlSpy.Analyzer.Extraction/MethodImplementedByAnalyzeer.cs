using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.ILSpy.Analyzers;

namespace IlSpy.Analyzer.Extraction;

class MethodImplementedByAnalyzer
{
    public IEnumerable<IMethod> Analyze(ISymbol analyzedSymbol, AnalyzerContext context)
    {
        Debug.Assert(analyzedSymbol is IMethod);
        var scope = context.GetScopeOf((IEntity)analyzedSymbol);
        foreach (var type in scope.GetTypesInScope(context.CancellationToken))
        {
            foreach (var result in AnalyzeType((IMethod)analyzedSymbol, type))
                yield return result;
        }
    }

    IEnumerable<IMethod> AnalyzeType(IMethod analyzedEntity, ITypeDefinition type)
    {
        var token = analyzedEntity.MetadataToken;
        var declaringTypeToken = analyzedEntity.DeclaringTypeDefinition.MetadataToken;
        var module = analyzedEntity.DeclaringTypeDefinition.ParentModule.PEFile;
        var allTypes = type.GetAllBaseTypeDefinitions();
        if (!allTypes.Any(t => t.MetadataToken == declaringTypeToken && t.ParentModule.PEFile == module))
            yield break;

        foreach (var method in type.Methods)
        {
            var baseMembers = InheritanceHelper.GetBaseMembers(method, true);
            if (baseMembers.Any(m => m.MetadataToken == token && m.ParentModule.PEFile == module))
                yield return method;
        }
    }
}
