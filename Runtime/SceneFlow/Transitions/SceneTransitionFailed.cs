namespace Setus.HorrorFramework.SceneFlow.Transitions
{
    public readonly struct SceneTransitionFailed
    {
        public SceneTransitionFailed(SceneTransitionRequest request, string reason)
        {
            Request = request;
            Reason = reason ?? string.Empty;
        }

        public SceneTransitionRequest Request { get; }
        public string Reason { get; }
    }
}
