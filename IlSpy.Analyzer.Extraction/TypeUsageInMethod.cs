using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.TypeSystem;

namespace ICSharpCode.ILSpy.Analyzers.Builtin;

public class TypeUsageInMethod
{
    public AstNode Statement { get; set; }
    public ITypeDefinition TypeDefinition { get; set; }
    public AstNode TypeNode { get; set; }

    public IMethod Method { get; set; }
    public int StatementLine { get; set; }

    public int StartColumn { get; set; }
    public int EndColumn { get; set; }

    public uint Token { get; set; }
}
