namespace SHA_Project.Models
{
    public enum IssueSeverity
    {
        Error,
        Warning
    }

    public class BuildIssue
    {
        public string FilePath { get; set; }
        public int LineNumber { get; set; }
        public int Column { get; set; }
        public string ErrorCode { get; set; }
        public string RawMessage { get; set; }
        public string HumanizedMessage { get; set; }
        public IssueSeverity Severity { get; set; }
    }
}
