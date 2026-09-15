using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Setus.HorrorFramework.Editor.Validators
{
    public enum FrameworkSceneValidationSeverity
    {
        Warning = 0,
        Error = 1
    }

    public sealed class FrameworkSceneValidationIssue
    {
        public FrameworkSceneValidationIssue(
            FrameworkSceneValidationSeverity severity,
            string code,
            string message,
            UnityEngine.Object owner = null)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            Owner = owner;
        }

        public FrameworkSceneValidationSeverity Severity { get; }
        public string Code { get; }
        public string Message { get; }
        public UnityEngine.Object Owner { get; }
        public bool IsError => Severity == FrameworkSceneValidationSeverity.Error;

        public override string ToString()
        {
            var ownerName = Owner != null ? Owner.name : "<scene>";
            return $"[{Severity}] {Code}: {ownerName}: {Message}";
        }
    }

    public sealed class FrameworkSceneValidationResult
    {
        public FrameworkSceneValidationResult(IEnumerable<FrameworkSceneValidationIssue> issues)
        {
            Issues = (issues ?? Enumerable.Empty<FrameworkSceneValidationIssue>()).ToArray();
        }

        public IReadOnlyList<FrameworkSceneValidationIssue> Issues { get; }
        public bool IsValid => Issues.All(issue => !issue.IsError);
        public int ErrorCount => Issues.Count(issue => issue.IsError);
        public int WarningCount => Issues.Count(issue => !issue.IsError);
    }
}
