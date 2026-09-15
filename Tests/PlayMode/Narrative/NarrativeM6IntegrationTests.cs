using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Setus.HorrorFramework.Core.Config;
using Setus.HorrorFramework.Core.Events;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Interaction.Interactors;
using Setus.HorrorFramework.Interaction.Prompts;
using Setus.HorrorFramework.Interaction.Triggers;
using Setus.HorrorFramework.Narrative.Gates;
using Setus.HorrorFramework.Narrative.Notes;
using Setus.HorrorFramework.Narrative.Objectives;
using Setus.HorrorFramework.Narrative.Phone;
using Setus.HorrorFramework.Player.Controller;
using Setus.HorrorFramework.Player.State;
using Setus.HorrorFramework.SaveProgression.Registry;
using Setus.HorrorFramework.SaveProgression.SaveSlots;
using Setus.HorrorFramework.SaveProgression.StableIds;
using Setus.HorrorFramework.UI.Objectives;
using Setus.HorrorFramework.UI.Phone;
using Setus.HorrorFramework.UI.Subtitles;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Setus.HorrorFramework.Tests.PlayMode.Narrative
{
    public sealed class NarrativeM6IntegrationTests
    {
        private readonly List<Object> createdObjects = new List<Object>();
        private readonly List<Scene> createdScenes = new List<Scene>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (var i = 0; i < createdScenes.Count; i++)
            {
                var scene = createdScenes[i];
                if (scene.IsValid() && scene.isLoaded)
                {
                    var unload = SceneManager.UnloadSceneAsync(scene);
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
        public IEnumerator StoryTrigger_ReentryAfterSceneRecreationAndRestore_DoesNotAdvanceTwice()
        {
            var context = CreateContext();
            var fixture = ConfigureScenario(context);
            var firstScene = CreateTestScene("M6 Trigger First");
            var enteringObject = CreateEnteringObject(firstScene, new Vector3(0f, 0f, 3f));
            var firstTrigger = CreateStoryBeatTrigger(firstScene, fixture.TriggerId, fixture.StoryBeatId);
            var arrivalCompletionCount = 0;
            var subscription = context.Events.Subscribe<ObjectiveCompleted>(completed =>
            {
                if (completed.ObjectiveId == fixture.ArrivalObjectiveId)
                {
                    arrivalCompletionCount++;
                }
            });

            yield return EnterAndExitTrigger(enteringObject, Vector3.zero);
            yield return EnterAndExitTrigger(enteringObject, Vector3.zero);

            Assert.That(arrivalCompletionCount, Is.EqualTo(1));
            Assert.That(context.Narrative.HasConsumedStoryBeat(fixture.StoryBeatId), Is.True);
            Assert.That(context.Objectives.IsComplete(fixture.ArrivalObjectiveId), Is.True);
            Assert.That(context.Objectives.ActiveObjectiveId, Is.EqualTo(fixture.PhoneObjectiveId));
            Assert.That(((InteractionTriggerState)firstTrigger.CaptureState()).HasFired, Is.True);

            var snapshot = context.Saves.Capture(new SaveSlotId("m6-trigger-reentry"));
            yield return UnloadTestScene(firstScene);

            var recreatedScene = CreateTestScene("M6 Trigger Recreated");
            var recreatedEnteringObject = CreateEnteringObject(recreatedScene, new Vector3(0f, 0f, 3f));
            var recreatedTrigger = CreateStoryBeatTrigger(recreatedScene, fixture.TriggerId, fixture.StoryBeatId);
            context.Saves.Restore(snapshot);
            yield return null;

            arrivalCompletionCount = 0;
            yield return EnterAndExitTrigger(recreatedEnteringObject, Vector3.zero);

            Assert.That(arrivalCompletionCount, Is.EqualTo(0));
            Assert.That(context.Objectives.CaptureState().CompletedObjectiveIds.Count, Is.EqualTo(1));
            Assert.That(context.Objectives.ActiveObjectiveId, Is.EqualTo(fixture.PhoneObjectiveId));
            Assert.That(((InteractionTriggerState)recreatedTrigger.CaptureState()).HasFired, Is.True);

            subscription.Dispose();
        }

        [UnityTest]
        public IEnumerator PhoneNoteRouteAndPresenters_RestoreLiveNarrativeAndObjectiveState()
        {
            var context = CreateContext();
            var fixture = ConfigureScenario(context);
            var objectiveRoot = Track(new GameObject("M6 Objective Root"));
            var objectiveTitle = CreateText("M6 Objective Title");
            var objectiveDescription = CreateText("M6 Objective Description");
            CreateObjectivePresenter(objectiveRoot, objectiveTitle, objectiveDescription);

            var phoneRoot = Track(new GameObject("M6 Phone Root"));
            phoneRoot.SetActive(false);
            var phoneSender = CreateText("M6 Phone Sender");
            var phoneMessage = CreateText("M6 Phone Message");
            var phoneStatus = CreateText("M6 Phone Status");
            var phonePresenter = CreatePhonePresenter(phoneRoot, phoneSender, phoneMessage, phoneStatus);

            var subtitleRoot = Track(new GameObject("M6 Subtitle Root"));
            subtitleRoot.SetActive(false);
            var subtitleSpeaker = CreateText("M6 Subtitle Speaker");
            var subtitleText = CreateText("M6 Subtitle Text");
            CreateSubtitlePresenter(subtitleRoot, subtitleSpeaker, subtitleText);

            var routeUnlocker = Track(new GameObject("M6 Route Unlocker"));
            routeUnlocker.SetActive(false);
            var unlocker = routeUnlocker.AddComponent<NarrativeRouteUnlocker>();
            SetPrivateField(unlocker, "completedObjectiveId", fixture.NoteObjectiveId);
            SetPrivateField(unlocker, "routeId", fixture.RouteId);
            routeUnlocker.SetActive(true);

            var barrierObject = Track(new GameObject("M6 Route Barrier"));
            barrierObject.SetActive(false);
            var lockedVisual = Track(new GameObject("M6 Route Locked Visual"));
            var barrier = barrierObject.AddComponent<NarrativeRouteBarrier>();
            SetPrivateField(barrier, "routeId", fixture.RouteId);
            SetPrivateField(barrier, "lockedVisual", lockedVisual);
            barrierObject.SetActive(true);

            var phoneObject = Track(new GameObject("M6 Phone Interactable"));
            phoneObject.SetActive(false);
            phoneObject.AddComponent<BoxCollider>();
            var phone = phoneObject.AddComponent<PhoneMessageInteractable>();
            SetPrivateField(phone, "message", fixture.PhoneDefinition);
            phoneObject.SetActive(true);

            var noteObject = Track(new GameObject("M6 Note Interactable"));
            noteObject.SetActive(false);
            noteObject.AddComponent<BoxCollider>();
            var note = noteObject.AddComponent<NarrativeNoteInteractable>();
            SetPrivateField(note, "note", fixture.NoteDefinition);
            noteObject.SetActive(true);

            yield return null;
            Assert.That(objectiveRoot.activeSelf, Is.True);
            Assert.That(objectiveTitle.text, Is.EqualTo("Arrive"));
            Assert.That(lockedVisual.activeSelf, Is.True);

            context.Events.Publish(new GameplaySignalRaised("m6.integration.wrong-gate", null));
            yield return null;
            Assert.That(context.Objectives.ActiveObjectiveId, Is.EqualTo(fixture.ArrivalObjectiveId));

            Assert.That(context.Narrative.TryConsumeStoryBeat(fixture.StoryBeatId), Is.True);
            yield return null;
            Assert.That(context.Objectives.ActiveObjectiveId, Is.EqualTo(fixture.PhoneObjectiveId));
            Assert.That(objectiveTitle.text, Is.EqualTo("Read phone"));

            var interactionContext = new InteractionContext(phoneObject, context);
            Assert.That(phone.Interact(interactionContext).Success, Is.True);
            yield return null;
            Assert.That(phoneRoot.activeSelf, Is.True);
            Assert.That(phoneSender.text, Is.EqualTo("Maintenance"));
            Assert.That(phoneStatus.text, Is.EqualTo("New message"));

            Assert.That(phone.Interact(interactionContext).Success, Is.True);
            yield return null;
            Assert.That(context.Objectives.ActiveObjectiveId, Is.EqualTo(fixture.NoteObjectiveId));
            Assert.That(phoneStatus.text, Is.EqualTo("Read"));

            Assert.That(phone.Interact(interactionContext).Success, Is.True);
            yield return null;
            Assert.That(phoneStatus.text, Is.EqualTo("Replied"));

            Assert.That(note.Interact(new InteractionContext(noteObject, context)).Success, Is.True);
            yield return null;
            Assert.That(subtitleRoot.activeSelf, Is.True);
            Assert.That(subtitleSpeaker.text, Is.EqualTo("Maintenance Log"));
            Assert.That(context.Narrative.IsRouteUnlocked(fixture.RouteId), Is.True);
            Assert.That(lockedVisual.activeSelf, Is.False);
            Assert.That(context.Objectives.ActiveObjectiveId, Is.Empty);

            Assert.That(context.Narrative.SetBranchFlag(fixture.BranchFlagId, true), Is.True);
            var snapshot = context.Saves.Capture(new SaveSlotId("m6-phone-note-route"));
            context.ResetForNewGameSession();
            yield return null;
            Assert.That(lockedVisual.activeSelf, Is.True);

            context.Saves.Restore(snapshot);
            yield return null;

            Assert.That(context.Objectives.IsComplete(fixture.ArrivalObjectiveId), Is.True);
            Assert.That(context.Objectives.IsComplete(fixture.PhoneObjectiveId), Is.True);
            Assert.That(context.Objectives.IsComplete(fixture.NoteObjectiveId), Is.True);
            Assert.That(context.Objectives.IsComplete(fixture.RouteObjectiveId), Is.True);
            Assert.That(context.Narrative.HasConsumedStoryBeat(fixture.StoryBeatId), Is.True);
            Assert.That(context.Narrative.TryGetPhoneProgress(fixture.PhoneMessageId, out var phoneProgress), Is.True);
            Assert.That(phoneProgress.Delivered && phoneProgress.Read && phoneProgress.Replied, Is.True);
            Assert.That(context.Narrative.HasReadNote(fixture.NoteId), Is.True);
            Assert.That(context.Narrative.IsRouteUnlocked(fixture.RouteId), Is.True);
            Assert.That(context.Narrative.TryGetBranchFlag(fixture.BranchFlagId, out var branchValue), Is.True);
            Assert.That(branchValue, Is.True);
            Assert.That(lockedVisual.activeSelf, Is.False);
            Assert.That(phoneRoot.activeSelf, Is.True);
            Assert.That(phoneStatus.text, Is.EqualTo("Replied"));

            phonePresenter.enabled = false;
            phoneRoot.SetActive(false);
            Assert.That(context.Narrative.TryDeliverPhoneMessage(fixture.FollowUpMessageId), Is.True);
            yield return null;
            Assert.That(phoneRoot.activeSelf, Is.False);

            phonePresenter.enabled = true;
            yield return null;
            Assert.That(phoneRoot.activeSelf, Is.True);
            Assert.That(phoneSender.text, Is.EqualTo("Security"));

            Object.Destroy(phonePresenter.gameObject);
            yield return null;
            phoneRoot.SetActive(false);
            Assert.That(context.Narrative.TryReadPhoneMessage(fixture.FollowUpMessageId), Is.True);
            yield return null;
            Assert.That(phoneRoot.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator RaycastPhoneFocus_IsBlockedUntilThePlayerReturnsToNormalControlState()
        {
            var context = CreateContext();
            var fixture = ConfigureScenario(context);
            var player = Track(new GameObject("M6 Interaction Player"));
            player.SetActive(false);
            var controlState = player.AddComponent<TestControlStateTarget>();
            controlState.SetControlState(PlayerControlState.Cutscene);
            var cameraObject = Track(new GameObject("M6 Interaction Camera"));
            cameraObject.transform.SetParent(player.transform);
            cameraObject.transform.localPosition = Vector3.zero;
            cameraObject.transform.localRotation = Quaternion.identity;
            var interactor = player.AddComponent<RaycastInteractor>();
            interactor.ConfigureRayOrigin(cameraObject.transform);
            player.SetActive(true);

            var phoneObject = Track(new GameObject("M6 Raycast Phone"));
            phoneObject.SetActive(false);
            phoneObject.transform.position = new Vector3(0f, 0f, 1.5f);
            phoneObject.AddComponent<BoxCollider>();
            var phone = phoneObject.AddComponent<PhoneMessageInteractable>();
            SetPrivateField(phone, "message", fixture.PhoneDefinition);
            phoneObject.SetActive(true);

            var receivedInteractivePrompt = false;
            var subscription = context.Events.Subscribe<InteractionPromptChanged>(changed =>
            {
                receivedInteractivePrompt |= changed.Prompt.CanInteract;
            });

            Physics.SyncTransforms();
            yield return null;
            Assert.That(receivedInteractivePrompt, Is.False);

            controlState.SetControlState(PlayerControlState.Normal);
            Physics.SyncTransforms();
            yield return null;

            Assert.That(receivedInteractivePrompt, Is.True);
            subscription.Dispose();
        }

        private HorrorGameContext CreateContext()
        {
            HorrorGameContext.ShutdownActive();
            var config = Track(ScriptableObject.CreateInstance<HorrorFrameworkConfig>());
            var context = HorrorGameContext.Initialize(config);
            context.Saveables.RegisterGlobal(new TestPlayerPoseOwner());
            return context;
        }

        private NarrativeFixture ConfigureScenario(HorrorGameContext context)
        {
            var fixture = new NarrativeFixture
            {
                TriggerId = "m6.integration.trigger",
                StoryBeatId = "m6.integration.arrival",
                PhoneMessageId = "m6.integration.phone.return",
                FollowUpMessageId = "m6.integration.phone.security",
                NoteId = "m6.integration.note.log",
                RouteId = "m6.integration.route.hallway",
                BranchFlagId = "m6.integration.branch.arrived",
                ArrivalObjectiveId = "m6.integration.objective.arrival",
                PhoneObjectiveId = "m6.integration.objective.phone",
                NoteObjectiveId = "m6.integration.objective.note",
                RouteObjectiveId = "m6.integration.objective.route"
            };

            fixture.PhoneDefinition = CreatePhoneDefinition(
                fixture.PhoneMessageId,
                "Maintenance",
                "Please read the maintenance log.",
                true,
                "Acknowledged.");
            var followUpDefinition = CreatePhoneDefinition(
                fixture.FollowUpMessageId,
                "Security",
                "Route clearance confirmed.",
                false,
                string.Empty);
            fixture.NoteDefinition = CreateNoteDefinition(
                fixture.NoteId,
                "Maintenance Log",
                "The hallway route is now safe.",
                10f);

            var arrival = CreateObjective(
                fixture.ArrivalObjectiveId,
                "Arrive",
                ObjectiveGateKind.StoryBeat,
                fixture.StoryBeatId,
                fixture.PhoneObjectiveId);
            var phone = CreateObjective(
                fixture.PhoneObjectiveId,
                "Read phone",
                ObjectiveGateKind.PhoneRead,
                fixture.PhoneMessageId,
                fixture.NoteObjectiveId);
            var note = CreateObjective(
                fixture.NoteObjectiveId,
                "Read log",
                ObjectiveGateKind.NoteRead,
                fixture.NoteId,
                fixture.RouteObjectiveId);
            var route = CreateObjective(
                fixture.RouteObjectiveId,
                "Unlock route",
                ObjectiveGateKind.RouteUnlocked,
                fixture.RouteId,
                string.Empty);

            context.Narrative.InitializeScenario("m6.integration.scenario");
            context.Narrative.ConfigurePhoneMessages(new[] { fixture.PhoneDefinition, followUpDefinition });
            context.Objectives.ConfigureScenario(
                "m6.integration.scenario",
                new[] { arrival, phone, note, route },
                fixture.ArrivalObjectiveId);
            return fixture;
        }

        private Scene CreateTestScene(string name)
        {
            var scene = SceneManager.CreateScene($"{name}-{Guid.NewGuid():N}");
            createdScenes.Add(scene);
            return scene;
        }

        private IEnumerator UnloadTestScene(Scene scene)
        {
            var unload = SceneManager.UnloadSceneAsync(scene);
            Assert.That(unload, Is.Not.Null);
            yield return unload;
        }

        private InteractionTriggerZone CreateStoryBeatTrigger(Scene scene, string triggerId, string storyBeatId)
        {
            var triggerObject = Track(new GameObject("M6 Story Trigger"));
            triggerObject.SetActive(false);
            SceneManager.MoveGameObjectToScene(triggerObject, scene);
            var collider = triggerObject.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            triggerObject.AddComponent<StableId>().Assign(triggerId);
            var trigger = triggerObject.AddComponent<InteractionTriggerZone>();
            var storyBeat = triggerObject.AddComponent<NarrativeStoryBeatTrigger>();
            SetPrivateField(storyBeat, "triggerId", triggerId);
            SetPrivateField(storyBeat, "storyBeatId", storyBeatId);
            triggerObject.SetActive(true);
            return trigger;
        }

        private GameObject CreateEnteringObject(Scene scene, Vector3 position)
        {
            var enteringObject = Track(new GameObject("M6 Trigger Entering Object"));
            SceneManager.MoveGameObjectToScene(enteringObject, scene);
            enteringObject.AddComponent<SphereCollider>();
            var body = enteringObject.AddComponent<Rigidbody>();
            body.useGravity = false;
            enteringObject.transform.position = position;
            return enteringObject;
        }

        private static IEnumerator EnterAndExitTrigger(GameObject enteringObject, Vector3 triggerPosition)
        {
            enteringObject.transform.position = triggerPosition;
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();

            enteringObject.transform.position = triggerPosition + new Vector3(0f, 0f, 3f);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
        }

        private PhoneMessageDefinition CreatePhoneDefinition(
            string messageId,
            string sender,
            string message,
            bool requiresReply,
            string reply)
        {
            var definition = Track(ScriptableObject.CreateInstance<PhoneMessageDefinition>());
            SetPrivateField(definition, "messageId", messageId);
            SetPrivateField(definition, "sender", sender);
            SetPrivateField(definition, "messageText", message);
            SetPrivateField(definition, "requiresReply", requiresReply);
            SetPrivateField(definition, "replyText", reply);
            return definition;
        }

        private NarrativeNoteDefinition CreateNoteDefinition(string noteId, string title, string body, float duration)
        {
            var definition = Track(ScriptableObject.CreateInstance<NarrativeNoteDefinition>());
            SetPrivateField(definition, "noteId", noteId);
            SetPrivateField(definition, "title", title);
            SetPrivateField(definition, "body", body);
            SetPrivateField(definition, "subtitleDuration", duration);
            return definition;
        }

        private ObjectiveDefinition CreateObjective(
            string objectiveId,
            string title,
            ObjectiveGateKind gateKind,
            string gateId,
            string nextObjectiveId)
        {
            var definition = Track(ScriptableObject.CreateInstance<ObjectiveDefinition>());
            SetPrivateField(definition, "objectiveId", objectiveId);
            SetPrivateField(definition, "title", title);
            SetPrivateField(definition, "description", title);
            SetPrivateField(definition, "completionGate", gateKind);
            SetPrivateField(definition, "completionGateId", gateId);
            SetPrivateField(definition, "nextObjectiveId", nextObjectiveId);
            return definition;
        }

        private ObjectivePresenter CreateObjectivePresenter(GameObject root, Text title, Text description)
        {
            var presenterObject = Track(new GameObject("M6 Objective Presenter"));
            presenterObject.SetActive(false);
            var presenter = presenterObject.AddComponent<ObjectivePresenter>();
            SetPrivateField(presenter, "root", root);
            SetPrivateField(presenter, "titleText", title);
            SetPrivateField(presenter, "descriptionText", description);
            presenterObject.SetActive(true);
            return presenter;
        }

        private PhoneMessagePresenter CreatePhonePresenter(GameObject root, Text sender, Text message, Text status)
        {
            var presenterObject = Track(new GameObject("M6 Phone Presenter"));
            presenterObject.SetActive(false);
            var presenter = presenterObject.AddComponent<PhoneMessagePresenter>();
            SetPrivateField(presenter, "root", root);
            SetPrivateField(presenter, "senderText", sender);
            SetPrivateField(presenter, "messageText", message);
            SetPrivateField(presenter, "statusText", status);
            presenterObject.SetActive(true);
            return presenter;
        }

        private SubtitlePresenter CreateSubtitlePresenter(GameObject root, Text speaker, Text text)
        {
            var presenterObject = Track(new GameObject("M6 Subtitle Presenter"));
            presenterObject.SetActive(false);
            var presenter = presenterObject.AddComponent<SubtitlePresenter>();
            SetPrivateField(presenter, "root", root);
            SetPrivateField(presenter, "speakerText", speaker);
            SetPrivateField(presenter, "subtitleText", text);
            presenterObject.SetActive(true);
            return presenter;
        }

        private Text CreateText(string name)
        {
            return Track(new GameObject(name)).AddComponent<Text>();
        }

        private T Track<T>(T target) where T : Object
        {
            createdObjects.Add(target);
            return target;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Expected field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private sealed class NarrativeFixture
        {
            public string TriggerId;
            public string StoryBeatId;
            public string PhoneMessageId;
            public string FollowUpMessageId;
            public string NoteId;
            public string RouteId;
            public string BranchFlagId;
            public string ArrivalObjectiveId;
            public string PhoneObjectiveId;
            public string NoteObjectiveId;
            public string RouteObjectiveId;
            public PhoneMessageDefinition PhoneDefinition;
            public NarrativeNoteDefinition NoteDefinition;
        }

        private sealed class TestPlayerPoseOwner : RuntimeStateOwnerBase<PlayerPose>
        {
            private PlayerPose pose = new PlayerPose(Vector3.zero, Quaternion.identity, 0f, PlayerControlState.Normal);

            public TestPlayerPoseOwner()
                : base(HorrorGlobalSaveStateRegistry.PlayerPoseStateKey)
            {
            }

            public override PlayerPose CaptureState()
            {
                return pose;
            }

            public override void RestoreState(PlayerPose state)
            {
                pose = state;
            }
        }

        private sealed class TestControlStateTarget : MonoBehaviour, IPlayerControlStateTarget
        {
            [SerializeField] private PlayerControlState controlState = PlayerControlState.Normal;

            public PlayerControlState CurrentControlState => controlState;

            public bool SetControlState(PlayerControlState state)
            {
                controlState = state;
                return true;
            }
        }
    }
}
