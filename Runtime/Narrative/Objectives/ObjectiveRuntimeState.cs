using System;
using System.Collections.Generic;
using UnityEngine;

namespace Setus.HorrorFramework.Narrative.Objectives
{
    [Serializable]
    public sealed class ObjectiveRuntimeState
    {
        [SerializeField] private string scenarioId;
        [SerializeField] private string activeObjectiveId;
        [SerializeField] private string[] completedObjectiveIds;

        public ObjectiveRuntimeState(
            string scenarioId = null,
            string activeObjectiveId = null,
            IEnumerable<string> completedObjectiveIds = null)
        {
            this.scenarioId = scenarioId ?? string.Empty;
            this.activeObjectiveId = activeObjectiveId ?? string.Empty;
            this.completedObjectiveIds = completedObjectiveIds == null
                ? Array.Empty<string>()
                : new List<string>(completedObjectiveIds).ToArray();
        }

        public string ScenarioId => scenarioId;
        public string ActiveObjectiveId => activeObjectiveId;
        public IReadOnlyList<string> CompletedObjectiveIds => completedObjectiveIds ?? Array.Empty<string>();
    }
}
