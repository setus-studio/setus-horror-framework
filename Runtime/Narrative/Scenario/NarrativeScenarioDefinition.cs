using System.Linq;
using Setus.HorrorFramework.Narrative.Objectives;
using Setus.HorrorFramework.Narrative.Phone;
using UnityEngine;

namespace Setus.HorrorFramework.Narrative.Scenario
{
    [CreateAssetMenu(
        fileName = "NarrativeScenarioDefinition",
        menuName = "Setus/Horror Framework/Narrative/Scenario Definition")]
    public sealed class NarrativeScenarioDefinition : ScriptableObject
    {
        [SerializeField] private string scenarioId;
        [SerializeField] private string initialObjectiveId;
        [SerializeField] private ObjectiveDefinition[] objectives;
        [SerializeField] private PhoneMessageDefinition[] phoneMessages;

        public string ScenarioId => scenarioId;
        public string InitialObjectiveId => initialObjectiveId;
        public ObjectiveDefinition[] Objectives => objectives;
        public PhoneMessageDefinition[] PhoneMessages => phoneMessages;

        public ObjectiveGraphValidationIssue ValidateObjectiveGraph()
        {
            var phoneMessageIds = phoneMessages == null
                ? null
                : phoneMessages.Select(message => message != null ? message.MessageId : string.Empty);
            return ObjectiveGraphValidator.Validate(scenarioId, objectives, initialObjectiveId, phoneMessageIds);
        }

        public void ValidateObjectiveGraphOrThrow()
        {
            var issue = ValidateObjectiveGraph();
            if (!issue.IsValid)
            {
                throw new System.InvalidOperationException(issue.ToDiagnostic(scenarioId));
            }
        }
    }
}
