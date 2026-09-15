using System;
using UnityEngine;

namespace Setus.HorrorFramework.SaveProgression.StableIds
{
    [DisallowMultipleComponent]
    public sealed class StableId : MonoBehaviour, IStableIdProvider
    {
        [SerializeField] private string stableId;

        string IStableIdProvider.StableId => stableId;

        public string Id => stableId;
        public UnityEngine.Object Owner => this;
        public bool HasStableId => !string.IsNullOrWhiteSpace(stableId);

        private void Reset()
        {
            EnsureStableId();
        }

        private void OnValidate()
        {
            EnsureStableId();
        }

        public void Assign(string id)
        {
            stableId = id;
        }

        private void EnsureStableId()
        {
            if (!string.IsNullOrWhiteSpace(stableId))
            {
                return;
            }

            stableId = Guid.NewGuid().ToString("N");
        }
    }
}
