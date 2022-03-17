namespace OmniSharp.Models.Metadata
{
    public class MetadataSource
    {
        public string AssemblyName { get; set; }
        public string TypeName { get; set; }
        public string ProjectName { get; set; }
        public string Language { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
        public string FileName { get; set; }
        public string SourceLine { get; set; }
        public string SourceText { get; set; }
        public string MemberName { get; set; }
        public int StatementLine { get; set; }
        public int StartColumn { get; set; }
        public int EndColumn { get; set; }
        public uint Token { get; set; }

        public uint ContainingTypeToken { get; set; }
        public string ContainingTypeFullName { get; set; }
        public string AssemblyFilePath { get; set; }
    }
}
