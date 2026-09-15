using UnityEngine;

namespace Setus.HorrorFramework.AI.Perception
{
    public enum StalkerSightReason
    {
        NoTarget = 0,
        OutOfRange = 1,
        OutsideFieldOfView = 2,
        TargetBlocked = 3,
        TargetNotHit = 4,
        TargetConfirmed = 5,
        QueryOverflow = 6
    }

    public readonly struct StalkerSightResult
    {
        public StalkerSightResult(StalkerSightReason reason, Vector3 targetPosition, Collider blocker)
        {
            Reason = reason;
            TargetPosition = targetPosition;
            Blocker = blocker;
        }

        public StalkerSightReason Reason { get; }
        public Vector3 TargetPosition { get; }
        public Collider Blocker { get; }
        public bool IsConfirmed => Reason == StalkerSightReason.TargetConfirmed;
    }
}
