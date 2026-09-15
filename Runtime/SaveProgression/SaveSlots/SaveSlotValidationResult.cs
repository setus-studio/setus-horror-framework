using System;
using Setus.HorrorFramework.SaveProgression.Registry;

namespace Setus.HorrorFramework.SaveProgression.SaveSlots
{
    public readonly struct SaveSlotValidationResult
    {
        private SaveSlotValidationResult(
            bool isValid,
            SaveSlotValidationStage failureStage,
            string errorCode,
            string diagnostic,
            SaveSlotMetadata metadata,
            SaveGameSnapshot snapshot,
            SaveRestoreReport report)
        {
            IsValid = isValid;
            FailureStage = failureStage;
            ErrorCode = errorCode;
            Diagnostic = diagnostic;
            Metadata = metadata;
            Snapshot = snapshot;
            Report = report;
        }

        public bool IsValid { get; }
        public SaveSlotValidationStage FailureStage { get; }
        public string ErrorCode { get; }
        public string Diagnostic { get; }
        public SaveSlotMetadata Metadata { get; }
        public SaveGameSnapshot Snapshot { get; }
        public SaveRestoreReport Report { get; }

        public static SaveSlotValidationResult Valid(
            SaveSlotMetadata metadata,
            SaveGameSnapshot snapshot,
            SaveRestoreReport report)
        {
            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            return new SaveSlotValidationResult(
                true,
                SaveSlotValidationStage.None,
                null,
                null,
                metadata,
                snapshot,
                report);
        }

        public static SaveSlotValidationResult Invalid(
            SaveSlotValidationStage failureStage,
            string errorCode,
            string diagnostic,
            SaveRestoreReport report = null)
        {
            if (failureStage == SaveSlotValidationStage.None)
            {
                throw new ArgumentOutOfRangeException(nameof(failureStage));
            }

            return new SaveSlotValidationResult(
                false,
                failureStage,
                errorCode,
                diagnostic,
                null,
                null,
                report);
        }
    }
}
