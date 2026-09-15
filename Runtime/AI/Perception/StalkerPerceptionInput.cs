using UnityEngine;

namespace Setus.HorrorFramework.AI.Perception
{
    public readonly struct StalkerPerceptionInput
    {
        public StalkerPerceptionInput(StalkerSightResult sight, bool proximityAware)
            : this(sight, proximityAware, sight.TargetPosition)
        {
        }

        public StalkerPerceptionInput(
            StalkerSightResult sight,
            bool proximityAware,
            Vector3 proximityTargetPosition)
        {
            Sight = sight;
            ProximityAware = proximityAware;
            ProximityTargetPosition = proximityTargetPosition;
        }

        public StalkerSightResult Sight { get; }
        public bool ProximityAware { get; }
        public Vector3 ProximityTargetPosition { get; }
    }
}
