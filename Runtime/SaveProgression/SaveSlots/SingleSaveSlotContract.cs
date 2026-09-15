namespace Setus.HorrorFramework.SaveProgression.SaveSlots
{
    public static class SingleSaveSlotContract
    {
        public const string CanonicalSlotValue = "primary";

        public static SaveSlotId SlotId => new SaveSlotId(CanonicalSlotValue);
    }
}
