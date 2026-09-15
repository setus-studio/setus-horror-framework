namespace Setus.HorrorFramework.SaveProgression.SaveSlots
{
    public interface ISaveSlotStore
    {
        void Save(SaveGameSnapshot snapshot);
        SaveSlotReadResult Read(SaveSlotId slotId);
        bool TryLoad(SaveSlotId slotId, out SaveGameSnapshot snapshot);
        bool TryGetMetadata(SaveSlotId slotId, out SaveSlotMetadata metadata);
        bool HasSlot(SaveSlotId slotId);
    }
}
