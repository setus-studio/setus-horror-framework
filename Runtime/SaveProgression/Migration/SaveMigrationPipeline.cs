using System;
using System.Collections.Generic;
using Setus.HorrorFramework.SaveProgression.SaveSlots;

namespace Setus.HorrorFramework.SaveProgression.Migration
{
    public sealed class SaveMigrationPipeline
    {
        private readonly Dictionary<int, ISaveMigrationRule> rulesBySourceVersion =
            new Dictionary<int, ISaveMigrationRule>();

        public void Register(ISaveMigrationRule rule)
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            if (rule.SourceVersion < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rule),
                    "Save migration source version must not be negative.");
            }

            if (rule.TargetVersion <= rule.SourceVersion)
            {
                throw new ArgumentException(
                    $"Save migration rule {rule.SourceVersion}->{rule.TargetVersion} must advance the schema version.",
                    nameof(rule));
            }

            if (rulesBySourceVersion.ContainsKey(rule.SourceVersion))
            {
                throw new InvalidOperationException(
                    $"A save migration rule is already registered for source schema version {rule.SourceVersion}.");
            }

            rulesBySourceVersion.Add(rule.SourceVersion, rule);
        }

        public SaveGameSnapshot MigrateToCurrent(SaveGameSnapshot snapshot)
        {
            return MigrateToVersion(snapshot, SaveSchemaVersion.Current);
        }

        public SaveGameSnapshot MigrateToVersion(SaveGameSnapshot snapshot, int targetSchemaVersion)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (targetSchemaVersion < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(targetSchemaVersion),
                    "Target save schema version must not be negative.");
            }

            if (snapshot.SchemaVersion > targetSchemaVersion)
            {
                throw new UnsupportedSaveVersionException(snapshot.SchemaVersion, targetSchemaVersion);
            }

            var migrated = snapshot;
            while (migrated.SchemaVersion < targetSchemaVersion)
            {
                if (!rulesBySourceVersion.TryGetValue(migrated.SchemaVersion, out var rule))
                {
                    throw new UnsupportedSaveVersionException(migrated.SchemaVersion, targetSchemaVersion);
                }

                migrated = rule.Migrate(migrated);
                if (migrated == null)
                {
                    throw new InvalidOperationException(
                        $"Save migration rule {rule.SourceVersion}->{rule.TargetVersion} returned a null snapshot.");
                }

                if (migrated.SchemaVersion != rule.TargetVersion)
                {
                    throw new InvalidOperationException(
                        $"Save migration rule {rule.SourceVersion}->{rule.TargetVersion} returned schema version " +
                        $"{migrated.SchemaVersion}, but its declared target is {rule.TargetVersion}.");
                }

                if (migrated.SchemaVersion > targetSchemaVersion)
                {
                    throw new InvalidOperationException(
                        $"Save migration rule {rule.SourceVersion}->{rule.TargetVersion} exceeds requested target " +
                        $"schema version {targetSchemaVersion}.");
                }
            }

            return migrated;
        }
    }
}
