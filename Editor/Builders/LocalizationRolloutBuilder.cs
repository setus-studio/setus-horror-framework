using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Setus.HorrorFramework.Editor.Localization;
using Setus.HorrorFramework.Localization;
using Setus.HorrorFramework.UI.Menus;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace Setus.HorrorFramework.Editor.Builders
{
    public static class LocalizationRolloutBuilder
    {
        [MenuItem("Setus/Horror Framework/Settings/Apply Phase 5 Localization Rollout")]
        public static void ApplyFromMenu()
        {
            Apply();
            Debug.Log(
                "Phase 5 localization rollout applied. Assign game-owned fonts in " +
                $"'{LocalizationRolloutContract.FontProfilePath}', then run Phase 5 localization validation.");
        }

        public static void Apply()
        {
            EnsureFolder(LocalizationRolloutContract.LocalizationRoot);
            EnsureFolder(LocalizationRolloutContract.LocalesPath);
            EnsureFolder(LocalizationRolloutContract.TablesPath);
            EnsureFolder(LocalizationRolloutContract.FontsPath);

            var settings = EnsureLocalizationSettings();
            LocalizationEditorSettings.ActiveLocalizationSettings = settings;
            var locales = EnsureLocales();
            var collection = EnsureStringTableCollection(locales);
            PopulateMissingTranslations(collection, locales);
            var profile = EnsureFontProfile();
            WireUiShell(profile);

            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(collection);
            EditorUtility.SetDirty(collection.SharedData);
            LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null, collection);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static LocalizationSettings EnsureLocalizationSettings()
        {
            var settings = LocalizationEditorSettings.ActiveLocalizationSettings;
            if (settings != null)
            {
                return settings;
            }

            var path = LocalizationRolloutContract.LocalizationRoot + "/GameLocalizationSettings.asset";
            settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(path);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                AssetDatabase.CreateAsset(settings, path);
            }

            return settings;
        }

        private static IReadOnlyDictionary<string, Locale> EnsureLocales()
        {
            var result = new Dictionary<string, Locale>(StringComparer.OrdinalIgnoreCase);
            foreach (var definition in LocalizationRolloutContract.SupportedLocales)
            {
                var locale = LocalizationEditorSettings.GetLocale(definition.Code);
                if (locale == null)
                {
                    var path = $"{LocalizationRolloutContract.LocalesPath}/{definition.AssetName}.asset";
                    locale = AssetDatabase.LoadAssetAtPath<Locale>(path);
                    if (locale == null)
                    {
                        locale = Locale.CreateLocale(definition.Code);
                        locale.name = definition.AssetName;
                        locale.LocaleName = definition.DisplayName;
                        AssetDatabase.CreateAsset(locale, path);
                    }

                    LocalizationEditorSettings.AddLocale(locale);
                }

                result.Add(definition.Code, locale);
            }

            var english = result["en"];
            if (LocalizationSettings.ProjectLocale != english)
            {
                LocalizationSettings.ProjectLocale = english;
            }

            return result;
        }

        private static StringTableCollection EnsureStringTableCollection(
            IReadOnlyDictionary<string, Locale> locales)
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(
                LocalizedTextReference.DefaultTable);
            if (collection == null)
            {
                collection = LocalizationEditorSettings.CreateStringTableCollection(
                    LocalizedTextReference.DefaultTable,
                    LocalizationRolloutContract.TablesPath,
                    locales.Values.ToArray());
            }

            foreach (var definition in LocalizationRolloutContract.SupportedLocales)
            {
                if (collection.GetTable(definition.Code) != null)
                {
                    continue;
                }

                var tablePath =
                    $"{LocalizationRolloutContract.TablesPath}/{LocalizedTextReference.DefaultTable}_{definition.Code}.asset";
                collection.AddNewTable(definition.Code, tablePath);
            }

            return collection;
        }

        private static void PopulateMissingTranslations(
            StringTableCollection collection,
            IReadOnlyDictionary<string, Locale> locales)
        {
            var english = collection.GetTable(locales["en"].Identifier) as StringTable;
            if (english == null)
            {
                throw new InvalidOperationException("The Game Text English table is required before Phase 5 rollout.");
            }

            foreach (var definition in LocalizationRolloutContract.SupportedLocales)
            {
                var table = collection.GetTable(locales[definition.Code].Identifier) as StringTable;
                if (table == null)
                {
                    throw new InvalidOperationException(
                        $"Could not create the Game Text table for locale '{definition.Code}'.");
                }

                foreach (var sharedEntry in collection.SharedData.Entries)
                {
                    var englishValue = english.GetEntry(sharedEntry.Id)?.Value ?? string.Empty;
                    var target = table.GetEntry(sharedEntry.Id);
                    if (target != null && !string.IsNullOrWhiteSpace(target.Value))
                    {
                        continue;
                    }

                    var value = FrameworkTranslationSeed.Get(
                        definition.Code,
                        sharedEntry.Key,
                        englishValue);
                    if (target == null)
                    {
                        table.AddEntry(sharedEntry.Id, value);
                    }
                    else
                    {
                        target.Value = value;
                    }
                }

                EditorUtility.SetDirty(table);
            }
        }

        private static LocaleFontProfile EnsureFontProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<LocaleFontProfile>(
                LocalizationRolloutContract.FontProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<LocaleFontProfile>();
                AssetDatabase.CreateAsset(profile, LocalizationRolloutContract.FontProfilePath);
            }

            var serialized = new SerializedObject(profile);
            var bindings = serialized.FindProperty("bindings");
            var existing = new Dictionary<string, (bool authored, Font font)>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < bindings.arraySize; index++)
            {
                var binding = bindings.GetArrayElementAtIndex(index);
                var code = binding.FindPropertyRelative("localeCode").stringValue;
                if (!string.IsNullOrWhiteSpace(code))
                {
                    existing[code] = (
                        binding.FindPropertyRelative("useAuthoredFont").boolValue,
                        binding.FindPropertyRelative("fontOverride").objectReferenceValue as Font);
                }
            }

            bindings.arraySize = LocalizationRolloutContract.SupportedLocales.Count;
            for (var index = 0; index < LocalizationRolloutContract.SupportedLocales.Count; index++)
            {
                var definition = LocalizationRolloutContract.SupportedLocales[index];
                var binding = bindings.GetArrayElementAtIndex(index);
                binding.FindPropertyRelative("localeCode").stringValue = definition.Code;
                if (existing.TryGetValue(definition.Code, out var current))
                {
                    binding.FindPropertyRelative("useAuthoredFont").boolValue = current.authored;
                    binding.FindPropertyRelative("fontOverride").objectReferenceValue = current.font;
                }
                else
                {
                    binding.FindPropertyRelative("useAuthoredFont").boolValue = definition.Code == "en";
                    binding.FindPropertyRelative("fontOverride").objectReferenceValue = null;
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static void WireUiShell(LocaleFontProfile profile)
        {
            var prefabPath = SceneFlowUiShellTemplateBuilder.UiShellPrefabPath;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var controller = root.GetComponent<RuntimeLocaleController>();
                if (controller == null)
                {
                    throw new InvalidOperationException(
                        $"Canonical UI shell '{prefabPath}' requires RuntimeLocaleController.");
                }

                var serialized = new SerializedObject(controller);
                var property = serialized.FindProperty("fontProfile");
                if (property.objectReferenceValue != profile)
                {
                    property.objectReferenceValue = profile;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
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
