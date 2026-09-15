using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Setus.HorrorFramework.Core.Config;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Interaction.Interactors;
using Setus.HorrorFramework.Interaction.Prompts;
using Setus.HorrorFramework.Interaction.Triggers;
using Setus.HorrorFramework.Player.State;
using Setus.HorrorFramework.SaveProgression.StableIds;
using UnityEngine;
using UnityEngine.TestTools;

namespace Setus.HorrorFramework.Tests.PlayMode.Interaction
{
    public sealed class InteractionRuntimeIntegrationTests
    {
        private readonly List<Object> createdObjects = new List<Object>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
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
        public IEnumerator RaycastFocus_PublishesPromptForIntersectedInteractable()
        {
            CreateContext();
            var player = Track(new GameObject("interaction-test-player"));
            player.AddComponent<TestControlStateTarget>();
            var cameraObject = Track(new GameObject("interaction-test-camera"));
            cameraObject.transform.SetParent(player.transform);
            cameraObject.transform.localPosition = Vector3.zero;
            cameraObject.transform.localRotation = Quaternion.identity;
            var interactor = player.AddComponent<RaycastInteractor>();
            interactor.ConfigureRayOrigin(cameraObject.transform);

            var target = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            target.name = "interaction-test-target";
            target.transform.position = new Vector3(0f, 0f, 1.5f);
            target.AddComponent<TestInteractable>();
            InteractionPromptChanged lastPrompt = default;
            var receivedPrompt = false;
            var subscription = HorrorGameContext.Active.Events.Subscribe<InteractionPromptChanged>(changed =>
            {
                lastPrompt = changed;
                receivedPrompt = true;
            });

            Physics.SyncTransforms();
            yield return null;

            Assert.That(receivedPrompt, Is.True);
            Assert.That(lastPrompt.Prompt.Text, Is.EqualTo("Use test target"));
            Assert.That(lastPrompt.Prompt.CanInteract, Is.True);
            Assert.That(lastPrompt.TargetName, Is.EqualTo(target.name));

            subscription.Dispose();
        }

        [UnityTest]
        public IEnumerator TriggerEntry_FiresOnceAndRemainsFiredAfterExit()
        {
            CreateContext();
            var triggerObject = Track(new GameObject("interaction-test-trigger"));
            var triggerCollider = triggerObject.AddComponent<BoxCollider>();
            triggerCollider.isTrigger = true;
            triggerObject.AddComponent<StableId>().Assign("test.trigger.entry");
            var trigger = triggerObject.AddComponent<InteractionTriggerZone>();

            var enteringObject = Track(new GameObject("interaction-test-entering-object"));
            enteringObject.AddComponent<SphereCollider>();
            var body = enteringObject.AddComponent<Rigidbody>();
            body.useGravity = false;
            enteringObject.transform.position = new Vector3(0f, 0f, 3f);

            var fireCount = 0;
            var subscription = HorrorGameContext.Active.Events.Subscribe<InteractionTriggerEntered>(_ => fireCount++);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();

            enteringObject.transform.position = Vector3.zero;
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();

            enteringObject.transform.position = new Vector3(0f, 0f, 3f);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();

            Assert.That(fireCount, Is.EqualTo(1));
            Assert.That(((InteractionTriggerState)trigger.CaptureState()).HasFired, Is.True);

            subscription.Dispose();
        }

        private void CreateContext()
        {
            HorrorGameContext.ShutdownActive();
            var config = Track(ScriptableObject.CreateInstance<HorrorFrameworkConfig>());
            HorrorGameContext.Initialize(config);
        }

        private T Track<T>(T target) where T : Object
        {
            createdObjects.Add(target);
            return target;
        }

        private sealed class TestControlStateTarget : MonoBehaviour, IPlayerControlStateTarget
        {
            public PlayerControlState CurrentControlState => PlayerControlState.Normal;

            public bool SetControlState(PlayerControlState state)
            {
                return state == PlayerControlState.Normal;
            }
        }

        private sealed class TestInteractable : MonoBehaviour, IInteractable
        {
            public Transform PromptAnchor => transform;

            public InteractionPrompt GetPrompt(InteractionContext context)
            {
                return new InteractionPrompt("Use test target", true, InteractionType.Use);
            }

            public InteractionResult Interact(InteractionContext context)
            {
                return InteractionResult.Succeeded();
            }
        }
    }
}
