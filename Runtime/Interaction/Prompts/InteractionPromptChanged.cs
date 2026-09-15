using Setus.HorrorFramework.Core.Events;
using Setus.HorrorFramework.Interaction.Interactors;

namespace Setus.HorrorFramework.Interaction.Prompts
{
    public readonly struct InteractionPromptChanged : IGameplayEvent
    {
        public InteractionPromptChanged(InteractionPrompt prompt, string targetName, float distance)
        {
            Prompt = prompt;
            TargetName = targetName;
            Distance = distance;
        }

        public InteractionPrompt Prompt { get; }
        public string TargetName { get; }
        public float Distance { get; }
    }
}
