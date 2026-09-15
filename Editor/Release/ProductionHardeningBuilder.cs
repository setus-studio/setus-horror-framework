using System;
using System.IO;
using Setus.HorrorFramework.Core.Config;
using Setus.HorrorFramework.Debugging.Logging;
using UnityEditor;
using UnityEngine;

namespace Setus.HorrorFramework.Editor.Release
{
    public static class ProductionHardeningBuilder
    {
        public const string SettingsRoot = "Assets/Game/Settings/Build";
        public const string ProductionProfilePath = SettingsRoot + "/Production.asset";
        public const string DevelopmentProfilePath = SettingsRoot + "/Development.asset";
        public const string PerformanceBudgetPath = SettingsRoot + "/PerformanceBudget.asset";
        public const string FrameworkConfigPath = "Assets/Game/Settings/HorrorFrameworkConfig.asset";

        [MenuItem("Setus/Horror Framework/Release/Create Missing M13 Defaults")]
        public static void CreateMissingDefaultsFromMenu()
        {
            CreateMissingDefaults();
            Debug.Log("Missing M13 production profiles, performance budget, and logging defaults are ready.");
        }

        public static void ApplyFromMenu()
        {
            CreateMissingDefaultsFromMenu();
        }

        // Retained for editor API compatibility. Apply now has non-destructive create-missing semantics.
        public static void Apply()
        {
            CreateMissingDefaults();
        }

        public static void CreateMissingDefaults()
        {
            CreateMissingDefaultsAtPaths(
                ProductionProfilePath,
                DevelopmentProfilePath,
                PerformanceBudgetPath,
                FrameworkConfigPath);
        }

        public static void CreateMissingDefaultsAtPaths(
            string productionProfilePath,
            string developmentProfilePath,
            string performanceBudgetPath,
            string frameworkConfigPath)
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            var changed = false;

            var production = EnsureAsset<HorrorBuildProfile>(productionProfilePath, out var createdProduction);
            if (createdProduction)
            {
                ConfigureProfile(
                    production,
                    "production",
                    target,
                    DefaultOutputPath(target, "Production"),
                    false,
                    false,
                    false,
                    true);
                changed = true;
            }

            var development = EnsureAsset<HorrorBuildProfile>(developmentProfilePath, out var createdDevelopment);
            if (createdDevelopment)
            {
                ConfigureProfile(
                    development,
                    "development",
                    target,
                    DefaultOutputPath(target, "Development"),
                    true,
                    true,
                    false,
                    true);
                changed = true;
            }

            var budget = EnsureAsset<HorrorPerformanceBudget>(performanceBudgetPath, out var createdBudget);
            if (createdBudget)
            {
                budget.Configure(60, 0, 16, 32, 8, 4);
                EditorUtility.SetDirty(budget);
                changed = true;
            }

            var config = EnsureAsset<HorrorFrameworkConfig>(frameworkConfigPath, out var createdConfig);
            if (createdConfig)
            {
                var serialized = new SerializedObject(config);
                serialized.FindProperty("debugEnabledByDefault").boolValue = false;
                serialized.FindProperty("enabledLogCategories").intValue = (int)HorrorLogCategory.None;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(config);
                changed = true;
            }

            if (changed)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
        }

        public static string DefaultOutputPath(BuildTarget target, string configuration)
        {
            string fileName;
            switch (target)
            {
                case BuildTarget.StandaloneOSX:
                    fileName = "SetusHorrorGame.app";
                    break;
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    fileName = "SetusHorrorGame.exe";
                    break;
                default:
                    fileName = "SetusHorrorGame";
                    break;
            }
            return $"Builds/{configuration}/{fileName}";
        }

        private static void ConfigureProfile(
            HorrorBuildProfile profile,
            string id,
            BuildTarget target,
            string output,
            bool development,
            bool debugging,
            bool profiler,
            bool strict)
        {
            profile.Configure(id, target, output, development, debugging, profiler, strict);
            EditorUtility.SetDirty(profile);
        }

        private static T EnsureAsset<T>(string path, out bool created) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                created = false;
                return asset;
            }

            var existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing != null)
            {
                throw new InvalidOperationException(
                    $"Cannot create {typeof(T).Name} at '{path}' because it contains " +
                    $"an asset of type {existing.GetType().Name}.");
            }

            EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            created = true;
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Asset folder path must not be empty.", nameof(path));
            }

            var segments = path.Split('/');
            var current = segments[0];
            for (var i = 1; i < segments.Length; i++)
            {
                var next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }
                current = next;
            }
        }
    }
}
