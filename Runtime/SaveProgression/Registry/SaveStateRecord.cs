using System;

namespace Setus.HorrorFramework.SaveProgression.Registry
{
    [Serializable]
    public sealed class SaveStateRecord
    {
        public SaveStateRecord(string stateKey, string stateTypeKey, object state)
        {
            if (string.IsNullOrWhiteSpace(stateKey))
            {
                throw new ArgumentException("State key must not be empty.", nameof(stateKey));
            }

            StateKey = stateKey;
            if (string.IsNullOrWhiteSpace(stateTypeKey))
            {
                throw new ArgumentException("State type key must not be empty.", nameof(stateTypeKey));
            }

            StateTypeKey = stateTypeKey;
            State = state;
        }

        public string StateKey { get; }
        public string StateTypeKey { get; }
        public object State { get; }
    }
}
