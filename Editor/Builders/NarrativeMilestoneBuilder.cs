using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Setus.HorrorFramework.Interaction.Triggers;
using Setus.HorrorFramework.Narrative.Gates;
using Setus.HorrorFramework.Narrative.Notes;
using Setus.HorrorFramework.Narrative.Objectives;
using Setus.HorrorFramework.Narrative.Phone;
using Setus.HorrorFramework.Narrative.Scenario;
using Setus.HorrorFramework.SaveProgression.Registry;
using Setus.HorrorFramework.SaveProgression.StableIds;
using Setus.HorrorFramework.UI.Objectives;
using Setus.HorrorFramework.UI.Phone;
using Setus.HorrorFramework.UI.Subtitles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Setus.HorrorFramework.Editor.Builders
{
    public static class NarrativeMilestoneBuilder
    {
        private const string GameplayScenePath = "Assets/Game/Scenes/Gameplay.unity";
        private const string ManifestPath = "Assets/Game/Settings/GameplayStableIdManifest.asset";
        private const string FixtureRootName = "M6NarrativeFixtures";
        private const string ScenarioPath = "Assets/Game/Content/Scenarios/M6_DebugScenario.asset";
        private const string PhoneMessagePath = "Assets/Game/Content/Messages/M6_ReturnCall.asset";
        private const string NotePath = "Assets/Game/Content/Notes/M6_MaintenanceLog.asset";
        private const string ArriveObjectivePath = "Assets/Game/Content/Objectives/M6_Arrive.asset";
        private const string PhoneObjectivePath = "Assets/Game/Content/Objectives/M6_ReadPhone.asset";
        private const string NoteObjectivePath = "Assets/Game/Content/Objectives/M6_ReadNote.asset";
        private const string RouteObjectivePath = "Assets/Game/Content/Objectives/M6_UseRoute.asset";

        [MenuItem("Setus/Horror Framework/Narrative/Apply M6 Narrative Foundation")]
        public static void ApplyM6NarrativeFoundationFromMenu()
        {
            ApplyM6NarrativeFoundation();
        }

        public static void ApplyM6NarrativeFoundation()
        {
            var content = CreateOrUpdateContent();
            UpdateUiShellPrefab();
            UpdateGameplayScene(content);
            UpdateStableIdManifest();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("Applied M6 narrative foundation artifacts.");
        }

        private static NarrativeContent CreateOrUpdateContent()
        {
            var phone = CreateOrLoad<PhoneMessageDefinition>(PhoneMessagePath);
            ConfigureString(phone, "messageId", "m6.message.return-call");
            ConfigureString(phone, "sender", "Unknown Number");
            ConfigureString(phone, "messageText", "The maintenance door is open. Do not stay in the hallway.");
            ConfigureBool(phone, "requiresReply", true);
            ConfigureString(phone, "replyText", "I am on my way.");

            var note = CreateOrLoad<NarrativeNoteDefinition>(NotePath);
            ConfigureString(note, "noteId", "m6.note.maintenance-log");
            ConfigureString(note, "title", "Maintenance Log");
            ConfigureString(note, "body", "Power returns after the hallway route is cleared.");
            ConfigureFloat(note, "subtitleDuration", 5f);

            var arrive = CreateObjective(
                ArriveObjectivePath,
                "m6.objective.arrive",
                "Reach the marked hallway",
                "Walk through the marked area.",
                ObjectiveGateKind.StoryBeat,
                "m6.story.arrival",
                "m6.objective.read-phone");
            var readPhone = CreateObjective(
                PhoneObjectivePath,
                "m6.objective.read-phone",
                "Check the phone",
                "Read the new message.",
                ObjectiveGateKind.PhoneRead,
                phone.MessageId,
                "m6.objective.read-note");
            var readNote = CreateObjective(
                NoteObjectivePath,
                "m6.objective.read-note",
                "Read the maintenance log",
                "Find the note near the door.",
                ObjectiveGateKind.NoteRead,
                note.NoteId,
                "m6.objective.use-route");
            var useRoute = CreateObjective(
                RouteObjectivePath,
                "m6.objective.use-route",
                "Use the cleared route",
                "The hallway route is now unlocked.",
                ObjectiveGateKind.RouteUnlocked,
                "m6.route.hallway",
                string.Empty);

            var scenario = CreateOrLoad<NarrativeScenarioDefinition>(ScenarioPath);
            ConfigureString(scenario, "scenarioId", "m6.debug.standalone-scenario");
            ConfigureString(scenario, "initialObjectiveId", arrive.ObjectiveId);
            ConfigureObjectArray(scenario, "objectives", arrive, readPhone, readNote, useRoute);
            ConfigureObjectArray(scenario, "phoneMessages", phone);
            scenario.ValidateObjectiveGraphOrThrow();
            return new NarrativeContent(scenario, phone, note);
        }

        private static ObjectiveDefinition CreateObjective(
            string path,
            string objectiveId,
            string title,
            string description,
            ObjectiveGateKind gate,
            string gateId,
            string nextObjectiveId)
        {
            var objective = CreateOrLoad<ObjectiveDefinition>(path);
            ConfigureString(objective, "objectiveId", objectiveId);
            ConfigureString(objective, "title", title);
            ConfigureString(objective, "description", description);
            ConfigureEnum(objective, "completionGate", (int)gate);
            ConfigureString(objective, "completionGateId", gateId);
            ConfigureString(objective, "nextObjectiveId", nextObjectiveId);
            return objective;
        }

        private static void UpdateUiShellPrefab()
        {
            SceneFlowUiShellTemplateBuilder.BuildDefaultUiShell(false);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SceneFlowUiShellTemplateBuilder.UiShellPrefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException("M6 requires the Setus UI shell prefab.");
            }

            var root = PrefabUtility.LoadPrefabContents(SceneFlowUiShellTemplateBuilder.UiShellPrefabPath);
            try
            {
                EnsureObjectivePresenter(root);
                EnsurePhonePresenter(root);
                EnsureSubtitlePresenter(root);
                PrefabUtility.SaveAsPrefabAsset(root, SceneFlowUiShellTemplateBuilder.UiShellPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsureObjectivePresenter(GameObject root)
        {
            var presenter = EnsureComponent<ObjectivePresenter>(root);
            var panel = EnsurePanel(root.transform, "NarrativeObjectivePanel", new Vector2(40f, -40f), new Vector2(500f, 126f),
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            var title = EnsureText(panel.transform, "Title", 25, TextAnchor.UpperLeft, new Vector2(18f, -14f), new Vector2(-18f, -52f));
            var description = EnsureText(panel.transform, "Description", 19, TextAnchor.UpperLeft, new Vector2(18f, -54f), new Vector2(-18f, -14f));
            ConfigureReference(presenter, "root", panel);
            ConfigureReference(presenter, "titleText", title);
            ConfigureReference(presenter, "descriptionText", description);
            panel.SetActive(false);
        }

        private static void EnsurePhonePresenter(GameObject root)
        {
            var presenter = EnsureComponent<PhoneMessagePresenter>(root);
            var panel = EnsurePanel(root.transform, "NarrativePhonePanel", new Vector2(-40f, 42f), new Vector2(500f, 160f),
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
            var sender = EnsureText(panel.transform, "Sender", 22, TextAnchor.UpperLeft, new Vector2(18f, -14f), new Vector2(-18f, -46f));
            var message = EnsureText(panel.transform, "Message", 18, TextAnchor.UpperLeft, new Vector2(18f, -50f), new Vector2(-18f, -48f));
            var status = EnsureText(panel.transform, "Status", 16, TextAnchor.LowerRight, new Vector2(18f, -124f), new Vector2(-18f, -14f));
            ConfigureReference(presenter, "root", panel);
            ConfigureReference(presenter, "senderText", sender);
            ConfigureReference(presenter, "messageText", message);
            ConfigureReference(presenter, "statusText", status);
            panel.SetActive(false);
        }

        private static void EnsureSubtitlePresenter(GameObject root)
        {
            var presenter = EnsureComponent<SubtitlePresenter>(root);
            var panel = EnsurePanel(root.transform, "NarrativeSubtitlePanel", new Vector2(0f, 150f), new Vector2(820f, 104f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            var speaker = EnsureText(panel.transform, "Speaker", 20, TextAnchor.UpperCenter, new Vector2(18f, -10f), new Vector2(-18f, -40f));
            var subtitle = EnsureText(panel.transform, "Subtitle", 22, TextAnchor.MiddleCenter, new Vector2(18f, -42f), new Vector2(-18f, -10f));
            ConfigureReference(presenter, "root", panel);
            ConfigureReference(presenter, "speakerText", speaker);
            ConfigureReference(presenter, "subtitleText", subtitle);
            panel.SetActive(false);
        }

        private static GameObject EnsurePanel(
            Transform parent,
            string name,
            Vector2 anchoredPosition,
            Vector2 size,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot)
        {
            var panel = FindChild(parent, name)?.gameObject ?? CreateUiObject(name);
            if (panel.transform.parent != parent)
            {
                panel.transform.SetParent(parent, false);
            }

            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            var image = EnsureComponent<Image>(panel);
            image.color = new Color(0.015f, 0.02f, 0.03f, 0.72f);
            image.raycastTarget = false;
            return panel;
        }

        private static Text EnsureText(
            Transform parent,
            string name,
            int fontSize,
            TextAnchor alignment,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            var textObject = FindChild(parent, name)?.gameObject ?? CreateUiObject(name);
            if (textObject.transform.parent != parent)
            {
                textObject.transform.SetParent(parent, false);
            }

            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            var text = EnsureComponent<Text>(textObject);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static void UpdateGameplayScene(NarrativeContent content)
        {
            var scene = OpenScene(GameplayScenePath);
            var root = GameObject.Find(FixtureRootName) ?? new GameObject(FixtureRootName);
            var scenarioRoot = GetOrCreateChild(root.transform, "Scenario");
            var adapter = EnsureComponent<NarrativeScenarioAdapter>(scenarioRoot.gameObject);
            ConfigureReference(adapter, "scenario", content.Scenario);

            var storyBeat = EnsureCube(root.transform, "M6_StoryBeatTrigger", new Vector3(0f, 1f, 7.25f), new Vector3(2.5f, 2f, 0.4f));
            SetStableId(storyBeat, "m6.story.arrival");
            EnsureComponent<BoxCollider>(storyBeat).isTrigger = true;
            EnsureComponent<InteractionTriggerZone>(storyBeat);
            var storyTrigger = EnsureComponent<NarrativeStoryBeatTrigger>(storyBeat);
            ConfigureString(storyTrigger, "triggerId", "m6.story.arrival");
            ConfigureString(storyTrigger, "storyBeatId", "m6.story.arrival");
            var branchFlag = EnsureComponent<NarrativeBranchFlagOnStoryBeat>(storyBeat);
            ConfigureString(branchFlag, "storyBeatId", "m6.story.arrival");
            ConfigureString(branchFlag, "flagId", "m6.branch.arrived");
            ConfigureBool(branchFlag, "value", true);

            var phone = EnsureCube(root.transform, "M6_Phone", new Vector3(1.45f, 0.75f, 9.25f), new Vector3(0.45f, 0.28f, 0.65f));
            var phoneInteractable = EnsureComponent<PhoneMessageInteractable>(phone);
            ConfigureReference(phoneInteractable, "message", content.PhoneMessage);

            var note = EnsureCube(root.transform, "M6_Note", new Vector3(-1.45f, 0.78f, 11.5f), new Vector3(0.6f, 0.42f, 0.08f));
            var noteInteractable = EnsureComponent<NarrativeNoteInteractable>(note);
            ConfigureReference(noteInteractable, "note", content.Note);

            var unlockerRoot = GetOrCreateChild(root.transform, "RouteUnlocker");
            var unlocker = EnsureComponent<NarrativeRouteUnlocker>(unlockerRoot.gameObject);
            ConfigureString(unlocker, "completedObjectiveId", "m6.objective.read-note");
            ConfigureString(unlocker, "routeId", "m6.route.hallway");

            var routeRoot = GetOrCreateChild(root.transform, "M6_RouteBarrier");
            routeRoot.position = new Vector3(0f, 1f, 14.25f);
            var routeVisual = EnsureCube(routeRoot, "LockedVisual", Vector3.zero, new Vector3(3.4f, 2f, 0.22f));
            routeVisual.transform.localPosition = Vector3.zero;
            var barrier = EnsureComponent<NarrativeRouteBarrier>(routeRoot.gameObject);
            ConfigureString(barrier, "routeId", "m6.route.hallway");
            ConfigureReference(barrier, "lockedVisual", routeVisual);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, GameplayScenePath);
        }

        private static void UpdateStableIdManifest()
        {
            var manifest = AssetDatabase.LoadAssetAtPath<StableIdManifest>(ManifestPath);
            if (manifest == null)
            {
                throw new InvalidOperationException("M6 requires the authored Gameplay Stable ID Manifest.");
            }

            var entries = manifest.Entries
                .Where(entry => entry != null &&
                    !string.Equals(entry.StableId, "m6.story.arrival", StringComparison.Ordinal))
                .ToList();
            entries.Add(new StableIdManifestEntry(
                "m6.story.arrival",
                StableIdManifestEntryKind.Optional,
                SaveRestorePolicy.ResumeDeterministicPhase,
                "M6 story beat trigger; durable one-shot progression is owned by narrative.state.",
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

        private static GameObject EnsureCube(Transform parent, string name, Vector3 position, Vector3 scale)
        {
            var existing = FindChild(parent, name);
            var cube = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (cube.transform.parent != parent)
            {
                cube.transform.SetParent(parent, true);
            }

            cube.name = name;
            cube.transform.position = position;
            cube.transform.localScale = scale;
            return cube;
        }

        private static Transform GetOrCreateChild(Transform parent, string name)
        {
            var child = FindChild(parent, name);
            if (child != null)
            {
                return child;
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void SetStableId(GameObject target, string id)
        {
            EnsureComponent<StableId>(target).Assign(id);
        }

        private static Scene OpenScene(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("M6 requires the authored Gameplay scene.", path);
            }

            return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
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
            return target.GetComponent<T>() ?? target.AddComponent<T>();
        }

        private static GameObject CreateUiObject(string name)
        {
            return new GameObject(name, typeof(RectTransform));
        }

        private static void ConfigureReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"Serialized property '{propertyName}' was not found on {target.GetType().Name}.");
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureString(UnityEngine.Object target, string propertyName, string value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"Serialized property '{propertyName}' was not found on {target.GetType().Name}.");
            }

            property.stringValue = value ?? string.Empty;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureBool(UnityEngine.Object target, string propertyName, bool value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"Serialized property '{propertyName}' was not found on {target.GetType().Name}.");
            }

            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureFloat(UnityEngine.Object target, string propertyName, float value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"Serialized property '{propertyName}' was not found on {target.GetType().Name}.");
            }

            property.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureEnum(UnityEngine.Object target, string propertyName, int value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"Serialized property '{propertyName}' was not found on {target.GetType().Name}.");
            }

            property.enumValueIndex = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureObjectArray(UnityEngine.Object target, string propertyName, params UnityEngine.Object[] values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null || !property.isArray)
            {
                throw new InvalidOperationException($"Serialized array '{propertyName}' was not found on {target.GetType().Name}.");
            }

            property.arraySize = values?.Length ?? 0;
            for (var i = 0; i < property.arraySize; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
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

        private readonly struct NarrativeContent
        {
            public NarrativeContent(
                NarrativeScenarioDefinition scenario,
                PhoneMessageDefinition phoneMessage,
                NarrativeNoteDefinition note)
            {
                Scenario = scenario;
                PhoneMessage = phoneMessage;
                Note = note;
            }

            public NarrativeScenarioDefinition Scenario { get; }
            public PhoneMessageDefinition PhoneMessage { get; }
            public NarrativeNoteDefinition Note { get; }
        }
    }
}
