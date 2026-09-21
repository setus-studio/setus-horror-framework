using System;
using System.Collections;
using System.Linq;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Debugging.Logging;
using Setus.HorrorFramework.Localization;
using Setus.HorrorFramework.Player.Controller;
using Setus.HorrorFramework.SaveProgression.Checkpoints;
using Setus.HorrorFramework.SaveProgression.Registry;
using Setus.HorrorFramework.SaveProgression.SaveSlots;
using Setus.HorrorFramework.SceneFlow.Transitions;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Setus.HorrorFramework.UI.Menus
{
    [DisallowMultipleComponent]
    public sealed class DebugSaveLoadPanel : MonoBehaviour
    {
        [SerializeField] private string checkpointId = "manual-save";
        [SerializeField] private Text statusText;
        [SerializeField] private Button quickSaveButton;
        [SerializeField] private Button quickLoadButton;

        private UiShellStateOwner uiShell;
        private UiShellScreen saveLoadSourceScreen = UiShellScreen.MainMenu;
        private SceneTransitionRunner transitionRunner;
        private SaveLoadCoordinator saveLoad;
        private LocalizedTextReference currentStatus;
        private AsyncOperationHandle<LocalizationSettings> localizationInitialization;
        private bool waitingForLocalization;
        private Coroutine pendingLocalizationRefresh;

        private void Awake()
        {
            ResolveButtons();
            ResolveTransitionRunner();
        }

        private void OnEnable()
        {
            var context = HorrorGameContext.Ensure();
            if (uiShell != null)
            {
                uiShell.Changed -= OnUiShellChanged;
            }

            uiShell = context.Services.GetRequired<UiShellStateOwner>();
            uiShell.Changed += OnUiShellChanged;
            if (saveLoad != null)
            {
                saveLoad.UserFailureChanged -= OnUserFailureChanged;
            }

            saveLoad = context.SaveLoad;
            saveLoad.UserFailureChanged += OnUserFailureChanged;
            if (LocalizationSettings.HasSettings)
            {
                LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
                LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            }

            RefreshPanelState(context);
            if (LocalizationSettings.HasSettings)
            {
                localizationInitialization = LocalizationSettings.InitializationOperation;
                if (!localizationInitialization.IsDone)
                {
                    waitingForLocalization = true;
                    localizationInitialization.Completed += OnLocalizationInitialized;
                }
            }
        }

        private void OnDisable()
        {
            if (uiShell != null)
            {
                uiShell.Changed -= OnUiShellChanged;
                uiShell = null;
            }

            if (saveLoad != null)
            {
                saveLoad.UserFailureChanged -= OnUserFailureChanged;
                saveLoad = null;
            }

            if (LocalizationSettings.HasSettings)
            {
                LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            }

            if (waitingForLocalization)
            {
                localizationInitialization.Completed -= OnLocalizationInitialized;
                waitingForLocalization = false;
            }

            if (pendingLocalizationRefresh != null)
            {
                StopCoroutine(pendingLocalizationRefresh);
                pendingLocalizationRefresh = null;
            }
        }

        public void QuickSave()
        {
            try
            {
                if (!CanQuickSave())
                {
                    UpdateStatus(new LocalizedTextReference(
                        FrameworkTextKeys.SaveUnavailableOutsideGameplay,
                        "Quick Save is unavailable outside gameplay."));
                    RefreshButtonState();
                    return;
                }

                var context = HorrorGameContext.Ensure();
                var playerPose = FindPlayerPose();
                var checkpoint = new CheckpointModel(
                    checkpointId,
                    SceneManager.GetActiveScene().name,
                    "debug",
                    playerPose);

                if (!context.SaveLoad.TryCaptureAndSave(
                        SingleSaveSlotContract.SlotId,
                        checkpoint,
                        out var result,
                        out _))
                {
                    ShowCurrentFailure(context.SaveLoad);
                    RefreshButtonState();
                    return;
                }

                LogReport(result.Report);
                UpdateStatus(new LocalizedTextReference(
                    FrameworkTextKeys.SaveStatusSaved,
                    "Game saved."));
            }
            catch (Exception exception)
            {
                HorrorGameContext.Active?.Logger.Error(
                    HorrorLogCategory.SaveProgression,
                    $"Save UI failed before capture: {exception}");
                UpdateStatus(SaveLoadUserFailure.SaveFailed().Message);
            }
        }

        public void QuickLoad()
        {
            if (!CanQuickLoad())
            {
                UpdateStatus(new LocalizedTextReference(
                    FrameworkTextKeys.LoadUnavailableFromPause,
                    "Quick Load is unavailable from the pause menu."));
                RefreshButtonState();
                return;
            }

            ResolveTransitionRunner();
            if (transitionRunner == null)
            {
                UpdateStatus(SaveLoadUserFailure.Unavailable().Message);
                return;
            }

            if (!transitionRunner.TryContinueFromSave(SingleSaveSlotContract.SlotId, out _))
            {
                ShowCurrentFailure(HorrorGameContext.Ensure().SaveLoad);
                RefreshButtonState();
                return;
            }

            UpdateStatus(new LocalizedTextReference(
                FrameworkTextKeys.SaveStatusLoading,
                "Loading saved game."));
        }

        private static PlayerPose? FindPlayerPose()
        {
            var providers = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            for (var i = 0; i < providers.Length; i++)
            {
                if (providers[i] is IPlayerPoseProvider poseProvider)
                {
                    return poseProvider.CurrentPose;
                }
            }

            return null;
        }

        private bool CanQuickSave()
        {
            return FindPlayerPose().HasValue;
        }

        private bool CanQuickLoad()
        {
            return saveLoadSourceScreen == UiShellScreen.MainMenu;
        }

        private void RefreshButtonState()
        {
            ResolveButtons();

            if (quickSaveButton != null)
            {
                var canQuickSave = CanQuickSave();
                quickSaveButton.interactable = canQuickSave;
                if (quickSaveButton.gameObject.activeSelf != canQuickSave)
                {
                    quickSaveButton.gameObject.SetActive(canQuickSave);
                }
            }

            if (quickLoadButton != null)
            {
                var canQuickLoad = CanQuickLoad();
                quickLoadButton.interactable = canQuickLoad;
                if (quickLoadButton.gameObject.activeSelf != canQuickLoad)
                {
                    quickLoadButton.gameObject.SetActive(canQuickLoad);
                }
            }
        }

        private void OnUiShellChanged(UiShellStateChanged changed)
        {
            if (changed.CurrentState.CurrentScreen == UiShellScreen.SaveLoadSlots)
            {
                if (changed.PreviousState.CurrentScreen != UiShellScreen.SaveLoadSlots)
                {
                    saveLoadSourceScreen = changed.PreviousState.CurrentScreen;
                }

                RefreshPanelState(HorrorGameContext.Ensure());
            }
        }

        private void RefreshPanelState(HorrorGameContext context)
        {
            RefreshButtonState();
            if (context.SaveLoad.CurrentUserFailure.HasValue)
            {
                UpdateStatus(context.SaveLoad.CurrentUserFailure.Value.Message);
                return;
            }

            if (context.SaveLoad.TryGetSlotFailure(SingleSaveSlotContract.SlotId, out var failure))
            {
                UpdateStatus(failure.Message);
                return;
            }

            UpdateStatus(new LocalizedTextReference(
                FrameworkTextKeys.SaveStatusReady,
                "A saved game is ready."));
        }

        private void ResolveButtons()
        {
            if (quickSaveButton != null && quickLoadButton != null)
            {
                return;
            }

            var buttons = GetComponentsInChildren<Button>(true);
            for (var i = 0; i < buttons.Length; i++)
            {
                if (quickSaveButton == null && buttons[i].name == "QuickSaveButton")
                {
                    quickSaveButton = buttons[i];
                }
                else if (quickLoadButton == null && buttons[i].name == "QuickLoadButton")
                {
                    quickLoadButton = buttons[i];
                }
            }
        }

        private void ResolveTransitionRunner()
        {
            if (transitionRunner == null)
            {
                transitionRunner = GetComponentInParent<SceneTransitionRunner>();
            }
        }

        private void OnUserFailureChanged(SaveLoadUserFailure? failure)
        {
            if (failure.HasValue)
            {
                UpdateStatus(failure.Value.Message);
                return;
            }

            RefreshPanelState(HorrorGameContext.Ensure());
        }

        private void OnLocaleChanged(Locale _)
        {
            ScheduleLocalizationRefresh();
        }

        private void OnLocalizationInitialized(AsyncOperationHandle<LocalizationSettings> _)
        {
            waitingForLocalization = false;
            ScheduleLocalizationRefresh();
        }

        private void ScheduleLocalizationRefresh()
        {
            if (!Application.isPlaying)
            {
                RenderCurrentStatus();
                return;
            }

            if (pendingLocalizationRefresh == null)
            {
                pendingLocalizationRefresh = StartCoroutine(RefreshStatusNextFrame());
            }
        }

        private IEnumerator RefreshStatusNextFrame()
        {
            yield return null;
            pendingLocalizationRefresh = null;
            if (isActiveAndEnabled)
            {
                RenderCurrentStatus();
            }
        }

        private void ShowCurrentFailure(SaveLoadCoordinator coordinator)
        {
            UpdateStatus(coordinator.CurrentUserFailure?.Message ?? SaveLoadUserFailure.Unavailable().Message);
        }

        private void UpdateStatus(LocalizedTextReference status)
        {
            currentStatus = status;
            RenderCurrentStatus();
        }

        private void RenderCurrentStatus()
        {
            var message = LocalizationTextResolver.Resolve(currentStatus);
            if (statusText != null)
            {
                statusText.text = message;
            }

            if (HorrorGameContext.Active?.Debug.IsEnabled == true)
            {
                Debug.Log(message, this);
            }
        }

        private static void LogReport(SaveRestoreReport report)
        {
            if (report == null || report.Issues.Count == 0)
            {
                return;
            }

            var warnings = report.Issues.Count(issue => issue.Severity == SaveRestoreIssueSeverity.Warning);
            var errors = report.Issues.Count(issue => issue.Severity == SaveRestoreIssueSeverity.Error);
            var firstIssue = report.Issues.FirstOrDefault();
            var issueDetail = firstIssue == null
                ? string.Empty
                : $" [{firstIssue.Code}: {firstIssue.StableId ?? firstIssue.Message}]";
            HorrorGameContext.Active?.Logger.Warning(
                HorrorLogCategory.SaveProgression,
                $"Save completed with warnings: {warnings}, errors: {errors}{issueDetail}");
        }

    }
}
