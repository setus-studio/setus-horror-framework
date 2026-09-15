using System.IO;
using System.Linq;
using NUnit.Framework;
using Setus.HorrorFramework.AI.Navigation;
using Setus.HorrorFramework.Atmosphere.Scares;
using Setus.HorrorFramework.Editor.Builders;
using Setus.HorrorFramework.Interaction.Interactables;
using Setus.HorrorFramework.Narrative.Scenario;
using Setus.HorrorFramework.SaveProgression.StableIds;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Setus.HorrorFramework.Tests.EditMode.Authoring
{
    public sealed class AuthoringMilestoneBuilderTests
    {
        private const string TestRoot = "Assets/Game/__SetusHorrorFrameworkTests/M9Authoring";
        private const string ScenePath = TestRoot + "/M9_AuthoringSample.unity";
        private const string ScenarioPath = TestRoot + "/M9_Scenario.asset";
        private const string ObjectivePath = TestRoot + "/M9_Objective.asset";
        private const string ScarePath = TestRoot + "/M9_Scare.asset";
        private const string ManifestPath = TestRoot + "/M9_Manifest.asset";

        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            DeleteTestAssets();
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            DeleteTestAssets();
        }

        [Test]
        public void BuildSampleCreatesCodeFreeScenarioAndPassesUnifiedValidation()
        {
            var result = AuthoringMilestoneBuilder.BuildSampleAtPaths(
                ScenePath,
                ScenarioPath,
                ObjectivePath,
                ScarePath,
                ManifestPath);

            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(0, result.ErrorCount);
            var scenario = AssetDatabase.LoadAssetAtPath<NarrativeScenarioDefinition>(ScenarioPath);
            var scare = AssetDatabase.LoadAssetAtPath<ScareDefinition>(ScarePath);
            Assert.IsNotNull(scenario);
            Assert.AreEqual("m9.sample.scenario", scenario.ScenarioId);
            Assert.AreEqual("m9.sample.objective", scenario.InitialObjectiveId);
            Assert.AreEqual("m9.sample.objective", scenario.Objectives.Single().ObjectiveId);
            Assert.IsNotNull(scare);
            Assert.AreEqual("m9.sample.scare", scare.ScareId);
            var manifest = AssetDatabase.LoadAssetAtPath<StableIdManifest>(ManifestPath);
            Assert.IsNotNull(manifest);
            Assert.IsTrue(manifest.Entries.All(entry => entry.SceneId == "M9_AuthoringSample"));
            Assert.IsTrue(SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<InspectableInteractable>(true))
                .Any());
            Assert.IsTrue(SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<ScareScenarioTrigger>(true))
                .Any());
            Assert.IsTrue(SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<StalkerPatrolRoute>(true))
                .Any());
        }

        [Test]
        public void CleanupRemovesEmptyGeneratedRootAndMeta()
        {
            AuthoringTestAssetCleanup.EnsureFolder(TestRoot);
            Assert.IsTrue(AssetDatabase.IsValidFolder(AuthoringTestAssetCleanup.GeneratedRoot));

            DeleteTestAssets();

            Assert.IsFalse(AssetDatabase.IsValidFolder(AuthoringTestAssetCleanup.GeneratedRoot));
            Assert.IsFalse(Directory.Exists(AuthoringTestAssetCleanup.GeneratedRoot));
            Assert.IsFalse(File.Exists(AuthoringTestAssetCleanup.GeneratedRoot + ".meta"));
        }

        private static void DeleteTestAssets()
        {
            AuthoringTestAssetCleanup.Delete(TestRoot);
        }
    }

    internal static class AuthoringTestAssetCleanup
    {
        public const string GeneratedRoot = "Assets/Game/__SetusHorrorFrameworkTests";

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var slash = path.LastIndexOf('/');
            var parent = slash > 0 ? path.Substring(0, slash) : "Assets";
            var folder = slash > 0 ? path.Substring(slash + 1) : path;
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }

        public static void Delete(string testRoot)
        {
            if (AssetDatabase.IsValidFolder(testRoot))
            {
                AssetDatabase.DeleteAsset(testRoot);
            }

            DeletePhysicalAsset(testRoot);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            if (Directory.Exists(GeneratedRoot) &&
                Directory.GetFileSystemEntries(GeneratedRoot).Length == 0)
            {
                if (AssetDatabase.IsValidFolder(GeneratedRoot))
                {
                    AssetDatabase.DeleteAsset(GeneratedRoot);
                }

                DeletePhysicalAsset(GeneratedRoot);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
        }

        private static void DeletePhysicalAsset(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }

            if (File.Exists(path + ".meta"))
            {
                File.Delete(path + ".meta");
            }
        }
    }
}
