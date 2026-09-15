using System.Collections.Generic;

namespace Setus.HorrorFramework.SaveProgression.SaveSlots
{
    public sealed class InMemorySaveSlotStore : ISaveSlotStore
    {
        private readonly Dictionary<SaveSlotId, SaveGameSnapshot> snapshots =
            new Dictionary<SaveSlotId, SaveGameSnapshot>();
        private readonly Dictionary<SaveSlotId, SaveSlotMetadata> metadataBySlot =
            new Dictionary<SaveSlotId, SaveSlotMetadata>();

        public void Save(SaveGameSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            snapshots[snapshot.SlotId] = snapshot;
            metadataBySlot[snapshot.SlotId] = SaveSlotMetadata.FromSnapshot(
                snapshot,
                SaveFileFormatVersion.Current);
        }

        public bool TryLoad(SaveSlotId slotId, out SaveGameSnapshot snapshot)
        {
            return snapshots.TryGetValue(slotId, out snapshot);
        }

        public SaveSlotReadResult Read(SaveSlotId slotId)
        {
            if (!snapshots.TryGetValue(slotId, out var snapshot) ||
                !metadataBySlot.TryGetValue(slotId, out var metadata))
            {
                return SaveSlotReadResult.Invalid(
                    SaveSlotValidationStage.Missing,
                    "slot-missing",
                    $"Save slot '{slotId}' does not exist.");
            }

            return SaveSlotReadResult.Valid(metadata, snapshot);
        }

        public bool HasSlot(SaveSlotId slotId)
        {
            return snapshots.ContainsKey(slotId);
        }

        public bool TryGetMetadata(SaveSlotId slotId, out SaveSlotMetadata metadata)
        {
            return metadataBySlot.TryGetValue(slotId, out metadata);
        }

        public void Clear()
        {
            snapshots.Clear();
            metadataBySlot.Clear();
        }
    }
}
