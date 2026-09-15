using Setus.HorrorFramework.Localization;

namespace Setus.HorrorFramework.SaveProgression.SaveSlots
{
    public enum SaveLoadUserFailureKind
    {
        None = 0,
        NoSave = 1,
        CorruptSave = 2,
        IncompatibleSave = 3,
        RestoreFailed = 4,
        SaveFailed = 5,
        Unavailable = 6
    }

    public readonly struct SaveLoadUserFailure
    {
        public SaveLoadUserFailure(
            SaveLoadUserFailureKind kind,
            LocalizedTextReference message)
        {
            Kind = kind;
            Message = message;
        }

        public SaveLoadUserFailureKind Kind { get; }
        public LocalizedTextReference Message { get; }

        public static SaveLoadUserFailure FromValidationStage(SaveSlotValidationStage stage)
        {
            switch (stage)
            {
                case SaveSlotValidationStage.Missing:
                    return NoSave();
                case SaveSlotValidationStage.FileFormat:
                case SaveSlotValidationStage.PayloadDecode:
                case SaveSlotValidationStage.Checkpoint:
                    return CorruptSave();
                case SaveSlotValidationStage.TypeKeyResolution:
                case SaveSlotValidationStage.SchemaMigration:
                case SaveSlotValidationStage.StructuralPreflight:
                case SaveSlotValidationStage.SemanticPreflight:
                    return IncompatibleSave();
                default:
                    return Unavailable();
            }
        }

        public static SaveLoadUserFailure NoSave()
        {
            return new SaveLoadUserFailure(
                SaveLoadUserFailureKind.NoSave,
                new LocalizedTextReference(
                    FrameworkTextKeys.SaveErrorNoSave,
                    "No saved game is available."));
        }

        public static SaveLoadUserFailure CorruptSave()
        {
            return new SaveLoadUserFailure(
                SaveLoadUserFailureKind.CorruptSave,
                new LocalizedTextReference(
                    FrameworkTextKeys.SaveErrorCorrupt,
                    "This save data is corrupted and cannot be loaded."));
        }

        public static SaveLoadUserFailure IncompatibleSave()
        {
            return new SaveLoadUserFailure(
                SaveLoadUserFailureKind.IncompatibleSave,
                new LocalizedTextReference(
                    FrameworkTextKeys.SaveErrorIncompatible,
                    "This save was created by an incompatible game version."));
        }

        public static SaveLoadUserFailure RestoreFailed()
        {
            return new SaveLoadUserFailure(
                SaveLoadUserFailureKind.RestoreFailed,
                new LocalizedTextReference(
                    FrameworkTextKeys.SaveErrorRestoreFailed,
                    "The saved game could not be restored."));
        }

        public static SaveLoadUserFailure SaveFailed()
        {
            return new SaveLoadUserFailure(
                SaveLoadUserFailureKind.SaveFailed,
                new LocalizedTextReference(
                    FrameworkTextKeys.SaveErrorSaveFailed,
                    "The game could not be saved."));
        }

        public static SaveLoadUserFailure Unavailable()
        {
            return new SaveLoadUserFailure(
                SaveLoadUserFailureKind.Unavailable,
                new LocalizedTextReference(
                    FrameworkTextKeys.SaveErrorUnavailable,
                    "Save and load are currently unavailable."));
        }
    }
}
