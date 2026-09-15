using System;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Narrative.Objectives;
using Setus.HorrorFramework.SaveProgression.SaveSlots;
using UnityEngine;

namespace Setus.HorrorFramework.Narrative.Scenario
{
    [DisallowMultipleComponent]
    public sealed class NarrativeScenarioAdapter : MonoBehaviour
    {
        [SerializeField] private NarrativeScenarioDefinition scenario;

        private HorrorGameContext context;
        private IDisposable restoreSubscription;

        private void Awake()
        {
            context = HorrorGameContext.Ensure();
        }

        private void OnEnable()
        {
            if (context == null)
            {
                context = HorrorGameContext.Ensure();
            }

            restoreSubscription?.Dispose();
            restoreSubscription = context.Events.Subscribe<SaveRestoreCompleted>(_ => ConfigureScenario());
            ConfigureScenario();
        }

        private void OnDisable()
        {
            restoreSubscription?.Dispose();
            restoreSubscription = null;
        }

        private void ConfigureScenario()
        {
            if (scenario == null)
            {
                Debug.LogError("Narrative scenario adapter requires a scenario definition.", this);
                return;
            }

            try
            {
                scenario.ValidateObjectiveGraphOrThrow();
                context.Narrative.InitializeScenario(scenario.ScenarioId);
                context.Narrative.ConfigurePhoneMessages(scenario.PhoneMessages);
                context.Objectives.ConfigureScenario(
                    scenario.ScenarioId,
                    scenario.Objectives,
                    scenario.InitialObjectiveId);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }
    }
}
