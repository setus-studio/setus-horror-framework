using System;
using System.IO;
using Setus.HorrorFramework.Atmosphere.Scares;
using Setus.HorrorFramework.Editor.Authoring;
using Setus.HorrorFramework.Editor.Validators;
using Setus.HorrorFramework.Interaction.Triggers;
using Setus.HorrorFramework.Narrative.Objectives;
using Setus.HorrorFramework.Narrative.Scenario;
using Setus.HorrorFramework.SaveProgression.Registry;
using Setus.HorrorFramework.SaveProgression.StableIds;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Setus.HorrorFramework.Editor.Builders
{
    public static class AuthoringMilestoneBuilder
    {
        public const string SampleScenePath = "Assets/Game/Scenes/Debug/M9_AuthoringSample.unity";
        public const string SampleContentRoot = "Assets/Game/Content/Authoring/M9";
        public const string SampleScenarioPath = SampleContentRoot + "/M9_Scenario.asset";
        public const string SampleObjectivePath = SampleContentRoot + "/M9_Objective.asset";
        public const string SampleScarePath = SampleContentRoot + "/M9_Scare.asset";
        public const string SampleManifestPath = SampleContentRoot + "/M9_StableIdManifest.asset";

        private const string RootName = "M9AuthoringSample";
        private const string InspectableId = "m9.sample.inspectable";
        private const string ObjectiveTriggerId = "m9.sample.objective-trigger";
        private const string ScareTriggerId = "m9.sample.scare-trigger";
        private const string ScareId = "m9.sample.scare";

        [MenuItem("Setus/Horror Framework/Authoring/Build or Inspect M9 Mini Scenario")]
        public static void BuildOrInspectSampleFromMenu()
        {
            var result = BuildDefaultSample();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(SampleScenePath);
            EditorGUIUtility.PingObject(Selection.activeObject);
            Debug.Log(
                $"M9 mini scenario regenerated and inspected: {result.ErrorCount} error(s), " +
                $"{result.WarningCount} warning(s). Scene: {SampleScenePath}");
        }

        public static void BuildSampleFromCommandLine()
        {
            var result = BuildDefaultSample();
            Debug.Log(
                $"M9 mini scenario regenerated and validated: {result.ErrorCount} error(s), " +
                $"{result.WarningCount} warning(s). Scene: {SampleScenePath}");
        }

        private static FrameworkSceneValidationResult BuildDefaultSample()
        {
            return BuildSampleAtPaths(
                SampleScenePath,
                SampleScenarioPath,
                SampleObjectivePath,
                SampleScarePath,
                SampleManifestPath);
        }

        public static FrameworkSceneValidationResult BuildSampleAtPaths(
            string scenePath,
            string scenarioPath,
            string objectivePath,
            string scarePath,
            string manifestPath)
        {
            RequireGamePath(scenePath);
            RequireGamePath(scenarioPath);
            RequireGamePath(objectivePath);
            RequireGamePath(scarePath);
            RequireGamePath(manifestPath);
            EnsureFolder(Path.GetDirectoryName(scenePath)?.Replace('\\', '/') ?? "Assets/Game/Scenes");
            var scene = OpenOrCreateScene(scenePath);
            RemoveExistingSampleRoot(scene);

            var scenario = HorrorAuthoringObjectFactory.CreateScenarioSkeleton(
                scenarioPath,
                objectivePath,
                "m9.sample.scenario",
                "m9.sample.objective",
                ObjectiveGateKind.InteractionTrigger,
                ObjectiveTriggerId);
            var scare = HorrorAuthoringObjectFactory.CreateScareDefinition(scarePath, ScareId);
            var manifest = CreateOrLoad<StableIdManifest>(manifestPath);
            var sceneId = Path.GetFileNameWithoutExtension(scenePath);
            manifest.ReplaceEntries(new[]
            {
                RequiredEntry(InspectableId, "M9 sample inspectable.", sceneId),
                RequiredEntry(ObjectiveTriggerId, "M9 sample objective trigger.", sceneId),
                RequiredEntry(ScareTriggerId, "M9 sample scare trigger.", sceneId)
            });
            EditorUtility.SetDirty(manifest);
            AssetDatabase.SaveAssetIfDirty(manifest);

            // Newly created assets can be reloaded by a later AssetDatabase operation. Resolve
            // canonical imported instances before serializing references into the generated scene.
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            scenario = AssetDatabase.LoadAssetAtPath<NarrativeScenarioDefinition>(scenarioPath);
            scare = AssetDatabase.LoadAssetAtPath<ScareDefinition>(scarePath);
            manifest = AssetDatabase.LoadAssetAtPath<StableIdManifest>(manifestPath);
            if (scenario == null || scare == null || manifest == null)
            {
                throw new InvalidOperationException(
                    "Generated M9 authoring assets could not be reloaded before scene construction.");
            }

            scenario.ValidateObjectiveGraphOrThrow();
            if (string.IsNullOrWhiteSpace(scare.ScareId))
            {
                throw new InvalidOperationException(
                    $"Generated M9 scare definition at '{scarePath}' has no ScareId after import.");
            }

            var root = new GameObject(RootName);

            var scenarioObject = new GameObject("NarrativeScenario");
            scenarioObject.transform.SetParent(root.transform, false);
            var narrativeAdapter = scenarioObject.AddComponent<NarrativeScenarioAdapter>();
            SetObjectReference(narrativeAdapter, "scenario", scenario);

            var scareCatalog = new GameObject("ScareScenario");
            scareCatalog.transform.SetParent(root.transform, false);
            var scareAdapter = scareCatalog.AddComponent<ScareScenarioAdapter>();
            HorrorAuthoringObjectFactory.SetObjectArray(scareAdapter, "scares", scare);

            HorrorAuthoringObjectFactory.CreateInspectable(
                root.transform,
                "Inspectable",
                InspectableId,
                new Vector3(-2f, 0.75f, 2f),
                false);
            CreateObjectiveTrigger(root.transform);
            HorrorAuthoringObjectFactory.CreateScareTrigger(
                root.transform,
                "ScareTrigger",
                ScareTriggerId,
                scare.ScareId,
                new Vector3(2f, 1f, 4f),
                false);
            HorrorAuthoringObjectFactory.CreatePatrolRoute(
                root.transform,
                "StalkerPatrolRoute",
                new Vector3(-2f, 0f, 6f),
                false);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new InvalidOperationException($"Could not save M9 sample scene at '{scenePath}'.");
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            manifest = AssetDatabase.LoadAssetAtPath<StableIdManifest>(manifestPath);
            if (manifest == null)
            {
                throw new InvalidOperationException(
                    $"Generated M9 sample manifest could not be reloaded from '{manifestPath}'.");
            }

            var validation = FrameworkSceneValidator.ValidateScene(scene, manifest);
            if (!validation.IsValid)
            {
                FrameworkSceneValidator.LogResult(scene.name, validation);
                throw new InvalidOperationException(
                    $"Generated M9 sample failed validation with {validation.ErrorCount} error(s).");
            }

            return validation;
        }

        private static void CreateObjectiveTrigger(Transform parent)
        {
            var target = new GameObject("ObjectiveTrigger");
            target.transform.SetParent(parent, false);
            target.transform.position = new Vector3(0f, 1f, 3f);
            var collider = target.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(2f, 2f, 0.5f);
            target.AddComponent<Rigidbody>().isKinematic = true;
            target.AddComponent<StableId>().Assign(ObjectiveTriggerId);
            target.AddComponent<InteractionTriggerZone>();
        }

        private static Scene OpenOrCreateScene(string path)
        {
            if (File.Exists(path))
            {
                return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }

            return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static void RemoveExistingSampleRoot(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                if (string.Equals(roots[i].name, RootName, StringComparison.Ordinal))
                {
                    UnityEngine.Object.DestroyImmediate(roots[i]);
                }
            }
        }

        private static StableIdManifestEntry RequiredEntry(string stableId, string note, string sceneId)
        {
            return new StableIdManifestEntry(
                stableId,
                StableIdManifestEntryKind.Required,
                SaveRestorePolicy.ResumeDeterministicPhase,
                note,
                sceneId);
        }

        private static T CreateOrLoad<T>(string path) where T : ScriptableObject
        {
            EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "Assets/Game");
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName) ?? throw new InvalidOperationException(
                $"Serialized property '{propertyName}' was not found on {target.GetType().Name}.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RequireGamePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/Game/", StringComparison.Ordinal))
            {
                throw new ArgumentException("M9 generated game content must be stored under Assets/Game.", nameof(path));
            }
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
