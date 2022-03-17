using System.Reflection.Metadata;
using ICSharpCode.Decompiler.CSharp.Syntax;

namespace ICSharpCode.ILSpy.Analyzers.Builtin;

public class UsageAsTextLocation
{
    public EntityHandle TypeEntityHandle { get; set; }
    public TextLocation StartLocation { get; set; }
    public TextLocation EndLocation { get; set; }
    public string Statement { get; set; }
}
