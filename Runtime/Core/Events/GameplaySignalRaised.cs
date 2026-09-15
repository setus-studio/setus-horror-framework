namespace Setus.HorrorFramework.Core.Events
{
    public readonly struct GameplaySignalRaised : IGameplayEvent
    {
        public GameplaySignalRaised(string signalId, GameplaySignalChannel channel)
        {
            SignalId = signalId;
            Channel = channel;
        }

        public string SignalId { get; }
        public GameplaySignalChannel Channel { get; }
    }
}
