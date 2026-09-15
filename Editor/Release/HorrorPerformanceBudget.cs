using UnityEngine;

namespace Setus.HorrorFramework.Editor.Release
{
    public sealed class HorrorPerformanceBudget : ScriptableObject
    {
        [SerializeField, Min(30)] private int targetFramesPerSecond = 60;
        [SerializeField, Min(0)] private int steadyStateGcBytesPerFrame;
        [SerializeField, Min(1)] private int maximumRaycastSources = 16;
        [SerializeField, Min(1)] private int maximumActiveAudioSources = 32;
        [SerializeField, Min(1)] private int maximumRealtimeLights = 8;
        [SerializeField, Min(1)] private int maximumGlobalVolumes = 4;

        public int TargetFramesPerSecond => targetFramesPerSecond;
        public int SteadyStateGcBytesPerFrame => steadyStateGcBytesPerFrame;
        public int MaximumRaycastSources => maximumRaycastSources;
        public int MaximumActiveAudioSources => maximumActiveAudioSources;
        public int MaximumRealtimeLights => maximumRealtimeLights;
        public int MaximumGlobalVolumes => maximumGlobalVolumes;

        public void Configure(
            int targetFps,
            int gcBytesPerFrame,
            int raycastSources,
            int audioSources,
            int realtimeLights,
            int globalVolumes)
        {
            targetFramesPerSecond = Mathf.Max(30, targetFps);
            steadyStateGcBytesPerFrame = Mathf.Max(0, gcBytesPerFrame);
            maximumRaycastSources = Mathf.Max(1, raycastSources);
            maximumActiveAudioSources = Mathf.Max(1, audioSources);
            maximumRealtimeLights = Mathf.Max(1, realtimeLights);
            maximumGlobalVolumes = Mathf.Max(1, globalVolumes);
        }
    }
}
