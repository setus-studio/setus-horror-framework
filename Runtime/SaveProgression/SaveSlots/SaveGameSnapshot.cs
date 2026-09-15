using System;
using System.Collections.Generic;
using System.Linq;
using Setus.HorrorFramework.SaveProgression.Checkpoints;
using Setus.HorrorFramework.SaveProgression.Registry;

namespace Setus.HorrorFramework.SaveProgression.SaveSlots
{
    [Serializable]
    public sealed class SaveGameSnapshot
    {
        public SaveGameSnapshot(
            int schemaVersion,
            SaveSlotId slotId,
            CheckpointModel checkpoint,
            IEnumerable<SaveStateRecord> globalStates,
            IEnumerable<StableSaveStateRecord> stableStates,
            long savedAtUtcTicks = 0,
            IEnumerable<string> visitedSceneIds = null)
        {
            SchemaVersion = schemaVersion;
            SlotId = slotId;
            Checkpoint = checkpoint;
            GlobalStates = (globalStates ?? Array.Empty<SaveStateRecord>())
                .OrderBy(state => state.StateKey, StringComparer.Ordinal)
                .ToArray();
            StableStates = (stableStates ?? Array.Empty<StableSaveStateRecord>())
                .OrderBy(state => state.StableId, StringComparer.Ordinal)
                .ThenBy(state => state.StateKey, StringComparer.Ordinal)
                .ToArray();
            var normalizedVisitedScenes = (visitedSceneIds ?? Array.Empty<string>())
                .Where(sceneId => !string.IsNullOrWhiteSpace(sceneId))
                .ToList();
            if (!string.IsNullOrWhiteSpace(checkpoint?.SceneName))
            {
                normalizedVisitedScenes.Add(checkpoint.SceneName);
            }

            VisitedSceneIds = normalizedVisitedScenes
                .Distinct(StringComparer.Ordinal)
                .OrderBy(sceneId => sceneId, StringComparer.Ordinal)
                .ToArray();
            SavedAtUtcTicks = savedAtUtcTicks == 0 ? DateTime.UtcNow.Ticks : savedAtUtcTicks;
        }

        public int SchemaVersion { get; }
        public SaveSlotId SlotId { get; }
        public CheckpointModel Checkpoint { get; }
        public IReadOnlyList<SaveStateRecord> GlobalStates { get; }
        public IReadOnlyList<StableSaveStateRecord> StableStates { get; }
        public IReadOnlyList<string> VisitedSceneIds { get; }
        public long SavedAtUtcTicks { get; }
    }
}
