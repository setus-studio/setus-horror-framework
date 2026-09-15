using System;
using System.Collections.Generic;
using UnityEngine;

namespace Setus.HorrorFramework.SaveProgression.StableIds
{
    [CreateAssetMenu(menuName = "Setus/Horror Framework/Save/Stable ID Manifest")]
    public sealed class StableIdManifest : ScriptableObject
    {
        [SerializeField] private List<StableIdManifestEntry> entries = new List<StableIdManifestEntry>();

        public IReadOnlyList<StableIdManifestEntry> Entries => entries;

        public bool TryGetEntry(string stableId, out StableIdManifestEntry entry)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                entry = null;
                return false;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var candidate = entries[i];
                if (candidate != null && string.Equals(candidate.StableId, stableId, StringComparison.Ordinal))
                {
                    entry = candidate;
                    return true;
                }
            }

            entry = null;
            return false;
        }

        public void ReplaceEntries(IEnumerable<StableIdManifestEntry> replacementEntries)
        {
            entries.Clear();
            if (replacementEntries == null)
            {
                return;
            }

            foreach (var entry in replacementEntries)
            {
                if (entry != null)
                {
                    entries.Add(entry);
                }
            }
        }
    }
}
