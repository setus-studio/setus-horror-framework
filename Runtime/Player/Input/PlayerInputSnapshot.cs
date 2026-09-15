using UnityEngine;

namespace Setus.HorrorFramework.Player.Input
{
    public readonly struct PlayerInputSnapshot
    {
        public PlayerInputSnapshot(Vector2 move, Vector2 look, bool sprintHeld, bool crouchHeld)
        {
            Move = Vector2.ClampMagnitude(move, 1f);
            Look = look;
            SprintHeld = sprintHeld;
            CrouchHeld = crouchHeld;
        }

        public Vector2 Move { get; }
        public Vector2 Look { get; }
        public bool SprintHeld { get; }
        public bool CrouchHeld { get; }
    }
}
