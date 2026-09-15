using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Setus.HorrorFramework.Player.Controller;
using Setus.HorrorFramework.Player.State;
using Setus.HorrorFramework.SaveProgression.Checkpoints;
using Setus.HorrorFramework.SaveProgression.Registry;
using Setus.HorrorFramework.SaveProgression.Serialization;
using UnityEngine;

namespace Setus.HorrorFramework.SaveProgression.SaveSlots
{
    public sealed class PersistentSaveSlotStore : ISaveSlotStore
    {
        private const string SaveDirectoryName = "SetusHorrorFramework/Saves";
        private const string FilePrefix = "slot-";
        private const string FileExtension = ".json";

        private readonly string rootDirectory;
        private readonly SaveStateTypeRegistry stateTypes;

        public PersistentSaveSlotStore(
            string rootDirectory = null,
            SaveStateTypeRegistry stateTypes = null)
        {
            this.rootDirectory = string.IsNullOrWhiteSpace(rootDirectory)
                ? Path.Combine(Application.persistentDataPath, SaveDirectoryName)
                : rootDirectory;
            this.stateTypes = stateTypes ?? HorrorSaveStateTypeRegistry.CreateDefault();
        }

        public void Save(SaveGameSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            Directory.CreateDirectory(rootDirectory);

            var targetPath = GetSlotFilePath(snapshot.SlotId);
            var temporaryPath = targetPath + ".tmp";
            var document = SaveFileDocument.FromSnapshot(snapshot, stateTypes);
            var json = JsonUtility.ToJson(document, true);

            try
            {
                File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));
                ReplaceAtomically(temporaryPath, targetPath);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        public bool TryLoad(SaveSlotId slotId, out SaveGameSnapshot snapshot)
        {
            var result = Read(slotId);
            snapshot = result.Snapshot;
            return result.IsValid;
        }

        public bool TryGetMetadata(SaveSlotId slotId, out SaveSlotMetadata metadata)
        {
            var result = Read(slotId);
            metadata = result.Metadata;
            return result.IsValid;
        }

        public bool HasSlot(SaveSlotId slotId)
        {
            return Read(slotId).IsValid;
        }

        public SaveSlotReadResult Read(SaveSlotId slotId)
        {
            var path = GetSlotFilePath(slotId);
            if (!File.Exists(path))
            {
                return SaveSlotReadResult.Invalid(
                    SaveSlotValidationStage.Missing,
                    "slot-missing",
                    $"Save slot '{slotId}' does not exist.");
            }

            SaveFileDocument document;
            string json;
            try
            {
                json = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception exception)
            {
                return SaveSlotReadResult.Invalid(
                    SaveSlotValidationStage.FileRead,
                    "slot-read-failed",
                    $"Save slot '{slotId}' could not be read: {exception.Message}");
            }

            try
            {
                document = JsonUtility.FromJson<SaveFileDocument>(json);
            }
            catch (Exception exception)
            {
                return SaveSlotReadResult.Invalid(
                    SaveSlotValidationStage.FileFormat,
                    "save-json-invalid",
                    $"Save slot '{slotId}' contains invalid JSON: {exception.Message}");
            }

            if (document == null)
            {
                return SaveSlotReadResult.Invalid(
                    SaveSlotValidationStage.FileFormat,
                    "save-document-invalid",
                    $"Save slot '{slotId}' did not contain a valid save document.");
            }

            if (!SaveFileFormatVersion.IsSupported(document.formatVersion))
            {
                return SaveSlotReadResult.Invalid(
                    SaveSlotValidationStage.FileFormat,
                    "save-format-unsupported",
                    $"Save slot '{slotId}' uses unsupported file format {document.formatVersion}.");
            }

            if (!document.IsValidFor(slotId))
            {
                return SaveSlotReadResult.Invalid(
                    SaveSlotValidationStage.FileFormat,
                    "save-document-invalid",
                    $"Save slot '{slotId}' has missing or inconsistent document fields.");
            }

            try
            {
                var metadata = document.ToMetadata();
                var snapshot = document.ToSnapshot(stateTypes);
                return SaveSlotReadResult.Valid(metadata, snapshot);
            }
            catch (SaveSlotReadException exception)
            {
                return SaveSlotReadResult.Invalid(
                    exception.FailureStage,
                    exception.ErrorCode,
                    exception.Message);
            }
            catch (Exception exception)
            {
                return SaveSlotReadResult.Invalid(
                    SaveSlotValidationStage.PayloadDecode,
                    "save-snapshot-invalid",
                    $"Save slot '{slotId}' could not be decoded: {exception.Message}");
            }
        }

        public string GetSlotFilePath(SaveSlotId slotId)
        {
            return Path.Combine(rootDirectory, FilePrefix + slotId.Value + FileExtension);
        }

        private static void ReplaceAtomically(string temporaryPath, string targetPath)
        {
            if (!File.Exists(targetPath))
            {
                File.Move(temporaryPath, targetPath);
                return;
            }

            try
            {
                File.Replace(temporaryPath, targetPath, null);
            }
            catch (PlatformNotSupportedException)
            {
                File.Copy(temporaryPath, targetPath, true);
                File.Delete(temporaryPath);
            }
        }

        [Serializable]
        private sealed class SaveFileDocument
        {
            public int formatVersion;
            public string slotId;
            public int schemaVersion;
            public long savedAtUtcTicks;
            public PersistedCheckpoint checkpoint;
            public List<PersistedGlobalState> globalStates;
            public List<PersistedStableState> stableStates;
            public List<string> visitedSceneIds;

            public static SaveFileDocument FromSnapshot(
                SaveGameSnapshot snapshot,
                SaveStateTypeRegistry stateTypes)
            {
                return new SaveFileDocument
                {
                    formatVersion = SaveFileFormatVersion.Current,
                    slotId = snapshot.SlotId.Value,
                    schemaVersion = snapshot.SchemaVersion,
                    savedAtUtcTicks = snapshot.SavedAtUtcTicks,
                    checkpoint = PersistedCheckpoint.FromCheckpoint(snapshot.Checkpoint),
                    globalStates = snapshot.GlobalStates
                        .OrderBy(state => state.StateKey, StringComparer.Ordinal)
                        .Select(state => PersistedGlobalState.FromRecord(state, stateTypes))
                        .ToList(),
                    stableStates = snapshot.StableStates
                        .OrderBy(state => state.StableId, StringComparer.Ordinal)
                        .ThenBy(state => state.StateKey, StringComparer.Ordinal)
                        .Select(state => PersistedStableState.FromRecord(state, stateTypes))
                        .ToList(),
                    visitedSceneIds = snapshot.VisitedSceneIds.ToList()
                };
            }

            public bool IsValidFor(SaveSlotId requestedSlotId)
            {
                return SaveFileFormatVersion.IsSupported(formatVersion) &&
                    string.Equals(slotId, requestedSlotId.Value, StringComparison.Ordinal) &&
                    schemaVersion >= 0 &&
                    globalStates != null &&
                    stableStates != null;
            }

            public SaveGameSnapshot ToSnapshot(SaveStateTypeRegistry stateTypes)
            {
                return new SaveGameSnapshot(
                    schemaVersion,
                    new SaveSlotId(slotId),
                    checkpoint?.ToCheckpoint(),
                    globalStates.Select(state => state.ToRecord(formatVersion, stateTypes)),
                    stableStates.Select(state => state.ToRecord(formatVersion, stateTypes)),
                    savedAtUtcTicks,
                    visitedSceneIds ?? new List<string>());
            }

            public SaveSlotMetadata ToMetadata()
            {
                return new SaveSlotMetadata(
                    new SaveSlotId(slotId),
                    formatVersion,
                    schemaVersion,
                    checkpoint?.sceneName,
                    checkpoint?.checkpointId,
                    checkpoint?.spawnId,
                    savedAtUtcTicks);
            }
        }

        [Serializable]
        private sealed class PersistedCheckpoint
        {
            public string checkpointId;
            public string sceneName;
            public string spawnId;
            public bool hasPlayerPose;
            public Vector3 playerPosition;
            public Quaternion playerRotation;
            public float cameraPitch;
            public int playerControlState;
            public long capturedAtUtcTicks;

            public static PersistedCheckpoint FromCheckpoint(CheckpointModel checkpoint)
            {
                if (checkpoint == null)
                {
                    return null;
                }

                var pose = checkpoint.PlayerPose;
                return new PersistedCheckpoint
                {
                    checkpointId = checkpoint.CheckpointId,
                    sceneName = checkpoint.SceneName,
                    spawnId = checkpoint.SpawnId,
                    hasPlayerPose = pose.HasValue,
                    playerPosition = pose.HasValue ? pose.Value.Position : default,
                    playerRotation = pose.HasValue ? pose.Value.Rotation : default,
                    cameraPitch = pose.HasValue ? pose.Value.CameraPitch : 0f,
                    playerControlState = pose.HasValue ? (int)pose.Value.ControlState : 0,
                    capturedAtUtcTicks = checkpoint.CapturedAtUtcTicks
                };
            }

            public CheckpointModel ToCheckpoint()
            {
                PlayerPose? pose = null;
                if (hasPlayerPose)
                {
                    pose = new PlayerPose(
                        playerPosition,
                        playerRotation,
                        cameraPitch,
                        (PlayerControlState)playerControlState);
                }

                return new CheckpointModel(
                    checkpointId,
                    sceneName,
                    spawnId,
                    pose,
                    capturedAtUtcTicks);
            }
        }

        [Serializable]
        private sealed class PersistedGlobalState
        {
            public string stateKey;
            public string stateTypeKey;
            public string stateTypeName;
            public PersistedStatePayload payload;

            public static PersistedGlobalState FromRecord(
                SaveStateRecord record,
                SaveStateTypeRegistry stateTypes)
            {
                if (record == null)
                {
                    throw new InvalidDataException("A global save state record was null.");
                }

                var codec = stateTypes.Resolve(record.StateTypeKey);
                return new PersistedGlobalState
                {
                    stateKey = record.StateKey,
                    stateTypeKey = codec.TypeKey,
                    payload = PersistedStatePayload.FromState(record.State, codec)
                };
            }

            public SaveStateRecord ToRecord(int formatVersion, SaveStateTypeRegistry stateTypes)
            {
                var codec = ResolveCodec(formatVersion, stateTypeKey, stateTypeName, stateTypes);
                return new SaveStateRecord(stateKey, codec.TypeKey, payload.ToState(codec));
            }
        }

        [Serializable]
        private sealed class PersistedStableState
        {
            public string stableId;
            public string stateKey;
            public string stateTypeKey;
            public string stateTypeName;
            public PersistedStatePayload payload;
            public bool hadActiveState;
            public SaveRestorePolicy activeRestorePolicy;
            public string sceneId;

            public static PersistedStableState FromRecord(
                StableSaveStateRecord record,
                SaveStateTypeRegistry stateTypes)
            {
                if (record == null)
                {
                    throw new InvalidDataException("A stable save state record was null.");
                }

                var codec = stateTypes.Resolve(record.StateTypeKey);
                return new PersistedStableState
                {
                    stableId = record.StableId,
                    stateKey = record.StateKey,
                    stateTypeKey = codec.TypeKey,
                    payload = PersistedStatePayload.FromState(record.State, codec),
                    hadActiveState = record.HadActiveState,
                    activeRestorePolicy = record.ActiveRestorePolicy,
                    sceneId = record.SceneId
                };
            }

            public StableSaveStateRecord ToRecord(int formatVersion, SaveStateTypeRegistry stateTypes)
            {
                var codec = ResolveCodec(formatVersion, stateTypeKey, stateTypeName, stateTypes);
                return new StableSaveStateRecord(
                    stableId,
                    stateKey,
                    codec.TypeKey,
                    payload.ToState(codec),
                    hadActiveState,
                    activeRestorePolicy,
                    sceneId);
            }
        }

        [Serializable]
        private sealed class PersistedStatePayload
        {
            public string kind;
            public string value;

            public static PersistedStatePayload FromState(object state, ISaveStateCodec codec)
            {
                if (state == null)
                {
                    throw new InvalidDataException("Save state payload must not be null.");
                }

                return new PersistedStatePayload
                {
                    value = codec.Serialize(state)
                };
            }

            public object ToState(ISaveStateCodec codec)
            {
                if (codec == null)
                {
                    throw new ArgumentNullException(nameof(codec));
                }

                try
                {
                    return codec.Deserialize(value);
                }
                catch (Exception exception)
                {
                    throw new SaveSlotReadException(
                        SaveSlotValidationStage.PayloadDecode,
                        "state-payload-decode-failed",
                        $"Save state payload for '{codec.TypeKey}' could not be decoded: {exception.Message}");
                }
            }
        }

        private static ISaveStateCodec ResolveCodec(
            int formatVersion,
            string stateTypeKey,
            string legacyTypeName,
            SaveStateTypeRegistry stateTypes)
        {
            if (formatVersion == SaveFileFormatVersion.Current)
            {
                try
                {
                    return stateTypes.Resolve(stateTypeKey);
                }
                catch (Exception exception)
                {
                    throw new SaveSlotReadException(
                        SaveSlotValidationStage.TypeKeyResolution,
                        "unknown-state-type-key",
                        $"Save state type key '{stateTypeKey}' is not registered: {exception.Message}");
                }
            }

            if (formatVersion == SaveFileFormatVersion.LegacyClrTypeIdentity)
            {
                try
                {
                    return stateTypes.ResolveLegacy(legacyTypeName);
                }
                catch (Exception exception)
                {
                    throw new SaveSlotReadException(
                        SaveSlotValidationStage.TypeKeyResolution,
                        "legacy-state-type-unmapped",
                        $"Legacy save state identity '{legacyTypeName}' is not mapped: {exception.Message}");
                }
            }

            throw new SaveSlotReadException(
                SaveSlotValidationStage.FileFormat,
                "save-format-unsupported",
                $"Unsupported save file format version: {formatVersion}.");
        }

        private sealed class SaveSlotReadException : Exception
        {
            public SaveSlotReadException(
                SaveSlotValidationStage failureStage,
                string errorCode,
                string message)
                : base(message)
            {
                FailureStage = failureStage;
                ErrorCode = errorCode;
            }

            public SaveSlotValidationStage FailureStage { get; }
            public string ErrorCode { get; }
        }
    }
}
