using NUnit.Framework;
using System.Reflection;
using Setus.HorrorFramework.Core.Events;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Localization;
using Setus.HorrorFramework.Player.Controller;
using Setus.HorrorFramework.Player.Input;
using Setus.HorrorFramework.Player.State;
using Setus.HorrorFramework.SaveProgression.Registry;
using Setus.HorrorFramework.SaveProgression.SaveSlots;
using Setus.HorrorFramework.SceneFlow.Loading;
using Setus.HorrorFramework.SceneFlow.Transitions;
using Setus.HorrorFramework.UI.Menus;
using UnityEngine;
using UnityEngine.UI;

namespace Setus.HorrorFramework.Tests.EditMode.Core
{
    public sealed class SceneFlowAndUiShellTests
    {
        private float originalTimeScale;
        private bool originalAudioPause;

        [SetUp]
        public void SetUp()
        {
            originalTimeScale = Time.timeScale;
            originalAudioPause = AudioListener.pause;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            HorrorGameContext.ShutdownActive();
        }

        [TearDown]
        public void TearDown()
        {
            HorrorGameContext.ShutdownActive();
            Time.timeScale = originalTimeScale;
            AudioListener.pause = originalAudioPause;
        }

        [Test]
        public void SceneTransitionService_PublishesEventsAndClearsLoadingState()
        {
            var events = new GameplayEventBus();
            var service = new SceneTransitionService(events);
            var startedCount = 0;
            var completedCount = 0;
            using (events.Subscribe<SceneTransitionStarted>(_ => startedCount++))
            using (events.Subscribe<SceneTransitionCompleted>(_ => completedCount++))
            {
                service.BeginTransition(new SceneTransitionRequest("Gameplay", "checkpoint-a", "spawn-a"));

                Assert.That(service.IsTransitioning, Is.True);
                Assert.That(service.CurrentState.TargetSceneName, Is.EqualTo("Gameplay"));
                Assert.That(service.CurrentState.CheckpointId, Is.EqualTo("checkpoint-a"));
                Assert.That(startedCount, Is.EqualTo(1));

                service.CompleteTransition();
            }

            Assert.That(service.IsTransitioning, Is.False);
            Assert.That(completedCount, Is.EqualTo(1));
        }

        [Test]
        public void SceneTransitionService_RejectsEmptySceneName()
        {
            var service = new SceneTransitionService();

            Assert.Throws<System.ArgumentException>(() =>
                service.BeginTransition(new SceneTransitionRequest("")));
        }

        [Test]
        public void PauseFlow_PauseAndResumeRestoresInputTimeAndAudio()
        {
            var events = new GameplayEventBus();
            var uiShell = new UiShellStateOwner(events);
            var inputModes = new CursorInputModeCoordinator();
            var pause = new PauseFlowController(uiShell, inputModes, events);
            var player = new FakePlayerControlStateTarget();
            pause.SetPlayerTarget(player);

            pause.Pause();

            Assert.That(Time.timeScale, Is.EqualTo(0f));
            Assert.That(AudioListener.pause, Is.True);
            Assert.That(uiShell.Current.CurrentScreen, Is.EqualTo(UiShellScreen.PauseMenu));
            Assert.That(inputModes.CurrentMode, Is.EqualTo(CursorInputMode.Menu));
            Assert.That(player.CurrentControlState, Is.EqualTo(PlayerControlState.Normal));

            pause.Resume();

            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(AudioListener.pause, Is.False);
            Assert.That(uiShell.Current.CurrentScreen, Is.EqualTo(UiShellScreen.Hidden));
            Assert.That(inputModes.CurrentMode, Is.EqualTo(CursorInputMode.Gameplay));
            Assert.That(player.CurrentControlState, Is.EqualTo(PlayerControlState.Normal));
        }

        [TestCase(PlayerControlState.Inspecting)]
        [TestCase(PlayerControlState.Cutscene)]
        [TestCase(PlayerControlState.Disabled)]
        public void PauseFlow_PauseAndResumePreservesExistingPlayerState(PlayerControlState initialState)
        {
            var events = new GameplayEventBus();
            var uiShell = new UiShellStateOwner(events);
            var inputModes = new CursorInputModeCoordinator();
            var pause = new PauseFlowController(uiShell, inputModes, events);
            var player = new FakePlayerControlStateTarget(initialState);
            pause.SetPlayerTarget(player);

            pause.Pause();
            pause.Resume();

            Assert.That(player.CurrentControlState, Is.EqualTo(initialState));
            Assert.That(inputModes.CurrentPolicy.PlayerControlState, Is.EqualTo(initialState));
        }

        [Test]
        public void PauseFlow_DeferredUiResumePreservesExistingPlayerState()
        {
            var events = new GameplayEventBus();
            var uiShell = new UiShellStateOwner(events);
            var inputModes = new CursorInputModeCoordinator();
            var pause = new PauseFlowController(uiShell, inputModes, events);
            var player = new FakePlayerControlStateTarget(PlayerControlState.Inspecting);
            pause.SetPlayerTarget(player);

            pause.Pause();
            pause.ResumeWithoutRestoringGameplayInput();
            pause.RequestGameplayInputRestore();
            pause.ApplyPendingGameplayInputRestore();

            Assert.That(player.CurrentControlState, Is.EqualTo(PlayerControlState.Inspecting));
            Assert.That(inputModes.CurrentPolicy.PlayerControlState, Is.EqualTo(PlayerControlState.Inspecting));
        }

        [Test]
        public void PauseFlow_HasGameplayTargetOnlyAfterPlayerRegistration()
        {
            var events = new GameplayEventBus();
            var uiShell = new UiShellStateOwner(events);
            var inputModes = new CursorInputModeCoordinator();
            var pause = new PauseFlowController(uiShell, inputModes, events);
            var player = new FakePlayerControlStateTarget();

            Assert.That(pause.HasGameplayTarget, Is.False);

            pause.SetPlayerTarget(player);

            Assert.That(pause.HasGameplayTarget, Is.True);

            pause.ClearPlayerTarget(player);

            Assert.That(pause.HasGameplayTarget, Is.False);
        }

        [Test]
        public void PauseFlow_MainMenuAlwaysAppliesMenuInputMode()
        {
            var events = new GameplayEventBus();
            var uiShell = new UiShellStateOwner(events);
            var inputModes = new CursorInputModeCoordinator();
            var pause = new PauseFlowController(uiShell, inputModes, events);

            uiShell.Show(UiShellScreen.MainMenu);

            Assert.That(inputModes.CurrentMode, Is.EqualTo(CursorInputMode.Menu));
            Assert.That(inputModes.CurrentPolicy.CursorLockMode, Is.EqualTo(CursorLockMode.None));
            Assert.That(inputModes.CurrentPolicy.CursorVisible, Is.True);
        }

        [Test]
        public void PauseFlow_PlayerRegisteredWhileMainMenuVisibleUsesMenuState()
        {
            var events = new GameplayEventBus();
            var uiShell = new UiShellStateOwner(events);
            var inputModes = new CursorInputModeCoordinator();
            var pause = new PauseFlowController(uiShell, inputModes, events);
            var player = new FakePlayerControlStateTarget();
            uiShell.Show(UiShellScreen.MainMenu);

            pause.SetPlayerTarget(player);

            Assert.That(player.CurrentControlState, Is.EqualTo(PlayerControlState.Menu));
            Assert.That(inputModes.CurrentMode, Is.EqualTo(CursorInputMode.Menu));
        }

        [Test]
        public void PauseFlow_LoadingScreenAppliesLoadingInputMode()
        {
            var events = new GameplayEventBus();
            var uiShell = new UiShellStateOwner(events);
            var inputModes = new CursorInputModeCoordinator();
            _ = new PauseFlowController(uiShell, inputModes, events);

            uiShell.Show(UiShellScreen.Loading);

            Assert.That(inputModes.CurrentMode, Is.EqualTo(CursorInputMode.Loading));
            Assert.That(inputModes.CurrentPolicy.CursorLockMode, Is.EqualTo(CursorLockMode.None));
            Assert.That(inputModes.CurrentPolicy.CursorVisible, Is.False);
        }

        [Test]
        public void PauseFlow_LoadingLocksGameplayInputWithoutMutatingSemanticState()
        {
            var events = new GameplayEventBus();
            var uiShell = new UiShellStateOwner(events);
            var inputModes = new CursorInputModeCoordinator();
            var pause = new PauseFlowController(uiShell, inputModes, events);
            var player = new FakePlayerControlStateTarget(PlayerControlState.Cutscene);
            pause.SetPlayerTarget(player);

            uiShell.Show(UiShellScreen.Loading);

            Assert.That(player.CurrentControlState, Is.EqualTo(PlayerControlState.Cutscene));
            Assert.That(player.IsGameplayInputLocked, Is.True);

            uiShell.Hide();

            Assert.That(player.IsGameplayInputLocked, Is.False);
        }

        [Test]
        public void DebugSaveLoadPanel_QuickSaveDoesNotCaptureOutsideGameplay()
        {
            var context = HorrorGameContext.Ensure();
            var panel = new GameObject("debug-save-panel");
            var debugSaveLoad = panel.AddComponent<DebugSaveLoadPanel>();

            debugSaveLoad.QuickSave();

            Assert.That(context.InMemorySaveSlots.HasSlot(SingleSaveSlotContract.SlotId), Is.False);

            Object.DestroyImmediate(panel);
        }

        [Test]
        public void ProductionUiExposesOneCanonicalSlotWithoutFakeSelectionState()
        {
            Assert.That(SingleSaveSlotContract.SlotId.Value, Is.EqualTo("primary"));
            Assert.That(typeof(UiShellState).GetProperty("SelectedSlotId"), Is.Null);
            Assert.That(typeof(UiShellStateOwner).GetMethod("SelectSlot"), Is.Null);
            Assert.That(typeof(StandaloneGameConfig).GetProperty("DefaultSaveSlotId"), Is.Null);
        }

        [Test]
        public void DebugSaveLoadPanel_QuickLoadIsUnavailableFromPauseMenu()
        {
            var context = HorrorGameContext.Ensure();
            var panel = new GameObject("debug-save-panel");
            var statusObject = new GameObject("Status");
            statusObject.transform.SetParent(panel.transform);
            var statusText = statusObject.AddComponent<Text>();
            var debugSaveLoad = panel.AddComponent<DebugSaveLoadPanel>();
            typeof(DebugSaveLoadPanel)
                .GetField("statusText", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(debugSaveLoad, statusText);
            typeof(DebugSaveLoadPanel)
                .GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(debugSaveLoad, null);

            context.UiShell.Show(UiShellScreen.PauseMenu);
            context.UiShell.Show(UiShellScreen.SaveLoadSlots);

            debugSaveLoad.QuickLoad();

            var status = (LocalizedTextReference)typeof(DebugSaveLoadPanel)
                .GetField("currentStatus", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(debugSaveLoad);
            Assert.That(status.EntryKey, Is.EqualTo(FrameworkTextKeys.LoadUnavailableFromPause));
            Assert.That(status.FallbackText, Is.EqualTo("Quick Load is unavailable from the pause menu."));
            Assert.That(statusText.text, Is.EqualTo(LocalizationTextResolver.Resolve(status)));

            Object.DestroyImmediate(panel);
        }

        [Test]
        public void SaveLoadFailureChannelShowsSafeFallbackWithoutTechnicalDiagnostic()
        {
            var context = HorrorGameContext.Ensure();
            var panel = new GameObject("save-feedback-panel");
            var statusObject = new GameObject("Status");
            statusObject.transform.SetParent(panel.transform);
            var statusText = statusObject.AddComponent<Text>();
            var saveLoadPanel = panel.AddComponent<DebugSaveLoadPanel>();
            typeof(DebugSaveLoadPanel)
                .GetField("statusText", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(saveLoadPanel, statusText);
            typeof(DebugSaveLoadPanel)
                .GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(saveLoadPanel, null);

            Assert.That(
                context.SaveLoad.TryPrepareContinue(
                    SingleSaveSlotContract.SlotId,
                    out _,
                    out var technicalDiagnostic),
                Is.False);

            Assert.That(technicalDiagnostic, Does.Contain("manifest"));
            var status = (LocalizedTextReference)typeof(DebugSaveLoadPanel)
                .GetField("currentStatus", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(saveLoadPanel);
            Assert.That(status.EntryKey, Is.EqualTo(FrameworkTextKeys.SaveErrorUnavailable));
            Assert.That(status.FallbackText, Is.EqualTo("Save and load are currently unavailable."));
            Assert.That(statusText.text, Is.EqualTo(LocalizationTextResolver.Resolve(status)));
            Assert.That(statusText.text, Does.Not.Contain("manifest"));

            Object.DestroyImmediate(panel);
        }

        [Test]
        public void RuntimeSettings_AreCapturedAndRestoredThroughSaveOwner()
        {
            var context = HorrorGameContext.Ensure();
            context.Saveables.RegisterGlobal(new TestPlayerPoseOwner());
            context.RuntimeSettings.SetMasterVolume(0.42f);
            context.RuntimeSettings.SetSubtitlesEnabled(false);

            var snapshot = context.Saves.Capture(new SaveSlotId("slot-a"));
            context.RuntimeSettings.SetMasterVolume(1f);
            context.RuntimeSettings.SetSubtitlesEnabled(true);

            context.Saves.Restore(snapshot);

            Assert.That(context.RuntimeSettings.Current.MasterVolume, Is.EqualTo(0.42f).Within(0.001f));
            Assert.That(context.RuntimeSettings.Current.SubtitlesEnabled, Is.False);
        }

        [Test]
        public void ContextSceneTransitionEvents_ToggleLoadingUiShell()
        {
            var context = HorrorGameContext.Ensure();

            context.Transitions.BeginTransition(new SceneTransitionRequest("Gameplay"));

            Assert.That(context.UiShell.Current.CurrentScreen, Is.EqualTo(UiShellScreen.Loading));

            context.Transitions.CompleteTransition();

            Assert.That(context.UiShell.Current.CurrentScreen, Is.EqualTo(UiShellScreen.Hidden));
        }

        [Test]
        public void FailedTransitionClearsLoadingUiShellState()
        {
            var context = HorrorGameContext.Ensure();

            context.Transitions.BeginTransition(new SceneTransitionRequest("Gameplay"));
            context.Transitions.FailTransition("Simulated transition failure.");

            Assert.That(context.Transitions.IsTransitioning, Is.False);
            Assert.That(context.UiShell.Current.CurrentScreen, Is.EqualTo(UiShellScreen.Hidden));
        }

        [Test]
        public void DuplicateUiShellIsDisabledBeforeItCanCreateAnEventSystem()
        {
            var primaryRoot = new GameObject("Primary Ui Shell");
            var duplicateRoot = new GameObject("Duplicate Ui Shell");
            try
            {
                var primaryLifetime = primaryRoot.AddComponent<UiShellLifetime>();
                InvokePrivateMethod(primaryLifetime, "Awake");
                var primaryEventSystem = primaryRoot.AddComponent<UiShellEventSystem>();
                InvokePrivateMethod(primaryEventSystem, "Awake");

                var duplicateLifetime = duplicateRoot.AddComponent<UiShellLifetime>();
                InvokePrivateMethod(duplicateLifetime, "Awake");
                var duplicateEventSystem = duplicateRoot.AddComponent<UiShellEventSystem>();
                InvokePrivateMethod(duplicateEventSystem, "Awake");

                Assert.That(primaryLifetime.IsPrimary, Is.True);
                Assert.That(duplicateRoot.activeSelf, Is.False);
                Assert.That(duplicateEventSystem.enabled, Is.False);
                Assert.That(
                    duplicateRoot.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true),
                    Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(primaryRoot);
                Object.DestroyImmediate(duplicateRoot);
            }
        }

        [Test]
        public void LegacyMenuCommandRouterIsNotPresentInTheRuntimeAssembly()
        {
            Assert.That(
                typeof(UiShellCommandAdapter).Assembly.GetType(
                    "Setus.HorrorFramework.UI.Menus.MenuCommandRouter"),
                Is.Null);
        }

        [Test]
        public void ContextResetForNewGameSessionClearsInventoryWithoutClearingDebugSaveSlot()
        {
            var context = HorrorGameContext.Ensure();
            context.Saveables.RegisterGlobal(new TestPlayerPoseOwner());
            context.Inventory.AddItem("m5.debug.key", true);
            var snapshot = context.Saves.Capture(new SaveSlotId("debug"));
            context.InMemorySaveSlots.Save(snapshot);

            context.ResetForNewGameSession();

            Assert.That(context.Inventory.HasItem("m5.debug.key"), Is.False);
            Assert.That(context.Inventory.Current.HeldItemId, Is.Null);
            Assert.That(context.InMemorySaveSlots.HasSlot(new SaveSlotId("debug")), Is.True);
        }

        private sealed class FakePlayerControlStateTarget : IPlayerControlStateTarget, IPlayerInputLockTarget
        {
            public FakePlayerControlStateTarget(PlayerControlState initialState = PlayerControlState.Normal)
            {
                CurrentControlState = initialState;
            }

            public PlayerControlState CurrentControlState { get; private set; }
            public bool IsGameplayInputLocked { get; private set; }

            public bool SetControlState(PlayerControlState state)
            {
                var changed = CurrentControlState != state;
                CurrentControlState = state;
                return changed;
            }

            public void SetGameplayInputLocked(bool isLocked)
            {
                IsGameplayInputLocked = isLocked;
            }
        }

        private sealed class TestPlayerPoseOwner : RuntimeStateOwnerBase<PlayerPose>
        {
            private PlayerPose pose = new PlayerPose(
                Vector3.zero,
                Quaternion.identity,
                0f,
                PlayerControlState.Normal);

            public TestPlayerPoseOwner()
                : base(HorrorGlobalSaveStateRegistry.PlayerPoseStateKey)
            {
            }

            public override PlayerPose CaptureState()
            {
                return pose;
            }

            public override void RestoreState(PlayerPose state)
            {
                pose = state;
            }
        }

        private static void InvokePrivateMethod(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Expected private method '{methodName}'.");
            method.Invoke(target, null);
        }
    }
}
