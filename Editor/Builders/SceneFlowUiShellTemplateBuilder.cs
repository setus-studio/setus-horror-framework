using Setus.HorrorFramework.SceneFlow.Loading;
using Setus.HorrorFramework.SceneFlow.Transitions;
using Setus.HorrorFramework.UI.Menus;
using Setus.HorrorFramework.UI.Prompt;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Setus.HorrorFramework.Editor.Builders
{
    public static class SceneFlowUiShellTemplateBuilder
    {
        public const string ConfigPath = "Assets/Game/Settings/StandaloneGameConfig.asset";
        public const string UiShellPrefabPath = "Assets/Game/Prefabs/UI/SetusUiShell.prefab";

        [MenuItem("Setus/Horror Framework/Scene Flow/Create Or Select UI Shell Template")]
        public static void CreateDefaultUiShellFromMenu()
        {
            BuildDefaultUiShell(false);
            Debug.Log($"UI shell template ready: {UiShellPrefabPath}");
        }

        [MenuItem("Setus/Horror Framework/Scene Flow/Regenerate UI Shell Template (Destructive)")]
        public static void RegenerateDefaultUiShellFromMenu()
        {
            if (!EditorUtility.DisplayDialog(
                    "Regenerate UI Shell Template",
                    "This deletes and recreates the UI shell prefab. Its GUID and designer edits will be lost.",
                    "Regenerate",
                    "Cancel"))
            {
                return;
            }

            BuildDefaultUiShell(true);
            Debug.LogWarning($"Regenerated UI shell template: {UiShellPrefabPath}");
        }

        public static GameObject BuildDefaultUiShell(bool destructiveRegeneration)
        {
            return BuildUiShellAtPaths(ConfigPath, UiShellPrefabPath, destructiveRegeneration);
        }

        public static GameObject BuildUiShellAtPaths(
            string configPath,
            string uiShellPrefabPath,
            bool destructiveRegeneration)
        {
            ValidateAssetPath(configPath, nameof(configPath));
            ValidateAssetPath(uiShellPrefabPath, nameof(uiShellPrefabPath));
            EnsureParentFolder(configPath);
            EnsureParentFolder(uiShellPrefabPath);

            var config = CreateOrLoadConfig(configPath);
            if (destructiveRegeneration && AssetDatabase.LoadAssetAtPath<GameObject>(uiShellPrefabPath) != null)
            {
                AssetDatabase.DeleteAsset(uiShellPrefabPath);
            }

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(uiShellPrefabPath);
            if (existing != null)
            {
                return existing;
            }

            var root = CreateUiObject("SetusUiShell");
            try
            {
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                root.AddComponent<GraphicRaycaster>();

                var scaler = root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                var runner = root.AddComponent<SceneTransitionRunner>();
                var adapter = root.AddComponent<UiShellCommandAdapter>();
                var presenter = root.AddComponent<UiShellCanvasPresenter>();
                var debugSaveLoad = root.AddComponent<DebugSaveLoadPanel>();
                var promptPresenter = root.AddComponent<InteractionPromptPresenter>();
                var crosshairPresenter = root.AddComponent<CrosshairPresenter>();
                root.AddComponent<UiShellLifetime>();
                root.AddComponent<UiShellEventSystem>();
                var pauseInput = root.AddComponent<PauseInputAdapter>();
                ConfigureObjectReference(runner, "gameConfig", config);
                ConfigureObjectReference(adapter, "gameConfig", config);
                ConfigureObjectReference(adapter, "transitionRunner", runner);
                ConfigureObjectReference(adapter, "pauseInputAdapter", pauseInput);

                var mainMenu = CreatePanel(root.transform, "MainMenuPanel", new Color(0.02f, 0.025f, 0.03f, 0.96f));
                var pauseMenu = CreatePanel(root.transform, "PauseMenuPanel", new Color(0.02f, 0.025f, 0.03f, 0.88f));
                var saveLoadSlots = CreatePanel(root.transform, "SaveLoadSlotsPanel", new Color(0.02f, 0.025f, 0.03f, 0.94f));
                var loading = CreatePanel(root.transform, "LoadingPanel", new Color(0.01f, 0.012f, 0.016f, 0.98f));
                var promptPanel = CreatePromptPanel(root.transform, promptPresenter);
                var crosshair = CreateCrosshair(root.transform, crosshairPresenter);

                BuildMainMenu(mainMenu.transform, adapter, presenter);
                BuildPauseMenu(pauseMenu.transform, adapter, presenter);
                BuildSaveLoadSlots(saveLoadSlots.transform, presenter, debugSaveLoad);
                BuildLoading(loading.transform);

                mainMenu.SetActive(true);
                pauseMenu.SetActive(false);
                saveLoadSlots.SetActive(false);
                loading.SetActive(false);
                promptPanel.SetActive(false);
                crosshair.SetActive(false);

                ConfigureObjectReference(presenter, "mainMenuPanel", mainMenu);
                ConfigureObjectReference(presenter, "pauseMenuPanel", pauseMenu);
                ConfigureObjectReference(presenter, "saveLoadSlotsPanel", saveLoadSlots);
                ConfigureObjectReference(presenter, "loadingPanel", loading);

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, uiShellPrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(uiShellPrefabPath, ImportAssetOptions.ForceSynchronousImport);
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static StandaloneGameConfig CreateOrLoadConfig(string configPath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<StandaloneGameConfig>(configPath);
            if (existing != null)
            {
                return existing;
            }

            var config = ScriptableObject.CreateInstance<StandaloneGameConfig>();
            AssetDatabase.CreateAsset(config, configPath);
            AssetDatabase.ImportAsset(configPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<StandaloneGameConfig>(configPath);
        }

        private static GameObject CreatePanel(Transform parent, string name, Color color)
        {
            var panel = CreateUiObject(name);
            panel.transform.SetParent(parent, false);
            Stretch(panel.GetComponent<RectTransform>());
            var image = panel.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            return panel;
        }

        private static void BuildMainMenu(Transform panel, UiShellCommandAdapter adapter, UiShellCanvasPresenter presenter)
        {
            var stack = CreateStack(panel, "MenuStack", 520f, 520f);
            CreateText(stack, "Title", "SETUS", 48, TextAnchor.MiddleCenter);
            CreateButton(stack, "NewGameButton", "New Game", adapter.StartNewGame);
            var continueButton = CreateButton(stack, "ContinueButton", "Continue", adapter.ContinueFromSave);
            CreateButton(stack, "SaveLoadButton", "Save / Load", presenter.ShowSaveLoadSlots);
            ConfigureObjectReference(adapter, "continueButton", continueButton);
        }

        private static void BuildPauseMenu(
            Transform panel,
            UiShellCommandAdapter adapter,
            UiShellCanvasPresenter presenter)
        {
            var stack = CreateStack(panel, "PauseStack", 440f, 420f);
            CreateText(stack, "Title", "Paused", 38, TextAnchor.MiddleCenter);
            CreateButton(stack, "ResumeButton", "Resume", adapter.Resume);
            CreateButton(stack, "SaveLoadButton", "Save / Load", presenter.ShowSaveLoadSlots);
            CreateButton(stack, "MainMenuButton", "Main Menu", adapter.LoadMainMenu);
        }

        private static void BuildSaveLoadSlots(
            Transform panel,
            UiShellCanvasPresenter presenter,
            DebugSaveLoadPanel debugSaveLoad)
        {
            var stack = CreateStack(panel, "SlotsStack", 620f, 520f);
            CreateText(stack, "Title", "Save / Load", 38, TextAnchor.MiddleCenter);
            var status = CreateText(stack, "SaveStatus", "No save available.", 22, TextAnchor.MiddleCenter);
            var quickSave = CreateButton(stack, "QuickSaveButton", "Quick Save", debugSaveLoad.QuickSave);
            var quickLoad = CreateButton(stack, "QuickLoadButton", "Quick Load", debugSaveLoad.QuickLoad);
            CreateButton(stack, "BackButton", "Back", presenter.ShowReturnScreen);

            ConfigureObjectReference(debugSaveLoad, "statusText", status);
            ConfigureObjectReference(debugSaveLoad, "quickSaveButton", quickSave);
            ConfigureObjectReference(debugSaveLoad, "quickLoadButton", quickLoad);
        }

        private static void BuildLoading(Transform panel)
        {
            var stack = CreateStack(panel, "LoadingStack", 520f, 220f);
            CreateText(stack, "LoadingText", "Loading...", 34, TextAnchor.MiddleCenter);
        }

        private static GameObject CreatePromptPanel(Transform parent, InteractionPromptPresenter presenter)
        {
            var panel = CreateUiObject("InteractionPromptPanel");
            panel.transform.SetParent(parent, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 96f);
            rect.sizeDelta = new Vector2(520f, 92f);

            var stack = CreateStack(panel.transform, "PromptStack", 520f, 92f);
            var prompt = CreateText(stack, "PromptText", string.Empty, 24, TextAnchor.MiddleCenter);
            var debug = CreateText(stack, "DebugText", string.Empty, 16, TextAnchor.MiddleCenter);
            debug.color = new Color(0.65f, 0.95f, 1f, 1f);
            debug.gameObject.SetActive(false);
            presenter.Configure(panel, prompt, debug);
            return panel;
        }

        private static GameObject CreateCrosshair(Transform parent, CrosshairPresenter presenter)
        {
            var root = CreateUiObject("Crosshair");
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(28f, 28f);

            CreateCrosshairLine(root.transform, "Horizontal", new Vector2(18f, 2f));
            CreateCrosshairLine(root.transform, "Vertical", new Vector2(2f, 18f));
            presenter.Configure(root);
            return root;
        }

        private static void CreateCrosshairLine(Transform parent, string name, Vector2 size)
        {
            var line = CreateUiObject(name);
            line.transform.SetParent(parent, false);
            var rect = line.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;

            var image = line.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.72f);
            image.raycastTarget = false;
        }

        private static Transform CreateStack(Transform parent, string name, float width, float height)
        {
            var stack = CreateUiObject(name);
            stack.transform.SetParent(parent, false);
            var rect = stack.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);

            var layout = stack.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16f;
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return stack.transform;
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

        private static Button CreateButton(Transform parent, string name, string text, UnityAction action)
        {
            var go = CreateUiObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.18f, 0.2f, 0.23f, 1f);
            var button = go.AddComponent<Button>();
            UnityEventTools.AddPersistentListener(button.onClick, action);

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, 56f);
            var layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = 56f;

            var label = CreateText(go.transform, "Label", text, 24, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
            return button;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static GameObject CreateUiObject(string name)
        {
            return new GameObject(name, typeof(RectTransform));
        }

        private static void ConfigureObjectReference(Object target, string propertyName, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureBool(Object target, string propertyName, bool value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ValidateAssetPath(string path, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/", System.StringComparison.Ordinal))
            {
                throw new System.ArgumentException("Asset paths must be project-relative paths below Assets/.", parameterName);
            }
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
    }
}
