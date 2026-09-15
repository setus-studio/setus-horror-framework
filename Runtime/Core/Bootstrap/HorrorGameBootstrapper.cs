using System;
using Setus.HorrorFramework.Core.Config;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Debugging.Logging;
using Setus.HorrorFramework.UI.Settings.Persistence;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Setus.HorrorFramework.Core.Bootstrap
{
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class HorrorGameBootstrapper : MonoBehaviour
    {
        [SerializeField] private HorrorFrameworkConfig config;
        [SerializeField] private bool persistAcrossSceneLoads = true;

        private static HorrorGameBootstrapper activeInstance;
        private HorrorGameContext context;
        private InputAction debugToggleAction;

        public HorrorGameContext Context => context;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            activeInstance = null;
        }

        private void Awake()
        {
            Initialize();
        }

        private void Update()
        {
            if (context == null || debugToggleAction == null)
            {
                return;
            }

            if (debugToggleAction.WasPressedThisFrame())
            {
                context.Debug.Toggle();
            }
        }

        private void OnEnable()
        {
            debugToggleAction?.Enable();
        }

        private void OnDisable()
        {
            debugToggleAction?.Disable();
        }

        private void OnDestroy()
        {
            context?.RuntimeSettings.Flush();
            if (activeInstance == this)
            {
                activeInstance = null;
            }

            debugToggleAction?.Dispose();
            debugToggleAction = null;
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused)
            {
                context?.RuntimeSettings.Flush();
            }
        }

        private void OnApplicationQuit()
        {
            context?.RuntimeSettings.Flush();
        }

        public HorrorGameContext Initialize()
        {
            if (config == null)
            {
                throw new InvalidOperationException(
                    "HorrorGameBootstrapper requires an assigned HorrorFrameworkConfig.");
            }

            if (activeInstance != null && activeInstance != this)
            {
                context = activeInstance.context;
                context.Logger.Warning(
                    HorrorLogCategory.Core,
                    "Duplicate HorrorGameBootstrapper was rejected; the existing bootstrap authority remains active.");
                RejectDuplicateBootstrapper();
                return context;
            }

            context = HorrorGameContext.Initialize(
                config,
                PersistentUserSettingsStore.CreateDefault());
            activeInstance = this;
            if (Application.isPlaying && persistAcrossSceneLoads)
            {
                DontDestroyOnLoad(gameObject);
            }

            ConfigureDebugToggleAction();
            return context;
        }

        private void ConfigureDebugToggleAction()
        {
            debugToggleAction?.Dispose();
            debugToggleAction = null;

            if (!config.DebugHotkeyEnabled || config.DebugToggleKey == Key.None)
            {
                return;
            }

            debugToggleAction = new InputAction(
                "ToggleDebug",
                InputActionType.Button,
                $"<Keyboard>/{config.DebugToggleKey.ToString().ToLowerInvariant()}");
            debugToggleAction.Enable();
        }

        private void RejectDuplicateBootstrapper()
        {
            if (!Application.isPlaying)
            {
                enabled = false;
                return;
            }

            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
