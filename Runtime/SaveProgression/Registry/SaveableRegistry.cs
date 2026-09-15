using System;
using System.Collections.Generic;
using System.Linq;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.SaveProgression.SaveSlots;
using Setus.HorrorFramework.SaveProgression.Serialization;
using Setus.HorrorFramework.SaveProgression.StableIds;

namespace Setus.HorrorFramework.SaveProgression.Registry
{
    public sealed class SaveableRegistry
    {
        private readonly SaveStateTypeRegistry stateTypes;
        private readonly GlobalSaveStateRegistry globalStateDeclarations;

        private readonly Dictionary<string, List<ISaveableStateOwner>> stableOwnersById =
            new Dictionary<string, List<ISaveableStateOwner>>();

        private readonly Dictionary<string, List<IRuntimeStateOwner>> globalOwnersByKey =
            new Dictionary<string, List<IRuntimeStateOwner>>();

        public SaveableRegistry(
            SaveStateTypeRegistry stateTypes = null,
            GlobalSaveStateRegistry globalStateDeclarations = null)
        {
            this.stateTypes = stateTypes ?? HorrorSaveStateTypeRegistry.CreateDefault();
            this.globalStateDeclarations = globalStateDeclarations ?? new GlobalSaveStateRegistry();
        }

        public IReadOnlyList<ISaveableStateOwner> StableOwners =>
            stableOwnersById.Values.SelectMany(owners => owners).Where(IsOwnerAlive).ToArray();

        public IReadOnlyList<IRuntimeStateOwner> GlobalOwners =>
            globalOwnersByKey.Values.SelectMany(owners => owners).Where(IsOwnerAlive).ToArray();

        public IReadOnlyList<string> LiveStableSceneIds => StableOwners
            .Select(GetOwnerSceneId)
            .Where(sceneId => !string.IsNullOrWhiteSpace(sceneId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(sceneId => sceneId, StringComparer.Ordinal)
            .ToArray();

        public GlobalSaveStateRegistry GlobalStateDeclarations => globalStateDeclarations;

        public IDisposable RegisterStable(ISaveableStateOwner owner)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            var registeredStableId = owner.StableId;
            if (!stableOwnersById.TryGetValue(registeredStableId, out var owners))
            {
                owners = new List<ISaveableStateOwner>();
                stableOwnersById.Add(registeredStableId, owners);
            }

            owners.RemoveAll(existing => !IsOwnerAlive(existing));
            owners.Add(owner);
            return new RegistrySubscription(() => UnregisterStable(registeredStableId, owner));
        }

        public IDisposable RegisterGlobal(IRuntimeStateOwner owner)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            var registeredStateKey = owner.StateKey;
            if (!globalOwnersByKey.TryGetValue(registeredStateKey, out var owners))
            {
                owners = new List<IRuntimeStateOwner>();
                globalOwnersByKey.Add(registeredStateKey, owners);
            }

            owners.RemoveAll(existing => !IsOwnerAlive(existing));
            owners.Add(owner);
            return new RegistrySubscription(() => UnregisterGlobal(registeredStateKey, owner));
        }

        public StableIdValidationResult ValidateStableIds(StableIdManifest manifest)
        {
            return ValidateStableIds(manifest, null);
        }

        public StableIdValidationResult ValidateStableIds(StableIdManifest manifest, string sceneId)
        {
            var providers = GetStableOwnersForScene(sceneId)
                .Select(owner => new SaveableStableIdProvider(owner))
                .ToArray();
            var scoped = StableIdValidator.Validate(providers, manifest, sceneId);
            if (string.IsNullOrWhiteSpace(sceneId))
            {
                return scoped;
            }

            var issues = new List<StableIdValidationIssue>(scoped.Issues);
            var globallyDuplicatedIds = new HashSet<string>(
                scoped.Duplicates.Select(issue => issue.StableId),
                StringComparer.Ordinal);
            var allProviders = StableOwners
                .Select(owner => new SaveableStableIdProvider(owner))
                .ToArray();
            foreach (var duplicate in StableIdValidator.Validate(allProviders).Duplicates)
            {
                if (globallyDuplicatedIds.Add(duplicate.StableId))
                {
                    issues.Add(duplicate);
                }
            }

            return new StableIdValidationResult(issues);
        }

        public SaveRestoreReport ValidateForCapture(StableIdManifest manifest)
        {
            return ValidateForCapture(manifest, null);
        }

        public SaveRestoreReport ValidateForCapture(StableIdManifest manifest, string sceneId)
        {
            var report = CreateReportFromStableIdValidation(ValidateStableIds(manifest, sceneId));
            AddStableManifestDeclarationIssues(manifest, sceneId, report);
            AddDuplicateGlobalStateIssues(report);
            ValidateGlobalOwnersForCapture(SaveSchemaVersion.Current, report);
            AddActiveRestorePolicyIssues(report, sceneId);
            return report;
        }

        public SaveRestoreReport ValidateStableSceneForCapture(StableIdManifest manifest, string sceneId)
        {
            var report = CreateReportFromStableIdValidation(ValidateStableIds(manifest, sceneId));
            AddStableManifestDeclarationIssues(manifest, sceneId, report);
            AddActiveRestorePolicyIssues(report, sceneId);
            return report;
        }

        public IReadOnlyList<SaveStateRecord> CaptureGlobalStates()
        {
            return GlobalOwners
                .OrderBy(owner => owner.StateKey, StringComparer.Ordinal)
                .Select(CaptureGlobalState)
                .ToArray();
        }

        public IReadOnlyList<StableSaveStateRecord> CaptureStableStates()
        {
            return CaptureStableStates(null, null);
        }

        public IReadOnlyList<StableSaveStateRecord> CaptureStableStates(
            StableIdManifest manifest,
            string sceneId)
        {
            return GetStableOwnersForScene(sceneId)
                .Select(owner => new StableSaveStateRecord(
                    owner.StableId,
                    owner.StateKey,
                    stateTypes.Resolve(owner.StateType).TypeKey,
                    owner.CaptureState(),
                    owner.HasActiveState,
                    owner.ActiveRestorePolicy,
                    ResolveStableSceneId(owner, manifest, sceneId)))
                .ToArray();
        }

        public bool HasLiveStableOwners(string sceneId)
        {
            return GetStableOwnersForScene(sceneId).Any();
        }

        public SaveRestoreReport Restore(
            IEnumerable<SaveStateRecord> globalStates,
            IEnumerable<StableSaveStateRecord> stableStates,
            StableIdManifest manifest,
            int schemaVersion = SaveSchemaVersion.Current,
            string sceneId = null,
            IEnumerable<string> visitedSceneIds = null)
        {
            var report = new SaveRestoreReport();
            var plan = BuildRestorePlan(
                globalStates,
                stableStates,
                manifest,
                schemaVersion,
                sceneId,
                visitedSceneIds,
                report);
            if (report.HasErrors)
            {
                return report;
            }

            // Restore dependencies are global runtime state first, then authored world state.
            // The plan is fully validated and captures rollback state before this point.
            ApplyRestorePlan(plan, report);
            return report;
        }

        // This validates the data that is knowable before the target scene registers its stable owners.
        // Scene-dependent owner compatibility and rollback staging remain in Restore after scene load.
        public SaveRestoreReport ValidateSlotRestoreCompatibility(
            IEnumerable<SaveStateRecord> globalStates,
            IEnumerable<StableSaveStateRecord> stableStates,
            StableIdManifest manifest,
            int schemaVersion = SaveSchemaVersion.Current,
            string sceneId = null,
            IEnumerable<string> visitedSceneIds = null)
        {
            var report = new SaveRestoreReport();
            var globalRecords = (globalStates ?? Array.Empty<SaveStateRecord>()).ToArray();
            var stableRecords = (stableStates ?? Array.Empty<StableSaveStateRecord>()).ToArray();

            ValidateRecordCollection(
                globalRecords,
                stableRecords,
                manifest,
                schemaVersion,
                sceneId,
                visitedSceneIds,
                report);
            if (report.HasErrors)
            {
                return report;
            }

            ValidateSlotGlobalOwnerAvailability(schemaVersion, report);
            if (report.HasErrors)
            {
                return report;
            }

            foreach (var state in globalRecords.OrderBy(record => record.StateKey, StringComparer.Ordinal))
            {
                if (!ValidateRecordPayload(state.StateTypeKey, state.State, null, report))
                {
                    continue;
                }

                if (TryGetLiveGlobalOwner(state.StateKey, out var owner) &&
                    ValidateRecordCompatibility(
                        owner,
                        state.StateKey,
                        state.StateTypeKey,
                        state.State,
                        null,
                        report))
                {
                    ValidateOwnerSemantics(owner, state.State, null, report);
                }
            }

            foreach (var state in stableRecords
                         .OrderBy(record => record.StableId, StringComparer.Ordinal)
                         .ThenBy(record => record.StateKey, StringComparer.Ordinal))
            {
                ValidateRecordPayload(state.StateTypeKey, state.State, state.StableId, report);
            }

            return report;
        }

        public SaveRestoreReport RestoreStableScene(
            IEnumerable<StableSaveStateRecord> stableStates,
            StableIdManifest manifest,
            string sceneId)
        {
            var report = ValidateStableSceneForCapture(manifest, sceneId);
            if (report.HasErrors)
            {
                return report;
            }

            var plan = new RestorePlan();
            var records = (stableStates ?? Array.Empty<StableSaveStateRecord>()).ToArray();
            AddStableRestoreOperations(plan, records, manifest, sceneId, report);
            if (!report.HasErrors)
            {
                ApplyRestorePlan(plan, report);
            }

            return report;
        }

        public void Clear()
        {
            stableOwnersById.Clear();
            globalOwnersByKey.Clear();
        }

        private RestorePlan BuildRestorePlan(
            IEnumerable<SaveStateRecord> globalStates,
            IEnumerable<StableSaveStateRecord> stableStates,
            StableIdManifest manifest,
            int schemaVersion,
            string sceneId,
            IEnumerable<string> visitedSceneIds,
            SaveRestoreReport report)
        {
            var plan = new RestorePlan();
            AddReportIssues(ValidateForCapture(manifest, sceneId), report);

            var globalRecords = (globalStates ?? Array.Empty<SaveStateRecord>()).ToArray();
            var stableRecords = (stableStates ?? Array.Empty<StableSaveStateRecord>()).ToArray();
            ValidateRecordCollection(
                globalRecords,
                stableRecords,
                manifest,
                schemaVersion,
                sceneId,
                visitedSceneIds,
                report);
            if (report.HasErrors)
            {
                return plan;
            }

            foreach (var state in globalRecords.OrderBy(record => record.StateKey, StringComparer.Ordinal))
            {
                if (!TryGetLiveGlobalOwner(state.StateKey, out var owner))
                {
                    if (globalStateDeclarations.TryGet(state.StateKey, out var declaration) &&
                        declaration.IsRequiredForSchema(schemaVersion))
                    {
                        report.AddError(
                            "missing-required-global-owner",
                            $"Required global state owner is not registered: {state.StateKey}");
                    }
                    else
                    {
                        report.AddWarning(
                            "missing-optional-global-owner",
                            $"Optional global state owner is not registered; current state is preserved: {state.StateKey}");
                    }

                    continue;
                }

                TryAddRestoreOperation(
                    plan.GlobalOperations,
                    owner,
                    state.StateKey,
                    state.StateTypeKey,
                    state.State,
                    null,
                    report);
            }

            AddStableRestoreOperations(plan, stableRecords, manifest, sceneId, report);

            return plan;
        }

        private void AddStableRestoreOperations(
            RestorePlan plan,
            IEnumerable<StableSaveStateRecord> stableRecords,
            StableIdManifest manifest,
            string sceneId,
            SaveRestoreReport report)
        {
            foreach (var state in stableRecords
                         .OrderBy(record => record.StableId, StringComparer.Ordinal)
                         .ThenBy(record => record.StateKey, StringComparer.Ordinal))
            {
                if (!StableRecordAppliesToScene(state, manifest, sceneId))
                {
                    continue;
                }

                if (manifest != null &&
                    manifest.TryGetEntry(state.StableId, out var entry) &&
                    entry.Kind == StableIdManifestEntryKind.Retired)
                {
                    report.AddWarning(
                        "retired-id-skipped",
                        $"Retired stable ID was skipped during restore: {state.StableId}",
                        state.StableId);
                    continue;
                }

                if (!TryGetLiveStableOwner(state.StableId, sceneId, out var owner))
                {
                    ReportMissingSavedStableState(state, manifest, report);
                    continue;
                }

                if (state.HadActiveState && !IsActiveStateRestoreSupported(state, owner, manifest))
                {
                    report.AddError(
                        "active-state-unsupported",
                        $"Saved active state for '{state.StableId}' has restore policy FailRestore.",
                        state.StableId);
                    continue;
                }

                TryAddRestoreOperation(
                    plan.StableOperations,
                    owner,
                    state.StateKey,
                    state.StateTypeKey,
                    state.State,
                    state.StableId,
                    report);
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

        private void ValidateRecordCollection(
            IReadOnlyList<SaveStateRecord> globalRecords,
            IReadOnlyList<StableSaveStateRecord> stableRecords,
            StableIdManifest manifest,
            int schemaVersion,
            string sceneId,
            IEnumerable<string> visitedSceneIds,
            SaveRestoreReport report)
        {
            var globalKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in globalRecords)
            {
                if (record == null)
                {
                    report.AddError("invalid-global-record", "Snapshot contained a null global state record.");
                    continue;
                }

                if (!globalKeys.Add(record.StateKey))
                {
                    report.AddError(
                        "duplicate-global-record",
                        $"Snapshot contained duplicate global state key: {record.StateKey}");
                }

                if (!globalStateDeclarations.TryGet(record.StateKey, out var declaration))
                {
                    report.AddError(
                        "unknown-global-state-key",
                        $"Snapshot contained an undeclared global state key: {record.StateKey}");
                    continue;
                }

                if (!declaration.AppliesToSchema(schemaVersion))
                {
                    report.AddError(
                        "global-state-before-introduction",
                        $"Global state '{record.StateKey}' is not valid before schema version " +
                        $"{declaration.IntroducedInSchemaVersion}.");
                    continue;
                }

                if (!string.Equals(record.StateTypeKey, declaration.StateTypeKey, StringComparison.Ordinal))
                {
                    report.AddError(
                        "global-state-type-key-mismatch",
                        $"Global state '{record.StateKey}' requires type key " +
                        $"'{declaration.StateTypeKey}', but the snapshot used '{record.StateTypeKey}'.");
                }
            }

            foreach (var declaration in globalStateDeclarations.Declarations)
            {
                if (declaration.IsRequiredForSchema(schemaVersion) &&
                    !globalKeys.Contains(declaration.StateKey))
                {
                    report.AddError(
                        "missing-required-global-record",
                        $"Snapshot omitted required global state record: {declaration.StateKey}");
                }
            }

            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in stableRecords)
            {
                if (record == null)
                {
                    report.AddError("invalid-stable-record", "Snapshot contained a null stable state record.");
                    continue;
                }

                if (!stableIds.Add(record.StableId))
                {
                    report.AddError(
                        "duplicate-stable-record",
                        $"Snapshot contained duplicate stable ID: {record.StableId}",
                        record.StableId);
                }

                if (manifest != null &&
                    manifest.TryGetEntry(record.StableId, out var declaredEntry) &&
                    !string.IsNullOrWhiteSpace(record.SceneId) &&
                    !string.IsNullOrWhiteSpace(declaredEntry.SceneId) &&
                    !string.Equals(record.SceneId, declaredEntry.SceneId, StringComparison.Ordinal))
                {
                    report.AddError(
                        "stable-state-scene-mismatch",
                        $"Stable state '{record.StableId}' belongs to scene '{record.SceneId}', " +
                        $"but its manifest entry declares '{declaredEntry.SceneId}'.",
                        record.StableId);
                }
            }

            if (manifest == null)
            {
                return;
            }

            var visited = new HashSet<string>(
                (visitedSceneIds ?? Array.Empty<string>())
                    .Where(value => !string.IsNullOrWhiteSpace(value)),
                StringComparer.Ordinal);
            if (!string.IsNullOrWhiteSpace(sceneId))
            {
                visited.Add(sceneId);
            }

            foreach (var entry in manifest.Entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.StableId) ||
                    entry.Kind != StableIdManifestEntryKind.Required ||
                    (!string.IsNullOrWhiteSpace(entry.SceneId) && !visited.Contains(entry.SceneId)))
                {
                    continue;
                }

                if (!stableIds.Contains(entry.StableId))
                {
                    report.AddError(
                        "missing-required-record",
                        $"Snapshot omitted required stable state record: {entry.StableId}",
                        entry.StableId);
                }
            }
        }

        private SaveStateRecord CaptureGlobalState(IRuntimeStateOwner owner)
        {
            if (!globalStateDeclarations.TryGet(owner.StateKey, out var declaration))
            {
                throw new InvalidOperationException(
                    $"Cannot capture undeclared global state owner: {owner.StateKey}");
            }

            var codec = stateTypes.Resolve(owner.StateType);
            if (!string.Equals(codec.TypeKey, declaration.StateTypeKey, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Global state owner '{owner.StateKey}' resolves to type key '{codec.TypeKey}', " +
                    $"but its declaration requires '{declaration.StateTypeKey}'.");
            }

            var state = owner.CaptureState();
            if (state == null || !owner.StateType.IsInstanceOfType(state))
            {
                throw new InvalidOperationException(
                    $"Global state owner '{owner.StateKey}' did not capture a valid " +
                    $"{owner.StateType.FullName} record.");
            }

            return new SaveStateRecord(owner.StateKey, declaration.StateTypeKey, state);
        }

        private void ValidateGlobalOwnersForCapture(int schemaVersion, SaveRestoreReport report)
        {
            var liveOwners = GlobalOwners;
            var liveKeys = new HashSet<string>(
                liveOwners.Select(owner => owner.StateKey),
                StringComparer.Ordinal);

            foreach (var declaration in globalStateDeclarations.Declarations)
            {
                if (!declaration.AppliesToSchema(schemaVersion))
                {
                    continue;
                }

                if (!stateTypes.TryResolve(declaration.StateTypeKey, out _))
                {
                    report.AddError(
                        "global-declaration-type-key-unknown",
                        $"Global state declaration '{declaration.StateKey}' references unknown type key " +
                        $"'{declaration.StateTypeKey}'.");
                }

                if (declaration.Requirement == GlobalSaveStateRequirement.Required &&
                    !liveKeys.Contains(declaration.StateKey))
                {
                    report.AddError(
                        "missing-required-global-owner",
                        $"Required global state owner is not registered for capture: {declaration.StateKey}");
                }
            }

            foreach (var owner in liveOwners)
            {
                if (!globalStateDeclarations.TryGet(owner.StateKey, out var declaration))
                {
                    report.AddError(
                        "undeclared-global-state-owner",
                        $"Global state owner has no persistence declaration: {owner.StateKey}");
                    continue;
                }

                if (!declaration.AppliesToSchema(schemaVersion))
                {
                    report.AddError(
                        "global-owner-before-introduction",
                        $"Global state owner '{owner.StateKey}' is registered before its introduction " +
                        $"in schema version {declaration.IntroducedInSchemaVersion}.");
                    continue;
                }

                if (!stateTypes.TryResolve(declaration.StateTypeKey, out var codec))
                {
                    report.AddError(
                        "global-declaration-type-key-unknown",
                        $"Global state declaration '{owner.StateKey}' references unknown type key " +
                        $"'{declaration.StateTypeKey}'.");
                    continue;
                }

                if (codec.StateType != owner.StateType)
                {
                    report.AddError(
                        "global-owner-type-mismatch",
                        $"Global state owner '{owner.StateKey}' uses type '{owner.StateType.FullName}', " +
                        $"but declaration type key '{declaration.StateTypeKey}' resolves to " +
                        $"'{codec.StateType.FullName}'.");
                }
            }
        }

        private void ValidateSlotGlobalOwnerAvailability(int schemaVersion, SaveRestoreReport report)
        {
            foreach (var declaration in globalStateDeclarations.Declarations)
            {
                if (!declaration.IsRequiredForSchema(schemaVersion) ||
                    declaration.OwnerScope != GlobalSaveStateOwnerScope.Context)
                {
                    continue;
                }

                if (!TryGetLiveGlobalOwner(declaration.StateKey, out _))
                {
                    report.AddError(
                        "missing-required-global-owner",
                        $"Required context global state owner is not registered: {declaration.StateKey}");
                }
            }
        }

        private bool TryGetLiveGlobalOwner(string stateKey, out IRuntimeStateOwner owner)
        {
            owner = null;
            if (!globalOwnersByKey.TryGetValue(stateKey, out var owners))
            {
                return false;
            }

            owner = owners.FirstOrDefault(IsOwnerAlive);
            return owner != null;
        }

        private bool TryGetLiveStableOwner(
            string stableId,
            string sceneId,
            out ISaveableStateOwner owner)
        {
            owner = null;
            if (!stableOwnersById.TryGetValue(stableId, out var owners))
            {
                return false;
            }

            owner = owners.FirstOrDefault(candidate =>
                IsOwnerAlive(candidate) && OwnerAppliesToScene(candidate, sceneId));
            return owner != null;
        }

        private static bool IsActiveStateRestoreSupported(
            StableSaveStateRecord state,
            ISaveableStateOwner owner,
            StableIdManifest manifest)
        {
            if (state.ActiveRestorePolicy == SaveRestorePolicy.FailRestore ||
                owner.ActiveRestorePolicy == SaveRestorePolicy.FailRestore)
            {
                return false;
            }

            if (manifest == null || !manifest.TryGetEntry(state.StableId, out var entry))
            {
                return state.ActiveRestorePolicy == owner.ActiveRestorePolicy;
            }

            return entry.ActiveRestorePolicy != SaveRestorePolicy.FailRestore &&
                state.ActiveRestorePolicy == owner.ActiveRestorePolicy &&
                state.ActiveRestorePolicy == entry.ActiveRestorePolicy;
        }

        private void TryAddRestoreOperation(
            ICollection<RestoreOperation> operations,
            IRuntimeStateOwner owner,
            string stateKey,
            string stateTypeKey,
            object restoredState,
            string stableId,
            SaveRestoreReport report)
        {
            if (!ValidateRecordCompatibility(owner, stateKey, stateTypeKey, restoredState, stableId, report))
            {
                return;
            }

            if (!ValidateOwnerSemantics(owner, restoredState, stableId, report))
            {
                return;
            }

            try
            {
                var rollbackState = owner.CaptureState();
                if (rollbackState == null || !owner.StateType.IsInstanceOfType(rollbackState))
                {
                    report.AddError(
                        "rollback-state-invalid",
                        $"Cannot stage rollback state for '{stateKey}'.",
                        stableId);
                    return;
                }

                Action rollback = owner is IRuntimeRollbackOwner rollbackOwner
                    ? rollbackOwner.CaptureRollbackAction()
                    : () => owner.RestoreState(rollbackState);
                if (rollback == null)
                {
                    throw new InvalidOperationException("Owner returned no rollback action.");
                }
                operations.Add(new RestoreOperation(owner, restoredState, rollback, stableId));
            }
            catch (Exception exception)
            {
                report.AddError(
                    "rollback-state-capture-failed",
                    $"Cannot stage rollback state for '{stateKey}': {exception.Message}",
                    stableId);
            }
        }

        private bool ValidateRecordCompatibility(
            IRuntimeStateOwner owner,
            string stateKey,
            string stateTypeKey,
            object state,
            string stableId,
            SaveRestoreReport report)
        {
            if (!string.Equals(stateKey, owner.StateKey, StringComparison.Ordinal))
            {
                report.AddError(
                    "state-key-mismatch",
                    $"Saved state key '{stateKey}' does not match owner key '{owner.StateKey}'.",
                    stableId);
                return false;
            }

            if (!stateTypes.TryResolve(stateTypeKey, out var codec) || codec.StateType != owner.StateType)
            {
                report.AddError(
                    "state-type-mismatch",
                    $"Saved state type key '{stateTypeKey}' does not match owner type '{owner.StateType.FullName}'.",
                    stableId);
                return false;
            }

            if (state == null || !codec.StateType.IsInstanceOfType(state))
            {
                report.AddError(
                    "state-payload-invalid",
                    $"Saved state payload is incompatible with codec '{codec.TypeKey}'.",
                    stableId);
                return false;
            }

            return true;
        }

        private bool ValidateRecordPayload(
            string stateTypeKey,
            object state,
            string stableId,
            SaveRestoreReport report)
        {
            if (!stateTypes.TryResolve(stateTypeKey, out var codec))
            {
                report.AddError(
                    "unknown-state-type-key",
                    $"Saved state type key is not registered: {stateTypeKey}",
                    stableId);
                return false;
            }

            if (state == null || !codec.StateType.IsInstanceOfType(state))
            {
                report.AddError(
                    "state-payload-invalid",
                    $"Saved state payload is incompatible with codec '{codec.TypeKey}'.",
                    stableId);
                return false;
            }

            return true;
        }

        private static bool ValidateOwnerSemantics(
            IRuntimeStateOwner owner,
            object state,
            string stableId,
            SaveRestoreReport report)
        {
            if (!(owner is IRestoreStateValidator validator))
            {
                return true;
            }

            try
            {
                var result = validator.ValidateRestoreState(state);
                if (result.IsValid)
                {
                    return true;
                }

                var reason = string.IsNullOrWhiteSpace(result.Reason)
                    ? "Owner rejected the restored state without a diagnostic reason."
                    : result.Reason;
                report.AddError(
                    "state-semantic-invalid",
                    $"Semantic validation failed for owner '{owner.StateKey}': {reason}",
                    stableId);
            }
            catch (Exception exception)
            {
                report.AddError(
                    "state-semantic-validation-failed",
                    $"Semantic validation threw for owner '{owner.StateKey}': {exception.Message}",
                    stableId);
            }

            return false;
        }

        private static void ApplyRestorePlan(RestorePlan plan, SaveRestoreReport report)
        {
            var appliedOperations = new List<RestoreOperation>();
            try
            {
                ApplyOperations(plan.GlobalOperations, appliedOperations);
                ApplyOperations(plan.StableOperations, appliedOperations);
            }
            catch (Exception exception)
            {
                report.AddError(
                    "restore-apply-failed",
                    $"Restore apply failed: {exception.Message}");
                RollBack(appliedOperations, report);
            }
        }

        private static void ApplyOperations(
            IEnumerable<RestoreOperation> operations,
            ICollection<RestoreOperation> appliedOperations)
        {
            foreach (var operation in operations)
            {
                appliedOperations.Add(operation);
                operation.Owner.RestoreState(operation.RestoredState);
            }
        }

        private static void RollBack(IReadOnlyList<RestoreOperation> appliedOperations, SaveRestoreReport report)
        {
            for (var index = appliedOperations.Count - 1; index >= 0; index--)
            {
                var operation = appliedOperations[index];
                try
                {
                    operation.Rollback();
                }
                catch (Exception exception)
                {
                    report.AddError(
                        "restore-rollback-failed",
                        $"Rollback failed for '{operation.Owner.StateKey}': {exception.Message}",
                        operation.StableId);
                }
            }
        }

        private sealed class RestorePlan
        {
            public List<RestoreOperation> GlobalOperations { get; } = new List<RestoreOperation>();
            public List<RestoreOperation> StableOperations { get; } = new List<RestoreOperation>();
        }

        private sealed class RestoreOperation
        {
            public RestoreOperation(
                IRuntimeStateOwner owner,
                object restoredState,
                Action rollback,
                string stableId)
            {
                Owner = owner;
                RestoredState = restoredState;
                Rollback = rollback;
                StableId = stableId;
            }

            public IRuntimeStateOwner Owner { get; }
            public object RestoredState { get; }
            public Action Rollback { get; }
            public string StableId { get; }
        }

        private void ReportMissingSavedStableState(
            StableSaveStateRecord state,
            StableIdManifest manifest,
            SaveRestoreReport report)
        {
            var kind = StableIdManifestEntryKind.Optional;
            if (manifest != null && manifest.TryGetEntry(state.StableId, out var entry))
            {
                kind = entry.Kind;
            }

            if (kind == StableIdManifestEntryKind.Required)
            {
                report.AddError(
                    "missing-required-id",
                    $"Missing required saveable for stable ID: {state.StableId}",
                    state.StableId);
                return;
            }

            report.AddWarning(
                "missing-optional-id",
                $"Saved stable ID is absent and will be skipped: {state.StableId}",
                state.StableId);
        }

        private void UnregisterStable(string registeredStableId, ISaveableStateOwner owner)
        {
            if (!stableOwnersById.TryGetValue(registeredStableId, out var owners))
            {
                return;
            }

            owners.Remove(owner);
            if (owners.Count == 0)
            {
                stableOwnersById.Remove(registeredStableId);
            }
        }

        private void UnregisterGlobal(string registeredStateKey, IRuntimeStateOwner owner)
        {
            if (!globalOwnersByKey.TryGetValue(registeredStateKey, out var owners))
            {
                return;
            }

            owners.Remove(owner);
            if (owners.Count == 0)
            {
                globalOwnersByKey.Remove(registeredStateKey);
            }
        }

        private static bool IsOwnerAlive(IRuntimeStateOwner owner)
        {
            if (owner == null)
            {
                return false;
            }

            return !(owner is UnityEngine.Object unityOwner) || unityOwner != null;
        }

        private static SaveRestoreReport CreateReportFromStableIdValidation(StableIdValidationResult result)
        {
            var report = new SaveRestoreReport();
            foreach (var issue in result.Issues)
            {
                var message = issue.StableId;
                if (issue.IssueType == StableIdValidationIssueType.DuplicateId)
                {
                    message = $"Duplicate stable ID: {issue.StableId}";
                }
                else if (issue.IssueType == StableIdValidationIssueType.MissingRequiredId)
                {
                    message = $"Missing required stable ID: {issue.StableId}";
                }
                else if (issue.IssueType == StableIdValidationIssueType.MissingOptionalId)
                {
                    message = $"Missing optional stable ID: {issue.StableId}";
                }

                if (issue.IsError)
                {
                    report.AddError(issue.IssueType.ToString(), message, issue.StableId);
                }
                else
                {
                    report.AddWarning(issue.IssueType.ToString(), message, issue.StableId);
                }
            }

            return report;
        }

        private void AddDuplicateGlobalStateIssues(SaveRestoreReport report)
        {
            foreach (var pair in globalOwnersByKey.Where(pair => pair.Value.Count > 1))
            {
                report.AddError(
                    "duplicate-global-state-key",
                    $"Duplicate global state owner key: {pair.Key}");
            }
        }

        private void AddActiveRestorePolicyIssues(SaveRestoreReport report, string sceneId)
        {
            foreach (var owner in GetStableOwnersForScene(sceneId))
            {
                if (owner.HasActiveState && owner.ActiveRestorePolicy == SaveRestorePolicy.FailRestore)
                {
                    report.AddError(
                        "active-state-unsupported",
                        $"Active state owner '{owner.StableId}' cannot be saved with restore policy FailRestore.",
                        owner.StableId);
                }
            }
        }

        private void AddStableManifestDeclarationIssues(
            StableIdManifest manifest,
            string sceneId,
            SaveRestoreReport report)
        {
            if (manifest == null)
            {
                return;
            }

            foreach (var owner in GetStableOwnersForScene(sceneId))
            {
                if (!manifest.TryGetEntry(owner.StableId, out var entry))
                {
                    report.AddError(
                        "stable-owner-undeclared",
                        $"Stable owner '{owner.StableId}' is not declared by the configured manifest.",
                        owner.StableId);
                    continue;
                }

                if (!entry.AppliesToScene(sceneId))
                {
                    report.AddError(
                        "stable-owner-scene-mismatch",
                        $"Stable owner '{owner.StableId}' is live in scene '{sceneId}', but its manifest " +
                        $"entry applies to scene '{entry.SceneId}'.",
                        owner.StableId);
                }
            }
        }

        private IEnumerable<ISaveableStateOwner> GetStableOwnersForScene(string sceneId)
        {
            return StableOwners.Where(owner => OwnerAppliesToScene(owner, sceneId));
        }

        private static bool OwnerAppliesToScene(ISaveableStateOwner owner, string sceneId)
        {
            if (string.IsNullOrWhiteSpace(sceneId))
            {
                return true;
            }

            var ownerSceneId = GetOwnerSceneId(owner);
            return string.IsNullOrWhiteSpace(ownerSceneId) ||
                string.Equals(ownerSceneId, sceneId, StringComparison.Ordinal);
        }

        private static string GetOwnerSceneId(ISaveableStateOwner owner)
        {
            if (owner is ISceneScopedSaveableStateOwner scopedOwner)
            {
                return scopedOwner.SceneId ?? string.Empty;
            }

            if (owner is UnityEngine.Component component && component.gameObject.scene.IsValid())
            {
                return component.gameObject.scene.name;
            }

            return string.Empty;
        }

        private static string ResolveStableSceneId(
            ISaveableStateOwner owner,
            StableIdManifest manifest,
            string fallbackSceneId)
        {
            if (manifest != null && manifest.TryGetEntry(owner.StableId, out var entry) &&
                !string.IsNullOrWhiteSpace(entry.SceneId))
            {
                return entry.SceneId;
            }

            var ownerSceneId = GetOwnerSceneId(owner);
            return !string.IsNullOrWhiteSpace(ownerSceneId)
                ? ownerSceneId
                : fallbackSceneId ?? string.Empty;
        }

        private static bool StableRecordAppliesToScene(
            StableSaveStateRecord state,
            StableIdManifest manifest,
            string sceneId)
        {
            if (string.IsNullOrWhiteSpace(sceneId))
            {
                return true;
            }

            var recordSceneId = state.SceneId;
            if (string.IsNullOrWhiteSpace(recordSceneId) && manifest != null &&
                manifest.TryGetEntry(state.StableId, out var entry))
            {
                recordSceneId = entry.SceneId;
            }

            return string.IsNullOrWhiteSpace(recordSceneId) ||
                string.Equals(recordSceneId, sceneId, StringComparison.Ordinal);
        }

        private sealed class RegistrySubscription : IDisposable
        {
            private readonly Action dispose;
            private bool disposed;

            public RegistrySubscription(Action dispose)
            {
                this.dispose = dispose;
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                dispose();
            }
        }

        private sealed class SaveableStableIdProvider : IStableIdProvider
        {
            private readonly ISaveableStateOwner owner;

            public SaveableStableIdProvider(ISaveableStateOwner owner)
            {
                this.owner = owner;
            }

            public string StableId => owner.StableId;
            public UnityEngine.Object Owner => null;
        }
    }
}
