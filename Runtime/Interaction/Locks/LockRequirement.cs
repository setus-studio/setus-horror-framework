using System;
using Setus.HorrorFramework.Inventory.Items;
using Setus.HorrorFramework.Inventory.Runtime;
using UnityEngine;

namespace Setus.HorrorFramework.Interaction.Locks
{
    [Serializable]
    public sealed class LockRequirement
    {
        [SerializeField] private InventoryItemDefinition requiredItem;
        [SerializeField] private string requiredItemId;
        [SerializeField] private bool consumeItemOnUnlock;

        public string RequiredItemId =>
            requiredItem != null && !string.IsNullOrWhiteSpace(requiredItem.ItemId)
                ? requiredItem.ItemId
                : requiredItemId;

        public bool ConsumeItemOnUnlock => consumeItemOnUnlock;
        public bool HasRequirement => !string.IsNullOrWhiteSpace(RequiredItemId);

        public bool CanUnlock(InventoryRuntimeModel inventory)
        {
            return !HasRequirement || (inventory != null && inventory.HasItem(RequiredItemId));
        }

        public void ApplyUnlockCost(InventoryRuntimeModel inventory)
        {
            if (consumeItemOnUnlock && HasRequirement)
            {
                inventory?.ConsumeItem(RequiredItemId);
            }
        }
    }
}
