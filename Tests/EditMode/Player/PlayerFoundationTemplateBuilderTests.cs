using System.Reflection;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Setus.HorrorFramework.Editor.Builders;
using Setus.HorrorFramework.Interaction.Interactors;
using Setus.HorrorFramework.Player.Camera;
using Setus.HorrorFramework.Player.Controller;
using Setus.HorrorFramework.Player.Input;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Setus.HorrorFramework.Tests.EditMode.Player
{
    public sealed class PlayerFoundationTemplateBuilderTests
    {
        private const string TestContainerPath = "Assets/__SetusHorrorFrameworkTests";
        private const string TestRootPath = TestContainerPath + "/PlayerFoundationTemplateBuilder";
        private const string TestInputActionsPath = TestRootPath + "/Settings/SetusPlayerControls.inputactions";
        private const string TestPlayerPrefabPath = TestRootPath + "/Prefabs/Player/SetusFirstPersonPlayer.prefab";
        private const string TestLegacyPlayerPrefabPath = TestRootPath + "/Prefabs/SetusFirstPersonPlayer.prefab";
        private const string TestReferencePrefabPath = TestRootPath + "/Prefabs/Player/PlayerReference.prefab";

        private string productionInputActionsGuid;
        private string productionPlayerPrefabGuid;

        [SetUp]
        public void SetUp()
        {
            productionInputActionsGuid = AssetDatabase.AssetPathToGUID(PlayerFoundationTemplateBuilder.InputActionsPath);
            productionPlayerPrefabGuid = AssetDatabase.AssetPathToGUID(PlayerFoundationTemplateBuilder.PlayerPrefabPath);
            DeleteTestAssets();
        }

        [TearDown]
        public void TearDown()
        {
            DeleteTestAssets();
            Assert.AreEqual(
                productionInputActionsGuid,
                AssetDatabase.AssetPathToGUID(PlayerFoundationTemplateBuilder.InputActionsPath),
                "Test cleanup must not change the production Input Actions asset GUID.");
            Assert.AreEqual(
                productionPlayerPrefabGuid,
                AssetDatabase.AssetPathToGUID(PlayerFoundationTemplateBuilder.PlayerPrefabPath),
                "Test cleanup must not change the production player prefab GUID.");
        }

        [Test]
        public void BuildAssetsAtTestPaths_CreatesInputActionsAndPlayerPrefab()
        {
            var result = PlayerFoundationTemplateBuilder.BuildAssetsAtPaths(
                TestInputActionsPath,
                TestPlayerPrefabPath,
                false);

            var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(result.InputActionsPath);
            Assert.IsNotNull(inputActions);
            Assert.IsNotNull(inputActions.FindAction("Player/Move"));
            Assert.IsNotNull(inputActions.FindAction("Player/Look"));
            Assert.IsNotNull(inputActions.FindAction("Player/Sprint"));
            Assert.IsNotNull(inputActions.FindAction("Player/Crouch"));
            Assert.IsNotNull(inputActions.FindAction("Player/Interact"));
            Assert.IsNotNull(inputActions.FindAction("Player/Pause"));

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(result.PlayerPrefabPath);
            Assert.IsNotNull(prefab);
            Assert.IsNotNull(prefab.GetComponent<CharacterController>());
            Assert.IsNotNull(prefab.GetComponent<InputSystemPlayerInputSource>());
            Assert.IsNotNull(prefab.GetComponent<FirstPersonPlayerController>());
            Assert.IsNotNull(prefab.GetComponentInChildren<FirstPersonCameraRig>(true));
            Assert.IsNotNull(prefab.GetComponentInChildren<Camera>(true));
            Assert.AreSame(inputActions, prefab.GetComponent<RaycastInteractor>().ActionsAsset);

            var characterController = prefab.GetComponent<CharacterController>();
            Assert.AreEqual(1.8f, characterController.height, 0.001f);
            Assert.AreEqual(0.28f, characterController.radius, 0.001f);
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<GameObject>(TestLegacyPlayerPrefabPath));
        }

        [Test]
        public void CanonicalPlayerPrefabPathUsesM10Convention()
        {
            Assert.AreEqual(
                "Assets/Game/Prefabs/Player/SetusFirstPersonPlayer.prefab",
                PlayerFoundationTemplateBuilder.PlayerPrefabPath);
        }

        [Test]
        public void ExistingPlayerPrefabReceivesSharedInteractActionWithoutGuidChange()
        {
            var result = PlayerFoundationTemplateBuilder.BuildAssetsAtPaths(
                TestInputActionsPath, TestPlayerPrefabPath, false);
            var guid = AssetDatabase.AssetPathToGUID(result.PlayerPrefabPath);
            var contents = PrefabUtility.LoadPrefabContents(result.PlayerPrefabPath);
            try
            {
                var interactor = contents.GetComponent<RaycastInteractor>();
                var serialized = new SerializedObject(interactor);
                serialized.FindProperty("actionsAsset").objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, result.PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            PlayerFoundationTemplateBuilder.BuildAssetsAtPaths(
                TestInputActionsPath, TestPlayerPrefabPath, false);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(result.PlayerPrefabPath);
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(result.InputActionsPath);
            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(result.PlayerPrefabPath));
            Assert.AreSame(actions, prefab.GetComponent<RaycastInteractor>().ActionsAsset);
        }

        [Test]
        public void InteractionFoundationRetainsSharedInteractActionsAsset()
        {
            var result = PlayerFoundationTemplateBuilder.BuildAssetsAtPaths(
                TestInputActionsPath, TestPlayerPrefabPath, false);
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(result.InputActionsPath);
            var contents = PrefabUtility.LoadPrefabContents(result.PlayerPrefabPath);
            try
            {
                var interactor = contents.GetComponent<RaycastInteractor>();
                interactor.ConfigureActionsAsset(null);

                var configure = typeof(InteractionMilestoneBuilder).GetMethod(
                    "ConfigurePlayerInteractor",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.IsNotNull(configure);
                configure.Invoke(null, new object[] { contents, actions });

                Assert.AreSame(actions, interactor.ActionsAsset);
                Assert.AreEqual(
                    "<Keyboard>/e",
                    actions.FindAction("Player/Interact").bindings[0].path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        [Test]
        public void PhaseThreeUpgradeAddsPauseOnceAndPreservesInputAssetGuid()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(TestInputActionsPath));
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = new InputActionMap("Player");
            var interact = map.AddAction("Interact", InputActionType.Button);
            interact.AddBinding("<Keyboard>/e");
            asset.AddActionMap(map);
            File.WriteAllText(TestInputActionsPath, asset.ToJson());
            UnityEngine.Object.DestroyImmediate(asset);
            AssetDatabase.ImportAsset(TestInputActionsPath, ImportAssetOptions.ForceSynchronousImport);
            var guid = AssetDatabase.AssetPathToGUID(TestInputActionsPath);
            var originalId = AssetDatabase.LoadAssetAtPath<InputActionAsset>(TestInputActionsPath)
                .FindAction("Player/Interact").id;

            TabbedSettingsLayoutBuilder.EnsurePauseActionAtPath(TestInputActionsPath);
            TabbedSettingsLayoutBuilder.EnsurePauseActionAtPath(TestInputActionsPath);

            var upgraded = AssetDatabase.LoadAssetAtPath<InputActionAsset>(TestInputActionsPath);
            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(TestInputActionsPath));
            Assert.AreEqual(originalId, upgraded.FindAction("Player/Interact").id);
            Assert.AreEqual(1, upgraded.FindActionMap("Player").actions.Count(action => action.name == "Pause"));
            Assert.AreEqual("<Keyboard>/escape", upgraded.FindAction("Player/Pause").bindings[0].path);
        }

        [Test]
        public void GeneratedPrefab_ResolvesInputFromActionsAsset()
        {
            var result = PlayerFoundationTemplateBuilder.BuildAssetsAtPaths(
                TestInputActionsPath,
                TestPlayerPrefabPath,
                false);
            var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(result.InputActionsPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(result.PlayerPrefabPath);
            Assert.IsNotNull(inputActions);
            Assert.IsNotNull(prefab);

            var instance = Object.Instantiate(prefab);

            try
            {
                var source = instance.GetComponent<InputSystemPlayerInputSource>();
                source.ResolveConfiguredActions();

                var resolvedMove = GetResolvedAction(source, "resolvedMoveAction");
                var resolvedLook = GetResolvedAction(source, "resolvedLookAction");

                Assert.AreSame(inputActions.FindAction("Player/Move"), resolvedMove);
                Assert.AreSame(inputActions.FindAction("Player/Look"), resolvedLook);
                Assert.Greater(resolvedMove.bindings.Count, 0);
                Assert.Greater(resolvedLook.bindings.Count, 0);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void SafeBuild_PreservesExistingTestAssetGuidsAndPrefabReference()
        {
            PlayerFoundationTemplateBuilder.BuildAssetsAtPaths(
                TestInputActionsPath,
                TestPlayerPrefabPath,
                false);

            var inputActionsGuid = AssetDatabase.AssetPathToGUID(TestInputActionsPath);
            var playerPrefabGuid = AssetDatabase.AssetPathToGUID(TestPlayerPrefabPath);
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TestPlayerPrefabPath);
            var referenceRoot = new GameObject("PlayerReference");

            try
            {
                PrefabUtility.InstantiatePrefab(playerPrefab, referenceRoot.transform);
                PrefabUtility.SaveAsPrefabAsset(referenceRoot, TestReferencePrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(referenceRoot);
            }

            PlayerFoundationTemplateBuilder.BuildAssetsAtPaths(
                TestInputActionsPath,
                TestPlayerPrefabPath,
                false);

            Assert.AreEqual(inputActionsGuid, AssetDatabase.AssetPathToGUID(TestInputActionsPath));
            Assert.AreEqual(playerPrefabGuid, AssetDatabase.AssetPathToGUID(TestPlayerPrefabPath));

            var referenceContents = PrefabUtility.LoadPrefabContents(TestReferencePrefabPath);
            try
            {
                var nestedPrefab = referenceContents.transform.GetChild(0).gameObject;
                var source = PrefabUtility.GetCorrespondingObjectFromSource(nestedPrefab);
                Assert.AreEqual(TestPlayerPrefabPath, AssetDatabase.GetAssetPath(source));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(referenceContents);
            }
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

        private static InputAction GetResolvedAction(InputSystemPlayerInputSource source, string fieldName)
        {
            var field = typeof(InputSystemPlayerInputSource).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            return (InputAction)field.GetValue(source);
        }
    }
}
