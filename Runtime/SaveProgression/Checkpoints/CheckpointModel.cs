using System;
using Setus.HorrorFramework.Player.Controller;
using UnityEngine;

namespace Setus.HorrorFramework.SaveProgression.Checkpoints
{
    [Serializable]
    public sealed class CheckpointModel
    {
        public CheckpointModel(
            string checkpointId,
            string sceneName,
            string spawnId,
            PlayerPose? playerPose = null,
            long capturedAtUtcTicks = 0)
        {
            CheckpointId = checkpointId;
            SceneName = sceneName;
            SpawnId = spawnId;
            PlayerPose = playerPose;
            CapturedAtUtcTicks = capturedAtUtcTicks == 0 ? DateTime.UtcNow.Ticks : capturedAtUtcTicks;
        }

        [SerializeField] private string checkpointId;
        [SerializeField] private string sceneName;
        [SerializeField] private string spawnId;
        [SerializeField] private PlayerPose? playerPose;
        [SerializeField] private long capturedAtUtcTicks;

        public string CheckpointId
        {
            get => checkpointId;
            private set => checkpointId = value;
        }

        public string SceneName
        {
            get => sceneName;
            private set => sceneName = value;
        }

        public string SpawnId
        {
            get => spawnId;
            private set => spawnId = value;
        }

        public PlayerPose? PlayerPose
        {
            get => playerPose;
            private set => playerPose = value;
        }

        public long CapturedAtUtcTicks
        {
            get => capturedAtUtcTicks;
            private set => capturedAtUtcTicks = value;
        }
    }
}
