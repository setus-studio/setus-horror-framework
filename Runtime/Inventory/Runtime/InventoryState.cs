using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Setus.HorrorFramework.Inventory.Runtime
{
    [Serializable]
    public sealed class InventoryState
    {
        public InventoryState(IEnumerable<string> itemIds = null, string heldItemId = null)
        {
            this.itemIds = (itemIds ?? Array.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            this.heldItemId = heldItemId;
        }

        [SerializeField] private string[] itemIds = Array.Empty<string>();
        [SerializeField] private string heldItemId;

        public IReadOnlyList<string> ItemIds => itemIds;
        public string HeldItemId => heldItemId;
    }
}
