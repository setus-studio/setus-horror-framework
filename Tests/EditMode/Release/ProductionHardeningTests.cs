using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Setus.HorrorFramework.Accessibility.Display;
using Setus.HorrorFramework.Editor.Builders;
using Setus.HorrorFramework.Editor.Release;
using Setus.HorrorFramework.Editor.Validators;
using Setus.HorrorFramework.Interaction.Triggers;
using Setus.HorrorFramework.Narrative.Scenario;
using Setus.HorrorFramework.SaveProgression.StableIds;
using Setus.HorrorFramework.SceneFlow.Loading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Setus.HorrorFramework.Tests.EditMode.Release
{
    public sealed class ProductionHardeningTests
    {
        private const string TestRoot = "Assets/Game/__SetusHorrorFrameworkTests/FWAudit05";
        private string testRunRoot;

        [SetUp]
        public void SetUp()
        {
            Setus.HorrorFramework.Tests.EditMode.Authoring.AuthoringTestAssetCleanup.Delete(TestRoot);
            testRunRoot = TestRoot + "/" + Guid.NewGuid().ToString("N");
        }

        [TearDown]
        public void TearDown()
        {
            Setus.HorrorFramework.Tests.EditMode.Authoring.AuthoringTestAssetCleanup.Delete(TestRoot);
            testRunRoot = null;
        }

        [Test]
        public void ProductionProfileRejectsDebugBuildOptions()
        {
            var profile = ScriptableObject.CreateInstance<HorrorBuildProfile>();
            try
            {
                profile.Configure("production", BuildTarget.StandaloneOSX, "Builds/Game.app", true, true, true, true);
                var result = ProductionReadinessValidator.ValidateProfile(profile);
                Assert.That(result.IsValid, Is.False);
                Assert.That(result.Issues.Any(issue => issue.Code == "release.profile.production-debug"), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ProductionProfileWithProjectRelativeOutputPassesProfileValidation()
        {
            var profile = ScriptableObject.CreateInstance<HorrorBuildProfile>();
            try
            {
                profile.Configure("production", BuildTarget.StandaloneOSX, "Builds/Production/Game.app", false, false, false, true);
                Assert.That(ProductionReadinessValidator.ValidateProfile(profile).IsValid, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void CommandLineArgumentsResolveExplicitProfileAndOutput()
        {
            var arguments = new[]
            {
                "Unity", "-batchmode", "-horrorBuildProfile", "Assets/Profile.asset",
                "-horrorBuildOutput", "Builds/Smoke/Game.app"
            };
            Assert.That(HorrorBuildCommand.ReadArgument(arguments, "-horrorBuildProfile"), Is.EqualTo("Assets/Profile.asset"));
            Assert.That(HorrorBuildCommand.ReadArgument(arguments, "-horrorBuildOutput"), Is.EqualTo("Builds/Smoke/Game.app"));
        }

        [Test]
        public void MissingDefaultsAreCreatedAndAuthoredValuesRemainStableAcrossRepeatedCreation()
        {
            Setus.HorrorFramework.Tests.EditMode.Authoring.AuthoringTestAssetCleanup.EnsureFolder(testRunRoot);
            var productionPath = testRunRoot + "/Production.asset";
            var developmentPath = testRunRoot + "/Development.asset";
            var budgetPath = testRunRoot + "/PerformanceBudget.asset";
            var configPath = testRunRoot + "/HorrorFrameworkConfig.asset";
            ProductionHardeningBuilder.CreateMissingDefaultsAtPaths(
                productionPath,
                developmentPath,
                budgetPath,
                configPath);
            var productionGuid = AssetDatabase.AssetPathToGUID(productionPath);
            var budgetGuid = AssetDatabase.AssetPathToGUID(budgetPath);
            var production = AssetDatabase.LoadAssetAtPath<HorrorBuildProfile>(productionPath);
            var development = AssetDatabase.LoadAssetAtPath<HorrorBuildProfile>(developmentPath);
            var budget = AssetDatabase.LoadAssetAtPath<HorrorPerformanceBudget>(budgetPath);
            var config = AssetDatabase.LoadAssetAtPath<Setus.HorrorFramework.Core.Config.HorrorFrameworkConfig>(
                configPath);

            Assert.That(production, Is.Not.Null);
            Assert.That(production.IsProduction, Is.True);
            Assert.That(development, Is.Not.Null);
            Assert.That(development.DevelopmentBuild, Is.True);
            Assert.That(budget, Is.Not.Null);
            Assert.That(budget.TargetFramesPerSecond, Is.EqualTo(60));
            Assert.That(budget.SteadyStateGcBytesPerFrame, Is.Zero);
            Assert.That(config, Is.Not.Null);
            Assert.That(config.DebugEnabledByDefault, Is.False);
            Assert.That(
                config.EnabledLogCategories,
                Is.EqualTo(Setus.HorrorFramework.Debugging.Logging.HorrorLogCategory.None));

            production.Configure(
                "custom-production",
                BuildTarget.StandaloneOSX,
                "Builds/Custom/Product.app",
                false,
                false,
                false,
                false);
            budget.Configure(90, 128, 7, 11, 3, 2);
            var serializedConfig = new SerializedObject(config);
            serializedConfig.FindProperty("debugEnabledByDefault").boolValue = true;
            serializedConfig.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(production);
            EditorUtility.SetDirty(budget);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            var authoredProduction = EditorJsonUtility.ToJson(production);
            var authoredBudget = EditorJsonUtility.ToJson(budget);
            var authoredConfig = EditorJsonUtility.ToJson(config);
            var productName = PlayerSettings.productName;

            ProductionHardeningBuilder.CreateMissingDefaultsAtPaths(
                productionPath,
                developmentPath,
                budgetPath,
                configPath);

            Assert.That(AssetDatabase.AssetPathToGUID(productionPath), Is.EqualTo(productionGuid));
            Assert.That(AssetDatabase.AssetPathToGUID(budgetPath), Is.EqualTo(budgetGuid));
            Assert.That(EditorJsonUtility.ToJson(production), Is.EqualTo(authoredProduction));
            Assert.That(EditorJsonUtility.ToJson(budget), Is.EqualTo(authoredBudget));
            Assert.That(EditorJsonUtility.ToJson(config), Is.EqualTo(authoredConfig));
            Assert.That(PlayerSettings.productName, Is.EqualTo(productName));
        }

        [Test]
        public void InvalidProfileFailsValidationWithoutBeingRepaired()
        {
            var profile = ScriptableObject.CreateInstance<HorrorBuildProfile>();
            try
            {
                profile.Configure(
                    "production",
                    BuildTarget.StandaloneOSX,
                    "/absolute/output.app",
                    true,
                    true,
                    true,
                    true);
                var before = EditorJsonUtility.ToJson(profile);

                var result = ProductionReadinessValidator.ValidateProfile(profile);

                Assert.That(result.IsValid, Is.False);
                Assert.That(result.Issues.Any(issue => issue.Code == "release.profile.output-invalid"), Is.True);
                Assert.That(result.Issues.Any(issue => issue.Code == "release.profile.production-debug"), Is.True);
                Assert.That(EditorJsonUtility.ToJson(profile), Is.EqualTo(before));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void MissingProfileFailsWithExplicitDiagnostic()
        {
            var result = ProductionReadinessValidator.ValidateProfile(null);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Issues.Any(issue => issue.Code == "release.profile.missing"), Is.True);
        }

        [Test]
        public void CanonicalValidationRetainsReleaseAssetsAcrossSceneInspection()
        {
            ProductionHardeningBuilder.Apply();
            var profile = AssetDatabase.LoadAssetAtPath<HorrorBuildProfile>(
                ProductionHardeningBuilder.ProductionProfilePath);
            var budget = AssetDatabase.LoadAssetAtPath<HorrorPerformanceBudget>(
                ProductionHardeningBuilder.PerformanceBudgetPath);
            var frameworkConfig = AssetDatabase.LoadAssetAtPath<Setus.HorrorFramework.Core.Config.HorrorFrameworkConfig>(
                ProductionHardeningBuilder.FrameworkConfigPath);
            var beforeProfile = EditorJsonUtility.ToJson(profile);
            var beforeBudget = EditorJsonUtility.ToJson(budget);
            var beforeFrameworkConfig = EditorJsonUtility.ToJson(frameworkConfig);
            var activeSceneBefore = SceneManager.GetActiveScene();
            var profileWasDirty = EditorUtility.IsDirty(profile);
            var budgetWasDirty = EditorUtility.IsDirty(budget);
            var frameworkConfigWasDirty = EditorUtility.IsDirty(frameworkConfig);

            var result = HorrorBuildCommand.ValidateCanonicalProject(
                ProductionHardeningBuilder.ProductionProfilePath);

            Assert.That(result.IsValid, Is.True, string.Join("\n", result.Issues));
            Assert.That(
                result.Issues.Any(issue => issue.Code == "release.profile.missing"),
                Is.False);
            Assert.That(
                result.Issues.Any(issue => issue.Code == "release.performance-budget.missing"),
                Is.False);
            Assert.That(EditorJsonUtility.ToJson(profile), Is.EqualTo(beforeProfile));
            Assert.That(EditorJsonUtility.ToJson(budget), Is.EqualTo(beforeBudget));
            Assert.That(EditorJsonUtility.ToJson(frameworkConfig), Is.EqualTo(beforeFrameworkConfig));
            Assert.That(EditorUtility.IsDirty(profile), Is.EqualTo(profileWasDirty));
            Assert.That(EditorUtility.IsDirty(budget), Is.EqualTo(budgetWasDirty));
            Assert.That(EditorUtility.IsDirty(frameworkConfig), Is.EqualTo(frameworkConfigWasDirty));
            Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(activeSceneBefore.handle));
        }
    }

    public sealed class ProductionReadinessSceneTests
    {
        private const string TestRoot = "Assets/Game/__SetusHorrorFrameworkTests/FWAudit01";

        private EditorBuildSettingsScene[] originalBuildScenes;
        private StandaloneGameConfig gameConfig;
        private StableIdManifest manifest;
        private HorrorPerformanceBudget budget;

        [SetUp]
        public void SetUp()
        {
            originalBuildScenes = EditorBuildSettings.scenes;
            Setus.HorrorFramework.Tests.EditMode.Authoring.AuthoringTestAssetCleanup.Delete(TestRoot);
            Setus.HorrorFramework.Tests.EditMode.Authoring.AuthoringTestAssetCleanup.EnsureFolder(TestRoot);
            manifest = ScriptableObject.CreateInstance<StableIdManifest>();
            AssetDatabase.CreateAsset(manifest, TestRoot + "/StableIdManifest.asset");
            budget = ScriptableObject.CreateInstance<HorrorPerformanceBudget>();
            budget.Configure(60, 0, 16, 32, 8, 4);
            AssetDatabase.CreateAsset(budget, TestRoot + "/PerformanceBudget.asset");
            gameConfig = ScriptableObject.CreateInstance<StandaloneGameConfig>();
            AssetDatabase.CreateAsset(gameConfig, TestRoot + "/StandaloneGameConfig.asset");
            ConfigureGame("Boot", "MainMenu", "Gameplay");
            AssetDatabase.SaveAssets();
        }

        [TearDown]
        public void TearDown()
        {
            EditorBuildSettings.scenes = originalBuildScenes;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            gameConfig = null;
            manifest = null;
            budget = null;
            Setus.HorrorFramework.Tests.EditMode.Authoring.AuthoringTestAssetCleanup.Delete(TestRoot);
        }

        [Test]
        public void GenericProductionValidationPassesWithoutM12Sample()
        {
            ConfigureBuildSettings(
                CreateScene("Boot"),
                CreateScene("MainMenu"),
                CreateGameplayScene("Gameplay"));

            var result = ProductionReadinessValidator.ValidateEnabledScenes(gameConfig, budget);

            Assert.That(result.IsValid, Is.True, string.Join("\n", result.Issues));
            Assert.That(result.Issues.Any(issue => issue.Code == "release.sample.invalid"), Is.False);
        }

        [Test]
        public void InvalidEnabledProductionSceneFailsGenericValidation()
        {
            ConfigureBuildSettings(
                CreateScene("Boot"),
                CreateScene("MainMenu"),
                CreateGameplayScene("Gameplay", scene =>
                {
                    var invalid = new GameObject("InvalidNarrative");
                    SceneManager.MoveGameObjectToScene(invalid, scene);
                    invalid.AddComponent<NarrativeScenarioAdapter>();
                }));

            var result = ProductionReadinessValidator.ValidateEnabledScenes(gameConfig, budget);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Issues.Any(issue =>
                issue.Code == "narrative.scenario.missing" && issue.Message.Contains("Gameplay.unity")), Is.True);
        }

        [Test]
        public void EveryEnabledGameplaySceneIsValidated()
        {
            ConfigureGame("Boot", "MainMenu", "GameplayA");
            var gameplayA = CreateGameplayScene("GameplayA", scene =>
            {
                var invalid = new GameObject("InvalidNarrativeA");
                SceneManager.MoveGameObjectToScene(invalid, scene);
                invalid.AddComponent<NarrativeScenarioAdapter>();
            });
            var gameplayB = CreateGameplayScene("GameplayB", scene =>
            {
                var invalid = new GameObject("InvalidNarrativeB");
                SceneManager.MoveGameObjectToScene(invalid, scene);
                invalid.AddComponent<NarrativeScenarioAdapter>();
            });
            ConfigureBuildSettings(CreateScene("Boot"), CreateScene("MainMenu"), gameplayA, gameplayB);

            var result = ProductionReadinessValidator.ValidateEnabledScenes(gameConfig, budget);

            Assert.That(result.Issues.Any(issue =>
                issue.Code == "narrative.scenario.missing" && issue.Message.Contains("GameplayA.unity")), Is.True);
            Assert.That(result.Issues.Any(issue =>
                issue.Code == "narrative.scenario.missing" && issue.Message.Contains("GameplayB.unity")), Is.True);
        }

        [Test]
        public void EveryGameplaySceneUsesTheCanonicalManifestForPersistentOwners()
        {
            ConfigureGame("Boot", "MainMenu", "GameplayA");
            ConfigureManifest(
                new StableIdManifestEntry(
                    "trigger.scene-a",
                    StableIdManifestEntryKind.Required,
                    sceneId: "GameplayA"),
                new StableIdManifestEntry(
                    "trigger.scene-b",
                    StableIdManifestEntryKind.Required,
                    sceneId: "GameplayB"));
            ConfigureBuildSettings(
                CreateScene("Boot"),
                CreateScene("MainMenu"),
                CreateGameplayScene("GameplayA", scene =>
                    CreatePersistentTrigger(scene, "TriggerA", "trigger.scene-a")),
                CreateGameplayScene("GameplayB", scene =>
                    CreatePersistentTrigger(scene, "TriggerB", "trigger.scene-b")));

            var result = ProductionReadinessValidator.ValidateEnabledScenes(gameConfig, budget);

            Assert.That(result.IsValid, Is.True, string.Join("\n", result.Issues));
            Assert.That(result.Issues.Any(issue => issue.Code == "save.manifest.missing"), Is.False);
        }

        [Test]
        public void RequiredEntryFromSceneADoesNotApplyToSceneB()
        {
            ConfigureGame("Boot", "MainMenu", "GameplayA");
            ConfigureManifest(new StableIdManifestEntry(
                "trigger.scene-a",
                StableIdManifestEntryKind.Required,
                sceneId: "GameplayA"));
            ConfigureBuildSettings(
                CreateScene("Boot"),
                CreateScene("MainMenu"),
                CreateGameplayScene("GameplayA", scene =>
                    CreatePersistentTrigger(scene, "TriggerA", "trigger.scene-a")),
                CreateGameplayScene("GameplayB"));

            var result = ProductionReadinessValidator.ValidateEnabledScenes(gameConfig, budget);

            Assert.That(result.IsValid, Is.True, string.Join("\n", result.Issues));
        }

        [Test]
        public void MissingRequiredOwnerFailsOnlyForItsApplicableGameplayScene()
        {
            ConfigureGame("Boot", "MainMenu", "GameplayA");
            ConfigureManifest(
                new StableIdManifestEntry(
                    "trigger.scene-a",
                    StableIdManifestEntryKind.Required,
                    sceneId: "GameplayA"),
                new StableIdManifestEntry(
                    "trigger.scene-b",
                    StableIdManifestEntryKind.Required,
                    sceneId: "GameplayB"));
            ConfigureBuildSettings(
                CreateScene("Boot"),
                CreateScene("MainMenu"),
                CreateGameplayScene("GameplayA", scene =>
                    CreatePersistentTrigger(scene, "TriggerA", "trigger.scene-a")),
                CreateGameplayScene("GameplayB"));

            var result = ProductionReadinessValidator.ValidateEnabledScenes(gameConfig, budget);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Issues.Any(issue =>
                issue.Code == "stable-id.MissingRequiredId" &&
                issue.Message.Contains("GameplayB.unity") &&
                issue.Message.Contains("trigger.scene-b")), Is.True);
            Assert.That(result.Issues.Any(issue =>
                issue.Code == "stable-id.MissingRequiredId" &&
                issue.Message.Contains("GameplayA.unity")), Is.False);
        }

        [Test]
        public void PresentM12SampleRunsOptionalSampleValidation()
        {
            ConfigureBuildSettings(
                CreateScene("Boot"),
                CreateScene("MainMenu"),
                CreateGameplayScene("Gameplay", scene =>
                {
                    var sampleRoot = new GameObject(FirstPersonHorrorTemplateBuilder.TemplateRootName);
                    SceneManager.MoveGameObjectToScene(sampleRoot, scene);
                }));

            var result = ProductionReadinessValidator.ValidateEnabledScenes(gameConfig, budget);

            Assert.That(result.Issues.Any(issue => issue.Code == "release.sample.invalid"), Is.True);
        }

        [Test]
        public void EnabledSceneValidationDoesNotMutateScenesOrConfiguration()
        {
            var boot = CreateScene("Boot");
            var menu = CreateScene("MainMenu");
            var gameplay = CreateGameplayScene("Gameplay");
            ConfigureBuildSettings(boot, menu, gameplay);
            var beforeScene = File.ReadAllBytes(gameplay);
            var beforeConfig = EditorJsonUtility.ToJson(gameConfig);
            var beforeBudget = EditorJsonUtility.ToJson(budget);
            var beforeBuildScenes = EditorBuildSettings.scenes
                .Select(scene => $"{scene.enabled}:{scene.path}")
                .ToArray();

            ProductionReadinessValidator.ValidateEnabledScenes(gameConfig, budget);

            CollectionAssert.AreEqual(beforeScene, File.ReadAllBytes(gameplay));
            Assert.That(EditorJsonUtility.ToJson(gameConfig), Is.EqualTo(beforeConfig));
            Assert.That(EditorJsonUtility.ToJson(budget), Is.EqualTo(beforeBudget));
            CollectionAssert.AreEqual(
                beforeBuildScenes,
                EditorBuildSettings.scenes.Select(scene => $"{scene.enabled}:{scene.path}").ToArray());
        }

        private void ConfigureGame(string boot, string menu, string gameplay)
        {
            var serialized = new SerializedObject(gameConfig);
            serialized.FindProperty("bootSceneName").stringValue = boot;
            serialized.FindProperty("mainMenuSceneName").stringValue = menu;
            serialized.FindProperty("firstGameplaySceneName").stringValue = gameplay;
            serialized.FindProperty("stableIdManifest").objectReferenceValue = manifest;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(gameConfig);
            AssetDatabase.SaveAssetIfDirty(gameConfig);
        }

        private void ConfigureManifest(params StableIdManifestEntry[] entries)
        {
            manifest.ReplaceEntries(entries);
            EditorUtility.SetDirty(manifest);
            AssetDatabase.SaveAssetIfDirty(manifest);
        }

        private static string CreateScene(string name, Action<Scene> configure = null)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            configure?.Invoke(scene);
            var path = $"{TestRoot}/{name}.unity";
            Assert.That(EditorSceneManager.SaveScene(scene, path), Is.True);
            return path;
        }

        private static string CreateGameplayScene(string name, Action<Scene> configure = null)
        {
            return CreateScene(name, scene =>
            {
                var brightness = new GameObject("BrightnessVolume");
                SceneManager.MoveGameObjectToScene(brightness, scene);
                brightness.AddComponent<Volume>().isGlobal = true;
                brightness.AddComponent<BrightnessVolumeSettingsApplier>();
                configure?.Invoke(scene);
            });
        }

        private static void CreatePersistentTrigger(Scene scene, string objectName, string stableId)
        {
            var owner = new GameObject(objectName);
            SceneManager.MoveGameObjectToScene(owner, scene);
            var collider = owner.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            owner.AddComponent<StableId>().Assign(stableId);
            owner.AddComponent<InteractionTriggerZone>();
        }

        private static void ConfigureBuildSettings(params string[] paths)
        {
            EditorBuildSettings.scenes = paths.Select(path => new EditorBuildSettingsScene(path, true)).ToArray();
        }
    }
}
