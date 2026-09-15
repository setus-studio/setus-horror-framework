using System;
using System.Collections.Generic;
using System.Linq;
using Setus.HorrorFramework.Atmosphere.Tension;
using Setus.HorrorFramework.Core.Events;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Debugging.Logging;
using Setus.HorrorFramework.SaveProgression.Registry;

namespace Setus.HorrorFramework.Atmosphere.Scares
{
    public sealed class ScareRuntimeModel : RuntimeStateOwnerBase<ScareRuntimeState>, IRestoreStateValidator, IRuntimeRollbackOwner
    {
        private readonly IGameplayEventBus events;
        private readonly TensionRuntimeModel tension;
        private readonly Dictionary<string, ScareDefinition> definitionsById =
            new Dictionary<string, ScareDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> definitionReferenceCounts =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, ScareLifecycleState> stateByScareId =
            new Dictionary<string, ScareLifecycleState>(StringComparer.Ordinal);
        private DefinitionRegistration configuredCatalog;

        public ScareRuntimeModel(IGameplayEventBus events, TensionRuntimeModel tension)
            : base(HorrorGlobalSaveStateRegistry.AtmosphereScareStateKey)
        {
            this.events = events;
            this.tension = tension;
        }

        public IReadOnlyList<ScareStateRecord> Scares => CaptureState().Scares;

        /// <summary>Replaces the global catalog registration without deleting durable scare history.</summary>
        public void Configure(IEnumerable<ScareDefinition> definitions)
        {
            var validated = ValidateDefinitionBatch(definitions);
            ValidateRegistrationCompatibility(validated, configuredCatalog);

            configuredCatalog?.Dispose();
            configuredCatalog = RegisterValidatedDefinitions(validated);
        }

        /// <summary>Registers scene-scoped definitions until the returned handle is disposed.</summary>
        public IDisposable RegisterDefinitions(IEnumerable<ScareDefinition> definitions)
        {
            var validated = ValidateDefinitionBatch(definitions);
            ValidateRegistrationCompatibility(validated, null);
            return RegisterValidatedDefinitions(validated);
        }

        private static ScareDefinition[] ValidateDefinitionBatch(IEnumerable<ScareDefinition> definitions)
        {
            var validated = (definitions ?? Enumerable.Empty<ScareDefinition>()).ToArray();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < validated.Length; i++)
            {
                var definition = validated[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.ScareId))
                {
                    throw new InvalidOperationException("Scare definitions require a non-empty ScareId.");
                }

                if (!ids.Add(definition.ScareId))
                {
                    throw new InvalidOperationException($"Duplicate scare ID: {definition.ScareId}");
                }
            }

            return validated;
        }

        private void ValidateRegistrationCompatibility(
            IReadOnlyList<ScareDefinition> definitions,
            DefinitionRegistration ignoredRegistration)
        {
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (!definitionsById.TryGetValue(definition.ScareId, out var registered) ||
                    ReferenceEquals(registered, definition))
                {
                    continue;
                }

                var activeReferences = definitionReferenceCounts[definition.ScareId];
                if (ignoredRegistration != null && ignoredRegistration.Contains(registered))
                {
                    activeReferences--;
                }

                if (activeReferences > 0)
                {
                    throw new InvalidOperationException(
                        $"Scare ID '{definition.ScareId}' is already registered with a different definition asset.");
                }
            }
        }

        private DefinitionRegistration RegisterValidatedDefinitions(ScareDefinition[] definitions)
        {
            for (var i = 0; i < definitions.Length; i++)
            {
                var definition = definitions[i];
                if (definitionsById.ContainsKey(definition.ScareId))
                {
                    definitionReferenceCounts[definition.ScareId]++;
                }
                else
                {
                    definitionsById.Add(definition.ScareId, definition);
                    definitionReferenceCounts.Add(definition.ScareId, 1);
                }

                if (!stateByScareId.ContainsKey(definition.ScareId))
                {
                    stateByScareId.Add(definition.ScareId, ScareLifecycleState.Armed);
                }
            }

            return new DefinitionRegistration(this, definitions);
        }

        private void UnregisterDefinitions(DefinitionRegistration registration)
        {
            var definitions = registration.Definitions;
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (!definitionReferenceCounts.TryGetValue(definition.ScareId, out var referenceCount))
                {
                    continue;
                }

                if (referenceCount > 1)
                {
                    definitionReferenceCounts[definition.ScareId] = referenceCount - 1;
                    continue;
                }

                ConsumeActive(definition.ScareId);
                definitionReferenceCounts.Remove(definition.ScareId);
                definitionsById.Remove(definition.ScareId);
            }
        }

        public bool TryGetDefinition(string scareId, out ScareDefinition definition)
        {
            return definitionsById.TryGetValue(scareId ?? string.Empty, out definition);
        }

        public bool TryGetState(string scareId, out ScareLifecycleState state)
        {
            return stateByScareId.TryGetValue(scareId ?? string.Empty, out state);
        }

        public bool TryTrigger(string scareId, bool debugTrigger = false)
        {
            var resolvedId = scareId ?? string.Empty;
            if (!definitionsById.TryGetValue(resolvedId, out var definition))
            {
                return false;
            }

            if (!stateByScareId.TryGetValue(resolvedId, out var current))
            {
                return false;
            }

            if (current != ScareLifecycleState.Armed)
            {
                return false;
            }

            SetState(resolvedId, ScareLifecycleState.Triggered, false);
            tension?.SetSourceIntensity(TensionSourceId(resolvedId), definition.TensionIntensity);
            SetState(resolvedId, ScareLifecycleState.Playing, false);
            PublishPowerState(definition, false);
            if (!string.IsNullOrWhiteSpace(definition.FallbackCueText))
            {
                events?.Publish(new AtmosphereSubtitleCueRequested(
                    definition.LocalizedFallbackSpeaker,
                    definition.LocalizedFallbackCue,
                    definition.FallbackCueDuration));
            }

            if (debugTrigger)
            {
                HorrorGameContext.Active?.Logger.Log(HorrorLogCategory.Atmosphere, $"Debug scare triggered: {resolvedId}");
            }

            return true;
        }

        public bool SetEnabled(string scareId, bool enabled)
        {
            if (!stateByScareId.TryGetValue(scareId ?? string.Empty, out var current) ||
                current == ScareLifecycleState.Consumed || current == ScareLifecycleState.Playing ||
                current == ScareLifecycleState.Triggered)
            {
                return false;
            }

            var target = enabled ? ScareLifecycleState.Armed : ScareLifecycleState.Disabled;
            if (current == target)
            {
                return false;
            }

            SetState(scareId, target, false);
            return true;
        }

        public bool Complete(string scareId)
        {
            return ConsumeActive(scareId);
        }

        // M7 uses a consume-on-interruption policy. This keeps semantic scare state in the
        // persistent model even when a scene-bound presentation can no longer finish its timer.
        public bool Interrupt(string scareId)
        {
            return ConsumeActive(scareId);
        }

        public int InterruptAllActive()
        {
            var activeIds = stateByScareId
                .Where(pair => IsActive(pair.Value))
                .Select(pair => pair.Key)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();

            var consumedCount = 0;
            for (var i = 0; i < activeIds.Length; i++)
            {
                if (ConsumeActive(activeIds[i]))
                {
                    consumedCount++;
                }
            }

            return consumedCount;
        }

        public bool ResetForDebug(string scareId)
        {
            if (!stateByScareId.ContainsKey(scareId ?? string.Empty))
            {
                return false;
            }

            tension?.ClearSource(TensionSourceId(scareId));
            if (TryGetDefinition(scareId, out var definition))
            {
                PublishPowerState(definition, true);
            }
            SetState(scareId, ScareLifecycleState.Armed, false);
            return true;
        }

        public void Reset()
        {
            var ids = stateByScareId.Keys.ToArray();
            for (var i = 0; i < ids.Length; i++)
            {
                tension?.ClearSource(TensionSourceId(ids[i]));
                if (TryGetDefinition(ids[i], out var definition))
                {
                    PublishPowerState(definition, true);
                }
                SetState(ids[i], ScareLifecycleState.Armed, false);
            }
        }

        public override ScareRuntimeState CaptureState()
        {
            return new ScareRuntimeState(stateByScareId.Select(pair => new ScareStateRecord(
                pair.Key,
                NormalizeForPersistence(pair.Value))));
        }

        public Action CaptureRollbackAction()
        {
            var rollbackState = new Dictionary<string, ScareLifecycleState>(
                stateByScareId,
                StringComparer.Ordinal);
            return () =>
            {
                stateByScareId.Clear();
                foreach (var pair in rollbackState)
                {
                    stateByScareId.Add(pair.Key, pair.Value);
                }
            };
        }

        public override void RestoreState(ScareRuntimeState state)
        {
            ClearOwnedTensionSources();
            stateByScareId.Clear();
            if (state?.Scares != null)
            {
                for (var i = 0; i < state.Scares.Count; i++)
                {
                    var record = state.Scares[i];
                    var normalized = NormalizeForPersistence(record.State);
                    stateByScareId[record.ScareId] = normalized;
                    if (TryGetDefinition(record.ScareId, out var definition))
                    {
                        PublishPowerState(definition, true);
                    }
                    events?.Publish(new ScareStateChanged(record.ScareId, ScareLifecycleState.Restored, true));
                    events?.Publish(new ScareStateChanged(record.ScareId, normalized, true));
                }
            }

            foreach (var definition in definitionsById.Values)
            {
                if (!stateByScareId.ContainsKey(definition.ScareId))
                {
                    stateByScareId.Add(definition.ScareId, ScareLifecycleState.Armed);
                }
            }
        }

        private void ClearOwnedTensionSources()
        {
            if (tension == null)
            {
                return;
            }

            var scareIds = stateByScareId.Keys.ToArray();
            for (var i = 0; i < scareIds.Length; i++)
            {
                tension.ClearSource(TensionSourceId(scareIds[i]));
            }
        }

        public RestoreStateValidationResult ValidateRestoreState(object state)
        {
            if (!(state is ScareRuntimeState scareState) || scareState.Scares == null)
            {
                return RestoreStateValidationResult.Invalid($"Expected {nameof(ScareRuntimeState)} with scare records.");
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < scareState.Scares.Count; i++)
            {
                var record = scareState.Scares[i];
                if (record == null || string.IsNullOrWhiteSpace(record.ScareId) ||
                    !Enum.IsDefined(typeof(ScareLifecycleState), record.State) || !ids.Add(record.ScareId))
                {
                    return RestoreStateValidationResult.Invalid("Scare state contains an invalid or duplicate record.");
                }

            }

            return RestoreStateValidationResult.Success;
        }

        private void SetState(string scareId, ScareLifecycleState state, bool restored)
        {
            stateByScareId[scareId] = state;
            var context = HorrorGameContext.Active;
            if (context?.Debug.IsEnabled == true)
            {
                context.Logger.Log(
                    HorrorLogCategory.Atmosphere,
                    $"Scare={scareId}, state={state}, restored={restored}");
            }
            events?.Publish(new ScareStateChanged(scareId, state, restored));
        }

        private static ScareLifecycleState NormalizeForPersistence(ScareLifecycleState state)
        {
            return state == ScareLifecycleState.Triggered || state == ScareLifecycleState.Playing ||
                   state == ScareLifecycleState.Restored
                ? ScareLifecycleState.Consumed
                : state;
        }

        private bool ConsumeActive(string scareId)
        {
            if (!stateByScareId.TryGetValue(scareId ?? string.Empty, out var current) || !IsActive(current))
            {
                return false;
            }

            tension?.ClearSource(TensionSourceId(scareId));
            if (TryGetDefinition(scareId, out var definition))
            {
                PublishPowerState(definition, true);
            }

            SetState(scareId, ScareLifecycleState.Consumed, false);
            return true;
        }

        private static bool IsActive(ScareLifecycleState state)
        {
            return state == ScareLifecycleState.Triggered || state == ScareLifecycleState.Playing;
        }

        private static string TensionSourceId(string scareId)
        {
            return $"scare:{scareId}";
        }

        private void PublishPowerState(ScareDefinition definition, bool isPowered)
        {
            if (!string.IsNullOrWhiteSpace(definition.PowerStateId))
            {
                events?.Publish(new ScarePowerStateChanged(definition.PowerStateId, isPowered));
            }
        }

        private sealed class DefinitionRegistration : IDisposable
        {
            private ScareRuntimeModel owner;

            public DefinitionRegistration(ScareRuntimeModel owner, ScareDefinition[] definitions)
            {
                this.owner = owner;
                Definitions = definitions;
            }

            public IReadOnlyList<ScareDefinition> Definitions { get; }

            public bool Contains(ScareDefinition definition)
            {
                for (var i = 0; i < Definitions.Count; i++)
                {
                    if (ReferenceEquals(Definitions[i], definition))
                    {
                        return true;
                    }
                }

                return false;
            }

            public void Dispose()
            {
                var currentOwner = owner;
                if (currentOwner == null)
                {
                    return;
                }

                owner = null;
                currentOwner.UnregisterDefinitions(this);
            }
        }
    }
}
