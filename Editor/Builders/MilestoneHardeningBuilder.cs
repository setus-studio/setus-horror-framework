using Setus.HorrorFramework.Core.Bootstrap;
using Setus.HorrorFramework.Core.Config;
using Setus.HorrorFramework.Player.Controller;
using Setus.HorrorFramework.SceneFlow.Loading;
using Setus.HorrorFramework.SceneFlow.Transitions;
using Setus.HorrorFramework.UI.Menus;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Setus.HorrorFramework.Editor.Builders
{
    public static class MilestoneHardeningBuilder
    {
        private const string BootScenePath = "Assets/Game/Scenes/Boot.unity";
        private const string MainMenuScenePath = "Assets/Game/Scenes/MainMenu.unity";
        private const string GameplayScenePath = "Assets/Game/Scenes/Gameplay.unity";
        private const string FrameworkConfigPath = "Assets/Game/Settings/HorrorFrameworkConfig.asset";

        [MenuItem("Setus/Horror Framework/Validation/Apply M0-M4 Hardening")]
        public static void ApplyM0ToM4HardeningFromMenu()
        {
            ApplyM0ToM4Hardening();
        }

        public static void ApplyM0ToM4Hardening()
        {
            PlayerFoundationTemplateBuilder.BuildDefaultAssets(false);
            SceneFlowUiShellTemplateBuilder.BuildDefaultUiShell(false);

            HardenPlayerPrefab();
            HardenUiShellPrefab();
            HardenBootScene();
            HardenMainMenuScene();
            HardenGameplayScene();

            AssetDatabase.SaveAssets();
            Debug.Log("Applied M0-M4 hardening artifacts.");
        }

        private static void HardenPlayerPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerFoundationTemplateBuilder.PlayerPrefabPath);
            if (prefab == null)
            {
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(PlayerFoundationTemplateBuilder.PlayerPrefabPath);
            try
            {
                var controller = root.GetComponent<FirstPersonPlayerController>();
                var saveAdapter = root.GetComponent<PlayerPoseSaveAdapter>();
                if (saveAdapter == null)
                {
                    saveAdapter = root.AddComponent<PlayerPoseSaveAdapter>();
                }

                ConfigureObjectReference(saveAdapter, "playerController", controller);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerFoundationTemplateBuilder.PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void HardenUiShellPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SceneFlowUiShellTemplateBuilder.UiShellPrefabPath);
            if (prefab == null)
            {
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(SceneFlowUiShellTemplateBuilder.UiShellPrefabPath);
            try
            {
                EnsureComponent<UiShellLifetime>(root);
                EnsureComponent<UiShellEventSystem>(root);
                var debugSaveLoad = EnsureComponent<DebugSaveLoadPanel>(root);
                var commandAdapter = root.GetComponent<UiShellCommandAdapter>();
                var presenter = root.GetComponent<UiShellCanvasPresenter>();
                var mainMenuPanel = FindChild(root.transform, "MainMenuPanel");
                if (mainMenuPanel != null && commandAdapter != null)
                {
                    HardenMainMenu(mainMenuPanel, commandAdapter);
                }
                var pausePanel = FindChild(root.transform, "PauseMenuPanel");
                if (pausePanel != null && presenter != null)
                {
                    HardenPauseMenu(pausePanel, presenter);
                }

                var slotsPanel = FindChild(root.transform, "SaveLoadSlotsPanel");
                if (slotsPanel != null && presenter != null)
                {
                    HardenSaveLoadSlots(slotsPanel, presenter, debugSaveLoad);
                }

                PrefabUtility.SaveAsPrefabAsset(root, SceneFlowUiShellTemplateBuilder.UiShellPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void HardenBootScene()
        {
            EnsureFolder("Assets/Game/Scenes");
            var scene = OpenOrCreateScene(BootScenePath);
            var root = FindOrCreateRoot("SetusBoot");
            var bootstrapper = EnsureComponent<HorrorGameBootstrapper>(root);
            var bootLoader = EnsureComponent<BootFlowLoader>(root);
            var frameworkConfig = CreateOrLoadFrameworkConfig();
            var gameConfig = AssetDatabase.LoadAssetAtPath<StandaloneGameConfig>(SceneFlowUiShellTemplateBuilder.ConfigPath);
            var uiShell = EnsureUiShellInBootScene();
            var transitionRunner = uiShell != null ? uiShell.GetComponent<SceneTransitionRunner>() : null;
            if (transitionRunner == null)
            {
                throw new System.InvalidOperationException("Boot scene requires SetusUiShell with SceneTransitionRunner.");
            }

            ConfigureObjectReference(bootstrapper, "config", frameworkConfig);
            ConfigureObjectReference(bootLoader, "frameworkConfig", frameworkConfig);
            ConfigureObjectReference(bootLoader, "gameConfig", gameConfig);
            ConfigureObjectReference(bootLoader, "transitionRunner", transitionRunner);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, BootScenePath);
        }

        private static void HardenMainMenuScene()
        {
            var scene = OpenOrCreateScene(MainMenuScenePath);
            EnsureMainMenuCamera();
            RemoveSceneAuthoredUiShells();
            DisableSceneEventSystemsOutsideUiShell();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
        }

        private static void HardenGameplayScene()
        {
            var scene = OpenOrCreateScene(GameplayScenePath);
            var player = GameObject.Find("SetusFirstPersonPlayer");
            if (player != null)
            {
                var controller = player.GetComponent<FirstPersonPlayerController>();
                var saveAdapter = EnsureComponent<PlayerPoseSaveAdapter>(player);
                ConfigureObjectReference(saveAdapter, "playerController", controller);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, GameplayScenePath);
        }

        private static void HardenSaveLoadSlots(
            Transform slotsPanel,
            UiShellCanvasPresenter presenter,
            DebugSaveLoadPanel debugSaveLoad)
        {
            var stack = FindChild(slotsPanel, "SlotsStack");
            if (stack == null)
            {
                return;
            }

            var placeholder = FindChild(stack, "SlotPlaceholder");
            if (placeholder != null)
            {
                Object.DestroyImmediate(placeholder.gameObject);
            }

            var title = FindChild(stack, "Title")?.GetComponent<Text>();
            if (title != null)
            {
                title.text = "Save / Load";
            }

            var statusTransform = FindChild(stack, "SaveStatus") ?? FindChild(stack, "DebugSlotStatus");
            var status = statusTransform?.GetComponent<Text>();
            if (status == null)
            {
                status = CreateText(stack, "SaveStatus", "No save available.", 22, TextAnchor.MiddleCenter);
            }
            else
            {
                status.gameObject.name = "SaveStatus";
                status.text = "No save available.";
            }

            var quickSave = EnsureButton(stack, "QuickSaveButton", "Quick Save", debugSaveLoad.QuickSave);
            var quickLoad = EnsureButton(stack, "QuickLoadButton", "Quick Load", debugSaveLoad.QuickLoad);
            var back = EnsureButton(stack, "BackButton", "Back", presenter.ShowReturnScreen);

            status.transform.SetSiblingIndex(1);
            quickSave.transform.SetSiblingIndex(2);
            quickLoad.transform.SetSiblingIndex(3);
            back.transform.SetSiblingIndex(4);
            ConfigureObjectReference(debugSaveLoad, "statusText", status);
            ConfigureObjectReference(debugSaveLoad, "quickSaveButton", quickSave);
            ConfigureObjectReference(debugSaveLoad, "quickLoadButton", quickLoad);
        }

        private static void HardenPauseMenu(Transform pausePanel, UiShellCanvasPresenter presenter)
        {
            var stack = FindChild(pausePanel, "PauseStack");
            if (stack == null)
            {
                return;
            }

            EnsureButton(stack, "SaveLoadButton", "Save / Load", presenter.ShowSaveLoadSlots);
        }

        private static void HardenMainMenu(Transform mainMenuPanel, UiShellCommandAdapter commandAdapter)
        {
            var stack = FindChild(mainMenuPanel, "MenuStack");
            if (stack == null)
            {
                return;
            }

            var continueButton = EnsureButton(stack, "ContinueButton", "Continue", commandAdapter.ContinueFromSave);
            ConfigureObjectReference(commandAdapter, "continueButton", continueButton);
        }

        private static GameObject EnsureUiShellInBootScene()
        {
            var existingShells = Object.FindObjectsByType<UiShellLifetime>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (existingShells.Length > 0)
            {
                return existingShells[0].gameObject;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SceneFlowUiShellTemplateBuilder.UiShellPrefabPath);
            if (prefab == null)
            {
                return null;
            }

            return PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        }

        private static void RemoveSceneAuthoredUiShells()
        {
            var shells = Object.FindObjectsByType<UiShellLifetime>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (var shell in shells)
            {
                Object.DestroyImmediate(shell.gameObject);
            }
        }

        private static HorrorFrameworkConfig CreateOrLoadFrameworkConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<HorrorFrameworkConfig>(FrameworkConfigPath);
            if (existing != null)
            {
                return existing;
            }

            EnsureFolder("Assets/Game/Settings");
            var config = ScriptableObject.CreateInstance<HorrorFrameworkConfig>();
            AssetDatabase.CreateAsset(config, FrameworkConfigPath);
            AssetDatabase.ImportAsset(FrameworkConfigPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<HorrorFrameworkConfig>(FrameworkConfigPath);
        }

        private static void EnsureMainMenuCamera()
        {
            var cameraObject = GameObject.Find("MainMenuCamera");
            if (cameraObject == null)
            {
                cameraObject = new GameObject("MainMenuCamera");
                cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            }

            var camera = EnsureComponent<Camera>(cameraObject);
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.depth = -10f;
        }

        private static void DisableSceneEventSystemsOutsideUiShell()
        {
            var eventSystems = Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (var eventSystem in eventSystems)
            {
                if (eventSystem.GetComponentInParent<UiShellEventSystem>() != null)
                {
                    continue;
                }

                eventSystem.gameObject.SetActive(false);
            }
        }

        private static Scene OpenOrCreateScene(string path)
        {
            if (System.IO.File.Exists(path))
            {
                return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, path);
            return scene;
        }

        private static GameObject FindOrCreateRoot(string name)
        {
            var existing = GameObject.Find(name);
            return existing != null ? existing : new GameObject(name);
        }

        private static Transform FindChild(Transform root, string name)
        {
            var children = root.GetComponentsInChildren<Transform>(true);
            foreach (var child in children)
            {
                if (child.name == name)
                {
                    return child;
                }
            }

            return null;
        }

        private static Button EnsureButton(Transform parent, string name, string text, UnityAction action)
        {
            var existing = FindChild(parent, name);
            var go = existing != null ? existing.gameObject : CreateUiObject(name);
            if (existing == null)
            {
                go.transform.SetParent(parent, false);
            }

            var image = EnsureComponent<Image>(go);
            image.color = new Color(0.18f, 0.2f, 0.23f, 1f);

            var button = EnsureComponent<Button>(go);
            ClearButtonListeners(button);
            UnityEventTools.AddPersistentListener(button.onClick, action);

            var layout = EnsureComponent<LayoutElement>(go);
            layout.preferredHeight = 56f;

            var label = FindChild(go.transform, "Label")?.GetComponent<Text>();
            if (label == null)
            {
                label = CreateText(go.transform, "Label", text, 24, TextAnchor.MiddleCenter);
                Stretch(label.rectTransform);
            }
            else
            {
                label.text = text;
            }

            return button;
        }

        private static void ClearButtonListeners(Button button)
        {
            button.onClick.RemoveAllListeners();
            while (button.onClick.GetPersistentEventCount() > 0)
            {
                UnityEventTools.RemovePersistentListener(button.onClick, 0);
            }
        }

        private static Text CreateText(Transform parent, string name, string text, int size, TextAnchor alignment)
        {
            var go = CreateUiObject(name);
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<Text>();
            label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = size;
            label.alignment = alignment;
            label.color = Color.white;
            label.raycastTarget = false;

            var layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = size * 1.7f;
            return label;
        }

        private static GameObject CreateUiObject(string name)
        {
            return new GameObject(name, typeof(RectTransform));
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static T EnsureComponent<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static void ConfigureObjectReference(Object target, string propertyName, Object value)
        {
            if (target == null)
            {
                return;
            }

            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureBool(Object target, string propertyName, bool value)
        {
            if (target == null)
            {
                return;
            }

            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
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
