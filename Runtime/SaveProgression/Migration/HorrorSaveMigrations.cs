using System;
using System.Collections.Generic;
using System.Linq;
using Setus.HorrorFramework.Narrative.Objectives;
using Setus.HorrorFramework.Narrative.Scenario;
using Setus.HorrorFramework.Atmosphere.Scares;
using Setus.HorrorFramework.Atmosphere.Tension;
using Setus.HorrorFramework.SaveProgression.Registry;
using Setus.HorrorFramework.SaveProgression.SaveSlots;
using Setus.HorrorFramework.SaveProgression.Serialization;
using Setus.HorrorFramework.UI.Settings;

namespace Setus.HorrorFramework.SaveProgression.Migration
{
    public static class HorrorSaveMigrations
    {
        public static void RegisterDefaults(SaveMigrationPipeline pipeline)
        {
            if (pipeline == null)
            {
                throw new ArgumentNullException(nameof(pipeline));
            }

            pipeline.Register(new SchemaOneToTwoNarrativeStateMigration());
            pipeline.Register(new SchemaTwoToThreeAtmosphereStateMigration());
            pipeline.Register(new SchemaThreeToFourAccessibilitySettingsMigration());
            pipeline.Register(new SchemaFourToFiveSceneScopedStableStateMigration());
        }

        private sealed class SchemaFourToFiveSceneScopedStableStateMigration : ISaveMigrationRule
        {
            public int SourceVersion => 4;
            public int TargetVersion => 5;

            public SaveGameSnapshot Migrate(SaveGameSnapshot snapshot)
            {
                var sceneId = snapshot.Checkpoint?.SceneName ?? string.Empty;
                var stableStates = snapshot.StableStates.Select(state => new StableSaveStateRecord(
                    state.StableId,
                    state.StateKey,
                    state.StateTypeKey,
                    state.State,
                    state.HadActiveState,
                    state.ActiveRestorePolicy,
                    sceneId)).ToArray();

                return new SaveGameSnapshot(
                    TargetVersion,
                    snapshot.SlotId,
                    snapshot.Checkpoint,
                    snapshot.GlobalStates,
                    stableStates,
                    snapshot.SavedAtUtcTicks,
                    string.IsNullOrWhiteSpace(sceneId) ? Array.Empty<string>() : new[] { sceneId });
            }
        }

        private sealed class SchemaThreeToFourAccessibilitySettingsMigration : ISaveMigrationRule
        {
            public int SourceVersion => 3;
            public int TargetVersion => 4;

            public SaveGameSnapshot Migrate(SaveGameSnapshot snapshot)
            {
                var globalStates = snapshot.GlobalStates.Select(MigrateSettingsRecord).ToArray();
                return new SaveGameSnapshot(
                    TargetVersion,
                    snapshot.SlotId,
                    snapshot.Checkpoint,
                    globalStates,
                    snapshot.StableStates,
                    snapshot.SavedAtUtcTicks);
            }

            private static SaveStateRecord MigrateSettingsRecord(SaveStateRecord record)
            {
                if (!string.Equals(
                        record.StateKey,
                        HorrorGlobalSaveStateRegistry.RuntimeSettingsStateKey,
                        StringComparison.Ordinal))
                {
                    return record;
                }

                RuntimeSettingsState migrated;
                if (record.State is RuntimeSettingsStateV1 legacy)
                {
                    migrated = new RuntimeSettingsState(
                        legacy.MasterVolume,
                        legacy.MouseSensitivity,
                        legacy.SubtitlesEnabled);
                }
                else if (record.State is RuntimeSettingsState legacyAliasState)
                {
                    // Format-v1 CLR aliases resolve through the current codec before schema migration.
                    migrated = new RuntimeSettingsState(
                        legacyAliasState.MasterVolume,
                        legacyAliasState.MouseSensitivity,
                        legacyAliasState.SubtitlesEnabled);
                }
                else
                {
                    throw new InvalidOperationException(
                        "Schema 3 runtime settings must decode through the v1 codec or its legacy CLR alias.");
                }

                return new SaveStateRecord(
                    record.StateKey,
                    SaveStateTypeKeys.RuntimeSettingsV2,
                    migrated);
            }
        }

        private sealed class SchemaTwoToThreeAtmosphereStateMigration : ISaveMigrationRule
        {
            public int SourceVersion => 2;
            public int TargetVersion => 3;

            public SaveGameSnapshot Migrate(SaveGameSnapshot snapshot)
            {
                var globalStates = new List<SaveStateRecord>(snapshot.GlobalStates);
                AddIfMissing(
                    globalStates,
                    HorrorGlobalSaveStateRegistry.AtmosphereScareStateKey,
                    SaveStateTypeKeys.AtmosphereScareRuntimeV1,
                    new ScareRuntimeState());
                AddIfMissing(
                    globalStates,
                    HorrorGlobalSaveStateRegistry.AtmosphereTensionStateKey,
                    SaveStateTypeKeys.AtmosphereTensionRuntimeV1,
                    new TensionRuntimeState());

                return new SaveGameSnapshot(
                    TargetVersion,
                    snapshot.SlotId,
                    snapshot.Checkpoint,
                    globalStates,
                    snapshot.StableStates,
                    snapshot.SavedAtUtcTicks);
            }
        }

        private sealed class SchemaOneToTwoNarrativeStateMigration : ISaveMigrationRule
        {
            public int SourceVersion => 1;
            public int TargetVersion => 2;

            public SaveGameSnapshot Migrate(SaveGameSnapshot snapshot)
            {
                var globalStates = new List<SaveStateRecord>(snapshot.GlobalStates);
                AddIfMissing(
                    globalStates,
                    HorrorGlobalSaveStateRegistry.ObjectiveStateKey,
                    SaveStateTypeKeys.ObjectiveRuntimeV1,
                    new ObjectiveRuntimeState());
                AddIfMissing(
                    globalStates,
                    HorrorGlobalSaveStateRegistry.NarrativeStateKey,
                    SaveStateTypeKeys.NarrativeRuntimeV1,
                    new NarrativeRuntimeState());

                return new SaveGameSnapshot(
                    TargetVersion,
                    snapshot.SlotId,
                    snapshot.Checkpoint,
                    globalStates,
                    snapshot.StableStates,
                    snapshot.SavedAtUtcTicks);
            }

        }

        private static void AddIfMissing(
            IList<SaveStateRecord> states,
            string stateKey,
            string stateTypeKey,
            object defaultState)
        {
            if (states.Any(state => string.Equals(state.StateKey, stateKey, StringComparison.Ordinal)))
            {
                return;
            }

            states.Add(new SaveStateRecord(stateKey, stateTypeKey, defaultState));
        }
    }
}
