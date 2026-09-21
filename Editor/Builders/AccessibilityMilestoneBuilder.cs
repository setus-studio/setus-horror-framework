using System;
using System.IO;
using System.Linq;
using Setus.HorrorFramework.Accessibility.Display;
using Setus.HorrorFramework.Localization;
using Setus.HorrorFramework.Narrative.Notes;
using Setus.HorrorFramework.Narrative.Objectives;
using Setus.HorrorFramework.Narrative.Phone;
using Setus.HorrorFramework.Atmosphere.Scares;
using Setus.HorrorFramework.AI.Tuning;
using Setus.HorrorFramework.UI.Menus;
using Setus.HorrorFramework.UI.Settings;
using Setus.HorrorFramework.UI.Settings.Display;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Setus.HorrorFramework.Editor.Builders
{
    public static class AccessibilityMilestoneBuilder
    {
        private const string LocalizationRoot = "Assets/Game/Localization";
        private const string LocalizationSettingsPath = LocalizationRoot + "/GameLocalizationSettings.asset";
        private const string EnglishLocalePath = LocalizationRoot + "/Locales/English (en).asset";
        private const string TablesPath = LocalizationRoot + "/String Tables";
        private const string GameplayScenePath = "Assets/Game/Scenes/Gameplay.unity";
        private const string BrightnessObjectName = "M11_AccessibilityBrightness";

        [MenuItem("Setus/Horror Framework/Accessibility/Apply M11 Accessibility And Localization Foundation")]
        public static void ApplyFromMenu()
        {
            var table = EnsureLocalizationFoundation();
            UpgradeUiShell(table);
            AssignContentLocalizationKeys(table);
            EnsurePlayerCameraPostProcessing();
            EnsureBrightnessVolume();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("Applied M11 accessibility, localization, and settings foundation.");
        }

        private static StringTable EnsureLocalizationFoundation()
        {
            EnsureFolder(LocalizationRoot);
            EnsureFolder(LocalizationRoot + "/Locales");
            EnsureFolder(TablesPath);

            var settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(LocalizationSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                AssetDatabase.CreateAsset(settings, LocalizationSettingsPath);
            }

            LocalizationEditorSettings.ActiveLocalizationSettings = settings;

            var english = LocalizationEditorSettings.GetLocale("en");
            if (english == null)
            {
                english = Locale.CreateLocale("en");
                AssetDatabase.CreateAsset(english, EnglishLocalePath);
                LocalizationEditorSettings.AddLocale(english);
            }

            LocalizationSettings.ProjectLocale = english;

            var collection = LocalizationEditorSettings.GetStringTableCollection(LocalizedTextReference.DefaultTable) ??
                LocalizationEditorSettings.CreateStringTableCollection(
                    LocalizedTextReference.DefaultTable,
                    TablesPath,
                    new[] { english });
            var table = collection.GetTable(english.Identifier) as StringTable;
            if (table == null)
            {
                throw new InvalidOperationException("M11 could not create the English Game Text string table.");
            }

            AddOrUpdate(table, "ui.main.title", "SETUS");
            AddOrUpdate(table, "ui.main.new-game", "New Game");
            AddOrUpdate(table, "ui.main.continue", "Continue");
            AddOrUpdate(table, "ui.common.save-load", "Save / Load");
            AddOrUpdate(table, "ui.common.settings", "Settings");
            AddOrUpdate(table, "ui.common.back", "Back");
            AddOrUpdate(table, "ui.pause.title", "Paused");
            AddOrUpdate(table, "ui.pause.resume", "Resume");
            AddOrUpdate(table, "ui.pause.main-menu", "Main Menu");
            AddOrUpdate(table, "ui.settings.title", "Accessibility & Settings");
            AddOrUpdate(table, FrameworkTextKeys.SettingsAudioTab, "Audio");
            AddOrUpdate(table, FrameworkTextKeys.SettingsControlsTab, "Controls");
            AddOrUpdate(table, FrameworkTextKeys.SettingsAccessibilityTab, "Accessibility");
            AddOrUpdate(table, FrameworkTextKeys.SettingsLanguageTab, "Language");
            AddOrUpdate(table, FrameworkTextKeys.SettingsDisplayTab, "Display");
            AddOrUpdate(table, FrameworkTextKeys.DisplayMode, "Display Mode");
            AddOrUpdate(table, FrameworkTextKeys.DisplayResolution, "Resolution");
            AddOrUpdate(table, FrameworkTextKeys.DisplayRefresh, "Refresh Rate");
            AddOrUpdate(table, FrameworkTextKeys.DisplayVSync, "VSync");
            AddOrUpdate(table, FrameworkTextKeys.DisplayFrameRate, "FPS Cap");
            AddOrUpdate(table, FrameworkTextKeys.DisplayQuality, "Graphics Quality");
            AddOrUpdate(table, FrameworkTextKeys.DisplayQualityLow, "Low");
            AddOrUpdate(table, FrameworkTextKeys.DisplayQualityMedium, "Medium");
            AddOrUpdate(table, FrameworkTextKeys.DisplayQualityHigh, "High");
            AddOrUpdate(table, FrameworkTextKeys.DisplayAntiAliasing, "Anti-aliasing");
            AddOrUpdate(table, FrameworkTextKeys.DisplayAaOff, "Off");
            AddOrUpdate(table, FrameworkTextKeys.DisplayApply, "Apply Display");
            AddOrUpdate(table, FrameworkTextKeys.DisplayKeep, "Keep");
            AddOrUpdate(table, FrameworkTextKeys.DisplayRevert, "Revert");
            AddOrUpdate(table, FrameworkTextKeys.DisplayWindowed, "Windowed");
            AddOrUpdate(table, FrameworkTextKeys.DisplayBorderless, "Borderless");
            AddOrUpdate(table, FrameworkTextKeys.DisplayFullscreen, "Fullscreen");
            AddOrUpdate(table, FrameworkTextKeys.DisplayAutomatic, "Automatic");
            AddOrUpdate(table, FrameworkTextKeys.DisplayUnlimited, "Unlimited");
            AddOrUpdate(table, FrameworkTextKeys.DisplayConfirmPrompt, "Keep these display settings?");
            AddOrUpdate(table, FrameworkTextKeys.DisplayPreviewing, "Display change is awaiting confirmation.");
            AddOrUpdate(table, FrameworkTextKeys.DisplaySaved, "Display settings saved.");
            AddOrUpdate(table, FrameworkTextKeys.DisplaySaveFailed, "Display changed, but preferences could not be saved.");
            AddOrUpdate(table, FrameworkTextKeys.DisplayApplyFailed, "Display change failed; previous settings restored.");
            AddOrUpdate(table, FrameworkTextKeys.DisplayTimedOut, "Display change timed out; previous settings restored.");
            AddOrUpdate(table, FrameworkTextKeys.DisplayReverted, "Previous display settings restored.");
            AddOrUpdate(table, FrameworkTextKeys.DisplayRevertFailed, "Display could not be restored. Check your screen settings.");
            AddOrUpdate(table, FrameworkTextKeys.SettingsResetAudio, "Reset Audio Defaults");
            AddOrUpdate(table, FrameworkTextKeys.SettingsResetMovement, "Reset Movement Settings");
            AddOrUpdate(table, FrameworkTextKeys.SettingsResetAccessibility, "Reset Accessibility Defaults");
            AddOrUpdate(table, FrameworkTextKeys.RebindInteract, "Interact");
            AddOrUpdate(table, FrameworkTextKeys.RebindPause, "Pause");
            AddOrUpdate(table, FrameworkTextKeys.RebindModalUnavailable, "Rebind dialog is not configured.");
            AddOrUpdate(table, FrameworkTextKeys.RebindConflict, "Already assigned to");
            AddOrUpdate(table, FrameworkTextKeys.RebindResetTitle, "Reset all bindings?");
            AddOrUpdate(table, FrameworkTextKeys.RebindResetPrompt, "Default controls will be restored.");
            AddOrUpdate(table, FrameworkTextKeys.RebindDialogCancel, "Cancel");
            AddOrUpdate(table, FrameworkTextKeys.RebindDialogRetry, "Retry");
            AddOrUpdate(table, FrameworkTextKeys.RebindDialogConfirmReset, "Reset");
            AddOrUpdate(table, FrameworkTextKeys.InteractionPrompt, "Interact");
            AddOrUpdate(table, FrameworkTextKeys.InteractionUnavailablePrompt, "Cannot interact");
            AddOrUpdate(table, FrameworkTextKeys.DoorOpenPrompt, "Open");
            AddOrUpdate(table, FrameworkTextKeys.DoorClosePrompt, "Close");
            AddOrUpdate(table, FrameworkTextKeys.DoorLockedPrompt, "Locked");
            AddOrUpdate(table, FrameworkTextKeys.DrawerOpenPrompt, "Open drawer");
            AddOrUpdate(table, FrameworkTextKeys.DrawerClosePrompt, "Close drawer");
            AddOrUpdate(table, FrameworkTextKeys.DrawerLockedPrompt, "Drawer is locked");
            AddOrUpdate(table, FrameworkTextKeys.PickupPrompt, "Pick up");
            AddOrUpdate(table, FrameworkTextKeys.PickupAlreadyCollectedPrompt, "Already picked up");
            AddOrUpdate(table, FrameworkTextKeys.InspectablePrompt, "Inspect");
            AddOrUpdate(table, FrameworkTextKeys.InspectableAlreadyInspectedPrompt, "Already inspected");
            AddOrUpdate(table, FrameworkTextKeys.UnknownSpeaker, "Unknown");
            AddOrUpdate(table, FrameworkTextKeys.StalkerChaseSubtitle, "Something has noticed you.");
            AddOrUpdate(table, FrameworkTextKeys.PhoneStatusNew, "New message");
            AddOrUpdate(table, FrameworkTextKeys.PhoneStatusRead, "Read");
            AddOrUpdate(table, FrameworkTextKeys.PhoneStatusReplied, "Replied");
            AddOrUpdate(table, FrameworkTextKeys.PlayerSpeaker, "You");
            AddOrUpdate(table, FrameworkTextKeys.PhoneCheckPrompt, "Check phone");
            AddOrUpdate(table, FrameworkTextKeys.PhoneReadPrompt, "Read message");
            AddOrUpdate(table, FrameworkTextKeys.PhoneReplyPrompt, "Reply");
            AddOrUpdate(table, FrameworkTextKeys.PhoneHandledPrompt, "Message read");
            AddOrUpdate(table, FrameworkTextKeys.PhoneUnavailablePrompt, "Phone unavailable");
            AddOrUpdate(table, FrameworkTextKeys.NoteReadPrompt, "Read note");
            AddOrUpdate(table, FrameworkTextKeys.NoteReadAgainPrompt, "Read again");
            AddOrUpdate(table, FrameworkTextKeys.NoteUnavailablePrompt, "Document unavailable");
            AddOrUpdate(table, FrameworkTextKeys.PhoneNotConfiguredResult, "Phone message is not configured.");
            AddOrUpdate(table, FrameworkTextKeys.PhoneDeliveredResult, "Message delivered");
            AddOrUpdate(table, FrameworkTextKeys.PhoneDeliveryFailedResult, "Message delivery failed");
            AddOrUpdate(table, FrameworkTextKeys.PhoneReadResult, "Message read");
            AddOrUpdate(table, FrameworkTextKeys.PhoneReplySentResult, "Reply sent");
            AddOrUpdate(table, FrameworkTextKeys.PhoneAlreadyHandledResult, "Message already handled");
            AddOrUpdate(table, FrameworkTextKeys.NoteNotConfiguredResult, "Note is not configured.");
            AddOrUpdate(table, FrameworkTextKeys.NoteReadResult, "Note read");
            AddOrUpdate(table, FrameworkTextKeys.RebindMissingActions, "Input Actions asset is not assigned.");
            AddOrUpdate(table, FrameworkTextKeys.RebindActionUnavailable, "Input action is unavailable.");
            AddOrUpdate(table, FrameworkTextKeys.RebindWaiting, "Press a control. Esc cancels.");
            AddOrUpdate(table, FrameworkTextKeys.RebindReset, "Bindings reset.");
            AddOrUpdate(table, FrameworkTextKeys.RebindUpdated, "Binding updated.");
            AddOrUpdate(table, FrameworkTextKeys.RebindCancelled, "Rebind cancelled.");
            AddOrUpdate(table, FrameworkTextKeys.SaveErrorNoSave, "No saved game is available.");
            AddOrUpdate(table, FrameworkTextKeys.SaveErrorCorrupt, "This save data is corrupted and cannot be loaded.");
            AddOrUpdate(table, FrameworkTextKeys.SaveErrorIncompatible, "This save was created by an incompatible game version.");
            AddOrUpdate(table, FrameworkTextKeys.SaveErrorRestoreFailed, "The saved game could not be restored.");
            AddOrUpdate(table, FrameworkTextKeys.SaveErrorSaveFailed, "The game could not be saved.");
            AddOrUpdate(table, FrameworkTextKeys.SaveErrorUnavailable, "Save and load are currently unavailable.");
            AddOrUpdate(table, FrameworkTextKeys.SaveStatusReady, "A saved game is ready.");
            AddOrUpdate(table, FrameworkTextKeys.SaveStatusSaved, "Game saved.");
            AddOrUpdate(table, FrameworkTextKeys.SaveStatusLoading, "Loading saved game.");
            AddOrUpdate(table, FrameworkTextKeys.SaveUnavailableOutsideGameplay, "Quick Save is unavailable outside gameplay.");
            AddOrUpdate(table, FrameworkTextKeys.LoadUnavailableFromPause, "Quick Load is unavailable from the pause menu.");
            EditorUtility.SetDirty(table);
            EditorUtility.SetDirty(table.SharedData);
            return table;
        }

        private static void UpgradeUiShell(StringTable table)
        {
            var prefabPath = SceneFlowUiShellTemplateBuilder.UiShellPrefabPath;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                SceneFlowUiShellTemplateBuilder.BuildDefaultUiShell(false);
            }

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var presenter = root.GetComponent<UiShellCanvasPresenter>();
                if (presenter == null)
                {
                    throw new InvalidOperationException("Canonical UI shell requires UiShellCanvasPresenter.");
                }

                EnsureComponent<RuntimeLocaleController>(root);
                var settingsPanel = root.transform.Find("SettingsPanel")?.gameObject;
                if (settingsPanel == null)
                {
                    settingsPanel = BuildSettingsPanel(root.transform, presenter);
                }

                ConfigureReference(presenter, "settingsPanel", settingsPanel);
                EnsureSettingsButton(root.transform.Find("MainMenuPanel/MenuStack"), presenter);
                EnsureSettingsButton(root.transform.Find("PauseMenuPanel/PauseStack"), presenter);
                LocalizeKnownUi(root, table);
                settingsPanel.SetActive(false);

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static GameObject BuildSettingsPanel(Transform parent, UiShellCanvasPresenter shell)
        {
            var panel = CreateUiObject("SettingsPanel", parent);
            Stretch(panel.GetComponent<RectTransform>());
            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.02f, 0.025f, 0.03f, 0.96f);

            var scroll = CreateUiObject("SettingsScroll", panel.transform);
            var scrollRectTransform = scroll.GetComponent<RectTransform>();
            scrollRectTransform.anchorMin = new Vector2(0.14f, 0.06f);
            scrollRectTransform.anchorMax = new Vector2(0.86f, 0.94f);
            scrollRectTransform.offsetMin = Vector2.zero;
            scrollRectTransform.offsetMax = Vector2.zero;
            var scrollRect = scroll.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;

            var viewport = CreateUiObject("Viewport", scroll.transform);
            Stretch(viewport.GetComponent<RectTransform>());
            viewport.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.18f);
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            scrollRect.viewport = viewport.GetComponent<RectTransform>();

            var content = CreateUiObject("Content", viewport.transform);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = new Vector2(24f, 0f);
            contentRect.offsetMax = new Vector2(-24f, 0f);
            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.padding = new RectOffset(12, 12, 16, 16);
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = contentRect;

            CreateText(content.transform, "Title", "Accessibility & Settings", 36, 64f);
            var settings = panel.AddComponent<AccessibilitySettingsPanel>();
            var master = CreateSliderRow(content.transform, "Master Volume", 0f, 1f);
            var ambience = CreateSliderRow(content.transform, "Ambience Volume", 0f, 1f);
            var sfx = CreateSliderRow(content.transform, "SFX Volume", 0f, 1f);
            var ui = CreateSliderRow(content.transform, "UI Volume", 0f, 1f);
            var voice = CreateSliderRow(content.transform, "Voice Volume", 0f, 1f);
            var subtitles = CreateToggleRow(content.transform, "Subtitles");
            var brightness = CreateSliderRow(content.transform, "Brightness", 0f, 1f);
            var shake = CreateSliderRow(content.transform, "Camera Shake", 0f, 1f);
            var headBob = CreateToggleRow(content.transform, "Head Bob");
            var headBobIntensity = CreateSliderRow(content.transform, "Head Bob Intensity", 0f, 1f);
            var sprintToggle = CreateToggleRow(content.transform, "Sprint Toggle (off = hold)");
            var sensitivity = CreateSliderRow(content.transform, "Mouse Sensitivity", 0.1f, 3f);
            var locale = CreateInputRow(content.transform, "Locale Code", "en");

            AddPersistent(master.onValueChanged, settings.SetMasterVolume);
            AddPersistent(ambience.onValueChanged, settings.SetAmbienceVolume);
            AddPersistent(sfx.onValueChanged, settings.SetSfxVolume);
            AddPersistent(ui.onValueChanged, settings.SetUiVolume);
            AddPersistent(voice.onValueChanged, settings.SetVoiceVolume);
            AddPersistent(subtitles.onValueChanged, settings.SetSubtitlesEnabled);
            AddPersistent(brightness.onValueChanged, settings.SetBrightness);
            AddPersistent(shake.onValueChanged, settings.SetCameraShakeIntensity);
            AddPersistent(headBob.onValueChanged, settings.SetHeadBobEnabled);
            AddPersistent(headBobIntensity.onValueChanged, settings.SetHeadBobIntensity);
            AddPersistent(sprintToggle.onValueChanged, settings.SetSprintToggle);
            AddPersistent(sensitivity.onValueChanged, settings.SetMouseSensitivity);
            UnityEventTools.AddPersistentListener(locale.onEndEdit, settings.SetLocaleCode);

            ConfigureReference(settings, "masterVolume", master);
            ConfigureReference(settings, "ambienceVolume", ambience);
            ConfigureReference(settings, "sfxVolume", sfx);
            ConfigureReference(settings, "uiVolume", ui);
            ConfigureReference(settings, "voiceVolume", voice);
            ConfigureReference(settings, "subtitlesEnabled", subtitles);
            ConfigureReference(settings, "brightness", brightness);
            ConfigureReference(settings, "cameraShakeIntensity", shake);
            ConfigureReference(settings, "headBobEnabled", headBob);
            ConfigureReference(settings, "headBobIntensity", headBobIntensity);
            ConfigureReference(settings, "sprintToggle", sprintToggle);
            ConfigureReference(settings, "mouseSensitivity", sensitivity);
            ConfigureReference(settings, "localeCode", locale);

            var rebind = panel.AddComponent<InputRebindPresenter>();
            ConfigureReference(rebind, "actions", AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                PlayerFoundationTemplateBuilder.InputActionsPath));
            CreateText(content.transform, "BindingsTitle", "Input Remapping", 28, 48f);
            CreateRebindButton(content.transform, "MoveForward", "Move Forward", rebind.RebindMoveForward);
            CreateRebindButton(content.transform, "MoveBackward", "Move Backward", rebind.RebindMoveBackward);
            CreateRebindButton(content.transform, "MoveLeft", "Move Left", rebind.RebindMoveLeft);
            CreateRebindButton(content.transform, "MoveRight", "Move Right", rebind.RebindMoveRight);
            CreateRebindButton(content.transform, "Look", "Look", rebind.RebindLook);
            CreateRebindButton(content.transform, "Sprint", "Sprint", rebind.RebindSprint);
            CreateRebindButton(content.transform, "Crouch", "Crouch", rebind.RebindCrouch);
            CreateRebindButton(content.transform, "ResetBindings", "Reset Bindings", rebind.ResetBindings);
            var rebindStatus = CreateText(content.transform, "RebindStatus", string.Empty, 18, 48f);
            ConfigureReference(rebind, "statusText", rebindStatus);

            CreateRebindButton(content.transform, "Back", "Back", shell.ShowReturnFromSettings);
            return panel;
        }

        private static void EnsureSettingsButton(Transform stack, UiShellCanvasPresenter presenter)
        {
            if (stack == null || stack.Find("SettingsButton") != null)
            {
                return;
            }

            CreateRebindButton(stack, "SettingsButton", "Settings", presenter.ShowSettings);
        }

        private static void LocalizeKnownUi(GameObject root, StringTable table)
        {
            Localize(root.transform.Find("MainMenuPanel/MenuStack/Title"), "ui.main.title", table);
            Localize(root.transform.Find("MainMenuPanel/MenuStack/NewGameButton/Label"), "ui.main.new-game", table);
            Localize(root.transform.Find("MainMenuPanel/MenuStack/ContinueButton/Label"), "ui.main.continue", table);
            Localize(root.transform.Find("MainMenuPanel/MenuStack/SaveLoadButton/Label"), "ui.common.save-load", table);
            Localize(root.transform.Find("MainMenuPanel/MenuStack/SettingsButton/Label"), "ui.common.settings", table);
            Localize(root.transform.Find("PauseMenuPanel/PauseStack/Title"), "ui.pause.title", table);
            Localize(root.transform.Find("PauseMenuPanel/PauseStack/ResumeButton/Label"), "ui.pause.resume", table);
            Localize(root.transform.Find("PauseMenuPanel/PauseStack/SaveLoadButton/Label"), "ui.common.save-load", table);
            Localize(root.transform.Find("PauseMenuPanel/PauseStack/SettingsButton/Label"), "ui.common.settings", table);
            Localize(root.transform.Find("PauseMenuPanel/PauseStack/MainMenuButton/Label"), "ui.pause.main-menu", table);
            Localize(root.transform.Find("SettingsPanel/SettingsScroll/Viewport/Content/Title"), "ui.settings.title", table);
            Localize(root.transform.Find("SettingsPanel/SettingsScroll/Viewport/Content/MasterVolumeRow/Label"), "ui.settings.master-volume", table);
            Localize(root.transform.Find("SettingsPanel/SettingsScroll/Viewport/Content/AmbienceVolumeRow/Label"), "ui.settings.ambience-volume", table);
            Localize(root.transform.Find("SettingsPanel/SettingsScroll/Viewport/Content/SFXVolumeRow/Label"), "ui.settings.sfx-volume", table);
            Localize(root.transform.Find("SettingsPanel/SettingsScroll/Viewport/Content/UIVolumeRow/Label"), "ui.settings.ui-volume", table);
            Localize(root.transform.Find("SettingsPanel/SettingsScroll/Viewport/Content/VoiceVolumeRow/Label"), "ui.settings.voice-volume", table);
            Localize(root.transform.Find("SettingsPanel/SettingsScroll/Viewport/Content/SubtitlesRow/Label"), "ui.settings.subtitles", table);
            Localize(root.transform.Find("SettingsPanel/SettingsScroll/Viewport/Content/BrightnessRow/Label"), "ui.settings.brightness", table);
            Localize(root.transform.Find("SettingsPanel/SettingsScroll/Viewport/Content/CameraShakeRow/Label"), "ui.settings.camera-shake", table);
            Localize(root.transform.Find("SettingsPanel/SettingsScroll/Viewport/Content/HeadBobRow/Label"), "ui.settings.head-bob", table);
            Localize(root.transform.Find("SettingsPanel/SettingsScroll/Viewport/Content/HeadBobIntensityRow/Label"), "ui.settings.head-bob-intensity", table);
            Localize(root.transform.Find("SettingsPanel/SettingsScroll/Viewport/Content/SprintToggle(off=hold)Row/Label"), "ui.settings.sprint-mode", table);
            Localize(root.transform.Find("SettingsPanel/SettingsScroll/Viewport/Content/MouseSensitivityRow/Label"), "ui.settings.mouse-sensitivity", table);
            Localize(root.transform.Find("SettingsPanel/SettingsScroll/Viewport/Content/LocaleCodeRow/Label"), "ui.settings.locale-code", table);
            Localize(root.transform.Find("SettingsPanel/SettingsScroll/Viewport/Content/BindingsTitle"), "ui.settings.input-remapping", table);
            Localize(root.transform.Find("SettingsPanel/SettingsScroll/Viewport/Content/MoveForward/Label"), "ui.settings.rebind.move-forward", table);
            Localize(root.transform.Find("SettingsPanel/SettingsScroll/Viewport/Content/MoveBackward/Label"), "ui.settings.rebind.move-backward", table);
            Localize(root.transform.Find("SettingsPanel/SettingsScroll/Viewport/Content/MoveLeft/Label"), "ui.settings.rebind.move-left", table);
            Localize(root.transform.Find("SettingsPanel/SettingsScroll/Viewport/Content/MoveRight/Label"), "ui.settings.rebind.move-right", table);
            Localize(root.transform.Find("SettingsPanel/SettingsScroll/Viewport/Content/Look/Label"), "ui.settings.rebind.look", table);
            Localize(root.transform.Find("SettingsPanel/SettingsScroll/Viewport/Content/Sprint/Label"), "ui.settings.rebind.sprint", table);
            Localize(root.transform.Find("SettingsPanel/SettingsScroll/Viewport/Content/Crouch/Label"), "ui.settings.rebind.crouch", table);
            Localize(root.transform.Find("SettingsPanel/SettingsScroll/Viewport/Content/ResetBindings/Label"), "ui.settings.rebind.reset", table);
            Localize(root.transform.Find("SettingsPanel/SettingsScroll/Viewport/Content/Back/Label"), "ui.common.back", table);
            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = Mathf.Min(14, text.fontSize);
                text.resizeTextMaxSize = text.fontSize;
            }
        }

        private static void Localize(Transform target, string key, StringTable table)
        {
            if (target == null)
            {
                return;
            }

            var label = target.GetComponent<Text>();
            if (label == null)
            {
                return;
            }

            AddOrUpdate(table, key, label.text);
            var presenter = EnsureComponent<LocalizedTextPresenter>(target.gameObject);
            presenter.Configure(label, key, label.text);
            EditorUtility.SetDirty(presenter);
        }

        private static void AssignContentLocalizationKeys(StringTable table)
        {
            foreach (var objective in FindAssets<ObjectiveDefinition>())
            {
                SetKey(objective, "titleLocalizationKey", $"objective.{objective.ObjectiveId}.title", objective.Title, table);
                SetKey(objective, "descriptionLocalizationKey", $"objective.{objective.ObjectiveId}.description", objective.Description, table);
            }

            foreach (var message in FindAssets<PhoneMessageDefinition>())
            {
                SetKey(message, "senderLocalizationKey", $"phone.{message.MessageId}.sender", message.Sender, table);
                SetKey(message, "messageLocalizationKey", $"phone.{message.MessageId}.message", message.MessageText, table);
                SetKey(message, "replyLocalizationKey", $"phone.{message.MessageId}.reply", message.ReplyText, table);
            }

            foreach (var note in FindAssets<NarrativeNoteDefinition>())
            {
                SetKey(note, "titleLocalizationKey", $"note.{note.NoteId}.title", note.Title, table);
                SetKey(note, "bodyLocalizationKey", $"note.{note.NoteId}.body", note.Body, table);
            }

            foreach (var scare in FindAssets<ScareDefinition>())
            {
                SetKey(scare, "fallbackSpeakerLocalizationKey", $"scare.{scare.ScareId}.speaker", scare.FallbackSpeaker, table);
                SetKey(scare, "fallbackCueLocalizationKey", $"scare.{scare.ScareId}.cue", scare.FallbackCueText, table);
            }

            foreach (var profile in FindAssets<StalkerAiTuningProfile>())
            {
                SetKey(
                    profile,
                    "chaseSubtitleLocalizationKey",
                    FrameworkTextKeys.StalkerChaseSubtitle,
                    profile.ChaseSubtitle,
                    table);
            }
        }

        private static void EnsureBrightnessVolume()
        {
            if (!File.Exists(GameplayScenePath))
            {
                return;
            }

            var scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            var brightness = scene.GetRootGameObjects().FirstOrDefault(root => root.name == BrightnessObjectName) ??
                new GameObject(BrightnessObjectName);
            var volume = EnsureComponent<Volume>(brightness);
            volume.isGlobal = true;
            volume.priority = 100f;
            EnsureComponent<BrightnessVolumeSettingsApplier>(brightness);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, GameplayScenePath);
        }

        private static void EnsurePlayerCameraPostProcessing()
        {
            var prefabPath = PlayerFoundationTemplateBuilder.PlayerPrefabPath;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                PlayerFoundationTemplateBuilder.BuildDefaultAssets(false);
            }

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var playerCamera = root.GetComponentInChildren<UnityEngine.Camera>(true);
                if (playerCamera == null)
                {
                    throw new InvalidOperationException(
                        "Canonical player prefab requires a Camera for M11 brightness post-processing.");
                }

                var cameraData = EnsureComponent<UniversalAdditionalCameraData>(playerCamera.gameObject);
                playerCamera.allowMSAA = false;
                cameraData.renderPostProcessing = true;
                cameraData.volumeLayerMask = ~0;
                EnsureComponent<PlayerCameraAntiAliasingApplier>(playerCamera.gameObject);
                EditorUtility.SetDirty(cameraData);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static T[] FindAssets<T>() where T : UnityEngine.Object
        {
            return AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { "Assets/Game" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset != null)
                .ToArray();
        }

        private static void SetKey(UnityEngine.Object asset, string field, string key, string fallback, StringTable table)
        {
            if (string.IsNullOrWhiteSpace(key) || key.Contains(".."))
            {
                return;
            }

            var serialized = new SerializedObject(asset);
            serialized.FindProperty(field).stringValue = key;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AddOrUpdate(table, key, fallback);
            EditorUtility.SetDirty(asset);
        }

        private static void AddOrUpdate(StringTable table, string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            var entry = table.GetEntry(key);
            if (entry == null)
            {
                table.AddEntry(key, value ?? string.Empty);
            }
            else
            {
                entry.Value = value ?? string.Empty;
            }
        }

        private static Slider CreateSliderRow(Transform parent, string label, float min, float max)
        {
            var row = CreateRow(parent, label);
            var sliderObject = CreateUiObject(label.Replace(" ", string.Empty) + "Slider", row);
            sliderObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var background = sliderObject.AddComponent<Image>();
            background.color = new Color(0.18f, 0.2f, 0.23f, 1f);
            var slider = sliderObject.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.targetGraphic = background;
            var handle = CreateUiObject("Handle", sliderObject.transform);
            var handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(24f, 40f);
            handle.AddComponent<Image>().color = new Color(0.72f, 0.86f, 0.92f, 1f);
            slider.handleRect = handleRect;
            return slider;
        }

        private static Toggle CreateToggleRow(Transform parent, string label)
        {
            var row = CreateRow(parent, label);
            var toggleObject = CreateUiObject(label.Replace(" ", string.Empty) + "Toggle", row);
            toggleObject.GetComponent<RectTransform>().sizeDelta = new Vector2(48f, 40f);
            toggleObject.AddComponent<LayoutElement>().preferredWidth = 48f;
            var background = toggleObject.AddComponent<Image>();
            background.color = new Color(0.18f, 0.2f, 0.23f, 1f);
            var checkmark = CreateUiObject("Checkmark", toggleObject.transform);
            Stretch(checkmark.GetComponent<RectTransform>(), 8f);
            var checkmarkImage = checkmark.AddComponent<Image>();
            checkmarkImage.color = new Color(0.55f, 0.9f, 0.72f, 1f);
            var toggle = toggleObject.AddComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = checkmarkImage;
            return toggle;
        }

        private static InputField CreateInputRow(Transform parent, string label, string value)
        {
            var row = CreateRow(parent, label);
            var inputObject = CreateUiObject(label.Replace(" ", string.Empty) + "Input", row);
            inputObject.AddComponent<Image>().color = new Color(0.18f, 0.2f, 0.23f, 1f);
            inputObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var text = CreateText(inputObject.transform, "Text", value, 20, 40f);
            Stretch(text.rectTransform, 8f);
            var input = inputObject.AddComponent<InputField>();
            input.textComponent = text;
            input.text = value;
            return input;
        }

        private static Transform CreateRow(Transform parent, string label)
        {
            var row = CreateUiObject(label.Replace(" ", string.Empty) + "Row", parent);
            row.AddComponent<LayoutElement>().preferredHeight = 48f;
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = false;
            var labelText = CreateText(row.transform, "Label", label, 20, 48f);
            var labelLayout = labelText.GetComponent<LayoutElement>();
            labelLayout.preferredWidth = 300f;
            return row.transform;
        }

        private static void CreateRebindButton(Transform parent, string name, string label, UnityAction callback)
        {
            var buttonObject = CreateUiObject(name, parent);
            buttonObject.AddComponent<LayoutElement>().preferredHeight = 50f;
            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.18f, 0.2f, 0.23f, 1f);
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            UnityEventTools.AddPersistentListener(button.onClick, callback);
            var text = CreateText(buttonObject.transform, "Label", label, 20, 50f);
            Stretch(text.rectTransform);
        }

        private static Text CreateText(Transform parent, string name, string value, int size, float height)
        {
            var textObject = CreateUiObject(name, parent);
            var text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Min(14, size);
            text.resizeTextMaxSize = size;
            textObject.AddComponent<LayoutElement>().preferredHeight = height;
            return text;
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            var target = new GameObject(name, typeof(RectTransform));
            target.transform.SetParent(parent, false);
            return target;
        }

        private static T EnsureComponent<T>(GameObject target) where T : Component
        {
            return target.GetComponent<T>() ?? target.AddComponent<T>();
        }

        private static void ConfigureReference(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null)
            {
                throw new InvalidOperationException($"Serialized field '{field}' was not found on {target.GetType().Name}.");
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddPersistent(UnityEvent<float> source, UnityAction<float> callback) =>
            UnityEventTools.AddPersistentListener(source, callback);

        private static void AddPersistent(UnityEvent<bool> source, UnityAction<bool> callback) =>
            UnityEventTools.AddPersistentListener(source, callback);

        private static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * inset;
            rect.offsetMax = -Vector2.one * inset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrWhiteSpace(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
