using Setus.HorrorFramework.Narrative.Scenario;
using UnityEditor;
using UnityEngine;

namespace Setus.HorrorFramework.Editor.Inspectors
{
    [CustomEditor(typeof(NarrativeScenarioDefinition))]
    public sealed class NarrativeScenarioDefinitionInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            var scenario = (NarrativeScenarioDefinition)target;
            var issue = scenario.ValidateObjectiveGraph();
            if (issue.IsValid)
            {
                EditorGUILayout.HelpBox("Objective graph validation passed.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(issue.ToDiagnostic(scenario.ScenarioId), MessageType.Error);
            }
        }
    }
}
