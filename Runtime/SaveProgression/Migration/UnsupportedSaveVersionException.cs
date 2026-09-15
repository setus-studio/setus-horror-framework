using System;

namespace Setus.HorrorFramework.SaveProgression.Migration
{
    public sealed class UnsupportedSaveVersionException : Exception
    {
        public UnsupportedSaveVersionException(int version, int currentVersion)
            : base($"Unsupported save schema version {version}. Current version is {currentVersion}.")
        {
            Version = version;
            CurrentVersion = currentVersion;
        }

        public int Version { get; }
        public int CurrentVersion { get; }
    }
}
