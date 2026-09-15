namespace Setus.HorrorFramework.AI.States
{
    public enum StalkerAiTransitionReason
    {
        Initialized = 0,
        Restored = 1,
        DisabledByOwner = 2,
        PatrolPointReached = 3,
        HeardTarget = 4,
        ProximityAwareness = 5,
        PartialSight = 6,
        ConfirmedSight = 7,
        SuspicionThresholdReached = 8,
        SuspicionExpired = 9,
        LostSightDelayElapsed = 10,
        SearchExpired = 11,
        ChaseAnticipationStarted = 12,
        ChaseAnticipationElapsed = 13
    }
}
