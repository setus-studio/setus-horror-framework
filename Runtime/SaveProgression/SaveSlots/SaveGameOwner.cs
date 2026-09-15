using System;
using System.Collections.Generic;
using System.Linq;
using Setus.HorrorFramework.Core.Events;
using Setus.HorrorFramework.Debugging.Logging;
using Setus.HorrorFramework.SaveProgression.Checkpoints;
using Setus.HorrorFramework.SaveProgression.Migration;
using Setus.HorrorFramework.SaveProgression.Registry;
using Setus.HorrorFramework.SaveProgression.StableIds;

namespace Setus.HorrorFramework.SaveProgression.SaveSlots
{
    public sealed class SaveGameOwner
    {
        private readonly SaveableRegistry registry;
        private readonly SaveMigrationPipeline migrationPipeline;
        private readonly IGameplayEventBus events;
        private readonly IHorrorLogger logger;
        private readonly Dictionary<string, StableSaveStateRecord> retainedStableStates =
            new Dictionary<string, StableSaveStateRecord>(StringComparer.Ordinal);
        private readonly HashSet<string> visitedSceneIds = new HashSet<string>(StringComparer.Ordinal);

        public SaveGameOwner(
            SaveableRegistry registry,
            SaveMigrationPipeline migrationPipeline,
            IGameplayEventBus events,
            IHorrorLogger logger)
        {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            this.migrationPipeline = migrationPipeline ?? throw new ArgumentNullException(nameof(migrationPipeline));
            this.events = events;
            this.logger = logger;
        }

        public int GlobalStateDeclarationRevision => registry.GlobalStateDeclarations.Revision;
        public IReadOnlyList<StableSaveStateRecord> RetainedStableStates => retainedStableStates.Values
            .OrderBy(state => state.StableId, StringComparer.Ordinal)
            .ThenBy(state => state.StateKey, StringComparer.Ordinal)
            .ToArray();
        public IReadOnlyList<string> VisitedSceneIds => visitedSceneIds
            .OrderBy(sceneId => sceneId, StringComparer.Ordinal)
            .ToArray();

        public SaveGameSnapshot Capture(
            SaveSlotId slotId,
            StableIdManifest manifest = null,
            CheckpointModel checkpoint = null)
        {
            return CaptureWithReport(slotId, manifest, checkpoint).Snapshot;
        }

        public SaveCaptureResult CaptureWithReport(
            SaveSlotId slotId,
            StableIdManifest manifest = null,
            CheckpointModel checkpoint = null)
        {
            var sceneId = checkpoint?.SceneName ?? string.Empty;
            var liveSceneIds = registry.LiveStableSceneIds;
            var report = registry.ValidateForCapture(manifest, sceneId);
            foreach (var liveSceneId in liveSceneIds)
            {
                if (!string.Equals(liveSceneId, sceneId, StringComparison.Ordinal))
                {
                    AddReportIssues(registry.ValidateStableSceneForCapture(manifest, liveSceneId), report);
                }
            }

            if (report.HasErrors)
            {
                throw new SaveValidationException("Save capture failed validation.", report);
            }

            var liveStableStates = registry.CaptureStableStates(manifest, null);
            var mergedStableStates = MergeStableStates(liveStableStates);
            var mergedVisitedScenes = new HashSet<string>(visitedSceneIds, StringComparer.Ordinal);
            if (!string.IsNullOrWhiteSpace(sceneId))
            {
                mergedVisitedScenes.Add(sceneId);
            }
            foreach (var liveSceneId in liveSceneIds)
            {
                mergedVisitedScenes.Add(liveSceneId);
            }

            var globalStates = registry.CaptureGlobalStates();
            var snapshot = new SaveGameSnapshot(
                SaveSchemaVersion.Current,
                slotId,
                checkpoint,
                globalStates,
                mergedStableStates.Values,
                visitedSceneIds: mergedVisitedScenes);

            var snapshotReport = registry.ValidateSlotRestoreCompatibility(
                snapshot.GlobalStates,
                snapshot.StableStates,
                manifest,
                snapshot.SchemaVersion,
                sceneId,
                snapshot.VisitedSceneIds);
            if (snapshotReport.HasErrors)
            {
                throw new SaveValidationException("Merged save capture failed validation.", snapshotReport);
            }

            ReplaceRetainedState(snapshot.StableStates, snapshot.VisitedSceneIds);

            return new SaveCaptureResult(snapshot, report);
        }

        public SaveRestoreReport Restore(SaveGameSnapshot snapshot, StableIdManifest manifest = null)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (!TryMigrate(snapshot, out var migrated, out var migrationReport))
            {
                throw new SaveValidationException("Save restore failed validation.", migrationReport);
            }

            var report = registry.Restore(
                migrated.GlobalStates,
                migrated.StableStates,
                manifest,
                migrated.SchemaVersion,
                migrated.Checkpoint?.SceneName,
                migrated.VisitedSceneIds);
            if (report.HasErrors)
            {
                throw new SaveValidationException("Save restore failed validation.", report);
            }

            ReplaceRetainedState(migrated.StableStates, migrated.VisitedSceneIds);

            logger?.Log(
                HorrorLogCategory.SaveProgression,
                $"Restored save slot '{migrated.SlotId}' with {report.Issues.Count} warnings.");

            events?.Publish(new SaveRestoreCompleted(
                migrated.SlotId,
                migrated.Checkpoint,
                report));
            return report;
        }

        public SaveRestorePreflightResult PreflightSlotRestore(
            SaveGameSnapshot snapshot,
            StableIdManifest manifest = null)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (!TryMigrate(snapshot, out var migrated, out var migrationReport))
            {
                return SaveRestorePreflightResult.Invalid(migrationReport);
            }

            var report = registry.ValidateSlotRestoreCompatibility(
                migrated.GlobalStates,
                migrated.StableStates,
                manifest,
                migrated.SchemaVersion,
                migrated.Checkpoint?.SceneName,
                migrated.VisitedSceneIds);
            return report.HasErrors
                ? SaveRestorePreflightResult.Invalid(report)
                : SaveRestorePreflightResult.Valid(migrated, report);
        }

        public SaveRestoreReport RetainSceneState(StableIdManifest manifest, string sceneId)
        {
            if (manifest == null)
            {
                throw new ArgumentNullException(nameof(manifest));
            }

            var report = new SaveRestoreReport();
            if (!registry.HasLiveStableOwners(sceneId))
            {
                return report;
            }

            report = registry.ValidateStableSceneForCapture(manifest, sceneId);
            if (report.HasErrors)
            {
                return report;
            }

            var captured = registry.CaptureStableStates(manifest, sceneId);
            var merged = MergeStableStates(captured);
            var mergedVisitedScenes = new HashSet<string>(visitedSceneIds, StringComparer.Ordinal);
            if (!string.IsNullOrWhiteSpace(sceneId))
            {
                mergedVisitedScenes.Add(sceneId);
            }

            ReplaceRetainedState(merged.Values, mergedVisitedScenes);
            return report;
        }

        public SaveRestoreReport RestoreRetainedScene(StableIdManifest manifest, string sceneId)
        {
            if (manifest == null)
            {
                throw new ArgumentNullException(nameof(manifest));
            }

            var hasExplicitSceneContract = manifest.Entries.Any(entry =>
                entry != null && string.Equals(entry.SceneId, sceneId, StringComparison.Ordinal));
            if (!registry.HasLiveStableOwners(sceneId) && !hasExplicitSceneContract)
            {
                return new SaveRestoreReport();
            }

            var report = registry.RestoreStableScene(retainedStableStates.Values, manifest, sceneId);
            if (!report.HasErrors && !string.IsNullOrWhiteSpace(sceneId))
            {
                visitedSceneIds.Add(sceneId);
            }

            return report;
        }

        public void ResetRetainedStableState()
        {
            retainedStableStates.Clear();
            visitedSceneIds.Clear();
        }

        private Dictionary<string, StableSaveStateRecord> MergeStableStates(
            IEnumerable<StableSaveStateRecord> liveStates)
        {
            var merged = new Dictionary<string, StableSaveStateRecord>(retainedStableStates, StringComparer.Ordinal);
            foreach (var state in liveStates ?? Array.Empty<StableSaveStateRecord>())
            {
                merged[state.StableId] = state;
            }

            return merged;
        }

        private void ReplaceRetainedState(
            IEnumerable<StableSaveStateRecord> states,
            IEnumerable<string> scenes)
        {
            retainedStableStates.Clear();
            foreach (var state in states ?? Array.Empty<StableSaveStateRecord>())
            {
                retainedStableStates[state.StableId] = state;
            }

            visitedSceneIds.Clear();
            foreach (var sceneId in scenes ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(sceneId))
                {
                    visitedSceneIds.Add(sceneId);
                }
            }
        }

        private static void AddReportIssues(SaveRestoreReport source, SaveRestoreReport target)
        {
            foreach (var issue in source.Issues)
            {
                if (issue.Severity == SaveRestoreIssueSeverity.Error)
                {
                    target.AddError(issue.Code, issue.Message, issue.StableId);
                }
                else
                {
                    target.AddWarning(issue.Code, issue.Message, issue.StableId);
                }
            }
        }

        private bool TryMigrate(
            SaveGameSnapshot snapshot,
            out SaveGameSnapshot migrated,
            out SaveRestoreReport report)
        {
            try
            {
                migrated = migrationPipeline.MigrateToCurrent(snapshot);
                report = null;
                return true;
            }
            catch (Exception exception)
            {
                migrated = null;
                report = new SaveRestoreReport();
                report.AddError(
                    "snapshot-schema-invalid",
                    $"Save snapshot could not be migrated to the current schema: {exception.Message}");
                return false;
            }
        }
    }
}
