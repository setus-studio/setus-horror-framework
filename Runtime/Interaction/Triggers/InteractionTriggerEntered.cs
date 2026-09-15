using Setus.HorrorFramework.Core.Events;

namespace Setus.HorrorFramework.Interaction.Triggers
{
    public readonly struct InteractionTriggerEntered : IGameplayEvent
    {
        public InteractionTriggerEntered(string triggerId, string otherName)
        {
            TriggerId = triggerId;
            OtherName = otherName;
        }

        public string TriggerId { get; }
        public string OtherName { get; }
    }
}
