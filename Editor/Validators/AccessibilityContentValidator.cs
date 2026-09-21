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
using Setus.HorrorFramework.Atmosphere.Audio;
using Setus.HorrorFramework.Audio.Settings;
using Setus.HorrorFramework.AI.Feedback;
using Setus.HorrorFramework.AI.Tuning;
using Setus.HorrorFramework.Interaction.Interactors;
using Setus.HorrorFramework.UI.Menus;
using Setus.HorrorFramework.UI.Settings;
using Setus.HorrorFramework.UI.Settings.Display;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
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

            ValidateGraphicsSettings(issues);

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
            else if (playerCamera.GetComponent<PlayerCameraAntiAliasingApplier>() == null)
            {
                issues.Add($"Owner '{Describe(playerCamera)}', field 'PlayerCameraAntiAliasingApplier': saved anti-aliasing needs a camera-local runtime applier.");
            }

            if (shell != null && shell.GetComponentInChildren<TabbedSettingsPresenter>(true) != null &&
                player != null)
            {
                var rebind = shell.GetComponentInChildren<InputRebindPresenter>(true);
                var rebindActions = rebind != null
                    ? new SerializedObject(rebind).FindProperty("actions")?.objectReferenceValue as InputActionAsset
                    : null;
                var interactor = player.GetComponent<RaycastInteractor>();
                if (interactor == null || interactor.ActionsAsset == null)
                    issues.Add($"Owner '{Describe(player)}', field 'RaycastInteractor.actionsAsset': shared Interact action asset is missing.");
                else if (rebindActions != null && interactor.ActionsAsset != rebindActions)
                    issues.Add($"Owner '{Describe(player)}', field 'RaycastInteractor.actionsAsset': must match InputRebindPresenter.actions.");
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

        private static void ValidateGraphicsSettings(ICollection<string> issues)
        {
            foreach (var name in new[] { "Low", "Medium", "High" })
            {
                var qualityIndex = Array.IndexOf(QualitySettings.names, name);
                if (qualityIndex < 0)
                    issues.Add($"Owner 'ProjectSettings/QualitySettings.asset', field 'name': Standalone quality level '{name}' is missing.");

                var path = $"Assets/Game/Settings/Graphics/{name}_RPAsset.asset";
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (asset == null)
                    issues.Add($"Owner '{path}', field 'asset': game-owned URP quality asset is missing.");
                else if (asset.msaaSampleCount != 1)
                    issues.Add($"Owner '{path}', field 'msaaSampleCount': must be Disabled so Off/FXAA/SMAA do not stack with MSAA.");

                if (qualityIndex >= 0 && asset != null)
                {
                    var mappingIssue = ValidateGraphicsQualityPipelineMapping(
                        name,
                        path,
                        asset,
                        QualitySettings.GetRenderPipelineAssetAt(qualityIndex));
                    if (!string.IsNullOrEmpty(mappingIssue))
                        issues.Add(mappingIssue);
                }
            }
        }

        public static string ValidateGraphicsQualityPipelineMapping(
            string qualityLevel,
            string expectedPath,
            RenderPipelineAsset expectedAsset,
            RenderPipelineAsset actualAsset)
        {
            if (expectedAsset != null && expectedAsset == actualAsset)
                return string.Empty;

            return $"Owner 'ProjectSettings/QualitySettings.asset', quality level '{qualityLevel}', " +
                $"field 'customRenderPipeline': expected '{DescribePipelineAsset(expectedAsset, expectedPath)}', " +
                $"actual '{DescribePipelineAsset(actualAsset, null)}'.";
        }

        private static string DescribePipelineAsset(RenderPipelineAsset asset, string fallbackPath)
        {
            if (asset == null)
                return string.IsNullOrEmpty(fallbackPath) ? "<missing>" : $"<missing> ({fallbackPath})";

            var path = AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrEmpty(path) ? asset.name : $"{asset.name} ({path})";
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

        public static IReadOnlyList<string> ValidateAudioSettingsReferences(
            GameObject shell,
            IEnumerable<GameObject> gameplayRoots)
        {
            var issues = new List<string>();
            ValidateAudioSettingsReferences(shell, gameplayRoots, issues);
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
                if (shell.GetComponentInChildren<TabbedSettingsPresenter>(true) != null)
                {
                    ValidateRequiredReferences(rebind, new[]
                    {
                        "rebindModal", "settingsLayoutGroup", "modalTitle", "modalPrompt",
                        "modalCurrent", "modalRetry", "modalConfirmReset", "modalCancel"
                    }, issues);
                    var serialized = new SerializedObject(rebind);
                    var labels = serialized.FindProperty("bindingLabels");
                    if (labels == null || labels.arraySize != 9 ||
                        Enumerable.Range(0, labels.arraySize)
                            .Any(index => labels.GetArrayElementAtIndex(index).objectReferenceValue == null))
                    {
                        issues.Add($"Owner '{Describe(rebind)}', field 'bindingLabels': nine current-binding labels are required.");
                    }

                    var actions = serialized.FindProperty("actions")?.objectReferenceValue as InputActionAsset;
                    if (actions != null)
                    {
                        foreach (var path in new[] { "Player/Interact", "Player/Pause" })
                        {
                            var action = actions.FindAction(path, false);
                            if (action == null)
                                issues.Add($"Owner '{Describe(rebind)}', field 'actions': required action '{path}' is missing.");
                            else if (!action.bindings.Any(binding =>
                                !binding.isComposite && !binding.isPartOfComposite &&
                                !string.IsNullOrEmpty(binding.path) &&
                                binding.path.StartsWith("<Keyboard>", StringComparison.OrdinalIgnoreCase)))
                                issues.Add($"Owner '{Describe(rebind)}', field 'actions': action '{path}' requires a keyboard binding for PC rebind.");
                        }
                        var pause = shell.GetComponent<PauseInputAdapter>();
                        var pauseActions = pause != null
                            ? new SerializedObject(pause).FindProperty("actionsAsset")?.objectReferenceValue
                            : null;
                        if (pause == null || pauseActions != actions)
                            issues.Add($"Owner '{Describe(shell)}', field 'PauseInputAdapter.actionsAsset': must reference InputRebindPresenter.actions.");
                        else if (!new SerializedObject(pause).FindProperty("escapeTogglesPause").boolValue)
                            issues.Add($"Owner '{Describe(pause)}', field 'escapeTogglesPause': Escape fallback must remain enabled for safe Pause recovery.");
                    }
                }
            }

            var tabbed = shell.GetComponentInChildren<TabbedSettingsPresenter>(true);
            if (tabbed != null)
            {
                if (shell.GetComponent<DisplaySettingsRuntimeApplier>() == null)
                    issues.Add($"Owner '{Describe(shell)}', field 'DisplaySettingsRuntimeApplier': saved display preferences require an applier on the UI shell root.");

                var display = shell.GetComponentInChildren<DisplaySettingsPresenter>(true);
                if (display == null)
                    issues.Add($"Owner '{Describe(tabbed)}', field 'DisplaySettingsPresenter': Display tab and confirmation presenter are missing.");
                else
                    ValidateRequiredReferences(display, new[]
                    {
                        "mode", "resolution", "refresh", "vSync", "frameRate", "quality", "antiAliasing",
                        "status", "apply", "confirmationModal", "countdown", "keep", "revert",
                        "settingsLayoutGroup"
                    }, issues);

                var tabbedFields = new SerializedObject(tabbed);
                var pages = tabbedFields.FindProperty("pages");
                var tabs = tabbedFields.FindProperty("tabs");
                if (pages == null || tabs == null || pages.arraySize != 5 || tabs.arraySize != 5 ||
                    pages.GetArrayElementAtIndex(4).objectReferenceValue == null ||
                    tabs.GetArrayElementAtIndex(4).objectReferenceValue == null)
                    issues.Add($"Owner '{Describe(tabbed)}', field 'pages/tabs': Display page and tab must be wired at index 4.");
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
                var roots = scene.GetRootGameObjects();
                ValidateGameplayReferences(roots, issues);
                var shell = AssetDatabase.LoadAssetAtPath<GameObject>(
                    SceneFlowUiShellTemplateBuilder.UiShellPrefabPath);
                ValidateAudioSettingsReferences(shell, roots, issues);
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

        private static void ValidateAudioSettingsReferences(
            GameObject shell,
            IEnumerable<GameObject> gameplayRoots,
            ICollection<string> issues)
        {
            var roots = (gameplayRoots ?? Enumerable.Empty<GameObject>())
                .Where(root => root != null)
                .Concat(shell != null ? new[] { shell } : Array.Empty<GameObject>())
                .Distinct()
                .ToArray();
            var consumers = CollectAudioVolumeConsumers(roots);

            foreach (var duplicate in consumers
                         .Where(consumer => consumer.Source != null)
                         .GroupBy(consumer => consumer.Source)
                         .Where(group => group.Count() > 1))
            {
                var labels = string.Join(", ", duplicate.Select(consumer =>
                    $"{consumer.Kind} '{Describe(consumer.Owner)}.{consumer.Field}' [{consumer.Category}]"));
                issues.Add(
                    $"Owner '{Describe(duplicate.Key)}', field 'volume': AudioSource has multiple settings " +
                    $"volume consumers: {labels}. Use exactly one specialized hook or AudioSourceSettingsBinding.");
            }

            if (shell == null)
                return;

            ValidateActiveAudioCategory(shell, "MasterVolumeRow", "Master", consumers.Count > 0, issues);
            ValidateActiveAudioCategory(shell, "AmbienceVolumeRow", nameof(AudioVolumeCategory.Ambience),
                consumers.Any(consumer => consumer.Category == AudioVolumeCategory.Ambience), issues);
            ValidateActiveAudioCategory(shell, "SFXVolumeRow", nameof(AudioVolumeCategory.Sfx),
                consumers.Any(consumer => consumer.Category == AudioVolumeCategory.Sfx), issues);
            ValidateActiveAudioCategory(shell, "UIVolumeRow", nameof(AudioVolumeCategory.Ui),
                consumers.Any(consumer => consumer.Category == AudioVolumeCategory.Ui), issues);
            ValidateActiveAudioCategory(shell, "VoiceVolumeRow", nameof(AudioVolumeCategory.Voice),
                consumers.Any(consumer => consumer.Category == AudioVolumeCategory.Voice), issues);
        }

        private static List<AudioVolumeConsumer> CollectAudioVolumeConsumers(IEnumerable<GameObject> roots)
        {
            var consumers = new List<AudioVolumeConsumer>();
            var seenOwners = new HashSet<Component>();
            foreach (var root in roots)
            {
                foreach (var binding in root.GetComponentsInChildren<AudioSourceSettingsBinding>(true))
                {
                    if (!seenOwners.Add(binding)) continue;
                    var serialized = new SerializedObject(binding);
                    var source = serialized.FindProperty("source")?.objectReferenceValue as AudioSource;
                    source ??= binding.GetComponent<AudioSource>();
                    var category = (AudioVolumeCategory)(serialized.FindProperty("category")?.enumValueIndex ??
                        (int)AudioVolumeCategory.Sfx);
                    AddAudioConsumer(consumers, binding, source, category, "source", "generic");
                }

                foreach (var ambience in root.GetComponentsInChildren<AmbienceLayerController>(true))
                {
                    if (!seenOwners.Add(ambience)) continue;
                    var serialized = new SerializedObject(ambience);
                    AddAudioConsumer(consumers, ambience,
                        serialized.FindProperty("lowTensionLayer")?.objectReferenceValue as AudioSource,
                        AudioVolumeCategory.Ambience, "lowTensionLayer", "specialized");
                    AddAudioConsumer(consumers, ambience,
                        serialized.FindProperty("highTensionLayer")?.objectReferenceValue as AudioSource,
                        AudioVolumeCategory.Ambience, "highTensionLayer", "specialized");
                }

                foreach (var scare in root.GetComponentsInChildren<ScareAudioCueHook>(true))
                {
                    if (!seenOwners.Add(scare)) continue;
                    AddAudioConsumer(consumers, scare,
                        new SerializedObject(scare).FindProperty("cueSource")?.objectReferenceValue as AudioSource,
                        AudioVolumeCategory.Sfx, "cueSource", "specialized");
                }

                foreach (var feedback in root.GetComponentsInChildren<StalkerAiFeedbackHook>(true))
                {
                    if (!seenOwners.Add(feedback)) continue;
                    AddAudioConsumer(consumers, feedback,
                        new SerializedObject(feedback).FindProperty("cueSource")?.objectReferenceValue as AudioSource,
                        AudioVolumeCategory.Sfx, "cueSource", "specialized");
                }
            }
            return consumers;
        }

        private static void AddAudioConsumer(
            ICollection<AudioVolumeConsumer> consumers,
            Component owner,
            AudioSource source,
            AudioVolumeCategory category,
            string field,
            string kind)
        {
            if (source != null)
                consumers.Add(new AudioVolumeConsumer(owner, source, category, field, kind));
        }

        private static void ValidateActiveAudioCategory(
            GameObject shell,
            string rowName,
            string category,
            bool hasConsumer,
            ICollection<string> issues)
        {
            var activeRows = shell.GetComponentsInChildren<Transform>(true)
                .Where(transform => transform.name == rowName && IsAudioControlActive(transform))
                .ToArray();
            if (activeRows.Length == 0 || hasConsumer)
                return;

            foreach (var row in activeRows)
            {
                issues.Add(
                    $"Owner '{Describe(row.gameObject)}', field '{category} audio category': active settings " +
                    "control requires a matching volume consumer. Hidden or disabled optional controls are allowed.");
            }
        }

        private static bool IsAudioControlActive(Transform row)
        {
            if (row == null || !row.gameObject.activeSelf)
                return false;

            return row.GetComponentsInChildren<UnityEngine.UI.Slider>(true).Any(slider =>
                slider != null && slider.enabled && slider.interactable &&
                IsActiveWithinRow(slider.transform, row));
        }

        private static bool IsActiveWithinRow(Transform current, Transform row)
        {
            while (current != null)
            {
                if (!current.gameObject.activeSelf)
                    return false;
                if (current == row)
                    return true;
                current = current.parent;
            }
            return false;
        }

        private sealed class AudioVolumeConsumer
        {
            public AudioVolumeConsumer(Component owner, AudioSource source, AudioVolumeCategory category,
                string field, string kind)
            {
                Owner = owner;
                Source = source;
                Category = category;
                Field = field;
                Kind = kind;
            }

            public Component Owner { get; }
            public AudioSource Source { get; }
            public AudioVolumeCategory Category { get; }
            public string Field { get; }
            public string Kind { get; }
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
                FrameworkTextKeys.DisplayAntiAliasing,
                FrameworkTextKeys.DisplayAaOff,
                FrameworkTextKeys.DisplayQualityLow,
                FrameworkTextKeys.DisplayQualityMedium,
                FrameworkTextKeys.DisplayQualityHigh,
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
