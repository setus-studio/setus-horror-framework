using Setus.HorrorFramework.Core.Config;
using Setus.HorrorFramework.Core.Events;
using Setus.HorrorFramework.Debugging.Logging;
using Setus.HorrorFramework.Inventory.Runtime;
using Setus.HorrorFramework.Narrative.Objectives;
using Setus.HorrorFramework.Narrative.Scenario;
using Setus.HorrorFramework.Atmosphere.Scares;
using Setus.HorrorFramework.Atmosphere.Tension;
using Setus.HorrorFramework.Player.Input;
using Setus.HorrorFramework.SaveProgression.Migration;
using Setus.HorrorFramework.SaveProgression.Registry;
using Setus.HorrorFramework.SaveProgression.SaveSlots;
using Setus.HorrorFramework.SaveProgression.Serialization;
using Setus.HorrorFramework.SaveProgression.StableIds;
using Setus.HorrorFramework.SceneFlow.Transitions;
using Setus.HorrorFramework.UI.Menus;
using Setus.HorrorFramework.UI.Settings;
using Setus.HorrorFramework.UI.Settings.Persistence;
using UnityEngine;

namespace Setus.HorrorFramework.Core.Services
{
    public sealed class HorrorGameContext
    {
        private HorrorGameContext(HorrorFrameworkConfig config, IUserSettingsStore userSettingsStore)
        {
            Config = config;
            Services = new HorrorServiceRegistry();
            Events = new GameplayEventBus();
            Debug = new HorrorDebugManager(config != null && config.DebugEnabledByDefault);
            Logger = new HorrorLogger(config != null ? config.EnabledLogCategories : HorrorLogCategory.Core);
            SaveStateTypes = HorrorSaveStateTypeRegistry.CreateDefault();
            GlobalSaveStates = HorrorGlobalSaveStateRegistry.CreateDefault();
            Saveables = new SaveableRegistry(SaveStateTypes, GlobalSaveStates);
            SaveMigrations = new SaveMigrationPipeline();
            HorrorSaveMigrations.RegisterDefaults(SaveMigrations);
            Saves = new SaveGameOwner(Saveables, SaveMigrations, Events, Logger);
            InMemorySaveSlots = new InMemorySaveSlotStore();
            PersistentSaveSlots = new PersistentSaveSlotStore(null, SaveStateTypes);
            SaveLoad = new SaveLoadCoordinator(Saves, PersistentSaveSlots, Logger);
            Transitions = new SceneTransitionService(Events, Logger);
            RuntimeSettings = new RuntimeSettingsModel(userSettingsStore);
            Inventory = new InventoryRuntimeModel(Events);
            Objectives = new ObjectiveRuntimeModel(Events);
            Narrative = new NarrativeRuntimeModel(Events);
            Tension = new TensionRuntimeModel(Events);
            Scares = new ScareRuntimeModel(Events, Tension);
            UiShell = new UiShellStateOwner(Events);
            InputModes = new CursorInputModeCoordinator();
            PauseFlow = new PauseFlowController(UiShell, InputModes, Events);
            Saveables.RegisterGlobal(RuntimeSettings);
            Saveables.RegisterGlobal(Inventory);
            Saveables.RegisterGlobal(Objectives);
            Saveables.RegisterGlobal(Narrative);
            Saveables.RegisterGlobal(Tension);
            Saveables.RegisterGlobal(Scares);

            Services.Register<IGameplayEventBus>(Events);
            Services.Register(Events);
            Services.Register(Debug);
            Services.Register<IHorrorLogger>(Logger);
            Services.Register(Logger);
            Services.Register(SaveStateTypes);
            Services.Register(GlobalSaveStates);
            Services.Register(Saveables);
            Services.Register(SaveMigrations);
            Services.Register(Saves);
            Services.Register(InMemorySaveSlots);
            Services.Register<ISaveSlotStore>(PersistentSaveSlots);
            Services.Register(PersistentSaveSlots);
            Services.Register(SaveLoad);
            Services.Register<ISceneTransitionService>(Transitions);
            Services.Register(Transitions);
            Services.Register(RuntimeSettings);
            Services.Register(Inventory);
            Services.Register(Objectives);
            Services.Register(Narrative);
            Services.Register(Tension);
            Services.Register(Scares);
            Services.Register(UiShell);
            Services.Register(InputModes);
            Services.Register(PauseFlow);

            Events.Subscribe<SceneTransitionStarted>(OnSceneTransitionStarted);
            Events.Subscribe<SaveRestoreCompleted>(_ => RuntimeSettings.CommitPendingLegacyImport());
            Events.Subscribe<SceneTransitionCompleted>(_ => UiShell.Hide());
            Events.Subscribe<SceneTransitionFailed>(_ => UiShell.Hide());
        }

        public static HorrorGameContext Active { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Active = null;
        }

        public HorrorFrameworkConfig Config { get; }
        public HorrorServiceRegistry Services { get; }
        public GameplayEventBus Events { get; }
        public HorrorDebugManager Debug { get; }
        public HorrorLogger Logger { get; }
        public SaveStateTypeRegistry SaveStateTypes { get; }
        public GlobalSaveStateRegistry GlobalSaveStates { get; }
        public SaveableRegistry Saveables { get; }
        public SaveMigrationPipeline SaveMigrations { get; }
        public SaveGameOwner Saves { get; }
        public InMemorySaveSlotStore InMemorySaveSlots { get; }
        public PersistentSaveSlotStore PersistentSaveSlots { get; }
        public SaveLoadCoordinator SaveLoad { get; }
        public SceneTransitionService Transitions { get; }
        public RuntimeSettingsModel RuntimeSettings { get; }
        public InventoryRuntimeModel Inventory { get; }
        public ObjectiveRuntimeModel Objectives { get; }
        public NarrativeRuntimeModel Narrative { get; }
        public TensionRuntimeModel Tension { get; }
        public ScareRuntimeModel Scares { get; }
        public UiShellStateOwner UiShell { get; }
        public CursorInputModeCoordinator InputModes { get; }
        public PauseFlowController PauseFlow { get; }

        public static HorrorGameContext Initialize(HorrorFrameworkConfig config)
        {
            return Initialize(config, null);
        }

        public static HorrorGameContext Initialize(
            HorrorFrameworkConfig config,
            IUserSettingsStore userSettingsStore)
        {
            if (config == null)
            {
                throw new System.ArgumentNullException(
                    nameof(config),
                    "HorrorGameContext requires HorrorFrameworkConfig during runtime bootstrap.");
            }

            if (Active != null)
            {
                if (Active.Config != config)
                {
                    throw new System.InvalidOperationException(
                        "HorrorGameContext was already initialized with a different HorrorFrameworkConfig.");
                }

                return Active;
            }

            Active = new HorrorGameContext(config, userSettingsStore);
            return Active;
        }

        public static HorrorGameContext Ensure()
        {
            if (Active != null)
            {
                return Active;
            }

            if (Application.isPlaying)
            {
                throw new System.InvalidOperationException(
                    "HorrorGameContext is unavailable because HorrorGameBootstrapper has not initialized it.");
            }

            // EditMode tests may construct isolated framework services without a scene bootstrap.
            Active = new HorrorGameContext(null, null);
            return Active;
        }

        public static void ShutdownActive()
        {
            if (Active == null)
            {
                return;
            }

            Active.PauseFlow.Resume();
            Active.RuntimeSettings.Flush();
            Active.Events.Clear();
            Active.Services.Clear();
            Active = null;
        }

        public void ResetForNewGameSession()
        {
            SaveLoad.ResetSessionState();
            PauseFlow.Resume();
            UiShell.Hide();
            Inventory.Clear();
            Objectives.Reset();
            Narrative.Reset();
            Tension.Reset();
            Scares.Reset();
        }

        public void ConfigureSaveProgression(StableIdManifest manifest)
        {
            SaveLoad.Configure(manifest);
        }

        private void OnSceneTransitionStarted(SceneTransitionStarted started)
        {
            if (started.Request.LoadSceneMode == UnityEngine.SceneManagement.LoadSceneMode.Single)
            {
                Scares.InterruptAllActive();
            }

            if (started.Request.ShowLoadingScreen)
            {
                UiShell.Show(UiShellScreen.Loading);
            }
        }
    }
}
