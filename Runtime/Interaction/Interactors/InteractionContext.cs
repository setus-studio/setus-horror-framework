using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Inventory.Runtime;
using Setus.HorrorFramework.Narrative.Scenario;
using UnityEngine;

namespace Setus.HorrorFramework.Interaction.Interactors
{
    public readonly struct InteractionContext
    {
        public InteractionContext(GameObject interactor, HorrorGameContext gameContext)
        {
            Interactor = interactor;
            GameContext = gameContext;
        }

        public GameObject Interactor { get; }
        public HorrorGameContext GameContext { get; }
        public InventoryRuntimeModel Inventory => GameContext?.Inventory;
        public NarrativeRuntimeModel Narrative => GameContext?.Narrative;
    }
}
