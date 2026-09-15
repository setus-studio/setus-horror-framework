using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Setus.HorrorFramework.Atmosphere.Camera;
using Setus.HorrorFramework.Atmosphere.Lighting;
using Setus.HorrorFramework.Atmosphere.PostProcessing;
using Setus.HorrorFramework.Atmosphere.Scares;
using Setus.HorrorFramework.Core.Config;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Player.Camera;
using Setus.HorrorFramework.Player.Controller;
using Setus.HorrorFramework.Player.State;
using Setus.HorrorFramework.SaveProgression.Registry;
using Setus.HorrorFramework.SceneFlow.Transitions;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Setus.HorrorFramework.Tests.PlayMode.Atmosphere
{
    public sealed class AtmosphereM7IntegrationTests
    {
        private readonly List<Object> createdObjects = new List<Object>();
        private readonly List<Scene> createdScenes = new List<Scene>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (var i = 0; i < createdScenes.Count; i++)
            {
                if (createdScenes[i].IsValid() && createdScenes[i].isLoaded)
                {
                    var unload = SceneManager.UnloadSceneAsync(createdScenes[i]);
                    if (unload != null)
                    {
                        yield return unload;
                    }
                }
            }

            createdScenes.Clear();
            for (var i = 0; i < createdObjects.Count; i++)
            {
                if (createdObjects[i] != null)
                {
                    Object.Destroy(createdObjects[i]);
                }
            }

            createdObjects.Clear();
            yield return null;
            HorrorGameContext.ShutdownActive();
        }

        [UnityTest]
        public IEnumerator PlayingScareResetsCameraLightAndVolumeWhenConsumed()
        {
            var context = CreateContext();
            var definition = Track(ScriptableObject.CreateInstance<ScareDefinition>());
            SetPrivateField(definition, "scareId", "m7.integration.scare");
            SetPrivateField(definition, "tensionIntensity", 0.8f);
            context.Scares.Configure(new[] { definition });

            var cameraObject = Track(new GameObject("M7 Test Camera"));
            var camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.fieldOfView = 60f;
            cameraObject.AddComponent<FirstPersonCameraRig>();
            var lightObject = Track(new GameObject("M7 Test Light"));
            var light = lightObject.AddComponent<Light>();
            light.intensity = 2f;
            var volumeObject = Track(new GameObject("M7 Test Volume"));
            var volume = volumeObject.AddComponent<Volume>();
            volume.weight = 0.2f;

            var presentation = Track(new GameObject("M7 Test Presentation"));
            presentation.SetActive(false);
            var cameraHook = presentation.AddComponent<ScareCameraReactionCoordinator>();
            SetPrivateField(cameraHook, "scareId", definition.ScareId);
            SetPrivateField(cameraHook, "targetCamera", camera);
            SetPrivateField(cameraHook, "fovKick", 4f);
            var lightHook = presentation.AddComponent<ScareLightFlickerHook>();
            SetPrivateField(lightHook, "scareId", definition.ScareId);
            SetPrivateField(lightHook, "affectedLights", new[] { light });
            var volumeHook = presentation.AddComponent<ScareVolumeReactionHook>();
            SetPrivateField(volumeHook, "scareId", definition.ScareId);
            SetPrivateField(volumeHook, "volume", volume);
            SetPrivateField(volumeHook, "activeWeight", 0.7f);
            presentation.SetActive(true);

            Assert.That(context.Scares.TryTrigger(definition.ScareId), Is.True);
            yield return null;
            Assert.That(context.Scares.TryGetState(definition.ScareId, out var playing), Is.True);
            Assert.That(playing, Is.EqualTo(ScareLifecycleState.Playing));
            Assert.That(camera.fieldOfView, Is.EqualTo(64f).Within(0.001f));
            Assert.That(volume.weight, Is.EqualTo(0.7f).Within(0.001f));

            Assert.That(context.Scares.Complete(definition.ScareId), Is.True);
            yield return null;
            Assert.That(context.Scares.TryGetState(definition.ScareId, out var consumed), Is.True);
            Assert.That(consumed, Is.EqualTo(ScareLifecycleState.Consumed));
            Assert.That(camera.fieldOfView, Is.EqualTo(60f).Within(0.001f));
            Assert.That(light.intensity, Is.EqualTo(2f).Within(0.001f));
            Assert.That(volume.weight, Is.EqualTo(0.2f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator CameraReactionComposesWithPostureAndHeadBobAndResetsOnlyItsContribution()
        {
            var context = CreateContext();
            var definition = CreateDefinition("m7.integration.camera-composition", 0.8f);
            context.Scares.Configure(new[] { definition });

            var cameraObject = Track(new GameObject("M7 Composed Player Camera"));
            cameraObject.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            var camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.fieldOfView = 60f;
            var cameraRig = cameraObject.AddComponent<FirstPersonCameraRig>();
            var postureOffset = Vector3.down * 0.6f;
            var headBobOffset = Vector3.up * 0.02f;
            cameraRig.SetPostureOffset(postureOffset);
            cameraRig.SetHeadBobOffset(headBobOffset);
            var composedWithoutReaction = cameraObject.transform.localPosition;

            var presentation = Track(new GameObject("M7 Composed Camera Reaction"));
            presentation.SetActive(false);
            var cameraHook = presentation.AddComponent<ScareCameraReactionCoordinator>();
            SetPrivateField(cameraHook, "scareId", definition.ScareId);
            SetPrivateField(cameraHook, "targetCamera", camera);
            SetPrivateField(cameraHook, "shakeDistance", 0.1f);
            SetPrivateField(cameraHook, "fovKick", 4f);
            presentation.SetActive(true);

            Assert.That(context.Scares.TryTrigger(definition.ScareId), Is.True);
            yield return null;

            var firstReactionOffset = cameraObject.transform.localPosition - composedWithoutReaction;
            Assert.That(firstReactionOffset.sqrMagnitude, Is.GreaterThan(0.000001f));
            Assert.That(camera.fieldOfView, Is.EqualTo(64f).Within(0.001f));

            postureOffset = Vector3.down * 0.5f;
            headBobOffset = Vector3.up * 0.03f;
            cameraRig.SetPostureOffset(postureOffset);
            cameraRig.SetHeadBobOffset(headBobOffset);
            var expectedBase = new Vector3(0f, 1.65f, 0f) + postureOffset + headBobOffset;
            Assert.That(
                Vector3.Distance(cameraObject.transform.localPosition, expectedBase + firstReactionOffset),
                Is.LessThan(0.0001f));

            Assert.That(context.Scares.Complete(definition.ScareId), Is.True);
            Assert.That(Vector3.Distance(cameraObject.transform.localPosition, expectedBase), Is.LessThan(0.0001f));
            Assert.That(camera.fieldOfView, Is.EqualTo(60f).Within(0.001f));

            cameraHook.enabled = false;
            Assert.That(Vector3.Distance(cameraObject.transform.localPosition, expectedBase), Is.LessThan(0.0001f));
        }

        [UnityTest]
        public IEnumerator RepeatedPlayingEventsDoNotAccumulateCameraReaction()
        {
            var context = CreateContext();
            const string scareId = "m7.integration.camera-repeat";
            var cameraObject = Track(new GameObject("M7 Repeated Reaction Camera"));
            cameraObject.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            var camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.fieldOfView = 60f;
            cameraObject.AddComponent<FirstPersonCameraRig>();

            var presentation = Track(new GameObject("M7 Repeated Camera Reaction"));
            presentation.SetActive(false);
            var cameraHook = presentation.AddComponent<ScareCameraReactionCoordinator>();
            SetPrivateField(cameraHook, "scareId", scareId);
            SetPrivateField(cameraHook, "targetCamera", camera);
            SetPrivateField(cameraHook, "shakeDistance", 0.1f);
            SetPrivateField(cameraHook, "fovKick", 4f);
            presentation.SetActive(true);

            context.Events.Publish(new ScareStateChanged(scareId, ScareLifecycleState.Playing, false));
            context.Events.Publish(new ScareStateChanged(scareId, ScareLifecycleState.Playing, false));
            yield return null;

            var displacement = cameraObject.transform.localPosition - new Vector3(0f, 1.65f, 0f);
            Assert.That(Mathf.Abs(displacement.x), Is.LessThanOrEqualTo(0.1001f));
            Assert.That(Mathf.Abs(displacement.y), Is.LessThanOrEqualTo(0.1001f));
            Assert.That(camera.fieldOfView, Is.EqualTo(64f).Within(0.001f));

            cameraHook.enabled = false;
            Assert.That(cameraObject.transform.localPosition, Is.EqualTo(new Vector3(0f, 1.65f, 0f)));
            Assert.That(camera.fieldOfView, Is.EqualTo(60f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator DisablingAndReenablingPresentationConsumesPlayingScareExactlyOnce()
        {
            var context = CreateContext();
            const string powerStateId = "m7.integration.disable.power";
            var definition = CreateDefinition("m7.integration.disable", 0.8f, powerStateId);
            context.Scares.Configure(new[] { definition });
            var lightObject = Track(new GameObject("M7 Disable Power Light"));
            var powerLight = lightObject.AddComponent<Light>();
            var presentation = CreatePresentationDriver(
                definition.ScareId,
                "M7 Disable Presentation",
                target =>
                {
                    var powerHook = target.AddComponent<ScarePowerStateHook>();
                    SetPrivateField(powerHook, "powerStateId", powerStateId);
                    SetPrivateField(powerHook, "affectedLights", new[] { powerLight });
                });
            var consumedEvents = 0;
            var subscription = context.Events.Subscribe<ScareStateChanged>(changed =>
            {
                if (changed.ScareId == definition.ScareId && changed.State == ScareLifecycleState.Consumed)
                {
                    consumedEvents++;
                }
            });

            Assert.That(context.Scares.TryTrigger(definition.ScareId), Is.True);
            Assert.That(powerLight.enabled, Is.False);
            presentation.SetActive(false);
            Assert.That(powerLight.enabled, Is.True);
            presentation.SetActive(true);
            yield return null;

            subscription.Dispose();
            AssertConsumed(context, definition.ScareId);
            Assert.That(context.Tension.CurrentIntensity, Is.Zero);
            Assert.That(consumedEvents, Is.EqualTo(1));
            Assert.That(context.Scares.TryTrigger(definition.ScareId), Is.False);
        }

        [UnityTest]
        public IEnumerator SceneUnloadAndReplacementPresentationCannotLeaveOrResumePlayingScare()
        {
            var context = CreateContext();
            var definition = CreateDefinition("m7.integration.scene-unload", 0.7f);
            context.Scares.Configure(new[] { definition });
            var cameraObject = Track(new GameObject("M7 Scene Unload Camera"));
            cameraObject.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            var camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.fieldOfView = 60f;
            cameraObject.AddComponent<FirstPersonCameraRig>();
            var sceneName = $"M7 Interruption {Guid.NewGuid():N}";
            var presentationScene = CreateTemporaryScene(sceneName);
            var presentation = CreatePresentationDriver(
                definition.ScareId,
                "M7 Scene Presentation",
                target =>
                {
                    var cameraHook = target.AddComponent<ScareCameraReactionCoordinator>();
                    SetPrivateField(cameraHook, "scareId", definition.ScareId);
                    SetPrivateField(cameraHook, "targetCamera", camera);
                    SetPrivateField(cameraHook, "shakeDistance", 0.1f);
                    SetPrivateField(cameraHook, "fovKick", 4f);
                });
            SceneManager.MoveGameObjectToScene(presentation, presentationScene);
            var consumedEvents = 0;
            var subscription = context.Events.Subscribe<ScareStateChanged>(changed =>
            {
                if (changed.ScareId == definition.ScareId && changed.State == ScareLifecycleState.Consumed)
                {
                    consumedEvents++;
                }
            });

            Assert.That(context.Scares.TryTrigger(definition.ScareId), Is.True);
            yield return null;
            Assert.That(camera.fieldOfView, Is.EqualTo(64f).Within(0.001f));
            var unload = SceneManager.UnloadSceneAsync(presentationScene);
            Assert.That(unload, Is.Not.Null);
            yield return unload;

            AssertConsumed(context, definition.ScareId);
            Assert.That(context.Tension.CurrentIntensity, Is.Zero);
            Assert.That(cameraObject.transform.localPosition, Is.EqualTo(new Vector3(0f, 1.65f, 0f)));
            Assert.That(camera.fieldOfView, Is.EqualTo(60f).Within(0.001f));

            var replacementScene = CreateTemporaryScene(sceneName);
            var replacement = CreatePresentationDriver(definition.ScareId, "M7 Replacement Presentation");
            SceneManager.MoveGameObjectToScene(replacement, replacementScene);
            yield return null;

            AssertConsumed(context, definition.ScareId);
            Assert.That(consumedEvents, Is.EqualTo(1));
            subscription.Dispose();
            var replacementUnload = SceneManager.UnloadSceneAsync(replacementScene);
            Assert.That(replacementUnload, Is.Not.Null);
            yield return replacementUnload;
        }

        [UnityTest]
        public IEnumerator SceneTransitionConsumesActiveScareBeforePresentationUnload()
        {
            var context = CreateContext();
            var definition = CreateDefinition("m7.integration.transition", 0.9f);
            context.Scares.Configure(new[] { definition });
            CreatePresentationDriver(definition.ScareId, "M7 Transition Presentation");

            Assert.That(context.Scares.TryTrigger(definition.ScareId), Is.True);
            context.Transitions.BeginTransition(new SceneTransitionRequest(
                "M7 Next Scene",
                showLoadingScreen: false));
            yield return null;

            AssertConsumed(context, definition.ScareId);
            Assert.That(context.Tension.CurrentIntensity, Is.Zero);
            context.Transitions.CompleteTransition();
        }

        [UnityTest]
        public IEnumerator PresentationEnabledAfterOrphanedPlayingStateConsumesItImmediately()
        {
            var context = CreateContext();
            var definition = CreateDefinition("m7.integration.late-presentation", 0.6f);
            context.Scares.Configure(new[] { definition });
            Assert.That(context.Scares.TryTrigger(definition.ScareId), Is.True);

            CreatePresentationDriver(definition.ScareId, "M7 Late Presentation");
            yield return null;

            AssertConsumed(context, definition.ScareId);
            Assert.That(context.Tension.CurrentIntensity, Is.Zero);
        }

        [UnityTest]
        public IEnumerator SceneCatalogUnloadRestoreAndReentryPreservesConsumedScare()
        {
            var context = CreateContext();
            var scareA = CreateDefinition("m7.integration.catalog-a", 0.6f);
            var scareB = CreateDefinition("m7.integration.catalog-b", 0.7f);
            var sceneAName = $"M7 Catalog A {Guid.NewGuid():N}";
            var sceneA = CreateTemporaryScene(sceneAName);
            CreateScenarioAdapter(sceneA, "M7 Catalog A", scareA);

            Assert.That(context.Scares.TryTrigger(scareA.ScareId), Is.True);
            Assert.That(context.Scares.Complete(scareA.ScareId), Is.True);
            var unloadA = SceneManager.UnloadSceneAsync(sceneA);
            Assert.That(unloadA, Is.Not.Null);
            yield return unloadA;

            Assert.That(context.Scares.TryGetState(scareA.ScareId, out var consumed), Is.True);
            Assert.That(consumed, Is.EqualTo(ScareLifecycleState.Consumed));
            Assert.That(context.Scares.TryTrigger(scareA.ScareId), Is.False);

            var sceneB = CreateTemporaryScene($"M7 Catalog B {Guid.NewGuid():N}");
            CreateScenarioAdapter(sceneB, "M7 Catalog B", scareB);
            var captured = context.Scares.CaptureState();
            context.Scares.RestoreState(captured);

            var recreatedA = CreateTemporaryScene(sceneAName);
            CreateScenarioAdapter(recreatedA, "M7 Catalog A Recreated", scareA);
            yield return null;

            Assert.That(context.Scares.TryGetState(scareA.ScareId, out consumed), Is.True);
            Assert.That(consumed, Is.EqualTo(ScareLifecycleState.Consumed));
            Assert.That(context.Scares.TryTrigger(scareA.ScareId), Is.False);
            Assert.That(context.Scares.TryTrigger(scareB.ScareId), Is.True);
        }

        private HorrorGameContext CreateContext()
        {
            HorrorGameContext.ShutdownActive();
            var config = Track(ScriptableObject.CreateInstance<HorrorFrameworkConfig>());
            var context = HorrorGameContext.Initialize(config);
            context.Saveables.RegisterGlobal(new TestPlayerPoseOwner());
            return context;
        }

        private ScareDefinition CreateDefinition(string scareId, float tension, string powerStateId = "")
        {
            var definition = Track(ScriptableObject.CreateInstance<ScareDefinition>());
            SetPrivateField(definition, "scareId", scareId);
            SetPrivateField(definition, "tensionIntensity", tension);
            SetPrivateField(definition, "powerStateId", powerStateId);
            return definition;
        }

        private GameObject CreatePresentationDriver(
            string scareId,
            string name,
            Action<GameObject> configureBeforeEnable = null)
        {
            var presentation = Track(new GameObject(name));
            presentation.SetActive(false);
            var driver = presentation.AddComponent<ScareLifecyclePresentationDriver>();
            SetPrivateField(driver, "scareId", scareId);
            configureBeforeEnable?.Invoke(presentation);
            presentation.SetActive(true);
            return presentation;
        }

        private Scene CreateTemporaryScene(string sceneName)
        {
            var scene = SceneManager.CreateScene(sceneName);
            createdScenes.Add(scene);
            return scene;
        }

        private GameObject CreateScenarioAdapter(Scene scene, string name, params ScareDefinition[] definitions)
        {
            var scenario = Track(new GameObject(name));
            scenario.SetActive(false);
            var adapter = scenario.AddComponent<ScareScenarioAdapter>();
            SetPrivateField(adapter, "scares", definitions);
            SceneManager.MoveGameObjectToScene(scenario, scene);
            scenario.SetActive(true);
            return scenario;
        }

        private static void AssertConsumed(HorrorGameContext context, string scareId)
        {
            Assert.That(context.Scares.TryGetState(scareId, out var state), Is.True);
            Assert.That(state, Is.EqualTo(ScareLifecycleState.Consumed));
            Assert.That(context.Scares.CaptureState().Scares.Single().State,
                Is.EqualTo(ScareLifecycleState.Consumed));
        }

        private T Track<T>(T target) where T : Object
        {
            createdObjects.Add(target);
            return target;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
            field.SetValue(target, value);
        }

        private sealed class TestPlayerPoseOwner : RuntimeStateOwnerBase<PlayerPose>
        {
            public TestPlayerPoseOwner()
                : base("player.pose")
            {
            }

            public override PlayerPose CaptureState()
            {
                return new PlayerPose(Vector3.zero, Quaternion.identity, 0f, PlayerControlState.Normal);
            }

            public override void RestoreState(PlayerPose state)
            {
            }
        }
    }
}
