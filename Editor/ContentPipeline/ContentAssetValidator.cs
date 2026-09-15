using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Setus.HorrorFramework.AI.Memory;
using Setus.HorrorFramework.AI.Navigation;
using Setus.HorrorFramework.Atmosphere.Scares;
using Setus.HorrorFramework.Editor.Validators;
using Setus.HorrorFramework.Interaction.Interactables;
using Setus.HorrorFramework.Interaction.Triggers;
using Setus.HorrorFramework.Narrative.Notes;
using Setus.HorrorFramework.Narrative.Objectives;
using Setus.HorrorFramework.Narrative.Phone;
using Setus.HorrorFramework.Narrative.Scenario;
using Setus.HorrorFramework.SaveProgression.StableIds;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Setus.HorrorFramework.Editor.ContentPipeline
{
    public static class ContentAssetValidator
    {
        public static FrameworkSceneValidationResult Validate(string gameRoot)
        {
            var issues = new List<FrameworkSceneValidationIssue>();
            try
            {
                gameRoot = StandaloneGameContentTemplate.NormalizeAndValidateRoot(gameRoot);
            }
            catch (ArgumentException exception)
            {
                issues.Add(Error("content.root.invalid", exception.Message));
                return new FrameworkSceneValidationResult(issues);
            }

            if (!AssetDatabase.IsValidFolder(gameRoot))
            {
                issues.Add(Error(
                    "content.root.missing",
                    $"Standalone game content root '{gameRoot}' does not exist. Run the folder-template command."));
                return new FrameworkSceneValidationResult(issues);
            }

            ValidateFolderTemplate(gameRoot, issues);
            ValidateAudio(gameRoot, issues);
            ValidatePrefabs(gameRoot, issues);
            ValidateDefinitions(gameRoot, issues);
            ValidateTextures(gameRoot, issues);
            ValidateModels(gameRoot, issues);
            return new FrameworkSceneValidationResult(issues);
        }

        public static void LogResult(string gameRoot, FrameworkSceneValidationResult result)
        {
            for (var i = 0; i < result.Issues.Count; i++)
            {
                var issue = result.Issues[i];
                if (issue.IsError)
                {
                    Debug.LogError(issue.ToString(), issue.Owner);
                }
                else
                {
                    Debug.LogWarning(issue.ToString(), issue.Owner);
                }
            }

            Debug.Log(
                result.Issues.Count == 0
                    ? $"Content pipeline validation passed for '{gameRoot}'."
                    : $"Content pipeline validation finished for '{gameRoot}': " +
                      $"{result.ErrorCount} error(s), {result.WarningCount} warning(s).");
        }

        private static void ValidateFolderTemplate(
            string gameRoot,
            ICollection<FrameworkSceneValidationIssue> issues)
        {
            foreach (var relativePath in StandaloneGameContentTemplate.RequiredRelativeFolders)
            {
                var path = $"{gameRoot}/{relativePath}";
                if (!AssetDatabase.IsValidFolder(path))
                {
                    issues.Add(Error(
                        "content.folder.missing",
                        $"Required standalone-game folder is missing: '{path}'."));
                }
            }
        }

        private static void ValidateAudio(
            string gameRoot,
            ICollection<FrameworkSceneValidationIssue> issues)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { gameRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!HorrorAudioImportRules.IsUnderGameAudioRoot(path))
                {
                    continue;
                }

                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (!HorrorAudioImportRules.TryResolve(path, out var rule))
                {
                    issues.Add(Error(
                        "content.audio.category.invalid",
                        $"Audio clip '{path}' is not inside a supported Audio category folder.",
                        clip));
                    continue;
                }

                if (!(AssetImporter.GetAtPath(path) is AudioImporter importer))
                {
                    issues.Add(Error(
                        "content.audio.importer.missing",
                        $"Audio clip '{path}' has no AudioImporter.",
                        clip));
                    continue;
                }

                ValidateAudioImporter(path, clip, importer, rule, issues);
                if (string.Equals(Path.GetExtension(path), ".mp3", StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(Warning(
                        "content.audio.lossy-source",
                        $"Audio source '{path}' is MP3 and will be re-encoded by Unity. Prefer WAV or AIFF masters.",
                        clip));
                }
            }
        }

        private static void ValidateAudioImporter(
            string path,
            AudioClip clip,
            AudioImporter importer,
            HorrorAudioImportRule rule,
            ICollection<FrameworkSceneValidationIssue> issues)
        {
            var settings = importer.defaultSampleSettings;
            AddAudioMismatch(
                settings.loadType == rule.LoadType,
                path,
                "loadType",
                rule.LoadType,
                settings.loadType,
                clip,
                issues);
            AddAudioMismatch(
                settings.compressionFormat == rule.CompressionFormat,
                path,
                "compressionFormat",
                rule.CompressionFormat,
                settings.compressionFormat,
                clip,
                issues);
            AddAudioMismatch(
                importer.forceToMono == rule.ForceToMono,
                path,
                "forceToMono",
                rule.ForceToMono,
                importer.forceToMono,
                clip,
                issues);
            AddAudioMismatch(
                importer.loadInBackground == rule.LoadInBackground,
                path,
                "loadInBackground",
                rule.LoadInBackground,
                importer.loadInBackground,
                clip,
                issues);
            AddAudioMismatch(
                settings.preloadAudioData == rule.PreloadAudioData,
                path,
                "preloadAudioData",
                rule.PreloadAudioData,
                settings.preloadAudioData,
                clip,
                issues);
            AddAudioMismatch(
                settings.sampleRateSetting == AudioSampleRateSetting.OptimizeSampleRate,
                path,
                "sampleRateSetting",
                AudioSampleRateSetting.OptimizeSampleRate,
                settings.sampleRateSetting,
                clip,
                issues);

            if (rule.CompressionFormat == AudioCompressionFormat.Vorbis)
            {
                AddAudioMismatch(
                    Mathf.Abs(settings.quality - rule.Quality) < 0.001f,
                    path,
                    "quality",
                    rule.Quality,
                    settings.quality,
                    clip,
                    issues);
            }
        }

        private static void AddAudioMismatch(
            bool matches,
            string path,
            string field,
            object expected,
            object actual,
            AudioClip owner,
            ICollection<FrameworkSceneValidationIssue> issues)
        {
            if (!matches)
            {
                issues.Add(Error(
                    "content.audio.import-setting",
                    $"Audio clip '{path}' field '{field}' must be '{expected}' but is '{actual}'. Reimport the asset.",
                    owner));
            }
        }

        private static void ValidatePrefabs(
            string gameRoot,
            ICollection<FrameworkSceneValidationIssue> issues)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { gameRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                GameObject root = null;
                try
                {
                    root = PrefabUtility.LoadPrefabContents(path);
                    ValidatePrefabStableIds(path, prefabAsset, root, issues);
                    ValidatePrefabComponents(path, prefabAsset, root, issues);
                }
                catch (Exception exception)
                {
                    issues.Add(Error(
                        "content.prefab.load-failed",
                        $"Prefab '{path}' could not be inspected: {exception.Message}",
                        prefabAsset));
                }
                finally
                {
                    if (root != null)
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }
            }
        }

        private static void ValidatePrefabStableIds(
            string path,
            GameObject prefabAsset,
            GameObject root,
            ICollection<FrameworkSceneValidationIssue> issues)
        {
            var result = StableIdValidator.Validate(
                root.GetComponentsInChildren<StableId>(true).Cast<IStableIdProvider>(),
                null);
            for (var i = 0; i < result.Issues.Count; i++)
            {
                var source = result.Issues[i];
                var ownerName = source.Providers.FirstOrDefault()?.Owner?.name ?? root.name;
                issues.Add(new FrameworkSceneValidationIssue(
                    source.IsError
                        ? FrameworkSceneValidationSeverity.Error
                        : FrameworkSceneValidationSeverity.Warning,
                    $"content.prefab.stable-id.{source.IssueType}",
                    $"Prefab '{path}', object '{ownerName}' failed {source.IssueType} validation for " +
                    $"StableId '{DisplayId(source.StableId)}'.",
                    prefabAsset));
            }
        }

        private static void ValidatePrefabComponents(
            string path,
            GameObject prefabAsset,
            GameObject root,
            ICollection<FrameworkSceneValidationIssue> issues)
        {
            foreach (var interactable in root.GetComponentsInChildren<PersistentInteractableBase>(true))
            {
                RequireAuthoredStableId(path, prefabAsset, interactable.gameObject, issues);
                if (interactable.GetComponentInChildren<Collider>(true) == null)
                {
                    issues.Add(Error(
                        "content.prefab.interactable.collider-missing",
                        $"Prefab '{path}', object '{interactable.name}' requires a Collider.",
                        prefabAsset));
                }
            }

            foreach (var trigger in root.GetComponentsInChildren<InteractionTriggerZone>(true))
            {
                RequireAuthoredStableId(path, prefabAsset, trigger.gameObject, issues);
                var collider = trigger.GetComponent<Collider>();
                if (collider == null || !collider.isTrigger)
                {
                    issues.Add(Error(
                        "content.prefab.trigger.collider-invalid",
                        $"Prefab '{path}', object '{trigger.name}' requires a trigger Collider on the same object.",
                        prefabAsset));
                }
            }

            foreach (var controller in root.GetComponentsInChildren<StalkerAiController>(true))
            {
                RequireAuthoredStableId(path, prefabAsset, controller.gameObject, issues);
                if (controller.GetComponent<NavMeshAgent>() == null)
                {
                    issues.Add(Error(
                        "content.prefab.ai.navmesh-agent-missing",
                        $"Prefab '{path}', object '{controller.name}' requires NavMeshAgent.",
                        prefabAsset));
                }

                if (controller.GetComponent<StalkerAiSaveAdapter>() == null)
                {
                    issues.Add(Error(
                        "content.prefab.ai.save-adapter-missing",
                        $"Prefab '{path}', object '{controller.name}' requires StalkerAiSaveAdapter.",
                        prefabAsset));
                }

                if (controller.TuningProfile == null)
                {
                    issues.Add(Error(
                        "content.prefab.ai.profile-missing",
                        $"Prefab '{path}', object '{controller.name}' requires StalkerAiTuningProfile.",
                        prefabAsset));
                }
            }
        }

        private static void RequireAuthoredStableId(
            string path,
            GameObject prefabAsset,
            GameObject owner,
            ICollection<FrameworkSceneValidationIssue> issues)
        {
            var stableId = owner.GetComponent<StableId>();
            if (stableId == null)
            {
                issues.Add(Error(
                    "content.prefab.stable-id.required",
                    $"Prefab '{path}', object '{owner.name}' owns persistent state and requires an authored StableId.",
                    prefabAsset));
            }
        }

        private static void ValidateDefinitions(
            string gameRoot,
            ICollection<FrameworkSceneValidationIssue> issues)
        {
            ValidateDefinitionIds<ObjectiveDefinition>(
                gameRoot,
                "objective",
                definition => definition.ObjectiveId,
                issues);
            ValidateDefinitionIds<PhoneMessageDefinition>(
                gameRoot,
                "message",
                definition => definition.MessageId,
                issues);
            ValidateDefinitionIds<NarrativeNoteDefinition>(
                gameRoot,
                "note",
                definition => definition.NoteId,
                issues);
            ValidateDefinitionIds<ScareDefinition>(
                gameRoot,
                "scare",
                definition => definition.ScareId,
                issues);
            ValidateDefinitionIds<NarrativeScenarioDefinition>(
                gameRoot,
                "scenario",
                definition => definition.ScenarioId,
                issues);

            foreach (var guid in AssetDatabase.FindAssets(
                         $"t:{nameof(NarrativeScenarioDefinition)}",
                         new[] { gameRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var scenario = AssetDatabase.LoadAssetAtPath<NarrativeScenarioDefinition>(path);
                if (scenario == null)
                {
                    continue;
                }

                var graph = scenario.ValidateObjectiveGraph();
                if (!graph.IsValid)
                {
                    issues.Add(Error(
                        "content.scenario.objective-graph-invalid",
                        $"Scenario asset '{path}' has an invalid objective graph: " +
                        graph.ToDiagnostic(scenario.ScenarioId),
                        scenario));
                }
            }
        }

        private static void ValidateDefinitionIds<T>(
            string gameRoot,
            string kind,
            Func<T, string> idSelector,
            ICollection<FrameworkSceneValidationIssue> issues)
            where T : UnityEngine.Object
        {
            var byId = new Dictionary<string, T>(StringComparer.Ordinal);
            foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { gameRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset == null)
                {
                    continue;
                }

                var id = idSelector(asset);
                if (string.IsNullOrWhiteSpace(id))
                {
                    issues.Add(Error(
                        $"content.{kind}-id.empty",
                        $"{typeof(T).Name} asset '{path}' requires a non-empty {kind} ID.",
                        asset));
                }
                else if (!HorrorContentNaming.IsValidId(id))
                {
                    issues.Add(Error(
                        $"content.{kind}-id.format",
                        $"{typeof(T).Name} asset '{path}' uses invalid ID '{id}'. " +
                        "Use lowercase dot-separated identifiers with optional '-' or '_' inside segments.",
                        asset));
                }
                else if (byId.TryGetValue(id, out var existing))
                {
                    issues.Add(Error(
                        $"content.{kind}-id.duplicate",
                        $"{typeof(T).Name} assets '{AssetDatabase.GetAssetPath(existing)}' and '{path}' " +
                        $"share ID '{id}'.",
                        asset));
                }
                else
                {
                    byId.Add(id, asset);
                }
            }
        }

        private static void ValidateTextures(
            string gameRoot,
            ICollection<FrameworkSceneValidationIssue> issues)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { gameRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                {
                    continue;
                }

                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (importer.isReadable)
                {
                    issues.Add(Warning(
                        "content.texture.read-write-enabled",
                        $"Texture '{path}' has Read/Write enabled. Disable it unless runtime CPU access is required.",
                        texture));
                }

                if (importer.maxTextureSize > 4096)
                {
                    issues.Add(Warning(
                        "content.texture.max-size",
                        $"Texture '{path}' allows max size {importer.maxTextureSize}. Verify that more than 4096 is necessary.",
                        texture));
                }

                if (importer.textureType != TextureImporterType.Sprite && !importer.mipmapEnabled)
                {
                    issues.Add(Warning(
                        "content.texture.mipmaps-disabled",
                        $"World texture '{path}' has mipmaps disabled. Confirm this is intentional.",
                        texture));
                }
            }
        }

        private static void ValidateModels(
            string gameRoot,
            ICollection<FrameworkSceneValidationIssue> issues)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { gameRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is ModelImporter importer && importer.isReadable)
                {
                    issues.Add(Warning(
                        "content.model.read-write-enabled",
                        $"Model '{path}' has Read/Write enabled. Disable it unless runtime mesh access is required.",
                        AssetDatabase.LoadAssetAtPath<GameObject>(path)));
                }
            }
        }

        private static string DisplayId(string id)
        {
            return string.IsNullOrWhiteSpace(id) ? "<empty>" : id;
        }

        private static FrameworkSceneValidationIssue Error(
            string code,
            string message,
            UnityEngine.Object owner = null)
        {
            return new FrameworkSceneValidationIssue(
                FrameworkSceneValidationSeverity.Error,
                code,
                message,
                owner);
        }

        private static FrameworkSceneValidationIssue Warning(
            string code,
            string message,
            UnityEngine.Object owner = null)
        {
            return new FrameworkSceneValidationIssue(
                FrameworkSceneValidationSeverity.Warning,
                code,
                message,
                owner);
        }
    }

    public static class HorrorContentNaming
    {
        public static bool IsValidId(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || !string.Equals(id, id.Trim(), StringComparison.Ordinal))
            {
                return false;
            }

            var previousWasSeparator = true;
            for (var i = 0; i < id.Length; i++)
            {
                var character = id[i];
                var isAlphaNumeric = (character >= 'a' && character <= 'z') ||
                                     (character >= '0' && character <= '9');
                var isSeparator = character == '.' || character == '-' || character == '_';
                if ((!isAlphaNumeric && !isSeparator) || (isSeparator && previousWasSeparator))
                {
                    return false;
                }

                previousWasSeparator = isSeparator;
            }

            return !previousWasSeparator;
        }
    }
}
