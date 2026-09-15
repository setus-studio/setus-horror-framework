using UnityEngine;

namespace Setus.HorrorFramework.Interaction.Interactors
{
    public interface IInteractable
    {
        Transform PromptAnchor { get; }
        InteractionPrompt GetPrompt(InteractionContext context);
        InteractionResult Interact(InteractionContext context);
    }
}
