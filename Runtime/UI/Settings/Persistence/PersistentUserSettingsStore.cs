using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Setus.HorrorFramework.UI.Settings.Persistence
{
    public sealed class PersistentUserSettingsStore : IUserSettingsStore
    {
        private const int CurrentFormatVersion = 1;
        private const string SettingsDirectoryName = "SetusHorrorFramework";
        private const string SettingsFileName = "user-settings.json";

        private readonly string filePath;

        public PersistentUserSettingsStore(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("User settings file path must not be empty.", nameof(filePath));
            }

            this.filePath = filePath;
        }

        public string FilePath => filePath;

        public static PersistentUserSettingsStore CreateDefault()
        {
            return new PersistentUserSettingsStore(Path.Combine(
                Application.persistentDataPath,
                SettingsDirectoryName,
                SettingsFileName));
        }

        public UserSettingsLoadResult Load()
        {
            var backupPath = GetBackupPath();
            var hasPrimary = File.Exists(filePath);
            var hasBackup = File.Exists(backupPath);
            var primaryDiagnostic = string.Empty;
            var backupDiagnostic = string.Empty;
            if (!hasPrimary && !hasBackup)
            {
                return UserSettingsLoadResult.Missing();
            }

            if (hasPrimary && TryRead(filePath, out var primary, out primaryDiagnostic))
            {
                return UserSettingsLoadResult.Loaded(primary);
            }

            if (hasBackup && TryRead(backupPath, out var backup, out backupDiagnostic))
            {
                return UserSettingsLoadResult.Recovered(
                    backup,
                    "The primary user settings file was invalid; the last-known-good backup was loaded.");
            }

            var diagnostic = hasPrimary
                ? primaryDiagnostic
                : backupDiagnostic;
            return UserSettingsLoadResult.Invalid(
                string.IsNullOrWhiteSpace(diagnostic)
                    ? "No valid user settings document could be loaded."
                    : diagnostic);
        }

        public void Save(RuntimeSettingsState state)
        {
            var validation = RuntimeSettingsModel.ValidateState(state);
            if (!validation.IsValid)
            {
                throw new InvalidDataException(
                    $"User settings cannot be persisted: {validation.Reason}");
            }

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporaryPath = filePath + ".tmp";
            var document = new UserSettingsDocument
            {
                formatVersion = CurrentFormatVersion,
                settings = state
            };

            try
            {
                File.WriteAllText(
                    temporaryPath,
                    JsonUtility.ToJson(document, true),
                    new UTF8Encoding(false));
                ReplaceAtomically(temporaryPath, filePath, GetBackupPath());
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private string GetBackupPath()
        {
            return filePath + ".bak";
        }

        private static bool TryRead(
            string path,
            out RuntimeSettingsState state,
            out string diagnostic)
        {
            state = null;
            diagnostic = string.Empty;
            try
            {
                var json = File.ReadAllText(path, Encoding.UTF8);
                var root = JToken.Parse(json);
                if (root.Type != JTokenType.Object ||
                    root["formatVersion"]?.Type != JTokenType.Integer ||
                    root["settings"]?.Type != JTokenType.Object)
                {
                    diagnostic = "User settings JSON has an invalid document shape.";
                    return false;
                }

                var display = root["settings"]["displaySettings"];
                if (display != null && display.Type != JTokenType.Object &&
                    display.Type != JTokenType.Null)
                {
                    diagnostic = "User settings displaySettings must be an object or null.";
                    return false;
                }

                var document = JsonUtility.FromJson<UserSettingsDocument>(json);
                if (document == null || document.formatVersion != CurrentFormatVersion)
                {
                    diagnostic = "User settings use an unsupported format version.";
                    return false;
                }

                var validation = RuntimeSettingsModel.ValidateState(document.settings);
                if (!validation.IsValid)
                {
                    diagnostic = $"User settings failed semantic validation: {validation.Reason}";
                    return false;
                }

                state = document.settings;
                return true;
            }
            catch (Exception exception)
            {
                diagnostic = $"User settings could not be read: {exception.Message}";
                return false;
            }
        }

        private static void ReplaceAtomically(
            string temporaryPath,
            string targetPath,
            string backupPath)
        {
            if (!File.Exists(targetPath))
            {
                File.Move(temporaryPath, targetPath);
                return;
            }

            try
            {
                File.Replace(temporaryPath, targetPath, backupPath);
            }
            catch (PlatformNotSupportedException)
            {
                File.Copy(targetPath, backupPath, true);
                File.Copy(temporaryPath, targetPath, true);
                File.Delete(temporaryPath);
            }
        }

        [Serializable]
        private sealed class UserSettingsDocument
        {
            public int formatVersion;
            public RuntimeSettingsState settings;
        }
    }
}
