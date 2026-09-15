using Setus.HorrorFramework.Core.Config;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Debugging.Logging;
using Setus.HorrorFramework.SceneFlow.Transitions;
using UnityEngine;

namespace Setus.HorrorFramework.SceneFlow.Loading
{
    [DisallowMultipleComponent]
    public sealed class BootFlowLoader : MonoBehaviour
    {
        [SerializeField] private HorrorFrameworkConfig frameworkConfig;
        [SerializeField] private StandaloneGameConfig gameConfig;
        [SerializeField] private SceneTransitionRunner transitionRunner;
        [SerializeField] private bool loadMainMenuOnStart = true;

        private bool hasStartedLoad;

        private void Awake()
        {
            if (frameworkConfig == null || gameConfig == null || transitionRunner == null)
            {
                throw new System.InvalidOperationException(
                    "BootFlowLoader requires HorrorFrameworkConfig, StandaloneGameConfig, and SceneTransitionRunner.");
            }

            var context = HorrorGameContext.Ensure();
            if (context.Config != frameworkConfig)
            {
                throw new System.InvalidOperationException(
                    "BootFlowLoader config does not match the active HorrorGameBootstrapper config.");
            }

            ConfigureSaveProgression(context);
        }

        private void Start()
        {
            if (!loadMainMenuOnStart || string.IsNullOrWhiteSpace(gameConfig.MainMenuSceneName))
            {
                return;
            }

            LoadMainMenuNow();
        }

        public void LoadMainMenuNow()
        {
            if (hasStartedLoad || string.IsNullOrWhiteSpace(gameConfig.MainMenuSceneName))
            {
                return;
            }

            hasStartedLoad = true;
            transitionRunner.LoadMainMenu();
        }

        private void ConfigureSaveProgression(HorrorGameContext context)
        {
            if (gameConfig == null || gameConfig.StableIdManifest == null)
            {
                context.Logger.Error(
                    HorrorLogCategory.SaveProgression,
                    "Production save/load is disabled because StandaloneGameConfig has no StableIdManifest.");
                return;
            }

            context.ConfigureSaveProgression(gameConfig.StableIdManifest);
        }
    }
}
