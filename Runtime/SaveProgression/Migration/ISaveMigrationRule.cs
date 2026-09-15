using Setus.HorrorFramework.SaveProgression.SaveSlots;

namespace Setus.HorrorFramework.SaveProgression.Migration
{
    public interface ISaveMigrationRule
    {
        int SourceVersion { get; }
        int TargetVersion { get; }
        SaveGameSnapshot Migrate(SaveGameSnapshot snapshot);
    }
}
