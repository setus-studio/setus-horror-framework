using System.IO;
using Setus.HorrorFramework.Player.Camera;
using Setus.HorrorFramework.Player.Controller;
using Setus.HorrorFramework.Player.Input;
using Setus.HorrorFramework.Interaction.Interactors;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Setus.HorrorFramework.Editor.Builders
{
    public static class PlayerFoundationTemplateBuilder
    {
        public const string InputActionsPath = "Assets/Game/Settings/SetusPlayerControls.inputactions";
        public const string PlayerPrefabPath = "Assets/Game/Prefabs/Player/SetusFirstPersonPlayer.prefab";

        [MenuItem("Setus/Horror Framework/Player/Create Or Select First Person Player Template")]
        public static void CreateDefaultAssetsFromMenu()
        {
            var result = BuildDefaultAssets(false);
            Debug.Log($"Player template ready: {result.PlayerPrefabPath}, {result.InputActionsPath}");
        }

        [MenuItem("Setus/Horror Framework/Player/Regenerate First Person Player Template (Destructive)")]
        public static void RegenerateDefaultAssetsFromMenu()
        {
            if (!EditorUtility.DisplayDialog(
                    "Regenerate Player Template",
                    "This deletes and recreates the player prefab and Input Actions asset. Existing GUIDs and designer edits will be lost.",
                    "Regenerate",
                    "Cancel"))
            {
                return;
            }

            var result = BuildDefaultAssets(true);
            Debug.LogWarning($"Regenerated player template: {result.PlayerPrefabPath}, {result.InputActionsPath}");
        }

        public static PlayerFoundationTemplateBuildResult BuildDefaultAssets(bool destructiveRegeneration)
        {
            return BuildAssetsAtPaths(InputActionsPath, PlayerPrefabPath, destructiveRegeneration);
        }

        public static PlayerFoundationTemplateBuildResult BuildAssetsAtPaths(
            string inputActionsPath,
            string playerPrefabPath,
            bool destructiveRegeneration)
        {
            ValidateAssetPath(inputActionsPath, nameof(inputActionsPath));
            ValidateAssetPath(playerPrefabPath, nameof(playerPrefabPath));
            EnsureParentFolder(inputActionsPath);
            EnsureParentFolder(playerPrefabPath);

            var inputActions = CreateInputActions(inputActionsPath, destructiveRegeneration);
            var prefab = CreatePlayerPrefab(inputActions, playerPrefabPath, destructiveRegeneration);

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(inputActionsPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(playerPrefabPath, ImportAssetOptions.ForceSynchronousImport);

            return new PlayerFoundationTemplateBuildResult(inputActionsPath, playerPrefabPath);
        }

        private static InputActionAsset CreateInputActions(string inputActionsPath, bool destructiveRegeneration)
        {
            if (destructiveRegeneration && AssetDatabase.LoadAssetAtPath<InputActionAsset>(inputActionsPath) != null)
            {
                AssetDatabase.DeleteAsset(inputActionsPath);
            }

            var existing = AssetDatabase.LoadAssetAtPath<InputActionAsset>(inputActionsPath);
            if (existing != null)
            {
                return existing;
            }

            var templateAsset = ScriptableObject.CreateInstance<InputActionAsset>();
            templateAsset.name = "SetusPlayerControls";

            var playerMap = new InputActionMap("Player");
            var move = playerMap.AddAction("Move", InputActionType.Value);
            move.expectedControlType = "Vector2";
            move.AddCompositeBinding("Dpad")
                .With("up", "<Keyboard>/w")
                .With("down", "<Keyboard>/s")
                .With("left", "<Keyboard>/a")
                .With("right", "<Keyboard>/d");
            move.AddBinding("<Gamepad>/leftStick");

            var look = playerMap.AddAction("Look", InputActionType.Value);
            look.expectedControlType = "Vector2";
            look.AddBinding("<Mouse>/delta");
            look.AddBinding("<Gamepad>/rightStick");

            var sprint = playerMap.AddAction("Sprint", InputActionType.Button);
            sprint.expectedControlType = "Button";
            sprint.AddBinding("<Keyboard>/leftShift");
            sprint.AddBinding("<Gamepad>/leftStickPress");

            var crouch = playerMap.AddAction("Crouch", InputActionType.Button);
            crouch.expectedControlType = "Button";
            crouch.AddBinding("<Keyboard>/leftCtrl");
            crouch.AddBinding("<Keyboard>/c");
            crouch.AddBinding("<Gamepad>/buttonEast");

            var interact = playerMap.AddAction("Interact", InputActionType.Button);
            interact.expectedControlType = "Button";
            interact.AddBinding("<Keyboard>/e");
            interact.AddBinding("<Gamepad>/buttonSouth");

            var pause = playerMap.AddAction("Pause", InputActionType.Button);
            pause.expectedControlType = "Button";
            pause.AddBinding("<Keyboard>/escape");

            templateAsset.AddActionMap(playerMap);
            File.WriteAllText(inputActionsPath, templateAsset.ToJson());
            Object.DestroyImmediate(templateAsset);

            AssetDatabase.ImportAsset(inputActionsPath, ImportAssetOptions.ForceSynchronousImport);
            var imported = AssetDatabase.LoadAssetAtPath<InputActionAsset>(inputActionsPath);
            if (imported == null)
            {
                throw new IOException($"Failed to import generated Input Actions asset: {inputActionsPath}");
            }

            return imported;
        }

        private static GameObject CreatePlayerPrefab(
            InputActionAsset inputActions,
            string playerPrefabPath,
            bool destructiveRegeneration)
        {
            if (destructiveRegeneration && AssetDatabase.LoadAssetAtPath<GameObject>(playerPrefabPath) != null)
            {
                AssetDatabase.DeleteAsset(playerPrefabPath);
            }

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(playerPrefabPath);
            if (existing != null)
            {
                var contents = PrefabUtility.LoadPrefabContents(playerPrefabPath);
                try
                {
                    var interactor = contents.GetComponent<RaycastInteractor>();
                    if (interactor != null && interactor.ActionsAsset == null)
                    {
                        interactor.ConfigureActionsAsset(inputActions);
                        PrefabUtility.SaveAsPrefabAsset(contents, playerPrefabPath);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
                return existing;
            }

            var root = new GameObject("SetusFirstPersonPlayer");
            try
            {
                var characterController = root.AddComponent<CharacterController>();
                characterController.height = 1.8f;
                characterController.radius = 0.28f;
                characterController.center = Vector3.up * 0.9f;
                characterController.slopeLimit = 50f;
                characterController.stepOffset = 0.32f;

                var inputSource = root.AddComponent<InputSystemPlayerInputSource>();
                inputSource.ConfigureActionsAsset(inputActions);

                var cameraObject = new GameObject("CameraRig");
                cameraObject.transform.SetParent(root.transform);
                cameraObject.transform.localPosition = new Vector3(0f, 1.62f, 0f);
                cameraObject.transform.localRotation = Quaternion.identity;

                var camera = cameraObject.AddComponent<UnityEngine.Camera>();
                camera.nearClipPlane = 0.03f;
                camera.fieldOfView = 70f;
                cameraObject.AddComponent<AudioListener>();

                var cameraRig = cameraObject.AddComponent<FirstPersonCameraRig>();
                ConfigureObjectReference(cameraRig, "playerCamera", camera);
                ConfigureObjectReference(cameraRig, "pitchRoot", cameraObject.transform);

                var controller = root.AddComponent<FirstPersonPlayerController>();
                ConfigureObjectReference(controller, "characterController", characterController);
                ConfigureObjectReference(controller, "cameraRig", cameraRig);
                ConfigureObjectReference(controller, "inputSourceBehaviour", inputSource);
                ConfigureFloat(controller, "walkSpeed", 3.2f);
                ConfigureFloat(controller, "sprintSpeed", 5.1f);
                ConfigureFloat(controller, "crouchSpeed", 1.8f);
                ConfigureFloat(controller, "lookSensitivity", 0.12f);
                ConfigureFloat(controller, "minimumPitch", -80f);
                ConfigureFloat(controller, "maximumPitch", 80f);
                ConfigureFloat(controller, "standingHeight", 1.8f);
                ConfigureFloat(controller, "crouchingHeight", 1.2f);
                ConfigureBool(controller, "headBobEnabled", true);

                var saveAdapter = root.AddComponent<PlayerPoseSaveAdapter>();
                ConfigureObjectReference(saveAdapter, "playerController", controller);

                var interactor = root.AddComponent<RaycastInteractor>();
                interactor.ConfigureRayOrigin(cameraObject.transform);
                interactor.ConfigureActionsAsset(inputActions);

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, playerPrefabPath);
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ConfigureObjectReference(Object target, string propertyName, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureFloat(Object target, string propertyName, float value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureBool(Object target, string propertyName, bool value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ValidateAssetPath(string path, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/", System.StringComparison.Ordinal))
            {
                throw new System.ArgumentException("Asset paths must be project-relative paths below Assets/.", parameterName);
            }
        }

        private static void EnsureParentFolder(string assetPath)
        {
            var slash = assetPath.LastIndexOf('/');
            EnsureFolder(assetPath.Substring(0, slash));
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
    }
}
