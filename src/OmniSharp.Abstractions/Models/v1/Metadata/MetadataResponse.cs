namespace OmniSharp.Models.Metadata
{
    public class MetadataResponse
    {
        public string SourceName { get; set; }
        public string Source { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }

        public int StartColumn { get; set; }
        public int EndColumn { get; set; }
    }
}
