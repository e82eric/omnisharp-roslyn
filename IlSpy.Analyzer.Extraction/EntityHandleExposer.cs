using System;
using System.Reflection;
using System.Reflection.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ICSharpCode.ILSpy.Analyzers.Builtin;

static class EntityHandleExposer
{
    public static uint Get(ISymbol sym)
    {
        var entity = sym as IEntity;
        if (entity != null)
        {
            Type typ = typeof(EntityHandle);
            FieldInfo type = typ.GetField("_vToken", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var value = type.GetValue(((IEntity)sym).MetadataToken);
            return (uint)value;
        }

        return default(uint);
    }
}
