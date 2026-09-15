using System;

namespace Setus.HorrorFramework.SaveProgression.Registry
{
    [Serializable]
    public sealed class StableSaveStateRecord
    {
        public StableSaveStateRecord(
            string stableId,
            string stateKey,
            string stateTypeKey,
            object state,
            bool hadActiveState,
            SaveRestorePolicy activeRestorePolicy,
            string sceneId = null)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                throw new ArgumentException("Stable ID must not be empty.", nameof(stableId));
            }

            if (string.IsNullOrWhiteSpace(stateKey))
            {
                throw new ArgumentException("State key must not be empty.", nameof(stateKey));
            }

            if (string.IsNullOrWhiteSpace(stateTypeKey))
            {
                throw new ArgumentException("State type key must not be empty.", nameof(stateTypeKey));
            }

            StableId = stableId;
            StateKey = stateKey;
            StateTypeKey = stateTypeKey;
            State = state;
            HadActiveState = hadActiveState;
            ActiveRestorePolicy = activeRestorePolicy;
            SceneId = sceneId ?? string.Empty;
        }

        public string StableId { get; }
        public string StateKey { get; }
        public string StateTypeKey { get; }
        public object State { get; }
        public bool HadActiveState { get; }
        public SaveRestorePolicy ActiveRestorePolicy { get; }
        public string SceneId { get; }
    }
}
