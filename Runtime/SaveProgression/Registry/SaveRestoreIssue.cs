namespace Setus.HorrorFramework.SaveProgression.Registry
{
    public sealed class SaveRestoreIssue
    {
        public SaveRestoreIssue(SaveRestoreIssueSeverity severity, string code, string message, string stableId = null)
        {
            Severity = severity;
            Code = code;
            Message = message;
            StableId = stableId;
        }

        public SaveRestoreIssueSeverity Severity { get; }
        public string Code { get; }
        public string Message { get; }
        public string StableId { get; }
    }
}
