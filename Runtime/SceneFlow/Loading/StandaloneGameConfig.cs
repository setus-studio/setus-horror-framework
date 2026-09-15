using Setus.HorrorFramework.SaveProgression.StableIds;
using UnityEngine;

namespace Setus.HorrorFramework.SceneFlow.Loading
{
    [CreateAssetMenu(
        fileName = "StandaloneGameConfig",
        menuName = "Setus/Horror Framework/Scene Flow/Standalone Game Config")]
    public sealed class StandaloneGameConfig : ScriptableObject
    {
        [SerializeField] private string bootSceneName = "Boot";
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private string firstGameplaySceneName = "Gameplay";
        [SerializeField] private string defaultCheckpointId = "new-game";
        [SerializeField] private string defaultSpawnId = "player-start";
        [SerializeField] private StableIdManifest stableIdManifest;

        public string BootSceneName => bootSceneName;
        public string MainMenuSceneName => mainMenuSceneName;
        public string FirstGameplaySceneName => firstGameplaySceneName;
        public string DefaultCheckpointId => defaultCheckpointId;
        public string DefaultSpawnId => defaultSpawnId;
        public StableIdManifest StableIdManifest => stableIdManifest;
    }
}
