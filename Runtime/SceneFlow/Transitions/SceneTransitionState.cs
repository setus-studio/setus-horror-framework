using System;
using UnityEngine;

namespace Setus.HorrorFramework.SceneFlow.Transitions
{
    [Serializable]
    public sealed class SceneTransitionState
    {
        public SceneTransitionState(
            SceneTransitionPhase phase = SceneTransitionPhase.Idle,
            string targetSceneName = "",
            string checkpointId = "",
            string spawnId = "",
            bool showLoadingScreen = true)
        {
            Phase = phase;
            TargetSceneName = targetSceneName ?? string.Empty;
            CheckpointId = checkpointId ?? string.Empty;
            SpawnId = spawnId ?? string.Empty;
            ShowLoadingScreen = showLoadingScreen;
        }

        [SerializeField] private SceneTransitionPhase phase;
        [SerializeField] private string targetSceneName;
        [SerializeField] private string checkpointId;
        [SerializeField] private string spawnId;
        [SerializeField] private bool showLoadingScreen;

        public SceneTransitionPhase Phase
        {
            get => phase;
            private set => phase = value;
        }

        public string TargetSceneName
        {
            get => targetSceneName;
            private set => targetSceneName = value;
        }

        public string CheckpointId
        {
            get => checkpointId;
            private set => checkpointId = value;
        }

        public string SpawnId
        {
            get => spawnId;
            private set => spawnId = value;
        }

        public bool ShowLoadingScreen
        {
            get => showLoadingScreen;
            private set => showLoadingScreen = value;
        }

        public bool IsLoading => Phase == SceneTransitionPhase.Loading;
        public static SceneTransitionState Idle => new SceneTransitionState();
    }
}
