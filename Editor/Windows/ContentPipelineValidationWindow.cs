using Setus.HorrorFramework.Editor.ContentPipeline;
using Setus.HorrorFramework.Editor.Validators;
using UnityEditor;
using UnityEngine;

namespace Setus.HorrorFramework.Editor.Windows
{
    public sealed class ContentPipelineValidationWindow : EditorWindow
    {
        private string gameRoot = StandaloneGameContentTemplate.DefaultGameRoot;
        private FrameworkSceneValidationResult result;
        private Vector2 scroll;

        public static void Open()
        {
            GetWindow<ContentPipelineValidationWindow>("Content Validation");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Standalone Game Content", EditorStyles.boldLabel);
            gameRoot = EditorGUILayout.TextField("Game Root", gameRoot);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create / Repair Folders"))
                {
                    StandaloneGameContentTemplate.CreateAt(gameRoot);
                    result = null;
                }

                if (GUILayout.Button("Validate Content"))
                {
                    result = ContentAssetValidator.Validate(gameRoot);
                }

                using (new EditorGUI.DisabledScope(result == null))
                {
                    if (GUILayout.Button("Log Report"))
                    {
                        ContentAssetValidator.LogResult(gameRoot, result);
                    }
                }
            }

            EditorGUILayout.HelpBox(
                "Checks the folder template, audio import policy, persistent prefab requirements, " +
                "content IDs, and basic texture/model memory guidance.",
                MessageType.Info);

            if (result == null)
            {
                return;
            }

            EditorGUILayout.HelpBox(
                $"{result.ErrorCount} error(s), {result.WarningCount} warning(s)",
                result.IsValid ? MessageType.Info : MessageType.Error);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (var i = 0; i < result.Issues.Count; i++)
            {
                DrawIssue(result.Issues[i]);
            }

            EditorGUILayout.EndScrollView();
        }

        private static void DrawIssue(FrameworkSceneValidationIssue issue)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"{issue.Severity}: {issue.Code}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(issue.Message, EditorStyles.wordWrappedLabel);
                if (issue.Owner == null)
                {
                    return;
                }

                EditorGUILayout.ObjectField("Asset", issue.Owner, typeof(Object), false);
                if (GUILayout.Button("Select Asset"))
                {
                    Selection.activeObject = issue.Owner;
                    EditorGUIUtility.PingObject(issue.Owner);
                }
            }
        }
    }
}
