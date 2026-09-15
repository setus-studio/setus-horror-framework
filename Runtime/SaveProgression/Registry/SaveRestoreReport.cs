using System.Collections.Generic;
using System.Linq;

namespace Setus.HorrorFramework.SaveProgression.Registry
{
    public sealed class SaveRestoreReport
    {
        private readonly List<SaveRestoreIssue> issues = new List<SaveRestoreIssue>();

        public IReadOnlyList<SaveRestoreIssue> Issues => issues;
        public bool HasErrors => issues.Any(issue => issue.Severity == SaveRestoreIssueSeverity.Error);
        public bool HasWarnings => issues.Any(issue => issue.Severity == SaveRestoreIssueSeverity.Warning);

        public void AddError(string code, string message, string stableId = null)
        {
            issues.Add(new SaveRestoreIssue(SaveRestoreIssueSeverity.Error, code, message, stableId));
        }

        public void AddWarning(string code, string message, string stableId = null)
        {
            issues.Add(new SaveRestoreIssue(SaveRestoreIssueSeverity.Warning, code, message, stableId));
        }
    }
}
