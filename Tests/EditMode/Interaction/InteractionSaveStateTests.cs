using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Interaction.Interactables;
using Setus.HorrorFramework.Interaction.Interactors;
using Setus.HorrorFramework.Interaction.Triggers;
using Setus.HorrorFramework.Player.Controller;
using Setus.HorrorFramework.Player.State;
using Setus.HorrorFramework.SaveProgression.Registry;
using Setus.HorrorFramework.SaveProgression.SaveSlots;
using Setus.HorrorFramework.SaveProgression.StableIds;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Setus.HorrorFramework.Tests.EditMode.Interaction
{
    public sealed class InteractionSaveStateTests
    {
        [SetUp]
        public void SetUp()
        {
            HorrorGameContext.ShutdownActive();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(GameObject.Find("door"));
            Object.DestroyImmediate(GameObject.Find("key"));
            Object.DestroyImmediate(GameObject.Find("trigger"));
            Object.DestroyImmediate(GameObject.Find("trigger-entering-object"));
            HorrorGameContext.ShutdownActive();
        }

        [Test]
        public void DoorInteractionStateCapturesAndRestoresOpenState()
        {
            var context = HorrorGameContext.Ensure();
            var door = CreateDoor("door", "door.front");
            var interactionContext = new InteractionContext(new GameObject("interactor"), context);

            var result = door.Interact(interactionContext);
            var state = (InteractionObjectState)door.CaptureState();

            Assert.IsTrue(result.Success);
            Assert.IsTrue(state.IsOpen);

            door.RestoreState(new InteractionObjectState(isOpen: false));
            Assert.IsFalse(((InteractionObjectState)door.CaptureState()).IsOpen);

            door.RestoreState(state);
            Assert.IsTrue(((InteractionObjectState)door.CaptureState()).IsOpen);
            Object.DestroyImmediate(interactionContext.Interactor);
        }

        [Test]
        public void PickupAddsInventoryAndRestoresConsumedStateThroughSaveOwner()
        {
            var context = CreateContextWithRequiredGlobalOwners();
            var pickup = CreatePickup("key", "pickup.key", "key.front");
            var interactionContext = new InteractionContext(new GameObject("interactor"), context);

            pickup.Interact(interactionContext);
            var snapshot = context.Saves.Capture(new SaveSlotId("debug"));

            Assert.IsTrue(context.Inventory.HasItem("key.front"));
            Assert.IsTrue(((InteractionObjectState)pickup.CaptureState()).IsConsumed);

            context.Inventory.ConsumeItem("key.front");
            pickup.RestoreState(new InteractionObjectState(isConsumed: false));

            context.Saves.Restore(snapshot);

            Assert.IsTrue(context.Inventory.HasItem("key.front"));
            Assert.IsTrue(((InteractionObjectState)pickup.CaptureState()).IsConsumed);
            Object.DestroyImmediate(interactionContext.Interactor);
        }

        [Test]
        public void LockedDoorUsesInventoryKeyWithoutConsumingWhenConfiguredToRetain()
        {
            var context = HorrorGameContext.Ensure();
            var door = CreateDoor("door", "door.locked");
            ConfigureBool(door, "isLocked", true);
            ConfigureNestedString(door, "lockRequirement", "requiredItemId", "key.front");
            ConfigureNestedBool(door, "lockRequirement", "consumeItemOnUnlock", false);
            context.Inventory.AddItem("key.front");

            var result = door.Interact(new InteractionContext(new GameObject("interactor"), context));
            var state = (InteractionObjectState)door.CaptureState();

            Assert.IsTrue(result.Success);
            Assert.IsFalse(state.IsLocked);
            Assert.IsTrue(state.IsOpen);
            Assert.IsTrue(context.Inventory.HasItem("key.front"));
            Object.DestroyImmediate(GameObject.Find("interactor"));
        }

        [Test]
        public void MissingKeyReturnsRejectionReason()
        {
            var context = HorrorGameContext.Ensure();
            var door = CreateDoor("door", "door.locked");
            ConfigureBool(door, "isLocked", true);
            ConfigureNestedString(door, "lockRequirement", "requiredItemId", "key.front");

            var prompt = door.GetPrompt(new InteractionContext(new GameObject("interactor"), context));

            Assert.IsFalse(prompt.CanInteract);
            Assert.AreEqual(InteractionRejectionReason.MissingItem, prompt.RejectionReason);
            Object.DestroyImmediate(GameObject.Find("interactor"));
        }

        [Test]
        public void DisabledButRequiredConsumedPickupRemainsInCapturedStableStates()
        {
            var context = CreateContextWithRequiredGlobalOwners();
            var pickup = CreatePickup("key", "pickup.key", "key.front");
            var interactionContext = new InteractionContext(new GameObject("interactor"), context);
            pickup.Interact(interactionContext);

            var snapshot = context.Saves.Capture(new SaveSlotId("debug"));

            Assert.IsTrue(snapshot.StableStates.Any(state => state.StableId == "pickup.key"));
            Object.DestroyImmediate(interactionContext.Interactor);
        }

        [Test]
        public void InteractionEnabledStateCapturesAndRestoresThroughPersistentInteractableBase()
        {
            var context = HorrorGameContext.Ensure();
            var door = CreateDoor("door", "door.enabled-state");
            door.SetInteractionEnabled(false);

            var captured = (InteractionObjectState)door.CaptureState();
            Assert.That(captured.IsEnabled, Is.False);

            door.SetInteractionEnabled(true);
            door.RestoreState(captured);

            Assert.That(door.IsInteractionEnabled, Is.False);
            var prompt = door.GetPrompt(new InteractionContext(new GameObject("interactor"), context));
            Assert.That(prompt.CanInteract, Is.False);
            Assert.That(prompt.RejectionReason, Is.EqualTo(InteractionRejectionReason.Disabled));
            Object.DestroyImmediate(GameObject.Find("interactor"));
        }

        [Test]
        public void FiredTriggerStateCapturesAndRestoresThroughSaveOwner()
        {
            var context = CreateContextWithRequiredGlobalOwners();
            var trigger = CreateTrigger("trigger", "trigger.entry");
            trigger.RestoreState(new InteractionTriggerState(hasFired: true));

            var snapshot = context.Saves.Capture(new SaveSlotId("debug"));
            var savedTriggerState = (InteractionTriggerState)snapshot.StableStates
                .Single(state => state.StableId == "trigger.entry")
                .State;
            Assert.IsTrue(savedTriggerState.HasFired);

            Object.DestroyImmediate(trigger.gameObject);
            trigger = CreateTrigger("trigger", "trigger.entry");

            context.Saves.Restore(snapshot);

            Assert.IsTrue(((InteractionTriggerState)trigger.CaptureState()).HasFired);
            Assert.IsTrue(snapshot.StableStates.Any(state => state.StableId == "trigger.entry"));
            Assert.AreEqual(1, context.Saveables.StableOwners.Count);

            var triggerEventCount = 0;
            context.Events.Subscribe<InteractionTriggerEntered>(_ => triggerEventCount++);
            var enteringObject = new GameObject("trigger-entering-object");
            var enteringCollider = enteringObject.AddComponent<BoxCollider>();
            InvokePrivateMethod(trigger, "OnTriggerEnter", enteringCollider);

            Assert.AreEqual(0, triggerEventCount);
        }

        [Test]
        public void MissingAuthoredStableIdDoesNotGenerateRuntimeIdentity()
        {
            var context = HorrorGameContext.Ensure();
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "missing-stable-id-door";
            var stableId = go.AddComponent<StableId>();
            stableId.Assign(string.Empty);

            LogAssert.Expect(
                LogType.Error,
                "Persistent interactable 'missing-stable-id-door' (DoorInteractable) requires an authored, non-empty StableId. Runtime-generated IDs are intentionally unsupported for authored persistent objects.");
            var door = go.AddComponent<DoorInteractable>();
            var prompt = door.GetPrompt(new InteractionContext(go, context));

            Assert.That(stableId.HasStableId, Is.False);
            Assert.That(door.StableId, Is.Empty);
            Assert.That(prompt.CanInteract, Is.False);
            Assert.That(context.Saveables.StableOwners, Is.Empty);
            Object.DestroyImmediate(go);
        }

        private static DoorInteractable CreateDoor(string name, string stableId)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.AddComponent<StableId>().Assign(stableId);
            var door = go.AddComponent<DoorInteractable>();
            ConfigureObjectReference(door, "movingRoot", go.transform);
            return door;
        }

        private static HorrorGameContext CreateContextWithRequiredGlobalOwners()
        {
            var context = HorrorGameContext.Ensure();
            context.Saveables.RegisterGlobal(new TestPlayerPoseOwner());
            return context;
        }

        private static PickupInteractable CreatePickup(string name, string stableId, string itemId)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.AddComponent<StableId>().Assign(stableId);
            var pickup = go.AddComponent<PickupInteractable>();
            ConfigureString(pickup, "itemIdOverride", itemId);
            return pickup;
        }

        private static InteractionTriggerZone CreateTrigger(string name, string stableId)
        {
            var go = new GameObject(name);
            go.AddComponent<BoxCollider>();
            var stableIdComponent = go.AddComponent<StableId>();
            stableIdComponent.Assign(stableId);
            var trigger = go.AddComponent<InteractionTriggerZone>();
            InvokePrivateMethod(trigger, "EnsureSaveRegistration");
            return trigger;
        }

        private static void InvokePrivateMethod(object target, string methodName, params object[] arguments)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Expected private method '{methodName}' on {target.GetType().Name}.");
            method.Invoke(target, arguments);
        }

        private static void ConfigureObjectReference(Object target, string propertyName, Object value)
        {
            var property = BeginSerializedProperty(target, propertyName);
            property.objectReferenceValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureString(Object target, string propertyName, string value)
        {
            var property = BeginSerializedProperty(target, propertyName);
            property.stringValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureBool(Object target, string propertyName, bool value)
        {
            var property = BeginSerializedProperty(target, propertyName);
            property.boolValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureNestedString(Object target, string propertyName, string childPropertyName, string value)
        {
            var property = BeginSerializedProperty(target, propertyName).FindPropertyRelative(childPropertyName);
            property.stringValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureNestedBool(Object target, string propertyName, string childPropertyName, bool value)
        {
            var property = BeginSerializedProperty(target, propertyName).FindPropertyRelative(childPropertyName);
            property.boolValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SerializedProperty BeginSerializedProperty(Object target, string propertyName)
        {
            return new SerializedObject(target).FindProperty(propertyName);
        }

        private sealed class TestPlayerPoseOwner : RuntimeStateOwnerBase<PlayerPose>
        {
            private PlayerPose pose = new PlayerPose(
                Vector3.zero,
                Quaternion.identity,
                0f,
                PlayerControlState.Normal);

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
    }
}
