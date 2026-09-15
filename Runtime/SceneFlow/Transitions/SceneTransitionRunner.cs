using System.Collections;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Debugging.Logging;
using Setus.HorrorFramework.SaveProgression.SaveSlots;
using Setus.HorrorFramework.SceneFlow.Loading;
using Setus.HorrorFramework.SceneFlow.Spawn;
using Setus.HorrorFramework.UI.Menus;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Setus.HorrorFramework.SceneFlow.Transitions
{
    [DisallowMultipleComponent]
    public sealed class SceneTransitionRunner : MonoBehaviour
    {
        [SerializeField] private StandaloneGameConfig gameConfig;

        private ISceneTransitionService transitions;
        private SaveLoadCoordinator saveLoad;
        private Coroutine activeRoutine;
        private int activeTransitionId;

        private void Awake()
        {
            var context = HorrorGameContext.Ensure();
            transitions = context.Services.GetRequired<ISceneTransitionService>();
            saveLoad = context.Services.GetRequired<SaveLoadCoordinator>();

            if (gameConfig == null)
            {
                throw new System.InvalidOperationException(
                    "SceneTransitionRunner requires an assigned StandaloneGameConfig.");
            }
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            CancelActiveTransition("Scene transition runner was disabled.");
        }

        public void LoadMainMenu()
        {
            if (gameConfig == null)
            {
                return;
            }

            TransitionTo(new SceneTransitionRequest(gameConfig.MainMenuSceneName));
        }

        public void StartNewGame()
        {
            if (gameConfig == null)
            {
                return;
            }

            HorrorGameContext.Active?.ResetForNewGameSession();
            TransitionTo(new SceneTransitionRequest(
                gameConfig.FirstGameplaySceneName,
                gameConfig.DefaultCheckpointId,
                gameConfig.DefaultSpawnId));
        }

        public bool TryContinueFromSave(SaveSlotId slotId, out string failureReason)
        {
            failureReason = null;
            if (activeRoutine != null)
            {
                failureReason = "Continue unavailable while another scene transition is running.";
                saveLoad?.ReportUnavailable(failureReason);
                return false;
            }

            if (saveLoad == null || !saveLoad.TryPrepareContinue(slotId, out var request, out failureReason))
            {
                return false;
            }

            TransitionTo(request);
            return true;
        }

        public void TransitionTo(string sceneName)
        {
            TransitionTo(new SceneTransitionRequest(sceneName));
        }

        public void TransitionTo(SceneTransitionRequest request)
        {
            if (activeRoutine != null)
            {
                return;
            }

            if (!TryBeginTransition(request, out var operation, out var failureReason))
            {
                FailActiveTransition(failureReason);
                return;
            }

            activeTransitionId++;
            activeRoutine = StartCoroutine(WaitForTransitionCompletion(operation, activeTransitionId));
        }

        private bool TryBeginTransition(
            SceneTransitionRequest request,
            out AsyncOperation operation,
            out string failureReason)
        {
            try
            {
                var activeScene = SceneManager.GetActiveScene();
                if (saveLoad != null && activeScene.IsValid() &&
                    !saveLoad.TryRetainSceneState(activeScene.name, out failureReason))
                {
                    operation = null;
                    return false;
                }

                transitions.BeginTransition(request);
                operation = SceneManager.LoadSceneAsync(request.SceneName, request.LoadSceneMode);
                failureReason = operation == null
                    ? $"Failed to start scene load: {request.SceneName}"
                    : null;
                return operation != null;
            }
            catch (System.Exception exception)
            {
                operation = null;
                failureReason = $"Scene transition failed: {exception.Message}";
                return false;
            }
        }

        private IEnumerator WaitForTransitionCompletion(AsyncOperation operation, int transitionId)
        {
            // This yield guarantees activeRoutine is assigned before completion can clear it.
            yield return null;

            try
            {
                while (!operation.isDone)
                {
                    yield return null;
                }

                TryCompleteActiveTransition(out _);
            }
            finally
            {
                if (transitions != null && transitions.IsTransitioning)
                {
                    FailActiveTransition("Scene transition ended before completion.");
                }

                if (transitionId == activeTransitionId)
                {
                    activeRoutine = null;
                }
            }
        }

        private bool TryCompleteActiveTransition(out string failureReason)
        {
            try
            {
                CompleteActiveTransition();
                failureReason = null;
                return true;
            }
            catch (System.Exception exception)
            {
                failureReason = $"Post-load transition failed: {exception.Message}";
                FailActiveTransition(failureReason, true);
                return false;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (transitions == null ||
                !transitions.IsTransitioning ||
                scene.name != transitions.ActiveRequest.SceneName)
            {
                return;
            }

            TryCompleteActiveTransition(out _);
        }

        private void CompleteActiveTransition()
        {
            if (transitions == null || !transitions.IsTransitioning)
            {
                return;
            }

            var completedRequest = transitions.ActiveRequest;
            var spawnResolution = PlayerSpawnResolver.ResolveAndApply(completedRequest);
            LogSpawnResolution(spawnResolution);

            var restoredPreparedSave = saveLoad != null && saveLoad.HasPreparedRestoreFor(completedRequest);
            if (saveLoad != null &&
                !saveLoad.TryRestorePrepared(completedRequest, out _, out var restoreFailure))
            {
                FailActiveTransition(restoreFailure, true);
                HorrorGameContext.Active?.UiShell.Show(UiShellScreen.SaveLoadSlots);
                return;
            }

            if (saveLoad != null && !restoredPreparedSave &&
                !saveLoad.TryRestoreRetainedScene(completedRequest.SceneName, out _, out var retainedFailure))
            {
                FailActiveTransition(retainedFailure, true);
                return;
            }

            transitions.CompleteTransition();

            if (gameConfig != null && completedRequest.SceneName == gameConfig.MainMenuSceneName)
            {
                HorrorGameContext.Active?.UiShell.Show(UiShellScreen.MainMenu);
                return;
            }

            var context = HorrorGameContext.Active;
            context?.PauseFlow.RequestGameplayInputRestore();
            context?.PauseFlow.ApplyPendingGameplayInputRestore();
        }

        private void LogSpawnResolution(PlayerSpawnResolution resolution)
        {
            if (resolution.Kind == PlayerSpawnResolutionKind.NotRequested || resolution.Applied)
            {
                return;
            }

            HorrorGameContext.Active?.Logger.Warning(
                HorrorLogCategory.SceneFlow,
                $"Spawn '{resolution.RequestedSpawnId}' was not applied: {resolution.Kind}. " +
                "The scene-authored player pose remains in use.");
        }

        private void CancelActiveTransition(string reason)
        {
            var hadActiveTransition = activeRoutine != null || (transitions != null && transitions.IsTransitioning);
            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
                activeRoutine = null;
            }

            if (hadActiveTransition)
            {
                FailActiveTransition(reason);
            }
        }

        private void FailActiveTransition(string reason, bool returnToSafeMenu = false)
        {
            saveLoad?.ClearPendingRestore();
            if (transitions != null && transitions.IsTransitioning)
            {
                transitions.FailTransition(reason);
            }

            var context = HorrorGameContext.Active;
            if (context == null)
            {
                return;
            }

            if (returnToSafeMenu || SceneManager.GetActiveScene().name == gameConfig.MainMenuSceneName)
            {
                context.PauseFlow.ResumeWithoutRestoringGameplayInput();
                context.UiShell.Show(UiShellScreen.MainMenu);
                return;
            }

            context.PauseFlow.ResumeWithoutRestoringGameplayInput();
            context.UiShell.Hide();
            context.PauseFlow.RequestGameplayInputRestore();
            context.PauseFlow.ApplyPendingGameplayInputRestore();
        }
    }
}
