using Setus.HorrorFramework.Localization;
using UnityEngine;

namespace Setus.HorrorFramework.AI.Tuning
{
    [CreateAssetMenu(fileName = "StalkerAiTuningProfile", menuName = "Setus/Horror Framework/AI/Stalker Tuning Profile")]
    public sealed class StalkerAiTuningProfile : ScriptableObject
    {
        [Header("Perception")]
        [SerializeField, Min(0.1f)] private float sightRange = 14f;
        [SerializeField, Range(1f, 180f)] private float fieldOfView = 105f;
        [SerializeField, Min(0f)] private float proximityRange = 2.25f;
        [SerializeField, Min(0f)] private float hearingRange = 10f;
        [SerializeField, Min(0.02f)] private float perceptionInterval = 0.1f;

        [Header("Suspicion")]
        [SerializeField, Min(0f)] private float suspicionGainPerSecond = 0.9f;
        [SerializeField, Min(0f)] private float suspicionDecayPerSecond = 0.3f;
        [SerializeField, Range(0.01f, 1f)] private float searchThreshold = 0.45f;
        [SerializeField, Min(0.05f)] private float chaseAnticipationDuration = 0.75f;
        [SerializeField, Min(0.1f)] private float lostSightDelay = 2f;
        [SerializeField, Min(0.1f)] private float searchDuration = 6f;

        [Header("Navigation")]
        [SerializeField, Min(0.1f)] private float patrolSpeed = 1.3f;
        [SerializeField, Min(0.1f)] private float searchSpeed = 2f;
        [SerializeField, Min(0.1f)] private float chaseSpeed = 3.4f;
        [SerializeField, Min(0.05f)] private float arrivalDistance = 0.35f;

        [Header("Player Feedback")]
        [SerializeField] private string chaseSubtitleLocalizationKey = FrameworkTextKeys.StalkerChaseSubtitle;
        [SerializeField, TextArea] private string chaseSubtitle = "Something has noticed you.";
        [SerializeField, Min(0.5f)] private float chaseSubtitleDuration = 2.2f;

        public float SightRange => sightRange;
        public float FieldOfView => fieldOfView;
        public float ProximityRange => proximityRange;
        public float HearingRange => hearingRange;
        public float PerceptionInterval => perceptionInterval;
        public float SuspicionGainPerSecond => suspicionGainPerSecond;
        public float SuspicionDecayPerSecond => suspicionDecayPerSecond;
        public float SearchThreshold => searchThreshold;
        public float ChaseAnticipationDuration => chaseAnticipationDuration;
        public float LostSightDelay => lostSightDelay;
        public float SearchDuration => searchDuration;
        public float PatrolSpeed => patrolSpeed;
        public float SearchSpeed => searchSpeed;
        public float ChaseSpeed => chaseSpeed;
        public float ArrivalDistance => arrivalDistance;
        public string ChaseSubtitleLocalizationKey => chaseSubtitleLocalizationKey;
        public string ChaseSubtitle => chaseSubtitle;
        public LocalizedTextReference LocalizedChaseSubtitle =>
            new LocalizedTextReference(chaseSubtitleLocalizationKey, chaseSubtitle);
        public float ChaseSubtitleDuration => chaseSubtitleDuration;
    }
}
