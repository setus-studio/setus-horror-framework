using System;

namespace Setus.HorrorFramework.SaveProgression.SaveSlots
{
    public enum SaveSlotValidationStage
    {
        None = 0,
        Missing = 1,
        FileRead = 2,
        FileFormat = 3,
        TypeKeyResolution = 4,
        PayloadDecode = 5,
        SchemaMigration = 6,
        Checkpoint = 7,
        StructuralPreflight = 8,
        SemanticPreflight = 9,
        Configuration = 10
    }

    public readonly struct SaveSlotReadResult
    {
        private SaveSlotReadResult(
            bool isValid,
            SaveSlotValidationStage failureStage,
            string errorCode,
            string diagnostic,
            SaveSlotMetadata metadata,
            SaveGameSnapshot snapshot)
        {
            IsValid = isValid;
            FailureStage = failureStage;
            ErrorCode = errorCode;
            Diagnostic = diagnostic;
            Metadata = metadata;
            Snapshot = snapshot;
        }

        public bool IsValid { get; }
        public SaveSlotValidationStage FailureStage { get; }
        public string ErrorCode { get; }
        public string Diagnostic { get; }
        public SaveSlotMetadata Metadata { get; }
        public SaveGameSnapshot Snapshot { get; }

        public static SaveSlotReadResult Valid(SaveSlotMetadata metadata, SaveGameSnapshot snapshot)
        {
            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            return new SaveSlotReadResult(true, SaveSlotValidationStage.None, null, null, metadata, snapshot);
        }

        public static SaveSlotReadResult Invalid(
            SaveSlotValidationStage failureStage,
            string errorCode,
            string diagnostic)
        {
            if (failureStage == SaveSlotValidationStage.None)
            {
                throw new ArgumentOutOfRangeException(nameof(failureStage));
            }

            return new SaveSlotReadResult(false, failureStage, errorCode, diagnostic, null, null);
        }
    }
}
