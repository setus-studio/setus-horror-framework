using System;
using Setus.HorrorFramework.AI.States;
using UnityEngine;

namespace Setus.HorrorFramework.AI.Memory
{
    [Serializable]
    public sealed class StalkerAiRuntimeState
    {
        [SerializeField] private StalkerAiState state;
        [SerializeField] private float suspicion;
        [SerializeField] private Vector3 lastKnownPosition;
        [SerializeField] private int patrolPointIndex;
        [SerializeField] private float lostSightRemaining;
        [SerializeField] private float searchRemaining;

        public StalkerAiRuntimeState(
            StalkerAiState state,
            float suspicion,
            Vector3 lastKnownPosition,
            int patrolPointIndex,
            float lostSightRemaining,
            float searchRemaining)
        {
            this.state = state;
            this.suspicion = suspicion;
            this.lastKnownPosition = lastKnownPosition;
            this.patrolPointIndex = patrolPointIndex;
            this.lostSightRemaining = lostSightRemaining;
            this.searchRemaining = searchRemaining;
        }

        public StalkerAiState State => state;
        public float Suspicion => suspicion;
        public Vector3 LastKnownPosition => lastKnownPosition;
        public int PatrolPointIndex => patrolPointIndex;
        public float LostSightRemaining => lostSightRemaining;
        public float SearchRemaining => searchRemaining;
    }
}
