using Setus.HorrorFramework.Core.Events;
using UnityEngine;

namespace Setus.HorrorFramework.Player.Controller
{
    public readonly struct PlayerFootstepEmitted : IGameplayEvent
    {
        public PlayerFootstepEmitted(Vector3 position, float travelledDistance)
        {
            Position = position;
            TravelledDistance = travelledDistance;
        }

        public Vector3 Position { get; }
        public float TravelledDistance { get; }
    }
}
