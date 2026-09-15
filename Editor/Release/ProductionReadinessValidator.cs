using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Setus.HorrorFramework.AI.Navigation;
using Setus.HorrorFramework.Core.Config;
using Setus.HorrorFramework.Debugging.Logging;
using Setus.HorrorFramework.Editor.Builders;
using Setus.HorrorFramework.Editor.ContentPipeline;
using Setus.HorrorFramework.Editor.Validators;
using Setus.HorrorFramework.Interaction.Interactors;
using Setus.HorrorFramework.SaveProgression.StableIds;
using Setus.HorrorFramework.SceneFlow.Loading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Setus.HorrorFramework.Editor.Release
{
    public static class ProductionReadinessValidator
    {
        public static FrameworkSceneValidationResult Validate(
            HorrorBuildProfile profile,
            HorrorPerformanceBudget budget,
            Scene loadedScene = default)
        {
            var issues = new List<FrameworkSceneValidationIssue>();
            ValidateProfile(profile, issues);
            var gameConfig = ValidateGameConfig(issues);
            var enabledScenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).ToArray();
            ValidateBuildScenes(enabledScenes, gameConfig, issues);
            ValidateProductionConfig(profile, issues);
            Append(issues, ContentAssetValidator.Validate("Assets/Game"));

            foreach (var message in AccessibilityContentValidator.ValidateProjectAssets())
            {
                issues.Add(Error("release.accessibility.invalid", message));
            }

            if (budget == null)
            {
                budget = AssetDatabase.LoadAssetAtPath<HorrorPerformanceBudget>(
                    ProductionHardeningBuilder.PerformanceBudgetPath);
            }
            if (budget == null)
            {
                issues.Add(Error("release.performance-budget.missing", "A HorrorPerformanceBudget asset is required."));
            }

            ValidateEnabledScenes(enabledScenes, gameConfig, budget, loadedScene, issues);

            return new FrameworkSceneValidationResult(issues);
        }

        public static FrameworkSceneValidationResult ValidateEnabledScenes(
            StandaloneGameConfig gameConfig,
            HorrorPerformanceBudget budget,
            Scene loadedScene = default)
        {
            var issues = new List<FrameworkSceneValidationIssue>();
            var enabledScenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).ToArray();
            ValidateBuildScenes(enabledScenes, gameConfig, issues);
            if (budget == null)
            {
                issues.Add(Error("release.performance-budget.missing", "A HorrorPerformanceBudget asset is required."));
            }
            ValidateEnabledScenes(enabledScenes, gameConfig, budget, loadedScene, issues);
            return new FrameworkSceneValidationResult(issues);
        }

        public static FrameworkSceneValidationResult ValidateProfile(HorrorBuildProfile profile)
        {
            var issues = new List<FrameworkSceneValidationIssue>();
            ValidateProfile(profile, issues);
            return new FrameworkSceneValidationResult(issues);
        }

        private static void ValidateProfile(
            HorrorBuildProfile profile,
            ICollection<FrameworkSceneValidationIssue> issues)
        {
            if (profile == null)
            {
                issues.Add(Error("release.profile.missing", "A HorrorBuildProfile is required."));
                return;
            }

            if (string.IsNullOrWhiteSpace(profile.ProfileId))
            {
                issues.Add(Error("release.profile.id-empty", "HorrorBuildProfile.ProfileId must not be empty.", profile));
            }

            if (string.IsNullOrWhiteSpace(profile.OutputPath) || Path.IsPathRooted(profile.OutputPath))
            {
                issues.Add(Error(
                    "release.profile.output-invalid",
                    "HorrorBuildProfile.OutputPath must be a non-empty project-relative path.",
                    profile));
            }

            if (profile.StrictValidation && !profile.IsProduction &&
                string.Equals(profile.ProfileId, "production", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(Error(
                    "release.profile.production-debug",
                    "The production profile cannot enable Development Build, Script Debugging, or Profiler connection.",
                    profile));
            }
        }

        private static void ValidateBuildScenes(
            IReadOnlyList<EditorBuildSettingsScene> enabled,
            StandaloneGameConfig gameConfig,
            ICollection<FrameworkSceneValidationIssue> issues)
        {
            if (enabled.Count == 0)
            {
                issues.Add(Error("release.build-scenes.empty", "At least one enabled Build Settings scene is required."));
                return;
            }

            for (var i = 0; i < enabled.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(enabled[i].path) || !File.Exists(enabled[i].path))
                {
                    issues.Add(Error(
                        "release.build-scenes.missing",
                        $"Enabled build scene '{enabled[i].path}' does not exist."));
                }
            }

            if (gameConfig == null)
            {
                return;
            }

            var requiredNames = new[]
            {
                gameConfig.BootSceneName,
                gameConfig.MainMenuSceneName,
                gameConfig.FirstGameplaySceneName
            };
            for (var i = 0; i < requiredNames.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(requiredNames[i]))
                {
                    issues.Add(Error("release.build-scenes.name-empty", "Configured build scene names must not be empty."));
                    continue;
                }

                var count = enabled.Count(scene => SceneName(scene.path) == requiredNames[i]);
                if (count != 1)
                {
                    issues.Add(Error(
                        "release.build-scenes.configured-scene",
                        $"Configured scene '{requiredNames[i]}' must resolve to exactly one enabled build scene; found {count}."));
                }
            }

            if (!string.Equals(SceneName(enabled[0].path), gameConfig.BootSceneName, StringComparison.Ordinal))
            {
                issues.Add(Error(
                    "release.build-scenes.boot-order",
                    $"Configured Boot scene '{gameConfig.BootSceneName}' must be the first enabled Build Settings scene."));
            }
        }

        private static void ValidateProductionConfig(
            HorrorBuildProfile profile,
            ICollection<FrameworkSceneValidationIssue> issues)
        {
            if (profile == null || !profile.IsProduction)
            {
                return;
            }

            var config = AssetDatabase.LoadAssetAtPath<HorrorFrameworkConfig>(
                ProductionHardeningBuilder.FrameworkConfigPath);
            if (config == null)
            {
                issues.Add(Error("release.logging.config-missing", "The canonical HorrorFrameworkConfig is missing."));
                return;
            }

            if (config.DebugEnabledByDefault)
            {
                issues.Add(Error("release.debug.enabled", "Debug overlay must be disabled by default for production.", config));
            }

            if (config.EnabledLogCategories != HorrorLogCategory.None)
            {
                issues.Add(Error(
                    "release.logging.enabled",
                    "Production logging categories must be None; runtime errors still use explicit fail-safe diagnostics.",
                    config));
            }
        }

        private static StandaloneGameConfig ValidateGameConfig(ICollection<FrameworkSceneValidationIssue> issues)
        {
            var config = AssetDatabase.LoadAssetAtPath<StandaloneGameConfig>(
                SceneFlowUiShellTemplateBuilder.ConfigPath);
            if (config == null || config.StableIdManifest == null)
            {
                issues.Add(Error(
                    "release.game-config.invalid",
                    "StandaloneGameConfig and its StableIdManifest are required for a production build.",
                    config));
            }
            return config;
        }

        private static void ValidateEnabledScenes(
            IReadOnlyList<EditorBuildSettingsScene> enabledScenes,
            StandaloneGameConfig gameConfig,
            HorrorPerformanceBudget budget,
            Scene loadedScene,
            ICollection<FrameworkSceneValidationIssue> issues)
        {
            if (gameConfig == null)
            {
                return;
            }

            foreach (var buildScene in enabledScenes)
            {
                var path = buildScene.path;
                if (!File.Exists(path))
                {
                    continue;
                }

                Scene preview = default;
                var scene = ResolveLoadedScene(path, loadedScene);
                try
                {
                    if (!scene.IsValid() || !scene.isLoaded)
                    {
                        preview = EditorSceneManager.OpenPreviewScene(path);
                        scene = preview;
                    }

                    var isProductionGameScene = !string.Equals(
                                                    scene.name,
                                                    gameConfig.BootSceneName,
                                                    StringComparison.Ordinal) &&
                                                !string.Equals(
                                                    scene.name,
                                                    gameConfig.MainMenuSceneName,
                                                    StringComparison.Ordinal);
                    var manifest = isProductionGameScene ? gameConfig.StableIdManifest : null;
                    AppendSceneIssues(issues, FrameworkSceneValidator.ValidateScene(scene, manifest), path);

                    if (!isProductionGameScene)
                    {
                        continue;
                    }

                    ValidateAuthoredBudgets(scene, budget, issues);
                    foreach (var message in AccessibilityContentValidator.ValidateGameplayReferences(
                                 scene.GetRootGameObjects()))
                    {
                        issues.Add(Error(
                            "release.accessibility.invalid",
                            $"Scene '{path}': {message}"));
                    }
                    if (ContainsM12Sample(scene))
                    {
                        var sampleIssues = FirstPersonHorrorTemplateBuilder.ValidateSampleContract(scene);
                        if (sampleIssues.Count > 0)
                        {
                            issues.Add(Error(
                                "release.sample.invalid",
                                $"Optional M12 sample in scene '{path}' failed validation:\n" +
                                string.Join("\n", sampleIssues.Select(issue => "- " + issue))));
                        }
                    }
                }
                catch (Exception exception)
                {
                    issues.Add(Error(
                        "release.scene.inspect-failed",
                        $"Scene '{path}' could not be inspected: {exception.Message}"));
                }
                finally
                {
                    if (preview.IsValid())
                    {
                        EditorSceneManager.ClosePreviewScene(preview);
                    }
                }
            }
        }

        private static Scene ResolveLoadedScene(string path, Scene suppliedScene)
        {
            if (suppliedScene.IsValid() && suppliedScene.isLoaded &&
                string.Equals(suppliedScene.path, path, StringComparison.Ordinal))
            {
                return suppliedScene;
            }

            var loaded = SceneManager.GetSceneByPath(path);
            return loaded.IsValid() && loaded.isLoaded ? loaded : default;
        }

        private static bool ContainsM12Sample(Scene scene)
        {
            return scene.GetRootGameObjects().Any(root => string.Equals(
                root.name,
                FirstPersonHorrorTemplateBuilder.TemplateRootName,
                StringComparison.Ordinal));
        }

        private static string SceneName(string path)
        {
            return Path.GetFileNameWithoutExtension(path) ?? string.Empty;
        }

        private static void ValidateAuthoredBudgets(
            Scene scene,
            HorrorPerformanceBudget budget,
            ICollection<FrameworkSceneValidationIssue> issues)
        {
            if (budget == null)
            {
                return;
            }

            var components = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Component>(true))
                .Where(component => component != null)
                .ToArray();
            WarnOver(issues, "raycast-sources", components.Count(component => component is RaycastInteractor || component is StalkerAiController), budget.MaximumRaycastSources, budget);
            WarnOver(issues, "audio-sources", components.OfType<AudioSource>().Count(), budget.MaximumActiveAudioSources, budget);
            WarnOver(issues, "realtime-lights", components.OfType<Light>().Count(light => light.lightmapBakeType == LightmapBakeType.Realtime), budget.MaximumRealtimeLights, budget);
            WarnOver(issues, "global-volumes", components.OfType<Volume>().Count(volume => volume.isGlobal), budget.MaximumGlobalVolumes, budget);
        }

        private static void WarnOver(
            ICollection<FrameworkSceneValidationIssue> issues,
            string field,
            int actual,
            int maximum,
            UnityEngine.Object owner)
        {
            if (actual > maximum)
            {
                issues.Add(new FrameworkSceneValidationIssue(
                    FrameworkSceneValidationSeverity.Warning,
                    $"release.performance-budget.{field}",
                    $"Authored count {actual} exceeds budget {maximum}; profile the standalone player before release.",
                    owner));
            }
        }

        private static void Append(
            ICollection<FrameworkSceneValidationIssue> destination,
            FrameworkSceneValidationResult source)
        {
            foreach (var issue in source.Issues)
            {
                destination.Add(issue);
            }
        }

        private static void AppendSceneIssues(
            ICollection<FrameworkSceneValidationIssue> destination,
            FrameworkSceneValidationResult source,
            string scenePath)
        {
            foreach (var issue in source.Issues)
            {
                var ownerName = issue.Owner != null ? issue.Owner.name : "<scene>";
                destination.Add(new FrameworkSceneValidationIssue(
                    issue.Severity,
                    issue.Code,
                    $"Scene '{scenePath}', object '{ownerName}': {issue.Message}"));
            }
        }

        private static FrameworkSceneValidationIssue Error(
            string code,
            string message,
            UnityEngine.Object owner = null)
        {
            return new FrameworkSceneValidationIssue(FrameworkSceneValidationSeverity.Error, code, message, owner);
        }
    }
}
