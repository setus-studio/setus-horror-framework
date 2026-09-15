using System.Collections;
using NUnit.Framework;
using Setus.HorrorFramework.Core.Config;
using Setus.HorrorFramework.Core.Events;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Player.Camera;
using Setus.HorrorFramework.Player.Controller;
using Setus.HorrorFramework.Player.State;
using Setus.HorrorFramework.SaveProgression.Migration;
using Setus.HorrorFramework.SaveProgression.Registry;
using Setus.HorrorFramework.SaveProgression.SaveSlots;
using Setus.HorrorFramework.SaveProgression.Serialization;
using UnityEngine;
using UnityEngine.TestTools;

namespace Setus.HorrorFramework.Tests.PlayMode.SaveLoad
{
    public sealed class PlayerSaveRestoreTests
    {
        [UnityTest]
        public IEnumerator SaveRestore_RestoresPlayerPositionRotationAndCameraPitch()
        {
            HorrorGameContext.ShutdownActive();
            var config = ScriptableObject.CreateInstance<HorrorFrameworkConfig>();
            var context = HorrorGameContext.Initialize(config);
            var player = CreatePlayer(new Vector3(1f, 1f, 2f), Quaternion.Euler(0f, 35f, 0f), out var controller);
            var saveAdapter = player.AddComponent<PlayerPoseSaveAdapter>();
            saveAdapter.enabled = false;
            saveAdapter.enabled = true;

            controller.RestorePose(new PlayerPose(
                new Vector3(1f, 1f, 2f),
                Quaternion.Euler(0f, 35f, 0f),
                18f,
                PlayerControlState.Normal));

            var snapshot = context.Saves.Capture(new SaveSlotId("slot-player"));
            controller.RestorePose(new PlayerPose(
                new Vector3(8f, 1f, 9f),
                Quaternion.Euler(0f, 120f, 0f),
                -10f,
                PlayerControlState.Menu));

            context.Saves.Restore(snapshot);
            yield return null;

            var restored = controller.CaptureState();
            Assert.That(Vector3.Distance(new Vector3(1f, 1f, 2f), restored.Position), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(Quaternion.Euler(0f, 35f, 0f), restored.Rotation), Is.LessThan(0.001f));
            Assert.AreEqual(18f, restored.CameraPitch, 0.001f);
            Assert.AreEqual(PlayerControlState.Normal, restored.ControlState);

            Object.Destroy(saveAdapter);
            Object.Destroy(player);
            HorrorGameContext.ShutdownActive();
            Object.Destroy(config);
        }

        [UnityTest]
        public IEnumerator FailedRestoreRollsBackExactPlayerControlAndCameraState()
        {
            const string failingStateKey = "zz.test.player-restore-failure";
            var previousCursorLockState = Cursor.lockState;
            var previousCursorVisible = Cursor.visible;
            var player = CreatePlayer(Vector3.up, Quaternion.identity, out var controller);
            var cameraRig = player.GetComponentInChildren<FirstPersonCameraRig>();
            var camera = cameraRig.Camera;
            try
            {
                controller.SetControlState(PlayerControlState.Menu);
                cameraRig.SetPitch(27f);
                cameraRig.SetPostureOffset(Vector3.down * 0.4f);
                cameraRig.SetHeadBobOffset(Vector3.up * 0.05f);
                cameraRig.SetReactionOffset(Vector3.right * 0.08f, 4f);
                Assert.That(controller.CaptureState().ControlState, Is.EqualTo(PlayerControlState.Normal));

                var rollbackPosition = player.transform.position;
                var rollbackRotation = player.transform.rotation;
                var rollbackCameraPosition = cameraRig.CameraTransform.localPosition;
                var rollbackCameraRotation = cameraRig.CameraTransform.localRotation;
                var rollbackFieldOfView = camera.fieldOfView;
                var rollbackCursorLockState = Cursor.lockState;
                var rollbackCursorVisible = Cursor.visible;

                var declarations = new GlobalSaveStateRegistry();
                declarations.Register(new GlobalSaveStateDeclaration(
                    HorrorGlobalSaveStateRegistry.PlayerPoseStateKey,
                    SaveStateTypeKeys.PlayerPoseV1,
                    GlobalSaveStateRequirement.Required,
                    SaveSchemaVersion.Current,
                    GlobalSaveStateOwnerScope.Scene,
                    GlobalSaveStateIntroductionPolicy.MigrationProvidesRecord));
                declarations.Register(new GlobalSaveStateDeclaration(
                    failingStateKey,
                    SaveStateTypeKeys.Int32V1,
                    GlobalSaveStateRequirement.Required,
                    SaveSchemaVersion.Current,
                    GlobalSaveStateOwnerScope.Context,
                    GlobalSaveStateIntroductionPolicy.MigrationProvidesRecord));

                var events = new GameplayEventBus();
                var registry = new SaveableRegistry(null, declarations);
                registry.RegisterGlobal(controller);
                registry.RegisterGlobal(new FailingGlobalOwner(failingStateKey));
                var saveOwner = new SaveGameOwner(registry, new SaveMigrationPipeline(), events, null);
                var snapshot = new SaveGameSnapshot(
                    SaveSchemaVersion.Current,
                    new SaveSlotId("player-exact-rollback"),
                    null,
                    new[]
                    {
                        new SaveStateRecord(
                            HorrorGlobalSaveStateRegistry.PlayerPoseStateKey,
                            SaveStateTypeKeys.PlayerPoseV1,
                            new PlayerPose(
                                new Vector3(8f, 2f, 9f),
                                Quaternion.Euler(0f, 120f, 0f),
                                -18f,
                                PlayerControlState.Cutscene)),
                        new SaveStateRecord(failingStateKey, SaveStateTypeKeys.Int32V1, 99)
                    },
                    System.Array.Empty<StableSaveStateRecord>());

                var controlStateChanges = 0;
                var restoreCompletedEvents = 0;
                controller.StateMachine.StateChanged += _ => controlStateChanges++;
                using (events.Subscribe<SaveRestoreCompleted>(_ => restoreCompletedEvents++))
                {
                    Assert.Throws<SaveValidationException>(() => saveOwner.Restore(snapshot));
                }

                Assert.That(controller.CurrentControlState, Is.EqualTo(PlayerControlState.Menu));
                Assert.That(player.transform.position, Is.EqualTo(rollbackPosition));
                Assert.That(player.transform.rotation, Is.EqualTo(rollbackRotation));
                Assert.That(cameraRig.Pitch, Is.EqualTo(27f).Within(0.001f));
                Assert.That(cameraRig.CameraTransform.localPosition, Is.EqualTo(rollbackCameraPosition));
                Assert.That(cameraRig.CameraTransform.localRotation, Is.EqualTo(rollbackCameraRotation));
                Assert.That(camera.fieldOfView, Is.EqualTo(rollbackFieldOfView).Within(0.001f));
                Assert.That(Cursor.lockState, Is.EqualTo(rollbackCursorLockState));
                Assert.That(Cursor.visible, Is.EqualTo(rollbackCursorVisible));
                Assert.That(controlStateChanges, Is.EqualTo(1));
                Assert.That(restoreCompletedEvents, Is.Zero);
            }
            finally
            {
                Cursor.lockState = previousCursorLockState;
                Cursor.visible = previousCursorVisible;
                Object.Destroy(player);
            }

            yield return null;
        }

        private static GameObject CreatePlayer(
            Vector3 position,
            Quaternion rotation,
            out FirstPersonPlayerController controller)
        {
            var player = new GameObject("Save Restore Player");
            player.transform.SetPositionAndRotation(position, rotation);

            var characterController = player.AddComponent<CharacterController>();
            characterController.height = 1.8f;
            characterController.radius = 0.25f;
            characterController.center = Vector3.up * 0.9f;

            var cameraObject = new GameObject("Camera Rig");
            cameraObject.transform.SetParent(player.transform);
            cameraObject.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            cameraObject.transform.localRotation = Quaternion.identity;
            cameraObject.AddComponent<UnityEngine.Camera>();
            cameraObject.AddComponent<FirstPersonCameraRig>();

            controller = player.AddComponent<FirstPersonPlayerController>();
            controller.enabled = false;
            return player;
        }

        private sealed class FailingGlobalOwner : RuntimeStateOwnerBase<int>
        {
            public FailingGlobalOwner(string stateKey)
                : base(stateKey)
            {
            }

            public override int CaptureState()
            {
                return 1;
            }

            public override void RestoreState(int state)
            {
                if (state == 99)
                {
                    throw new System.InvalidOperationException("Simulated later restore failure.");
                }
            }
        }
    }
}
