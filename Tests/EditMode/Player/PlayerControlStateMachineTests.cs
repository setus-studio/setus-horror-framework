using NUnit.Framework;
using Setus.HorrorFramework.Player.State;

namespace Setus.HorrorFramework.Tests.EditMode.Player
{
    public sealed class PlayerControlStateMachineTests
    {
        [Test]
        public void StatePolicies_MapInputAndCursorModes()
        {
            var normal = PlayerControlStatePolicy.For(PlayerControlState.Normal);
            Assert.IsTrue(normal.CanMove);
            Assert.IsTrue(normal.CanLook);
            Assert.IsTrue(normal.CursorLocked);

            var interactionLocked = PlayerControlStatePolicy.For(PlayerControlState.InteractionLocked);
            Assert.IsFalse(interactionLocked.CanMove);
            Assert.IsTrue(interactionLocked.CanLook);
            Assert.IsTrue(interactionLocked.CursorLocked);

            var inspecting = PlayerControlStatePolicy.For(PlayerControlState.Inspecting);
            Assert.IsFalse(inspecting.CanMove);
            Assert.IsFalse(inspecting.CanLook);
            Assert.IsFalse(inspecting.CursorLocked);

            var menu = PlayerControlStatePolicy.For(PlayerControlState.Menu);
            Assert.IsFalse(menu.CanMove);
            Assert.IsFalse(menu.CanLook);
            Assert.IsFalse(menu.CursorLocked);

            var disabled = PlayerControlStatePolicy.For(PlayerControlState.Disabled);
            Assert.IsFalse(disabled.CanMove);
            Assert.IsFalse(disabled.CanLook);
            Assert.IsTrue(disabled.CursorLocked);
        }

        [Test]
        public void SetState_RaisesTransitionOnce()
        {
            var stateMachine = new PlayerControlStateMachine();
            var transitionCount = 0;
            PlayerControlStateChanged lastTransition = default;
            stateMachine.StateChanged += transition =>
            {
                transitionCount++;
                lastTransition = transition;
            };

            Assert.IsTrue(stateMachine.SetState(PlayerControlState.Menu));
            Assert.IsFalse(stateMachine.SetState(PlayerControlState.Menu));

            Assert.AreEqual(1, transitionCount);
            Assert.AreEqual(PlayerControlState.Normal, lastTransition.PreviousState);
            Assert.AreEqual(PlayerControlState.Menu, lastTransition.CurrentState);
            Assert.AreEqual(PlayerControlState.Menu, stateMachine.CurrentState);
        }

        [Test]
        public void Persistence_NormalizesTransientMenuStateOnly()
        {
            Assert.That(
                PlayerControlStatePolicy.NormalizeForPersistence(PlayerControlState.Menu),
                Is.EqualTo(PlayerControlState.Normal));
            Assert.That(
                PlayerControlStatePolicy.NormalizeForPersistence(PlayerControlState.Disabled),
                Is.EqualTo(PlayerControlState.Disabled));
        }
    }
}
