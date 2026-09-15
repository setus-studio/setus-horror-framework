using System;
using Setus.HorrorFramework.SaveProgression.Registry;

namespace Setus.HorrorFramework.SaveProgression.SaveSlots
{
    public readonly struct SaveRestorePreflightResult
    {
        private SaveRestorePreflightResult(SaveGameSnapshot snapshot, SaveRestoreReport report)
        {
            Snapshot = snapshot;
            Report = report;
        }

        public SaveGameSnapshot Snapshot { get; }
        public SaveRestoreReport Report { get; }
        public bool IsValid => Snapshot != null && Report != null && !Report.HasErrors;

        public static SaveRestorePreflightResult Valid(SaveGameSnapshot snapshot, SaveRestoreReport report)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            return new SaveRestorePreflightResult(snapshot, report);
        }

        public static SaveRestorePreflightResult Invalid(SaveRestoreReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            return new SaveRestorePreflightResult(null, report);
        }
    }
}
