using System.IO;
using Setus.HorrorFramework.Interaction.Interactables;
using Setus.HorrorFramework.Interaction.Interactors;
using Setus.HorrorFramework.Interaction.Triggers;
using Setus.HorrorFramework.Player.Controller;
using Setus.HorrorFramework.SaveProgression.StableIds;
using Setus.HorrorFramework.UI.Prompt;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Setus.HorrorFramework.Editor.Builders
{
    public static class InteractionMilestoneBuilder
    {
        private const string GameplayScenePath = "Assets/Game/Scenes/Gameplay.unity";
        private const string FixtureRootName = "M5InteractionFixtures";
        private const string DebugKeyItemId = "m5.debug.key";

        [MenuItem("Setus/Horror Framework/Interaction/Apply M5 Interaction Foundation")]
        public static void ApplyM5InteractionFoundationFromMenu()
        {
            ApplyM5InteractionFoundation();
        }

        public static void ApplyM5InteractionFoundation()
        {
            PlayerFoundationTemplateBuilder.BuildDefaultAssets(false);
            SceneFlowUiShellTemplateBuilder.BuildDefaultUiShell(false);

            var inputActions = EnsureInteractInputAction();
            HardenPlayerPrefab(inputActions);
            HardenUiShellPrefab();
            HardenGameplayScene(inputActions);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("Applied M5 interaction foundation artifacts.");
        }

        private static InputActionAsset EnsureInteractInputAction()
        {
            var path = PlayerFoundationTemplateBuilder.InputActionsPath;
            var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            if (inputActions == null)
            {
                return null;
            }

            if (inputActions.FindAction("Player/Interact", false) != null)
            {
                return inputActions;
            }

            var playerMap = inputActions.FindActionMap("Player", false) ?? inputActions.AddActionMap("Player");
            var interact = playerMap.AddAction("Interact", InputActionType.Button);
            interact.expectedControlType = "Button";
            interact.AddBinding("<Keyboard>/e");
            interact.AddBinding("<Gamepad>/buttonSouth");

            File.WriteAllText(path, inputActions.ToJson());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
        }

        private static void HardenPlayerPrefab(InputActionAsset inputActions)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerFoundationTemplateBuilder.PlayerPrefabPath) == null)
            {
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(PlayerFoundationTemplateBuilder.PlayerPrefabPath);
            try
            {
                ConfigurePlayerInteractor(root, inputActions);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerFoundationTemplateBuilder.PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void HardenUiShellPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(SceneFlowUiShellTemplateBuilder.UiShellPrefabPath) == null)
            {
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(SceneFlowUiShellTemplateBuilder.UiShellPrefabPath);
            try
            {
                EnsurePromptPresenter(root);
                PrefabUtility.SaveAsPrefabAsset(root, SceneFlowUiShellTemplateBuilder.UiShellPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void HardenGameplayScene(InputActionAsset inputActions)
        {
            var scene = OpenOrCreateScene(GameplayScenePath);
            var player = GameObject.Find("SetusFirstPersonPlayer");
            if (player == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerFoundationTemplateBuilder.PlayerPrefabPath);
                if (prefab != null)
                {
                    player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    player.name = "SetusFirstPersonPlayer";
                    player.transform.position = new Vector3(0f, 0.4f, -4f);
                    player.transform.rotation = Quaternion.identity;
                }
            }

            if (player != null)
            {
                ConfigurePlayerInteractor(player, inputActions);
            }

            EnsureFixtureObjects();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, GameplayScenePath);
        }

        private static void ConfigurePlayerInteractor(GameObject player, InputActionAsset inputActions)
        {
            var interactor = EnsureComponent<RaycastInteractor>(player);
            var camera = player.GetComponentInChildren<Camera>(true);
            if (camera != null)
            {
                interactor.ConfigureRayOrigin(camera.transform);
            }

            var interactAction = inputActions != null ? inputActions.FindAction("Player/Interact", false) : null;
            if (interactAction != null)
            {
                interactor.ConfigureInteractAction(interactAction);
            }
        }

        private static void EnsurePromptPresenter(GameObject root)
        {
            var presenter = EnsureComponent<InteractionPromptPresenter>(root);
            var panel = FindChild(root.transform, "InteractionPromptPanel");
            if (panel == null)
            {
                panel = CreateUiObject("InteractionPromptPanel").transform;
                panel.SetParent(root.transform, false);
                var rect = panel.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0f);
                rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = new Vector2(0f, 96f);
                rect.sizeDelta = new Vector2(520f, 92f);
            }

            var stack = FindChild(panel, "PromptStack");
            if (stack == null)
            {
                stack = CreateUiObject("PromptStack").transform;
                stack.SetParent(panel, false);
                var rect = stack.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(520f, 92f);

                var layout = stack.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.spacing = 4f;
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childControlWidth = true;
                layout.childControlHeight = false;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
            }

            var prompt = EnsureText(stack, "PromptText", string.Empty, 24, TextAnchor.MiddleCenter);
            var debug = EnsureText(stack, "DebugText", string.Empty, 16, TextAnchor.MiddleCenter);
            debug.color = new Color(0.65f, 0.95f, 1f, 1f);
            debug.gameObject.SetActive(false);
            panel.gameObject.SetActive(false);

            ConfigureObjectReference(presenter, "root", panel.gameObject);
            ConfigureObjectReference(presenter, "promptText", prompt);
            ConfigureObjectReference(presenter, "debugText", debug);

            EnsureCrosshair(root);
        }

        private static void EnsureCrosshair(GameObject root)
        {
            var presenter = EnsureComponent<CrosshairPresenter>(root);
            var crosshair = FindChild(root.transform, "Crosshair");
            if (crosshair == null)
            {
                crosshair = CreateUiObject("Crosshair").transform;
                crosshair.SetParent(root.transform, false);
                var rect = crosshair.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(28f, 28f);
            }

            EnsureCrosshairLine(crosshair, "Horizontal", new Vector2(18f, 2f));
            EnsureCrosshairLine(crosshair, "Vertical", new Vector2(2f, 18f));
            crosshair.gameObject.SetActive(false);
            ConfigureObjectReference(presenter, "root", crosshair.gameObject);
        }

        private static void EnsureCrosshairLine(Transform parent, string name, Vector2 size)
        {
            var child = FindChild(parent, name);
            var go = child != null ? child.gameObject : CreateUiObject(name);
            if (child == null)
            {
                go.transform.SetParent(parent, false);
            }

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;

            var image = EnsureComponent<Image>(go);
            image.color = new Color(1f, 1f, 1f, 0.72f);
            image.raycastTarget = false;
        }

        private static void EnsureFixtureObjects()
        {
            var root = GameObject.Find(FixtureRootName);
            if (root == null)
            {
                root = new GameObject(FixtureRootName);
            }

            EnsureDoor(root.transform, "M5_Door", "m5.door", new Vector3(0f, 1f, 2.5f), false);
            EnsureDrawer(root.transform, "M5_Drawer", "m5.drawer", new Vector3(1.35f, 0.55f, 2.5f));
            EnsurePickup(root.transform, "M5_KeyPickup", "m5.keypickup", new Vector3(-1.35f, 0.45f, 2.5f), DebugKeyItemId);
            EnsureDoor(root.transform, "M5_LockedDoor", "m5.lockeddoor", new Vector3(-2.7f, 1f, 2.5f), true);
            EnsureInspectable(root.transform, "M5_Inspectable", "m5.inspectable", new Vector3(2.7f, 0.7f, 2.5f));
            EnsureTrigger(root.transform, "M5_TriggerZone", "m5.trigger", new Vector3(0f, 1f, 5.5f));
        }

        private static void EnsureDoor(Transform parent, string name, string stableId, Vector3 position, bool locked)
        {
            var go = FindOrCreatePrimitive(parent, name, PrimitiveType.Cube);
            go.transform.position = position;
            go.transform.localScale = new Vector3(0.2f, 2f, 1.1f);
            SetStableId(go, stableId);
            var door = EnsureComponent<DoorInteractable>(go);
            ConfigureObjectReference(door, "movingRoot", go.transform);
            ConfigureBool(door, "isLocked", locked);
            ConfigureNestedString(door, "lockRequirement", "requiredItemId", locked ? DebugKeyItemId : string.Empty);
            ConfigureNestedBool(door, "lockRequirement", "consumeItemOnUnlock", false);
        }

        private static void EnsureDrawer(Transform parent, string name, string stableId, Vector3 position)
        {
            var go = FindOrCreatePrimitive(parent, name, PrimitiveType.Cube);
            go.transform.position = position;
            go.transform.localScale = new Vector3(0.8f, 0.45f, 0.55f);
            SetStableId(go, stableId);
            var drawer = EnsureComponent<DrawerInteractable>(go);
            ConfigureObjectReference(drawer, "movingRoot", go.transform);
        }

        private static void EnsurePickup(Transform parent, string name, string stableId, Vector3 position, string itemId)
        {
            var go = FindOrCreatePrimitive(parent, name, PrimitiveType.Cube);
            go.transform.position = position;
            go.transform.localScale = new Vector3(0.32f, 0.32f, 0.32f);
            SetStableId(go, stableId);
            var pickup = EnsureComponent<PickupInteractable>(go);
            ConfigureString(pickup, "itemIdOverride", itemId);
        }

        private static void EnsureInspectable(Transform parent, string name, string stableId, Vector3 position)
        {
            var go = FindOrCreatePrimitive(parent, name, PrimitiveType.Cube);
            go.transform.position = position;
            go.transform.localScale = new Vector3(0.75f, 0.75f, 0.18f);
            SetStableId(go, stableId);
            var inspectable = EnsureComponent<InspectableInteractable>(go);
            ConfigureString(inspectable, "inspectText", "M5 inspectable fixture.");
        }

        private static void EnsureTrigger(Transform parent, string name, string stableId, Vector3 position)
        {
            var go = FindOrCreatePrimitive(parent, name, PrimitiveType.Cube);
            go.transform.position = position;
            go.transform.localScale = new Vector3(2f, 2f, 0.35f);
            SetStableId(go, stableId);
            var collider = EnsureComponent<BoxCollider>(go);
            collider.isTrigger = true;
            EnsureComponent<InteractionTriggerZone>(go);
        }

        private static GameObject FindOrCreatePrimitive(Transform parent, string name, PrimitiveType primitiveType)
        {
            var existing = FindChild(parent, name);
            if (existing != null)
            {
                return existing.gameObject;
            }

            var go = GameObject.CreatePrimitive(primitiveType);
            go.name = name;
            go.transform.SetParent(parent, true);
            return go;
        }

        private static void SetStableId(GameObject target, string id)
        {
            var stableId = EnsureComponent<StableId>(target);
            stableId.Assign(id);
        }

        private static Scene OpenOrCreateScene(string path)
        {
            if (File.Exists(path))
            {
                return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, path);
            return scene;
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

        private static Text EnsureText(Transform parent, string name, string text, int size, TextAnchor alignment)
        {
            var child = FindChild(parent, name);
            var go = child != null ? child.gameObject : CreateUiObject(name);
            if (child == null)
            {
                go.transform.SetParent(parent, false);
            }

            var label = EnsureComponent<Text>(go);
            label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = size;
            label.alignment = alignment;
            label.color = Color.white;
            label.raycastTarget = false;

            var layout = EnsureComponent<LayoutElement>(go);
            layout.preferredHeight = size * 1.5f;
            return label;
        }

        private static GameObject CreateUiObject(string name)
        {
            return new GameObject(name, typeof(RectTransform));
        }

        private static T EnsureComponent<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static void ConfigureObjectReference(Object target, string propertyName, Object value)
        {
            var property = BeginSerializedProperty(target, propertyName);
            if (property == null)
            {
                return;
            }

            property.objectReferenceValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureString(Object target, string propertyName, string value)
        {
            var property = BeginSerializedProperty(target, propertyName);
            if (property == null)
            {
                return;
            }

            property.stringValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureBool(Object target, string propertyName, bool value)
        {
            var property = BeginSerializedProperty(target, propertyName);
            if (property == null)
            {
                return;
            }

            property.boolValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureNestedString(Object target, string propertyName, string childPropertyName, string value)
        {
            var property = BeginNestedSerializedProperty(target, propertyName, childPropertyName);
            if (property == null)
            {
                return;
            }

            property.stringValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureNestedBool(Object target, string propertyName, string childPropertyName, bool value)
        {
            var property = BeginNestedSerializedProperty(target, propertyName, childPropertyName);
            if (property == null)
            {
                return;
            }

            property.boolValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SerializedProperty BeginSerializedProperty(Object target, string propertyName)
        {
            if (target == null)
            {
                return null;
            }

            var serialized = new SerializedObject(target);
            return serialized.FindProperty(propertyName);
        }

        private static SerializedProperty BeginNestedSerializedProperty(
            Object target,
            string propertyName,
            string childPropertyName)
        {
            var property = BeginSerializedProperty(target, propertyName);
            return property?.FindPropertyRelative(childPropertyName);
        }
    }
}
