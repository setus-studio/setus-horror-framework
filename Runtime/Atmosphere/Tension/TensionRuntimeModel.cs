using System;
using System.Collections.Generic;
using System.Linq;
using Setus.HorrorFramework.Atmosphere.Scares;
using Setus.HorrorFramework.Core.Events;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Debugging.Logging;
using Setus.HorrorFramework.SaveProgression.Registry;

namespace Setus.HorrorFramework.Atmosphere.Tension
{
    public sealed class TensionRuntimeModel : RuntimeStateOwnerBase<TensionRuntimeState>, IRestoreStateValidator, IRuntimeRollbackOwner
    {
        private readonly IGameplayEventBus events;
        private readonly Dictionary<string, float> intensityBySource =
            new Dictionary<string, float>(StringComparer.Ordinal);

        public TensionRuntimeModel(IGameplayEventBus events = null)
            : base(HorrorGlobalSaveStateRegistry.AtmosphereTensionStateKey)
        {
            this.events = events;
        }

        public float CurrentIntensity { get; private set; }
        public IReadOnlyList<TensionSourceState> Sources => CaptureState().Sources;

        public bool SetSourceIntensity(string sourceId, float intensity)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                return false;
            }

            var clamped = Math.Max(0f, Math.Min(1f, intensity));
            if (intensityBySource.TryGetValue(sourceId, out var existing) && Math.Abs(existing - clamped) < 0.0001f)
            {
                return false;
            }

            intensityBySource[sourceId] = clamped;
            Recalculate(sourceId);
            return true;
        }

        public bool ClearSource(string sourceId)
        {
            if (string.IsNullOrWhiteSpace(sourceId) || !intensityBySource.Remove(sourceId))
            {
                return false;
            }

            Recalculate(sourceId);
            return true;
        }

        public void Reset()
        {
            if (intensityBySource.Count == 0 && CurrentIntensity <= 0f)
            {
                return;
            }

            intensityBySource.Clear();
            Recalculate(string.Empty);
        }

        public override TensionRuntimeState CaptureState()
        {
            // Active scare presentation is persisted as Consumed, so its temporary tension must not
            // survive independently and recreate an incomplete atmosphere after restore.
            return new TensionRuntimeState(intensityBySource
                .Where(pair => !pair.Key.StartsWith("scare:", StringComparison.Ordinal))
                .Select(pair => new TensionSourceState(pair.Key, pair.Value)));
        }

        public Action CaptureRollbackAction()
        {
            var rollbackSources = new Dictionary<string, float>(intensityBySource, StringComparer.Ordinal);
            var rollbackIntensity = CurrentIntensity;
            return () =>
            {
                intensityBySource.Clear();
                foreach (var pair in rollbackSources)
                {
                    intensityBySource.Add(pair.Key, pair.Value);
                }

                CurrentIntensity = rollbackIntensity;
            };
        }

        public override void RestoreState(TensionRuntimeState state)
        {
            intensityBySource.Clear();
            if (state?.Sources != null)
            {
                for (var i = 0; i < state.Sources.Count; i++)
                {
                    var source = state.Sources[i];
                    intensityBySource[source.SourceId] = source.Intensity;
                }
            }

            Recalculate(string.Empty);
        }

        public RestoreStateValidationResult ValidateRestoreState(object state)
        {
            if (!(state is TensionRuntimeState tensionState) || tensionState.Sources == null)
            {
                return RestoreStateValidationResult.Invalid($"Expected {nameof(TensionRuntimeState)} with sources.");
            }

            var sourceIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < tensionState.Sources.Count; i++)
            {
                var source = tensionState.Sources[i];
                if (source == null || string.IsNullOrWhiteSpace(source.SourceId) ||
                    float.IsNaN(source.Intensity) || float.IsInfinity(source.Intensity) ||
                    source.Intensity < 0f || source.Intensity > 1f || !sourceIds.Add(source.SourceId))
                {
                    return RestoreStateValidationResult.Invalid("Tension state contains an invalid or duplicate source.");
                }
            }

            return RestoreStateValidationResult.Success;
        }

        private void Recalculate(string sourceId)
        {
            CurrentIntensity = intensityBySource.Count == 0 ? 0f : intensityBySource.Values.Max();
            var context = HorrorGameContext.Active;
            if (context?.Debug.IsEnabled == true)
            {
                context.Logger.Log(
                    HorrorLogCategory.Atmosphere,
                    $"Tension={CurrentIntensity:0.00}, source={sourceId ?? string.Empty}");
            }
            events?.Publish(new TensionChanged(CurrentIntensity, sourceId));
        }
    }
}
