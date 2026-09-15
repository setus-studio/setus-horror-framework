using Setus.HorrorFramework.Core.Services;

namespace Setus.HorrorFramework.SceneFlow.Transitions
{
    public interface ISceneTransitionService : IRuntimeStateOwner<SceneTransitionState>
    {
        SceneTransitionState CurrentState { get; }
        bool IsTransitioning { get; }
        SceneTransitionRequest ActiveRequest { get; }
        void BeginTransition(SceneTransitionRequest request);
        void CompleteTransition();
        void FailTransition(string reason);
    }
}
