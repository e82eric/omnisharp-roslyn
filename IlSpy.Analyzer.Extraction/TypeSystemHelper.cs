using System.Collections.Generic;
using System.IO;
using System.Linq;
using ICSharpCode.Decompiler.TypeSystem;

namespace IlSpy.Analyzer.Extraction
{
    internal class TypeSystemHelper
    {
        public static ITypeDefinition FindContainingType(ITypeDefinition symbol)
        {
            if (symbol.DeclaringType == null)
            {
                return symbol;
            }
            IType result = symbol.DeclaringType;

            while (result.DeclaringType != null)
            {
                result = result.DeclaringType;
            }

            if (result is ParameterizedType)
            {
                return null;
            }

            return (ITypeDefinition)result;
        }

        public static IMember FindMethod(IType type, string methodName, IReadOnlyList<string> methodParameterTypes)
        {
            var methods = type.GetMembers().Where(m =>
            {
                if (m.Name != methodName)
                {
                    return false;
                }

                var method = m as IMethod;

                if (method != null)
                {
                    if (method.Parameters.Count != methodParameterTypes.Count)
                    {
                        return false;
                    }

                    for (int i = 0; i < methodParameterTypes.Count; i++)
                    {
                        if (method.Parameters[i].Type.ReflectionName != methodParameterTypes[i])
                        {
                            return false;
                        }
                    }
                }
                else
                {
                    if (m.Name != methodName)
                    {
                        return false;
                    }
                }

                return true;
            });

            return (IMember)methods.FirstOrDefault();
        }

        public static IProperty FindProperty(IType type, string methodName)
        {
            var properties = type.GetProperties().Where(m =>
            {
                if (m.Name != methodName)
                {
                    return false;
                }

                return true;
            });

            return (IProperty)properties.FirstOrDefault();
        }

        public static string GetFilePathForExternalSymbol(string projectName, ITypeDefinition topLevelSymbol)
        {
            return
                $"$metadata$/Project/{Folderize(projectName)}/Assembly/{Folderize(topLevelSymbol.Compilation.MainModule.AssemblyName)}/Symbol/{Folderize(GetTypeDisplayString(topLevelSymbol))}.cs"
                    .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        }

        private static string GetTypeDisplayString(ITypeDefinition symbol)
        {
            return symbol.Name;
        }

        private static string Folderize(string path) => string.Join("/", path.Split('.'));
    }
}
