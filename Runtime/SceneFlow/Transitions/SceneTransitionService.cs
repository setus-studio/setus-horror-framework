using System;
using Setus.HorrorFramework.Core.Events;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Debugging.Logging;

namespace Setus.HorrorFramework.SceneFlow.Transitions
{
    public sealed class SceneTransitionService :
        RuntimeStateOwnerBase<SceneTransitionState>,
        ISceneTransitionService
    {
        private readonly IGameplayEventBus events;
        private readonly IHorrorLogger logger;
        private SceneTransitionState state = SceneTransitionState.Idle;
        private SceneTransitionRequest activeRequest;

        public SceneTransitionService(IGameplayEventBus events = null, IHorrorLogger logger = null)
            : base("scene-flow.transition")
        {
            this.events = events;
            this.logger = logger;
        }

        public SceneTransitionState CurrentState => state;
        public bool IsTransitioning => state.IsLoading;
        public SceneTransitionRequest ActiveRequest => activeRequest;

        public void BeginTransition(SceneTransitionRequest request)
        {
            if (!request.IsValid)
            {
                throw new ArgumentException("Scene transition requires a non-empty scene name.", nameof(request));
            }

            if (IsTransitioning)
            {
                throw new InvalidOperationException(
                    $"Scene transition already in progress: {state.TargetSceneName}");
            }

            activeRequest = request;
            state = new SceneTransitionState(
                SceneTransitionPhase.Loading,
                request.SceneName,
                request.CheckpointId,
                request.SpawnId,
                request.ShowLoadingScreen);

            logger?.Log(HorrorLogCategory.SceneFlow, $"Loading scene '{request.SceneName}'.");
            events?.Publish(new SceneTransitionStarted(request));
        }

        public void CompleteTransition()
        {
            if (!IsTransitioning)
            {
                return;
            }

            var completed = activeRequest;
            state = SceneTransitionState.Idle;
            activeRequest = default;

            logger?.Log(HorrorLogCategory.SceneFlow, $"Loaded scene '{completed.SceneName}'.");
            events?.Publish(new SceneTransitionCompleted(completed));
        }

        public void FailTransition(string reason)
        {
            var failed = activeRequest;
            state = SceneTransitionState.Idle;
            activeRequest = default;

            logger?.Error(HorrorLogCategory.SceneFlow, reason);
            events?.Publish(new SceneTransitionFailed(failed, reason));
        }

        public override SceneTransitionState CaptureState()
        {
            return state;
        }

        public override void RestoreState(SceneTransitionState restoredState)
        {
            state = restoredState != null && !restoredState.IsLoading
                ? restoredState
                : SceneTransitionState.Idle;
            activeRequest = default;
        }
    }
}
