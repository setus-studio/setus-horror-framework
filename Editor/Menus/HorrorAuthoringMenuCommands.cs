using System;
using System.IO;
using System.Linq;
using Setus.HorrorFramework.Atmosphere.Scares;
using Setus.HorrorFramework.Editor.Authoring;
using Setus.HorrorFramework.Narrative.Objectives;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Setus.HorrorFramework.Editor.Menus
{
    public static class HorrorAuthoringMenuCommands
    {
        private const string GameContentRoot = "Assets/Game/Content";

        [MenuItem("GameObject/Setus Horror/Inspectable Interactable", false, 10)]
        public static void CreateInspectable()
        {
            var target = HorrorAuthoringObjectFactory.CreateInspectable(
                Selection.activeTransform,
                "Inspectable",
                CreateId("interaction.inspectable"),
                ResolveCreationPosition(),
                true);
            SelectAndFrame(target);
        }

        [MenuItem("GameObject/Setus Horror/Scare Trigger", false, 11)]
        public static void CreateScareTrigger()
        {
            var definition = Selection.activeObject as ScareDefinition;
            if (definition == null)
            {
                EnsureFolder(GameContentRoot + "/Scares");
                var path = EditorUtility.SaveFilePanelInProject(
                    "Create Scare Definition",
                    "ScareDefinition",
                    "asset",
                    "Choose a location under Assets/Game for the scare definition.",
                    GameContentRoot + "/Scares");
                if (string.IsNullOrWhiteSpace(path))
                {
                    return;
                }

                if (!path.StartsWith("Assets/Game/", StringComparison.Ordinal))
                {
                    EditorUtility.DisplayDialog(
                        "Invalid location",
                        "Game-specific scare definitions must be stored under Assets/Game.",
                        "OK");
                    return;
                }

                path = AssetDatabase.GenerateUniqueAssetPath(path);
                definition = HorrorAuthoringObjectFactory.CreateScareDefinition(path, CreateId("scare"));
                AssetDatabase.SaveAssets();
            }

            var selectedParent = Selection.activeTransform;
            var targetScene = ResolveTargetScene(selectedParent);
            var trigger = CreateScareTriggerInScene(
                definition,
                targetScene,
                selectedParent,
                ResolveCreationPosition());
            SelectAndFrame(trigger);
        }

        public static GameObject CreateScareTriggerInScene(
            ScareDefinition definition,
            Scene targetScene,
            Transform selectedParent,
            Vector3 position)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.ScareId))
            {
                throw new ArgumentException("Scare authoring requires a definition with a non-empty ScareId.", nameof(definition));
            }

            if (!targetScene.IsValid() || !targetScene.isLoaded)
            {
                throw new ArgumentException("Scare authoring requires a valid loaded target scene.", nameof(targetScene));
            }

            if (selectedParent != null && selectedParent.gameObject.scene != targetScene)
            {
                throw new ArgumentException("Selected parent must belong to the target scene.", nameof(selectedParent));
            }

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create Horror Scare Trigger");
            var adapter = FindOrCreateScareCatalog(targetScene, selectedParent);
            var definitions = new System.Collections.Generic.List<UnityEngine.Object>();
            foreach (var existing in adapter.Definitions)
            {
                if (existing != null && !definitions.Contains(existing))
                {
                    definitions.Add(existing);
                }
            }

            if (!definitions.Contains(definition))
            {
                definitions.Add(definition);
            }

            HorrorAuthoringObjectFactory.SetObjectArrayWithUndo(
                adapter,
                "scares",
                "Update Horror Scare Catalog",
                definitions.ToArray());
            var trigger = HorrorAuthoringObjectFactory.CreateScareTrigger(
                selectedParent,
                "ScareTrigger",
                CreateId("trigger.scare"),
                definition.ScareId,
                position,
                true,
                targetScene);
            EditorUtility.SetDirty(adapter);
            EditorSceneManager.MarkSceneDirty(targetScene);
            Undo.CollapseUndoOperations(undoGroup);
            return trigger;
        }

        [MenuItem("GameObject/Setus Horror/AI Patrol Route", false, 12)]
        public static void CreatePatrolRoute()
        {
            var route = HorrorAuthoringObjectFactory.CreatePatrolRoute(
                Selection.activeTransform,
                "StalkerPatrolRoute",
                ResolveCreationPosition(),
                true);
            SelectAndFrame(route.gameObject);
        }

        [MenuItem("Assets/Create/Setus/Horror Framework/Narrative/Scenario Skeleton", false, 120)]
        public static void CreateScenarioSkeleton()
        {
            EnsureFolder(GameContentRoot + "/Scenarios");
            var scenarioPath = EditorUtility.SaveFilePanelInProject(
                "Create Narrative Scenario Skeleton",
                "NarrativeScenario",
                "asset",
                "Choose a location under Assets/Game for the scenario.",
                GameContentRoot + "/Scenarios");
            if (string.IsNullOrWhiteSpace(scenarioPath))
            {
                return;
            }

            if (!scenarioPath.StartsWith("Assets/Game/", StringComparison.Ordinal))
            {
                EditorUtility.DisplayDialog(
                    "Invalid location",
                    "Game-specific narrative assets must be stored under Assets/Game.",
                    "OK");
                return;
            }

            scenarioPath = AssetDatabase.GenerateUniqueAssetPath(scenarioPath);
            var directory = Path.GetDirectoryName(scenarioPath)?.Replace('\\', '/') ?? GameContentRoot + "/Scenarios";
            var baseName = Path.GetFileNameWithoutExtension(scenarioPath);
            var objectivePath = AssetDatabase.GenerateUniqueAssetPath($"{directory}/{baseName}_Objective.asset");
            var scenario = HorrorAuthoringObjectFactory.CreateScenarioSkeleton(
                scenarioPath,
                objectivePath,
                CreateId("scenario"),
                CreateId("objective"),
                ObjectiveGateKind.InteractionTrigger,
                CreateId("trigger.objective"));
            AssetDatabase.SaveAssets();
            Selection.activeObject = scenario;
            EditorGUIUtility.PingObject(scenario);
        }

        private static ScareScenarioAdapter FindOrCreateScareCatalog(Scene targetScene, Transform selectedParent)
        {
            if (selectedParent != null)
            {
                var selectedAdapter = selectedParent.GetComponentInParent<ScareScenarioAdapter>(true);
                if (selectedAdapter == null)
                {
                    selectedAdapter = selectedParent.GetComponentInChildren<ScareScenarioAdapter>(true);
                }

                if (selectedAdapter != null)
                {
                    return selectedAdapter;
                }
            }

            var adapters = targetScene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<ScareScenarioAdapter>(true))
                .ToArray();
            if (adapters.Length == 1)
            {
                return adapters[0];
            }

            if (adapters.Length > 1)
            {
                throw new InvalidOperationException(
                    $"Scene '{targetScene.name}' contains multiple ScareScenarioAdapters. " +
                    "Select the intended catalog object before creating a scare trigger.");
            }

            var root = new GameObject("ScareScenario");
            if (root.scene != targetScene)
            {
                SceneManager.MoveGameObjectToScene(root, targetScene);
            }

            Undo.RegisterCreatedObjectUndo(root, "Create Horror Scare Catalog");
            return Undo.AddComponent<ScareScenarioAdapter>(root);
        }

        private static Scene ResolveTargetScene(Transform selectedParent)
        {
            if (selectedParent != null)
            {
                var selectedScene = selectedParent.gameObject.scene;
                if (selectedScene.IsValid() && selectedScene.isLoaded)
                {
                    return selectedScene;
                }
            }

            return SceneManager.GetActiveScene();
        }

        private static Vector3 ResolveCreationPosition()
        {
            if (Selection.activeTransform != null)
            {
                return Selection.activeTransform.position;
            }

            var sceneView = SceneView.lastActiveSceneView;
            return sceneView != null ? sceneView.pivot : Vector3.zero;
        }

        private static string CreateId(string prefix)
        {
            return $"{prefix}.{Guid.NewGuid():N}";
        }

        private static void SelectAndFrame(GameObject target)
        {
            Selection.activeGameObject = target;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var slash = path.LastIndexOf('/');
            var parent = slash > 0 ? path.Substring(0, slash) : "Assets";
            var folder = slash > 0 ? path.Substring(slash + 1) : path;
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
