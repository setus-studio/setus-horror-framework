using System;
using System.Collections.Generic;
using System.Linq;
using Setus.HorrorFramework.Editor.Builders;
using Setus.HorrorFramework.Editor.Localization;
using Setus.HorrorFramework.Localization;
using Setus.HorrorFramework.UI.Menus;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace Setus.HorrorFramework.Editor.Validators
{
    public static class LocalizationRolloutValidator
    {
        public readonly struct GlyphCoverageSource
        {
            private readonly Func<char, bool> hasCharacter;

            public GlyphCoverageSource(string name, Func<char, bool> hasCharacter)
            {
                Name = string.IsNullOrWhiteSpace(name) ? "<unnamed>" : name;
                this.hasCharacter = hasCharacter ?? throw new ArgumentNullException(nameof(hasCharacter));
            }

            public string Name { get; }

            public bool HasCharacter(char character) => hasCharacter(character);
        }

        [MenuItem("Setus/Horror Framework/Validation/Validate Phase 5 Localization Rollout")]
        public static void ValidateFromMenu()
        {
            var issues = ValidateProject();
            foreach (var issue in issues)
            {
                Debug.LogError($"[Settings Phase 5] {issue}");
            }

            if (issues.Count == 0)
            {
                Debug.Log("Settings Phase 5 localization validation passed.");
            }
        }

        public static IReadOnlyList<string> ValidateProject()
        {
            var issues = new List<string>();
            var settings = LocalizationEditorSettings.ActiveLocalizationSettings;
            if (settings == null)
            {
                issues.Add("Owner '<project>', field 'LocalizationSettings': active settings asset is missing.");
                return issues;
            }

            var collection = LocalizationEditorSettings.GetStringTableCollection(
                LocalizedTextReference.DefaultTable);
            if (collection == null)
            {
                issues.Add(
                    $"Owner '<project>', field '{LocalizedTextReference.DefaultTable}': String Table Collection is missing.");
                return issues;
            }

            var profile = AssetDatabase.LoadAssetAtPath<LocaleFontProfile>(
                LocalizationRolloutContract.FontProfilePath);
            if (profile == null)
            {
                issues.Add(
                    $"Owner '{LocalizationRolloutContract.FontProfilePath}', field 'asset': locale font profile is missing.");
            }
            else
            {
                ValidateFontProfileShape(profile, issues);
            }

            var shell = AssetDatabase.LoadAssetAtPath<GameObject>(
                SceneFlowUiShellTemplateBuilder.UiShellPrefabPath);
            var controller = shell != null ? shell.GetComponent<RuntimeLocaleController>() : null;
            if (controller == null)
            {
                issues.Add(
                    $"Owner '{SceneFlowUiShellTemplateBuilder.UiShellPrefabPath}', field 'RuntimeLocaleController': component is missing.");
            }
            else if (controller.FontProfile != profile)
            {
                issues.Add(
                    $"Owner '{SceneFlowUiShellTemplateBuilder.UiShellPrefabPath}', field 'fontProfile': " +
                    $"must reference '{LocalizationRolloutContract.FontProfilePath}'.");
            }

            foreach (var definition in LocalizationRolloutContract.SupportedLocales)
            {
                ValidateLocale(definition, collection, profile, shell, issues);
            }

            return issues;
        }

        public static IReadOnlyList<string> ValidateGlyphCorpus(
            string localeCode,
            string tableName,
            string tablePath,
            IEnumerable<KeyValuePair<string, string>> entries,
            IReadOnlyList<GlyphCoverageSource> fontChain)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            if (fontChain == null)
            {
                throw new ArgumentNullException(nameof(fontChain));
            }

            var issues = new List<string>();
            var chainName = fontChain.Count == 0
                ? "<empty>"
                : string.Join(" -> ", fontChain.Select(source => source.Name));
            var coverage = new Dictionary<char, bool>();
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.Value))
                {
                    continue;
                }

                var checkedCharacters = new HashSet<char>();
                foreach (var character in entry.Value)
                {
                    if (char.IsControl(character) || !checkedCharacters.Add(character))
                    {
                        continue;
                    }

                    if (!coverage.TryGetValue(character, out var hasGlyph))
                    {
                        hasGlyph = fontChain.Any(source => source.HasCharacter(character));
                        coverage.Add(character, hasGlyph);
                    }

                    if (hasGlyph)
                    {
                        continue;
                    }

                    issues.Add(
                        $"Owner '{tablePath}', locale '{localeCode}', table '{tableName}', key '{entry.Key}': " +
                        $"font chain [{chainName}] is missing glyph '{FormatGlyph(character)}' " +
                        $"(U+{(int)character:X4}).");
                }
            }

            return issues;
        }

        private static void ValidateFontProfileShape(
            LocaleFontProfile profile,
            ICollection<string> issues)
        {
            var serialized = new SerializedObject(profile);
            var bindings = serialized.FindProperty("bindings");
            var codes = new List<string>();
            for (var index = 0; index < bindings.arraySize; index++)
            {
                var code = bindings.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("localeCode").stringValue;
                if (string.IsNullOrWhiteSpace(code))
                {
                    issues.Add(
                        $"Owner '{LocalizationRolloutContract.FontProfilePath}', field 'bindings[{index}].localeCode': " +
                        "locale code is empty.");
                }
                else if (!LocalizationRolloutContract.IsTargetLocale(code))
                {
                    issues.Add(
                        $"Owner '{LocalizationRolloutContract.FontProfilePath}', field 'bindings[{index}].localeCode': " +
                        $"unsupported release locale '{code}'.");
                }

                codes.Add(code);
            }

            foreach (var duplicate in codes
                         .Where(code => !string.IsNullOrWhiteSpace(code))
                         .GroupBy(code => code, System.StringComparer.OrdinalIgnoreCase)
                         .Where(group => group.Count() > 1))
            {
                issues.Add(
                    $"Owner '{LocalizationRolloutContract.FontProfilePath}', field 'bindings': " +
                    $"locale '{duplicate.Key}' is configured more than once.");
            }
        }

        private static void ValidateLocale(
            SupportedLocaleDefinition definition,
            StringTableCollection collection,
            LocaleFontProfile profile,
            GameObject shell,
            ICollection<string> issues)
        {
            var locale = LocalizationEditorSettings.GetLocale(definition.Code);
            if (locale == null)
            {
                issues.Add(
                    $"Owner '<project>', field 'AvailableLocales': required locale '{definition.Code}' is missing.");
                return;
            }

            ValidateAssetScope(locale, definition.Code, "Locale", issues);
            var table = collection.GetTable(locale.Identifier) as StringTable;
            var corpus = new List<KeyValuePair<string, string>>();
            if (table == null)
            {
                issues.Add(
                    $"Owner '{LocalizedTextReference.DefaultTable}', field '{definition.Code}': locale table is missing.");
            }
            else
            {
                ValidateAssetScope(table, definition.Code, "StringTable", issues);
                foreach (var sharedEntry in collection.SharedData.Entries)
                {
                    var entry = table.GetEntry(sharedEntry.Id);
                    if (entry == null || string.IsNullOrWhiteSpace(entry.Value))
                    {
                        issues.Add(
                            $"Owner '{AssetDatabase.GetAssetPath(table)}', field '{sharedEntry.Key}': " +
                            $"translation for locale '{definition.Code}' is missing or empty.");
                    }
                    else
                    {
                        corpus.Add(new KeyValuePair<string, string>(sharedEntry.Key, entry.Value));
                    }
                }
            }

            ValidateFont(definition, profile, shell, table, corpus, issues);
        }

        private static void ValidateFont(
            SupportedLocaleDefinition definition,
            LocaleFontProfile profile,
            GameObject shell,
            StringTable table,
            IReadOnlyList<KeyValuePair<string, string>> corpus,
            ICollection<string> issues)
        {
            if (profile == null ||
                !profile.TryGetConfiguration(definition.Code, out var useAuthoredFont, out var fontOverride))
            {
                issues.Add(
                    $"Owner '{LocalizationRolloutContract.FontProfilePath}', field 'bindings[{definition.Code}]': " +
                    "locale font configuration is missing.");
                return;
            }

            var fonts = useAuthoredFont
                ? shell == null
                    ? new Font[0]
                    : shell.GetComponentsInChildren<UnityEngine.UI.Text>(true)
                        .Select(label => label.font)
                        .Where(font => font != null)
                        .Distinct()
                        .ToArray()
                : fontOverride == null ? new Font[0] : new[] { fontOverride };

            if (fonts.Length == 0)
            {
                issues.Add(
                    $"Owner '{LocalizationRolloutContract.FontProfilePath}', field 'bindings[{definition.Code}].fontOverride': " +
                    "a game-owned font is required unless authored UI fonts are explicitly used.");
                return;
            }

            foreach (var font in fonts)
            {
                var fontChain = ResolveFontChain(font);
                var sources = fontChain
                    .Select(candidate => new GlyphCoverageSource(candidate.name, candidate.HasCharacter))
                    .ToArray();
                var tablePath = table != null
                    ? AssetDatabase.GetAssetPath(table)
                    : $"{LocalizedTextReference.DefaultTable}_{definition.Code}";
                var tableName = table != null ? table.name : LocalizedTextReference.DefaultTable;
                foreach (var issue in ValidateGlyphCorpus(
                             definition.Code,
                             tableName,
                             tablePath,
                             corpus,
                             sources))
                {
                    issues.Add(issue);
                }

                var path = AssetDatabase.GetAssetPath(font);
                if (!useAuthoredFont &&
                    !path.StartsWith(LocalizationRolloutContract.FontsPath + "/", System.StringComparison.Ordinal))
                {
                    issues.Add(
                        $"Owner '{LocalizationRolloutContract.FontProfilePath}', field 'bindings[{definition.Code}].fontOverride': " +
                        $"font must be game-owned under '{LocalizationRolloutContract.FontsPath}'.");
                }
            }
        }

        private static IReadOnlyList<Font> ResolveFontChain(Font primary)
        {
            var chain = new List<Font>();
            var visited = new HashSet<int>();
            AddFontAndFallbacks(primary, chain, visited);
            return chain;
        }

        private static void AddFontAndFallbacks(
            Font font,
            ICollection<Font> chain,
            ISet<int> visited)
        {
            if (font == null || !visited.Add(font.GetInstanceID()))
            {
                return;
            }

            chain.Add(font);
            var path = AssetDatabase.GetAssetPath(font);
            var importer = string.IsNullOrWhiteSpace(path) ? null : AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                return;
            }

            var serialized = new SerializedObject(importer);
            var fallbacks = serialized.FindProperty("fallbackFontReferences") ??
                            serialized.FindProperty("m_FallbackFontReferences");
            if (fallbacks == null || !fallbacks.isArray)
            {
                return;
            }

            for (var index = 0; index < fallbacks.arraySize; index++)
            {
                var fallback = fallbacks.GetArrayElementAtIndex(index).objectReferenceValue as Font;
                AddFontAndFallbacks(fallback, chain, visited);
            }
        }

        private static string FormatGlyph(char character)
        {
            return character == '\'' ? "\\'" : character.ToString();
        }

        private static void ValidateAssetScope(
            UnityEngine.Object asset,
            string localeCode,
            string field,
            ICollection<string> issues)
        {
            var path = AssetDatabase.GetAssetPath(asset);
            if (!path.StartsWith(LocalizationRolloutContract.LocalizationRoot + "/", System.StringComparison.Ordinal))
            {
                issues.Add(
                    $"Owner '{path}', field '{field}[{localeCode}]': localization content must be game-owned " +
                    $"under '{LocalizationRolloutContract.LocalizationRoot}'.");
            }
        }
    }
}
