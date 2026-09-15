using System.Linq;
using NUnit.Framework;
using Setus.HorrorFramework.Atmosphere.Scares;
using Setus.HorrorFramework.Editor.Authoring;
using Setus.HorrorFramework.Editor.Menus;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Setus.HorrorFramework.Tests.EditMode.Authoring
{
    public sealed class ScareAuthoringCommandTests
    {
        private const string TestRoot = "Assets/Game/__SetusHorrorFrameworkTests/M9F1";
        private const string FirstScenePath = TestRoot + "/FirstScene.unity";

        private ScareDefinition definition;
        private ScareDefinition additionalDefinition;

        [SetUp]
        public void SetUp()
        {
            Undo.ClearAll();
            DeleteTestAssets();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            definition = CreateDefinition("m9.test.scare");
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            if (definition != null)
            {
                Object.DestroyImmediate(definition);
            }

            if (additionalDefinition != null)
            {
                Object.DestroyImmediate(additionalDefinition);
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            DeleteTestAssets();
        }

        [Test]
        public void SingleSceneCreatesTriggerAndCatalogInTargetScene()
        {
            var scene = SceneManager.GetActiveScene();

            var trigger = HorrorAuthoringMenuCommands.CreateScareTriggerInScene(
                definition,
                scene,
                null,
                Vector3.one);

            var adapter = FindComponents<ScareScenarioAdapter>(scene).Single();
            Assert.AreEqual(scene, trigger.scene);
            Assert.AreEqual(scene, adapter.gameObject.scene);
            Assert.IsTrue(adapter.Definitions.Contains(definition));
            Assert.AreEqual(definition.ScareId, trigger.GetComponent<ScareScenarioTrigger>().ScareId);
        }

        [Test]
        public void AdditiveSceneDoesNotMutateCatalogOwnedByAnotherScene()
        {
            var firstScene = SceneManager.GetActiveScene();
            var firstCatalog = new GameObject("FirstSceneCatalog").AddComponent<ScareScenarioAdapter>();
            additionalDefinition = CreateDefinition("m9.test.existing");
            HorrorAuthoringObjectFactory.SetObjectArray(firstCatalog, "scares", additionalDefinition);
            EnsureFolder(TestRoot);
            Assert.IsTrue(EditorSceneManager.SaveScene(firstScene, FirstScenePath));

            var secondScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(secondScene);
            var selectedParent = new GameObject("SecondSceneRoot").transform;

            var trigger = HorrorAuthoringMenuCommands.CreateScareTriggerInScene(
                definition,
                secondScene,
                selectedParent,
                Vector3.zero);

            Assert.AreEqual(secondScene, trigger.scene);
            Assert.AreEqual(1, firstCatalog.Definitions.Count);
            Assert.AreSame(additionalDefinition, firstCatalog.Definitions[0]);
            var secondCatalog = FindComponents<ScareScenarioAdapter>(secondScene).Single();
            Assert.IsTrue(secondCatalog.Definitions.Contains(definition));
        }

        [Test]
        public void UndoRemovesTriggerAndRestoresExistingCatalog()
        {
            var scene = SceneManager.GetActiveScene();
            var catalog = new GameObject("Catalog").AddComponent<ScareScenarioAdapter>();

            HorrorAuthoringMenuCommands.CreateScareTriggerInScene(
                definition,
                scene,
                null,
                Vector3.zero);
            Assert.IsTrue(catalog.Definitions.Contains(definition));
            Assert.AreEqual(1, FindComponents<ScareScenarioTrigger>(scene).Length);

            Undo.PerformUndo();

            Assert.IsFalse(catalog.Definitions.Contains(definition));
            Assert.AreEqual(0, FindComponents<ScareScenarioTrigger>(scene).Length);
        }

        [Test]
        public void RedoRestoresTriggerAndCatalogMutation()
        {
            var scene = SceneManager.GetActiveScene();
            var catalog = new GameObject("Catalog").AddComponent<ScareScenarioAdapter>();

            HorrorAuthoringMenuCommands.CreateScareTriggerInScene(
                definition,
                scene,
                null,
                Vector3.zero);
            Undo.PerformUndo();
            Undo.PerformRedo();

            Assert.IsTrue(catalog.Definitions.Contains(definition));
            var trigger = FindComponents<ScareScenarioTrigger>(scene).Single();
            Assert.AreEqual(definition.ScareId, trigger.ScareId);
        }

        private static ScareDefinition CreateDefinition(string scareId)
        {
            var result = ScriptableObject.CreateInstance<ScareDefinition>();
            var serialized = new SerializedObject(result);
            serialized.FindProperty("scareId").stringValue = scareId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return result;
        }

        private static T[] FindComponents<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }

        private static void EnsureFolder(string path)
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

        private static void DeleteTestAssets()
        {
            AuthoringTestAssetCleanup.Delete(TestRoot);
        }
    }
}
