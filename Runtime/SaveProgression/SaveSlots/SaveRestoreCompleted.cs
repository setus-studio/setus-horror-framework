using Setus.HorrorFramework.Core.Events;
using Setus.HorrorFramework.SaveProgression.Checkpoints;
using Setus.HorrorFramework.SaveProgression.Registry;

namespace Setus.HorrorFramework.SaveProgression.SaveSlots
{
    public readonly struct SaveRestoreCompleted : IGameplayEvent
    {
        public SaveRestoreCompleted(SaveSlotId slotId, CheckpointModel checkpoint, SaveRestoreReport report)
        {
            SlotId = slotId;
            Checkpoint = checkpoint;
            Report = report;
        }

        public SaveSlotId SlotId { get; }
        public CheckpointModel Checkpoint { get; }
        public SaveRestoreReport Report { get; }
    }
}
