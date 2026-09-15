using NUnit.Framework;
using Setus.HorrorFramework.Interaction.Interactors;
using Setus.HorrorFramework.Player.State;

namespace Setus.HorrorFramework.Tests.EditMode.Interaction
{
    public sealed class PlayerInteractionPermissionPolicyTests
    {
        [Test]
        public void NormalGameplay_AllowsInteraction()
        {
            Assert.That(
                PlayerInteractionPermissionPolicy.CanInteract(
                    PlayerControlState.Normal,
                    false,
                    false,
                    false),
                Is.True);
        }

        [TestCase(PlayerControlState.Inspecting)]
        [TestCase(PlayerControlState.Cutscene)]
        [TestCase(PlayerControlState.Menu)]
        [TestCase(PlayerControlState.Disabled)]
        [TestCase(PlayerControlState.InteractionLocked)]
        public void NonGameplayControlStates_BlockInteraction(PlayerControlState state)
        {
            Assert.That(
                PlayerInteractionPermissionPolicy.CanInteract(state, false, false, false),
                Is.False);
        }

        [Test]
        public void PauseMenuAndTransition_BlockInteraction()
        {
            Assert.That(
                PlayerInteractionPermissionPolicy.CanInteract(
                    PlayerControlState.Normal,
                    true,
                    true,
                    false),
                Is.False);
            Assert.That(
                PlayerInteractionPermissionPolicy.CanInteract(
                    PlayerControlState.Normal,
                    false,
                    false,
                    true),
                Is.False);
        }
    }
}
