using Setus.HorrorFramework.Core.Events;

namespace Setus.HorrorFramework.Interaction.Interactors
{
    public readonly struct InteractionPerformed : IGameplayEvent
    {
        public InteractionPerformed(string targetName, InteractionType interactionType, InteractionResult result)
        {
            TargetName = targetName;
            InteractionType = interactionType;
            Result = result;
        }

        public string TargetName { get; }
        public InteractionType InteractionType { get; }
        public InteractionResult Result { get; }
    }
}
