using System;
using System.IO;
using Setus.HorrorFramework.Localization;
using Setus.HorrorFramework.UI.Menus;
using Setus.HorrorFramework.UI.Settings;
using Setus.HorrorFramework.UI.Settings.Display;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Setus.HorrorFramework.Editor.Builders
{
    public static class TabbedSettingsLayoutBuilder
    {
        private static readonly Color Ink = new Color(0.075f, 0.09f, 0.095f, 1f);
        private static readonly Color Band = new Color(0.11f, 0.13f, 0.135f, 1f);
        private static readonly Color Control = new Color(0.17f, 0.19f, 0.2f, 1f);
        private static readonly Color Accent = new Color(0.16f, 0.41f, 0.4f, 1f);
        private static readonly Color TextColor = new Color(0.91f, 0.92f, 0.88f, 1f);

        [MenuItem("Setus/Horror Framework/Settings/Apply Tabbed Settings Layout")]
        public static void Apply()
        {
            ApplyToPrefabAtPath(SceneFlowUiShellTemplateBuilder.UiShellPrefabPath);
        }

        [MenuItem("Setus/Horror Framework/Settings/Apply Production Controls")]
        public static void ApplyProductionControls()
        {
            PlayerFoundationTemplateBuilder.BuildDefaultAssets(false);
            EnsurePauseActionAtPath(PlayerFoundationTemplateBuilder.InputActionsPath);
            ApplyToPrefabAtPath(SceneFlowUiShellTemplateBuilder.UiShellPrefabPath);
        }

        [MenuItem("Setus/Horror Framework/Settings/Apply Display Settings")]
        public static void ApplyDisplaySettings()
        {
            ApplyToPrefabAtPath(SceneFlowUiShellTemplateBuilder.UiShellPrefabPath);
        }

        public static void EnsurePauseActionAtPath(string actionsPath)
        {
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(actionsPath);
            if (actions == null)
                throw new InvalidOperationException($"Input Actions asset is missing: {actionsPath}");
            if (actions.FindAction("Player/Pause", false) != null) return;
            var player = actions.FindActionMap("Player", false);
            if (player == null)
                throw new InvalidOperationException($"Input Actions asset '{actionsPath}' has no Player map.");

            var pause = player.AddAction("Pause", InputActionType.Button);
            pause.expectedControlType = "Button";
            pause.AddBinding("<Keyboard>/escape");
            File.WriteAllText(actionsPath, actions.ToJson());
            AssetDatabase.ImportAsset(actionsPath, ImportAssetOptions.ForceSynchronousImport);
        }

        public static void ApplyToPrefabAtPath(string prefabPath)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                throw new InvalidOperationException($"UI shell prefab is missing: {prefabPath}");
            }

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var panel = Require(root.transform, "SettingsPanel");
                var existingLayout = panel.Find("GameSettingsLayout");
                if (existingLayout != null)
                {
                    var changed = ApplyScrollPolicy(existingLayout);
                    changed |= EnsurePhase2Controls(panel, existingLayout);
                    changed |= EnsurePhase3Controls(root.transform, panel, existingLayout);
                    changed |= EnsurePhase4Controls(root.transform, panel, existingLayout);
                    if (changed)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    }
                    Debug.Log("Game Settings layout is current.");
                    return;
                }

                var shell = root.GetComponent<UiShellCanvasPresenter>();
                var legacyScroll = Require(panel, "SettingsScroll");
                var source = Require(legacyScroll, "Viewport/Content");
                var mainButton = Require(root.transform, "MainMenuPanel/MenuStack/SettingsButton")
                    .GetComponent<UnityEngine.UI.Button>();
                var pauseButton = Require(root.transform, "PauseMenuPanel/PauseStack/SettingsButton")
                    .GetComponent<UnityEngine.UI.Button>();
                if (shell == null || mainButton == null || pauseButton == null)
                {
                    throw new InvalidOperationException("UI shell requires a presenter and both Settings buttons.");
                }

                var audioNames = new[] { "MasterVolumeRow", "AmbienceVolumeRow", "SFXVolumeRow", "UIVolumeRow", "VoiceVolumeRow" };
                var controlsNames = new[]
                {
                    "MouseSensitivityRow", "SprintToggle(off=hold)Row", "BindingsTitle",
                    "MoveForward", "MoveBackward", "MoveLeft", "MoveRight", "Look", "Sprint", "Crouch",
                    "ResetBindings", "RebindStatus"
                };
                var accessibilityNames = new[]
                {
                    "SubtitlesRow", "BrightnessRow", "CameraShakeRow", "HeadBobRow", "HeadBobIntensityRow"
                };
                foreach (var name in audioNames) Require(source, name);
                foreach (var name in controlsNames) Require(source, name);
                foreach (var name in accessibilityNames) Require(source, name);
                var title = Require(source, "Title");
                var back = Require(source, "Back");

                var background = panel.GetComponent<UnityEngine.UI.Image>();
                if (background == null)
                {
                    throw new InvalidOperationException("SettingsPanel requires its existing full-screen Image.");
                }

                var layout = CreateRect("GameSettingsLayout", panel);
                Stretch(layout);
                background.color = Ink;

                var header = CreateRect("Header", layout);
                Anchor(header, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -112f), Vector2.zero);
                AddBand(header, Band);
                title.SetParent(header, false);
                Anchor((RectTransform)title, Vector2.zero, Vector2.one, new Vector2(48f, 0f), new Vector2(-120f, 0f));
                var titleText = title.GetComponent<UnityEngine.UI.Text>();
                titleText.alignment = TextAnchor.MiddleLeft;
                titleText.fontSize = 40;
                titleText.color = TextColor;

                var body = CreateRect("Body", layout);
                Anchor(body, Vector2.zero, Vector2.one, new Vector2(0f, 88f), new Vector2(0f, -112f));
                var sidebar = CreateRect("TabNavigation", body);
                Anchor(sidebar, Vector2.zero, new Vector2(0f, 1f), new Vector2(24f, 0f), new Vector2(280f, 0f));
                AddBand(sidebar, Band);
                var navLayout = sidebar.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
                navLayout.padding = new RectOffset(12, 12, 22, 18);
                navLayout.spacing = 12f;
                navLayout.childControlWidth = true;
                navLayout.childControlHeight = true;
                navLayout.childForceExpandWidth = true;
                navLayout.childForceExpandHeight = false;

                var contentFrame = CreateRect("TabContent", body);
                Anchor(contentFrame, Vector2.zero, Vector2.one, new Vector2(304f, 0f), new Vector2(-28f, 0f));
                var tabs = new[]
                {
                    CreateButton("AudioTab", "Audio", FrameworkTextKeys.SettingsAudioTab, sidebar),
                    CreateButton("ControlsTab", "Controls", FrameworkTextKeys.SettingsControlsTab, sidebar),
                    CreateButton("AccessibilityTab", "Accessibility", FrameworkTextKeys.SettingsAccessibilityTab, sidebar),
                    CreateButton("LanguageTab", "Language", FrameworkTextKeys.SettingsLanguageTab, sidebar)
                };
                tabs[0].targetGraphic.color = Accent;

                var pages = new[]
                {
                    CreatePage("AudioPage", "Audio", FrameworkTextKeys.SettingsAudioTab, contentFrame, out var audioContent),
                    CreatePage("ControlsPage", "Controls", FrameworkTextKeys.SettingsControlsTab, contentFrame, out var controlsContent),
                    CreatePage("AccessibilityPage", "Accessibility", FrameworkTextKeys.SettingsAccessibilityTab, contentFrame, out var accessibilityContent),
                    CreatePage("LanguagePage", "Language", FrameworkTextKeys.SettingsLanguageTab, contentFrame, out var languageContent)
                };

                MoveRows(source, audioContent, audioNames);
                MoveRows(source, controlsContent, controlsNames);
                MoveRows(source, accessibilityContent, accessibilityNames);
                StyleRows(audioContent);
                StyleRows(controlsContent);
                StyleRows(accessibilityContent);

                // UI and Voice remain opt-in until a game wires sources for those categories.
                Require(audioContent, "UIVolumeRow").gameObject.SetActive(false);
                Require(audioContent, "VoiceVolumeRow").gameObject.SetActive(false);

                var localeCodes = new[] { "en", "vi", "ja", "ko", "es", "zh-Hans", "zh-Hant" };
                var localeNames = new[]
                {
                    "English", "Tiếng Việt", "日本語", "한국어", "Español", "简体中文", "繁體中文"
                };
                var localeButtons = new UnityEngine.UI.Button[localeCodes.Length];
                for (var i = 0; i < localeButtons.Length; i++)
                {
                    localeButtons[i] = CreateButton("Locale_" + localeCodes[i], localeNames[i], null, languageContent);
                    localeButtons[i].gameObject.SetActive(i == 0);
                }

                var footer = CreateRect("Footer", layout);
                Anchor(footer, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 88f));
                AddBand(footer, Band);
                back.SetParent(footer, false);
                Anchor((RectTransform)back, Vector2.zero, Vector2.zero, new Vector2(32f, 18f), new Vector2(224f, 70f));
                var backButton = back.GetComponent<UnityEngine.UI.Button>();
                for (var i = backButton.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                {
                    UnityEventTools.RemovePersistentListener(backButton.onClick, i);
                }
                var closeButton = CreateButton("CloseButton", "X", null, header);
                Anchor((RectTransform)closeButton.transform, Vector2.one, Vector2.one,
                    new Vector2(-100f, -88f), new Vector2(-32f, -20f));
                closeButton.GetComponent<UnityEngine.UI.LayoutElement>().enabled = false;

                var controller = panel.gameObject.AddComponent<TabbedSettingsPresenter>();
                controller.Configure(shell, pages, tabs, localeButtons, localeCodes,
                    panel.GetComponent<Setus.HorrorFramework.UI.Settings.InputRebindPresenter>(),
                    mainButton, pauseButton);
                UnityEventTools.AddPersistentListener(tabs[0].onClick, controller.ShowAudio);
                UnityEventTools.AddPersistentListener(tabs[1].onClick, controller.ShowControls);
                UnityEventTools.AddPersistentListener(tabs[2].onClick, controller.ShowAccessibility);
                UnityEventTools.AddPersistentListener(tabs[3].onClick, controller.ShowLanguage);
                UnityEventTools.AddPersistentListener(localeButtons[0].onClick, controller.SelectEnglish);
                UnityEventTools.AddPersistentListener(localeButtons[1].onClick, controller.SelectVietnamese);
                UnityEventTools.AddPersistentListener(localeButtons[2].onClick, controller.SelectJapanese);
                UnityEventTools.AddPersistentListener(localeButtons[3].onClick, controller.SelectKorean);
                UnityEventTools.AddPersistentListener(localeButtons[4].onClick, controller.SelectSpanish);
                UnityEventTools.AddPersistentListener(localeButtons[5].onClick, controller.SelectChineseSimplified);
                UnityEventTools.AddPersistentListener(localeButtons[6].onClick, controller.SelectChineseTraditional);
                UnityEventTools.AddPersistentListener(backButton.onClick, controller.Close);
                UnityEventTools.AddPersistentListener(closeButton.onClick, controller.Close);

                for (var i = 1; i < pages.Length; i++) pages[i].SetActive(false);
                legacyScroll.gameObject.SetActive(false);
                EnsurePhase2Controls(panel, layout);
                EnsurePhase3Controls(root.transform, panel, layout);
                EnsurePhase4Controls(root.transform, panel, layout);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log("Game Settings layout applied to the existing UI shell prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void MoveRows(Transform source, Transform destination, string[] names)
        {
            foreach (var name in names)
            {
                Require(source, name).SetParent(destination, false);
            }
        }

        private static void StyleRows(Transform content)
        {
            foreach (Transform row in content)
            {
                var label = row.Find("Label")?.GetComponent<UnityEngine.UI.Text>();
                if (label != null)
                {
                    label.color = TextColor;
                    label.alignment = TextAnchor.MiddleLeft;
                    label.fontSize = 23;
                }
                var element = row.GetComponent<UnityEngine.UI.LayoutElement>();
                if (element != null && row.name != "BindingsTitle" && row.name != "RebindStatus")
                {
                    element.preferredHeight = 64f;
                }
            }
        }

        private static GameObject CreatePage(
            string name, string heading, string key, Transform parent, out Transform content)
        {
            var page = CreateRect(name, parent);
            Stretch(page);
            var scroll = page.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.horizontal = false;
            ConfigureScroll(scroll);

            var viewport = CreateRect("Viewport", page);
            Stretch(viewport);
            var image = viewport.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(1f, 1f, 1f, 0.01f);
            image.raycastTarget = true;
            viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
            scroll.viewport = viewport;

            var contentRect = CreateRect("Content", viewport);
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = Vector2.one;
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = new Vector2(20f, 0f);
            contentRect.offsetMax = new Vector2(-20f, 0f);
            var layout = contentRect.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 22, 32);
            layout.spacing = 18f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            contentRect.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit =
                UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = contentRect;
            var title = CreateText("Heading", heading, 36, contentRect);
            title.alignment = TextAnchor.MiddleLeft;
            title.gameObject.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 72f;
            title.gameObject.AddComponent<LocalizedTextPresenter>().Configure(title, key, heading);
            content = contentRect;
            return page.gameObject;
        }

        private static bool ApplyScrollPolicy(Transform layout)
        {
            var frame = Require(layout, "Body/TabContent");
            var changed = false;
            foreach (var pageName in new[]
            {
                "AudioPage", "ControlsPage", "AccessibilityPage", "LanguagePage", "DisplayPage"
            })
            {
                var page = frame.Find(pageName);
                if (page == null && pageName == "DisplayPage") continue;
                var scroll = Require(frame, pageName).GetComponent<UnityEngine.UI.ScrollRect>();
                if (scroll == null)
                {
                    throw new InvalidOperationException($"Settings page '{pageName}' requires a ScrollRect.");
                }

                if (scroll.movementType != UnityEngine.UI.ScrollRect.MovementType.Clamped ||
                    scroll.inertia || scroll.scrollSensitivity != 12f)
                {
                    ConfigureScroll(scroll);
                    changed = true;
                }
            }

            return changed;
        }

        private static bool EnsurePhase2Controls(Transform panel, Transform layout)
        {
            var settings = panel.GetComponent<AccessibilitySettingsPanel>();
            if (settings == null)
            {
                throw new InvalidOperationException("SettingsPanel requires AccessibilitySettingsPanel.");
            }

            var frame = Require(layout, "Body/TabContent");
            var audio = Require(frame, "AudioPage/Viewport/Content");
            var controls = Require(frame, "ControlsPage/Viewport/Content");
            var accessibility = Require(frame, "AccessibilityPage/Viewport/Content");
            var changed = false;
            changed |= EnsureValue(settings, audio, "MasterVolumeRow", "masterVolumeValue");
            changed |= EnsureValue(settings, audio, "AmbienceVolumeRow", "ambienceVolumeValue");
            changed |= EnsureValue(settings, audio, "SFXVolumeRow", "sfxVolumeValue");
            changed |= EnsureValue(settings, audio, "UIVolumeRow", "uiVolumeValue");
            changed |= EnsureValue(settings, audio, "VoiceVolumeRow", "voiceVolumeValue");
            changed |= EnsureValue(settings, controls, "MouseSensitivityRow", "mouseSensitivityValue");
            changed |= EnsureValue(settings, accessibility, "BrightnessRow", "brightnessValue");
            changed |= EnsureValue(settings, accessibility, "CameraShakeRow", "cameraShakeValue");
            changed |= EnsureValue(settings, accessibility, "HeadBobIntensityRow", "headBobIntensityValue");
            changed |= EnsureReset(audio, "ResetAudioDefaults", "Reset Audio Defaults",
                FrameworkTextKeys.SettingsResetAudio, settings.ResetAudioDefaults);
            changed |= EnsureReset(controls, "ResetMovementDefaults", "Reset Movement Settings",
                FrameworkTextKeys.SettingsResetMovement, settings.ResetMovementDefaults);
            changed |= EnsureReset(accessibility, "ResetAccessibilityDefaults", "Reset Accessibility Defaults",
                FrameworkTextKeys.SettingsResetAccessibility, settings.ResetAccessibilityDefaults);
            return changed;
        }

        private static bool EnsurePhase3Controls(Transform root, Transform panel, Transform layout)
        {
            var rebind = panel.GetComponent<InputRebindPresenter>();
            if (rebind == null)
                throw new InvalidOperationException("SettingsPanel requires InputRebindPresenter.");

            var actionsProperty = new SerializedObject(rebind).FindProperty("actions");
            var actions = actionsProperty?.objectReferenceValue as UnityEngine.InputSystem.InputActionAsset;
            if (actions == null)
                throw new InvalidOperationException("InputRebindPresenter requires the player Input Actions asset.");
            if (actions.FindAction("Player/Interact", false) == null ||
                actions.FindAction("Player/Pause", false) == null)
                throw new InvalidOperationException(
                    "Player/Interact and Player/Pause are required. Run Apply Production Controls first.");

            var pause = root.GetComponent<PauseInputAdapter>();
            if (pause == null)
                throw new InvalidOperationException("UI shell requires PauseInputAdapter.");
            var pauseSerialized = new SerializedObject(pause);
            var pauseActions = pauseSerialized.FindProperty("actionsAsset");
            var changed = false;
            if (pauseActions.objectReferenceValue == null)
            {
                pauseActions.objectReferenceValue = actions;
                pauseSerialized.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }
            else if (pauseActions.objectReferenceValue != actions)
            {
                throw new InvalidOperationException("PauseInputAdapter and InputRebindPresenter must share one Input Actions asset.");
            }

            var controls = Require(layout, "Body/TabContent/ControlsPage/Viewport/Content");
            var reset = Require(controls, "ResetBindings");
            var interact = controls.Find("Interact");
            if (interact == null)
            {
                interact = CreateButton("Interact", "Interact", FrameworkTextKeys.RebindInteract, controls).transform;
                UnityEventTools.AddPersistentListener(interact.GetComponent<UnityEngine.UI.Button>().onClick,
                    rebind.RebindInteract);
                interact.SetSiblingIndex(reset.GetSiblingIndex());
                changed = true;
            }
            var pauseRow = controls.Find("Pause");
            if (pauseRow == null)
            {
                pauseRow = CreateButton("Pause", "Pause", FrameworkTextKeys.RebindPause, controls).transform;
                UnityEventTools.AddPersistentListener(pauseRow.GetComponent<UnityEngine.UI.Button>().onClick,
                    rebind.RebindPause);
                pauseRow.SetSiblingIndex(reset.GetSiblingIndex());
                changed = true;
            }

            var bindingNames = new[]
            {
                "MoveForward", "MoveBackward", "MoveLeft", "MoveRight", "Look",
                "Sprint", "Crouch", "Interact", "Pause"
            };
            var bindingTexts = new UnityEngine.UI.Text[bindingNames.Length];
            for (var i = 0; i < bindingNames.Length; i++)
            {
                var button = Require(controls, bindingNames[i]);
                var label = Require(button, "Label").GetComponent<UnityEngine.UI.Text>();
                if (label == null)
                    throw new InvalidOperationException($"Rebind row '{bindingNames[i]}' has no label.");
                var current = button.Find("BindingText")?.GetComponent<UnityEngine.UI.Text>();
                if (current == null)
                {
                    var labelRect = (RectTransform)label.transform;
                    Anchor(labelRect, new Vector2(0f, 0f), new Vector2(0.68f, 1f),
                        new Vector2(18f, 0f), Vector2.zero);
                    current = CreateText("BindingText", string.Empty, 22, button);
                    current.alignment = TextAnchor.MiddleRight;
                    current.raycastTarget = false;
                    current.GetComponent<UnityEngine.UI.LayoutElement>().enabled = false;
                    Anchor(current.rectTransform, new Vector2(0.68f, 0f), Vector2.one,
                        Vector2.zero, new Vector2(-18f, 0f));
                    changed = true;
                }
                bindingTexts[i] = current;
            }

            var resetButton = reset.GetComponent<UnityEngine.UI.Button>();
            var resetListenerFound = false;
            for (var i = resetButton.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            {
                if (resetButton.onClick.GetPersistentTarget(i) != rebind) continue;
                var method = resetButton.onClick.GetPersistentMethodName(i);
                if (method == nameof(InputRebindPresenter.RequestResetBindings))
                    resetListenerFound = true;
                else if (method == nameof(InputRebindPresenter.ResetBindings))
                {
                    UnityEventTools.RemovePersistentListener(resetButton.onClick, i);
                    changed = true;
                }
            }
            if (!resetListenerFound)
            {
                UnityEventTools.AddPersistentListener(resetButton.onClick, rebind.RequestResetBindings);
                changed = true;
            }

            var group = layout.GetComponent<UnityEngine.CanvasGroup>();
            if (group == null)
            {
                group = layout.gameObject.AddComponent<UnityEngine.CanvasGroup>();
                changed = true;
            }
            var modal = panel.Find("RebindModal");
            if (modal == null)
            {
                modal = CreateRebindModal(panel, rebind);
                changed = true;
            }
            var dialog = Require(modal, "Dialog");
            changed |= rebind.Configure(bindingTexts, modal.gameObject, group,
                Require(dialog, "Title").GetComponent<UnityEngine.UI.Text>(),
                Require(dialog, "Prompt").GetComponent<UnityEngine.UI.Text>(),
                Require(dialog, "Current").GetComponent<UnityEngine.UI.Text>(),
                Require(dialog, "Actions/Retry").GetComponent<UnityEngine.UI.Button>(),
                Require(dialog, "Actions/ConfirmReset").GetComponent<UnityEngine.UI.Button>(),
                Require(dialog, "Actions/Cancel").GetComponent<UnityEngine.UI.Button>());
            return changed;
        }

        private static Transform CreateRebindModal(Transform parent, InputRebindPresenter rebind)
        {
            var modal = CreateRect("RebindModal", parent);
            Stretch(modal);
            var backdrop = modal.gameObject.AddComponent<UnityEngine.UI.Image>();
            backdrop.color = new Color(0.02f, 0.03f, 0.035f, 0.88f);
            backdrop.raycastTarget = true;

            var dialog = CreateRect("Dialog", modal);
            Anchor(dialog, Vector2.one * 0.5f, Vector2.one * 0.5f,
                new Vector2(-300f, -175f), new Vector2(300f, 175f));
            AddBand(dialog, Band);
            var stack = dialog.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            stack.padding = new RectOffset(24, 24, 20, 20);
            stack.spacing = 10f;
            stack.childControlWidth = true;
            stack.childControlHeight = true;
            stack.childForceExpandWidth = true;
            stack.childForceExpandHeight = false;

            CreateText("Title", string.Empty, 30, dialog);
            CreateText("Prompt", string.Empty, 23, dialog);
            CreateText("Current", string.Empty, 25, dialog);
            var actions = CreateRect("Actions", dialog);
            actions.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 68f;
            var row = actions.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            row.spacing = 12f;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = false;
            var cancel = CreateButton("Cancel", "Cancel", FrameworkTextKeys.RebindDialogCancel, actions);
            var retry = CreateButton("Retry", "Retry", FrameworkTextKeys.RebindDialogRetry, actions);
            var confirm = CreateButton("ConfirmReset", "Reset", FrameworkTextKeys.RebindDialogConfirmReset, actions);
            UnityEventTools.AddPersistentListener(cancel.onClick, rebind.CancelCurrentRebind);
            UnityEventTools.AddPersistentListener(retry.onClick, rebind.RetryCurrentRebind);
            UnityEventTools.AddPersistentListener(confirm.onClick, rebind.ConfirmResetBindings);
            retry.gameObject.SetActive(false);
            confirm.gameObject.SetActive(false);
            modal.gameObject.SetActive(false);
            return modal;
        }

        private static bool EnsurePhase4Controls(Transform root, Transform panel, Transform layout)
        {
            var controller = panel.GetComponent<TabbedSettingsPresenter>();
            if (controller == null)
                throw new InvalidOperationException("SettingsPanel requires TabbedSettingsPresenter.");

            var changed = false;
            if (root.GetComponent<DisplaySettingsRuntimeApplier>() == null)
            {
                root.gameObject.AddComponent<DisplaySettingsRuntimeApplier>();
                changed = true;
            }

            var navigation = Require(layout, "Body/TabNavigation");
            var content = Require(layout, "Body/TabContent");
            var tab = navigation.Find("DisplayTab")?.GetComponent<UnityEngine.UI.Button>();
            if (tab == null)
            {
                tab = CreateButton("DisplayTab", "Display", FrameworkTextKeys.SettingsDisplayTab, navigation);
                changed = true;
            }
            changed |= EnsureButtonListener(tab, controller.ShowDisplay);

            var page = content.Find("DisplayPage");
            if (page == null)
            {
                CreatePage("DisplayPage", "Display", FrameworkTextKeys.SettingsDisplayTab,
                    content, out _).SetActive(false);
                page = Require(content, "DisplayPage");
                changed = true;
            }
            var rows = Require(page, "Viewport/Content");
            changed |= EnsureDisplayDropdown(rows, "ModeRow", "Display Mode", FrameworkTextKeys.DisplayMode);
            changed |= EnsureDisplayDropdown(rows, "ResolutionRow", "Resolution", FrameworkTextKeys.DisplayResolution);
            changed |= EnsureDisplayDropdown(rows, "RefreshRow", "Refresh Rate", FrameworkTextKeys.DisplayRefresh);
            changed |= EnsureDisplayToggle(rows, "VSyncRow", "VSync", FrameworkTextKeys.DisplayVSync);
            changed |= EnsureDisplayDropdown(rows, "FrameRateRow", "FPS Cap", FrameworkTextKeys.DisplayFrameRate);
            changed |= EnsureDisplayDropdown(rows, "QualityRow", "Graphics Quality", FrameworkTextKeys.DisplayQuality);
            changed |= EnsureDisplayDropdown(rows, "AntiAliasingRow", "Anti-aliasing", FrameworkTextKeys.DisplayAntiAliasing);

            var apply = rows.Find("ApplyDisplay")?.GetComponent<UnityEngine.UI.Button>();
            if (apply == null)
            {
                apply = CreateButton("ApplyDisplay", "Apply Display", FrameworkTextKeys.DisplayApply, rows);
                changed = true;
            }
            var status = rows.Find("DisplayStatus")?.GetComponent<UnityEngine.UI.Text>();
            if (status == null)
            {
                status = CreateText("DisplayStatus", string.Empty, 21, rows);
                status.gameObject.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 54f;
                changed = true;
            }

            var modal = panel.Find("DisplayConfirmModal");
            if (modal == null)
            {
                modal = CreateDisplayConfirmModal(panel);
                changed = true;
            }
            var dialog = Require(modal, "Dialog");
            var keep = Require(dialog, "Actions/Keep").GetComponent<UnityEngine.UI.Button>();
            var revert = Require(dialog, "Actions/Revert").GetComponent<UnityEngine.UI.Button>();
            var presenter = panel.GetComponent<DisplaySettingsPresenter>();
            if (presenter == null)
            {
                presenter = panel.gameObject.AddComponent<DisplaySettingsPresenter>();
                changed = true;
            }
            var layoutGroup = layout.GetComponent<UnityEngine.CanvasGroup>();
            if (layoutGroup == null)
                throw new InvalidOperationException("GameSettingsLayout requires a CanvasGroup for display confirmation.");
            changed |= presenter.Configure(
                Require(rows, "ModeRow/Dropdown").GetComponent<UnityEngine.UI.Dropdown>(),
                Require(rows, "ResolutionRow/Dropdown").GetComponent<UnityEngine.UI.Dropdown>(),
                Require(rows, "RefreshRow/Dropdown").GetComponent<UnityEngine.UI.Dropdown>(),
                Require(rows, "VSyncRow/Toggle").GetComponent<UnityEngine.UI.Toggle>(),
                Require(rows, "FrameRateRow/Dropdown").GetComponent<UnityEngine.UI.Dropdown>(),
                Require(rows, "QualityRow/Dropdown").GetComponent<UnityEngine.UI.Dropdown>(),
                Require(rows, "AntiAliasingRow/Dropdown").GetComponent<UnityEngine.UI.Dropdown>(),
                status, apply, modal.gameObject,
                Require(dialog, "Countdown").GetComponent<UnityEngine.UI.Text>(), keep, revert, layoutGroup);
            changed |= EnsureButtonListener(apply, presenter.Apply);
            changed |= EnsureButtonListener(keep, presenter.Keep);
            changed |= EnsureButtonListener(revert, presenter.Revert);
            changed |= controller.ConfigureDisplayTab(page.gameObject, tab, presenter);
            return changed;
        }

        private static bool EnsureDisplayDropdown(Transform parent, string name, string label, string key)
        {
            var row = parent.Find(name);
            var changed = false;
            if (row == null)
            {
                row = CreateDisplayRow(parent, name, label, key);
                changed = true;
            }

            var dropdownObject = row.Find("Dropdown")?.gameObject;
            if (dropdownObject == null)
            {
                dropdownObject = UnityEngine.UI.DefaultControls.CreateDropdown(
                    new UnityEngine.UI.DefaultControls.Resources());
                dropdownObject.name = "Dropdown";
                dropdownObject.transform.SetParent(row, false);
                changed = true;
            }
            Anchor((RectTransform)dropdownObject.transform, new Vector2(0.43f, 0f), Vector2.one,
                new Vector2(0f, 5f), new Vector2(-8f, -5f));
            var background = dropdownObject.GetComponent<UnityEngine.UI.Image>();
            background.color = Control;
            var dropdown = dropdownObject.GetComponent<UnityEngine.UI.Dropdown>();
            var caption = dropdown.captionText;
            caption.color = TextColor;
            caption.fontSize = 22;
            changed |= ApplyDropdownTheme(dropdown);
            return changed;
        }

        private static bool ApplyDropdownTheme(UnityEngine.UI.Dropdown dropdown)
        {
            if (dropdown == null)
                throw new InvalidOperationException("Settings dropdown is missing its Dropdown component.");

            var changed = false;
            changed |= SetImageColor(dropdown.GetComponent<UnityEngine.UI.Image>(), Control);
            changed |= SetTextStyle(dropdown.captionText, TextColor, 22);
            changed |= SetTextStyle(dropdown.itemText, TextColor, 21);

            var colors = dropdown.colors;
            var themedColors = colors;
            themedColors.normalColor = Color.white;
            themedColors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            themedColors.pressedColor = new Color(0.88f, 0.92f, 0.92f, 1f);
            themedColors.selectedColor = new Color(1.04f, 1.04f, 1.04f, 1f);
            themedColors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.65f);
            if (colors.normalColor != themedColors.normalColor ||
                colors.highlightedColor != themedColors.highlightedColor ||
                colors.pressedColor != themedColors.pressedColor ||
                colors.selectedColor != themedColors.selectedColor ||
                colors.disabledColor != themedColors.disabledColor)
            {
                dropdown.colors = themedColors;
                changed = true;
            }

            var template = dropdown.template;
            if (template == null)
                throw new InvalidOperationException($"Settings dropdown '{dropdown.name}' has no popup template.");

            changed |= SetImageColor(template.GetComponent<UnityEngine.UI.Image>(), Ink);
            var viewport = template.Find("Viewport");
            if (viewport != null)
                changed |= SetImageColor(viewport.GetComponent<UnityEngine.UI.Image>(), Ink);

            var item = template.Find("Viewport/Content/Item");
            var toggle = item != null ? item.GetComponent<UnityEngine.UI.Toggle>() : null;
            if (toggle != null)
            {
                var itemRect = (RectTransform)item;
                if (itemRect.sizeDelta != new Vector2(0f, 40f))
                {
                    itemRect.sizeDelta = new Vector2(0f, 40f);
                    changed = true;
                }

                var labelRect = dropdown.itemText != null
                    ? dropdown.itemText.rectTransform
                    : null;
                if (labelRect != null)
                {
                    if (labelRect.offsetMin != new Vector2(30f, 4f))
                    {
                        labelRect.offsetMin = new Vector2(30f, 4f);
                        changed = true;
                    }
                    if (labelRect.offsetMax != new Vector2(-10f, -4f))
                    {
                        labelRect.offsetMax = new Vector2(-10f, -4f);
                        changed = true;
                    }
                    if (dropdown.itemText.alignment != TextAnchor.MiddleLeft)
                    {
                        dropdown.itemText.alignment = TextAnchor.MiddleLeft;
                        changed = true;
                    }
                    if (dropdown.itemText.verticalOverflow != VerticalWrapMode.Overflow)
                    {
                        dropdown.itemText.verticalOverflow = VerticalWrapMode.Overflow;
                        changed = true;
                    }
                }

                var content = item.parent as RectTransform;
                if (content != null && content.sizeDelta.y < 40f)
                {
                    content.sizeDelta = new Vector2(content.sizeDelta.x, 40f);
                    changed = true;
                }

                var itemColors = toggle.colors;
                var themedItemColors = itemColors;
                themedItemColors.normalColor = Band;
                themedItemColors.highlightedColor = Accent;
                themedItemColors.pressedColor = new Color(0.11f, 0.32f, 0.31f, 1f);
                themedItemColors.selectedColor = Accent;
                themedItemColors.disabledColor = new Color(0.12f, 0.13f, 0.13f, 0.55f);
                if (itemColors.normalColor != themedItemColors.normalColor ||
                    itemColors.highlightedColor != themedItemColors.highlightedColor ||
                    itemColors.pressedColor != themedItemColors.pressedColor ||
                    itemColors.selectedColor != themedItemColors.selectedColor ||
                    itemColors.disabledColor != themedItemColors.disabledColor)
                {
                    toggle.colors = themedItemColors;
                    changed = true;
                }

                changed |= SetImageColor(toggle.targetGraphic as UnityEngine.UI.Image, Color.white);
                changed |= SetImageColor(toggle.graphic as UnityEngine.UI.Image, TextColor);
            }

            var arrow = dropdown.transform.Find("Arrow")?.GetComponent<UnityEngine.UI.Image>();
            changed |= SetImageColor(arrow, TextColor);

            var scrollbar = template.Find("Scrollbar");
            if (scrollbar != null)
            {
                changed |= SetImageColor(scrollbar.GetComponent<UnityEngine.UI.Image>(), Band);
                changed |= SetImageColor(
                    scrollbar.Find("Sliding Area/Handle")?.GetComponent<UnityEngine.UI.Image>(), Accent);
            }

            return changed;
        }

        private static bool SetImageColor(UnityEngine.UI.Image image, Color color)
        {
            if (image == null || image.color == color) return false;
            image.color = color;
            return true;
        }

        private static bool SetTextStyle(UnityEngine.UI.Text text, Color color, int fontSize)
        {
            if (text == null) return false;
            var changed = false;
            if (text.color != color)
            {
                text.color = color;
                changed = true;
            }
            if (text.fontSize != fontSize)
            {
                text.fontSize = fontSize;
                changed = true;
            }
            return changed;
        }

        private static bool EnsureDisplayToggle(Transform parent, string name, string label, string key)
        {
            var row = parent.Find(name);
            var changed = false;
            if (row == null)
            {
                row = CreateDisplayRow(parent, name, label, key);
                changed = true;
            }

            var toggleObject = row.Find("Toggle")?.gameObject;
            if (toggleObject == null)
            {
                toggleObject = UnityEngine.UI.DefaultControls.CreateToggle(
                    new UnityEngine.UI.DefaultControls.Resources());
                toggleObject.name = "Toggle";
                toggleObject.transform.SetParent(row, false);
                changed = true;
            }
            Anchor((RectTransform)toggleObject.transform, new Vector2(0.43f, 0f), Vector2.one,
                new Vector2(0f, 5f), new Vector2(-8f, -5f));
            var toggle = toggleObject.GetComponent<UnityEngine.UI.Toggle>();
            var hitArea = toggleObject.GetComponent<UnityEngine.UI.Image>();
            if (hitArea == null)
            {
                hitArea = toggleObject.AddComponent<UnityEngine.UI.Image>();
                changed = true;
            }
            if (hitArea.color != Color.clear)
            {
                hitArea.color = Color.clear;
                changed = true;
            }
            if (!hitArea.raycastTarget)
            {
                hitArea.raycastTarget = true;
                changed = true;
            }

            var background = toggle.targetGraphic as UnityEngine.UI.Image;
            if (background != null)
            {
                if (background.color != Control)
                {
                    background.color = Control;
                    changed = true;
                }
                var box = background.rectTransform;
                var centerLeft = new Vector2(0f, 0.5f);
                if (box.anchorMin != centerLeft || box.anchorMax != centerLeft ||
                    box.anchoredPosition != new Vector2(20f, 0f) || box.sizeDelta != new Vector2(32f, 32f))
                {
                    box.anchorMin = centerLeft;
                    box.anchorMax = centerLeft;
                    box.anchoredPosition = new Vector2(20f, 0f);
                    box.sizeDelta = new Vector2(32f, 32f);
                    changed = true;
                }
            }

            var checkmark = toggle.graphic as UnityEngine.UI.Image;
            if (checkmark != null && checkmark.color != Accent)
            {
                checkmark.color = Accent;
                changed = true;
            }

            var toggleLabel = toggleObject.transform.Find("Label")?.GetComponent<UnityEngine.UI.Text>();
            if (toggleLabel != null && toggleLabel.text != string.Empty)
            {
                toggleLabel.text = string.Empty;
                changed = true;
            }
            return changed;
        }

        private static RectTransform CreateDisplayRow(Transform parent, string name, string label, string key)
        {
            var row = CreateRect(name, parent);
            row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 72f;
            var text = CreateText("Label", label, 22, row);
            text.alignment = TextAnchor.MiddleLeft;
            text.gameObject.GetComponent<UnityEngine.UI.LayoutElement>().enabled = false;
            Anchor(text.rectTransform, Vector2.zero, new Vector2(0.43f, 1f),
                new Vector2(6f, 0f), new Vector2(-8f, 0f));
            text.gameObject.AddComponent<LocalizedTextPresenter>().Configure(text, key, label);
            return row;
        }

        private static Transform CreateDisplayConfirmModal(Transform parent)
        {
            var modal = CreateRect("DisplayConfirmModal", parent);
            Stretch(modal);
            var backdrop = modal.gameObject.AddComponent<UnityEngine.UI.Image>();
            backdrop.color = new Color(0.02f, 0.03f, 0.035f, 0.9f);
            backdrop.raycastTarget = true;
            var dialog = CreateRect("Dialog", modal);
            Anchor(dialog, Vector2.one * 0.5f, Vector2.one * 0.5f,
                new Vector2(-300f, -140f), new Vector2(300f, 140f));
            AddBand(dialog, Band);
            var stack = dialog.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            stack.padding = new RectOffset(24, 24, 24, 24);
            stack.spacing = 16f;
            stack.childControlWidth = true;
            stack.childControlHeight = true;
            stack.childForceExpandWidth = true;
            stack.childForceExpandHeight = false;
            var countdown = CreateText("Countdown", "Keep these display settings? 15s", 26, dialog);
            countdown.alignment = TextAnchor.MiddleCenter;
            countdown.gameObject.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 100f;
            var actions = CreateRect("Actions", dialog);
            actions.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 70f;
            var row = actions.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            row.spacing = 12f;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = false;
            CreateButton("Keep", "Keep", FrameworkTextKeys.DisplayKeep, actions);
            CreateButton("Revert", "Revert", FrameworkTextKeys.DisplayRevert, actions);
            modal.gameObject.SetActive(false);
            return modal;
        }

        private static bool EnsureButtonListener(UnityEngine.UI.Button button,
            UnityEngine.Events.UnityAction callback)
        {
            var callbackTarget = callback.Target as UnityEngine.Object;
            if (callbackTarget == null)
                throw new ArgumentException("Persistent button callbacks must target a Unity object.", nameof(callback));

            for (var i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                if (button.onClick.GetPersistentTarget(i) == callbackTarget &&
                    button.onClick.GetPersistentMethodName(i) == callback.Method.Name)
                    return false;
            }
            UnityEventTools.AddPersistentListener(button.onClick, callback);
            return true;
        }

        private static bool EnsureValue(AccessibilitySettingsPanel settings, Transform content, string rowName, string field)
        {
            var row = Require(content, rowName);
            var label = row.Find("ValueText")?.GetComponent<UnityEngine.UI.Text>();
            var changed = false;
            if (label == null)
            {
                label = CreateText("ValueText", string.Empty, 22, row);
                label.alignment = TextAnchor.MiddleRight;
                var element = label.GetComponent<UnityEngine.UI.LayoutElement>();
                element.preferredWidth = 76f;
                element.preferredHeight = 64f;
                label.raycastTarget = false;
                changed = true;
            }

            var serialized = new SerializedObject(settings);
            var property = serialized.FindProperty(field);
            if (property == null)
            {
                throw new InvalidOperationException($"Settings field '{field}' is missing.");
            }
            if (property.objectReferenceValue != label)
            {
                property.objectReferenceValue = label;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }
            return changed;
        }

        private static bool EnsureReset(Transform content, string name, string label, string key,
            UnityEngine.Events.UnityAction callback)
        {
            if (content.Find(name) != null)
            {
                return false;
            }
            var button = CreateButton(name, label, key, content);
            UnityEventTools.AddPersistentListener(button.onClick, callback);
            return true;
        }

        private static void ConfigureScroll(UnityEngine.UI.ScrollRect scroll)
        {
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            scroll.scrollSensitivity = 12f;
        }

        private static UnityEngine.UI.Button CreateButton(string name, string label, string key, Transform parent)
        {
            var rect = CreateRect(name, parent);
            rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 68f;
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = Control;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.transition = UnityEngine.UI.Selectable.Transition.None;
            var text = CreateText("Label", label, 25, rect);
            Stretch(text.rectTransform, 18f);
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            if (!string.IsNullOrEmpty(key))
            {
                text.gameObject.AddComponent<LocalizedTextPresenter>().Configure(text, key, label);
            }
            return button;
        }

        private static UnityEngine.UI.Text CreateText(string name, string value, int size, Transform parent)
        {
            var rect = CreateRect(name, parent);
            var text = rect.gameObject.AddComponent<UnityEngine.UI.Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.color = TextColor;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 16;
            text.resizeTextMaxSize = size;
            rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 64f;
            return text;
        }

        private static void AddBand(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return (RectTransform)obj.transform;
        }

        private static Transform Require(Transform parent, string path) =>
            parent.Find(path) ?? throw new InvalidOperationException($"Missing UI object: {parent.name}/{path}");

        private static void Stretch(RectTransform rect, float inset = 0f) =>
            Anchor(rect, Vector2.zero, Vector2.one, Vector2.one * inset, -Vector2.one * inset);

        private static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetsMin, Vector2 offsetsMax)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetsMin;
            rect.offsetMax = offsetsMax;
        }
    }
}
