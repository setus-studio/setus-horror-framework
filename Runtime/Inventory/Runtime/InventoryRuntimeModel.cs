using System;
using System.Collections.Generic;
using Setus.HorrorFramework.Core.Events;
using Setus.HorrorFramework.Core.Services;

namespace Setus.HorrorFramework.Inventory.Runtime
{
    public sealed class InventoryRuntimeModel : RuntimeStateOwnerBase<InventoryState>, IRestoreStateValidator
    {
        private readonly IGameplayEventBus events;
        private readonly HashSet<string> itemIds = new HashSet<string>(StringComparer.Ordinal);
        private string heldItemId;

        public InventoryRuntimeModel(IGameplayEventBus events = null)
            : base("inventory.state")
        {
            this.events = events;
        }

        public InventoryState Current => CaptureState();

        public bool HasItem(string itemId)
        {
            return !string.IsNullOrWhiteSpace(itemId) && itemIds.Contains(itemId);
        }

        public bool AddItem(string itemId, bool holdItem = false)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return false;
            }

            var added = itemIds.Add(itemId);
            if (holdItem)
            {
                heldItemId = itemId;
            }

            if (added || holdItem)
            {
                PublishChanged();
            }

            return added;
        }

        public bool ConsumeItem(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId) || !itemIds.Remove(itemId))
            {
                return false;
            }

            if (string.Equals(heldItemId, itemId, StringComparison.Ordinal))
            {
                heldItemId = null;
            }

            PublishChanged();
            return true;
        }

        public void SetHeldItem(string itemId)
        {
            heldItemId = HasItem(itemId) ? itemId : null;
            PublishChanged();
        }

        public void Clear()
        {
            if (itemIds.Count == 0 && string.IsNullOrWhiteSpace(heldItemId))
            {
                return;
            }

            itemIds.Clear();
            heldItemId = null;
            PublishChanged();
        }

        public override InventoryState CaptureState()
        {
            return new InventoryState(itemIds, heldItemId);
        }

        public override void RestoreState(InventoryState state)
        {
            itemIds.Clear();
            heldItemId = null;

            if (state != null)
            {
                for (var i = 0; i < state.ItemIds.Count; i++)
                {
                    itemIds.Add(state.ItemIds[i]);
                }

                heldItemId = state.HeldItemId;
            }

            PublishChanged();
        }

        public RestoreStateValidationResult ValidateRestoreState(object state)
        {
            if (!(state is InventoryState inventoryState))
            {
                return RestoreStateValidationResult.Invalid(
                    $"Expected {nameof(InventoryState)}.");
            }

            var restoredItemIds = inventoryState.ItemIds;
            if (restoredItemIds == null)
            {
                return RestoreStateValidationResult.Invalid("Inventory item IDs must not be null.");
            }

            var uniqueItemIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < restoredItemIds.Count; i++)
            {
                var itemId = restoredItemIds[i];
                if (string.IsNullOrWhiteSpace(itemId))
                {
                    return RestoreStateValidationResult.Invalid("Inventory item IDs must not be empty.");
                }

                if (!uniqueItemIds.Add(itemId))
                {
                    return RestoreStateValidationResult.Invalid(
                        $"Inventory contains duplicate item ID '{itemId}'.");
                }
            }

            if (!string.IsNullOrEmpty(inventoryState.HeldItemId) &&
                (string.IsNullOrWhiteSpace(inventoryState.HeldItemId) ||
                 !uniqueItemIds.Contains(inventoryState.HeldItemId)))
            {
                return RestoreStateValidationResult.Invalid(
                    $"Held item ID '{inventoryState.HeldItemId}' is not present in the inventory.");
            }

            return RestoreStateValidationResult.Success;
        }

        private void PublishChanged()
        {
            events?.Publish(new InventoryChanged(CaptureState()));
        }
    }
}
