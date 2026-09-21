using System;
using System.IO;
using System.Linq;
using Setus.HorrorFramework.Editor.Validators;
using Setus.HorrorFramework.Localization;
using Setus.HorrorFramework.UI.Settings.Display;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Setus.HorrorFramework.Editor.Builders
{
    public static class GraphicsSettingsMilestoneBuilder
    {
        private const string GraphicsRoot = "Assets/Game/Settings/Graphics";

        [MenuItem("Setus/Horror Framework/Settings/Apply Phase 6 Graphics Settings")]
        public static void ApplyFromMenu()
        {
            Apply();
            Debug.Log("Phase 6 graphics settings are wired. Inspect the player camera, UI prefab, and localization tables before Play Mode.");
        }

        public static void Apply()
        {
            EnsureDefaults();
            ValidateGamePresets();
            EnsurePlayerCamera();
            EnsureLocalizationKeys();
            LocalizationRolloutBuilder.Apply();
            TabbedSettingsLayoutBuilder.ApplyDisplaySettings();
            AssetDatabase.SaveAssets();
        }

        public static void EnsureDefaults()
        {
            var names = new[] { "Low", "Medium", "High" };
            var missingAssets = names.Where(name =>
                AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PresetPath(name)) == null).ToArray();
            var missingLevels = names.Where(name => !QualitySettings.names.Contains(name)).ToArray();
            if (missingAssets.Length == 0 && missingLevels.Length == 0)
                return;

            var qualityObject = QualitySettings.GetQualitySettings();
            if (qualityObject == null)
                throw new InvalidOperationException("Unity Quality Settings are unavailable for preset creation.");
            var serialized = new SerializedObject(qualityObject);
            var levels = serialized.FindProperty("m_QualitySettings");
            if (levels == null || levels.arraySize == 0)
                throw new InvalidOperationException("Unity Quality Settings cannot be inspected for preset creation.");
            if (missingLevels.Length > 0)
            {
                var prototype = levels.GetArrayElementAtIndex(levels.arraySize - 1);
                foreach (var field in new[]
                         {
                             "name", "customRenderPipeline", "antiAliasing", "shadowDistance",
                             "lodBias", "globalTextureMipmapLimit", "excludedTargetPlatforms"
                         })
                {
                    if (prototype.FindPropertyRelative(field) == null)
                        throw new InvalidOperationException($"Unity Quality Settings field '{field}' is unavailable.");
                }
            }

            UniversalRenderPipelineAsset template = null;
            if (missingAssets.Length > 0)
            {
                template = ResolveTemplate();
                if (template == null)
                    throw new InvalidOperationException(
                        "Assign a URP asset in Project Settings > Graphics or Quality before creating graphics presets.");
                foreach (var name in missingAssets)
                {
                    if (AssetDatabase.LoadMainAssetAtPath(PresetPath(name)) != null)
                        throw new InvalidOperationException($"Preset path contains a non-URP asset: {PresetPath(name)}");
                }

                EnsureFolder(GraphicsRoot);
                foreach (var name in missingAssets)
                    AssetDatabase.CreateAsset(CreateDefaultPipelineAsset(template, name), PresetPath(name));
            }

            foreach (var name in missingLevels)
            {
                if (!levels.GetArrayElementAtIndex(levels.arraySize - 1).DuplicateCommand())
                    throw new InvalidOperationException("Unity Quality Settings could not duplicate a preset level.");
                levels = serialized.FindProperty("m_QualitySettings");
                var level = levels.GetArrayElementAtIndex(levels.arraySize - 1);
                level.FindPropertyRelative("name").stringValue = name;
                level.FindPropertyRelative("customRenderPipeline").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PresetPath(name));
                level.FindPropertyRelative("antiAliasing").intValue = 0;
                level.FindPropertyRelative("shadowDistance").floatValue =
                    name == "Low" ? 25f : name == "Medium" ? 35f : 40f;
                level.FindPropertyRelative("lodBias").floatValue =
                    name == "Low" ? 0.8f : name == "Medium" ? 1.25f : 2f;
                level.FindPropertyRelative("globalTextureMipmapLimit").intValue = name == "Low" ? 1 : 0;

                var excluded = level.FindPropertyRelative("excludedTargetPlatforms");
                excluded.arraySize = 2;
                excluded.GetArrayElementAtIndex(0).stringValue = "Android";
                excluded.GetArrayElementAtIndex(1).stringValue = "iPhone";
            }

            if (missingLevels.Length > 0)
            {
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(qualityObject);
            }
        }

        public static UniversalRenderPipelineAsset CreateDefaultPipelineAsset(
            UniversalRenderPipelineAsset template, string name)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            if (name != "Low" && name != "Medium" && name != "High")
                throw new ArgumentException("Expected Low, Medium, or High.", nameof(name));

            var asset = UnityEngine.Object.Instantiate(template);
            asset.name = $"{name}_RPAsset";
            asset.msaaSampleCount = 1;
            asset.renderScale = name == "Low" ? 0.85f : name == "Medium" ? 0.9f : 1f;
            asset.shadowDistance = name == "Low" ? 25f : name == "Medium" ? 35f : 50f;
            asset.mainLightShadowmapResolution = name == "High" ? 2048 : 1024;
            asset.shadowCascadeCount = name == "High" ? 4 : 2;
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("m_SoftShadowsSupported").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static UniversalRenderPipelineAsset ResolveTemplate()
        {
            var template = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PresetPath("High"));
            if (template != null) return template;
            template = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (template != null) return template;
            for (var index = 0; index < QualitySettings.names.Length; index++)
            {
                template = QualitySettings.GetRenderPipelineAssetAt(index) as UniversalRenderPipelineAsset;
                if (template != null) return template;
            }
            return null;
        }

        private static string PresetPath(string name) => $"{GraphicsRoot}/{name}_RPAsset.asset";

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent))
                throw new InvalidOperationException($"Cannot create graphics folder '{path}'.");
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static void EnsureLocalizationKeys()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(
                LocalizedTextReference.DefaultTable);
            var english = LocalizationEditorSettings.GetLocale("en");
            var table = english != null ? collection?.GetTable(english.Identifier) as StringTable : null;
            if (table == null)
                throw new InvalidOperationException("Phase 6 requires the English Game Text table.");

            AddMissing(table, FrameworkTextKeys.DisplayAntiAliasing, "Anti-aliasing");
            AddMissing(table, FrameworkTextKeys.DisplayAaOff, "Off");
            AddMissing(table, FrameworkTextKeys.DisplayQualityLow, "Low");
            AddMissing(table, FrameworkTextKeys.DisplayQualityMedium, "Medium");
            AddMissing(table, FrameworkTextKeys.DisplayQualityHigh, "High");
            EditorUtility.SetDirty(table);
            EditorUtility.SetDirty(collection.SharedData);
        }

        private static void AddMissing(StringTable table, string key, string fallback)
        {
            if (table.GetEntry(key) == null) table.AddEntry(key, fallback);
        }

        private static void ValidateGamePresets()
        {
            foreach (var name in new[] { "Low", "Medium", "High" })
            {
                if (!QualitySettings.names.Contains(name))
                    throw new InvalidOperationException($"Quality Settings must expose a '{name}' level for the active build target.");
                var path = PresetPath(name);
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (asset == null || asset.msaaSampleCount != 1)
                    throw new InvalidOperationException($"'{path}' must be a URP asset with MSAA disabled for Off/FXAA/SMAA.");
                var qualityIndex = Array.IndexOf(QualitySettings.names, name);
                var issue = AccessibilityContentValidator.ValidateGraphicsQualityPipelineMapping(
                    name, path, asset, QualitySettings.GetRenderPipelineAssetAt(qualityIndex));
                if (!string.IsNullOrEmpty(issue)) throw new InvalidOperationException(issue);
            }
        }

        private static void EnsurePlayerCamera()
        {
            var path = PlayerFoundationTemplateBuilder.PlayerPrefabPath;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                throw new InvalidOperationException($"Player prefab is missing: {path}");

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var camera = root.GetComponentInChildren<Camera>(true);
                if (camera == null)
                    throw new InvalidOperationException($"Player prefab '{path}' has no Camera.");
                var changed = false;
                var data = camera.GetComponent<UniversalAdditionalCameraData>();
                if (data == null)
                {
                    data = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                    changed = true;
                }
                changed |= camera.allowMSAA || !data.renderPostProcessing;
                changed |= camera.GetComponent<PlayerCameraAntiAliasingApplier>() == null;
                camera.allowMSAA = false;
                data.renderPostProcessing = true;
                if (camera.GetComponent<PlayerCameraAntiAliasingApplier>() == null)
                    camera.gameObject.AddComponent<PlayerCameraAntiAliasingApplier>();
                if (changed)
                    PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
