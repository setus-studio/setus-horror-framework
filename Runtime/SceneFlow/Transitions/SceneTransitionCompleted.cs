namespace Setus.HorrorFramework.SceneFlow.Transitions
{
    public readonly struct SceneTransitionCompleted
    {
        public SceneTransitionCompleted(SceneTransitionRequest request)
        {
            Request = request;
        }

        public SceneTransitionRequest Request { get; }
    }
}
