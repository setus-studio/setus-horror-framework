using System;

namespace Setus.HorrorFramework.SaveProgression.SaveSlots
{
    [Serializable]
    public sealed class SaveSlotMetadata
    {
        public SaveSlotMetadata(
            SaveSlotId slotId,
            int saveFormatVersion,
            int schemaVersion,
            string sceneName,
            string checkpointId,
            string spawnId,
            long savedAtUtcTicks,
            string displayName = null)
        {
            SlotId = slotId;
            SaveFormatVersion = saveFormatVersion;
            SchemaVersion = schemaVersion;
            SceneName = sceneName;
            CheckpointId = checkpointId;
            SpawnId = spawnId;
            SavedAtUtcTicks = savedAtUtcTicks;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? slotId.Value : displayName;
        }

        public SaveSlotId SlotId { get; }
        public int SaveFormatVersion { get; }
        public int SchemaVersion { get; }
        public string SceneName { get; }
        public string CheckpointId { get; }
        public string SpawnId { get; }
        public string DisplayName { get; }
        public long SavedAtUtcTicks { get; }

        public static SaveSlotMetadata FromSnapshot(SaveGameSnapshot snapshot, int saveFormatVersion)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            return new SaveSlotMetadata(
                snapshot.SlotId,
                saveFormatVersion,
                snapshot.SchemaVersion,
                snapshot.Checkpoint?.SceneName,
                snapshot.Checkpoint?.CheckpointId,
                snapshot.Checkpoint?.SpawnId,
                snapshot.SavedAtUtcTicks);
        }
    }
}
