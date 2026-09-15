namespace Setus.HorrorFramework.SceneFlow.Transitions
{
    public readonly struct SceneTransitionStarted
    {
        public SceneTransitionStarted(SceneTransitionRequest request)
        {
            Request = request;
        }

        public SceneTransitionRequest Request { get; }
    }
}
