using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Setus.HorrorFramework.Atmosphere.Audio;
using Setus.HorrorFramework.Atmosphere.Camera;
using Setus.HorrorFramework.Atmosphere.Lighting;
using Setus.HorrorFramework.Atmosphere.PostProcessing;
using Setus.HorrorFramework.Atmosphere.Scares;
using Setus.HorrorFramework.Editor.Validators;
using Setus.HorrorFramework.Interaction.Triggers;
using Setus.HorrorFramework.Player.Camera;
using Setus.HorrorFramework.SaveProgression.Registry;
using Setus.HorrorFramework.SaveProgression.StableIds;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Setus.HorrorFramework.Editor.Builders
{
    public static class AtmosphereMilestoneBuilder
    {
        private const string GameplayScenePath = "Assets/Game/Scenes/Gameplay.unity";
        private const string ManifestPath = "Assets/Game/Settings/GameplayStableIdManifest.asset";
        private const string FixtureRootName = "M7AtmosphereFixtures";
        private const string HallwayScarePath = "Assets/Game/Content/Scares/M7_HallwayPowerScare.asset";
        private const string DebugScarePath = "Assets/Game/Content/Scares/M7_DebugScare.asset";
        private const string HallwayScareId = "m7.scare.hallway-power";
        private const string DebugScareId = "m7.scare.debug-power";
        private const string HallwayTriggerId = "m7.scare.hallway-trigger";
        private const string HallwayPowerStateId = "m7.power.hallway";

        [MenuItem("Setus/Horror Framework/Atmosphere/Apply M7 Atmosphere Foundation")]
        public static void ApplyM7AtmosphereFoundationFromMenu()
        {
            ApplyM7AtmosphereFoundation();
        }

        public static void ApplyM7AtmosphereFoundation()
        {
            var content = CreateOrUpdateContent();
            UpdateGameplayScene(content);
            UpdateStableIdManifest();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("Applied M7 atmosphere foundation artifacts.");
        }

        private static AtmosphereContent CreateOrUpdateContent()
        {
            var hallway = CreateOrLoad<ScareDefinition>(HallwayScarePath);
            ConfigureString(hallway, "scareId", HallwayScareId);
            ConfigureFloat(hallway, "tensionIntensity", 0.72f);
            ConfigureFloat(hallway, "presentationDuration", 1.8f);
            ConfigureString(hallway, "powerStateId", HallwayPowerStateId);
            ConfigureString(hallway, "fallbackSpeaker", "Hallway");
            ConfigureString(hallway, "fallbackCueText", "The lights cut out somewhere ahead.");
            ConfigureFloat(hallway, "fallbackCueDuration", 2.8f);

            var debug = CreateOrLoad<ScareDefinition>(DebugScarePath);
            ConfigureString(debug, "scareId", DebugScareId);
            ConfigureFloat(debug, "tensionIntensity", 0.45f);
            ConfigureFloat(debug, "presentationDuration", 1.2f);
            ConfigureString(debug, "fallbackSpeaker", "Debug");
            ConfigureString(debug, "fallbackCueText", "Debug scare cue.");
            ConfigureFloat(debug, "fallbackCueDuration", 1.5f);
            return new AtmosphereContent(hallway, debug);
        }

        private static void UpdateGameplayScene(AtmosphereContent content)
        {
            ClearSceneSelectionBeforeReplacingScene();
            var scene = OpenScene(GameplayScenePath);
            var root = GameObject.Find(FixtureRootName) ?? new GameObject(FixtureRootName);

            var scenario = GetOrCreateChild(root.transform, "Scenario");
            ConfigureObjectArray(EnsureComponent<ScareScenarioAdapter>(scenario.gameObject), "scares", content.Hallway, content.Debug);

            var trigger = EnsureCube(root.transform, "M7_ScareTrigger", new Vector3(0f, 1f, 17.5f), new Vector3(2.5f, 2f, 0.35f));
            SetStableId(trigger, HallwayTriggerId);
            EnsureComponent<BoxCollider>(trigger).isTrigger = true;
            var interactionTrigger = EnsureComponent<InteractionTriggerZone>(trigger);
            ConfigureBool(interactionTrigger, "requirePlayerController", true);
            var scenarioTrigger = EnsureComponent<ScareScenarioTrigger>(trigger);
            ConfigureString(scenarioTrigger, "triggerId", HallwayTriggerId);
            ConfigureString(scenarioTrigger, "scareId", HallwayScareId);

            var powerLightObject = GetOrCreateChild(root.transform, "M7_PowerLight").gameObject;
            powerLightObject.transform.position = new Vector3(0f, 2.7f, 17.5f);
            var powerLight = EnsureComponent<Light>(powerLightObject);
            powerLight.type = LightType.Point;
            powerLight.range = 10f;
            powerLight.intensity = 6f;

            var flickerLightObject = GetOrCreateChild(root.transform, "M7_FlickerLight").gameObject;
            flickerLightObject.transform.position = new Vector3(1.8f, 2.4f, 17.5f);
            var flickerLight = EnsureComponent<Light>(flickerLightObject);
            flickerLight.type = LightType.Point;
            flickerLight.range = 8f;
            flickerLight.intensity = 4f;
            flickerLight.color = new Color(1f, 0.72f, 0.42f);

            var presentation = GetOrCreateChild(root.transform, "M7_ScarePresentation").gameObject;
            var audioSource = EnsureComponent<AudioSource>(presentation);
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            var audio = EnsureComponent<ScareAudioCueHook>(presentation);
            ConfigureString(audio, "scareId", HallwayScareId);
            ConfigureReference(audio, "cueSource", audioSource);
            var lifecycle = EnsureComponent<ScareLifecyclePresentationDriver>(presentation);
            ConfigureString(lifecycle, "scareId", HallwayScareId);
            var flicker = EnsureComponent<ScareLightFlickerHook>(presentation);
            ConfigureString(flicker, "scareId", HallwayScareId);
            ConfigureObjectArray(flicker, "affectedLights", flickerLight);
            var power = EnsureComponent<ScarePowerStateHook>(presentation);
            ConfigureString(power, "powerStateId", HallwayPowerStateId);
            ConfigureObjectArray(power, "affectedLights", powerLight);

            var volumeObject = GetOrCreateChild(root.transform, "M7_ScareVolume").gameObject;
            var volume = EnsureComponent<Volume>(volumeObject);
            volume.isGlobal = true;
            volume.weight = 0f;
            var volumeHook = EnsureComponent<ScareVolumeReactionHook>(presentation);
            ConfigureString(volumeHook, "scareId", HallwayScareId);
            ConfigureReference(volumeHook, "volume", volume);

            var cameraRig = UnityEngine.Object.FindFirstObjectByType<FirstPersonCameraRig>(FindObjectsInactive.Include);
            var camera = cameraRig != null ? cameraRig.Camera : null;
            if (camera == null)
            {
                throw new InvalidOperationException(
                    "M7 camera reaction requires a FirstPersonCameraRig with an assigned player camera.");
            }

            var cameraReaction = EnsureComponent<ScareCameraReactionCoordinator>(presentation);
            ConfigureString(cameraReaction, "scareId", HallwayScareId);
            ConfigureReference(cameraReaction, "targetCamera", camera);

            var ambience = GetOrCreateChild(root.transform, "M7_Ambience").gameObject;
            var low = EnsureComponent<AudioSource>(GetOrCreateChild(ambience.transform, "LowLayer").gameObject);
            var high = EnsureComponent<AudioSource>(GetOrCreateChild(ambience.transform, "HighLayer").gameObject);
            low.loop = true;
            high.loop = true;
            var layers = EnsureComponent<AmbienceLayerController>(ambience);
            ConfigureReference(layers, "lowTensionLayer", low);
            ConfigureReference(layers, "highTensionLayer", high);

            var debug = GetOrCreateChild(root.transform, "M7_DebugScareTrigger").gameObject;
            var debugTrigger = EnsureComponent<ScareDebugTrigger>(debug);
            ConfigureString(debugTrigger, "scareId", DebugScareId);
            var debugLifecycle = EnsureComponent<ScareLifecyclePresentationDriver>(debug);
            ConfigureString(debugLifecycle, "scareId", DebugScareId);

            var validation = ScareScenarioSceneValidator.ValidateScene(scene);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(validation.Issues[0].ToDiagnostic());
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, GameplayScenePath);
        }

        private static void UpdateStableIdManifest()
        {
            var manifest = AssetDatabase.LoadAssetAtPath<StableIdManifest>(ManifestPath);
            if (manifest == null)
            {
                throw new InvalidOperationException("M7 requires the Gameplay Stable ID Manifest.");
            }

            var entries = manifest.Entries
                .Where(entry => entry != null && !string.Equals(entry.StableId, HallwayTriggerId, StringComparison.Ordinal))
                .ToList();
            entries.Add(new StableIdManifestEntry(
                HallwayTriggerId,
                StableIdManifestEntryKind.Required,
                SaveRestorePolicy.ConsumeAndSkipPresentation,
                "M7 scenario trigger. Scare consumption is owned by atmosphere.scare-state.",
                Path.GetFileNameWithoutExtension(GameplayScenePath)));
            manifest.ReplaceEntries(entries.OrderBy(entry => entry.StableId, StringComparer.Ordinal));
            EditorUtility.SetDirty(manifest);
        }

        private static T CreateOrLoad<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            EnsureParentFolder(path);
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static Scene OpenScene(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("M7 requires the authored Gameplay scene.", path);
            }

            return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        private static void ClearSceneSelectionBeforeReplacingScene()
        {
            var selectedGameObject = Selection.activeGameObject;
            if (selectedGameObject != null && selectedGameObject.scene.IsValid())
            {
                Selection.activeObject = null;
            }
        }

        private static GameObject EnsureCube(Transform parent, string name, Vector3 position, Vector3 scale)
        {
            var child = FindChild(parent, name);
            var target = child != null ? child.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = name;
            target.transform.SetParent(parent, true);
            target.transform.position = position;
            target.transform.localScale = scale;
            return target;
        }

        private static Transform GetOrCreateChild(Transform parent, string name)
        {
            return FindChild(parent, name) ?? new GameObject(name).transform.WithParent(parent);
        }

        private static Transform WithParent(this Transform child, Transform parent)
        {
            child.SetParent(parent, false);
            return child;
        }

        private static Transform FindChild(Transform root, string name)
        {
            var children = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < children.Length; i++)
            {
                if (children[i].name == name)
                {
                    return children[i];
                }
            }

            return null;
        }

        private static T EnsureComponent<T>(GameObject target) where T : Component
        {
            var existing = target.GetComponent<T>();
            return existing != null ? existing : target.AddComponent<T>();
        }

        private static void SetStableId(GameObject target, string stableId)
        {
            EnsureComponent<StableId>(target).Assign(stableId);
        }

        private static void ConfigureString(UnityEngine.Object target, string propertyName, string value)
        {
            var serialized = new SerializedObject(target);
            var property = RequireProperty(serialized, propertyName, target);
            property.stringValue = value ?? string.Empty;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureFloat(UnityEngine.Object target, string propertyName, float value)
        {
            var serialized = new SerializedObject(target);
            var property = RequireProperty(serialized, propertyName, target);
            property.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureBool(UnityEngine.Object target, string propertyName, bool value)
        {
            var serialized = new SerializedObject(target);
            var property = RequireProperty(serialized, propertyName, target);
            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var property = RequireProperty(serialized, propertyName, target);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureObjectArray(UnityEngine.Object target, string propertyName, params UnityEngine.Object[] values)
        {
            var serialized = new SerializedObject(target);
            var property = RequireProperty(serialized, propertyName, target);
            if (!property.isArray)
            {
                throw new InvalidOperationException($"Serialized property '{propertyName}' is not an array.");
            }

            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SerializedProperty RequireProperty(SerializedObject serialized, string propertyName, UnityEngine.Object target)
        {
            return serialized.FindProperty(propertyName) ?? throw new InvalidOperationException(
                $"Serialized property '{propertyName}' was not found on {target.GetType().Name}.");
        }

        private static void EnsureParentFolder(string assetPath)
        {
            var slash = assetPath.LastIndexOf('/');
            EnsureFolder(assetPath.Substring(0, slash));
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

        private readonly struct AtmosphereContent
        {
            public AtmosphereContent(ScareDefinition hallway, ScareDefinition debug)
            {
                Hallway = hallway;
                Debug = debug;
            }

            public ScareDefinition Hallway { get; }
            public ScareDefinition Debug { get; }
        }
    }
}
