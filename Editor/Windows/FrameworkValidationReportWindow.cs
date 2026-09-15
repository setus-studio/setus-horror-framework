using System;
using Setus.HorrorFramework.Editor.Validators;
using Setus.HorrorFramework.SaveProgression.StableIds;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Setus.HorrorFramework.Editor.Windows
{
    public sealed class FrameworkValidationReportWindow : EditorWindow
    {
        private StableIdManifest manifest;
        private FrameworkSceneValidationResult result;
        private Vector2 scroll;

        [MenuItem("Setus/Horror Framework/Validation/Open Report Window")]
        public static void Open()
        {
            GetWindow<FrameworkValidationReportWindow>("Horror Validation");
        }

        private void OnEnable()
        {
            if (manifest == null)
            {
                manifest = Selection.activeObject as StableIdManifest;
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Active Scene Validation", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(SceneManager.GetActiveScene().path, EditorStyles.miniLabel);
            manifest = (StableIdManifest)EditorGUILayout.ObjectField(
                "Stable ID Manifest",
                manifest,
                typeof(StableIdManifest),
                false);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Validate Active Scene"))
                {
                    ValidateActiveScene();
                }

                using (new EditorGUI.DisabledScope(result == null))
                {
                    if (GUILayout.Button("Log Report"))
                    {
                        FrameworkSceneValidator.LogResult(SceneManager.GetActiveScene().name, result);
                    }
                }
            }

            EditorGUILayout.Space();
            if (result == null)
            {
                EditorGUILayout.HelpBox("Run validation to inspect authoring issues.", MessageType.Info);
                return;
            }

            var messageType = result.IsValid ? MessageType.Info : MessageType.Error;
            EditorGUILayout.HelpBox(
                $"{result.ErrorCount} error(s), {result.WarningCount} warning(s)",
                messageType);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (var i = 0; i < result.Issues.Count; i++)
            {
                DrawIssue(result.Issues[i]);
            }

            EditorGUILayout.EndScrollView();
        }

        private void ValidateActiveScene()
        {
            result = FrameworkSceneValidator.ValidateScene(SceneManager.GetActiveScene(), manifest);
            Repaint();
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

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.ObjectField("Object", issue.Owner, typeof(UnityEngine.Object), true);
                    if (GUILayout.Button("Select", GUILayout.Width(64f)))
                    {
                        Selection.activeObject = issue.Owner;
                        EditorGUIUtility.PingObject(issue.Owner);
                    }
                }
            }
        }
    }
}
