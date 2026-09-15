using System.Linq;
using Setus.HorrorFramework.SaveProgression.StableIds;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Setus.HorrorFramework.Editor.Validators
{
    public static class StableIdManifestSceneValidator
    {
        [MenuItem("Setus/Horror Framework/Save/Validate Active Scene Stable IDs")]
        public static void ValidateActiveSceneFromMenu()
        {
            var manifest = Selection.activeObject as StableIdManifest;
            var result = ValidateActiveScene(manifest);

            foreach (var issue in result.Issues)
            {
                var message = $"{issue.IssueType}: {issue.StableId}";
                if (issue.IsError)
                {
                    Debug.LogError(message, issue.Providers.FirstOrDefault()?.Owner);
                }
                else
                {
                    Debug.LogWarning(message, issue.Providers.FirstOrDefault()?.Owner);
                }
            }

            if (result.Issues.Count == 0)
            {
                Debug.Log("Stable ID validation passed for active scene.");
            }
        }

        public static StableIdValidationResult ValidateActiveScene(StableIdManifest manifest)
        {
            var activeScene = SceneManager.GetActiveScene();
            var providers = activeScene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<StableId>(true))
                .Cast<IStableIdProvider>()
                .ToArray();

            var sceneId = string.IsNullOrWhiteSpace(activeScene.name)
                ? "<unsaved-scene>"
                : activeScene.name;
            return StableIdValidator.Validate(providers, manifest, sceneId);
        }
    }
}
