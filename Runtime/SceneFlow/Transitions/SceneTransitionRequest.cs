using System;
using Setus.HorrorFramework.SaveProgression.Checkpoints;
using UnityEngine.SceneManagement;

namespace Setus.HorrorFramework.SceneFlow.Transitions
{
    public readonly struct SceneTransitionRequest
    {
        public SceneTransitionRequest(
            string sceneName,
            string checkpointId = "",
            string spawnId = "",
            LoadSceneMode loadSceneMode = LoadSceneMode.Single,
            bool showLoadingScreen = true)
        {
            SceneName = sceneName ?? string.Empty;
            CheckpointId = checkpointId ?? string.Empty;
            SpawnId = spawnId ?? string.Empty;
            LoadSceneMode = loadSceneMode;
            ShowLoadingScreen = showLoadingScreen;
        }

        public string SceneName { get; }
        public string CheckpointId { get; }
        public string SpawnId { get; }
        public LoadSceneMode LoadSceneMode { get; }
        public bool ShowLoadingScreen { get; }

        public bool IsValid => !string.IsNullOrWhiteSpace(SceneName);

        public static SceneTransitionRequest FromCheckpoint(CheckpointModel checkpoint)
        {
            if (checkpoint == null)
            {
                throw new ArgumentNullException(nameof(checkpoint));
            }

            return new SceneTransitionRequest(
                checkpoint.SceneName,
                checkpoint.CheckpointId,
                checkpoint.SpawnId);
        }
    }
}
