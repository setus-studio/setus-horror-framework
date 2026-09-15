using System.Collections.Generic;

namespace Setus.HorrorFramework.SaveProgression.StableIds
{
    public sealed class StableIdValidationIssue
    {
        public StableIdValidationIssue(
            StableIdValidationIssueType issueType,
            string stableId,
            IReadOnlyList<IStableIdProvider> providers)
        {
            IssueType = issueType;
            StableId = stableId;
            Providers = providers;
        }

        public StableIdValidationIssueType IssueType { get; }
        public string StableId { get; }
        public IReadOnlyList<IStableIdProvider> Providers { get; }
        public bool IsError =>
            IssueType == StableIdValidationIssueType.MissingId ||
            IssueType == StableIdValidationIssueType.DuplicateId ||
            IssueType == StableIdValidationIssueType.MissingRequiredId;
    }
}
