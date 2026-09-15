using System.IO;
using System.Linq;
using NUnit.Framework;
using Setus.HorrorFramework.Editor.Builders;
using Setus.HorrorFramework.SceneFlow.Loading;
using Setus.HorrorFramework.SceneFlow.Transitions;
using Setus.HorrorFramework.UI.Menus;
using UnityEditor;
using UnityEngine;

namespace Setus.HorrorFramework.Tests.EditMode.SceneFlow
{
    public sealed class SceneFlowUiShellTemplateBuilderTests
    {
        private const string TestContainerPath = "Assets/__SetusHorrorFrameworkTests";
        private const string TestRootPath = TestContainerPath + "/SceneFlowUiShellTemplateBuilder";
        private const string TestConfigPath = TestRootPath + "/Settings/StandaloneGameConfig.asset";
        private const string TestUiShellPrefabPath = TestRootPath + "/Prefabs/UI/SetusUiShell.prefab";
        private const string TestLegacyUiShellPrefabPath = TestRootPath + "/Prefabs/SetusUiShell.prefab";

        private string productionConfigGuid;
        private string productionUiShellPrefabGuid;

        [SetUp]
        public void SetUp()
        {
            productionConfigGuid = AssetDatabase.AssetPathToGUID(SceneFlowUiShellTemplateBuilder.ConfigPath);
            productionUiShellPrefabGuid = AssetDatabase.AssetPathToGUID(SceneFlowUiShellTemplateBuilder.UiShellPrefabPath);
            DeleteTestAssets();
        }

        [TearDown]
        public void TearDown()
        {
            DeleteTestAssets();
            Assert.AreEqual(productionConfigGuid, AssetDatabase.AssetPathToGUID(SceneFlowUiShellTemplateBuilder.ConfigPath));
            Assert.AreEqual(productionUiShellPrefabGuid, AssetDatabase.AssetPathToGUID(SceneFlowUiShellTemplateBuilder.UiShellPrefabPath));
        }

        [Test]
        public void SafeBuild_PreservesExistingTestAssetGuidsAndConfigReference()
        {
            SceneFlowUiShellTemplateBuilder.BuildUiShellAtPaths(
                TestConfigPath,
                TestUiShellPrefabPath,
                false);

            var configGuid = AssetDatabase.AssetPathToGUID(TestConfigPath);
            var uiShellPrefabGuid = AssetDatabase.AssetPathToGUID(TestUiShellPrefabPath);

            SceneFlowUiShellTemplateBuilder.BuildUiShellAtPaths(
                TestConfigPath,
                TestUiShellPrefabPath,
                false);

            Assert.AreEqual(configGuid, AssetDatabase.AssetPathToGUID(TestConfigPath));
            Assert.AreEqual(uiShellPrefabGuid, AssetDatabase.AssetPathToGUID(TestUiShellPrefabPath));
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<GameObject>(TestLegacyUiShellPrefabPath));

            var contents = PrefabUtility.LoadPrefabContents(TestUiShellPrefabPath);
            try
            {
                var runner = contents.GetComponent<SceneTransitionRunner>();
                var adapter = contents.GetComponent<UiShellCommandAdapter>();
                var savePanel = contents.GetComponent<DebugSaveLoadPanel>();
                var config = AssetDatabase.LoadAssetAtPath<StandaloneGameConfig>(TestConfigPath);
                var serializedRunner = new SerializedObject(runner);
                var serializedAdapter = new SerializedObject(adapter);
                var serializedSavePanel = new SerializedObject(savePanel);
                var labels = contents.GetComponentsInChildren<UnityEngine.UI.Text>(true);
                Assert.AreSame(config, serializedRunner.FindProperty("gameConfig").objectReferenceValue);
                Assert.AreSame(config, serializedAdapter.FindProperty("gameConfig").objectReferenceValue);
                Assert.IsNotNull(serializedAdapter.FindProperty("continueButton").objectReferenceValue);
                Assert.IsNull(serializedSavePanel.FindProperty("slotId"));
                Assert.IsTrue(labels.Any(label => label.text == "Save / Load"));
                Assert.IsTrue(labels.Any(label => label.name == "SaveStatus"));
                Assert.IsFalse(labels.Any(label => label.text.Contains("Debug slot")));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        [Test]
        public void CanonicalUiShellPrefabPathUsesM10Convention()
        {
            Assert.AreEqual(
                "Assets/Game/Prefabs/UI/SetusUiShell.prefab",
                SceneFlowUiShellTemplateBuilder.UiShellPrefabPath);
        }

        private static void DeleteTestAssets()
        {
            if (AssetDatabase.IsValidFolder(TestContainerPath))
            {
                AssetDatabase.DeleteAsset(TestContainerPath);
            }

            if (Directory.Exists(TestContainerPath))
            {
                Directory.Delete(TestContainerPath, true);
            }

            var metaPath = TestContainerPath + ".meta";
            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }

            AssetDatabase.Refresh();
        }
    }
}
