using System;
using Setus.HorrorFramework.SaveProgression.Registry;
using UnityEngine;

namespace Setus.HorrorFramework.SaveProgression.StableIds
{
    [Serializable]
    public sealed class StableIdManifestEntry
    {
        [SerializeField] private string stableId;
        [SerializeField] private StableIdManifestEntryKind kind;
        [SerializeField] private SaveRestorePolicy activeRestorePolicy;
        [SerializeField] private string note;
        [SerializeField] private string sceneId;

        public StableIdManifestEntry(
            string stableId,
            StableIdManifestEntryKind kind,
            SaveRestorePolicy activeRestorePolicy = SaveRestorePolicy.ResumeDeterministicPhase,
            string note = null,
            string sceneId = null)
        {
            this.stableId = stableId;
            this.kind = kind;
            this.activeRestorePolicy = activeRestorePolicy;
            this.note = note;
            this.sceneId = sceneId ?? string.Empty;
        }

        public string StableId => stableId;
        public StableIdManifestEntryKind Kind => kind;
        public SaveRestorePolicy ActiveRestorePolicy => activeRestorePolicy;
        public string Note => note;
        public string SceneId => sceneId ?? string.Empty;

        public bool AppliesToScene(string candidateSceneId)
        {
            return string.IsNullOrWhiteSpace(SceneId) ||
                string.IsNullOrWhiteSpace(candidateSceneId) ||
                string.Equals(SceneId, candidateSceneId, StringComparison.Ordinal);
        }
    }
}
