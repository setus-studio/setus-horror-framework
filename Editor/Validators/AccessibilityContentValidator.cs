using System;
using System.Collections.Generic;
using System.Linq;
using Setus.HorrorFramework.Accessibility.Display;
using Setus.HorrorFramework.Editor.Builders;
using Setus.HorrorFramework.Localization;
using Setus.HorrorFramework.Narrative.Notes;
using Setus.HorrorFramework.Narrative.Objectives;
using Setus.HorrorFramework.Narrative.Phone;
using Setus.HorrorFramework.Atmosphere.Scares;
using Setus.HorrorFramework.AI.Tuning;
using Setus.HorrorFramework.UI.Menus;
using Setus.HorrorFramework.UI.Settings;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Setus.HorrorFramework.Editor.Validators
{
    public static class AccessibilityContentValidator
    {
        private const string GameplayScenePath = "Assets/Game/Scenes/Gameplay.unity";

        [MenuItem("Setus/Horror Framework/Validation/Validate M11 Accessibility And Localization")]
        public static void ValidateFromMenu()
        {
            var issues = ValidateProject();
            foreach (var issue in issues)
            {
                Debug.LogError($"[M11] {issue}");
            }

            if (issues.Count == 0)
            {
                Debug.Log("M11 accessibility and localization validation passed.");
            }
        }

        public static IReadOnlyList<string> ValidateProject()
        {
            var issues = new List<string>(ValidateProjectAssets());
            ValidateGameplayScene(issues);
            return issues;
        }

        public static IReadOnlyList<string> ValidateProjectAssets()
        {
            var issues = new List<string>();
            if (LocalizationEditorSettings.ActiveLocalizationSettings == null)
            {
                issues.Add("Localization Settings is not configured for the project.");
            }

            var textTable = LocalizationEditorSettings.GetStringTableCollection(LocalizedTextReference.DefaultTable);
            if (textTable == null)
            {
                issues.Add($"String Table Collection '{LocalizedTextReference.DefaultTable}' is missing.");
            }
            else
            {
                ValidateRequiredFrameworkKeys(textTable, issues);
            }

            var shell = AssetDatabase.LoadAssetAtPath<GameObject>(SceneFlowUiShellTemplateBuilder.UiShellPrefabPath);
            if (shell == null)
            {
                issues.Add($"Canonical UI shell is missing at '{SceneFlowUiShellTemplateBuilder.UiShellPrefabPath}'.");
            }
            else
            {
                ValidateUiShellReferences(shell, textTable, issues);
            }

            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerFoundationTemplateBuilder.PlayerPrefabPath);
            var playerCamera = player != null ? player.GetComponentInChildren<UnityEngine.Camera>(true) : null;
            var cameraData = playerCamera != null
                ? playerCamera.GetComponent<UniversalAdditionalCameraData>()
                : null;
            if (playerCamera == null)
            {
                issues.Add("Canonical player prefab is missing its player Camera.");
            }
            else if (cameraData == null || !cameraData.renderPostProcessing)
            {
                issues.Add(
                    "Canonical player Camera must enable URP post-processing for the brightness Volume setting.");
            }

            ValidateKeys<ObjectiveDefinition>(
                "objectiveId",
                new[] { "titleLocalizationKey", "descriptionLocalizationKey" },
                issues,
                textTable);
            ValidateKeys<PhoneMessageDefinition>(
                "messageId",
                new[] { "senderLocalizationKey", "messageLocalizationKey", "replyLocalizationKey" },
                issues,
                textTable);
            ValidateKeys<NarrativeNoteDefinition>(
                "noteId",
                new[] { "titleLocalizationKey", "bodyLocalizationKey" },
                issues,
                textTable);
            ValidateKeys<ScareDefinition>(
                "scareId",
                new[] { "fallbackSpeakerLocalizationKey", "fallbackCueLocalizationKey" },
                issues,
                textTable,
                allowEmptyTextFields: true);
            ValidateStandaloneKey<StalkerAiTuningProfile>(
                "chaseSubtitleLocalizationKey",
                "chaseSubtitle",
                issues,
                textTable);
            return issues;
        }

        public static IReadOnlyList<string> ValidateUiShellReferences(GameObject shell)
        {
            var issues = new List<string>();
            var textTable = LocalizationEditorSettings.GetStringTableCollection(
                LocalizedTextReference.DefaultTable);
            ValidateUiShellReferences(shell, textTable, issues);
            return issues;
        }

        public static IReadOnlyList<string> ValidateGameplayReferences(IEnumerable<GameObject> roots)
        {
            var issues = new List<string>();
            ValidateGameplayReferences(roots, issues);
            return issues;
        }

        private static void ValidateUiShellReferences(
            GameObject shell,
            StringTableCollection textTable,
            ICollection<string> issues)
        {
            if (shell == null)
            {
                issues.Add("Owner '<missing UI shell>', field 'root': canonical UI shell is missing.");
                return;
            }

            if (shell.GetComponent<RuntimeLocaleController>() == null)
            {
                issues.Add($"Owner '{Describe(shell)}', field 'RuntimeLocaleController': required component is missing.");
            }

            var settingsPanel = shell.GetComponentInChildren<AccessibilitySettingsPanel>(true);
            if (settingsPanel == null)
            {
                issues.Add($"Owner '{Describe(shell)}', field 'AccessibilitySettingsPanel': required component is missing.");
            }
            else
            {
                ValidateRequiredReferences(
                    settingsPanel,
                    new[]
                    {
                        "masterVolume",
                        "ambienceVolume",
                        "sfxVolume",
                        "uiVolume",
                        "voiceVolume",
                        "subtitlesEnabled",
                        "brightness",
                        "cameraShakeIntensity",
                        "headBobEnabled",
                        "headBobIntensity",
                        "sprintToggle",
                        "mouseSensitivity",
                        "localeCode"
                    },
                    issues);
            }

            var rebind = shell.GetComponentInChildren<InputRebindPresenter>(true);
            if (rebind == null)
            {
                issues.Add($"Owner '{Describe(shell)}', field 'InputRebindPresenter': required component is missing.");
            }
            else
            {
                ValidateRequiredReferences(rebind, new[] { "actions", "statusText" }, issues);
            }

            foreach (var presenter in shell.GetComponentsInChildren<LocalizedTextPresenter>(true))
            {
                ValidateLocalizedPresenter(presenter, textTable, issues);
            }
        }

        private static void ValidateRequiredReferences(
            Component owner,
            IEnumerable<string> fields,
            ICollection<string> issues)
        {
            var serialized = new SerializedObject(owner);
            foreach (var field in fields)
            {
                var property = serialized.FindProperty(field);
                if (property == null)
                {
                    issues.Add(
                        $"Owner '{Describe(owner)}', field '{field}': serialized field could not be inspected.");
                }
                else if (property.objectReferenceValue == null)
                {
                    issues.Add($"Owner '{Describe(owner)}', field '{field}': required reference is missing.");
                }
            }
        }

        private static void ValidateLocalizedPresenter(
            LocalizedTextPresenter presenter,
            StringTableCollection defaultTable,
            ICollection<string> issues)
        {
            var serialized = new SerializedObject(presenter);
            var target = serialized.FindProperty("target")?.objectReferenceValue;
            if (target == null && presenter.GetComponent<UnityEngine.UI.Text>() == null)
            {
                issues.Add($"Owner '{Describe(presenter)}', field 'target': required Text reference is missing.");
            }

            var tableName = serialized.FindProperty("tableCollection")?.stringValue;
            if (string.IsNullOrWhiteSpace(tableName))
            {
                issues.Add($"Owner '{Describe(presenter)}', field 'tableCollection': localization table is empty.");
                return;
            }

            var entryKey = serialized.FindProperty("entryKey")?.stringValue;
            if (string.IsNullOrWhiteSpace(entryKey))
            {
                issues.Add($"Owner '{Describe(presenter)}', field 'entryKey': localization key is empty.");
                return;
            }

            var table = tableName == LocalizedTextReference.DefaultTable
                ? defaultTable
                : LocalizationEditorSettings.GetStringTableCollection(tableName);
            if (table == null)
            {
                issues.Add(
                    $"Owner '{Describe(presenter)}', field 'tableCollection': String Table Collection " +
                    $"'{tableName}' does not exist.");
            }
            else if (table.SharedData.GetEntry(entryKey) == null)
            {
                issues.Add(
                    $"Owner '{Describe(presenter)}', field 'entryKey': localization key '{entryKey}' " +
                    $"does not exist in '{tableName}'.");
            }
        }

        private static void ValidateGameplayScene(ICollection<string> issues)
        {
            if (!System.IO.File.Exists(GameplayScenePath))
            {
                issues.Add(
                    $"Owner '{GameplayScenePath}', field 'scene': " +
                    "canonical Gameplay scene is missing.");
                return;
            }

            UnityEngine.SceneManagement.Scene scene = default;
            try
            {
                scene = EditorSceneManager.OpenPreviewScene(GameplayScenePath);
                ValidateGameplayReferences(scene.GetRootGameObjects(), issues);
            }
            catch (Exception exception)
            {
                issues.Add(
                    $"Owner '{GameplayScenePath}', field 'scene': " +
                    $"could not inspect Gameplay scene: {exception.Message}");
            }
            finally
            {
                if (scene.IsValid())
                {
                    EditorSceneManager.ClosePreviewScene(scene);
                }
            }
        }

        private static void ValidateGameplayReferences(
            IEnumerable<GameObject> roots,
            ICollection<string> issues)
        {
            var rootArray = (roots ?? Enumerable.Empty<GameObject>())
                .Where(root => root != null)
                .ToArray();
            var appliers = rootArray
                .SelectMany(root => root.GetComponentsInChildren<BrightnessVolumeSettingsApplier>(true))
                .ToArray();
            if (appliers.Length == 0)
            {
                issues.Add(
                    "Owner 'Gameplay', field 'BrightnessVolumeSettingsApplier': required component is missing.");
                return;
            }

            foreach (var applier in appliers)
            {
                var serialized = new SerializedObject(applier);
                var referencedVolume = serialized.FindProperty("volume")?.objectReferenceValue as Volume;
                var localVolume = applier.GetComponent<Volume>();
                var effectiveVolume = referencedVolume != null ? referencedVolume : localVolume;
                if (effectiveVolume == null)
                {
                    issues.Add($"Owner '{Describe(applier)}', field 'volume': required Volume is missing.");
                    continue;
                }

                if (referencedVolume != null && referencedVolume != localVolume)
                {
                    issues.Add(
                        $"Owner '{Describe(applier)}', field 'volume': reference must target the co-located Volume.");
                }

                if (!effectiveVolume.isGlobal)
                {
                    issues.Add(
                        $"Owner '{Describe(applier)}', field 'volume.isGlobal': brightness Volume must be global.");
                }

                var minimum = serialized.FindProperty("minimumPostExposure")?.floatValue ?? float.NaN;
                var maximum = serialized.FindProperty("maximumPostExposure")?.floatValue ?? float.NaN;
                if (float.IsNaN(minimum) || float.IsInfinity(minimum) ||
                    float.IsNaN(maximum) || float.IsInfinity(maximum) ||
                    minimum > maximum)
                {
                    issues.Add(
                        $"Owner '{Describe(applier)}', field 'minimumPostExposure/maximumPostExposure': " +
                        "values must be finite and minimum must not exceed maximum.");
                }
            }
        }

        private static string Describe(Component component)
        {
            return component == null ? "<missing>" : $"{Describe(component.gameObject)} ({component.GetType().Name})";
        }

        private static string Describe(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return "<missing>";
            }

            var names = new Stack<string>();
            for (var current = gameObject.transform; current != null; current = current.parent)
            {
                names.Push(current.name);
            }

            var location = AssetDatabase.GetAssetPath(gameObject);
            if (string.IsNullOrWhiteSpace(location))
            {
                location = gameObject.scene.IsValid() ? gameObject.scene.path : string.Empty;
            }

            var hierarchy = string.Join("/", names);
            return string.IsNullOrWhiteSpace(location) ? hierarchy : $"{location}:{hierarchy}";
        }

        private static void ValidateStandaloneKey<T>(
            string keyField,
            string fallbackField,
            ICollection<string> issues,
            StringTableCollection textTable)
            where T : UnityEngine.Object
        {
            foreach (var asset in AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { "Assets/Game" })
                         .Select(AssetDatabase.GUIDToAssetPath)
                         .Select(AssetDatabase.LoadAssetAtPath<T>)
                         .Where(asset => asset != null))
            {
                var serialized = new SerializedObject(asset);
                var key = serialized.FindProperty(keyField)?.stringValue;
                var fallback = serialized.FindProperty(fallbackField)?.stringValue;
                if (string.IsNullOrWhiteSpace(key))
                {
                    issues.Add(
                        $"Asset '{AssetDatabase.GetAssetPath(asset)}' field '{keyField}' is empty " +
                        $"for authored fallback '{fallback ?? string.Empty}'.");
                    continue;
                }

                if (textTable != null && textTable.SharedData.GetEntry(key) == null)
                {
                    issues.Add(
                        $"Asset '{AssetDatabase.GetAssetPath(asset)}' field '{keyField}' references " +
                        $"missing '{LocalizedTextReference.DefaultTable}' entry '{key}'.");
                }
            }
        }

        private static void ValidateKeys<T>(
            string idField,
            IReadOnlyList<string> keyFields,
            ICollection<string> issues,
            StringTableCollection textTable,
            bool allowEmptyTextFields = false)
            where T : UnityEngine.Object
        {
            var assets = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { "Assets/Game" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset != null);
            foreach (var asset in assets)
            {
                var serialized = new SerializedObject(asset);
                var id = serialized.FindProperty(idField)?.stringValue;
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                foreach (var field in keyFields)
                {
                    var key = serialized.FindProperty(field)?.stringValue;
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        if (textTable != null && textTable.SharedData.GetEntry(key) == null)
                        {
                            issues.Add(
                                $"Asset '{AssetDatabase.GetAssetPath(asset)}' field '{field}' references " +
                                $"missing '{LocalizedTextReference.DefaultTable}' entry '{key}'.");
                        }
                        continue;
                    }

                    if (allowEmptyTextFields && field == "fallbackCueLocalizationKey")
                    {
                        var fallback = serialized.FindProperty("fallbackCueText")?.stringValue;
                        if (string.IsNullOrWhiteSpace(fallback))
                        {
                            continue;
                        }
                    }

                    issues.Add($"Asset '{AssetDatabase.GetAssetPath(asset)}' field '{field}' is empty for ID '{id}'.");
                }
            }
        }

        private static void ValidateRequiredFrameworkKeys(
            StringTableCollection textTable,
            ICollection<string> issues)
        {
            var requiredKeys = new[]
            {
                FrameworkTextKeys.InteractionPrompt,
                FrameworkTextKeys.InteractionUnavailablePrompt,
                FrameworkTextKeys.DoorOpenPrompt,
                FrameworkTextKeys.DoorClosePrompt,
                FrameworkTextKeys.DoorLockedPrompt,
                FrameworkTextKeys.DrawerOpenPrompt,
                FrameworkTextKeys.DrawerClosePrompt,
                FrameworkTextKeys.DrawerLockedPrompt,
                FrameworkTextKeys.PickupPrompt,
                FrameworkTextKeys.PickupAlreadyCollectedPrompt,
                FrameworkTextKeys.InspectablePrompt,
                FrameworkTextKeys.InspectableAlreadyInspectedPrompt,
                FrameworkTextKeys.UnknownSpeaker,
                FrameworkTextKeys.StalkerChaseSubtitle,
                FrameworkTextKeys.PhoneStatusNew,
                FrameworkTextKeys.PhoneStatusRead,
                FrameworkTextKeys.PhoneStatusReplied,
                FrameworkTextKeys.PlayerSpeaker,
                FrameworkTextKeys.PhoneCheckPrompt,
                FrameworkTextKeys.PhoneReadPrompt,
                FrameworkTextKeys.PhoneReplyPrompt,
                FrameworkTextKeys.PhoneHandledPrompt,
                FrameworkTextKeys.PhoneUnavailablePrompt,
                FrameworkTextKeys.NoteReadPrompt,
                FrameworkTextKeys.NoteReadAgainPrompt,
                FrameworkTextKeys.NoteUnavailablePrompt,
                FrameworkTextKeys.PhoneNotConfiguredResult,
                FrameworkTextKeys.PhoneDeliveredResult,
                FrameworkTextKeys.PhoneDeliveryFailedResult,
                FrameworkTextKeys.PhoneReadResult,
                FrameworkTextKeys.PhoneReplySentResult,
                FrameworkTextKeys.PhoneAlreadyHandledResult,
                FrameworkTextKeys.NoteNotConfiguredResult,
                FrameworkTextKeys.NoteReadResult,
                FrameworkTextKeys.RebindMissingActions,
                FrameworkTextKeys.RebindActionUnavailable,
                FrameworkTextKeys.RebindWaiting,
                FrameworkTextKeys.RebindReset,
                FrameworkTextKeys.RebindUpdated,
                FrameworkTextKeys.RebindCancelled,
                FrameworkTextKeys.SaveErrorNoSave,
                FrameworkTextKeys.SaveErrorCorrupt,
                FrameworkTextKeys.SaveErrorIncompatible,
                FrameworkTextKeys.SaveErrorRestoreFailed,
                FrameworkTextKeys.SaveErrorSaveFailed,
                FrameworkTextKeys.SaveErrorUnavailable,
                FrameworkTextKeys.SaveStatusReady,
                FrameworkTextKeys.SaveStatusSaved,
                FrameworkTextKeys.SaveStatusLoading,
                FrameworkTextKeys.SaveUnavailableOutsideGameplay,
                FrameworkTextKeys.LoadUnavailableFromPause
            };

            foreach (var key in requiredKeys)
            {
                if (textTable.SharedData.GetEntry(key) == null)
                {
                    issues.Add(
                        $"String Table Collection '{LocalizedTextReference.DefaultTable}' is missing required entry '{key}'.");
                }
            }
        }
    }
}
