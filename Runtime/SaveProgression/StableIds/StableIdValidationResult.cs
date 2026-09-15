using System.Collections.Generic;
using System.Linq;

namespace Setus.HorrorFramework.SaveProgression.StableIds
{
    public sealed class StableIdValidationResult
    {
        public StableIdValidationResult(IReadOnlyList<StableIdValidationIssue> issues)
        {
            Issues = issues;
        }

        public IReadOnlyList<StableIdValidationIssue> Issues { get; }
        public bool HasErrors => Issues.Any(issue => issue.IsError);
        public bool HasWarnings => Issues.Any(issue => !issue.IsError);

        public IEnumerable<StableIdValidationIssue> Duplicates =>
            Issues.Where(issue => issue.IssueType == StableIdValidationIssueType.DuplicateId);

        public IEnumerable<StableIdValidationIssue> MissingIds =>
            Issues.Where(issue => issue.IssueType == StableIdValidationIssueType.MissingId);

        public IEnumerable<StableIdValidationIssue> MissingRequiredIds =>
            Issues.Where(issue => issue.IssueType == StableIdValidationIssueType.MissingRequiredId);

        public IEnumerable<StableIdValidationIssue> MissingOptionalIds =>
            Issues.Where(issue => issue.IssueType == StableIdValidationIssueType.MissingOptionalId);
    }
}
