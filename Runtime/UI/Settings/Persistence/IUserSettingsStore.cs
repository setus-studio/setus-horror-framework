namespace Setus.HorrorFramework.UI.Settings.Persistence
{
    public enum UserSettingsLoadStatus
    {
        Missing = 0,
        Loaded = 1,
        RecoveredFromBackup = 2,
        Invalid = 3
    }

    public readonly struct UserSettingsLoadResult
    {
        private UserSettingsLoadResult(
            UserSettingsLoadStatus status,
            RuntimeSettingsState state,
            string diagnostic)
        {
            Status = status;
            State = state;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public UserSettingsLoadStatus Status { get; }
        public RuntimeSettingsState State { get; }
        public string Diagnostic { get; }

        public static UserSettingsLoadResult Missing()
        {
            return new UserSettingsLoadResult(UserSettingsLoadStatus.Missing, null, string.Empty);
        }

        public static UserSettingsLoadResult Loaded(RuntimeSettingsState state)
        {
            return new UserSettingsLoadResult(UserSettingsLoadStatus.Loaded, state, string.Empty);
        }

        public static UserSettingsLoadResult Recovered(RuntimeSettingsState state, string diagnostic)
        {
            return new UserSettingsLoadResult(
                UserSettingsLoadStatus.RecoveredFromBackup,
                state,
                diagnostic);
        }

        public static UserSettingsLoadResult Invalid(string diagnostic)
        {
            return new UserSettingsLoadResult(UserSettingsLoadStatus.Invalid, null, diagnostic);
        }
    }

    public interface IUserSettingsStore
    {
        UserSettingsLoadResult Load();
        void Save(RuntimeSettingsState state);
    }
}
