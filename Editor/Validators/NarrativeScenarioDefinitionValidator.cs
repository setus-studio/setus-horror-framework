using Setus.HorrorFramework.Narrative.Scenario;
using UnityEditor;
using UnityEngine;

namespace Setus.HorrorFramework.Editor.Validators
{
    public static class NarrativeScenarioDefinitionValidator
    {
        [MenuItem("Setus/Horror Framework/Narrative/Validate Selected Scenario")]
        public static void ValidateSelectedScenarioFromMenu()
        {
            var scenario = Selection.activeObject as NarrativeScenarioDefinition;
            if (scenario == null)
            {
                Debug.LogError("Select a NarrativeScenarioDefinition asset before validating its objective graph.");
                return;
            }

            var issue = scenario.ValidateObjectiveGraph();
            if (issue.IsValid)
            {
                Debug.Log($"Objective graph validation passed for scenario '{scenario.ScenarioId}'.", scenario);
                return;
            }

            Debug.LogError(issue.ToDiagnostic(scenario.ScenarioId), scenario);
        }

        [MenuItem("Setus/Horror Framework/Narrative/Validate Selected Scenario", true)]
        private static bool CanValidateSelectedScenario()
        {
            return Selection.activeObject is NarrativeScenarioDefinition;
        }
    }
}
