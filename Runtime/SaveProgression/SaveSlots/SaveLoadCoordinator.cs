using System;
using System.Collections.Generic;
using System.Linq;
using Setus.HorrorFramework.Debugging.Logging;
using Setus.HorrorFramework.SaveProgression.Checkpoints;
using Setus.HorrorFramework.SaveProgression.Registry;
using Setus.HorrorFramework.SaveProgression.StableIds;
using Setus.HorrorFramework.SceneFlow.Transitions;

namespace Setus.HorrorFramework.SaveProgression.SaveSlots
{
    public sealed class SaveLoadCoordinator
    {
        private readonly SaveGameOwner saveOwner;
        private readonly ISaveSlotStore slotStore;
        private readonly IHorrorLogger logger;
        private readonly Dictionary<SaveSlotId, SaveSlotValidationResult> validationBySlot =
            new Dictionary<SaveSlotId, SaveSlotValidationResult>();

        private StableIdManifest manifest;
        private SaveGameSnapshot pendingRestoreSnapshot;
        private SceneTransitionRequest pendingRestoreRequest;
        private int cachedGlobalStateDeclarationRevision = -1;
        private SaveLoadUserFailure? currentUserFailure;

        public SaveLoadCoordinator(
            SaveGameOwner saveOwner,
            ISaveSlotStore slotStore,
            IHorrorLogger logger = null)
        {
            this.saveOwner = saveOwner ?? throw new ArgumentNullException(nameof(saveOwner));
            this.slotStore = slotStore ?? throw new ArgumentNullException(nameof(slotStore));
            this.logger = logger;
        }

        public bool IsConfigured => manifest != null;
        public StableIdManifest Manifest => manifest;
        public SaveLoadUserFailure? CurrentUserFailure => currentUserFailure;
        public event Action<SaveLoadUserFailure?> UserFailureChanged;

        public void Configure(StableIdManifest configuredManifest)
        {
            if (configuredManifest == null)
            {
                throw new ArgumentNullException(
                    nameof(configuredManifest),
                    "Production save/load requires an authored StableIdManifest.");
            }

            if (manifest != null && manifest != configuredManifest)
            {
                throw new InvalidOperationException(
                    "Save/load was already configured with a different StableIdManifest.");
            }

            manifest = configuredManifest;
            validationBySlot.Clear();
            cachedGlobalStateDeclarationRevision = saveOwner.GlobalStateDeclarationRevision;
            ClearUserFailure();
        }

        public SaveCaptureResult CaptureAndSave(SaveSlotId slotId, CheckpointModel checkpoint)
        {
            EnsureConfigured();
            var result = saveOwner.CaptureWithReport(slotId, manifest, checkpoint);
            slotStore.Save(result.Snapshot);
            InvalidateSlotValidation(slotId);
            ClearUserFailure();
            logger?.Log(HorrorLogCategory.SaveProgression, $"Saved durable slot '{slotId}'.");
            return result;
        }

        public bool TryCaptureAndSave(
            SaveSlotId slotId,
            CheckpointModel checkpoint,
            out SaveCaptureResult result,
            out string failureReason)
        {
            result = null;
            failureReason = null;
            try
            {
                result = CaptureAndSave(slotId, checkpoint);
                return true;
            }
            catch (Exception exception)
            {
                failureReason = $"Save capture failed: {exception.Message}";
                SetUserFailure(SaveLoadUserFailure.SaveFailed(), failureReason, true);
                return false;
            }
        }

        public bool HasValidSlot(SaveSlotId slotId)
        {
            return ValidateSlot(slotId).IsValid;
        }

        public SaveSlotValidationResult ValidateSlot(SaveSlotId slotId)
        {
            return ValidateSlot(slotId, refresh: false);
        }

        public void InvalidateSlotValidation(SaveSlotId slotId)
        {
            validationBySlot.Remove(slotId);
        }

        public bool TryPrepareContinue(
            SaveSlotId slotId,
            out SceneTransitionRequest request,
            out string failureReason)
        {
            request = default;
            failureReason = null;

            var validation = ValidateSlot(slotId, refresh: true);
            if (!validation.IsValid)
            {
                failureReason = validation.Diagnostic;
                SetUserFailure(
                    SaveLoadUserFailure.FromValidationStage(validation.FailureStage),
                    failureReason,
                    false);
                return false;
            }

            ClearUserFailure();
            request = new SceneTransitionRequest(
                validation.Metadata.SceneName,
                validation.Metadata.CheckpointId,
                validation.Metadata.SpawnId);
            pendingRestoreSnapshot = validation.Snapshot;
            pendingRestoreRequest = request;
            return true;
        }

        public bool TryRestorePrepared(
            SceneTransitionRequest completedRequest,
            out SaveRestoreReport report,
            out string failureReason)
        {
            report = null;
            failureReason = null;

            if (pendingRestoreSnapshot == null || !RequestsMatch(pendingRestoreRequest, completedRequest))
            {
                return true;
            }

            var snapshot = pendingRestoreSnapshot;
            ClearPendingRestore();

            try
            {
                report = saveOwner.Restore(snapshot, manifest);
                return true;
            }
            catch (Exception exception)
            {
                failureReason = $"Save restore failed: {GetTechnicalRestoreDiagnostic(exception)}";
                SetUserFailure(SaveLoadUserFailure.RestoreFailed(), failureReason, true);
                return false;
            }
        }

        public bool HasPreparedRestoreFor(SceneTransitionRequest request)
        {
            return pendingRestoreSnapshot != null && RequestsMatch(pendingRestoreRequest, request);
        }

        public void ClearPendingRestore()
        {
            pendingRestoreSnapshot = null;
            pendingRestoreRequest = default;
        }

        public bool TryRetainSceneState(string sceneId, out string failureReason)
        {
            failureReason = null;
            if (!IsConfigured)
            {
                return true;
            }

            try
            {
                var report = saveOwner.RetainSceneState(manifest, sceneId);
                if (!report.HasErrors)
                {
                    return true;
                }

                failureReason = FirstErrorMessage(report, "Scene state capture failed.");
                return false;
            }
            catch (Exception exception)
            {
                failureReason = $"Scene state capture failed: {exception.Message}";
                return false;
            }
        }

        public bool TryRestoreRetainedScene(
            string sceneId,
            out SaveRestoreReport report,
            out string failureReason)
        {
            report = null;
            failureReason = null;
            if (!IsConfigured)
            {
                return true;
            }

            try
            {
                report = saveOwner.RestoreRetainedScene(manifest, sceneId);
                if (!report.HasErrors)
                {
                    return true;
                }

                failureReason = FirstErrorMessage(report, "Scene state restore failed.");
                return false;
            }
            catch (Exception exception)
            {
                failureReason = $"Scene state restore failed: {exception.Message}";
                return false;
            }
        }

        public void ResetSessionState()
        {
            ClearPendingRestore();
            ClearUserFailure();
            saveOwner.ResetRetainedStableState();
            validationBySlot.Clear();
        }

        public bool TryGetSlotFailure(SaveSlotId slotId, out SaveLoadUserFailure failure)
        {
            var validation = ValidateSlot(slotId);
            if (validation.IsValid)
            {
                failure = default;
                return false;
            }

            failure = SaveLoadUserFailure.FromValidationStage(validation.FailureStage);
            return true;
        }

        internal void ReportUnavailable(string technicalDiagnostic)
        {
            SetUserFailure(SaveLoadUserFailure.Unavailable(), technicalDiagnostic, false);
        }

        private void EnsureConfigured()
        {
            if (!IsConfigured)
            {
                throw new InvalidOperationException(
                    "Production save/load requires an authored StableIdManifest before capture or restore.");
            }
        }

        private SaveSlotValidationResult ValidateSlot(SaveSlotId slotId, bool refresh)
        {
            if (!IsConfigured)
            {
                return SaveSlotValidationResult.Invalid(
                    SaveSlotValidationStage.Configuration,
                    "manifest-not-configured",
                    "Load unavailable: Stable ID manifest is not configured.");
            }

            var declarationRevision = saveOwner.GlobalStateDeclarationRevision;
            if (cachedGlobalStateDeclarationRevision != declarationRevision)
            {
                validationBySlot.Clear();
                cachedGlobalStateDeclarationRevision = declarationRevision;
            }

            if (!refresh && validationBySlot.TryGetValue(slotId, out var cached))
            {
                return cached;
            }

            var read = slotStore.Read(slotId);
            if (!read.IsValid)
            {
                var invalidRead = SaveSlotValidationResult.Invalid(
                    read.FailureStage,
                    read.ErrorCode,
                    $"Load unavailable: {read.Diagnostic}");
                validationBySlot[slotId] = invalidRead;
                return invalidRead;
            }

            var preflight = saveOwner.PreflightSlotRestore(read.Snapshot, manifest);
            if (!preflight.IsValid)
            {
                var issue = preflight.Report.Issues.FirstOrDefault(
                    candidate => candidate.Severity == SaveRestoreIssueSeverity.Error);
                var failureStage = GetPreflightFailureStage(issue?.Code);
                var diagnostic = issue == null
                    ? "Load unavailable: save restore preflight failed."
                    : $"Load unavailable: {issue.Message}";
                var invalidPreflight = SaveSlotValidationResult.Invalid(
                    failureStage,
                    issue?.Code ?? "restore-preflight-failed",
                    diagnostic,
                    preflight.Report);
                validationBySlot[slotId] = invalidPreflight;
                return invalidPreflight;
            }

            if (!HasValidCheckpoint(read.Metadata, preflight.Snapshot))
            {
                var invalidCheckpoint = SaveSlotValidationResult.Invalid(
                    SaveSlotValidationStage.Checkpoint,
                    "checkpoint-metadata-invalid",
                    "Load unavailable: slot checkpoint metadata is invalid.",
                    preflight.Report);
                validationBySlot[slotId] = invalidCheckpoint;
                return invalidCheckpoint;
            }

            var valid = SaveSlotValidationResult.Valid(read.Metadata, preflight.Snapshot, preflight.Report);
            validationBySlot[slotId] = valid;
            return valid;
        }

        private static bool HasValidCheckpoint(SaveSlotMetadata metadata, SaveGameSnapshot snapshot)
        {
            return snapshot.Checkpoint != null &&
                !string.IsNullOrWhiteSpace(metadata.SceneName) &&
                !string.IsNullOrWhiteSpace(metadata.CheckpointId) &&
                string.Equals(metadata.SceneName, snapshot.Checkpoint.SceneName, StringComparison.Ordinal) &&
                string.Equals(metadata.CheckpointId, snapshot.Checkpoint.CheckpointId, StringComparison.Ordinal);
        }

        private static SaveSlotValidationStage GetPreflightFailureStage(string errorCode)
        {
            if (string.Equals(errorCode, "snapshot-schema-invalid", StringComparison.Ordinal))
            {
                return SaveSlotValidationStage.SchemaMigration;
            }

            return errorCode != null && errorCode.StartsWith("state-semantic-", StringComparison.Ordinal)
                ? SaveSlotValidationStage.SemanticPreflight
                : SaveSlotValidationStage.StructuralPreflight;
        }

        private static bool RequestsMatch(SceneTransitionRequest left, SceneTransitionRequest right)
        {
            return string.Equals(left.SceneName, right.SceneName, StringComparison.Ordinal) &&
                string.Equals(left.CheckpointId, right.CheckpointId, StringComparison.Ordinal) &&
                string.Equals(left.SpawnId, right.SpawnId, StringComparison.Ordinal);
        }

        private static string FirstErrorMessage(SaveRestoreReport report, string fallback)
        {
            var issue = report.Issues.FirstOrDefault(
                candidate => candidate.Severity == SaveRestoreIssueSeverity.Error);
            return issue == null ? fallback : issue.Message;
        }

        private static string GetTechnicalRestoreDiagnostic(Exception exception)
        {
            if (exception is SaveValidationException validationException &&
                validationException.Report != null)
            {
                var issue = validationException.Report.Issues.FirstOrDefault(
                    candidate => candidate.Severity == SaveRestoreIssueSeverity.Error);
                if (issue != null)
                {
                    return $"[{issue.Code}] {issue.Message}";
                }
            }

            return exception.Message;
        }

        private void SetUserFailure(
            SaveLoadUserFailure failure,
            string technicalDiagnostic,
            bool isError)
        {
            currentUserFailure = failure;
            if (!string.IsNullOrWhiteSpace(technicalDiagnostic))
            {
                if (isError)
                {
                    logger?.Error(HorrorLogCategory.SaveProgression, technicalDiagnostic);
                }
                else
                {
                    logger?.Warning(HorrorLogCategory.SaveProgression, technicalDiagnostic);
                }
            }

            UserFailureChanged?.Invoke(currentUserFailure);
        }

        private void ClearUserFailure()
        {
            if (!currentUserFailure.HasValue)
            {
                return;
            }

            currentUserFailure = null;
            UserFailureChanged?.Invoke(null);
        }
    }
}
