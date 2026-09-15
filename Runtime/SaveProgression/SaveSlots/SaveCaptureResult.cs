using Setus.HorrorFramework.SaveProgression.Registry;

namespace Setus.HorrorFramework.SaveProgression.SaveSlots
{
    public sealed class SaveCaptureResult
    {
        public SaveCaptureResult(SaveGameSnapshot snapshot, SaveRestoreReport report)
        {
            Snapshot = snapshot;
            Report = report;
        }

        public SaveGameSnapshot Snapshot { get; }
        public SaveRestoreReport Report { get; }
    }
}
