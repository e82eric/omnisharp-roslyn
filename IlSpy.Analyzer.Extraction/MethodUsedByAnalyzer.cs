using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Disassembler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.ILSpy.Analyzers;

namespace IlSpy.Analyzer.Extraction;

/// <summary>
/// Shows entities that are used by a method.
/// </summary>
class MethodUsedByAnalyzer
{
    const GetMemberOptions Options = GetMemberOptions.IgnoreInheritedMembers | GetMemberOptions.ReturnMemberDefinitions;

    public IEnumerable<ISymbol> Analyze(ISymbol analyzedSymbol, AnalyzerContext context)
    {
        var analyzedMethod = (IMember)analyzedSymbol;
        var mapping = GetCodeMappingInfo(analyzedMethod.ParentModule.PEFile,
            analyzedMethod.DeclaringTypeDefinition.MetadataToken);

        // var parentMethod = mapping.GetParentMethod((MethodDefinitionHandle)analyzedMethod.MetadataToken);
        // if (parentMethod != analyzedMethod.MetadataToken)
        // 	yield return ((MetadataModule)analyzedMethod.ParentModule).GetDefinition(parentMethod);

        var scope = context.GetScopeOf(analyzedMethod);
        foreach (var type in scope.GetTypesInScope(context.CancellationToken))
        {
            var parentModule = (MetadataModule)type.ParentModule;
            mapping = GetCodeMappingInfo(parentModule.PEFile, type.MetadataToken);
            var methods = type.GetMembers(m => m is IMethod, Options).OfType<IMethod>();
            foreach (var method in methods)
            {
                if (IsUsedInMethod((IMethod)analyzedSymbol, method, context))
                {
                    var parent = mapping.GetParentMethod((MethodDefinitionHandle)method.MetadataToken);
                    yield return parentModule.GetDefinition(parent);
                }
            }

            foreach (var property in type.Properties)
            {
                if (property.CanGet && IsUsedInMethod((IMethod)analyzedSymbol, property.Getter, context))
                {
                    yield return property;
                    continue;
                }

                if (property.CanSet && IsUsedInMethod((IMethod)analyzedSymbol, property.Setter, context))
                {
                    yield return property;
                    continue;
                }
            }

            foreach (var @event in type.Events)
            {
                if (@event.CanAdd && IsUsedInMethod((IMethod)analyzedSymbol, @event.AddAccessor, context))
                {
                    yield return @event;
                    continue;
                }

                if (@event.CanRemove && IsUsedInMethod((IMethod)analyzedSymbol, @event.RemoveAccessor, context))
                {
                    yield return @event;
                    continue;
                }

                if (@event.CanInvoke && IsUsedInMethod((IMethod)analyzedSymbol, @event.InvokeAccessor, context))
                {
                    yield return @event;
                    continue;
                }
            }
        }
    }

    public virtual CodeMappingInfo GetCodeMappingInfo(PEFile module, System.Reflection.Metadata.EntityHandle member)
    {
        var declaringType = member.GetDeclaringType(module.Metadata);

        if (declaringType.IsNil && member.Kind == HandleKind.TypeDefinition)
        {
            declaringType = (TypeDefinitionHandle)member;
        }

        return new CodeMappingInfo(module, declaringType);
    }

    bool IsUsedInMethod(IMethod analyzedEntity, IMethod method, AnalyzerContext context)
    {
        return ScanMethodBody(analyzedEntity, method, context.GetMethodBody(method));
    }

    static bool ScanMethodBody(IMethod analyzedMethod, IMethod method, MethodBodyBlock methodBody)
    {
        if (methodBody == null)
            return false;

        var mainModule = (MetadataModule)method.ParentModule;
        var blob = methodBody.GetILReader();

        var baseMethod = InheritanceHelper.GetBaseMember(analyzedMethod);
        var genericContext =
            new ICSharpCode.Decompiler.TypeSystem.GenericContext(); // type parameters don't matter for this analyzer

        while (blob.RemainingBytes > 0)
        {
            ILOpCode opCode;
            try
            {
                opCode = blob.DecodeOpCode();
                if (!IsSupportedOpCode(opCode))
                {
                    ILParser.SkipOperand(ref blob, opCode);
                    continue;
                }
            }
            catch (BadImageFormatException)
            {
                return false; // unexpected end of blob
            }

            var member = MetadataTokenHelpers.EntityHandleOrNil(blob.ReadInt32());
            if (member.IsNil || !member.Kind.IsMemberKind())
                continue;

            IMember m;
            try
            {
                m = (mainModule.ResolveEntity(member, genericContext) as IMember)?.MemberDefinition;
            }
            catch (BadImageFormatException)
            {
                continue;
            }

            if (m == null)
                continue;

            if (opCode == ILOpCode.Callvirt && baseMethod != null)
            {
                if (IsSameMember(baseMethod, m))
                {
                    return true;
                }
            }
            else
            {
                if (IsSameMember(analyzedMethod, m))
                {
                    return true;
                }
            }
        }

        return false;
    }

    static bool IsSupportedOpCode(ILOpCode opCode)
    {
        switch (opCode)
        {
            case ILOpCode.Call:
            case ILOpCode.Callvirt:
            case ILOpCode.Ldtoken:
            case ILOpCode.Ldftn:
            case ILOpCode.Ldvirtftn:
            case ILOpCode.Newobj:
                return true;
            default:
                return false;
        }
    }

    static bool IsSameMember(IMember analyzedMethod, IMember m)
    {
        if (m.MetadataToken == analyzedMethod.MetadataToken)
        {
            if(m.ParentModule.PEFile == analyzedMethod.ParentModule.PEFile)
            {
                return true;
            }
        }

        return false;

        // return m.MetadataToken == analyzedMethod.MetadataToken
        //        && m.ParentModule.PEFile == analyzedMethod.ParentModule.PEFile;
    }
}
