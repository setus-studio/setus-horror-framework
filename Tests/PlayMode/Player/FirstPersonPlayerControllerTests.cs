using System.Collections;
using NUnit.Framework;
using Setus.HorrorFramework.Player.Camera;
using Setus.HorrorFramework.Player.Controller;
using Setus.HorrorFramework.Player.Input;
using Setus.HorrorFramework.Player.State;
using Setus.HorrorFramework.SaveProgression.Checkpoints;
using Setus.HorrorFramework.SceneFlow.Spawn;
using Setus.HorrorFramework.SceneFlow.Transitions;
using Setus.HorrorFramework.Core.Config;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.UI.Settings;
using UnityEngine;
using UnityEngine.TestTools;

namespace Setus.HorrorFramework.Tests.PlayMode.Player
{
    public sealed class FirstPersonPlayerControllerTests
    {
        [UnityTest]
        public IEnumerator Tick_MovesWithCharacterControllerAndStopsAtCollider()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.05f, 0f);
            ground.transform.localScale = new Vector3(8f, 0.1f, 8f);

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 1f, 1.25f);
            wall.transform.localScale = new Vector3(4f, 2f, 0.2f);

            var player = CreatePlayer(Vector3.up, Quaternion.identity, out var controller, out var input);
            input.Snapshot = new PlayerInputSnapshot(Vector2.up, Vector2.zero, false, false);
            Physics.SyncTransforms();

            for (var i = 0; i < 80; i++)
            {
                controller.Tick(0.02f);
                yield return null;
            }

            Assert.Greater(player.transform.position.z, 0.05f);
            Assert.Less(player.transform.position.z, 1.05f);

            Object.Destroy(player);
            Object.Destroy(ground);
            Object.Destroy(wall);
        }

        [UnityTest]
        public IEnumerator RestorePose_RestoresPositionRotationPitchAndControlState()
        {
            var player = CreatePlayer(Vector3.up, Quaternion.identity, out var controller, out _);
            var pose = new PlayerPose(
                new Vector3(3f, 1f, 4f),
                Quaternion.Euler(0f, 90f, 0f),
                25f,
                PlayerControlState.Cutscene);

            controller.RestorePose(pose);
            yield return null;

            var restored = controller.CaptureState();
            Assert.That(Vector3.Distance(pose.Position, restored.Position), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(pose.Rotation, restored.Rotation), Is.LessThan(0.001f));
            Assert.AreEqual(25f, restored.CameraPitch, 0.001f);
            Assert.AreEqual(PlayerControlState.Cutscene, restored.ControlState);

            Object.Destroy(player);
        }

        [UnityTest]
        public IEnumerator RestorePose_NormalizesTransientMenuState()
        {
            var player = CreatePlayer(Vector3.up, Quaternion.identity, out var controller, out _);

            controller.RestorePose(new PlayerPose(
                Vector3.up,
                Quaternion.identity,
                0f,
                PlayerControlState.Menu));
            yield return null;

            Assert.That(controller.CurrentControlState, Is.EqualTo(PlayerControlState.Normal));
            Object.Destroy(player);
        }

        [UnityTest]
        public IEnumerator Crouch_DoesNotStandIntoAnOverheadCollider()
        {
            var player = CreatePlayer(Vector3.zero, Quaternion.identity, out var controller, out var input);
            var characterController = player.GetComponent<CharacterController>();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.05f, 0f);
            ground.transform.localScale = new Vector3(4f, 0.1f, 4f);
            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.transform.position = new Vector3(0f, 1.52f, 0f);
            ceiling.transform.localScale = new Vector3(2f, 0.1f, 2f);
            Physics.SyncTransforms();

            input.Snapshot = new PlayerInputSnapshot(Vector2.zero, Vector2.zero, false, true);
            controller.Tick(0.2f);
            Assert.That(characterController.height, Is.EqualTo(1.2f).Within(0.001f));

            input.Snapshot = new PlayerInputSnapshot(Vector2.zero, Vector2.zero, false, false);
            controller.Tick(0.2f);
            Assert.That(characterController.height, Is.EqualTo(1.2f).Within(0.001f));

            Object.Destroy(ceiling);
            yield return null;
            Physics.SyncTransforms();
            controller.Tick(0.2f);
            Assert.That(characterController.height, Is.EqualTo(1.8f).Within(0.001f));

            Object.Destroy(player);
            Object.Destroy(ground);
        }

        [UnityTest]
        public IEnumerator Crouch_LowersCameraWithControllerHeightAndRestoresStandingHeight()
        {
            var player = CreatePlayer(Vector3.zero, Quaternion.identity, out var controller, out var input);
            var characterController = player.GetComponent<CharacterController>();
            var cameraTransform = player.GetComponentInChildren<FirstPersonCameraRig>().CameraTransform;
            var standingCameraHeight = cameraTransform.localPosition.y;

            input.Snapshot = new PlayerInputSnapshot(Vector2.zero, Vector2.zero, false, true);
            controller.Tick(0.2f);
            yield return null;

            Assert.That(characterController.height, Is.EqualTo(1.2f).Within(0.001f));
            Assert.That(
                cameraTransform.localPosition.y,
                Is.EqualTo(standingCameraHeight + characterController.height - 1.8f).Within(0.001f));

            input.Snapshot = new PlayerInputSnapshot(Vector2.zero, Vector2.zero, false, false);
            controller.Tick(0.2f);
            yield return null;

            Assert.That(characterController.height, Is.EqualTo(1.8f).Within(0.001f));
            Assert.That(cameraTransform.localPosition.y, Is.EqualTo(standingCameraHeight).Within(0.001f));

            Object.Destroy(player);
        }

        [UnityTest]
        public IEnumerator CheckpointSpawn_ResolvesRequestedSpawnAndDefaultFallback()
        {
            var player = CreatePlayer(Vector3.zero, Quaternion.identity, out _, out _);
            var checkpointSpawn = CreateSpawnPoint("checkpoint-spawn", "checkpoint-door", new Vector3(3f, 1f, 4f));
            var fallbackSpawn = CreateSpawnPoint("fallback-spawn", PlayerSpawnResolver.DefaultSpawnId, new Vector3(-2f, 1f, 5f));
            Physics.SyncTransforms();

            var checkpoint = new CheckpointModel("checkpoint-01", "Gameplay", "checkpoint-door");
            var exactResult = PlayerSpawnResolver.ResolveAndApply(SceneTransitionRequest.FromCheckpoint(checkpoint));
            Assert.That(exactResult.Kind, Is.EqualTo(PlayerSpawnResolutionKind.AppliedRequestedSpawn));
            Assert.That(player.transform.position, Is.EqualTo(checkpointSpawn.transform.position));

            player.transform.position = Vector3.zero;
            var fallbackResult = PlayerSpawnResolver.ResolveAndApply(
                new SceneTransitionRequest("Gameplay", spawnId: "missing-spawn"));
            Assert.That(fallbackResult.Kind, Is.EqualTo(PlayerSpawnResolutionKind.AppliedDefaultFallback));
            Assert.That(player.transform.position, Is.EqualTo(fallbackSpawn.transform.position));

            Object.Destroy(player);
            Object.Destroy(checkpointSpawn.gameObject);
            Object.Destroy(fallbackSpawn.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SprintToggleRemainsActiveAfterButtonRelease()
        {
            var config = ScriptableObject.CreateInstance<HorrorFrameworkConfig>();
            HorrorGameContext.Initialize(config).RuntimeSettings.SetSprintInputMode(SprintInputMode.Toggle);
            var player = CreatePlayer(Vector3.up, Quaternion.identity, out var controller, out var input);
            try
            {
                input.Snapshot = new PlayerInputSnapshot(Vector2.up, Vector2.zero, true, false);
                controller.Tick(0.1f);
                input.Snapshot = new PlayerInputSnapshot(Vector2.up, Vector2.zero, false, false);
                controller.Tick(0.1f);

                Assert.That(player.transform.position.z, Is.GreaterThan(0.9f));
            }
            finally
            {
                Object.Destroy(player);
                HorrorGameContext.ShutdownActive();
                Object.Destroy(config);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator ZeroHeadBobIntensityKeepsAuthoredCameraHeightWhileMoving()
        {
            var config = ScriptableObject.CreateInstance<HorrorFrameworkConfig>();
            HorrorGameContext.Initialize(config).RuntimeSettings.SetHeadBobIntensity(0f);
            var player = CreatePlayer(Vector3.up, Quaternion.identity, out var controller, out var input);
            var camera = player.GetComponentInChildren<FirstPersonCameraRig>().CameraTransform;
            var authoredHeight = camera.localPosition.y;
            try
            {
                input.Snapshot = new PlayerInputSnapshot(Vector2.up, Vector2.zero, false, false);
                controller.Tick(0.1f);
                Assert.That(camera.localPosition.y, Is.EqualTo(authoredHeight).Within(0.001f));
            }
            finally
            {
                Object.Destroy(player);
                HorrorGameContext.ShutdownActive();
                Object.Destroy(config);
            }

            yield return null;
        }

        private static GameObject CreatePlayer(
            Vector3 position,
            Quaternion rotation,
            out FirstPersonPlayerController controller,
            out TestInputSource input)
        {
            var player = new GameObject("Test First Person Player");
            player.transform.SetPositionAndRotation(position, rotation);

            var characterController = player.AddComponent<CharacterController>();
            characterController.height = 1.8f;
            characterController.radius = 0.25f;
            characterController.center = Vector3.up * 0.9f;

            input = player.AddComponent<TestInputSource>();

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

        private static PlayerSpawnPoint CreateSpawnPoint(string name, string spawnId, Vector3 position)
        {
            var spawn = new GameObject(name).AddComponent<PlayerSpawnPoint>();
            spawn.transform.position = position;
            var spawnIdField = typeof(PlayerSpawnPoint).GetField(
                "spawnId",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            spawnIdField.SetValue(spawn, spawnId);
            return spawn;
        }

        private sealed class TestInputSource : MonoBehaviour, IPlayerInputSource
        {
            public PlayerInputSnapshot Snapshot { get; set; }

            public PlayerInputSnapshot ReadInput()
            {
                return Snapshot;
            }
        }
    }
}
