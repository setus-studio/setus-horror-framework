using Setus.HorrorFramework.Editor.Validators;
using Setus.HorrorFramework.SceneFlow.Loading;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Setus.HorrorFramework.Editor.Inspectors
{
    [CustomEditor(typeof(StandaloneGameConfig))]
    public sealed class StandaloneGameConfigInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            var config = (StandaloneGameConfig)target;
            if (config.StableIdManifest == null)
            {
                EditorGUILayout.HelpBox(
                    "A StableIdManifest is required for production save/continue validation.",
                    MessageType.Error);
            }

            if (string.IsNullOrWhiteSpace(config.BootSceneName) ||
                string.IsNullOrWhiteSpace(config.MainMenuSceneName) ||
                string.IsNullOrWhiteSpace(config.FirstGameplaySceneName))
            {
                EditorGUILayout.HelpBox("Boot, Main Menu and Gameplay scene names must be authored.", MessageType.Error);
            }

            if (GUILayout.Button("Validate Active Scene"))
            {
                var scene = SceneManager.GetActiveScene();
                FrameworkSceneValidator.LogResult(
                    scene.name,
                    FrameworkSceneValidator.ValidateScene(scene, config.StableIdManifest));
            }
        }
    }
}
